using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace HomeFocus.Updater;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            var package = Arg(args, "--package") ?? throw new ArgumentException("Missing --package.");
            var sha256 = Arg(args, "--sha256") ?? throw new ArgumentException("Missing --sha256.");
            var installDir = Arg(args, "--installDir") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "HomeFocus");
            if (!File.Exists(package)) throw new FileNotFoundException("Update package missing.", package);

            var digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(package)));
            if (!digest.Equals(sha256.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Update SHA-256 verification failed.");

            foreach (var p in Process.GetProcessesByName("HomeFocus")) { try { p.CloseMainWindow(); p.WaitForExit(2500); if (!p.HasExited) p.Kill(true); } catch { } }
            foreach (var p in Process.GetProcessesByName("HomeFocus.Agent")) { try { p.Kill(true); p.WaitForExit(5000); } catch { } }

            Thread.Sleep(1200);
            var staging = Path.Combine(Path.GetTempPath(), "HomeFocusUpdate-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            ZipFile.ExtractToDirectory(package, staging, true);
            var payload = Directory.Exists(Path.Combine(staging, "HomeFocus")) ? Path.Combine(staging, "HomeFocus") : staging;
            var required = new[] { "HomeFocus.exe", "HomeFocus.Agent.exe", "HomeFocus.Updater.exe" };
            if (required.Any(x => !File.Exists(Path.Combine(payload, x)))) throw new InvalidOperationException("Update package is incomplete.");

            Directory.CreateDirectory(installDir);
            foreach (var file in Directory.GetFiles(payload, "*", SearchOption.TopDirectoryOnly))
                File.Copy(file, Path.Combine(installDir, Path.GetFileName(file)), true);

            Run("schtasks.exe", "/Run /TN \"HomeFocus Agent\"");
            Thread.Sleep(1000);
            Process.Start(new ProcessStartInfo { FileName = Path.Combine(installDir, "HomeFocus.exe"), UseShellExecute = true });
            try { Directory.Delete(staging, true); } catch { }
            return 0;
        }
        catch (Exception ex)
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "HomeFocus");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "HomeFocus.log"), $"{DateTimeOffset.Now:O} Updater: {ex}\r\n");
            }
            catch { }
            return 1;
        }
    }

    static string? Arg(string[] args, string name)
    {
        var i = Array.FindIndex(args, x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    static void Run(string file, string arguments)
    {
        using var p = Process.Start(new ProcessStartInfo { FileName = file, Arguments = arguments, UseShellExecute = false, CreateNoWindow = true });
        p?.WaitForExit(5000);
    }
}
