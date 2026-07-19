using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PlanKanban.Models;
using PlanKanban.Services;
using Forms = System.Windows.Forms;

namespace PlanKanban.Views;

public partial class SettingsWindow : Window
{
    private AppSettings _settings = null!;
    private string _pendingHotKey = "";
    private bool _suspendEvents;

    public event Action? SettingsApplied;

    public AppSettings Settings
    {
        get => _settings;
        set { _settings = value; LoadSettings(); }
    }

    public SettingsWindow()
    {
        _suspendEvents = true;   // InitializeComponent 阶段 Slider.ValueChanged 会触发，此时 TextBlock 还没建好
        InitializeComponent();
        _suspendEvents = false;
    }

    private void LoadSettings()
    {
        _suspendEvents = true;
        EdgeLeft.IsChecked  = _settings.Edge == DockEdge.Left;
        EdgeRight.IsChecked = _settings.Edge == DockEdge.Right;
        EdgeTop.IsChecked   = _settings.Edge == DockEdge.Top;
        TriggerDelay.Value  = Math.Clamp(_settings.EdgeTriggerDelayMs, 0, 800);
        AutoHideDelay.Value = Math.Clamp(_settings.AutoHideDelayMs, 400, 6000);
        PanelWidth.Value    = Math.Clamp(_settings.PanelWidth, 260, 460);
        ThemeSystem.IsChecked = _settings.Theme == ThemeMode.System;
        ThemeLight.IsChecked = _settings.Theme == ThemeMode.Light;
        ThemeDark.IsChecked = _settings.Theme == ThemeMode.Dark;
        _pendingHotKey = _settings.HotKey;
        HotKeyBox.Text = _pendingHotKey;
        AutoStartBox.IsChecked = AutoStartService.IsEnabled();
        RequireClickBox.IsChecked = _settings.RequireClickToExpand;
        BuildMonitorCombo();
        UpdateLabels();
        _suspendEvents = false;
    }

    private void BuildMonitorCombo()
    {
        MonitorCombo.Items.Clear();
        MonitorCombo.Items.Add(new ComboBoxItem { Content = "跟随鼠标屏幕", Tag = "" });
        int selected = 0;
        var screens = Forms.Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            var s = screens[i];
            MonitorCombo.Items.Add(new ComboBoxItem
            {
                Content = $"显示器 {i + 1}  {s.Bounds.Width}×{s.Bounds.Height}",
                Tag = s.DeviceName
            });
            if (s.DeviceName == _settings.ScreenDeviceName) selected = i + 1;
        }
        MonitorCombo.SelectedIndex = selected;
    }

    private void UpdateLabels()
    {
        if (TriggerDelayVal == null) return;
        TriggerDelayVal.Text = $"{(int)TriggerDelay.Value} ms";
        AutoHideVal.Text = $"{(int)AutoHideDelay.Value} ms";
        PanelWidthVal.Text = $"{(int)PanelWidth.Value} px";
    }

    private void Num_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suspendEvents) return;
        UpdateLabels();
    }

    private void HotKeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
                 or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin) return;
        var mod = Keyboard.Modifiers;
        var parts = new List<string>();
        if (mod.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mod.HasFlag(ModifierKeys.Alt))     parts.Add("Alt");
        if (mod.HasFlag(ModifierKeys.Shift))   parts.Add("Shift");
        if (mod.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        if (parts.Count == 0) return; // 至少一个修饰键
        var formsKey = (Forms.Keys)KeyInterop.VirtualKeyFromKey(key);
        _pendingHotKey = string.Join('+', parts) + "+" + formsKey;
        HotKeyBox.Text = _pendingHotKey;
    }

    private void MonitorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suspendEvents) return;
        if (MonitorCombo.SelectedItem is ComboBoxItem item)
        {
            var tag = item.Tag as string;
            _settings.ScreenDeviceName = string.IsNullOrEmpty(tag) ? null : tag;
        }
    }

    private void AutoStartBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_suspendEvents) return;
        bool enabled = AutoStartBox.IsChecked == true;
        AutoStartService.Set(enabled);
        _settings.AutoStart = enabled;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        Commit();
        SettingsApplied?.Invoke();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Commit();
        SettingsApplied?.Invoke();
        Close();
    }

    private void Commit()
    {
        if (EdgeLeft.IsChecked == true) _settings.Edge = DockEdge.Left;
        else if (EdgeTop.IsChecked == true) _settings.Edge = DockEdge.Top;
        else _settings.Edge = DockEdge.Right;

        if (ThemeSystem.IsChecked == true) _settings.Theme = ThemeMode.System;
        else if (ThemeLight.IsChecked == true) _settings.Theme = ThemeMode.Light;
        else _settings.Theme = ThemeMode.Dark;

        _settings.EdgeTriggerDelayMs = (int)TriggerDelay.Value;
        _settings.AutoHideDelayMs = (int)AutoHideDelay.Value;
        _settings.PanelWidth = (int)PanelWidth.Value;
        _settings.RequireClickToExpand = RequireClickBox.IsChecked == true;

        if (!string.IsNullOrEmpty(_pendingHotKey))
            _settings.HotKey = _pendingHotKey;
    }
}