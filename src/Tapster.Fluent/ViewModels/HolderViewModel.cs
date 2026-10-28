namespace Tapster_Fluent.ViewModels;

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tapster;
using Tapster.Services;

/// <summary>
/// ViewModel managing the multi-stage Key-Holder sequence pipeline and execution state.
/// </summary>
public partial class HolderViewModel : ViewModelBase
{
    private readonly IKeyHolderService _holderService;
    private readonly PanicDetector _panicDetector;
    private CancellationTokenSource? _cts;

    public ObservableCollection<KeyHolderStage> Stages { get; } = new();

    [ObservableProperty]
    private int _activeStageIndex;

    [ObservableProperty]
    private int _runningStageIndex = -1;

    [ObservableProperty]
    private int _repeatLoops = 1;

    [ObservableProperty]
    private double _delaySeconds = 3.0;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private double _progressPercentage;

    public HolderViewModel(
        IKeyHolderService? holderService = null,
        PanicDetector? panicDetector = null,
        Action<Action>? dispatcher = null)
        : base(dispatcher)
    {
        _holderService = holderService ?? new KeyHolderService();
        _panicDetector = panicDetector ?? new PanicDetector();
    }

    [RelayCommand]
    public async Task ToggleAsync()
    {
        if (IsRunning)
        {
            Stop();
        }
        else
        {
            await StartAsync();
        }
    }

    [RelayCommand]
    public async Task StartAsync()
    {
        if (IsRunning)
        {
            return;
        }

        if (Stages.Count == 0 || Stages.All(s => string.IsNullOrWhiteSpace(s.KeyCombo)))
        {
            StatusText = LocalizationManager.Get("KeyHolder_ValidationNoKeys");
            return;
        }

        IsRunning = true;
        RunningStageIndex = -1;
        ProgressPercentage = 0;
        StatusText = LocalizationManager.Get("Common_Running");

        _cts = new CancellationTokenSource();

        await ExecuteAutomationTaskAsync(
            DelaySeconds,
            _panicDetector,
            _cts,
            ExecuteHolderCoreAsync,
            UpdateProgress,
            Stop);
    }

    private async Task ExecuteHolderCoreAsync(CancellationToken token)
    {
        int loops = Math.Max(1, RepeatLoops);
        var options = new KeyHolderOptions
        {
            Stages = Stages.ToList(),
            RepeatLoops = loops
        };

        await _holderService.RunAsync(
            options,
            token,
            reportProgress: UpdateProgress,
            onStageStarted: stageIdx => RunOnUIThread(() => RunningStageIndex = stageIdx),
            onPipelineCompleted: () => RunOnUIThread(() => RunningStageIndex = -1),
            panicDetector: _panicDetector);
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
        RunningStageIndex = -1;
        ProgressPercentage = 0;
        Keyboard.ReleaseAllModifiers();

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
    public void AddStage(KeyHolderStage? stage = null)
    {
        var newStage = stage ?? new KeyHolderStage();
        Stages.Add(newStage);
        ActiveStageIndex = Stages.Count - 1;
    }

    [RelayCommand]
    public void RemoveStage(int index)
    {
        if (index >= 0 && index < Stages.Count)
        {
            Stages.RemoveAt(index);
            if (ActiveStageIndex >= Stages.Count)
            {
                ActiveStageIndex = Math.Max(0, Stages.Count - 1);
            }
        }
    }

    [RelayCommand]
    public void ClearStageKeys(int index)
    {
        if (index >= 0 && index < Stages.Count)
        {
            Stages[index].KeyCombo = string.Empty;
        }
    }
}
