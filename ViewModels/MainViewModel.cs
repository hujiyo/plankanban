using System.Collections.ObjectModel;
using System.Windows.Input;
using PlanKanban.Models;
using PlanKanban.Services;

namespace PlanKanban.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly AppData _data;
    private readonly DebouncedSaver _saver;
    private string _newGoalText = "";

    public ObservableCollection<GoalItem> Goals { get; }
    public ObservableCollection<GoalItem> History { get; }
    public ObservableCollection<GoalItem> Archive { get; }
    public AppSettings Settings => _data.Settings;
    public event Action? StatsChanged;

    public GoalItem? CurrentGoal => Goals.FirstOrDefault(g => !g.IsDone);

    public bool HasCurrentGoal => CurrentGoal != null;

    private void SyncCurrentFlags()
    {
        var cur = CurrentGoal;
        foreach (var g in Goals)
        {
            g.IsCurrent = (g == cur);
        }
    }

    private void EmitDerivedProps()
    {
        OnPropertyChanged(nameof(CurrentGoal));
        OnPropertyChanged(nameof(HasCurrentGoal));
        SyncCurrentFlags();
    }

    public string NewGoalText
    {
        get => _newGoalText;
        set => Set(ref _newGoalText, value);
    }

    public MainViewModel(AppData data, DebouncedSaver saver)
    {
        _data = data;
        _saver = saver;
        Goals = new ObservableCollection<GoalItem>(data.Goals);
        History = new ObservableCollection<GoalItem>(data.History ?? new());
        Archive = new ObservableCollection<GoalItem>(data.Archive ?? new());
        foreach (var g in Goals) { g.PropertyChanged += OnGoalChanged; }
        SyncCurrentFlags();
        Goals.CollectionChanged += (_, _) =>
        {
            ScheduleSave();
            StatsChanged?.Invoke();
            EmitDerivedProps();
        };
        Archive.CollectionChanged += (_, _) => { ScheduleSave(); StatsChanged?.Invoke(); };
    }

    private void OnGoalChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(GoalItem.IsEditing)) ScheduleSave();
        if (e.PropertyName == nameof(GoalItem.IsDone))
        {
            StatsChanged?.Invoke();
            EmitDerivedProps();
        }
        if (e.PropertyName == nameof(GoalItem.Title) && sender is GoalItem g && g == CurrentGoal)
            OnPropertyChanged(nameof(CurrentGoal));
    }

    private void ScheduleSave()
    {
        _data.Goals = Goals.ToList();
        _data.History = History.ToList();
        _data.Archive = Archive.ToList();
        _saver.Schedule(_data);
    }

    // ----- 命令 -----
    public ICommand AddGoalCommand => new RelayCommand(_ =>
    {
        var text = NewGoalText?.Trim();
        if (string.IsNullOrEmpty(text)) return;
        var g = new GoalItem { Title = text };
        g.PropertyChanged += OnGoalChanged;
        Goals.Add(g);
        NewGoalText = "";
        ScheduleSave();
    });

    public ICommand InsertGoalCommand => new RelayCommand(idx =>
    {
        var text = NewGoalText?.Trim();
        if (string.IsNullOrEmpty(text)) return;
        var g = new GoalItem { Title = text };
        g.PropertyChanged += OnGoalChanged;
        int at = idx is int i ? i : Goals.Count;
        if (at < 0) at = 0;
        if (at > Goals.Count) at = Goals.Count;
        Goals.Insert(Math.Clamp(at, 0, Goals.Count), g);
        NewGoalText = "";
        ScheduleSave();
    });

    public ICommand DeleteGoalCommand => new RelayCommand(p =>
    {
        if (p is GoalItem g)
        {
            g.PropertyChanged -= OnGoalChanged;
            Goals.Remove(g);
            // 移入历史，保留可恢复
            History.Insert(0, g);
            if (History.Count > 200) History.RemoveAt(History.Count - 1);
            ScheduleSave();
        }
    });

    public ICommand RestoreFromHistoryCommand => new RelayCommand(p =>
    {
        if (p is GoalItem g)
        {
            History.Remove(g);
            g.IsDone = false;
            g.PropertyChanged += OnGoalChanged;
            Goals.Add(g);
            ScheduleSave();
        }
    });

    public ICommand ClearHistoryCommand => new RelayCommand(_ =>
    {
        History.Clear();
        ScheduleSave();
    });

    public ICommand ToggleDoneCommand => new RelayCommand(p =>
    {
        if (p is GoalItem g) g.IsDone = !g.IsDone;
    });

    public ICommand PinToTopCommand => new RelayCommand(p =>
    {
        if (p is GoalItem g)
        {
            int i = Goals.IndexOf(g);
            if (i > 0) { Goals.Move(i, 0); ScheduleSave(); }
        }
    });

    public ICommand StartEditCommand => new RelayCommand(p =>
    {
        if (p is GoalItem g) { foreach (var x in Goals) x.IsEditing = false; g.IsEditing = true; }
    });

    public ICommand ConfirmEditCommand => new RelayCommand(p =>
    {
        if (p is GoalItem g) { g.IsEditing = false; ScheduleSave(); }
    });

    public ICommand ArchiveCompletedCommand => new RelayCommand(_ =>
    {
        var done = Goals.Where(x => x.IsDone).ToList();
        foreach (var d in done)
        {
            d.PropertyChanged -= OnGoalChanged;
            Goals.Remove(d);
            // 固化完成时间戳
            d.ArchivedAt = DateTime.UtcNow;
            Archive.Insert(0, d);
            if (Archive.Count > 1000) Archive.RemoveAt(Archive.Count - 1);
        }
        ScheduleSave();
    });

    public ICommand RestoreFromArchiveCommand => new RelayCommand(p =>
    {
        if (p is GoalItem g)
        {
            Archive.Remove(g);
            g.ArchivedAt = null;
            g.IsDone = false;
            g.PropertyChanged += OnGoalChanged;
            Goals.Add(g);
            ScheduleSave();
        }
    });

    public ICommand ClearArchiveCommand => new RelayCommand(_ =>
    {
        Archive.Clear();
        ScheduleSave();
    });

    public ICommand MoveUpCommand => new RelayCommand(p =>
    {
        if (p is GoalItem g)
        {
            int i = Goals.IndexOf(g);
            if (i > 0) { Goals.Move(i, i - 1); ScheduleSave(); }
        }
    });

    public ICommand MoveDownCommand => new RelayCommand(p =>
    {
        if (p is GoalItem g)
        {
            int i = Goals.IndexOf(g);
            if (i >= 0 && i < Goals.Count - 1) { Goals.Move(i, i + 1); ScheduleSave(); }
        }
    });
}