namespace TapStrum.Fluent.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using TapStrum;
using TapStrum.Services;
using TapStrum.Fluent.ViewModels;
using Xunit;

public class ClickerViewModelTests
{
    private sealed class MockClickerService : IAutoClickerService
    {
        public AutoClickerOptions? LastOptions { get; private set; }
        public bool WasRunCalled { get; private set; }
        public Action<int, int>? PointClickedCallback { get; set; }

        public Task RunAsync(
            AutoClickerOptions options,
            CancellationToken cancellationToken,
            Action<string, double>? reportProgress = null,
            Action<int, int>? onPointClicked = null,
            PanicDetector? panicDetector = null)
        {
            WasRunCalled = true;
            LastOptions = options;
            reportProgress?.Invoke("Running clicker", 50.0);
            onPointClicked?.Invoke(100, 200);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        var vm = new ClickerViewModel();

        Assert.Equal(0.1, vm.IntervalSeconds);
        Assert.Equal(100, vm.TotalClicks);
        Assert.False(vm.IsInfiniteClicks);
        Assert.Equal(0, vm.MouseButtonIndex);
        Assert.False(vm.IsHoldMode);
        Assert.False(vm.IsKeySpammer);
        Assert.Equal("space", vm.SpamKey);
        Assert.False(vm.JitterEnabled);
        Assert.Equal(15.0, vm.TimeJitterPercent);
        Assert.Equal(0.0, vm.LocationJitterPx);
        Assert.False(vm.ShowTargetMarker);
        Assert.Null(vm.TargetX);
        Assert.Null(vm.TargetY);
        Assert.Equal(3.0, vm.DelaySeconds);
        Assert.False(vm.IsRunning);
    }

    [Fact]
    public void SetCoordinates_And_ClearCoordinates_UpdatesProperties()
    {
        var vm = new ClickerViewModel();

        vm.SetCoordinates(250, 450);
        Assert.Equal(250, vm.TargetX);
        Assert.Equal(450, vm.TargetY);

        vm.ClearCoordinates();
        Assert.Null(vm.TargetX);
        Assert.Null(vm.TargetY);
    }

    [Fact]
    public async Task StartAsync_ValidationFails_WhenKeySpammerWithoutKeyAsync()
    {
        var mockService = new MockClickerService();
        var vm = new ClickerViewModel(mockService)
        {
            IsKeySpammer = true,
            SpamKey = "   ",
            DelaySeconds = 0
        };

        await vm.StartAsync();

        Assert.False(vm.IsRunning);
        Assert.False(mockService.WasRunCalled);
        Assert.False(string.IsNullOrWhiteSpace(vm.StatusText));
    }

    [Fact]
    public async Task StartAsync_ExecutesServiceAndInvokesCallbackAsync()
    {
        var mockService = new MockClickerService();
        (int X, int Y)? clickedPoint = null;

        var vm = new ClickerViewModel(
            mockService,
            onPointClicked: (x, y) => clickedPoint = (x, y))
        {
            IntervalSeconds = 0.05,
            TotalClicks = 25,
            MouseButtonIndex = 1,
            ShowTargetMarker = true,
            DelaySeconds = 0
        };

        await vm.StartAsync();

        Assert.True(mockService.WasRunCalled);
        Assert.NotNull(mockService.LastOptions);
        Assert.Equal(50, mockService.LastOptions.IntervalMs);
        Assert.Equal(25, mockService.LastOptions.TotalClicks);
        Assert.Equal("right", mockService.LastOptions.MouseButton);
        Assert.Equal((100, 200), clickedPoint);
        Assert.False(vm.IsRunning);
    }

    [Fact]
    public async Task ToggleAsync_StartsAndStopsTaskAsync()
    {
        var mockService = new MockClickerService();
        var vm = new ClickerViewModel(mockService)
        {
            DelaySeconds = 0
        };

        await vm.ToggleAsync();
        Assert.True(mockService.WasRunCalled);
        Assert.False(vm.IsRunning);

        vm.Stop("User abort");
        Assert.Equal("User abort", vm.StatusText);
    }
}
