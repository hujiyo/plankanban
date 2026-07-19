using System.Windows;
using System.Windows.Threading;
using PlanKanban.Models;
using Forms = System.Windows.Forms;

namespace PlanKanban.Services;

/// <summary>边缘悬停检测：低频定时器（默认 ~30Hz）轮询鼠标位置，
/// 命中边缘后持续 EdgeTriggerDelayMs 才触发，避免误触。
/// 离开边缘后自动复位。</summary>
public sealed class EdgeDetector : IDisposable
{
    private readonly DispatcherTimer _timer;
    private DateTime _enterTime = DateTime.MinValue;
    private bool _fired;

    public AppSettings Settings { get; set; } = new();
    public event Action? EdgeHit;

    public EdgeDetector()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };   // ~60Hz，明显更跟手
        _timer.Tick += OnTick;
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();
    public void Reset() { _enterTime = DateTime.MinValue; _fired = false; }

    private void OnTick(object? sender, EventArgs e)
    {
        if (Settings == null) return;
        var p = Forms.Cursor.Position;
        var screen = ScreenForPoint(p, Settings.ScreenDeviceName);
        if (screen == null) return;

        var wa = screen.WorkingArea;
        int zone = Math.Max(1, Settings.EdgeTriggerZone);
        bool atEdge = Settings.Edge switch
        {
            DockEdge.Left => p.X >= wa.Left - 1 && p.X <= wa.Left + zone,
            DockEdge.Right => p.X <= wa.Right + 1 && p.X >= wa.Right - zone,
            DockEdge.Top => p.Y >= wa.Top - 1 && p.Y <= wa.Top + zone,
            _ => false
        };

        if (atEdge)
        {
            if (!_fired)
            {
                if (_enterTime == DateTime.MinValue) _enterTime = DateTime.UtcNow;
                if ((DateTime.UtcNow - _enterTime).TotalMilliseconds >= Settings.EdgeTriggerDelayMs)
                {
                    _fired = true;
                    EdgeHit?.Invoke();
                }
            }
        }
        else
        {
            _enterTime = DateTime.MinValue;
            _fired = false;
        }
    }

    private static Forms.Screen? ScreenForPoint(System.Drawing.Point p, string? deviceName)
    {
        var all = Forms.Screen.AllScreens;
        if (all.Length == 0) return null;
        if (!string.IsNullOrEmpty(deviceName))
        {
            var s = Array.Find(all, x => x.DeviceName == deviceName);
            if (s != null) return s;
        }
        return Forms.Screen.FromPoint(p);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}