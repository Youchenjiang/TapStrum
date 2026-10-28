namespace Tapster.Fluent.Tests;

using System.Threading.Tasks;
using Tapster;
using Tapster_Fluent.ViewModels;
using Xunit;

public class MacroViewModelTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        var vm = new MacroViewModel();

        Assert.Empty(vm.Actions);
        Assert.Equal(1, vm.RepeatLoops);
        Assert.Equal(1.0, vm.Speed);
        Assert.Equal(3.0, vm.DelaySeconds);
        Assert.True(vm.IgnoreMouseMove);
        Assert.False(vm.IsRecording);
        Assert.False(vm.IsRunning);
    }

    [Fact]
    public void AddDeleteClearActions_ManipulatesCollection()
    {
        var vm = new MacroViewModel();
        var action1 = new MacroAction { Type = MacroActionType.ClickLeft, X = 100, Y = 200, DelayMs = 50 };
        var action2 = new MacroAction { Type = MacroActionType.ClickRight, X = 300, Y = 400, DelayMs = 50 };

        vm.AddAction(action1);
        vm.AddAction(action2);
        Assert.Equal(2, vm.Actions.Count);

        vm.DeleteAction(0);
        Assert.Single(vm.Actions);
        Assert.Equal(MacroActionType.ClickRight, vm.Actions[0].Type);

        vm.ClearActions();
        Assert.Empty(vm.Actions);
    }

    [Fact]
    public async Task PlayAsync_ValidationFails_WhenActionsEmptyAsync()
    {
        var vm = new MacroViewModel
        {
            DelaySeconds = 0
        };

        await vm.PlayAsync();

        Assert.False(vm.IsRunning);
        Assert.False(string.IsNullOrWhiteSpace(vm.StatusText));
    }
}
