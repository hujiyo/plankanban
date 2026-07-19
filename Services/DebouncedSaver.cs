using System.Windows.Threading;

namespace PlanKanban.Services;

/// <summary>防抖保存：拖动滚动条、勾选等高频改动只在静止后写盘一次。</summary>
public sealed class DebouncedSaver : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly JsonDataStore _store;
    private AppData? _pending;
    private readonly object _lock = new();

    public DebouncedSaver(JsonDataStore store, int delayMs = 800)
    {
        _store = store;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            AppData? data;
            lock (_lock) data = _pending;
            if (data != null) _store.Save(data);
        };
    }

    public void Schedule(AppData data)
    {
        lock (_lock) _pending = data;
        _timer.Stop();
        _timer.Start();
    }

    public void Flush()
    {
        _timer.Stop();
        AppData? data;
        lock (_lock) data = _pending;
        if (data != null) _store.Save(data);
        lock (_lock) _pending = null;
    }

    public void Dispose() => Flush();
}