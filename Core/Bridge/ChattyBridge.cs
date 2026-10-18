using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using ChattyWidgets.Core.Services;
using ChattyWidgets.Core.Win32;

namespace ChattyWidgets.Core.Bridge;

[ClassInterface(ClassInterfaceType.AutoDual)]
[ComVisible(true)]
public class ChattyBridge
{
    private readonly string _widgetId;
    private readonly string _instanceId;
    private readonly Action? _closeAction;
    private readonly Action? _duplicateAction;
    private readonly Action? _deleteInstanceAction;
    private readonly Action<WidgetLayer>? _changeLayerAction;
    private readonly Action<bool>? _clickThroughAction;
    private readonly Action<bool>? _lockAction;
    private readonly Action<double>? _opacityAction;
    private readonly Func<double>? _getOpacityFunc;
    private readonly Action? _dragMoveAction;
    private readonly Action? _contextMenuAction;
    private readonly Action<double, double>? _moveByAction;
    private readonly Action? _snapAction;
    private readonly Action<string, string>? _addMenuItemAction;
    private readonly Action<double, double>? _resizeAction;
    private readonly Func<bool>? _isLockedFunc;
    private readonly Func<bool>? _isClickThroughFunc;
    private readonly Action<bool>? _autoHideDotAction;
    private readonly Func<bool>? _isAutoHideDotFunc;
    private readonly Action? _openDevToolsAction;
    private readonly Action? _reloadAction;
    private readonly Action<bool>? _hoverAction;
    private readonly string _storageFilePath;
    private readonly string _dataStorageFilePath;

    public ChattyBridge(
        string widgetId,
        string? instanceId = null,
        Action? closeAction = null,
        Action? duplicateAction = null,
        Action? deleteInstanceAction = null,
        Action<WidgetLayer>? changeLayerAction = null,
        Action<bool>? clickThroughAction = null,
        Action<bool>? lockAction = null,
        Action<double>? opacityAction = null,
        Func<double>? getOpacityFunc = null,
        Action? dragMoveAction = null,
        Action? contextMenuAction = null,
        Action<double, double>? moveByAction = null,
        Action? snapAction = null,
        Action<string, string>? addMenuItemAction = null,
        Action<double, double>? resizeAction = null,
        Func<bool>? isLockedFunc = null,
        Func<bool>? isClickThroughFunc = null,
        Action<bool>? autoHideDotAction = null,
        Func<bool>? isAutoHideDotFunc = null,
        Action? openDevToolsAction = null,
        Action? reloadAction = null,
        Action<bool>? hoverAction = null)
    {
        _widgetId = widgetId;
        _instanceId = !string.IsNullOrEmpty(instanceId) ? instanceId : widgetId;
        _closeAction = closeAction;
        _duplicateAction = duplicateAction;
        _deleteInstanceAction = deleteInstanceAction;
        _changeLayerAction = changeLayerAction;
        _clickThroughAction = clickThroughAction;
        _lockAction = lockAction;
        _opacityAction = opacityAction;
        _getOpacityFunc = getOpacityFunc;
        _dragMoveAction = dragMoveAction;
        _contextMenuAction = contextMenuAction;
        _moveByAction = moveByAction;
        _snapAction = snapAction;
        _addMenuItemAction = addMenuItemAction;
        _resizeAction = resizeAction;
        _isLockedFunc = isLockedFunc;
        _isClickThroughFunc = isClickThroughFunc;
        _autoHideDotAction = autoHideDotAction;
        _isAutoHideDotFunc = isAutoHideDotFunc;
        _openDevToolsAction = openDevToolsAction;
        _reloadAction = reloadAction;
        _hoverAction = hoverAction;

        string widgetDir = Path.Combine(ChwPackageService.WidgetsDir, widgetId);
        Directory.CreateDirectory(widgetDir);
        _storageFilePath = Path.Combine(widgetDir, "storage.json");

        string dataDir = Path.Combine(ChwPackageService.DataDir, _instanceId);
        Directory.CreateDirectory(dataDir);
        _dataStorageFilePath = Path.Combine(dataDir, "storage.json");

        // Seed instance storage from existing storage if needed
        if (!File.Exists(_dataStorageFilePath))
        {
            string baseDataStorage = Path.Combine(ChwPackageService.DataDir, widgetId, "storage.json");
            if (File.Exists(baseDataStorage))
            {
                try { File.Copy(baseDataStorage, _dataStorageFilePath, true); } catch { }
            }
            else if (File.Exists(_storageFilePath))
            {
                try { File.Copy(_storageFilePath, _dataStorageFilePath, true); } catch { }
            }
        }
    }

    // --- SYSTEM API ---

    public double getCpu() => SystemMonitorService.Instance.CurrentMetrics.CpuPercent;
    public double getMemoryPercent() => SystemMonitorService.Instance.CurrentMetrics.MemoryPercent;
    public double getMemoryUsed() => SystemMonitorService.Instance.CurrentMetrics.UsedMemoryGb;
    public double getMemoryTotal() => SystemMonitorService.Instance.CurrentMetrics.TotalMemoryGb;
    public double getDiskPercent() => SystemMonitorService.Instance.CurrentMetrics.DiskPercent;
    public double getDiskFree() => SystemMonitorService.Instance.CurrentMetrics.FreeDiskGb;
    public double getNetDownload() => SystemMonitorService.Instance.CurrentMetrics.NetworkDownloadKbps;
    public double getNetUpload() => SystemMonitorService.Instance.CurrentMetrics.NetworkUploadKbps;

    public string getSystemMetricsJson()
    {
        return JsonSerializer.Serialize(SystemMonitorService.Instance.CurrentMetrics);
    }

    // --- MEDIA API ---

    public void playPause() => MediaMonitorService.Instance.PlayPause();
    public void nextTrack() => MediaMonitorService.Instance.Next();
    public void prevTrack() => MediaMonitorService.Instance.Previous();
    public void volumeUp() => MediaMonitorService.Instance.VolumeUp();
    public void volumeDown() => MediaMonitorService.Instance.VolumeDown();
    public void mute() => MediaMonitorService.Instance.Mute();

    public string getMediaJson()
    {
        return JsonSerializer.Serialize(MediaMonitorService.Instance.CurrentMedia);
    }

    // --- STORAGE API ---

    public string storageGet(string key, string defaultValue)
    {
        try
        {
            string path = File.Exists(_storageFilePath) ? _storageFilePath : _dataStorageFilePath;
            if (!File.Exists(path))
                return defaultValue;

            string json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict != null && dict.TryGetValue(key, out var val))
            {
                return val;
            }
        }
        catch { }
        return defaultValue;
    }

    public void storageSet(string key, string value)
    {
        try
        {
            Dictionary<string, string> dict;
            string readPath = File.Exists(_dataStorageFilePath) ? _dataStorageFilePath : _storageFilePath;
            if (File.Exists(readPath))
            {
                string json = File.ReadAllText(readPath);
                dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
            else
            {
                dict = new();
            }

            dict[key] = value;
            string output = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_dataStorageFilePath, output);
            if (_instanceId == _widgetId)
            {
                try { File.WriteAllText(_storageFilePath, output); } catch { }
            }
        }
        catch { }
    }

    public void storageRemove(string key)
    {
        try
        {
            string readPath = File.Exists(_dataStorageFilePath) ? _dataStorageFilePath : _storageFilePath;
            if (File.Exists(readPath))
            {
                string json = File.ReadAllText(readPath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (dict != null && dict.Remove(key))
                {
                    string output = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_dataStorageFilePath, output);
                    if (_instanceId == _widgetId)
                    {
                        try { File.WriteAllText(_storageFilePath, output); } catch { }
                    }
                }
            }
        }
        catch { }
    }

    // --- MEDIA FILE API ---

    public string selectMediaFile()
    {
        string selected = string.Empty;
        Application.Current.Dispatcher.Invoke(() =>
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Выберите изображение или видео для виджета",
                    Filter = "Медиафайлы (*.jpg;*.png;*.gif;*.webp;*.mp4;*.webm)|*.jpg;*.jpeg;*.png;*.gif;*.webp;*.mp4;*.webm|Изображения (*.jpg;*.png;*.gif;*.webp)|*.jpg;*.jpeg;*.png;*.gif;*.webp|Видео (*.mp4;*.webm)|*.mp4;*.webm|Все файлы (*.*)|*.*",
                    Multiselect = false
                };

                if (dlg.ShowDialog() == true)
                {
                    string ext = Path.GetExtension(dlg.FileName);
                    string targetFileName = "user_media" + ext;
                    string dataDir = Path.Combine(ChwPackageService.DataDir, _instanceId);
                    Directory.CreateDirectory(dataDir);
                    string targetDataPath = Path.Combine(dataDir, targetFileName);
                    File.Copy(dlg.FileName, targetDataPath, overwrite: true);

                    string widgetDir = Path.Combine(ChwPackageService.WidgetsDir, _widgetId);
                    Directory.CreateDirectory(widgetDir);
                    string targetWidgetPath = Path.Combine(widgetDir, targetFileName);
                    try { File.Copy(dlg.FileName, targetWidgetPath, overwrite: true); } catch { }

                    selected = _instanceId == _widgetId ? targetFileName : new Uri(targetDataPath).AbsoluteUri;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ChattyBridge] selectMediaFile error: {ex.Message}");
            }
        });
        return selected;
    }

    public string copyDroppedFile(string sourceFilePath)
    {
        string result = string.Empty;
        try
        {
            if (File.Exists(sourceFilePath))
            {
                string ext = Path.GetExtension(sourceFilePath);
                string targetFileName = "user_media" + ext;
                string dataDir = Path.Combine(ChwPackageService.DataDir, _instanceId);
                Directory.CreateDirectory(dataDir);
                string targetDataPath = Path.Combine(dataDir, targetFileName);
                File.Copy(sourceFilePath, targetDataPath, overwrite: true);

                string widgetDir = Path.Combine(ChwPackageService.WidgetsDir, _widgetId);
                Directory.CreateDirectory(widgetDir);
                string targetWidgetPath = Path.Combine(widgetDir, targetFileName);
                try { File.Copy(sourceFilePath, targetWidgetPath, overwrite: true); } catch { }

                result = _instanceId == _widgetId ? targetFileName : new Uri(targetDataPath).AbsoluteUri;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChattyBridge] copyDroppedFile error: {ex.Message}");
        }
        return result;
    }

    // --- WINDOW & INSTANCE API ---

    public string getInstanceId() => _instanceId;
    public string getWidgetId() => _widgetId;

    public void duplicate()
    {
        Application.Current?.Dispatcher?.BeginInvoke(new Action(() => _duplicateAction?.Invoke()));
    }

    public void deleteWidget()
    {
        Application.Current?.Dispatcher?.BeginInvoke(new Action(() => _deleteInstanceAction?.Invoke()));
    }

    public void closeWidget()
    {
        Application.Current.Dispatcher.Invoke(() => _closeAction?.Invoke());
    }

    public void setLayer(string layer)
    {
        WidgetLayer l = layer.ToLowerInvariant() switch
        {
            "top" or "alwaysontop" => WidgetLayer.Top,
            "normal" or "floating" => WidgetLayer.Normal,
            _ => WidgetLayer.Desktop
        };
        Application.Current.Dispatcher.Invoke(() => _changeLayerAction?.Invoke(l));
    }

    public void setClickThrough(bool enabled)
    {
        Application.Current.Dispatcher.Invoke(() => _clickThroughAction?.Invoke(enabled));
    }

    public void setOpacity(double opacity)
    {
        Application.Current.Dispatcher.Invoke(() => _opacityAction?.Invoke(opacity));
    }

    public double getOpacity() => _getOpacityFunc?.Invoke() ?? 1.0;

    public void dragMove()
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(() => _dragMoveAction?.Invoke()));
    }

    public void moveBy(double deltaX, double deltaY)
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(() => _moveByAction?.Invoke(deltaX, deltaY)));
    }

    public void snapToScreen()
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(() => _snapAction?.Invoke()));
    }

    public void showContextMenu()
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(() => _contextMenuAction?.Invoke()));
    }

    public void notifyHover(bool hovered)
    {
        Application.Current?.Dispatcher?.BeginInvoke(new Action(() => _hoverAction?.Invoke(hovered)));
    }

    public void setLocked(bool locked)
    {
        Application.Current.Dispatcher.Invoke(() => _lockAction?.Invoke(locked));
    }

    public bool isLocked() => _isLockedFunc?.Invoke() ?? false;

    public bool isClickThrough() => _isClickThroughFunc?.Invoke() ?? false;

    public void setAutoHideDot(bool autoHide)
    {
        Application.Current.Dispatcher.Invoke(() => _autoHideDotAction?.Invoke(autoHide));
    }

    public bool isAutoHideDot() => _isAutoHideDotFunc?.Invoke() ?? false;

    public void addMenuItem(string id, string title)
    {
        Application.Current.Dispatcher.Invoke(() => _addMenuItemAction?.Invoke(id, title));
    }

    public void resize(double width, double height)
    {
        Application.Current.Dispatcher.Invoke(() => _resizeAction?.Invoke(width, height));
    }

    public string getSetting(string key)
    {
        var win = WidgetManager.Instance?.GetWindow(_instanceId);
        if (win != null && win.Settings.CustomSettings.TryGetValue(key, out var val) && val != null)
        {
            return val;
        }
        return string.Empty;
    }

    public void setSetting(string key, string value)
    {
        var win = WidgetManager.Instance?.GetWindow(_instanceId);
        if (win != null)
        {
            win.Settings.CustomSettings[key] = value;
            WidgetManager.Instance?.SaveSettings();
        }
    }

    // --- EXTENDED APIS ---

    public void notify(string title, string message)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            ChattyWidgets.UI.TrayIconManager.CurrentInstance?.ShowNotification(title, message);
        });
    }

    public void clipboardWrite(string text)
    {
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            try { System.Windows.Clipboard.SetText(text); } catch { }
        });
    }

    public string clipboardRead()
    {
        string res = string.Empty;
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            try { res = System.Windows.Clipboard.GetText(); } catch { }
        });
        return res;
    }

    public double getBatteryLevel()
    {
        try
        {
            var p = System.Windows.Forms.SystemInformation.PowerStatus;
            return Math.Round((double)p.BatteryLifePercent * 100.0, 1);
        }
        catch { return 100; }
    }

    public bool isBatteryCharging()
    {
        try
        {
            var p = System.Windows.Forms.SystemInformation.PowerStatus;
            return p.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online;
        }
        catch { return true; }
    }

    public string getWindowsAccentColor()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key != null)
            {
                var val = key.GetValue("ColorizationColor");
                if (val is int intVal)
                {
                    byte r = (byte)((intVal >> 16) & 0xFF);
                    byte g = (byte)((intVal >> 8) & 0xFF);
                    byte b = (byte)(intVal & 0xFF);
                    return $"#{r:X2}{g:X2}{b:X2}";
                }
            }
        }
        catch { }
        return "#6366F1";
    }

    public void launchApp(string target)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChattyBridge] launchApp failed: {ex.Message}");
        }
    }

    public void openDevTools()
    {
        Application.Current?.Dispatcher?.BeginInvoke(new Action(() => _openDevToolsAction?.Invoke()));
    }

    public void reload()
    {
        Application.Current?.Dispatcher?.BeginInvoke(new Action(() => _reloadAction?.Invoke()));
    }

    public double getGpuPercent() => SystemMonitorService.Instance.CurrentMetrics.GpuPercent;
    public double getGpuMemoryPercent() => SystemMonitorService.Instance.CurrentMetrics.GpuMemoryPercent;
    public double getGpuMemoryTotal() => SystemMonitorService.Instance.CurrentMetrics.TotalGpuMemoryGb;
    public double getGpuMemoryUsed() => SystemMonitorService.Instance.CurrentMetrics.UsedGpuMemoryGb;
    public double getGpuTemperature() => SystemMonitorService.Instance.CurrentMetrics.GpuTemperature;
    public string getGpuName() => SystemMonitorService.Instance.CurrentMetrics.GpuName;

    public void log(string message)
    {
        App.Log($"[Bridge:{_widgetId}] {message}");
        System.Diagnostics.Debug.WriteLine($"[Widget:{_widgetId}] {message}");
    }
}
