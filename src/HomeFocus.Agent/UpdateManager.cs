using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HomeFocus.Shared;

namespace HomeFocus.Agent;

internal sealed class UpdateManager
{
    readonly PolicyStore store;
    readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(45) };
    bool checking;

    public UpdateManager(PolicyStore store)
    {
        this.store = store;
        http.DefaultRequestHeaders.UserAgent.ParseAdd("HomeFocus-Windows/1.0");
    }

    public async Task CheckIfDueAsync(CancellationToken ct)
    {
        var u = store.Config.Updates;
        if (!u.Enabled || checking || u.ManifestUrl.Contains("OWNER/REPO", StringComparison.OrdinalIgnoreCase)) return;
        var last = store.State.LastUpdateCheck;
        if (last.HasValue && DateTimeOffset.Now - last.Value < TimeSpan.FromHours(Math.Max(1, u.CheckEveryHours))) return;
        await CheckNowAsync(ct);
    }

    public async Task CheckNowAsync(CancellationToken ct)
    {
        if (checking) return;
        checking = true;
        try
        {
            store.MutateState(s => { s.LastUpdateCheck = DateTimeOffset.Now; s.LastUpdateError = null; });
            var manifestText = await http.GetStringAsync(store.Config.Updates.ManifestUrl, ct);
            var manifest = JsonSerializer.Deserialize<UpdateManifest>(manifestText, JsonDefaults.Options)
                           ?? throw new InvalidOperationException("Invalid update manifest.");
            var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
            if (!Version.TryParse(manifest.Version, out var next)) throw new InvalidOperationException("Manifest version is invalid.");
            if (next <= current)
            {
                store.MutateState(s => s.AvailableVersion = null);
                return;
            }

            store.MutateState(s => s.AvailableVersion = manifest.Version);
            if (!store.Config.Updates.AutoInstall) return;

            Directory.CreateDirectory(HomeFocusPaths.UpdateDir);
            var package = Path.Combine(HomeFocusPaths.UpdateDir, $"HomeFocus-{manifest.Version}.zip");
            var bytes = await http.GetByteArrayAsync(manifest.PackageUrl, ct);
            var digest = Convert.ToHexString(SHA256.HashData(bytes));
            if (!digest.Equals(manifest.Sha256.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Downloaded update failed SHA-256 verification.");
            await File.WriteAllBytesAsync(package, bytes, ct);

            var updaterInstalled = Path.Combine(HomeFocusPaths.InstallDir, "HomeFocus.Updater.exe");
            if (!File.Exists(updaterInstalled)) throw new FileNotFoundException("Updater executable is missing.");
            var runner = Path.Combine(HomeFocusPaths.UpdateDir, "HomeFocus.Updater.Runner.exe");
            File.Copy(updaterInstalled, runner, true);
            Process.Start(new ProcessStartInfo
            {
                FileName = runner,
                Arguments = $"--package \"{package}\" --sha256 {manifest.Sha256} --installDir \"{HomeFocusPaths.InstallDir}\"",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            await Task.Delay(300);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            store.MutateState(s => s.LastUpdateError = ex.Message);
            Log.Write("Update: " + ex.Message);
        }
        finally { checking = false; }
    }
}
