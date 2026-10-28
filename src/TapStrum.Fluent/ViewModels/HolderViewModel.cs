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
/// ViewModel managing the Key-Holder pipeline stages, loop iterations, and execution state.
/// </summary>
public partial class HolderViewModel : TaskViewModelBase
{
    private readonly IKeyHolderService _holderService;

    public ObservableCollection<KeyHolderStage> Stages { get; } = new();

    [ObservableProperty]
    private int _activeStageIndex;

    [ObservableProperty]
    private int _runningStageIndex = -1;

    [ObservableProperty]
    private int _repeatLoops = 1;

    public HolderViewModel(
        IKeyHolderService? holderService = null,
        PanicDetector? panicDetector = null,
        Action<Action>? dispatcher = null)
        : base(panicDetector, dispatcher)
    {
        _holderService = holderService ?? new KeyHolderService();
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

        RunningStageIndex = -1;
        await RunAutomationWorkflowAsync(ExecuteHolderCoreAsync);
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
            panicDetector: PanicDetectorInstance);
    }

    public override void Stop(string? message = null)
    {
        base.Stop(message);
        RunningStageIndex = -1;
        Keyboard.ReleaseAllModifiers();
    }

    public void AddStage(KeyHolderStage stage)
    {
        Stages.Add(stage);
        ActiveStageIndex = Stages.Count - 1;
    }

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

    public void ClearStageKeys(int index)
    {
        if (index >= 0 && index < Stages.Count)
        {
            Stages[index].KeyCombo = string.Empty;
        }
    }
}
