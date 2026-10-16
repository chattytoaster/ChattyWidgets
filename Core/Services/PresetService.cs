using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChattyWidgets.Core.Models;

namespace ChattyWidgets.Core.Services;

public class PresetService
{
    private static PresetService? _instance;
    public static PresetService Instance => _instance ??= new PresetService();

    public event Action? PresetsChanged;

    private PresetService()
    {
    }

    public void EnsureDefaultPresets() => CleanDefaultPresets();

    public void CleanDefaultPresets()
    {
        var settings = WidgetManager.Instance.Settings;
        settings.Presets ??= new List<DesktopPreset>();

        // Remove old built-in presets so user has a clean slate
        int removed = settings.Presets.RemoveAll(p => p.Id is "work" or "media" or "gaming" or "minimal");
        if (removed > 0)
        {
            if (settings.ActivePresetId is "work" or "media" or "gaming" or "minimal")
            {
                settings.ActivePresetId = string.Empty;
            }
            WidgetManager.Instance.SaveSettings();
            PresetsChanged?.Invoke();
        }
    }

    public void ApplyPreset(string presetId)
    {
        var settings = WidgetManager.Instance.Settings;
        var preset = settings.Presets.FirstOrDefault(p => p.Id == presetId);
        if (preset == null) return;

        settings.ActivePresetId = presetId;

        // Stop all active widgets first
        var activeWindows = WidgetManager.Instance.GetActiveWindows().ToList();
        foreach (var win in activeWindows)
        {
            if (!preset.Widgets.ContainsKey(win.InstanceId))
            {
                WidgetManager.Instance.StopWidget(win.InstanceId);
            }
        }

        // Apply preset positions and launch widgets
        foreach (var kvp in preset.Widgets)
        {
            string instanceId = kvp.Key;
            var presetSetting = kvp.Value;
            string baseId = presetSetting.BaseWidgetId;

            settings.Widgets[instanceId] = new WidgetInstanceSettings
            {
                Id = instanceId,
                WidgetId = baseId,
                Title = !string.IsNullOrWhiteSpace(presetSetting.Title) ? presetSetting.Title : "",
                IsEnabled = presetSetting.IsEnabled,
                Left = presetSetting.Left,
                Top = presetSetting.Top,
                Width = presetSetting.Width,
                Height = presetSetting.Height,
                Layer = presetSetting.Layer,
                IsClickThrough = presetSetting.IsClickThrough,
                IsLocked = presetSetting.IsLocked,
                Opacity = presetSetting.Opacity,
                Scale = presetSetting.Scale,
                AutoHideAnchorDot = presetSetting.AutoHideAnchorDot,
                TargetMonitor = presetSetting.TargetMonitor
            };

            if (presetSetting.IsEnabled)
            {
                WidgetManager.Instance.StartWidget(baseId, instanceId);
                var win = WidgetManager.Instance.GetActiveWindow(instanceId);
                if (win != null)
                {
                    win.Left = presetSetting.Left;
                    win.Top = presetSetting.Top;
                    win.Width = presetSetting.Width;
                    win.Height = presetSetting.Height;
                }
            }
        }

        WidgetManager.Instance.SaveSettings();
        PresetsChanged?.Invoke();
    }

    public DesktopPreset SaveCurrentAsPreset(string name, string icon = "⭐", string description = "Пользовательский пресет", string author = "User")
    {
        var settings = WidgetManager.Instance.Settings;
        settings.Presets ??= new List<DesktopPreset>();
        string id = "preset_" + Guid.NewGuid().ToString("N")[..8];

        var dict = new Dictionary<string, WidgetInstanceSettings>();
        foreach (var win in WidgetManager.Instance.GetActiveWindows())
        {
            dict[win.InstanceId] = new WidgetInstanceSettings
            {
                Id = win.InstanceId,
                WidgetId = win.BaseWidgetId,
                Title = win.Settings.Title,
                IsEnabled = true,
                Left = win.Left,
                Top = win.Top,
                Width = win.Width,
                Height = win.Height,
                Layer = win.Settings.Layer,
                IsClickThrough = win.Settings.IsClickThrough,
                IsLocked = win.Settings.IsLocked,
                Opacity = win.Settings.Opacity,
                Scale = win.Settings.Scale,
                AutoHideAnchorDot = win.Settings.AutoHideAnchorDot,
                TargetMonitor = win.Settings.TargetMonitor
            };
        }

        var newPreset = new DesktopPreset
        {
            Id = id,
            Name = name,
            Icon = icon,
            Description = description,
            Author = author,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            Widgets = dict
        };

        settings.Presets.Add(newPreset);
        settings.ActivePresetId = id;
        WidgetManager.Instance.SaveSettings();
        PresetsChanged?.Invoke();

        return newPreset;
    }

    public void ExportPresetToFile(DesktopPreset preset, string filePath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        string json = JsonSerializer.Serialize(preset, options);
        File.WriteAllText(filePath, json, Encoding.UTF8);
    }

    public DesktopPreset ImportPresetFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Файл пресета не найден", filePath);

        string json = File.ReadAllText(filePath, Encoding.UTF8);
        var preset = JsonSerializer.Deserialize<DesktopPreset>(json);
        if (preset == null || string.IsNullOrWhiteSpace(preset.Name))
            throw new InvalidOperationException("Некорректный файл пресета .chwp.");

        // Generate a new unique preset ID to avoid collisions
        preset.Id = "preset_" + Guid.NewGuid().ToString("N")[..8];

        var settings = WidgetManager.Instance.Settings;
        settings.Presets ??= new List<DesktopPreset>();

        // If a preset with this name already exists, add indicator
        if (settings.Presets.Any(p => p.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase)))
        {
            preset.Name = $"{preset.Name} (Импорт)";
        }

        settings.Presets.Add(preset);
        settings.ActivePresetId = preset.Id;
        WidgetManager.Instance.SaveSettings();
        PresetsChanged?.Invoke();

        return preset;
    }

    public void DeletePreset(string presetId)
    {
        var settings = WidgetManager.Instance.Settings;
        var p = settings.Presets?.FirstOrDefault(x => x.Id == presetId);
        if (p != null)
        {
            settings.Presets?.Remove(p);
            if (settings.ActivePresetId == presetId)
            {
                settings.ActivePresetId = string.Empty;
            }
            WidgetManager.Instance.SaveSettings();
            PresetsChanged?.Invoke();
        }
    }
}
