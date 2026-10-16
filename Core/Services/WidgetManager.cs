using System.IO;
using System.Text.Json;
using ChattyWidgets.Core.Models;
using ChattyWidgets.Core.Win32;
using ChattyWidgets.UI;
using Microsoft.Win32;

namespace ChattyWidgets.Core.Services;

public class WidgetManager
{
    private static WidgetManager? _instance;
    public static WidgetManager Instance => _instance ??= new WidgetManager();

    private readonly Dictionary<string, WidgetWindow> _activeWindows = new();
    public AppSettings Settings { get; private set; } = new();

    public event Action? WidgetsChanged;

    private WidgetManager()
    {
    }

    public void Initialize()
    {
        LoadSettings();
        ChwPackageService.RegisterFileAssociation();

        // Start all widgets that were saved as enabled
        var installed = ChwPackageService.GetInstalledWidgets();
        foreach (var (manifest, dir) in installed)
        {
            if (Settings.Widgets.TryGetValue(manifest.Id, out var widgetSetting))
            {
                if (widgetSetting.IsEnabled)
                {
                    StartWidget(manifest.Id);
                }
            }
        }
    }

    public bool IsWidgetActive(string widgetId) => _activeWindows.ContainsKey(widgetId);

    public WidgetWindow? GetActiveWindow(string widgetId)
    {
        _activeWindows.TryGetValue(widgetId, out var win);
        return win;
    }

    public bool StartWidget(string widgetId)
    {
        if (_activeWindows.TryGetValue(widgetId, out var existing))
        {
            existing.Show();
            existing.Activate();
            existing.Topmost = true;
            existing.Topmost = false;
            return true;
        }

        var installed = ChwPackageService.GetInstalledWidgets();
        var item = installed.FirstOrDefault(x => x.Manifest.Id == widgetId);
        if (item.Manifest == null)
            return false;

        if (!Settings.Widgets.TryGetValue(widgetId, out var widgetSetting))
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double defaultLeft = Math.Max(50, screenWidth - item.Manifest.DefaultWidth - 40);
            double defaultTop = 60 + (_activeWindows.Count * 60);

            widgetSetting = new WidgetInstanceSettings
            {
                Id = widgetId,
                IsEnabled = true,
                Width = item.Manifest.DefaultWidth,
                Height = item.Manifest.DefaultHeight,
                Left = defaultLeft,
                Top = defaultTop,
                Layer = WidgetLayer.Desktop,
                IsClickThrough = false,
                Opacity = 1.0
            };
            Settings.Widgets[widgetId] = widgetSetting;
        }

        widgetSetting.IsEnabled = true;

        var window = new WidgetWindow(item.Manifest, item.DirectoryPath, widgetSetting);
        window.WidgetClosed += OnWidgetClosed;
        window.SettingsChanged += OnWidgetSettingsChanged;

        _activeWindows[widgetId] = window;
        window.Show();
        window.Activate();

        SaveSettings();
        WidgetsChanged?.Invoke();
        return true;
    }

    public void StopWidget(string widgetId)
    {
        if (_activeWindows.TryGetValue(widgetId, out var window))
        {
            window.Settings.IsEnabled = false;
            window.Close();
            _activeWindows.Remove(widgetId);
            SaveSettings();
            WidgetsChanged?.Invoke();
        }
    }

    public void ToggleWidget(string widgetId)
    {
        if (IsWidgetActive(widgetId))
            StopWidget(widgetId);
        else
            StartWidget(widgetId);
    }

    private void OnWidgetClosed(WidgetWindow window)
    {
        if (_activeWindows.ContainsKey(window.Manifest.Id))
        {
            _activeWindows.Remove(window.Manifest.Id);
            SaveSettings();
            WidgetsChanged?.Invoke();
        }
    }

    private void OnWidgetSettingsChanged(WidgetWindow window)
    {
        Settings.Widgets[window.Manifest.Id] = window.Settings;
        SaveSettings();
    }

    public void LoadSettings()
    {
        try
        {
            if (File.Exists(ChwPackageService.SettingsFile))
            {
                string json = File.ReadAllText(ChwPackageService.SettingsFile);
                Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                Settings = new AppSettings();
            }
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    public void SaveSettings()
    {
        try
        {
            string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ChwPackageService.SettingsFile, json);
        }
        catch { }
    }

    public void SetAutostart(bool enable)
    {
        Settings.StartWithWindows = enable;
        SaveSettings();

        try
        {
            const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using var key = Registry.CurrentUser.OpenSubKey(runKey, true);
            if (key != null)
            {
                string? exe = Environment.ProcessPath;
                if (enable && !string.IsNullOrEmpty(exe))
                {
                    key.SetValue("ChattyWidgets", $"\"{exe}\" --minimized");
                }
                else
                {
                    key.DeleteValue("ChattyWidgets", false);
                }
            }
        }
        catch { }
    }
}
