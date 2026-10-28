namespace Tapster_Fluent.Views;

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

/// <summary>
/// View displaying application metadata, system specifications, and links.
/// </summary>
public sealed partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();
    }

    private void OpenStore_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://apps.microsoft.com/detail/9n4whn5np91c",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open Microsoft Store: {ex.Message}");
        }
    }

    private void OpenSettingsFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string folder = AppSettings.SettingsFolder;
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open settings folder: {ex.Message}");
        }
    }
}
