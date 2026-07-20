using System.Text.Json;
using PlanKanban.Models;

namespace PlanKanban.Services;

public sealed class AppData
{
    public List<GoalItem> Goals { get; set; } = new();
    public List<GoalItem> History { get; set; } = new();   // 已删除目标，保留供日后恢复
    public List<GoalItem> Archive { get; set; } = new();   // 已归档完成项，含完成时间戳
    public AppSettings Settings { get; set; } = new();

    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, JsonElement>? _ExtensionData { get; set; }
}