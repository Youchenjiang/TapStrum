using System;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Tapster;
using WinRT;

namespace Tapster_Fluent;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var singleInstanceService = new SingleInstanceService();
        if (!singleInstanceService.TryAcquire())
        {
            // Another instance is already running; wake it up and exit cleanly
            SingleInstanceService.SignalWake();
            return;
        }

        // Listen for wake signals from secondary instances or the launcher
        singleInstanceService.StartWakeListener(() =>
        {
            MainWindow.Instance?.DispatcherQueue.TryEnqueue(() =>
            {
                MainWindow.Instance?.BringToFront();
            });
        });

        ComWrappersSupport.InitializeComWrappers();
        Application.Start((p) =>
        {
            var context = new DispatcherQueueSynchronizationContext(
                DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}
