# Plan Kanban —— Your TODO List

一个极简边缘呼出式 Windows 桌面小工具：平时只贴在屏幕边缘一条极窄的触发条，鼠标一碰边缘/按下快捷键就展开面板，随手管理当前目标清单。

- 单文件 exe，双击即用，无需安装、无需额外运行时
- 自动隐藏 + 全局快捷键呼出
- 目标增删改、勾选完成、置顶"当前目标"
- 浅色/深色主题/跟随系统
- 系统托盘常驻、开机自启动
- 数据本地持久化

---

## 一、快速使用

1. 双击 `PlanKanban.exe` 启动。
2. 默认贴靠 **右边缘**，屏幕右侧会出现一条 4px 灰色细条，鼠标移上去即展开面板，防误触模式下需单击触发条呼出。
3. 支持快捷键全局呼出/收起。
4. 关闭面板：点击面板右上角的"收起"图标，或把鼠标移开面板后自动收回。
5. 程序始终在系统托盘运行，右键托盘图标可"显示/隐藏""打开设置""开机自启动""退出"。

### 基本操作

| 操作 | 方式 |
|---|---|
| 新增目标 | 在输入框输入文字，**回车**追加到列表末尾 |
| 插入到顶部 | **Shift+回车**，或点"插入到顶部"按钮 |
| 删除目标 | 鼠标悬停某行 → 右侧 ✕ |
| 重命名 | 鼠标悬停某行 → ✏，回车提交 / Esc 取消 |
| 勾选完成 | 行首勾选框 |
| 设为当前目标 | 单击该行 |
| 调整顺序 | 鼠标悬停某行 → ↑ / ↓ |
| 清除已完成 | 面板底部"清除已完成"按钮 |

"当前目标"会以高亮卡片置顶显示，字号更大、带主题强调色边框。

---

## 二、设置说明

托盘菜单 → "设置..."，可调整：

**贴靠边缘**：左 / 右 / 上三选一。任意时刻改动即时生效。

**全局快捷键**：在输入框聚焦状态下直接按下你要的组合键即可。默认 `Alt+Q`。
- 至少要含一个修饰键（Ctrl/Alt/Shift/Win）
- 例如 `Ctrl+J`、`Alt+Space`、`Win+K`
- 不建议单用 Win 键或与系统已占用快捷键冲突

**触发延迟**：鼠标接近边缘多久后开始呼出（0~800 ms）。默认 80 ms。

**自动收回延迟**：鼠标离开面板后多久自动收起（400~6000 ms）。默认 1500 ms。

**面板宽度**：260~460 px 可调；默认 0，表示按屏幕宽度约 27% 自动计算（当前布局以自动计算为准）。

**防误触**：勾选后，鼠标贴边只高亮触发条、不自动呼出，需再单击触发条才展开看板；全局快捷键不受影响。

**主题**：跟随系统 / 浅色 / 深色。

**触发显示器**：多屏环境下可指定面板挂在哪块屏幕边缘，或选"跟随鼠标屏幕"（默认）。

**开机自启动**：勾选即写入注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\PlanKanban`，下次开机自动启动。若程序被移动到其它位置，下次启动时会自动把 Run 键里的路径更新为当前 exe 路径，无需重新设置。

所有设置改动后点"应用"立即保存；点"关闭"会同时保存。

---

## 三、数据存储

- 数据文件：`%AppData%\PlanKanban\data.json`
  通常等价于 `C:\Users\<你>\AppData\Roaming\PlanKanban\data.json`
- 该文件保存所有目标和当前设置。
- 写入采用防抖：每次改动后约 0.8 秒静止时统一写盘，避免频繁 IO。
- 程序退出/重启电脑后，下次启动会自动恢复完整列表与"当前目标"状态。
- 想备份/迁移：复制此文件夹即可。想重置：退出程序后删除该文件。

---

## 四、如何设置开机自启动

有两种等价方式：

1. **图形界面**：托盘菜单 → "开机自启动"勾选；或在 设置 → "开机自启动"勾选。
2. **手动**：按 `Win+R` → `shell:startup` → 在打开的文件夹中放一个 `PlanKanban.exe` 的快捷方式。

取消时取消勾选或删除该快捷方式即可。

---

## 五、构建说明（开发者）

### 环境
- .NET 8 SDK (`winget install Microsoft.DotNet.SDK.8`)
- Windows 10/11 x64

### 一键构建单文件 exe
```powershell
.\build.ps1
```
产出位置：`publish\PlanKanban.exe`（单文件，自包含 .NET 运行时，约 63 MB）。

### 等价命令
```powershell
dotnet publish PlanKanban.csproj `
  -c Release `
  -o publish `
  --nologo
```
`PlanKanban.csproj` 已内嵌以下发布属性：
- `SelfContained=true` 内嵌 .NET 运行时
- `PublishSingleFile=true` 单文件
- `IncludeNativeLibrariesForSelfExtract=true` 原生库也打包进单文件
- `EnableCompressionInSingleFile=true` 启用压缩（减小体积）
- 目标 `RuntimeIdentifier=win-x64`

### 运行
无需安装、无需用户机器装有 .NET。直接双击 `PlanKanban.exe` 即可。

---

## 六、验收指标对照

| 指标 | 实测 | 说明 |
|---|---|---|
| 单文件 exe | ✅ | `publish\PlanKanban.exe`，约 63MB |
| 免预装运行时 | ✅ | self-contained，内嵌 .NET 8 运行时 |
| 双击即用 | ✅ | 无安装步骤 |
| 空闲内存 | ~80–150MB（私有） / 物理工作集可被 OS 进一步回收至 30–40MB | WPF + 运行时的固有开销 |
| CPU 空闲 | ≈0% | 收起态面板已 `Collapsed`，无持续渲染 |
| 鼠标贴边响应 | ≈80ms | 默认触发延迟 80ms |
| 重启后恢复 | ✅ | 数据持久化于 `%AppData%\PlanKanban\data.json` |
| 浅/深色 | ✅ | 跟随系统主题（监听 `UserPreferenceChanged`） |

---

## 七、架构简述

### 1. 模块划分
```
PlanKanban/
├── Models/
│   ├── AppSettings.cs     # 设置实体（贴边、快捷键、触发延迟、自动收回、主题、屏幕、防误触、自启动）
│   └── GoalItem.cs        # 目标实体（INotifyPropertyChanged）
├── Services/
│   ├── JsonDataStore.cs   # JSON 持久化（%AppData%\PlanKanban\data.json）
│   ├── AppData.cs         # 数据容器（Goals + CurrentGoalId + Settings）
│   ├── DebouncedSaver.cs  # 防抖写盘，避免高频 IO
│   ├── EdgeDetector.cs    # 边缘悬停检测（DispatcherTimer 轮询鼠标位置，默认 16ms ~60Hz）
│   ├── GlobalHotKeyService.cs  # RegisterHotKey + 隐藏窗口 WndProc
│   ├── AutoStartService.cs# 写注册表 Run 键
│   └── MemoryTuner.cs     # EmptyWorkingSet 主动回收物理页
├── ViewModels/
│   ├── MainViewModel.cs             # MVVM（自实现 RelayCommand）
│   ├── ViewModelBase.cs
│   └── BooleanToVisibilityConverter.cs
├── Views/
│   ├── MainWindow.xaml(.cs)         # 主面板：触发条 + 收起/展开（瞬时定位）
│   └── SettingsWindow.xaml(.cs)     # 设置窗
├── Themes/ Light.xaml Dark.xaml   # 双主题色资源
├── Styles.xaml                       # 全局控件样式（CheckBox/Button/TextBox/ComboBox）
├── ThemeTracker.cs                   # 监听系统主题切换
├── App.xaml(.cs)                      # 单实例、托盘、生命周期、热键注册、主题应用
└── app.manifest                      # PerMonitorV2 DPI、asInvoker
```

### 2. 边缘检测实现
`EdgeDetector` 使用 `DispatcherTimer`，默认 16ms（~60Hz）逐次取 `Forms.Cursor.Position`，
比较它是否落在所选显示屏边缘的 4px 像素带内。命中后需持续 `EdgeTriggerDelayMs`
（默认 80ms）才真正触发展开，避免鼠标擦过时频繁闪烁；开启「防误触」时则只高亮触发条、不展开。
离开边缘自动复位计数。性能：单次只读一个鼠标坐标 + 一次几何比较，对 CPU 几乎无压力。

### 3. 全局热键实现
`GlobalHotKeyService` 在主窗口的 HWND 上调用 `user32!RegisterHotKey`，并通过
`HwndSource.AddHook` 在 WndProc 中拦截 `WM_HOTKEY` 消息。默认 `Alt+Q`，
设置窗口里用 `PreviewKeyDown` 捕获组合键并即时改注。`MOD_NOREPEAT` 避免长按重复触发。

### 4 不抢焦点
主窗口 `Focusable=False`，关闭按钮、设置窗均在单独窗口处理；面板从不夺取前台焦点，
不影响用户当前正在操作的其它程序。


### 5. 资源占用优化
- **收起态面板 Collapsed**：WPF 在 Visibility=Collapsed 时跳过 measure/arrange/render，GPU 几乎为 0。
- **边缘检测降频余地**：可在空闲时进一步降低 timer 频率（当前 16ms ~60Hz 已足够低）。
- **写盘防抖**：所有改动进 `DebouncedSaver`，0.8s 静止后才落盘，避免每勾选一次就 IO。
- **EmptyWorkingSet**：收起后调用 `psapi!EmptyWorkingSet` 让 OS 回收物理页，
  任务管理器的"内存"列会显著下降（私有内存不变，但实际驻留物理 RAM 减少）。
- **单例 Mutex**：防多开重复占用。
- **托盘 = 唯一常驻 UI**：关闭主窗口不退出进程，仅缩到托盘。
- **避免事件泄漏**：删除目标时主动 `-= PropertyChanged`。

### 6. 数据持久化
`%AppData%\PlanKanban\data.json` 包含 `Goals / CurrentGoalId / Settings` 三段。
启动时一次性读入内存，运行时全在内存操作，写盘经防抖；程序崩溃/重启时上次成功保存的状态可恢复。

### 7. 主题
- `Themes/Light.xaml`、`Themes/Dark.xaml` 各定义一套画刷资源。
- `ThemeTracker` 监听 `SystemEvents.UserPreferenceChanged`（General 类别），
  触发 `App.ApplyTheme` 重切资源字典。
- 也可手动在 设置 → 主题 切换 三种模式。

---

## 八、已知约束 / 取舍

- **体积 ~63MB**：self-contained .NET 8 单文件 exe 必然内嵌运行时，已在选型阶段明确取舍：用更大体积换"用户机器免装 .NET"。
- **首次启动稍慢 ~1s**：单文件 exe 内含压缩运行时，首次解压到临时目录缓存后即快速启动。
- **拖拽排序**：以"↑/↓"按钮代替，等效且对鼠标精度要求更低。