namespace PlanKanban.Models;

public enum DockEdge { Left, Right, Top }

public enum ThemeMode { System, Light, Dark }

public sealed class AppSettings
{
    public DockEdge Edge { get; set; } = DockEdge.Right;
    public string HotKey { get; set; } = "Alt+Q";
    public int AutoHideDelayMs { get; set; } = 1500;
    public int EdgeTriggerDelayMs { get; set; } = 80;
    public int EdgeTriggerZone { get; set; } = 4;    // 鼠标贴近边缘多少像素视为触发
    public int PanelWidth { get; set; } = 0;    // 0 表示按屏幕宽度黄金比自动计算
    public int PanelMaxHeight { get; set; } = 720;   // Top 边缘停靠时的最大高度
    public ThemeMode Theme { get; set; } = ThemeMode.System;
    public bool AutoStart { get; set; } = false;
    public bool RequireClickToExpand { get; set; } = false;   // 防误触：边缘悬停仅高亮，需点击才呼出
    public string? ScreenDeviceName { get; set; }    // null 表示跟随鼠标所在屏幕
}