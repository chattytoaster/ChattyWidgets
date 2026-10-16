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

    public void EnsureDefaultPresets()
    {
        var settings = WidgetManager.Instance.Settings;
        if (settings.Presets == null || settings.Presets.Count == 0)
        {
            settings.Presets = GetDefaultPresets();
            WidgetManager.Instance.SaveSettings();
        }
    }

    public List<DesktopPreset> GetDefaultPresets()
    {
        return new List<DesktopPreset>
        {
            new DesktopPreset
            {
                Id = "work",
                Name = "Работа & Продуктивность",
                Icon = "💼",
                Description = "Календарь, Заметки ToDo, Погода и Финансы",
                Widgets = new Dictionary<string, WidgetInstanceSettings>
                {
                    ["com.chatty.calendar-glass"] = new() { Id = "com.chatty.calendar-glass", IsEnabled = true, Left = 1580, Top = 80, Width = 310, Height = 340 },
                    ["com.chatty.sticky-notes"] = new() { Id = "com.chatty.sticky-notes", IsEnabled = true, Left = 1580, Top = 440, Width = 310, Height = 360 },
                    ["com.chatty.weather-card"] = new() { Id = "com.chatty.weather-card", IsEnabled = true, Left = 1240, Top = 80, Width = 320, Height = 210 },
                    ["com.chatty.crypto-tracker"] = new() { Id = "com.chatty.crypto-tracker", IsEnabled = true, Left = 1240, Top = 310, Width = 320, Height = 250 }
                }
            },
            new DesktopPreset
            {
                Id = "media",
                Name = "Медиа & Релакс",
                Icon = "🎵",
                Description = "Музыкальный плеер, Фото/видео рамка и Часы",
                Widgets = new Dictionary<string, WidgetInstanceSettings>
                {
                    ["com.chatty.music-player"] = new() { Id = "com.chatty.music-player", IsEnabled = true, Left = 1500, Top = 80, Width = 380, Height = 260 },
                    ["com.chatty.media-frame"] = new() { Id = "com.chatty.media-frame", IsEnabled = true, Left = 1500, Top = 360, Width = 380, Height = 340 },
                    ["com.chatty.clock-glass"] = new() { Id = "com.chatty.clock-glass", IsEnabled = true, Left = 1180, Top = 80, Width = 300, Height = 200 }
                }
            },
            new DesktopPreset
            {
                Id = "gaming",
                Name = "Гейминг & Мониторинг",
                Icon = "🎮",
                Description = "Системный монитор и компактные часы",
                Widgets = new Dictionary<string, WidgetInstanceSettings>
                {
                    ["com.chatty.system-gauges"] = new() { Id = "com.chatty.system-gauges", IsEnabled = true, Left = 1580, Top = 60, Width = 310, Height = 400 },
                    ["com.chatty.clock-glass"] = new() { Id = "com.chatty.clock-glass", IsEnabled = true, Left = 1580, Top = 480, Width = 310, Height = 200 }
                }
            },
            new DesktopPreset
            {
                Id = "minimal",
                Name = "Минимализм",
                Icon = "✨",
                Description = "Только стильные полупрозрачные часы",
                Widgets = new Dictionary<string, WidgetInstanceSettings>
                {
                    ["com.chatty.clock-glass"] = new() { Id = "com.chatty.clock-glass", IsEnabled = true, Left = 1580, Top = 80, Width = 310, Height = 200 }
                }
            }
        };
    }

    public void ApplyPreset(string presetId)
    {
        var settings = WidgetManager.Instance.Settings;
        var preset = settings.Presets.FirstOrDefault(p => p.Id == presetId);
        if (preset == null) return;

        settings.ActivePresetId = presetId;

        // Stop all active widgets first
        var activeIds = WidgetManager.Instance.GetActiveWindows().Select(w => w.Manifest.Id).ToList();
        foreach (var id in activeIds)
        {
            if (!preset.Widgets.ContainsKey(id))
            {
                WidgetManager.Instance.StopWidget(id);
            }
        }

        // Apply preset positions and launch widgets
        foreach (var kvp in preset.Widgets)
        {
            string widgetId = kvp.Key;
            var presetSetting = kvp.Value;

            settings.Widgets[widgetId] = new WidgetInstanceSettings
            {
                Id = widgetId,
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
                WidgetManager.Instance.StartWidget(widgetId);
                var win = WidgetManager.Instance.GetActiveWindow(widgetId);
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

    public DesktopPreset SaveCurrentAsPreset(string name, string icon = "⭐", string description = "Пользовательский пресет")
    {
        var settings = WidgetManager.Instance.Settings;
        string id = "preset_" + Guid.NewGuid().ToString("N")[..8];

        var dict = new Dictionary<string, WidgetInstanceSettings>();
        foreach (var win in WidgetManager.Instance.GetActiveWindows())
        {
            dict[win.Manifest.Id] = new WidgetInstanceSettings
            {
                Id = win.Manifest.Id,
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
            Widgets = dict
        };

        settings.Presets.Add(newPreset);
        settings.ActivePresetId = id;
        WidgetManager.Instance.SaveSettings();
        PresetsChanged?.Invoke();

        return newPreset;
    }

    public void DeletePreset(string presetId)
    {
        var settings = WidgetManager.Instance.Settings;
        var p = settings.Presets.FirstOrDefault(x => x.Id == presetId);
        if (p != null)
        {
            settings.Presets.Remove(p);
            if (settings.ActivePresetId == presetId)
            {
                settings.ActivePresetId = "all";
            }
            WidgetManager.Instance.SaveSettings();
            PresetsChanged?.Invoke();
        }
    }
}
