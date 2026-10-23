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
    public bool IsInitialized { get; private set; }

    public event Action? WidgetsChanged;

    private WidgetManager()
    {
    }

    private static void Log(string msg)
    {
        try
        {
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChattyWidgets");
            File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now:HH:mm:ss.fff}] [WM] {msg}\n");
        }
        catch { }
    }

    public void Initialize()
    {
        Log("Initialize start");
        LoadSettings();
        IsInitialized = true;
        Log("LoadSettings done");
        ChwPackageService.RegisterFileAssociation();

        var installed = ChwPackageService.GetInstalledWidgets();
        var installedSet = new HashSet<string>(installed.Select(x => x.Manifest.Id));
        Log($"Installed count: {installed.Count}");

        // If Settings.Widgets is empty but widgets are installed, restore default active widgets
        // so user never experiences an empty desktop after an update or cleared settings file
        if (Settings.Widgets.Count == 0 && installed.Count > 0)
        {
            Log("Settings.Widgets was empty; restoring default widgets for installed packages");
            var defaultStartIds = new[]
            {
                "com.github.hlebsmaslomcht.analog-clock",
                "com.chatty.weather-card",
                "com.chatty.system-gauges",
                "com.chatty.music-player"
            };

            foreach (var defaultId in defaultStartIds)
            {
                if (installedSet.Contains(defaultId))
                {
                    GetOrCreateWidgetSettings(defaultId);
                }
            }
            SaveSettings();
        }

        // Launch all widget instances that were saved as enabled
        foreach (var kvp in Settings.Widgets.ToList())
        {
            string instanceId = kvp.Key;
            var widgetSetting = kvp.Value;
            string baseId = widgetSetting.BaseWidgetId;

            if (widgetSetting.IsEnabled && installedSet.Contains(baseId))
            {
                Log($"Starting {baseId} ({instanceId})");
                try
                {
                    StartWidget(baseId, instanceId);
                    Log($"Started {baseId} ({instanceId}) OK");
                }
                catch (Exception ex)
                {
                    Log($"ERROR starting {baseId}: {ex}");
                }
            }
        }

        Log("Presets check");
        PresetService.Instance.EnsureDefaultPresets();
        Log("HotReload check");
        HotReloadService.Instance.Start();
        Log("Initialize complete");
    }

    public IEnumerable<WidgetWindow> GetActiveWindows() => _activeWindows.Values;
    public int ActiveCount => _activeWindows.Count;

    public bool IsWidgetActive(string instanceOrBaseId) =>
        _activeWindows.ContainsKey(instanceOrBaseId) || _activeWindows.Values.Any(w => w.BaseWidgetId == instanceOrBaseId);

    public int GetActiveInstancesCount(string baseWidgetId) =>
        _activeWindows.Values.Count(w => w.BaseWidgetId == baseWidgetId);

    public WidgetWindow? GetWindow(string instanceId) =>
        _activeWindows.TryGetValue(instanceId, out var win) ? win : null;

    public WidgetWindow? GetActiveWindow(string instanceOrBaseId)
    {
        if (_activeWindows.TryGetValue(instanceOrBaseId, out var win))
            return win;
        return _activeWindows.Values.FirstOrDefault(w => w.BaseWidgetId == instanceOrBaseId);
    }

    public void ApplyGlobalTheme(string theme)
    {
        Settings.Theme = theme;
        SaveSettings();
        foreach (var win in _activeWindows.Values)
        {
            try { win.ApplyTheme(); } catch { }
        }
    }

    public void UpdateAlwaysResizable(bool alwaysResizable)
    {
        Settings.AlwaysResizable = alwaysResizable;
        SaveSettings();
        foreach (var win in _activeWindows.Values)
        {
            try { win.UpdateResizeState(); } catch { }
        }
    }

    public void MoveWidgetToMonitor(string instanceId, int monitorIndex)
    {
        if (_activeWindows.TryGetValue(instanceId, out var window))
        {
            var mon = ScreenHelper.GetMonitor(monitorIndex);
            window.Settings.TargetMonitor = monitorIndex;
            window.Left = mon.Left + 50;
            window.Top = mon.Top + 60;
            window.Settings.Left = window.Left;
            window.Settings.Top = window.Top;
            SaveSettings();
        }
    }

    public bool StartWidget(string widgetId, string? instanceId = null)
    {
        string instId = !string.IsNullOrEmpty(instanceId) ? instanceId : widgetId;

        if (_activeWindows.TryGetValue(instId, out var existing))
        {
            existing.Show();
            if (existing.Settings.Layer != WidgetLayer.Desktop)
            {
                existing.Activate();
                existing.Topmost = true;
                existing.Topmost = false;
            }
            return true;
        }

        var installed = ChwPackageService.GetInstalledWidgets();
        var item = installed.FirstOrDefault(x => x.Manifest.Id == widgetId);
        if (item.Manifest == null)
            return false;

        if (!Settings.Widgets.TryGetValue(instId, out var widgetSetting))
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double defaultLeft = Math.Max(50, screenWidth - item.Manifest.DefaultWidth - 40);
            int count = Math.Max(_activeWindows.Count, Settings.Widgets.Count);
        double defaultTop = 60 + (count * 70);

            widgetSetting = new WidgetInstanceSettings
            {
                Id = instId,
                WidgetId = widgetId,
                Title = item.Manifest.Name,
                IsEnabled = true,
                Width = item.Manifest.DefaultWidth,
                Height = item.Manifest.DefaultHeight,
                Left = defaultLeft,
                Top = defaultTop,
                Layer = WidgetLayer.Desktop,
                IsClickThrough = false,
                AutoHideAnchorDot = true,
                Opacity = 1.0
            };
            Settings.Widgets[instId] = widgetSetting;
        }
        else
        {
            widgetSetting.WidgetId = widgetId;
            if (string.IsNullOrWhiteSpace(widgetSetting.Title))
            {
                widgetSetting.Title = item.Manifest.Name;
            }
        }

        widgetSetting.IsEnabled = true;

        Log($"StartWidget: creating WidgetWindow for {instId}");
        var window = new WidgetWindow(item.Manifest, item.DirectoryPath, widgetSetting);
        window.WidgetClosed += OnWidgetClosed;
        window.SettingsChanged += OnWidgetSettingsChanged;

        _activeWindows[instId] = window;
        Log($"StartWidget: calling window.Show() for {instId}");
        window.Show();
        if (window.Settings.Layer != WidgetLayer.Desktop)
        {
            Log($"StartWidget: calling window.Activate() for {instId}");
            window.Activate();
        }
        Log($"StartWidget: finished for {instId}");

        SaveSettings();
        WidgetsChanged?.Invoke();
        return true;
    }

    public void StopWidget(string instanceOrWidgetId)
    {
        if (_activeWindows.TryGetValue(instanceOrWidgetId, out var window))
        {
            window.Settings.IsEnabled = false;
            window.Close();
            _activeWindows.Remove(instanceOrWidgetId);
            SaveSettings();
            WidgetsChanged?.Invoke();
            return;
        }

        var matchingWindows = _activeWindows.Values
            .Where(w => w.BaseWidgetId == instanceOrWidgetId)
            .ToList();

        if (matchingWindows.Count > 0)
        {
            foreach (var w in matchingWindows)
            {
                w.Settings.IsEnabled = false;
                w.Close();
                _activeWindows.Remove(w.InstanceId);
            }
            SaveSettings();
            WidgetsChanged?.Invoke();
        }
    }

    public void RestartWidgetInstances(string baseWidgetId)
    {
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            var matchingWindows = _activeWindows.Values
                .Where(w => w.BaseWidgetId == baseWidgetId)
                .Select(w => (w.InstanceId, w.Settings))
                .ToList();

            if (matchingWindows.Count == 0) return;

            foreach (var (instId, _) in matchingWindows)
            {
                if (_activeWindows.TryGetValue(instId, out var win))
                {
                    win.Close();
                    _activeWindows.Remove(instId);
                }
            }

            foreach (var (instId, _) in matchingWindows)
            {
                StartWidget(baseWidgetId, instId);
            }
        });
    }

    public string? DuplicateWidget(string sourceId)
    {
        try
        {
            WidgetInstanceSettings? sourceSettings = null;
            if (Settings.Widgets.TryGetValue(sourceId, out var s))
            {
                sourceSettings = s;
            }
            else if (_activeWindows.TryGetValue(sourceId, out var win))
            {
                sourceSettings = win.Settings;
            }

            string baseWidgetId = sourceSettings?.BaseWidgetId ?? (sourceId.Contains('#') ? sourceId.Split('#')[0] : sourceId);
            var installed = ChwPackageService.GetInstalledWidgets();
            var item = installed.FirstOrDefault(x => x.Manifest.Id == baseWidgetId);
            if (item.Manifest == null)
                return null;

            string newInstanceId = $"{baseWidgetId}#inst_{Guid.NewGuid().ToString("N")[..8]}";

            int copyIndex = Settings.Widgets.Values.Count(w => w.BaseWidgetId == baseWidgetId) + 1;
            string copyTitle = $"{item.Manifest.Name} (Копия {copyIndex})";

            double newLeft = (sourceSettings != null ? sourceSettings.Left : 100) + 35;
            double newTop = (sourceSettings != null ? sourceSettings.Top : 100) + 35;

            double virtLeft = SystemParameters.VirtualScreenLeft;
            double virtTop = SystemParameters.VirtualScreenTop;
            double virtWidth = SystemParameters.VirtualScreenWidth;
            double virtHeight = SystemParameters.VirtualScreenHeight;

            if (newLeft > virtLeft + virtWidth - 120)
                newLeft = virtLeft + 50;
            if (newTop > virtTop + virtHeight - 120)
                newTop = virtTop + 60;

            var newSettings = new WidgetInstanceSettings
            {
                Id = newInstanceId,
                WidgetId = baseWidgetId,
                Title = copyTitle,
                IsEnabled = true,
                Left = Math.Round(newLeft),
                Top = Math.Round(newTop),
                Width = sourceSettings != null && sourceSettings.Width > 0 ? Math.Round(sourceSettings.Width) : item.Manifest.DefaultWidth,
                Height = sourceSettings != null && sourceSettings.Height > 0 ? Math.Round(sourceSettings.Height) : item.Manifest.DefaultHeight,
                Layer = sourceSettings?.Layer ?? WidgetLayer.Desktop,
                Opacity = sourceSettings?.Opacity ?? 1.0,
                Scale = sourceSettings?.Scale ?? 1.0,
                IsClickThrough = false,
                IsLocked = false,
                AutoHideAnchorDot = sourceSettings?.AutoHideAnchorDot ?? true,
                TargetMonitor = sourceSettings?.TargetMonitor ?? 0
            };

            // Copy instance data from source if it exists
            string sourceDataDir = Path.Combine(ChwPackageService.DataDir, sourceId);
            if (!Directory.Exists(sourceDataDir))
            {
                sourceDataDir = Path.Combine(ChwPackageService.DataDir, baseWidgetId);
            }

            string targetDataDir = Path.Combine(ChwPackageService.DataDir, newInstanceId);
            try
            {
                if (Directory.Exists(sourceDataDir))
                {
                    Directory.CreateDirectory(targetDataDir);
                    foreach (var file in Directory.GetFiles(sourceDataDir))
                    {
                        string fileName = Path.GetFileName(file);
                        File.Copy(file, Path.Combine(targetDataDir, fileName), true);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WidgetManager] Failed copying instance data: {ex.Message}");
            }

            Settings.Widgets[newInstanceId] = newSettings;
            SaveSettings();

            StartWidget(baseWidgetId, newInstanceId);
            return newInstanceId;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WidgetManager] Error duplicating widget: {ex}");
            return null;
        }
    }

    public void DeleteWidgetInstance(string instanceId)
    {
        if (_activeWindows.TryGetValue(instanceId, out var win))
        {
            win.Settings.IsEnabled = false;
            win.Close();
            _activeWindows.Remove(instanceId);
        }

        if (Settings.Widgets.Remove(instanceId))
        {
            SaveSettings();
        }

        try
        {
            string instanceDataDir = Path.Combine(ChwPackageService.DataDir, instanceId);
            if (Directory.Exists(instanceDataDir))
            {
                Directory.Delete(instanceDataDir, true);
            }
        }
        catch { }

        WidgetsChanged?.Invoke();
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
        if (_activeWindows.ContainsKey(window.InstanceId))
        {
            _activeWindows.Remove(window.InstanceId);
            SaveSettings();
            WidgetsChanged?.Invoke();
        }
    }

    private void OnWidgetSettingsChanged(WidgetWindow window)
    {
        Settings.Widgets[window.InstanceId] = window.Settings;
        SaveSettings();
    }

    public WidgetInstanceSettings GetOrCreateWidgetSettings(string instanceId, string? baseWidgetId = null)
    {
        if (Settings.Widgets.TryGetValue(instanceId, out var existing))
        {
            return existing;
        }

        string baseId = baseWidgetId ?? (instanceId.Contains('#') ? instanceId.Split('#')[0] : instanceId);
        var installed = ChwPackageService.GetInstalledWidgets();
        var item = installed.FirstOrDefault(x => x.Manifest.Id == baseId);

        double screenWidth = SystemParameters.PrimaryScreenWidth;
        double defaultW = item.Manifest?.DefaultWidth > 0 ? item.Manifest.DefaultWidth : 300;
        double defaultH = item.Manifest?.DefaultHeight > 0 ? item.Manifest.DefaultHeight : 200;
        double defaultLeft = Math.Max(50, screenWidth - defaultW - 40);
        int count = Math.Max(_activeWindows.Count, Settings.Widgets.Count);
        double defaultTop = 60 + (count * 70);

        var widgetSetting = new WidgetInstanceSettings
        {
            Id = instanceId,
            WidgetId = baseId,
            Title = item.Manifest?.Name ?? baseId,
            IsEnabled = true,
            Width = defaultW,
            Height = defaultH,
            Left = defaultLeft,
            Top = defaultTop,
            Layer = WidgetLayer.Desktop,
            IsClickThrough = false,
            AutoHideAnchorDot = true,
            Opacity = 1.0,
            Scale = 1.0
        };

        Settings.Widgets[instanceId] = widgetSetting;
        SaveSettings();
        return widgetSetting;
    }

    public void LoadSettings()
    {
        try
        {
            if (File.Exists(ChwPackageService.SettingsFile))
            {
                string json = File.ReadAllText(ChwPackageService.SettingsFile);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loaded != null)
                    {
                        Settings = loaded;
                        Log($"Settings loaded successfully. Widgets count: {Settings.Widgets.Count}");
                        return;
                    }
                }
            }

            // Fallback to backup if main file is missing or empty
            string bakFile = ChwPackageService.SettingsFile + ".bak";
            if (File.Exists(bakFile))
            {
                string bakJson = File.ReadAllText(bakFile);
                if (!string.IsNullOrWhiteSpace(bakJson))
                {
                    var loadedBak = JsonSerializer.Deserialize<AppSettings>(bakJson);
                    if (loadedBak != null)
                    {
                        Settings = loadedBak;
                        Log($"Settings recovered from backup. Widgets count: {Settings.Widgets.Count}");
                        SaveSettings();
                        return;
                    }
                }
            }

            Settings = new AppSettings();
        }
        catch (Exception ex)
        {
            Log($"LoadSettings error: {ex.Message}");
            try
            {
                string bakFile = ChwPackageService.SettingsFile + ".bak";
                if (File.Exists(bakFile))
                {
                    string bakJson = File.ReadAllText(bakFile);
                    var loadedBak = JsonSerializer.Deserialize<AppSettings>(bakJson);
                    if (loadedBak != null)
                    {
                        Settings = loadedBak;
                        Log($"Settings recovered from backup after parse error. Widgets count: {Settings.Widgets.Count}");
                        return;
                    }
                }
            }
            catch { }
            Settings = new AppSettings();
        }
    }

    public void SaveSettings()
    {
        if (!IsInitialized)
        {
            Log("SaveSettings skipped: WidgetManager is not initialized");
            return;
        }

        try
        {
            string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            string settingsFile = ChwPackageService.SettingsFile;
            string tempFile = settingsFile + ".tmp";
            string bakFile = settingsFile + ".bak";

            File.WriteAllText(tempFile, json);

            if (File.Exists(settingsFile))
            {
                try { File.Copy(settingsFile, bakFile, true); } catch { }
            }

            File.Move(tempFile, settingsFile, true);
        }
        catch (Exception ex)
        {
            Log($"SaveSettings error: {ex.Message}");
        }
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
