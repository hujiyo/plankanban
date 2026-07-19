using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PlanKanban.Models;

namespace PlanKanban.Services;

/// <summary>JSON 文件持久化，存放在 %AppData%\PlanKanban\data.json。
/// 写入使用防抖（由调用方控制），避免高频 IO。</summary>
public sealed class JsonDataStore
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlanKanban");
    private static readonly string FilePath = Path.Combine(Dir, "data.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string DataFilePath => FilePath;
    public static string DataDir => Dir;

    public AppData Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var data = JsonSerializer.Deserialize<AppData>(json, JsonOpts);
                if (data != null)
                {
                    data.Goals ??= new();
                    data.Settings ??= new();
                    return data;
                }
            }
        }
        catch { /* 损坏文件忽略，使用默认 */ }
        return new AppData();
    }

    public void Save(AppData data)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.Serialize(data, JsonOpts);
            File.WriteAllText(FilePath, json);
        }
        catch { /* 静默失败，避免后台保存抛异常导致崩溃 */ }
    }
}