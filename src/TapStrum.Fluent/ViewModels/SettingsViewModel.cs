namespace TapStrum.Fluent.ViewModels;

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TapStrum.Services;

/// <summary>
/// ViewModel managing application settings, persistence, and hotkey configurations.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly IAppSettingsService _settingsService;

    [ObservableProperty]
    private string _language = "auto";

    [ObservableProperty]
    private bool _startMinimizedToTray;

    [ObservableProperty]
    private bool _minimizeToTrayOnClose = true;

    [ObservableProperty]
    private bool _startOnBoot;

    [ObservableProperty]
    private double _delaySeconds = 3.0;

    [ObservableProperty]
    private string _hotkeyClicker = "F6";

    [ObservableProperty]
    private string _hotkeyHolder = "F7";

    [ObservableProperty]
    private string _hotkeyTyper = "F8";

    [ObservableProperty]
    private string _hotkeyMacro = "F9";

    [ObservableProperty]
    private string _hotkeyPanicKill = "F10";

    [ObservableProperty]
    private bool _panicEnabled = true;

    [ObservableProperty]
    private bool _clickerHoldMode;

    [ObservableProperty]
    private bool _clickerIsKeySpammer;

    [ObservableProperty]
    private string _clickerSpamKey = "space";

    [ObservableProperty]
    private bool _clickerJitterEnabled;

    [ObservableProperty]
    private double _clickerTimeJitterPercent = 15.0;

    [ObservableProperty]
    private double _clickerLocationJitterPx;

    [ObservableProperty]
    private bool _clickerShowTargetMarker;

    [ObservableProperty]
    private string _typerInputMode = "keystroke";

    [ObservableProperty]
    private string _typerTrailingKey = "none";

    [ObservableProperty]
    private bool _typerJitterEnabled;

    [ObservableProperty]
    private bool _macroIgnoreMouseMove = true;

    public SettingsViewModel(
        IAppSettingsService? settingsService = null,
        Action<Action>? dispatcher = null)
        : base(dispatcher)
    {
        _settingsService = settingsService ?? new AppSettingsService();
        Load();
    }

    [RelayCommand]
    public void Load()
    {
        var data = _settingsService.Load();

        Language = data.Language;
        StartMinimizedToTray = data.StartMinimizedToTray;
        MinimizeToTrayOnClose = data.MinimizeToTrayOnClose;
        DelaySeconds = data.DelaySeconds;

        HotkeyClicker = data.HotkeyClicker;
        HotkeyHolder = data.HotkeyHolder;
        HotkeyTyper = data.HotkeyTyper;
        HotkeyMacro = data.HotkeyMacro;
        HotkeyPanicKill = data.HotkeyPanicKill;

        ClickerHoldMode = data.ClickerHoldMode;
        ClickerIsKeySpammer = data.ClickerIsKeySpammer;
        ClickerSpamKey = data.ClickerSpamKey;
        ClickerJitterEnabled = data.ClickerJitterEnabled;
        ClickerTimeJitterPercent = data.ClickerTimeJitterPercent;
        ClickerLocationJitterPx = data.ClickerLocationJitterPx;
        ClickerShowTargetMarker = data.ClickerShowTargetMarker;

        TyperInputMode = data.TyperInputMode;
        TyperTrailingKey = data.TyperTrailingKey;
        TyperJitterEnabled = data.TyperJitterEnabled;
        MacroIgnoreMouseMove = data.MacroIgnoreMouseMove;
    }

    [RelayCommand]
    public void Save()
    {
        var data = new AppSettingsData
        {
            Language = Language,
            StartMinimizedToTray = StartMinimizedToTray,
            MinimizeToTrayOnClose = MinimizeToTrayOnClose,
            DelaySeconds = DelaySeconds,
            HotkeyClicker = HotkeyClicker,
            HotkeyHolder = HotkeyHolder,
            HotkeyTyper = HotkeyTyper,
            HotkeyMacro = HotkeyMacro,
            HotkeyPanicKill = HotkeyPanicKill,
            ClickerHoldMode = ClickerHoldMode,
            ClickerIsKeySpammer = ClickerIsKeySpammer,
            ClickerSpamKey = ClickerSpamKey,
            ClickerJitterEnabled = ClickerJitterEnabled,
            ClickerTimeJitterPercent = ClickerTimeJitterPercent,
            ClickerLocationJitterPx = ClickerLocationJitterPx,
            ClickerShowTargetMarker = ClickerShowTargetMarker,
            TyperInputMode = TyperInputMode,
            TyperTrailingKey = TyperTrailingKey,
            TyperJitterEnabled = TyperJitterEnabled,
            MacroIgnoreMouseMove = MacroIgnoreMouseMove
        };

        _settingsService.Save(data);
    }

    [RelayCommand]
    public void ResetToDefaults()
    {
        var defaults = new AppSettingsData();

        Language = defaults.Language;
        StartMinimizedToTray = defaults.StartMinimizedToTray;
        MinimizeToTrayOnClose = defaults.MinimizeToTrayOnClose;
        DelaySeconds = defaults.DelaySeconds;

        HotkeyClicker = defaults.HotkeyClicker;
        HotkeyHolder = defaults.HotkeyHolder;
        HotkeyTyper = defaults.HotkeyTyper;
        HotkeyMacro = defaults.HotkeyMacro;
        HotkeyPanicKill = defaults.HotkeyPanicKill;

        ClickerHoldMode = defaults.ClickerHoldMode;
        ClickerIsKeySpammer = defaults.ClickerIsKeySpammer;
        ClickerSpamKey = defaults.ClickerSpamKey;
        ClickerJitterEnabled = defaults.ClickerJitterEnabled;
        ClickerTimeJitterPercent = defaults.ClickerTimeJitterPercent;
        ClickerLocationJitterPx = defaults.ClickerLocationJitterPx;
        ClickerShowTargetMarker = defaults.ClickerShowTargetMarker;

        TyperInputMode = defaults.TyperInputMode;
        TyperTrailingKey = defaults.TyperTrailingKey;
        TyperJitterEnabled = defaults.TyperJitterEnabled;
        MacroIgnoreMouseMove = defaults.MacroIgnoreMouseMove;

        Save();
    }
}
