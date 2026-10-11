using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using HomeFocus.Shared;

namespace HomeFocus.Agent;

internal sealed class FirewallManager
{
    string? lastSignature;
    const string Group = "HomeFocus";

    public async Task ReconcileAsync(HomeFocusConfig config, IReadOnlyList<string> activePrograms, CancellationToken ct)
    {
        var signature = config.Enabled + "|" + config.AllowLocalNetwork + "|" + string.Join("|", activePrograms.OrderBy(x => x));
        if (signature == lastSignature) return;

        if (!config.Enabled)
        {
            await RunPowerShellAsync(RestoreScript(), ct);
            lastSignature = signature;
            return;
        }

        await RunPowerShellAsync(EnableScript(config.AllowLocalNetwork, activePrograms), ct);
        lastSignature = signature;
    }

    static string Q(string value) => "'" + value.Replace("'", "''") + "'";

    static string EnableScript(bool allowLan, IReadOnlyList<string> programs)
    {
        var backup = Q(HomeFocusPaths.FirewallBackupPath);
        var agent = Q(Path.Combine(HomeFocusPaths.InstallDir, "HomeFocus.Agent.exe"));
        var sb = new StringBuilder();
        sb.AppendLine("$ErrorActionPreference='Stop'");
        sb.AppendLine($"if (!(Test-Path {backup})) {{ Get-NetFirewallProfile | Select-Object Name,DefaultOutboundAction | ConvertTo-Json -Compress | Set-Content -Encoding UTF8 {backup} }}");
        sb.AppendLine($"Get-NetFirewallRule -Group {Q(Group)} -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue");
        sb.AppendLine("Set-NetFirewallProfile -Profile Domain,Private,Public -DefaultOutboundAction Block");
        sb.AppendLine($"New-NetFirewallRule -DisplayName 'HomeFocus agent web' -Group {Q(Group)} -Direction Outbound -Action Allow -Program {agent} -Protocol TCP -RemotePort 80,443 -Profile Any | Out-Null");
        sb.AppendLine($"New-NetFirewallRule -DisplayName 'HomeFocus DNS UDP' -Group {Q(Group)} -Direction Outbound -Action Allow -Protocol UDP -RemotePort 53 -Profile Any | Out-Null");
        sb.AppendLine($"New-NetFirewallRule -DisplayName 'HomeFocus DNS TCP' -Group {Q(Group)} -Direction Outbound -Action Allow -Protocol TCP -RemotePort 53 -Profile Any | Out-Null");
        sb.AppendLine($"New-NetFirewallRule -DisplayName 'HomeFocus DHCP' -Group {Q(Group)} -Direction Outbound -Action Allow -Protocol UDP -LocalPort 68 -RemotePort 67 -Profile Any | Out-Null");
        sb.AppendLine($"New-NetFirewallRule -DisplayName 'HomeFocus NTP' -Group {Q(Group)} -Direction Outbound -Action Allow -Protocol UDP -RemotePort 123 -Profile Any | Out-Null");
        if (allowLan)
            sb.AppendLine($"New-NetFirewallRule -DisplayName 'HomeFocus local network' -Group {Q(Group)} -Direction Outbound -Action Allow -RemoteAddress LocalSubnet -Profile Any | Out-Null");
        var i = 0;
        foreach (var p in programs.Distinct(StringComparer.OrdinalIgnoreCase))
            sb.AppendLine($"New-NetFirewallRule -DisplayName 'HomeFocus reward app {++i}' -Group {Q(Group)} -Direction Outbound -Action Allow -Program {Q(p)} -Profile Any | Out-Null");
        return sb.ToString();
    }

    static string RestoreScript()
    {
        var backup = Q(HomeFocusPaths.FirewallBackupPath);
        return $@"
$ErrorActionPreference='Stop'
Get-NetFirewallRule -Group {Q(Group)} -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue
if (Test-Path {backup}) {{
  $items = Get-Content -Raw {backup} | ConvertFrom-Json
  foreach ($item in @($items)) {{ Set-NetFirewallProfile -Name $item.Name -DefaultOutboundAction $item.DefaultOutboundAction }}
}}
";
    }

    static async Task RunPowerShellAsync(string script, CancellationToken ct)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Could not start PowerShell.");
        var errorTask = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync(ct);
        var error = await errorTask;
        if (p.ExitCode != 0) throw new InvalidOperationException("Firewall update failed: " + error.Trim());
    }
}
