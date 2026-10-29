using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TapStrum.Services;

/// <summary>
/// Default implementation of <see cref="IKeyHolderService"/> executing multi-stage key hold
/// and rest pipelines with progress reporting, stage transitions, and safety panic monitoring.
/// </summary>
public sealed class KeyHolderService : IKeyHolderService
{
    private readonly Action<string> _pressAction;
    private readonly Action<string> _releaseAction;
    private readonly Action _releaseAllModifiersAction;
    private readonly Action<int, PanicDetector?, CancellationToken> _sleepAction;

    /// <summary>
    /// Initializes a new instance of <see cref="KeyHolderService"/> with default keyboard drivers.
    /// </summary>
    public KeyHolderService()
        : this(
            pressAction: Keyboard.Press,
            releaseAction: Keyboard.Release,
            releaseAllModifiersAction: Keyboard.ReleaseAllModifiers,
            sleepAction: PanicSafetyHelper.SleepWithPanicCheck)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="KeyHolderService"/> with custom delegates for testing.
    /// </summary>
    public KeyHolderService(
        Action<string> pressAction,
        Action<string> releaseAction,
        Action releaseAllModifiersAction,
        Action<int, PanicDetector?, CancellationToken> sleepAction)
    {
        _pressAction = pressAction ?? throw new ArgumentNullException(nameof(pressAction));
        _releaseAction = releaseAction ?? throw new ArgumentNullException(nameof(releaseAction));
        _releaseAllModifiersAction = releaseAllModifiersAction ?? throw new ArgumentNullException(nameof(releaseAllModifiersAction));
        _sleepAction = sleepAction ?? throw new ArgumentNullException(nameof(sleepAction));
    }

    /// <inheritdoc/>
    public async Task RunAsync(
        KeyHolderOptions options,
        CancellationToken cancellationToken,
        Action<string, double>? reportProgress = null,
        Action<int>? onStageStarted = null,
        Action? onPipelineCompleted = null,
        PanicDetector? panicDetector = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var indexedStagesToRun = options.Stages
            .Select((stage, index) => (OriginalIndex: index, Stage: stage.Clone()))
            .Where(x => !string.IsNullOrWhiteSpace(x.Stage.KeyCombo))
            .ToList();

        if (indexedStagesToRun.Count == 0)
        {
            throw new InvalidOperationException("No valid steps in pipeline!");
        }

        int repeatLoops = Math.Max(0, options.RepeatLoops);

        await Task.Run(() =>
        {
            try
            {
                int currentLoop = 0;
                while (!cancellationToken.IsCancellationRequested && (repeatLoops == 0 || currentLoop < repeatLoops))
                {
                    currentLoop++;
                    bool isFinalLoop = (repeatLoops > 0 && currentLoop == repeatLoops);
                    string loopPrefix = FormatHolderLoopPrefix(currentLoop, repeatLoops);
                    ExecuteHolderStagesPipeline(
                        indexedStagesToRun,
                        loopPrefix,
                        isFinalLoop,
                        reportProgress,
                        onStageStarted,
                        panicDetector,
                        cancellationToken);
                }
            }
            finally
            {
                onPipelineCompleted?.Invoke();
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private static string FormatHolderLoopPrefix(int currentLoop, int repeatLoops)
    {
        if (repeatLoops == 1) return "";
        if (repeatLoops == 0) return string.Format(LocalizationManager.Get("KeyHolder_LoopInfinitePrefix"), currentLoop);
        return string.Format(LocalizationManager.Get("KeyHolder_LoopPrefix"), currentLoop, repeatLoops);
    }

    private void ExecuteHolderStagesPipeline(
        List<(int OriginalIndex, KeyHolderStage Stage)> stagesToRun,
        string loopPrefix,
        bool isFinalLoop,
        Action<string, double>? reportProgress,
        Action<int>? onStageStarted,
        PanicDetector? panicDetector,
        CancellationToken token)
    {
        for (int i = 0; i < stagesToRun.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            PanicSafetyHelper.CheckPanicSafety(panicDetector);

            var (originalIdx, stage) = stagesToRun[i];
            onStageStarted?.Invoke(originalIdx);

            string stepPrefix = stagesToRun.Count > 1
                ? $"{loopPrefix}{string.Format(LocalizationManager.Get("KeyHolder_StepPrefix"), i + 1, stagesToRun.Count)}"
                : loopPrefix;
            bool isLastStepOfRun = isFinalLoop && (i == stagesToRun.Count - 1);

            ExecuteHolderStageHoldPhase(stage, stepPrefix, reportProgress, panicDetector, token);

            if (!isLastStepOfRun)
            {
                ExecuteHolderStageRestPhase(stage, stepPrefix, reportProgress, panicDetector, token);
            }
        }
    }

    private void ExecuteHolderStageHoldPhase(
        KeyHolderStage stage,
        string stepPrefix,
        Action<string, double>? reportProgress,
        PanicDetector? panicDetector,
        CancellationToken token)
    {
        int durationMs = (int)(stage.HoldDurationSec * 1000);
        try
        {
            _pressAction(stage.KeyCombo);
            int elapsedMs = 0;
            const int checkIntervalMs = 50;

            while (!token.IsCancellationRequested && (durationMs <= 0 || elapsedMs < durationMs))
            {
                PanicSafetyHelper.CheckPanicSafety(panicDetector);

                if (durationMs > 0)
                {
                    double pct = (double)elapsedMs / durationMs * 100;
                    reportProgress?.Invoke(
                        string.Format(LocalizationManager.Get("KeyHolder_HoldingDuration"), stepPrefix, stage.KeyCombo, elapsedMs / 1000.0, stage.HoldDurationSec),
                        pct);
                }
                else
                {
                    reportProgress?.Invoke(
                        string.Format(LocalizationManager.Get("KeyHolder_HoldingContinuously"), stepPrefix, stage.KeyCombo),
                        100);
                }

                _sleepAction(checkIntervalMs, panicDetector, token);
                elapsedMs += checkIntervalMs;
            }
        }
        finally
        {
            _releaseAction(stage.KeyCombo);
            _releaseAllModifiersAction();
        }
    }

    private void ExecuteHolderStageRestPhase(
        KeyHolderStage stage,
        string stepPrefix,
        Action<string, double>? reportProgress,
        PanicDetector? panicDetector,
        CancellationToken token)
    {
        if (stage.RestDurationSec <= 0 || token.IsCancellationRequested)
        {
            return;
        }

        int restMs = (int)(stage.RestDurationSec * 1000);
        int restElapsedMs = 0;
        const int checkIntervalMs = 50;

        while (!token.IsCancellationRequested && restElapsedMs < restMs)
        {
            PanicSafetyHelper.CheckPanicSafety(panicDetector);
            double pct = (double)restElapsedMs / restMs * 100;
            reportProgress?.Invoke(
                string.Format(LocalizationManager.Get("KeyHolder_RestingDuration"), stepPrefix, restElapsedMs / 1000.0, stage.RestDurationSec),
                pct);
            _sleepAction(checkIntervalMs, panicDetector, token);
            restElapsedMs += checkIntervalMs;
        }
    }
}
