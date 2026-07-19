using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PlanKanban.Models;
using PlanKanban.ViewModels;
using PlanKanban.Services;
using Forms = System.Windows.Forms;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace PlanKanban.Views;

public partial class MainWindow : Window
{
    public AppSettings Settings { get; set; } = new();
    public event Action<IntPtr>? HwndReady;
    public bool IsExpanded => _expanded;
    public bool IsAnimating => _animating;

    private bool _expanded;
    private bool _animating;
    private bool _hoverSelf;       // 鼠标在面板上
    private bool _hoverTrigger;   // 鼠标在触发条上
    private long _collapseSeq;     // 收起序号，用于丢弃过期的异步 Visibility 切换
    private readonly DispatcherTimer _autoHide;

    public MainWindow()
    {
        InitializeComponent();
        _autoHide = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _autoHide.Tick += (_, _) => { _autoHide.Stop(); if (!_hoverSelf && !_hoverTrigger) Collapse(); };
        Log("MainWindow ctor");

        // 鼠标移入面板：取消自动收起；移开：触发延迟收起
        Panel.MouseEnter      += (_, _) => { _hoverSelf = true;  _autoHide.Stop(); };
        Panel.MouseLeave      += (_, _) => { _hoverSelf = false; ScheduleAutoHide(); };
        Loaded += (_, _) =>
        {
            UpdateCounter();
            // 确保触发条初始就是常规灰色（ Loaded 后资源已就绪）
            try
            {
                var brush = (Brush)Application.Current.FindResource("TriggerBarBrush");
                TriggerBar.Background = brush;
                TriggerBar.Opacity = 0.55;
            }
            catch { }
            if (DataContext is MainViewModel vm)
            {
                vm.Goals.CollectionChanged += (_, _) => Dispatcher.BeginInvoke(new Action(UpdateCounter));
                vm.Archive.CollectionChanged += (_, _) => Dispatcher.BeginInvoke(new Action(UpdateCounter));
                vm.StatsChanged += () => Dispatcher.BeginInvoke(new Action(UpdateCounter));
            }
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        Log($"OnSourceInitialized hwnd={helper.Handle}");
        HwndReady?.Invoke(helper.Handle);
        UpdatePlacement();
        _autoHide.Interval = TimeSpan.FromMilliseconds(Settings.AutoHideDelayMs);
        // 临时诊断：默认展开，确认窗口本身可见
        _expanded = true;
        Panel.Visibility = Visibility.Visible;
        AnimatePanel(collapsed: false, animate: false);
    }

    public void OnSettingsChanged()
    {
        _autoHide.Interval = TimeSpan.FromMilliseconds(Settings.AutoHideDelayMs);
        UpdatePlacement();
        if (_expanded) { Panel.Visibility = Visibility.Visible; AnimatePanel(collapsed: false, animate: false); }
        else { Panel.Visibility = Visibility.Collapsed; AnimatePanel(collapsed: true, animate: false); }
    }

/// <summary>设置窗（模态）关闭后调用，强制复位动画/可见性状态，
    /// 防止因模态消息循环中 EdgeDetector 触发的 Expand/Collapse 与
    /// 异步 Background 派发的 Visibility 切换产生 race 导致看板锁死。</summary>
public void ResetStateAfterModal()
{
    _animating = false;          // 强制解锁动画锁
    _autoHide.Stop();            // 取消任何待触发的自动收起
    // 按 _expanded 重新校正可见性与 transform，保证一致
    if (_expanded)
    {
        Panel.Visibility = Visibility.Visible;
        AnimatePanel(collapsed: false, animate: false);
    }
    else
    {
        Panel.Visibility = Visibility.Collapsed;
        AnimatePanel(collapsed: true, animate: false);
    }
}

    public void ApplyTheme(bool dark)
    {
        PanelShadow.Color = dark ? Colors.Black : Color.FromRgb(0x33, 0x33, 0x33);
        PanelShadow.Opacity = dark ? 0.45 : 0.30;
    }

    private bool _historyOpen;
    private void HistoryToggle_Click(object sender, RoutedEventArgs e)
    {
        _historyOpen = !_historyOpen;
        ApplyHistoryToggle();
    }

    private void ApplyHistoryToggle()
    {
        if (HistoryPanel == null) return;
        if (_historyOpen)
        {
            HistoryPanel.Height = double.NaN;
            HistoryChevron.Data = (Geometry)Application.Current.FindResource("IconChevronUp");
        }
        else
        {
            HistoryPanel.Height = 0;
            HistoryChevron.Data = (Geometry)Application.Current.FindResource("IconChevronDown");
        }
        UpdateHistoryCount();
    }

    private void UpdateHistoryCount()
    {
        if (DataContext is not MainViewModel vm) return;
        if (HistoryCountText == null) return;
        HistoryCountText.Text = $"归档 ({vm.Archive.Count})";
        HistoryEmptyText.Visibility = vm.Archive.Count == 0 && _historyOpen
            ? Visibility.Visible : Visibility.Collapsed;
    }

    public void Toggle() { if (_expanded) Collapse(); else Expand(); }

    /// <summary>防误触模式下：边缘触变后仅高亮触发条，等待用户点击再展开。</summary>
    public void PrimeTriggerBar()
    {
        if (_expanded) return;
        TriggerBar.Opacity = 1.0;
        TriggerBar.Background = TryFindBrush("TriggerBarHotBrush");
    }

    public void Expand()
    {
        if (_expanded) return;
        _expanded = true;
        _collapseSeq++;   // 使任何挂起的延迟收起回调失效
        Panel.Visibility = Visibility.Visible;
        AnimatePanel(collapsed: false, animate: false);   // 瞬间到位
        TriggerBar.Opacity = 0.7;
        TriggerBar.Background = TryFindBrush("TriggerBarHotBrush");
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try { if (IsVisible) InputBox.Focus(); } catch { }
        }), DispatcherPriority.Input);
    }

    public void Collapse()
    {
        if (!_expanded) return;
        _expanded = false;
        _autoHide.Stop();
        _collapseSeq++;
        AnimatePanel(collapsed: true, animate: false);   // 瞬间到位
        TriggerBar.Opacity = 0.55;
        TriggerBar.Background = TryFindBrush("TriggerBarBrush");
        try { Keyboard.ClearFocus(); } catch { }
        if (DataContext is MainViewModel vm)
        {
            foreach (var g in vm.Goals) g.IsEditing = false;
        }
        Panel.Visibility = Visibility.Collapsed;
        try { MemoryTuner.Trim(); } catch { }
    }

    private static Brush TryFindBrush(string key)
    {
        try { return (Brush)Application.Current.FindResource(key); } catch { return Brushes.Transparent; }
    }

    public void ForceClose() => Close();

    private void AnimatePanel(bool collapsed, bool animate)
    {
        double targetX = 0, targetY = 0;
        switch (Settings.Edge)
        {
            case DockEdge.Right: targetX = collapsed ? Width : 0; break;
            case DockEdge.Left:  targetX = collapsed ? -Width : 0; break;
            case DockEdge.Top:   targetY = collapsed ? -Height : 0; break;
        }

        // 先停掉旧动画，避免 HoldEnd 锁住属性
        PanelTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
        PanelTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);

        if (!animate)
        {
            PanelTransform.X = targetX;
            PanelTransform.Y = targetY;
            return;
        }

        _animating = true;
        var dur = TimeSpan.FromMilliseconds(220);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        if (targetX != PanelTransform.X)
        {
            var ax = new DoubleAnimation(PanelTransform.X, targetX, dur)
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };
            ax.Completed += (_, _) =>
            {
                PanelTransform.X = targetX;
                _animating = false;
            };
            PanelTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, ax);
        }
        if (targetY != PanelTransform.Y)
        {
            var ay = new DoubleAnimation(PanelTransform.Y, targetY, dur)
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };
            ay.Completed += (_, _) =>
            {
                PanelTransform.Y = targetY;
                _animating = false;
            };
            PanelTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, ay);
        }
        // 兜底：强制复位
        var guard = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
        guard.Tick += (_, _) =>
        {
            guard.Stop();
            PanelTransform.X = targetX;
            PanelTransform.Y = targetY;
            _animating = false;
        };
        guard.Start();
    }

    /// <summary>根据贴靠边缘和当前屏幕，重新设置窗口位置与尺寸。</summary>
    public void UpdatePlacement()
    {
        var settings = Settings;
        var all = Forms.Screen.AllScreens;
        if (all.Length == 0) return;
        Forms.Screen screen;
        if (!string.IsNullOrEmpty(settings.ScreenDeviceName))
        {
            screen = Array.Find(all, s => s.DeviceName == settings.ScreenDeviceName) ?? all[0];
        }
        else
        {
            screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        }
        var wa = screen.WorkingArea;

        double sx = 1.0, sy = 1.0;
        var ct = PresentationSource.FromVisual(this)?.CompositionTarget;
        if (ct != null)
        {
            var m = ct.TransformToDevice;
            sx = m.M11; sy = m.M22;
        }

        // 宽度：屏幕宽度 × 黄金比(0.618) × 1/4 ≈ 屏宽 15.5%，
// 但实际过窄。改成"屏宽 1/4 略偏向黄金比例"，约 27% 屏宽，最终视觉接近 Win11 通知面板。
        double panelWidthDip = Math.Clamp(wa.Width * 0.27 / sx, 300, 560);
        double panelMaxHeightDip = settings.PanelMaxHeight;
        Log($"UpdatePlacement: wa=({wa.Left},{wa.Top},{wa.Width},{wa.Height}) sx={sx} sy={sy} panelWidthDip={panelWidthDip}");
        switch (settings.Edge)
        {
            case DockEdge.Right:
                Width = panelWidthDip;
                Height = wa.Height / sy;
                Left = (wa.Right - panelWidthDip * sx) / sx;
                Top = wa.Top / sy;
                break;
            case DockEdge.Left:
                Width = panelWidthDip;
                Height = wa.Height / sy;
                Left = wa.Left / sx;
                Top = wa.Top / sy;
                break;
            case DockEdge.Top:
                Width = panelWidthDip;
                Height = Math.Min(panelMaxHeightDip, wa.Height * 0.7) / sy;
                Left = (wa.Left + (wa.Width - panelWidthDip * sx) / 2) / sx;
                Top = wa.Top / sy;
                break;
        }

        ApplyTriggerBarLayout();
    }

    private void ApplyTriggerBarLayout()
    {
        // 重置
        TriggerBar.HorizontalAlignment = HorizontalAlignment.Stretch;
        TriggerBar.VerticalAlignment = VerticalAlignment.Stretch;
        TriggerBar.Width = double.NaN;
        TriggerBar.Height = double.NaN;
        TriggerBar.HorizontalAlignment = Settings.Edge switch
        {
            DockEdge.Right => HorizontalAlignment.Right,
            DockEdge.Left => HorizontalAlignment.Left,
            _ => HorizontalAlignment.Stretch
        };
        TriggerBar.VerticalAlignment = Settings.Edge switch
        {
            DockEdge.Top => VerticalAlignment.Top,
            _ => VerticalAlignment.Stretch
        };
        if (Settings.Edge != DockEdge.Top) TriggerBar.Width = 4;
        else TriggerBar.Height = 4;
    }

    private void ScheduleAutoHide()
    {
        if (!_expanded) return;
        _autoHide.Stop();
        _autoHide.Start();
    }

    // ----- 事件处理 -----

    private void CollapseBtn_Click(object sender, RoutedEventArgs e) => Collapse();

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app) app.OpenSettings();
    }

    private void TriggerBar_MouseEnter(object sender, MouseEventArgs e)
    {
        _hoverTrigger = true;
        TriggerBar.Opacity = 1.0;
        TriggerBar.Background = (Brush)Application.Current.FindResource("TriggerBarHotBrush");
        if (!_expanded && !Settings.RequireClickToExpand) Expand();
    }

    private void TriggerBar_MouseLeave(object sender, MouseEventArgs e)
    {
        _hoverTrigger = false;
        TriggerBar.Opacity = _expanded ? 0.7 : 0.55;
        TriggerBar.Background = (Brush)Application.Current.FindResource("TriggerBarBrush");
        ScheduleAutoHide();
    }

    private void TriggerBar_Click(object sender, MouseButtonEventArgs e) => Toggle();

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is not MainViewModel vm) return;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            vm.InsertGoalCommand.Execute(0);
        }
        else
        {
            vm.AddGoalCommand.Execute(null);
        }
        e.Handled = true;
    }

    private void InsertBtn_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.InsertGoalCommand.Execute(0);
    }

    private void AddBtn_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.AddGoalCommand.Execute(null);
    }

    private void EditBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is GoalItem g)
        {
            if (DataContext is MainViewModel vm) vm.StartEditCommand.Execute(g);
        }
    }

    private void DeleteBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is GoalItem g)
        {
            if (DataContext is MainViewModel vm) vm.DeleteGoalCommand.Execute(g);
        }
    }

    private void MoveUpBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is GoalItem g)
        {
            if (DataContext is MainViewModel vm) vm.MoveUpCommand.Execute(g);
        }
    }

    private void MoveDownBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is GoalItem g)
        {
            if (DataContext is MainViewModel vm) vm.MoveDownCommand.Execute(g);
        }
    }

    private void EditBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Dispatcher.BeginInvoke(new Action(() =>
            {
                tb.Focus();
                tb.SelectAll();
            }), DispatcherPriority.Input);
        }
    }

    private void EditBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (DataContext is not MainViewModel vm) return;
        if (tb.DataContext is not GoalItem g) return;

        if (e.Key == Key.Enter)
        {
            // 提交编辑
            var binding = tb.GetBindingExpression(TextBox.TextProperty);
            if (binding != null)
            {
                // 提交前先更新源：把当前输入文本写入 Title，但 IsEditing 还没置 false
                if (string.IsNullOrWhiteSpace(tb.Text)) tb.Text = g.Title; // 防止空标题
                binding.UpdateSource();
            }
            vm.ConfirmEditCommand.Execute(g);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            // 取消：还原文本并退出编辑
            tb.Text = g.Title;
            g.IsEditing = false;
            e.Handled = true;
        }
    }

    private void EditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is GoalItem g)
        {
            // 失焦后自动提交并退出
            var binding = tb.GetBindingExpression(TextBox.TextProperty);
            try { if (!string.IsNullOrWhiteSpace(tb.Text)) binding?.UpdateSource(); }
            catch { }
            g.IsEditing = false;
            if (DataContext is MainViewModel vm) vm.ConfirmEditCommand.Execute(g);
        }
    }

    private void GoalCheck_Click(object sender, RoutedEventArgs e) => UpdateCounter();

    private void GoalRow_MouseEnter(object sender, MouseEventArgs e) { }
    private void GoalRow_MouseLeave(object sender, MouseEventArgs e) { }

    private void GoalRow_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (sender is FrameworkElement fe && fe.DataContext is GoalItem g)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SetCurrentGoalCommand.Execute(g);
            }
        }
    }

    private void List_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // 让鼠标滚轮可滚动列表（即使悬浮在 ItemsControl 上）
        e.Handled = false;
    }

    private void UpdateCounter()
    {
        if (DataContext is MainViewModel vm)
        {
            int total = vm.Goals.Count;
            int done = vm.Goals.Count(x => x.IsDone);
            CounterText.Text = total == 0 ? "暂无目标" : $"{done}/{total} 已完成";
            UpdateHistoryCount();
        }
        else
        {
            CounterText.Text = string.Empty;
        }
    }

    private static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PlanKanban", "debug.log");

    public static void Log(string msg)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
        }
        catch { }
    }
}