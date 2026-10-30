using System;
using System.IO;
using Microsoft.UI.Xaml;

namespace TapStrum.Fluent;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        UnhandledException += (sender, e) => LogCrash("UnhandledException: " + e.Exception);

        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            LogCrash("InitializeComponent failed: " + ex);
            throw;
        }
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow();

            string[] cmdArgs = Environment.GetCommandLineArgs();
            bool startInTray = AppSettings.Current.StartMinimizedToTray;
            foreach (var arg in cmdArgs)
            {
                if (string.Equals(arg, "--tray", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "-minimized", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "-tray", StringComparison.OrdinalIgnoreCase))
                {
                    startInTray = true;
                    break;
                }
            }

            if (!startInTray)
            {
                _window.Activate();
            }
        }
        catch (Exception ex)
        {
            LogCrash("OnLaunched failed: " + ex);
            throw;
        }
    }

    internal static void LogCrash(string message)
    {
        try
        {
            string path = Path.Join(Path.GetTempPath(), "tapster_crash.log");
            File.AppendAllText(path, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                string appDir = Path.Join(localAppData, "TapStrum");
                Directory.CreateDirectory(appDir);
                File.AppendAllText(Path.Join(appDir, "crash.log"), $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging failure must never terminate application
        }
    }
}
