using System;
using System.Threading;
using System.Threading.Tasks;

namespace TapStrum.Services;

/// <summary>
/// Domain service interface for automated keystroke and clipboard typing.
/// </summary>
public interface IAutoTyperService
{
    /// <summary>
    /// Executes the typing simulation asynchronously according to the specified options.
    /// </summary>
    /// <param name="options">Configuration options specifying text, mode, intervals, and trailing keys.</param>
    /// <param name="cancellationToken">Cancellation token to gracefully interrupt typing.</param>
    /// <param name="reportProgress">Optional progress callback reporting human-readable message and percentage.</param>
    /// <param name="clipboardService">Optional clipboard service implementation for Clipboard mode.</param>
    /// <param name="panicDetector">Optional hardware-level panic detector instance for safety aborts.</param>
    Task RunAsync(
        AutoTyperOptions options,
        CancellationToken cancellationToken,
        Action<string, double>? reportProgress = null,
        IClipboardService? clipboardService = null,
        PanicDetector? panicDetector = null);
}
