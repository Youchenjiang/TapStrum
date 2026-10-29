using System;
using System.Threading;
using System.Threading.Tasks;

namespace TapStrum.Services;

/// <summary>
/// Domain service interface for the multi-stage key holding execution pipeline.
/// </summary>
public interface IKeyHolderService
{
    /// <summary>
    /// Executes the multi-stage key hold and rest pipeline asynchronously.
    /// </summary>
    /// <param name="options">Configuration options specifying stages and repeat loop counts.</param>
    /// <param name="cancellationToken">Cancellation token to gracefully stop pipeline execution.</param>
    /// <param name="reportProgress">Optional progress callback reporting human-readable message and percentage.</param>
    /// <param name="onStageStarted">Optional callback notifying when a stage begins execution, passing its original 0-based index.</param>
    /// <param name="onPipelineCompleted">Optional callback invoked when pipeline execution finishes or is cancelled.</param>
    /// <param name="panicDetector">Optional hardware panic detector instance for safety aborts.</param>
    Task RunAsync(
        KeyHolderOptions options,
        CancellationToken cancellationToken,
        Action<string, double>? reportProgress = null,
        Action<int>? onStageStarted = null,
        Action? onPipelineCompleted = null,
        PanicDetector? panicDetector = null);
}
