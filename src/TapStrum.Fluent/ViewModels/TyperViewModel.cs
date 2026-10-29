namespace TapStrum.Fluent.ViewModels;

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TapStrum;
using TapStrum.Services;

/// <summary>
/// ViewModel managing the Auto-Typer UI state, text history, and clipboard/keystroke execution.
/// </summary>
public partial class TyperViewModel : TaskViewModelBase
{
    private const string KeyEnter = "enter";
    private const string KeyTab = "tab";
    private const string KeyNone = "none";

    private readonly IAutoTyperService _typerService;
    private readonly IClipboardService? _clipboardService;

    public ObservableCollection<string> History { get; } = new();

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private bool _isClipboardMode;

    [ObservableProperty]
    private int _trailingKeyIndex;

    [ObservableProperty]
    private double _intervalSeconds = 0.03;

    [ObservableProperty]
    private bool _jitterEnabled;

    public TyperViewModel(
        IAutoTyperService? typerService = null,
        IClipboardService? clipboardService = null,
        PanicDetector? panicDetector = null,
        Action<Action>? dispatcher = null)
        : base(panicDetector, dispatcher)
    {
        _typerService = typerService ?? new AutoTyperService();
        _clipboardService = clipboardService;
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

        if (string.IsNullOrWhiteSpace(Text))
        {
            StatusText = LocalizationManager.Get("Typer_ValidationEmpty");
            return;
        }

        await RunAutomationWorkflowAsync(ExecuteTyperCoreAsync);
    }

    private async Task ExecuteTyperCoreAsync(CancellationToken token)
    {
        string trailingKey = TrailingKeyIndex switch
        {
            1 => KeyEnter,
            2 => KeyTab,
            _ => KeyNone
        };

        int intervalMs = Math.Max(5, (int)(IntervalSeconds * 1000));
        var options = new AutoTyperOptions
        {
            Text = Text,
            Mode = IsClipboardMode ? AutoTyperMode.Clipboard : AutoTyperMode.Keystroke,
            IntervalMs = intervalMs,
            JitterEnabled = JitterEnabled,
            TrailingKey = trailingKey
        };

        await _typerService.RunAsync(
            options,
            token,
            reportProgress: UpdateProgress,
            clipboardService: _clipboardService,
            panicDetector: PanicDetectorInstance);

        RecordHistory(trailingKey);
    }

    private void RecordHistory(string trailingKey) =>
        RunOnUIThread(() =>
        {
            string modeDesc = IsClipboardMode ? "Pasted" : "Typed";
            string summary = $"{DateTime.UtcNow:HH:mm:ss} - {modeDesc} {Text.Length} chars" +
                             (trailingKey != KeyNone ? $" + [{trailingKey}]" : "");
            History.Insert(0, summary);
        });

    [RelayCommand]
    public void ClearText()
    {
        Text = string.Empty;
    }

    [RelayCommand]
    public async Task PasteTextAsync()
    {
        if (_clipboardService is not null)
        {
            var pasted = await _clipboardService.GetTextAsync();
            if (!string.IsNullOrEmpty(pasted))
            {
                Text = pasted;
            }
        }
    }
}
