using System;
using System.Threading;
using System.Threading.Tasks;

namespace TapStrum.Services;

/// <summary>
/// Domain service interface for automated mouse clicking and keyboard key spamming.
/// </summary>
public interface IAutoClickerService
{
    /// <summary>
    /// Executes the auto-clicker or key-spammer loop asynchronously until completed, cancelled, or aborted by panic.
    /// </summary>
    /// <param name="options">Configuration options specifying targets, jitter, intervals, and counts.</param>
    /// <param name="cancellationToken">Cancellation token to gracefully stop execution.</param>
    /// <param name="reportProgress">Optional callback reporting human-readable status text and progress percentage.</param>
    /// <param name="onPointClicked">Optional callback invoked with the calculated screen coordinates after each click.</param>
    /// <param name="panicDetector">Optional hardware-level panic detector instance for safety checks.</param>
    Task RunAsync(
        AutoClickerOptions options,
        CancellationToken cancellationToken,
        Action<string, double>? reportProgress = null,
        Action<int, int>? onPointClicked = null,
        PanicDetector? panicDetector = null);
}
