using System;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using TapStrum;
using WinRT;

namespace TapStrum.Fluent;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
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
                MainWindow.Instance?.DispatcherQueue.TryEnqueue(() =>
                    MainWindow.Instance?.BringToFront()));

            ComWrappersSupport.InitializeComWrappers();
            Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });
        }
        catch (Exception ex)
        {
            App.LogCrash("Fatal program startup failure: " + ex);
            throw;
        }
    }
}
