namespace Tapster_Fluent.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tapster;

/// <summary>
/// ViewModel managing Macro recording, editing, and replay playback.
/// </summary>
public partial class MacroViewModel : ViewModelBase
{
    private readonly MacroRecorder _recorder;
    private readonly PanicDetector _panicDetector;
    private readonly Action<IReadOnlyList<MarkerPoint>>? _setMarkersCallback;
    private readonly Action<int, int, int>? _onMarkerRippleCallback;
    private readonly Action? _clearMarkersCallback;
    private CancellationTokenSource? _cts;

    public ObservableCollection<MacroAction> Actions { get; } = new();

    [ObservableProperty]
    private int _repeatLoops = 1;

    [ObservableProperty]
    private double _speed = 1.0;

    [ObservableProperty]
    private double _delaySeconds = 3.0;

    [ObservableProperty]
    private bool _ignoreMouseMove = true;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private double _progressPercentage;

    public MacroViewModel(
        MacroRecorder? recorder = null,
        PanicDetector? panicDetector = null,
        Action<IReadOnlyList<MarkerPoint>>? setMarkersCallback = null,
        Action<int, int, int>? onMarkerRippleCallback = null,
        Action? clearMarkersCallback = null,
        Action<Action>? dispatcher = null)
        : base(dispatcher)
    {
        _recorder = recorder ?? new MacroRecorder();
        _panicDetector = panicDetector ?? new PanicDetector();
        _setMarkersCallback = setMarkersCallback;
        _onMarkerRippleCallback = onMarkerRippleCallback;
        _clearMarkersCallback = clearMarkersCallback;
    }

    [RelayCommand]
    public void StartRecording()
    {
        if (IsRunning || IsRecording)
        {
            return;
        }

        IsRecording = true;
        StatusText = LocalizationManager.Get("Macro_Recording");
        _recorder.StartRecording(null, IgnoreMouseMove);
    }

    [RelayCommand]
    public void StopRecording()
    {
        if (!IsRecording)
        {
            return;
        }

        _recorder.StopRecording();
        IsRecording = false;

        Actions.Clear();
        foreach (var action in _recorder.Actions)
        {
            Actions.Add(action);
        }

        StatusText = string.Format(LocalizationManager.Get("Macro_ActionsCount"), Actions.Count);
    }

    [RelayCommand]
    public async Task TogglePlayAsync()
    {
        if (IsRunning)
        {
            Stop();
        }
        else
        {
            await PlayAsync();
        }
    }

    [RelayCommand]
    public async Task PlayAsync()
    {
        if (IsRunning || IsRecording)
        {
            return;
        }

        if (Actions.Count == 0)
        {
            StatusText = LocalizationManager.Get("Macro_ValidationNoActions");
            return;
        }

        // Sync actions into recorder if modified
        _recorder.Clear();
        foreach (var action in Actions)
        {
            _recorder.InsertAction(_recorder.Actions.Count, action);
        }

        IsRunning = true;
        ProgressPercentage = 0;
        StatusText = LocalizationManager.Get("Common_Running");

        _cts = new CancellationTokenSource();

        try
        {
            await ExecuteAutomationTaskAsync(
                DelaySeconds,
                _panicDetector,
                _cts,
                ExecuteMacroCoreAsync,
                UpdateProgress,
                Stop);
        }
        finally
        {
            if (_clearMarkersCallback is not null)
            {
                RunOnUIThread(_clearMarkersCallback);
            }
        }
    }

    private async Task ExecuteMacroCoreAsync(CancellationToken token)
    {
        var clickPoints = Actions
            .Where(act => act.Type is MacroActionType.ClickLeft or MacroActionType.ClickRight or MacroActionType.ClickMiddle)
            .Select((act, idx) => new MarkerPoint(idx + 1, act.X, act.Y))
            .ToList();

        if (_setMarkersCallback is not null)
        {
            RunOnUIThread(() => _setMarkersCallback(clickPoints));
        }

        int currentClickInLoop = 0;

        await _recorder.ReplayAsync(
            RepeatLoops,
            Speed,
            token,
            progressCallback: (loop, step) =>
            {
                if (step == 1)
                {
                    currentClickInLoop = 0;
                }

                double pct = (double)step / Actions.Count * 100.0;
                string msg = string.Format(LocalizationManager.Get("Macro_ReplayingProgress"), loop, step, Actions.Count);
                UpdateProgress(msg, pct);
            },
            actionExecutingCallback: action =>
            {
                if (action.Type is MacroActionType.ClickLeft or MacroActionType.ClickRight or MacroActionType.ClickMiddle)
                {
                    currentClickInLoop++;
                    int thisClick = currentClickInLoop;
                    if (_onMarkerRippleCallback is not null)
                    {
                        RunOnUIThread(() => _onMarkerRippleCallback(action.X, action.Y, thisClick));
                    }
                    _panicDetector.SyncProgrammaticPosition(action.X, action.Y);
                }
                else if (action.Type is MacroActionType.MouseMove)
                {
                    _panicDetector.SyncProgrammaticPosition(action.X, action.Y);
                }
            },
            checkPanicCallback: () =>
            {
                if (_panicDetector.CheckPanic(out var reason))
                {
                    throw new OperationCanceledException(reason ?? PanicDetector.EmergencyPanicMessage);
                }
            });
    }

    private void UpdateProgress(string msg, double pct) =>
        RunOnUIThread(() =>
        {
            StatusText = msg;
            if (pct >= 0)
            {
                ProgressPercentage = Math.Min(100, Math.Max(0, pct));
            }
        });

    [RelayCommand]
    public void Stop(string? message = null)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

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

    [RelayCommand]
    public void ClearActions()
    {
        Actions.Clear();
        _recorder.Clear();
        StatusText = string.Empty;
    }

    [RelayCommand]
    public void DeleteAction(int index)
    {
        if (index >= 0 && index < Actions.Count)
        {
            Actions.RemoveAt(index);
            _recorder.RemoveActionAt(index);
        }
    }

    public void AddAction(MacroAction action)
    {
        if (action is not null)
        {
            Actions.Add(action);
            _recorder.InsertAction(_recorder.Actions.Count, action);
        }
    }
}
