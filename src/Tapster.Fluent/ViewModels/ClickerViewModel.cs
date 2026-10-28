namespace Tapster_Fluent.ViewModels;

using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tapster;
using Tapster.Services;

/// <summary>
/// ViewModel managing the Auto-Clicker and Key-Spammer UI state and execution pipeline.
/// </summary>
public partial class ClickerViewModel : TaskViewModelBase
{
    private readonly IAutoClickerService _clickerService;
    private readonly Action<int, int>? _onPointClicked;

    [ObservableProperty]
    private double _intervalSeconds = 0.1;

    [ObservableProperty]
    private int _totalClicks = 100;

    [ObservableProperty]
    private bool _isInfiniteClicks;

    [ObservableProperty]
    private int _mouseButtonIndex;

    [ObservableProperty]
    private bool _isHoldMode;

    [ObservableProperty]
    private bool _isKeySpammer;

    [ObservableProperty]
    private string _spamKey = "space";

    [ObservableProperty]
    private bool _jitterEnabled;

    [ObservableProperty]
    private double _timeJitterPercent = 15.0;

    [ObservableProperty]
    private double _locationJitterPx;

    [ObservableProperty]
    private bool _showTargetMarker;

    [ObservableProperty]
    private double? _targetX;

    [ObservableProperty]
    private double? _targetY;

    public ClickerViewModel(
        IAutoClickerService? clickerService = null,
        PanicDetector? panicDetector = null,
        Action<int, int>? onPointClicked = null,
        Action<Action>? dispatcher = null)
        : base(panicDetector, dispatcher)
    {
        _clickerService = clickerService ?? new AutoClickerService();
        _onPointClicked = onPointClicked;
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

        if (IsKeySpammer && string.IsNullOrWhiteSpace(SpamKey))
        {
            StatusText = LocalizationManager.Get("Clicker_ValidationSpamKey");
            return;
        }

        await RunAutomationWorkflowAsync(ExecuteClickerCoreAsync);
    }

    private async Task ExecuteClickerCoreAsync(CancellationToken token)
    {
        string button = MouseButtonIndex switch
        {
            1 => "right",
            2 => "middle",
            _ => "left"
        };

        int intervalMs = Math.Max(5, (int)(IntervalSeconds * 1000));
        int effectiveClicks = IsInfiniteClicks ? 0 : Math.Max(0, TotalClicks);

        int? x = TargetX.HasValue && !double.IsNaN(TargetX.Value) ? (int)TargetX.Value : null;
        int? y = TargetY.HasValue && !double.IsNaN(TargetY.Value) ? (int)TargetY.Value : null;

        var options = new AutoClickerOptions
        {
            IsSpammer = IsKeySpammer,
            SpamKey = SpamKey.Trim(),
            MouseButton = button,
            IntervalMs = intervalMs,
            TotalClicks = effectiveClicks,
            TargetX = x,
            TargetY = y,
            JitterEnabled = JitterEnabled,
            TimeJitterPct = TimeJitterPercent,
            LocationJitterPx = LocationJitterPx,
            IsHoldMode = IsHoldMode,
            HotkeyVk = 0x75
        };

        await _clickerService.RunAsync(
            options,
            token,
            reportProgress: UpdateProgress,
            onPointClicked: HandlePointClicked,
            panicDetector: PanicDetectorInstance);
    }

    private void HandlePointClicked(int px, int py)
    {
        if (ShowTargetMarker && !IsKeySpammer && _onPointClicked is not null)
        {
            RunOnUIThread(() => _onPointClicked(px, py));
        }
    }

    [RelayCommand]
    public void ClearCoordinates()
    {
        this.TargetX = null;
        this.TargetY = null;
    }

    public void SetCoordinates(int x, int y)
    {
        this.TargetX = x;
        this.TargetY = y;
    }
}
