namespace TapStrum.Services;

/// <summary>
/// Data model representing user application preferences and automation configurations.
/// </summary>
public class AppSettingsData
{
    public bool StartMinimizedToTray { get; set; }
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public string Language { get; set; } = "auto";
    public double DelaySeconds { get; set; } = 3;

    public string HotkeyClicker { get; set; } = "F6";
    public string HotkeyHolder { get; set; } = "F7";
    public string HotkeyTyper { get; set; } = "F8";
    public string HotkeyMacro { get; set; } = "F9";
    public string HotkeyPanicKill { get; set; } = "F10";

    public bool ClickerHoldMode { get; set; }
    public bool ClickerIsKeySpammer { get; set; }
    public string ClickerSpamKey { get; set; } = "space";
    public bool ClickerJitterEnabled { get; set; }
    public double ClickerTimeJitterPercent { get; set; } = 15;
    public double ClickerLocationJitterPx { get; set; }
    public bool ClickerShowTargetMarker { get; set; }

    public string TyperInputMode { get; set; } = "keystroke";
    public string TyperTrailingKey { get; set; } = "none";
    public bool TyperJitterEnabled { get; set; }

    public bool MacroIgnoreMouseMove { get; set; } = true;
}
