#pragma warning disable S2325 // WinUI XAML event handlers and controls are generated in partial class
namespace TapStrum.Fluent.Views;

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TapStrum;
using TapStrum.Fluent.ViewModels;

/// <summary>
/// View managing user application preferences, startup toggles, and hotkey overview.
/// </summary>
public sealed partial class SettingsView : UserControl
{
    public SettingsViewModel? ViewModel => DataContext as SettingsViewModel;

    public event EventHandler? NavigateToAboutRequested;

    public SettingsView()
    {
        InitializeComponent();
        Loaded += SettingsView_Loaded;
    }

    private void SettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        PopulateLanguages();
        SyncUiFromSettings();
    }

    public void PopulateLanguages()
    {
        LanguageCombo.Items.Clear();
        int selectedIndex = 0;
        for (int i = 0; i < LocalizationManager.SupportedLanguages.Count; i++)
        {
            var (code, _) = LocalizationManager.SupportedLanguages[i];
            var name = LocalizationManager.GetLanguageDisplayName(code);
            LanguageCombo.Items.Add(new ComboBoxItem { Content = name, Tag = code });
            if (string.Equals(code, AppSettings.Current.Language, StringComparison.OrdinalIgnoreCase))
            {
                selectedIndex = i;
            }
        }

        LanguageCombo.SelectedIndex = selectedIndex;
    }

    public void SyncUiFromSettings()
    {
        StartOnBootToggle.IsOn = AppSettings.Current.StartOnBoot;
        StartMinimizedToggle.IsOn = AppSettings.Current.StartMinimizedToTray;
        MinimizeOnCloseToggle.IsOn = AppSettings.Current.MinimizeToTrayOnClose;

        if (MainWindow.Instance is { } mw)
        {
            AlwaysOnTopToggle.IsOn = mw.IsAlwaysOnTop;
        }
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageCombo.SelectedItem is ComboBoxItem item && item.Tag is string code)
        {
            if (string.Equals(AppSettings.Current.Language, code, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            AppSettings.Current.Language = code;
            AppSettings.Current.Save();
            LocalizationManager.SetLanguage(code);
        }
    }

    private void StartOnBootToggle_Toggled(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.StartOnBoot = StartOnBootToggle.IsOn;
        if (ViewModel is not null)
        {
            ViewModel.StartOnBoot = StartOnBootToggle.IsOn;
        }
    }

    private void StartMinimizedToggle_Toggled(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.StartMinimizedToTray = StartMinimizedToggle.IsOn;
        AppSettings.Current.Save();
        if (ViewModel is not null)
        {
            ViewModel.StartMinimizedToTray = StartMinimizedToggle.IsOn;
        }
    }

    private void MinimizeOnCloseToggle_Toggled(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.MinimizeToTrayOnClose = MinimizeOnCloseToggle.IsOn;
        AppSettings.Current.Save();
        if (ViewModel is not null)
        {
            ViewModel.MinimizeToTrayOnClose = MinimizeOnCloseToggle.IsOn;
        }
    }

    private void AlwaysOnTopToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (MainWindow.Instance is { } mw && mw.IsAlwaysOnTop != AlwaysOnTopToggle.IsOn)
        {
            mw.SetAlwaysOnTop(AlwaysOnTopToggle.IsOn);
        }
    }

    private void NavigateToAbout_Click(object sender, RoutedEventArgs e)
    {
        NavigateToAboutRequested?.Invoke(this, EventArgs.Empty);
    }
}
