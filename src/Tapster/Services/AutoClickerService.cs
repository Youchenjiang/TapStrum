using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tapster.Services;

/// <summary>
/// Default implementation of <see cref="IAutoClickerService"/> providing precise timing,
/// spatial and temporal jitter, safety panic monitoring, and hold-to-click detection.
/// </summary>
public sealed class AutoClickerService : IAutoClickerService
{
    private readonly Action<int, int, string> _clickAtAction;
    private readonly Action<string> _clickAction;
    private readonly Func<(int X, int Y)> _getPositionFunc;
    private readonly Action<string> _spamAction;
    private readonly Func<int, bool> _isKeyDownFunc;
    private readonly Action<int, PanicDetector?, CancellationToken> _sleepAction;

    /// <summary>
    /// Initializes a new instance of <see cref="AutoClickerService"/> with standard hardware simulation drivers.
    /// </summary>
    public AutoClickerService()
        : this(
            clickAtAction: Mouse.ClickAt,
            clickAction: Mouse.Click,
            getPositionFunc: Mouse.GetPosition,
            spamAction: KeySpammer.Spam,
            isKeyDownFunc: KeySpammer.IsKeyDown,
            sleepAction: PanicSafetyHelper.SleepWithPanicCheck)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AutoClickerService"/> with custom simulation delegates for testing.
    /// </summary>
    public AutoClickerService(
        Action<int, int, string> clickAtAction,
        Action<string> clickAction,
        Func<(int X, int Y)> getPositionFunc,
        Action<string> spamAction,
        Func<int, bool> isKeyDownFunc,
        Action<int, PanicDetector?, CancellationToken> sleepAction)
    {
        _clickAtAction = clickAtAction ?? throw new ArgumentNullException(nameof(clickAtAction));
        _clickAction = clickAction ?? throw new ArgumentNullException(nameof(clickAction));
        _getPositionFunc = getPositionFunc ?? throw new ArgumentNullException(nameof(getPositionFunc));
        _spamAction = spamAction ?? throw new ArgumentNullException(nameof(spamAction));
        _isKeyDownFunc = isKeyDownFunc ?? throw new ArgumentNullException(nameof(isKeyDownFunc));
        _sleepAction = sleepAction ?? throw new ArgumentNullException(nameof(sleepAction));
    }

    /// <inheritdoc/>
    public async Task RunAsync(
        AutoClickerOptions options,
        CancellationToken cancellationToken,
        Action<string, double>? reportProgress = null,
        Action<int, int>? onPointClicked = null,
        PanicDetector? panicDetector = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        int intervalMs = Math.Max(5, options.IntervalMs);
        int totalClicks = options.TotalClicks;
        bool infinite = totalClicks <= 0;

        await Task.Run(() =>
        {
            int count = 0;
            while (!cancellationToken.IsCancellationRequested && (infinite || count < totalClicks))
            {
                PanicSafetyHelper.CheckPanicSafety(panicDetector);

                if (options.IsHoldMode && !_isKeyDownFunc(options.HotkeyVk))
                {
                    break;
                }

                ExecuteSingleIteration(options, onPointClicked, panicDetector);
                count++;

                ReportProgress(options, count, totalClicks, infinite, reportProgress);

                int sleepMs = options.JitterEnabled && options.TimeJitterPct > 0
                    ? JitterHelper.ApplyTimeJitter(intervalMs, options.TimeJitterPct)
                    : intervalMs;

                if (sleepMs > 0)
                {
                    _sleepAction(sleepMs, panicDetector, cancellationToken);
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private void ExecuteSingleIteration(
        AutoClickerOptions options,
        Action<int, int>? onPointClicked,
        PanicDetector? panicDetector)
    {
        if (options.IsSpammer)
        {
            _spamAction(options.SpamKey);
            return;
        }

        if (options.TargetX.HasValue && options.TargetY.HasValue)
        {
            var (clickX, clickY) = JitterHelper.ApplyLocationJitter(
                options.TargetX.Value,
                options.TargetY.Value,
                options.LocationJitterPx);

            onPointClicked?.Invoke(clickX, clickY);

            panicDetector?.SyncProgrammaticPosition(clickX, clickY);
            _clickAtAction(clickX, clickY, options.MouseButton);
            panicDetector?.SyncProgrammaticPosition(clickX, clickY);
        }
        else
        {
            var (curX, curY) = _getPositionFunc();
            onPointClicked?.Invoke(curX, curY);
            _clickAction(options.MouseButton);
        }
    }

    private static void ReportProgress(
        AutoClickerOptions options,
        int count,
        int totalClicks,
        bool infinite,
        Action<string, double>? reportProgress)
    {
        if (reportProgress is null) return;

        if (options.IsSpammer)
        {
            if (!infinite)
            {
                string message = string.Format(
                    LocalizationManager.Get("Clicker_SpammingKey"),
                    options.SpamKey,
                    count,
                    totalClicks);
                reportProgress(message, (double)count / totalClicks * 100);
            }
            else
            {
                string message = string.Format(
                    LocalizationManager.Get("Clicker_SpammingKeyInfinite"),
                    options.SpamKey,
                    count);
                reportProgress(message, 100);
            }
        }
        else
        {
            if (!infinite)
            {
                string message = string.Format(
                    LocalizationManager.Get("Clicker_ClickingCount"),
                    count,
                    totalClicks);
                reportProgress(message, (double)count / totalClicks * 100);
            }
            else
            {
                string message = string.Format(
                    LocalizationManager.Get("Clicker_ClickingInfinite"),
                    count);
                reportProgress(message, 100);
            }
        }
    }
}
