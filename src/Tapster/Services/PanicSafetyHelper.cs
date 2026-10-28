using System;
using System.Threading;

namespace Tapster.Services;

/// <summary>
/// Provides shared panic detection and cancellable delay utilities across background automation services.
/// </summary>
internal static class PanicSafetyHelper
{
    /// <summary>
    /// Checks panic detector safety and raises an <see cref="OperationCanceledException"/> if panic conditions are triggered.
    /// </summary>
    /// <param name="panicDetector">The active panic detector instance.</param>
    public static void CheckPanicSafety(PanicDetector? panicDetector)
    {
        if (panicDetector is not null && panicDetector.CheckPanic(out var reason))
        {
            throw new OperationCanceledException(reason ?? PanicDetector.EmergencyPanicMessage);
        }
    }

    /// <summary>
    /// Delays execution in discrete intervals checking cancellation and panic safety flags.
    /// </summary>
    /// <param name="totalMs">The total delay duration in milliseconds.</param>
    /// <param name="panicDetector">The active panic detector instance.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static void SleepWithPanicCheck(int totalMs, PanicDetector? panicDetector, CancellationToken cancellationToken = default)
    {
        const int stepMs = 20;
        int elapsed = 0;
        while (elapsed < totalMs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckPanicSafety(panicDetector);
            int chunk = Math.Min(stepMs, totalMs - elapsed);
            Thread.Sleep(chunk);
            elapsed += chunk;
        }
    }
}
