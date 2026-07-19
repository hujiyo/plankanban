using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace PlanKanban.Services;

/// <summary>全局热键：通过 user32 RegisterHotKey + 隐藏窗口的 WndProc 接收 WM_HOTKEY。</summary>
public sealed class GlobalHotKeyService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    private HwndSource? _source;
    private IntPtr _hwnd;
    private const int Id = 0x9001;
    private bool _registered;
    private string? _current;

    public event Action? HotKeyPressed;

    public void Initialize(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == Id)
        {
            HotKeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public bool Register(string hotKey)
    {
        Unregister();
        if (!TryParse(hotKey, out var mod, out var vk)) return false;
        _registered = RegisterHotKey(_hwnd, Id, mod | MOD_NOREPEAT, vk);
        if (_registered) _current = hotKey;
        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(_hwnd, Id);
            _registered = false;
        }
    }

    public static bool TryParse(string hotKey, out uint mod, out uint vk)
    {
        mod = 0; vk = 0;
        if (string.IsNullOrWhiteSpace(hotKey)) return false;
        var parts = hotKey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        uint localMod = 0; uint localVk = 0; bool hasKey = false;
        foreach (var p in parts)
        {
            switch (p.ToUpperInvariant())
            {
                case "ALT": localMod |= MOD_ALT; break;
                case "CTRL":
                case "CONTROL": localMod |= MOD_CONTROL; break;
                case "SHIFT": localMod |= MOD_SHIFT; break;
                case "WIN": localMod |= MOD_WIN; break;
                default:
                    if (Enum.TryParse<System.Windows.Forms.Keys>(p, true, out var k))
                    {
                        localVk = (uint)k;
                        hasKey = true;
                    }
                    else return false;
                    break;
            }
        }
        if (!hasKey) return false;
        mod = localMod; vk = localVk;
        return true;
    }

    public void Dispose()
    {
        Unregister();
        _source?.RemoveHook(WndProc);
        _source = null;
    }
}