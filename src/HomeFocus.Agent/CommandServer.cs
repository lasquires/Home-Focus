using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using HomeFocus.Shared;

namespace HomeFocus.Agent;

internal sealed class CommandServer
{
    readonly PolicyStore store;
    readonly PolicyEngine engine;
    readonly FirewallManager firewall;
    readonly UpdateManager updater;
    readonly TcpListener listener = new(IPAddress.Loopback, 48191);

    public CommandServer(PolicyStore store, PolicyEngine engine, FirewallManager firewall, UpdateManager updater)
        => (this.store, this.engine, this.firewall, this.updater) = (store, engine, firewall, updater);

    public async Task RunAsync(CancellationToken ct)
    {
        listener.Start(20);
        Log.Write("Command API listening on 127.0.0.1:48191");
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleAsync(client, ct), ct);
            }
        }
        finally { listener.Stop(); }
    }

    async Task HandleAsync(TcpClient client, CancellationToken ct)
    {
        using var connectedClient = client;
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, false, 8192, leaveOpen: true);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), 8192, leaveOpen: true) { AutoFlush = true };
        AgentResponse response;
        try
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) return;
            var request = JsonSerializer.Deserialize<AgentRequest>(line, JsonDefaults.WireOptions) ?? new AgentRequest();
            response = await DispatchAsync(request, ct);
        }
        catch (Exception ex) { response = new AgentResponse { Ok = false, Error = ex.Message }; }
        await writer.WriteLineAsync(JsonSerializer.Serialize(response, JsonDefaults.WireOptions));
    }

    async Task<AgentResponse> DispatchAsync(AgentRequest req, CancellationToken ct)
    {
        switch (req.Type)
        {
            case "status":
                return Ok(status: engine.Status());
            case "initializePin":
                if (!string.IsNullOrWhiteSpace(store.Config.ParentPinHash)) return Fail("A parent PIN already exists.");
                if (!ValidPin(req.Pin)) return Fail("PIN must be 4–12 digits.");
                var created = PinHasher.Create(req.Pin!);
                store.Config.ParentPinSalt = created.Salt;
                store.Config.ParentPinHash = created.Hash;
                store.SaveConfig();
                return Ok();
            case "verifyPin":
                return ParentAuthorized(req.Pin) ? Ok() : Fail("Incorrect PIN.");
            case "getConfig":
                return ParentAuthorized(req.Pin) ? Ok(config: store.Config) : Fail("Incorrect PIN.");
            case "saveConfig":
                if (!ParentAuthorized(req.Pin)) return Fail("Incorrect PIN.");
                if (req.Config is null) return Fail("Missing configuration.");
                ValidateConfig(req.Config);
                // The PIN verifier remains agent-owned unless a dedicated change-PIN command is used.
                req.Config.ParentPinSalt = store.Config.ParentPinSalt;
                req.Config.ParentPinHash = store.Config.ParentPinHash;
                store.ReplaceConfig(req.Config);
                await firewall.ReconcileAsync(store.Config, engine.ActiveProgramGrants(), ct);
                return Ok(status: engine.Status());
            case "changePin":
                if (!ParentAuthorized(req.Pin)) return Fail("Incorrect current PIN.");
                // New PIN is carried in TaskId to keep the wire model tiny.
                if (!ValidPin(req.TaskId)) return Fail("New PIN must be 4–12 digits.");
                var changed = PinHasher.Create(req.TaskId!);
                store.Config.ParentPinSalt = changed.Salt;
                store.Config.ParentPinHash = changed.Hash;
                store.SaveConfig();
                return Ok();
            case "completeTask":
                if (string.IsNullOrWhiteSpace(req.TaskId)) return Fail("Missing task.");
                var grant = engine.CompleteTask(req.TaskId);
                await firewall.ReconcileAsync(store.Config, engine.ActiveProgramGrants(), ct);
                return Ok(grant: grant, status: engine.Status());
            case "endGrant":
                if (string.IsNullOrWhiteSpace(req.GrantId)) return Fail("Missing grant.");
                engine.EndGrant(req.GrantId);
                await firewall.ReconcileAsync(store.Config, engine.ActiveProgramGrants(), ct);
                return Ok(status: engine.Status());
            case "checkUpdate":
                if (!ParentAuthorized(req.Pin)) return Fail("Incorrect PIN.");
                await updater.CheckNowAsync(ct);
                return Ok(status: engine.Status());
            default:
                return Fail("Unknown request.");
        }
    }

    bool ParentAuthorized(string? pin) => !string.IsNullOrWhiteSpace(pin) &&
        PinHasher.Verify(pin, store.Config.ParentPinSalt, store.Config.ParentPinHash);
    static bool ValidPin(string? pin) => pin is { Length: >= 4 and <= 12 } && pin.All(char.IsDigit);

    static void ValidateConfig(HomeFocusConfig c)
    {
        if (c.Tasks.Any(t => t.RewardMinutes < 1 || t.RewardMinutes > 1440)) throw new InvalidOperationException("Task reward must be 1–1440 minutes.");
        if (c.Tasks.Any(t => t.IceClicks < 1 || t.IceClicks > 100000)) throw new InvalidOperationException("Ice clicks must be 1–100000.");
        if (c.Tasks.Any(t => t.PassagesRequired < 1 || t.PassagesRequired > 100)) throw new InvalidOperationException("Passages required must be 1–100.");
        foreach (var d in c.AlwaysAllowedDomains.Concat(c.RewardGroups.SelectMany(g => g.Domains)))
            if (string.IsNullOrWhiteSpace(DomainMatcher.Normalize(d))) throw new InvalidOperationException("Empty/invalid domain entry.");
    }

    static AgentResponse Ok(AgentStatus? status = null, HomeFocusConfig? config = null, AccessGrant? grant = null)
        => new() { Ok = true, Status = status, Config = config, Grant = grant };
    static AgentResponse Fail(string error) => new() { Ok = false, Error = error };
}
