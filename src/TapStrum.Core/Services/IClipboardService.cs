using System.Threading.Tasks;

namespace TapStrum.Services;

/// <summary>
/// Abstraction for accessing and updating system clipboard content.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Retrieves current plain text content from the clipboard, or null if none.
    /// </summary>
    Task<string?> GetTextAsync();

    /// <summary>
    /// Sets plain text content onto the clipboard.
    /// </summary>
    /// <param name="text">The string to place onto the clipboard.</param>
    Task SetTextAsync(string text);
}
