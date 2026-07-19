using Microsoft.Win32;

namespace PlanKanban;

/// <summary>监听系统浅色/深色主题切换。</summary>
internal static class ThemeTracker
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private static Action? _callback;
    private static bool _running;

    public static void Start(Action onChange)
    {
        _callback = onChange;
        if (!_running)
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            _running = true;
        }
    }

    public static void Stop()
    {
        if (_running)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _running = false;
        }
        _callback = null;
    }

    private static void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General) _callback?.Invoke();
    }

    public static bool SystemIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            var v = key?.GetValue("AppsUseLightTheme");
            if (v is int i) return i == 0;
        }
        catch { }
        return false;
    }
}