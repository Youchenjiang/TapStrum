using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tapster;
using Tapster.Services;
using Xunit;

namespace Tapster.Core.Tests;

public class KeyHolderServiceTests
{
    [Fact]
    public async Task RunAsync_NullOrEmptyStages_ThrowsInvalidOperationExceptionAsync()
    {
        var service = new KeyHolderService();
        var options = new KeyHolderOptions { Stages = Array.Empty<KeyHolderStage>() };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RunAsync(options, CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_ExecutesHoldAndRestPhasesInOrderAsync()
    {
        var pressed = new List<string>();
        var released = new List<string>();
        int modifierReleases = 0;
        var startedStages = new List<int>();
        bool pipelineCompleted = false;

        var service = new KeyHolderService(
            pressAction: pressed.Add,
            releaseAction: released.Add,
            releaseAllModifiersAction: () => modifierReleases++,
            sleepAction: (ms, pd, token) => { });

        var stages = new List<KeyHolderStage>
        {
            new("w", holdDurationSec: 0.1, restDurationSec: 0.05),
            new("shift+w", holdDurationSec: 0.1, restDurationSec: 0.05)
        };

        var options = new KeyHolderOptions
        {
            Stages = stages,
            RepeatLoops = 1
        };

        await service.RunAsync(
            options,
            CancellationToken.None,
            onStageStarted: startedStages.Add,
            onPipelineCompleted: () => pipelineCompleted = true);

        Assert.Equal(2, pressed.Count);
        Assert.Equal("w", pressed[0]);
        Assert.Equal("shift+w", pressed[1]);

        Assert.Equal(2, released.Count);
        Assert.Equal("w", released[0]);
        Assert.Equal("shift+w", released[1]);

        Assert.Equal(2, modifierReleases);
        Assert.Equal(new[] { 0, 1 }, startedStages);
        Assert.True(pipelineCompleted);
    }

    [Fact]
    public async Task RunAsync_RepeatLoops_ExecutesExactLoopsAsync()
    {
        int pressCount = 0;

        var service = new KeyHolderService(
            pressAction: k => pressCount++,
            releaseAction: k => { },
            releaseAllModifiersAction: () => { },
            sleepAction: (ms, pd, token) => { });

        var options = new KeyHolderOptions
        {
            Stages = new[] { new KeyHolderStage("space", holdDurationSec: 0.05, restDurationSec: 0.05) },
            RepeatLoops = 3
        };

        await service.RunAsync(options, CancellationToken.None);

        Assert.Equal(3, pressCount);
    }

    [Fact]
    public async Task RunAsync_InfiniteLoops_StopsOnCancellationAsync()
    {
        int pressCount = 0;
        using var cts = new CancellationTokenSource();

        var service = new KeyHolderService(
            pressAction: k =>
            {
                pressCount++;
                if (pressCount >= 4)
                {
                    cts.Cancel();
                }
            },
            releaseAction: k => { },
            releaseAllModifiersAction: () => { },
            sleepAction: (ms, pd, token) => { });

        var options = new KeyHolderOptions
        {
            Stages = new[] { new KeyHolderStage("a", holdDurationSec: 0.05, restDurationSec: 0.05) },
            RepeatLoops = 0 // infinite
        };

        await service.RunAsync(options, cts.Token);

        Assert.True(pressCount >= 4);
    }

    [Fact]
    public async Task RunAsync_PanicDetected_ThrowsOperationCanceledExceptionAsync()
    {
        int escCall = 0;
        bool[] escSequence = [true, false, true, false, true];
        var panicDetector = new PanicDetector(
            isEscPressedFunc: () => escSequence[Math.Min(escCall++, escSequence.Length - 1)]);

        var service = new KeyHolderService(
            pressAction: k => { },
            releaseAction: k => { },
            releaseAllModifiersAction: () => { },
            sleepAction: (ms, pd, token) =>
            {
                if (pd is not null && pd.CheckPanic(out var reason))
                {
                    throw new OperationCanceledException(reason ?? PanicDetector.EmergencyPanicMessage);
                }
            });

        var options = new KeyHolderOptions
        {
            Stages = new[] { new KeyHolderStage("space", holdDurationSec: 0.5, restDurationSec: 0.1) }
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.RunAsync(options, CancellationToken.None, panicDetector: panicDetector));
    }
}
