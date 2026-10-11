using System;
using System.Threading;

namespace Tapster;

/// <summary>
/// Manages single-instance enforcement and inter-process wake communication.
/// </summary>
public sealed class SingleInstanceService : IDisposable
{
    public const string DefaultMutexName = @"Local\TapStrum_SingleInstance_Mutex";
    public const string DefaultWakeEventName = @"Local\TapStrum_Wake_Event";

    private readonly string _mutexName;
    private readonly string _wakeEventName;

    private Mutex? _mutex;
    private bool _ownsMutex;
    private EventWaitHandle? _wakeEvent;
    private CancellationTokenSource? _cts;
    private Thread? _listenerThread;
    private bool _disposed;

    public bool OwnsMutex => _ownsMutex;

    public SingleInstanceService(
        string mutexName = DefaultMutexName,
        string wakeEventName = DefaultWakeEventName)
    {
        _mutexName = mutexName;
        _wakeEventName = wakeEventName;
    }

    /// <summary>
    /// Attempts to acquire the single-instance mutex.
    /// Returns true if this instance acquired ownership, false if an existing instance is running.
    /// </summary>
    public bool TryAcquire()
    {
        if (_ownsMutex) return true;

        try
        {
            _mutex = new Mutex(true, _mutexName, out bool createdNew);
            _ownsMutex = createdNew;
            return createdNew;
        }
        catch (AbandonedMutexException)
        {
            _ownsMutex = true;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Signals the active instance's wake event and grants foreground window permission.
    /// </summary>
    public static bool SignalWake(string eventName = DefaultWakeEventName)
    {
        try
        {
            NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);

            if (EventWaitHandle.TryOpenExisting(eventName, out var wakeEvent))
            {
                using (wakeEvent)
                {
                    wakeEvent.Set();
                }
                return true;
            }
        }
        catch
        {
            // IPC errors ignored during signaling
        }

        return false;
    }

    /// <summary>
    /// Starts a background thread listening for wake signals.
    /// </summary>
    public void StartWakeListener(Action onWakeRequested)
    {
        ArgumentNullException.ThrowIfNull(onWakeRequested);

        if (_wakeEvent != null || !_ownsMutex) return;

        try
        {
            _wakeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, _wakeEventName);
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _listenerThread = new Thread(() => ListenLoop(_wakeEvent, token, onWakeRequested))
            {
                IsBackground = true,
                Name = "TapStrum_SingleInstance_WakeListener"
            };
            _listenerThread.Start();
        }
        catch
        {
            // Fallback gracefully if event creation fails
        }
    }

    private static void ListenLoop(EventWaitHandle wakeEvent, CancellationToken token, Action onWakeRequested)
    {
        var waitHandles = new WaitHandle[] { wakeEvent, token.WaitHandle };

        while (!token.IsCancellationRequested)
        {
            int index = WaitHandle.WaitAny(waitHandles);
            if (index == 0 && !token.IsCancellationRequested)
            {
                InvokeWakeCallback(onWakeRequested);
            }
        }
    }

    private static void InvokeWakeCallback(Action onWakeRequested)
    {
        try
        {
            onWakeRequested();
        }
        catch
        {
            // Suppress callback exceptions to keep listener alive
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _cts?.Dispose();

        if (_ownsMutex && _mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Mutex wasn't owned by this thread
            }
        }

        _mutex?.Dispose();
        _wakeEvent?.Dispose();
    }
}
