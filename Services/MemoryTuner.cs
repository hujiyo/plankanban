using System.Runtime.InteropServices;

namespace PlanKanban.Services;

/// <summary>进程内存优化：定期 / 在收起后调用 EmptyWorkingSet 让 OS 回收物理页，
/// 任务管理器看到的"内存占用"会显著下降。私有内存不变。</summary>
public static class MemoryTuner
{
    [DllImport("psapi.dll", SetLastError = true)]
    private static extern int EmptyWorkingSet(IntPtr hProcess);

    public static void Trim()
    {
        try { EmptyWorkingSet(System.Diagnostics.Process.GetCurrentProcess().Handle); }
        catch { /* 静默 */ }
    }
}