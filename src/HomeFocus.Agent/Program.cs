using HomeFocus.Shared;

namespace HomeFocus.Agent;

internal static class Program
{
    static async Task Main(string[] args)
    {
        Directory.CreateDirectory(HomeFocusPaths.ProgramDataDir);
        Directory.CreateDirectory(HomeFocusPaths.UpdateDir);

        var store = new PolicyStore();
        var engine = new PolicyEngine(store);
        var firewall = new FirewallManager();
        var proxy = new ProxyServer(engine);
        var updater = new UpdateManager(store);
        var commands = new CommandServer(store, engine, firewall, updater);

        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write("Fatal: " + e.ExceptionObject);
        Log.Write("Agent starting.");

        await firewall.ReconcileAsync(store.Config, engine.ActiveProgramGrants(), CancellationToken.None);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        var jobs = new[]
        {
            proxy.RunAsync(cts.Token),
            commands.RunAsync(cts.Token),
            ReconcileLoop(store, engine, firewall, updater, cts.Token)
        };
        await Task.WhenAll(jobs);
    }

    static async Task ReconcileLoop(PolicyStore store, PolicyEngine engine, FirewallManager firewall, UpdateManager updater, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                engine.PruneExpired();
                await firewall.ReconcileAsync(store.Config, engine.ActiveProgramGrants(), ct);
                await updater.CheckIfDueAsync(ct);
            }
            catch (Exception ex) { Log.Write("Loop: " + ex.Message); }
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }
}

internal static class Log
{
    static readonly object Gate = new();
    public static void Write(string text)
    {
        lock (Gate)
        {
            Directory.CreateDirectory(HomeFocusPaths.ProgramDataDir);
            File.AppendAllText(HomeFocusPaths.LogPath, $"{DateTimeOffset.Now:O} {text}{Environment.NewLine}");
        }
    }
}
