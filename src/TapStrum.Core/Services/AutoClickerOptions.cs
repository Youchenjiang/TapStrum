namespace Tapster.Services;

/// <summary>
/// Configuration parameters for the automated clicking and key-spamming engine.
/// </summary>
public sealed record AutoClickerOptions
{
    /// <summary>
    /// Gets a value indicating whether to perform keyboard key spamming instead of mouse clicking.
    /// </summary>
    public bool IsSpammer { get; init; }

    /// <summary>
    /// Gets the key or combo to spam when <see cref="IsSpammer"/> is true.
    /// </summary>
    public string SpamKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the mouse button to click ("left", "right", or "middle").
    /// </summary>
    public string MouseButton { get; init; } = "left";

    /// <summary>
    /// Gets the delay interval between click iterations in milliseconds (minimum 5 ms).
    /// </summary>
    public int IntervalMs { get; init; } = 100;

    /// <summary>
    /// Gets the target total click count (0 or negative represents infinite looping).
    /// </summary>
    public int TotalClicks { get; init; }

    /// <summary>
    /// Gets the optional target X coordinate on screen. If null, clicks at current cursor position.
    /// </summary>
    public int? TargetX { get; init; }

    /// <summary>
    /// Gets the optional target Y coordinate on screen. If null, clicks at current cursor position.
    /// </summary>
    public int? TargetY { get; init; }

    /// <summary>
    /// Gets a value indicating whether time jitter is enabled.
    /// </summary>
    public bool JitterEnabled { get; init; }

    /// <summary>
    /// Gets the percentage deviation for time jitter (e.g. 20.0 for +/- 20%).
    /// </summary>
    public double TimeJitterPct { get; init; }

    /// <summary>
    /// Gets the radius in pixels for spatial location jitter.
    /// </summary>
    public double LocationJitterPx { get; init; }

    /// <summary>
    /// Gets a value indicating whether hold-to-click mode is enabled (stops when hotkey is released).
    /// </summary>
    public bool IsHoldMode { get; init; }

    /// <summary>
    /// Gets the virtual key code monitored during hold-to-click mode (default: 0x75 for F6).
    /// </summary>
    public int HotkeyVk { get; init; } = 0x75;
}
