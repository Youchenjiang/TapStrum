using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tapster.Services;

/// <summary>
/// Default implementation of <see cref="IAutoTyperService"/> supporting both keystroke emulation
/// and instant clipboard paste with automatic clipboard preservation and trailing key execution.
/// </summary>
public sealed class AutoTyperService : IAutoTyperService
{
    private readonly Action<char> _typeAction;
    private readonly Action<string> _tapAction;
    private readonly Action _pasteAction;
    private readonly Func<bool> _isEscPressedFunc;
    private readonly Action<int, PanicDetector?, CancellationToken> _sleepAction;

    /// <summary>
    /// Initializes a new instance of <see cref="AutoTyperService"/> with standard hardware simulation drivers.
    /// </summary>
    public AutoTyperService()
        : this(
            typeAction: Keyboard.Type,
            tapAction: Keyboard.Tap,
            pasteAction: Keyboard.Paste,
            isEscPressedFunc: Keyboard.IsEscPressed,
            sleepAction: PanicSafetyHelper.SleepWithPanicCheck)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AutoTyperService"/> with custom delegates for testing.
    /// </summary>
    public AutoTyperService(
        Action<char> typeAction,
        Action<string> tapAction,
        Action pasteAction,
        Func<bool> isEscPressedFunc,
        Action<int, PanicDetector?, CancellationToken> sleepAction)
    {
        _typeAction = typeAction ?? throw new ArgumentNullException(nameof(typeAction));
        _tapAction = tapAction ?? throw new ArgumentNullException(nameof(tapAction));
        _pasteAction = pasteAction ?? throw new ArgumentNullException(nameof(pasteAction));
        _isEscPressedFunc = isEscPressedFunc ?? throw new ArgumentNullException(nameof(isEscPressedFunc));
        _sleepAction = sleepAction ?? throw new ArgumentNullException(nameof(sleepAction));
    }

    /// <inheritdoc/>
    public async Task RunAsync(
        AutoTyperOptions options,
        CancellationToken cancellationToken,
        Action<string, double>? reportProgress = null,
        IClipboardService? clipboardService = null,
        PanicDetector? panicDetector = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrEmpty(options.Text))
        {
            throw new InvalidOperationException("No text to type!");
        }

        if (options.Mode == AutoTyperMode.Clipboard)
        {
            await RunClipboardAsync(options, reportProgress, clipboardService, panicDetector, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await RunKeystrokesAsync(options, reportProgress, panicDetector, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RunClipboardAsync(
        AutoTyperOptions options,
        Action<string, double>? reportProgress,
        IClipboardService? clipboardService,
        PanicDetector? panicDetector,
        CancellationToken cancellationToken)
    {
        string text = options.Text;
        string? previousText = null;

        if (clipboardService is not null)
        {
            try
            {
                previousText = await clipboardService.GetTextAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore failure reading previous clipboard content
            }

            await clipboardService.SetTextAsync(text).ConfigureAwait(false);
        }

        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                PanicSafetyHelper.CheckPanicSafety(panicDetector);
                reportProgress?.Invoke(
                    string.Format(LocalizationManager.Get("Typer_PastingClipboard"), text.Length),
                    50);

                _pasteAction();

                cancellationToken.ThrowIfCancellationRequested();
                PanicSafetyHelper.CheckPanicSafety(panicDetector);

                ApplyTrailingKey(options.TrailingKey, cancellationToken);
                reportProgress?.Invoke(
                    string.Format(LocalizationManager.Get("Typer_PastedClipboard"), text.Length),
                    100);
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (clipboardService is not null && !string.IsNullOrEmpty(previousText))
            {
                await Task.Delay(150, CancellationToken.None).ConfigureAwait(false);
                try
                {
                    await clipboardService.SetTextAsync(previousText).ConfigureAwait(false);
                }
                catch
                {
                    // Ignore failure to restore clipboard
                }
            }
        }
    }

    private async Task RunKeystrokesAsync(
        AutoTyperOptions options,
        Action<string, double>? reportProgress,
        PanicDetector? panicDetector,
        CancellationToken cancellationToken)
    {
        string text = options.Text;
        int intervalMs = Math.Max(0, options.IntervalMs);

        await Task.Run(() =>
        {
            for (int i = 0; i < text.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PanicSafetyHelper.CheckPanicSafety(panicDetector);
                _typeAction(text[i]);

                int charIndex = i + 1;
                reportProgress?.Invoke(
                    string.Format(LocalizationManager.Get("Typer_TypingChar"), charIndex, text.Length),
                    (double)charIndex / text.Length * 100);

                int sleepMs = options.JitterEnabled && intervalMs > 0
                    ? JitterHelper.ApplyTimeJitter(intervalMs, 15, minIntervalMs: 1)
                    : intervalMs;

                if (sleepMs > 0)
                {
                    _sleepAction(sleepMs, panicDetector, cancellationToken);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            PanicSafetyHelper.CheckPanicSafety(panicDetector);
            ApplyTrailingKey(options.TrailingKey, cancellationToken);
        }, cancellationToken).ConfigureAwait(false);
    }

    private void ApplyTrailingKey(string trailingKey, CancellationToken token)
    {
        if (token.IsCancellationRequested || _isEscPressedFunc())
        {
            return;
        }

        if (string.Equals(trailingKey, "Enter", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trailingKey, "Tab", StringComparison.OrdinalIgnoreCase))
        {
            Thread.Sleep(50);
            if (!token.IsCancellationRequested && !_isEscPressedFunc())
            {
                _tapAction(trailingKey.ToLowerInvariant());
            }
        }
    }
}
