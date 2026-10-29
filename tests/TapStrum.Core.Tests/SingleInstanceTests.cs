using System;
using System.Threading.Tasks;
using TapStrum;
using Xunit;

namespace TapStrum.Core.Tests;

public class SingleInstanceTests
{
    [Fact]
    public void TryAcquire_FirstInstance_AcquiresSuccessfully()
    {
        string uniqueMutex = $@"Local\TapStrum_Test_Mutex_{Guid.NewGuid():N}";
        using var service = new SingleInstanceService(uniqueMutex);

        bool acquired = service.TryAcquire();

        Assert.True(acquired);
        Assert.True(service.OwnsMutex);
    }

    [Fact]
    public void TryAcquire_SecondInstance_ReturnsFalse()
    {
        string uniqueMutex = $@"Local\TapStrum_Test_Mutex_{Guid.NewGuid():N}";
        using var first = new SingleInstanceService(uniqueMutex);
        using var second = new SingleInstanceService(uniqueMutex);

        bool firstAcquired = first.TryAcquire();
        bool secondAcquired = second.TryAcquire();

        Assert.True(firstAcquired);
        Assert.False(secondAcquired);
        Assert.False(second.OwnsMutex);
    }

    [Fact]
    public void Dispose_FirstInstance_AllowsSecondInstanceToAcquire()
    {
        string uniqueMutex = $@"Local\TapStrum_Test_Mutex_{Guid.NewGuid():N}";
        var first = new SingleInstanceService(uniqueMutex);
        using var second = new SingleInstanceService(uniqueMutex);

        Assert.True(first.TryAcquire());
        first.Dispose();

        Assert.True(second.TryAcquire());
        Assert.True(second.OwnsMutex);
    }

    [Fact]
    public async Task SignalWake_WhenListenerActive_TriggersCallbackAsync()
    {
        string uniqueMutex = $@"Local\TapStrum_Test_Mutex_{Guid.NewGuid():N}";
        string uniqueEvent = $@"Local\TapStrum_Test_Event_{Guid.NewGuid():N}";

        using var service = new SingleInstanceService(uniqueMutex, uniqueEvent);
        Assert.True(service.TryAcquire());

        var tcs = new TaskCompletionSource<bool>();
        service.StartWakeListener(() => tcs.TrySetResult(true));

        bool signaled = SingleInstanceService.SignalWake(uniqueEvent);
        Assert.True(signaled);

        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(3000));
        Assert.Same(tcs.Task, completedTask);
        Assert.True(await tcs.Task);
    }

    [Fact]
    public void SignalWake_WhenNoListenerActive_ReturnsFalse()
    {
        string nonExistentEvent = $@"Local\TapStrum_NonExistent_{Guid.NewGuid():N}";

        bool signaled = SingleInstanceService.SignalWake(nonExistentEvent);

        Assert.False(signaled);
    }
}
