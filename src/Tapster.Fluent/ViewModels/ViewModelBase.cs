namespace Tapster_Fluent.ViewModels;

using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Tapster;

/// <summary>
/// Base class for all Tapster ViewModels providing thread dispatching, localization, and shared execution helpers.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    private readonly Action<Action> _dispatcher;

    protected ViewModelBase(Action<Action>? dispatcher = null)
    {
        _dispatcher = dispatcher ?? (action => action());
    }

    /// <summary>
    /// Executes the specified action on the UI thread or inline when in unit tests.
    /// </summary>
    /// <param name="action">Action to dispatch.</param>
    protected void RunOnUIThread(Action action)
    {
        if (action is null)
        {
            return;
        }

        _dispatcher(action);
    }

    /// <summary>
    /// Executes a preflight countdown before task execution, supporting cancellation and panic detection.
    /// </summary>
    protected static async Task RunCountdownAsync(
        double delaySeconds,
        Action<string, double> updateProgress,
        PanicDetector? panicDetector,
        CancellationToken cancellationToken)
    {
        if (delaySeconds <= 0)
        {
            return;
        }

        int totalSeconds = (int)Math.Ceiling(delaySeconds);
        for (int remaining = totalSeconds; remaining > 0; remaining--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (panicDetector is not null && panicDetector.CheckPanic(out var reason))
            {
                throw new OperationCanceledException(reason ?? PanicDetector.EmergencyPanicMessage);
            }

            double percentage = (totalSeconds - remaining) / (double)totalSeconds * 100.0;
            string message = string.Format(LocalizationManager.Get("Common_StartingIn"), remaining);
            updateProgress(message, percentage);
            await Task.Delay(1000, cancellationToken);
        }
    }

    /// <summary>
    /// Formats task cancellation messages with special handling for panic triggers.
    /// </summary>
    protected static string FormatCancellationMessage(OperationCanceledException ex)
    {
        if (ex.Message.Contains("panic", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("Esc", StringComparison.OrdinalIgnoreCase))
        {
            return ex.Message;
        }

        return LocalizationManager.Get("Common_TaskCanceled");
    }
}
