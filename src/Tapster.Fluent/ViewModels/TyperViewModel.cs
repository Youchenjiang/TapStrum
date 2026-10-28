namespace Tapster_Fluent.ViewModels;

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tapster;
using Tapster.Services;

/// <summary>
/// ViewModel managing the Auto-Typer UI state, input history, and typing pipeline.
/// </summary>
public partial class TyperViewModel : ViewModelBase
{
    private const string KeyNone = "none";
    private const string KeyEnter = "enter";
    private const string KeyTab = "tab";

    private readonly IAutoTyperService _typerService;
    private readonly IClipboardService? _clipboardService;
    private readonly PanicDetector _panicDetector;
    private CancellationTokenSource? _cts;

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

    [ObservableProperty]
    private double _delaySeconds = 3.0;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private double _progressPercentage;

    public ObservableCollection<string> History { get; } = new();

    public TyperViewModel(
        IAutoTyperService? typerService = null,
        IClipboardService? clipboardService = null,
        PanicDetector? panicDetector = null,
        Action<Action>? dispatcher = null)
        : base(dispatcher)
    {
        _typerService = typerService ?? new AutoTyperService();
        _clipboardService = clipboardService;
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

        if (string.IsNullOrWhiteSpace(Text))
        {
            StatusText = LocalizationManager.Get("Typer_ValidationEmpty");
            return;
        }

        IsRunning = true;
        ProgressPercentage = 0;
        StatusText = LocalizationManager.Get("Common_Running");

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _panicDetector.Reset();

        try
        {
            await RunCountdownAsync(DelaySeconds, (msg, pct) =>
            {
                RunOnUIThread(() =>
                {
                    StatusText = msg;
                    ProgressPercentage = pct;
                });
            }, _panicDetector, token);

            RunOnUIThread(() =>
            {
                ProgressPercentage = 100;
                StatusText = LocalizationManager.Get("Common_Running");
            });

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
                reportProgress: (msg, pct) => RunOnUIThread(() =>
                {
                    StatusText = msg;
                    if (pct >= 0)
                    {
                        ProgressPercentage = Math.Min(100, Math.Max(0, pct));
                    }
                }),
                clipboardService: _clipboardService,
                panicDetector: _panicDetector);

            RunOnUIThread(() =>
            {
                string modeDesc = IsClipboardMode ? "Pasted" : "Typed";
                string summary = $"{DateTime.UtcNow:HH:mm:ss} - {modeDesc} {Text.Length} chars" +
                                 (trailingKey != KeyNone ? $" + [{trailingKey}]" : "");
                History.Insert(0, summary);
            });

            Stop(LocalizationManager.Get("Common_Finished"));
        }
        catch (OperationCanceledException ex)
        {
            Stop(FormatCancellationMessage(ex));
        }
        catch (Exception ex)
        {
            Stop(string.Format(LocalizationManager.Get("Common_Error"), ex.Message));
        }
    }

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
