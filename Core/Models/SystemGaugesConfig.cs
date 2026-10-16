using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChattyWidgets.Core.Services;

namespace ChattyWidgets.Core.Models;

public enum GaugeDisplayType
{
    Bar,
    Circle
}

public class MetricGaugeItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("displayType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GaugeDisplayType DisplayType { get; set; } = GaugeDisplayType.Bar;

    [JsonPropertyName("colorHex")]
    public string ColorHex { get; set; } = "#38BDF8";
}

public class SystemGaugesConfig
{
    [JsonPropertyName("items")]
    public List<MetricGaugeItem> Items { get; set; } = new();

    public static SystemGaugesConfig CreateDefault()
    {
        return new SystemGaugesConfig
        {
            Items = new List<MetricGaugeItem>
            {
                new() { Id = "cpu", Title = "Процессор (CPU)", IsEnabled = true, DisplayType = GaugeDisplayType.Bar, ColorHex = "#38BDF8" },
                new() { Id = "ram", Title = "Память (RAM)", IsEnabled = true, DisplayType = GaugeDisplayType.Bar, ColorHex = "#A855F7" },
                new() { Id = "disk", Title = "Диск (C:)", IsEnabled = true, DisplayType = GaugeDisplayType.Bar, ColorHex = "#F59E0B" },
                new() { Id = "net", Title = "Сеть (Загрузка / Отдача)", IsEnabled = true, DisplayType = GaugeDisplayType.Bar, ColorHex = "#10B981" },
                new() { Id = "gpu", Title = "Видеокарта (GPU)", IsEnabled = false, DisplayType = GaugeDisplayType.Bar, ColorHex = "#EC4899" }
            }
        };
    }

    public static string GetConfigPath(string instanceId)
    {
        string dir = Path.Combine(ChwPackageService.DataDir, instanceId);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "gauges_config.json");
    }

    public static SystemGaugesConfig Load(string instanceId)
    {
        try
        {
            string path = GetConfigPath(instanceId);
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<SystemGaugesConfig>(json);
                if (config != null && config.Items.Count > 0)
                {
                    EnsureAllMetricsPresent(config);
                    return config;
                }
            }
        }
        catch { }

        return CreateDefault();
    }

    public static void Save(string instanceId, SystemGaugesConfig config)
    {
        try
        {
            string path = GetConfigPath(instanceId);
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch { }
    }

    private static void EnsureAllMetricsPresent(SystemGaugesConfig config)
    {
        var defaultCfg = CreateDefault();
        foreach (var defItem in defaultCfg.Items)
        {
            if (!config.Items.Any(i => i.Id.Equals(defItem.Id, StringComparison.OrdinalIgnoreCase)))
            {
                config.Items.Add(defItem);
            }
        }
    }
}
