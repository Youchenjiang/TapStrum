using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace Tapster.Launcher;

internal static class Program
{
    private static readonly string AppVersion = typeof(Program).Assembly.GetName().Version switch
    {
        { } v when v.Major > 0 => $"{v.Major}.{v.Minor}.{v.Build}",
        _ => "1.3.0"
    };

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    private const int SW_RESTORE = 9;
    private const int ASFW_ANY = -1;
    private const string WakeEventName = @"Local\TapStrum_Wake_Event";

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appDir = Path.Combine(localAppData, "TapStrum", $"app-v{AppVersion}");
            string targetExe = ResolveTargetExe(appDir);
            string hashFile = Path.Combine(appDir, ".payload_hash");

            var assembly = Assembly.GetExecutingAssembly();
            using Stream? stream = assembly.GetManifestResourceStream("payload.zip")
                ?? assembly.GetManifestResourceStream("Tapster.Launcher.payload.zip");

            string currentHash = ComputePayloadHash(stream);

            if (NeedsExtraction(targetExe, hashFile, currentHash))
            {
                PerformExtraction(appDir, stream, hashFile, currentHash);
                targetExe = ResolveTargetExe(appDir);
            }
            else if (ActivateExistingInstance(appDir))
            {
                return 0;
            }

            if (!File.Exists(targetExe))
            {
                MessageBoxW(IntPtr.Zero, "Failed to locate TapStrum.Fluent.exe after extraction.", "TapStrum Error", 0x10);
                return 1;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = targetExe,
                Arguments = string.Join(" ", args),
                WorkingDirectory = appDir,
                UseShellExecute = true
            };

            Process.Start(startInfo);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBoxW(IntPtr.Zero, $"TapStrum Launcher encountered an error:\n{ex.Message}", "TapStrum Error", 0x10);
            return 1;
        }
    }

    private static string ResolveTargetExe(string appDir)
    {
        string targetExe = Path.Combine(appDir, "TapStrum.Fluent.exe");
        if (File.Exists(targetExe))
        {
            return targetExe;
        }

        string legacyExe = Path.Combine(appDir, "Tapster.Fluent.exe");
        return File.Exists(legacyExe) ? legacyExe : targetExe;
    }

    private static string ComputePayloadHash(Stream? stream)
    {
        if (stream == null)
        {
            return string.Empty;
        }

        using var sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(stream);
        stream.Position = 0;
        return Convert.ToHexString(hashBytes);
    }

    private static bool NeedsExtraction(string targetExe, string hashFile, string currentHash)
    {
        if (!File.Exists(targetExe) || !File.Exists(hashFile))
        {
            return true;
        }

        try
        {
            return !string.Equals(File.ReadAllText(hashFile).Trim(), currentHash, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }

    private static void PerformExtraction(string appDir, Stream? stream, string hashFile, string currentHash)
    {
        KillExistingInstances(appDir);
        ExtractPayload(appDir, stream);

        if (!string.IsNullOrEmpty(currentHash))
        {
            try { File.WriteAllText(hashFile, currentHash); } catch { }
        }
    }

    private static void KillExistingInstances(string appDir)
    {
        try
        {
            var processes = Process.GetProcessesByName("TapStrum.Fluent")
                .Concat(Process.GetProcessesByName("Tapster.Fluent"))
                .ToArray();
            foreach (var p in processes)
            {
                try
                {
                    p.Kill();
                    p.WaitForExit(1000);
                }
                catch { }
            }
            Thread.Sleep(200);
        }
        catch { }
    }

    private static bool ActivateExistingInstance(string appDir)
    {
        try
        {
            var processes = Process.GetProcessesByName("TapStrum.Fluent")
                .Concat(Process.GetProcessesByName("Tapster.Fluent"))
                .ToArray();
            if (processes.Length == 0) return false;

            AllowSetForegroundWindow(ASFW_ANY);
            SignalWakeEvent();

            foreach (var p in processes)
            {
                if (p.MainWindowHandle != IntPtr.Zero)
                {
                    ShowWindow(p.MainWindowHandle, SW_RESTORE);
                    SetForegroundWindow(p.MainWindowHandle);
                    break;
                }
            }

            return true;
        }
        catch { }
        return false;
    }

    private static void SignalWakeEvent()
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(WakeEventName, out var wakeEvent))
            {
                using (wakeEvent)
                {
                    wakeEvent.Set();
                }
            }
        }
        catch (Exception ex)
        {
            // Best effort: cross-process wake event signaling may fail if permissions differ; safely ignored
            Debug.WriteLine($"Failed to signal wake event: {ex.Message}");
        }
    }

    private static void ExtractPayload(string targetDir, Stream? stream)
    {
        for (int retry = 0; retry < 3; retry++)
        {
            try
            {
                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, true);
                }
                Directory.CreateDirectory(targetDir);
                break;
            }
            catch
            {
                Thread.Sleep(300);
            }
        }

        if (stream == null)
        {
            string fallbackPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "payload.zip");
            if (File.Exists(fallbackPath))
            {
                ZipFile.ExtractToDirectory(fallbackPath, targetDir, true);
                return;
            }
            throw new FileNotFoundException("Embedded application payload was not found.");
        }

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        archive.ExtractToDirectory(targetDir, true);
    }
}
