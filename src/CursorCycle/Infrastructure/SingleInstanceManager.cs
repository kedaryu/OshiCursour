namespace CursorCycle.Infrastructure;

public sealed class SingleInstanceManager : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle? _showWindowEvent;
    private RegisteredWaitHandle? _registeredWait;
    private bool _disposed;

    public SingleInstanceManager(string applicationId)
    {
        var mutexName = $@"Local\{applicationId}.Mutex";
        var eventName = $@"Local\{applicationId}.ShowWindow";

        _mutex = new Mutex(true, mutexName, out var createdNew);
        IsPrimaryInstance = createdNew;

        if (IsPrimaryInstance)
        {
            _showWindowEvent = new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                eventName);
        }
    }

    public bool IsPrimaryInstance { get; }

    public void StartListening(Action showWindow)
    {
        if (!IsPrimaryInstance || _showWindowEvent is null)
        {
            return;
        }

        _registeredWait = ThreadPool.RegisterWaitForSingleObject(
            _showWindowEvent,
            (_, _) =>
            {
                try
                {
                    showWindow();
                }
                catch (ObjectDisposedException)
                {
                    // アプリ終了中の通知は無視する。
                }
            },
            null,
            Timeout.Infinite,
            false);
    }

    public static void SignalPrimaryInstance(string applicationId)
    {
        var eventName = $@"Local\{applicationId}.ShowWindow";

        try
        {
            using var handle = EventWaitHandle.OpenExisting(eventName);
            handle.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // 先行インスタンスが終了処理中なら何もしない。
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _registeredWait?.Unregister(null);
        _showWindowEvent?.Dispose();

        if (IsPrimaryInstance)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // 所有権が既に解放されている場合は無視する。
            }
        }

        _mutex.Dispose();
    }
}
