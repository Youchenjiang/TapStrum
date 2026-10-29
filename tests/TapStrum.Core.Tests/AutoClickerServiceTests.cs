using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TapStrum.Services;
using Xunit;

namespace TapStrum.Core.Tests;

public class AutoClickerServiceTests
{
    [Fact]
    public async Task RunAsync_NullOptions_ThrowsArgumentNullExceptionAsync()
    {
        var service = new AutoClickerService();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.RunAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_FiniteClicks_ExecutesExpectedCountAndReportsProgressAsync()
    {
        int clickCount = 0;
        var progressReports = new List<(string Message, double Percentage)>();
        var clickedPoints = new List<(int X, int Y)>();

        var service = new AutoClickerService(
            clickAtAction: (x, y, btn) => clickCount++,
            clickAction: btn => clickCount++,
            getPositionFunc: () => (100, 200),
            spamAction: key => { },
            isKeyDownFunc: vk => true,
            sleepAction: (ms, pd, token) => { });

        var options = new AutoClickerOptions
        {
            IsSpammer = false,
            MouseButton = "left",
            IntervalMs = 10,
            TotalClicks = 5
        };

        await service.RunAsync(
            options,
            CancellationToken.None,
            reportProgress: (msg, pct) => progressReports.Add((msg, pct)),
            onPointClicked: (x, y) => clickedPoints.Add((x, y)));

        Assert.Equal(5, clickCount);
        Assert.Equal(5, clickedPoints.Count);
        Assert.Equal(5, progressReports.Count);
        Assert.Equal(100.0, progressReports[^1].Percentage);
    }

    [Fact]
    public async Task RunAsync_InfiniteClicks_TerminatesOnCancellationAsync()
    {
        int clickCount = 0;
        using var cts = new CancellationTokenSource();

        var service = new AutoClickerService(
            clickAtAction: (x, y, btn) => clickCount++,
            clickAction: btn =>
            {
                clickCount++;
                if (clickCount >= 3)
                {
                    cts.Cancel();
                }
            },
            getPositionFunc: () => (50, 50),
            spamAction: key => { },
            isKeyDownFunc: vk => true,
            sleepAction: (ms, pd, token) => { });

        var options = new AutoClickerOptions
        {
            IsSpammer = false,
            TotalClicks = 0, // infinite
            IntervalMs = 10
        };

        await service.RunAsync(options, cts.Token);

        Assert.True(clickCount >= 3);
    }

    [Fact]
    public async Task RunAsync_HoldMode_StopsWhenKeyReleasedAsync()
    {
        int clickCount = 0;
        bool keyHeld = true;

        var service = new AutoClickerService(
            clickAtAction: (x, y, btn) => clickCount++,
            clickAction: btn =>
            {
                clickCount++;
                if (clickCount >= 2)
                {
                    keyHeld = false;
                }
            },
            getPositionFunc: () => (0, 0),
            spamAction: key => { },
            isKeyDownFunc: vk => keyHeld,
            sleepAction: (ms, pd, token) => { });

        var options = new AutoClickerOptions
        {
            IsHoldMode = true,
            HotkeyVk = 0x75,
            TotalClicks = 10,
            IntervalMs = 10
        };

        await service.RunAsync(options, CancellationToken.None);

        Assert.Equal(2, clickCount);
    }

    [Fact]
    public async Task RunAsync_SpammerMode_InvokesSpamActionAsync()
    {
        var spammedKeys = new List<string>();

        var service = new AutoClickerService(
            clickAtAction: (x, y, btn) => { },
            clickAction: btn => { },
            getPositionFunc: () => (0, 0),
            spamAction: spammedKeys.Add,
            isKeyDownFunc: vk => true,
            sleepAction: (ms, pd, token) => { });

        var options = new AutoClickerOptions
        {
            IsSpammer = true,
            SpamKey = "space",
            TotalClicks = 4,
            IntervalMs = 10
        };

        await service.RunAsync(options, CancellationToken.None);

        Assert.Equal(4, spammedKeys.Count);
        Assert.All(spammedKeys, k => Assert.Equal("space", k));
    }

    [Fact]
    public async Task RunAsync_TargetCoordinates_AppliesLocationJitterAndInvokesClickAtAsync()
    {
        var clickedCoords = new List<(int X, int Y, string Button)>();

        var service = new AutoClickerService(
            clickAtAction: (x, y, btn) => clickedCoords.Add((x, y, btn)),
            clickAction: btn => { },
            getPositionFunc: () => (0, 0),
            spamAction: key => { },
            isKeyDownFunc: vk => true,
            sleepAction: (ms, pd, token) => { });

        var options = new AutoClickerOptions
        {
            IsSpammer = false,
            MouseButton = "right",
            TargetX = 500,
            TargetY = 300,
            LocationJitterPx = 0,
            TotalClicks = 2,
            IntervalMs = 10
        };

        await service.RunAsync(options, CancellationToken.None);

        Assert.Equal(2, clickedCoords.Count);
        Assert.Equal((500, 300, "right"), clickedCoords[0]);
    }

    [Fact]
    public async Task RunAsync_PanicDetected_ThrowsOperationCanceledExceptionAsync()
    {
        int escCall = 0;
        bool[] escSequence = [true, false, true, false, true];
        var panicDetector = new PanicDetector(
            isEscPressedFunc: () => escSequence[Math.Min(escCall++, escSequence.Length - 1)]);

        var service = new AutoClickerService(
            clickAtAction: (x, y, btn) => { },
            clickAction: btn => { },
            getPositionFunc: () => (0, 0),
            spamAction: key => { },
            isKeyDownFunc: vk => true,
            sleepAction: (ms, pd, token) =>
            {
                // Verify sleep with panic check throws when panic detected
                if (pd is not null && pd.CheckPanic(out var reason))
                {
                    throw new OperationCanceledException(reason ?? PanicDetector.EmergencyPanicMessage);
                }
            });

        var options = new AutoClickerOptions
        {
            TotalClicks = 10,
            IntervalMs = 10
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.RunAsync(options, CancellationToken.None, panicDetector: panicDetector));
    }
}
