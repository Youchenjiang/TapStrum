using System;
using System.Collections.Generic;

namespace Tapster.Services;

/// <summary>
/// Configuration options for the multi-stage key holding execution pipeline.
/// </summary>
public sealed record KeyHolderOptions
{
    /// <summary>
    /// Gets the list of stages to execute in the pipeline sequence.
    /// </summary>
    public IReadOnlyList<KeyHolderStage> Stages { get; init; } = Array.Empty<KeyHolderStage>();

    /// <summary>
    /// Gets the total number of repeat loops (0 = infinite / continuous until cancelled, 1 = execute once, >1 = n times).
    /// </summary>
    public int RepeatLoops { get; init; } = 1;
}
