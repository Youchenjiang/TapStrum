namespace TapStrum.Services;

/// <summary>
/// Operational mode for automated typing simulation.
/// </summary>
public enum AutoTyperMode
{
    /// <summary>
    /// Type character by character with configurable intervals and optional human-like jitter.
    /// </summary>
    Keystroke,

    /// <summary>
    /// Instantly paste the entire content via clipboard and restore previous clipboard content.
    /// </summary>
    Clipboard
}

/// <summary>
/// Configuration options for the automated typing engine.
/// </summary>
public sealed record AutoTyperOptions
{
    /// <summary>
    /// Gets the text content to be typed or pasted.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Gets the typing mode (Keystroke or Clipboard).
    /// </summary>
    public AutoTyperMode Mode { get; init; } = AutoTyperMode.Keystroke;

    /// <summary>
    /// Gets the interval between keystrokes in milliseconds (for Keystroke mode).
    /// </summary>
    public int IntervalMs { get; init; } = 20;

    /// <summary>
    /// Gets a value indicating whether natural typing jitter (+/- 15%) is applied.
    /// </summary>
    public bool JitterEnabled { get; init; }

    /// <summary>
    /// Gets the optional trailing key ("Enter", "Tab", or "None").
    /// </summary>
    public string TrailingKey { get; init; } = "None";
}
