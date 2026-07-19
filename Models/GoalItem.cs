using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace PlanKanban.Models;

public sealed class GoalItem : INotifyPropertyChanged
{
    private string _title = "";
    private bool _isDone;
    private string _note = "";
    private DateTime? _dueDate;
    private string _tag = "";
    private bool _isEditing;

    public Guid Id { get; init; } = Guid.NewGuid();

    public string Title
    {
        get => _title;
        set { if (_title != value) { _title = value; OnPropertyChanged(); } }
    }

    public bool IsDone
    {
        get => _isDone;
        set { if (_isDone != value) { _isDone = value; OnPropertyChanged(); } }
    }

    public string Note
    {
        get => _note;
        set { if (_note != value) { _note = value; OnPropertyChanged(); } }
    }

    public string Tag
    {
        get => _tag;
        set { if (_tag != value) { _tag = value; OnPropertyChanged(); } }
    }

    public DateTime? DueDate
    {
        get => _dueDate;
        set { if (_dueDate != value) { _dueDate = value; OnPropertyChanged(); } }
    }

    /// <summary>归档时间：一旦归档即固化，记录该事项的"完成时刻"。</summary>
    public DateTime? ArchivedAt { get; set; }

    [JsonIgnore]
    public bool IsEditing
    {
        get => _isEditing;
        set { if (_isEditing != value) { _isEditing = value; OnPropertyChanged(); } }
    }

    [JsonIgnore]
    public string ArchivedAtText => ArchivedAt.HasValue
        ? ArchivedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
        : "";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}