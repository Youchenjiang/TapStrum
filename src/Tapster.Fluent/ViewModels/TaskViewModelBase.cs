namespace Tapster_Fluent.ViewModels;

using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tapster;

/// <summary>
/// Abstract base class for runnable automation ViewModels that manage countdown, cancellation, and progress state.
/// </summary>
public abstract partial class TaskViewModelBase : ViewModelBase
{
    protected readonly PanicDetector PanicDetectorInstance;
    protected CancellationTokenSource? Cts;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private double _delaySeconds = 3.0;

    protected TaskViewModelBase(
        PanicDetector? panicDetector = null,
        Action<Action>? dispatcher = null)
        : base(dispatcher)
    {
        PanicDetectorInstance = panicDetector ?? new PanicDetector();
    }

    [RelayCommand]
    public virtual void Stop(string? message = null)
    {
        Cts?.Cancel();
        Cts?.Dispose();
        Cts = null;

        IsRunning = false;
        ProgressPercentage = 0;

        if (message is not null)
        {
            StatusText = message;
        }
        else if (string.IsNullOrEmpty(StatusText) || StatusText == LocalizationManager.Get("Common_Running"))
        {
            StatusText = LocalizationManager.Get("Common_StoppedByUser");
        }
    }

    protected void UpdateProgress(string msg, double pct) =>
        RunOnUIThread(() =>
        {
            StatusText = msg;
            if (pct >= 0)
            {
                ProgressPercentage = Math.Min(100, Math.Max(0, pct));
            }
        });

    protected async Task RunAutomationWorkflowAsync(Func<CancellationToken, Task> taskBody)
    {
        IsRunning = true;
        ProgressPercentage = 0;
        StatusText = LocalizationManager.Get("Common_Running");

        Cts = new CancellationTokenSource();

        await ExecuteAutomationTaskAsync(
            DelaySeconds,
            PanicDetectorInstance,
            Cts,
            taskBody,
            UpdateProgress,
            Stop);
    }
}
