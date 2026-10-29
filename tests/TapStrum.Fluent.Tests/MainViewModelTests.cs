namespace TapStrum.Fluent.Tests;

using TapStrum.Fluent.ViewModels;
using Xunit;

public class MainViewModelTests
{
    [Fact]
    public void Constructor_InitializesChildViewModelsAndNavigation()
    {
        var vm = new MainViewModel();

        Assert.Equal(MainViewModel.TabTyper, vm.SelectedNavigationTag);
        Assert.NotNull(vm.Clicker);
        Assert.NotNull(vm.Holder);
        Assert.NotNull(vm.Typer);
        Assert.NotNull(vm.Macro);
        Assert.NotNull(vm.Settings);
        Assert.False(vm.IsAnyTaskRunning);
        Assert.Null(vm.RunningTaskName);
    }

    [Fact]
    public void StopAll_StopsAnyRunningChildTasks()
    {
        var vm = new MainViewModel();

        vm.Clicker.IsRunning = true;
        Assert.True(vm.IsAnyTaskRunning);
        Assert.Equal(MainViewModel.TabClicker, vm.RunningTaskName);

        vm.StopAll("Manual panic");

        Assert.False(vm.Clicker.IsRunning);
        Assert.False(vm.IsAnyTaskRunning);
        Assert.Null(vm.RunningTaskName);
        Assert.Equal("Manual panic", vm.GlobalStatusMessage);
    }
}
