namespace TapStrum.Fluent.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using TapStrum;
using TapStrum.Services;
using TapStrum.Fluent.ViewModels;
using Xunit;

public class HolderViewModelTests
{
    private sealed class MockHolderService : IKeyHolderService
    {
        public KeyHolderOptions? LastOptions { get; private set; }
        public bool WasRunCalled { get; private set; }

        public Task RunAsync(
            KeyHolderOptions options,
            CancellationToken cancellationToken,
            Action<string, double>? reportProgress = null,
            Action<int>? onStageStarted = null,
            Action? onPipelineCompleted = null,
            PanicDetector? panicDetector = null)
        {
            WasRunCalled = true;
            LastOptions = options;
            onStageStarted?.Invoke(0);
            reportProgress?.Invoke("Holding step 1", 50.0);
            onPipelineCompleted?.Invoke();
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        var vm = new HolderViewModel();

        Assert.Empty(vm.Stages);
        Assert.Equal(0, vm.ActiveStageIndex);
        Assert.Equal(-1, vm.RunningStageIndex);
        Assert.Equal(1, vm.RepeatLoops);
        Assert.Equal(3.0, vm.DelaySeconds);
        Assert.False(vm.IsRunning);
    }

    [Fact]
    public void AddRemoveClearStage_ManipulatesCollection()
    {
        var vm = new HolderViewModel();

        vm.AddStage(new KeyHolderStage("ctrl+c", 2.0, 0.5));
        vm.AddStage(new KeyHolderStage("ctrl+v", 2.0, 0.5));
        Assert.Equal(2, vm.Stages.Count);
        Assert.Equal(1, vm.ActiveStageIndex);

        vm.ClearStageKeys(0);
        Assert.Equal(string.Empty, vm.Stages[0].KeyCombo);

        vm.RemoveStage(0);
        Assert.Single(vm.Stages);
        Assert.Equal("ctrl+v", vm.Stages[0].KeyCombo);
    }

    [Fact]
    public void MoveStage_ReordersStagesAndUpdatesActiveIndex()
    {
        var vm = new HolderViewModel();
        vm.AddStage(new KeyHolderStage("a", 1.0, 0.5));
        vm.AddStage(new KeyHolderStage("b", 1.0, 0.5));
        vm.AddStage(new KeyHolderStage("c", 1.0, 0.5));

        vm.MoveStage(0, 2);
        Assert.Equal("b", vm.Stages[0].KeyCombo);
        Assert.Equal("c", vm.Stages[1].KeyCombo);
        Assert.Equal("a", vm.Stages[2].KeyCombo);
        Assert.Equal(2, vm.ActiveStageIndex);
    }

    [Fact]
    public async Task StartAsync_ValidationFails_WhenStagesEmptyOrNoKeysAsync()
    {
        var mockService = new MockHolderService();
        var vm = new HolderViewModel(mockService)
        {
            DelaySeconds = 0
        };

        await vm.StartAsync();

        Assert.False(vm.IsRunning);
        Assert.False(mockService.WasRunCalled);
        Assert.False(string.IsNullOrWhiteSpace(vm.StatusText));
    }

    [Fact]
    public async Task StartAsync_ExecutesPipelineAndUpdatesRunningIndexAsync()
    {
        var mockService = new MockHolderService();
        var vm = new HolderViewModel(mockService)
        {
            RepeatLoops = 3,
            DelaySeconds = 0
        };

        vm.AddStage(new KeyHolderStage("w", 5.0, 1.0));

        await vm.StartAsync();

        Assert.True(mockService.WasRunCalled);
        Assert.NotNull(mockService.LastOptions);
        Assert.Equal(3, mockService.LastOptions.RepeatLoops);
        Assert.Single(mockService.LastOptions.Stages);
        Assert.Equal(-1, vm.RunningStageIndex);
        Assert.False(vm.IsRunning);
    }
}
