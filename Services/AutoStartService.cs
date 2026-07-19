using Microsoft.Win32;

namespace PlanKanban.Services;

/// <summary>开机自启动：写入 HKCU\Software\Microsoft\Windows\CurrentVersion\Run。</summary>
public static class AutoStartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PlanKanban";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) != null;
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled)
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe)) key?.SetValue(ValueName, $"\"{exe}\"");
        }
        else
        {
            key?.DeleteValue(ValueName, false);
        }
    }
}