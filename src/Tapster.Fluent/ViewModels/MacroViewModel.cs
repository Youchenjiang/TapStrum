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
/// ViewModel managing the Macro Recorder/Replay UI state, actions list, and marker overlay coordination.
/// </summary>
public partial class MacroViewModel : TaskViewModelBase
{
    private readonly MacroRecorder _recorder;
    private readonly Action<List<MarkerPoint>>? _setMarkersCallback;
    private readonly Action? _clearMarkersCallback;
    private readonly Action<int, int, int>? _onMarkerRippleCallback;

    public ObservableCollection<MacroAction> Actions { get; } = new();

    [ObservableProperty]
    private int _repeatLoops = 1;

    [ObservableProperty]
    private double _speed = 1.0;

    [ObservableProperty]
    private bool _ignoreMouseMove = true;

    [ObservableProperty]
    private bool _isRecording;

    public MacroViewModel(
        MacroRecorder? recorder = null,
        PanicDetector? panicDetector = null,
        Action<List<MarkerPoint>>? setMarkersCallback = null,
        Action? clearMarkersCallback = null,
        Action<int, int, int>? onMarkerRippleCallback = null,
        Action<Action>? dispatcher = null)
        : base(panicDetector, dispatcher)
    {
        _recorder = recorder ?? new MacroRecorder();
        _setMarkersCallback = setMarkersCallback;
        _clearMarkersCallback = clearMarkersCallback;
        _onMarkerRippleCallback = onMarkerRippleCallback;
    }

    [RelayCommand]
    public void StartRecording()
    {
        if (IsRunning || IsRecording)
        {
            return;
        }

        IsRecording = true;
        StatusText = LocalizationManager.Get("Macro_RecordingStatus");
        _recorder.StartRecording(ignoreMouseMove: IgnoreMouseMove);
    }

    [RelayCommand]
    public void StopRecording()
    {
        if (!IsRecording)
        {
            return;
        }

        IsRecording = false;
        _recorder.StopRecording();
        StatusText = LocalizationManager.Get("Macro_RecordingStopped");

        Actions.Clear();
        foreach (var action in _recorder.Actions)
        {
            Actions.Add(action);
        }
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

        _recorder.Clear();
        foreach (var action in Actions)
        {
            _recorder.InsertAction(_recorder.Actions.Count, action);
        }

        try
        {
            await RunAutomationWorkflowAsync(ExecuteMacroCoreAsync);
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
                    PanicDetectorInstance.SyncProgrammaticPosition(action.X, action.Y);
                }
                else if (action.Type is MacroActionType.MouseMove)
                {
                    PanicDetectorInstance.SyncProgrammaticPosition(action.X, action.Y);
                }
            },
            checkPanicCallback: () =>
            {
                if (PanicDetectorInstance.CheckPanic(out var reason))
                {
                    throw new OperationCanceledException(reason ?? PanicDetector.EmergencyPanicMessage);
                }
            });
    }

    public override void Stop(string? message = null)
    {
        base.Stop(message);
        IsRecording = false;
        _recorder.StopRecording();
    }

    public void AddAction(MacroAction action)
    {
        Actions.Add(action);
    }

    public void DeleteAction(int index)
    {
        if (index >= 0 && index < Actions.Count)
        {
            Actions.RemoveAt(index);
        }
    }

    public void ClearActions()
    {
        Actions.Clear();
        _recorder.Clear();
    }
}
