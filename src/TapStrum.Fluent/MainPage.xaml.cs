namespace TapStrum.Fluent;

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TapStrum;
using TapStrum.Fluent.ViewModels;
using TapStrum.Fluent.Views;

/// <summary>
/// Root navigation container and coordinator for TapStrum functional views and ViewModels.
/// </summary>
public sealed partial class MainPage : Page
{
    public const string TabTyper = "Typer";
    public const string TabHolder = "Holder";
    public const string TabClicker = "Clicker";
    public const string TabMacro = "Macro";
    public const string TabSettings = "Settings";
    public const string TabAbout = "About";

    public static MainPage? Instance { get; private set; }

    public MainViewModel ViewModel { get; }

    private readonly TargetMarkerOverlay _targetMarkerOverlay = new();

    public MainPage()
    {
        Instance = this;
        ViewModel = new MainViewModel(
            clicker: new ClickerViewModel(
                onPointClicked: (x, y) => _targetMarkerOverlay.ShowClickRipple(x, y),
                dispatcher: action => DispatcherQueue.TryEnqueue(() => action())),
            macro: new MacroViewModel(
                setMarkersCallback: markers => _targetMarkerOverlay.SetMarkers(markers),
                onMarkerRippleCallback: (x, y, clickNum) => _targetMarkerOverlay.ShowClickRipple(x, y, clickNum),
                clearMarkersCallback: () => _targetMarkerOverlay.ClearAndHide(),
                dispatcher: action => DispatcherQueue.TryEnqueue(() => action())),
            dispatcher: action => DispatcherQueue.TryEnqueue(() => action()));

        InitializeComponent();
        Loaded += MainPage_Loaded;
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        TyperPanel.ViewModel = ViewModel.Typer;
        HolderPanel.ViewModel = ViewModel.Holder;
        ClickerPanel.ViewModel = ViewModel.Clicker;
        MacroPanel.ViewModel = ViewModel.Macro;
        SettingsPanel.DataContext = ViewModel.Settings;

        SettingsPanel.NavigateToAboutRequested += (_, _) => NavView.SelectedItem = NavItemAbout;

        NavView.SelectedItem = NavItemTyper;
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            SwitchToTab(TabSettings);
            return;
        }

        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            SwitchToTab(tag);
        }
    }

    private void SwitchToTab(string tab)
    {
        ViewModel.SelectedNavigationTag = tab;

        TyperPanel.Visibility = tab == TabTyper ? Visibility.Visible : Visibility.Collapsed;
        HolderPanel.Visibility = tab == TabHolder ? Visibility.Visible : Visibility.Collapsed;
        ClickerPanel.Visibility = tab == TabClicker ? Visibility.Visible : Visibility.Collapsed;
        MacroPanel.Visibility = tab == TabMacro ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = tab == TabSettings ? Visibility.Visible : Visibility.Collapsed;
        AboutPanel.Visibility = tab == TabAbout ? Visibility.Visible : Visibility.Collapsed;

        UpdateTargetMarkerOverlay();
    }

    private void UpdateTargetMarkerOverlay()
    {
        if (ViewModel.SelectedNavigationTag == TabClicker &&
            ViewModel.Clicker.ShowTargetMarker &&
            !ViewModel.Clicker.IsKeySpammer &&
            ViewModel.Clicker.TargetX is double targetX &&
            ViewModel.Clicker.TargetY is double targetY)
        {
            _targetMarkerOverlay.Update((int)targetX, (int)targetY, ViewModel.Clicker.LocationJitterPx, true);
        }
        else
        {
            _targetMarkerOverlay.ClearAndHide();
        }
    }

    public void ToggleClickerFromHotkey()
    {
        DispatcherQueue.TryEnqueue(async () => await ViewModel.Clicker.ToggleAsync());
    }

    public void ToggleHolderFromHotkey()
    {
        DispatcherQueue.TryEnqueue(async () => await ViewModel.Holder.ToggleAsync());
    }

    public void ToggleTyperFromHotkey()
    {
        DispatcherQueue.TryEnqueue(async () => await ViewModel.Typer.ToggleAsync());
    }

    public void ToggleMacroFromHotkey()
    {
        DispatcherQueue.TryEnqueue(async () => await ViewModel.Macro.TogglePlayAsync());
    }

    public void PanicKillAll()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ViewModel.StopAll(LocalizationManager.Get("Common_StoppedByUser"));
            _targetMarkerOverlay.ClearAndHide();
            Keyboard.ReleaseAllModifiers();
        });
    }

    public void OnGuiHidden()
    {
        DispatcherQueue.TryEnqueue(() => _targetMarkerOverlay.ClearAndHide());
    }

    public void OnGuiRestored()
    {
        DispatcherQueue.TryEnqueue(UpdateTargetMarkerOverlay);
    }

    public void OnMainWindowAlwaysOnTopChanged(bool isTop)
    {
        DispatcherQueue.TryEnqueue(() => SettingsPanel.SyncUiFromSettings());
    }
}
