using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows;
using Forms = System.Windows.Forms;
using PlanKanban.Models;
using PlanKanban.Services;
using PlanKanban.ViewModels;
using PlanKanban.Views;
using FontStyle = System.Drawing.FontStyle;

namespace PlanKanban;

public partial class App : Application
{
    private static readonly Mutex SingleMutex = new(false, "Global\\PlanKanban_SingleInstance_Mutex");

    private JsonDataStore _store = null!;
    private DebouncedSaver _saver = null!;
    private AppData _data = null!;
    private MainViewModel _vm = null!;
    private MainWindow _main = null!;
    private GlobalHotKeyService _hotKey = null!;
    private EdgeDetector _edge = null!;
    private Forms.NotifyIcon? _tray;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 捕获所有未处理异常，避免静默崩溃退出
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Views.MainWindow.Log("UNHANDLED Domain: " + args.ExceptionObject);
        System.Windows.Threading.Dispatcher.CurrentDispatcher.UnhandledException += (_, args) =>
        {
            Views.MainWindow.Log("UNHANDLED Dispatcher: " + args.Exception);
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
            Views.MainWindow.Log("UNHANDLED Task: " + args.Exception);

        if (!SingleMutex.WaitOne(0, false))
        {
            Shutdown();
            return;
        }

        _store = new JsonDataStore();
        _data = _store.Load();
        foreach (var g in _data.Goals) g.IsEditing = false;
        _saver = new DebouncedSaver(_store);

        _vm = new MainViewModel(_data, _saver);

        _hotKey = new GlobalHotKeyService();
        _hotKey.HotKeyPressed += OnHotKey;

        _main = new MainWindow { DataContext = _vm, Settings = _data.Settings };
        _main.Loaded += (_, _) => ApplyTheme();
        // HwndReady 在 OnSourceInitialized 中触发，Show() 内部即触发，
        // 因此必须在 Show() 之前订阅，否则会错过事件导致热键不注册。
        _main.HwndReady += hwnd =>
        {
            _hotKey.Initialize(hwnd);
            var ok = _hotKey.Register(_data.Settings.HotKey);
            Views.MainWindow.Log($"HotKey registered: ok={ok}, key={_data.Settings.HotKey}, err={System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
        };
        _main.Show();

        _edge = new EdgeDetector { Settings = _data.Settings };
        _edge.EdgeHit += () => Dispatcher.Invoke(() =>
        {
            if (_data.Settings.RequireClickToExpand) _main.PrimeTriggerBar();
            else _main.Expand();
        });
        _edge.Start();

        _main.Closed += (_, _) => { if (!_exiting) ShutdownImpl(quit: false); };

        ThemeTracker.Start(() => Dispatcher.Invoke(ApplyTheme));
        BuildTray();
    }

    private void OnHotKey() => Dispatcher.Invoke(() => _main.Toggle());

    private void BuildTray()
    {
        _tray = new Forms.NotifyIcon
        {
            Icon = BuildAppIcon(),
            Text = "Plan Kanban",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu(),
        };
        _tray.MouseClick += (_, me) =>
        {
            if (me.Button == Forms.MouseButtons.Left)
                Dispatcher.Invoke(() => _main.Toggle());
        };
    }

    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip { RenderMode = Forms.ToolStripRenderMode.System };
        menu.Items.Add("显示/隐藏", null, (_, _) => Dispatcher.Invoke(() => _main.Toggle()));
        menu.Items.Add("设置...", null, (_, _) => Dispatcher.Invoke(OpenSettings));
        menu.Items.Add(new Forms.ToolStripSeparator());
        var auto = new Forms.ToolStripMenuItem("开机自启动") { Checked = AutoStartService.IsEnabled(), CheckOnClick = true };
        auto.CheckedChanged += (_, _) =>
        {
            AutoStartService.Set(auto.Checked);
            _data.Settings.AutoStart = auto.Checked;
            _saver.Schedule(_data);
        };
        menu.Items.Add(auto);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(() => ShutdownImpl(quit: true)));
        return menu;
    }

    public void OpenSettings()
    {
        Views.MainWindow.Log($"OpenSettings: before, expanded={_main.IsExpanded}, animating={_main.IsAnimating}");
        _edge.Stop();   // 设置窗打开期间暂停边缘检测，避免与异步 Visibility 切换产生 race
        var win = new SettingsWindow
        {
            Owner = _main,
            DataContext = _vm,
            Settings = _data.Settings,
        };
        win.SettingsApplied += () =>
        {
            _edge.Settings = _data.Settings;
            _hotKey.Unregister();
            _hotKey.Register(_data.Settings.HotKey);
            ApplyTheme();
            _main.OnSettingsChanged();
            _saver.Schedule(_data);
        };
        // 设置窗关闭后做一次强制复位，避免因模态消息循环导致动画/可见性状态错乱
        win.Closed += (_, _) =>
        {
            Views.MainWindow.Log($"OpenSettings: closed, expanded={_main.IsExpanded}, animating={_main.IsAnimating}");
            _main.ResetStateAfterModal();
            _edge.Start();   // 重新启用边缘检测
        };
        win.ShowDialog();
    }

    private void ApplyTheme()
    {
        var mode = _data.Settings.Theme;
        bool dark = mode switch
        {
            ThemeMode.Dark => true,
            ThemeMode.Light => false,
            _ => ThemeTracker.SystemIsDark(),
        };
        var themeDict = new System.Windows.ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Themes/{(dark ? "Dark" : "Light")}.xaml")
        };
        // 始终保留 Styles.xaml，仅替换主题字典
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(themeDict);
        Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Styles.xaml")
        });
        _main?.ApplyTheme(dark);
    }

    private void ShutdownImpl(bool quit)
    {
        if (_exiting) return;
        _exiting = true;
        try { _saver.Flush(); } catch { }
        try { _edge?.Dispose(); } catch { }
        try { _hotKey?.Dispose(); } catch { }
        try { if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; } } catch { }
        try { ThemeTracker.Stop(); } catch { }
        if (quit)
        {
            _main?.ForceClose();
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ShutdownImpl(quit: true);
        base.OnExit(e);
    }

    // 生成 32x32 透明图标：圆角方块 + "K"
    private static Icon BuildAppIcon()
    {
        var bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.Transparent);
            using var path = new GraphicsPath();
            var rect = new Rectangle(3, 3, 26, 26);
            int r = 8;
            path.AddArc(rect.X, rect.Y, r, r, 180, 90);
            path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
            path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
            path.CloseFigure();
            using var bg = new LinearGradientBrush(rect, Color.FromArgb(74, 124, 240), Color.FromArgb(50, 92, 220), 90f);
            g.FillPath(bg, path);
            g.DrawPath(new Pen(Color.FromArgb(110, 140, 255), 1.2f), path);
            using var font = new Font("Segoe UI", 16, FontStyle.Bold, GraphicsUnit.Pixel);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("K", font, Brushes.White, new RectangleF(0, 0, 32, 32), sf);
        }
        var h = bmp.GetHicon();
        var icon = Icon.FromHandle(h);
        return icon;
    }
}