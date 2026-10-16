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
    private readonly Action? _closeAction;
    private readonly Action<WidgetLayer>? _changeLayerAction;
    private readonly Action<bool>? _clickThroughAction;
    private readonly Action<bool>? _lockAction;
    private readonly Action<double>? _opacityAction;
    private readonly Action? _dragMoveAction;
    private readonly Action? _contextMenuAction;
    private readonly Action<double, double>? _moveByAction;
    private readonly Action? _snapAction;
    private readonly Action<string, string>? _addMenuItemAction;
    private readonly Action<double, double>? _resizeAction;
    private readonly Func<bool>? _isLockedFunc;
    private readonly Func<bool>? _isClickThroughFunc;
    private readonly string _storageFilePath;
    private readonly string _dataStorageFilePath;

    public ChattyBridge(
        string widgetId,
        Action? closeAction = null,
        Action<WidgetLayer>? changeLayerAction = null,
        Action<bool>? clickThroughAction = null,
        Action<bool>? lockAction = null,
        Action<double>? opacityAction = null,
        Action? dragMoveAction = null,
        Action? contextMenuAction = null,
        Action<double, double>? moveByAction = null,
        Action? snapAction = null,
        Action<string, string>? addMenuItemAction = null,
        Action<double, double>? resizeAction = null,
        Func<bool>? isLockedFunc = null,
        Func<bool>? isClickThroughFunc = null)
    {
        _widgetId = widgetId;
        _closeAction = closeAction;
        _changeLayerAction = changeLayerAction;
        _clickThroughAction = clickThroughAction;
        _lockAction = lockAction;
        _opacityAction = opacityAction;
        _dragMoveAction = dragMoveAction;
        _contextMenuAction = contextMenuAction;
        _moveByAction = moveByAction;
        _snapAction = snapAction;
        _addMenuItemAction = addMenuItemAction;
        _resizeAction = resizeAction;
        _isLockedFunc = isLockedFunc;
        _isClickThroughFunc = isClickThroughFunc;

        string widgetDir = Path.Combine(ChwPackageService.WidgetsDir, widgetId);
        Directory.CreateDirectory(widgetDir);
        _storageFilePath = Path.Combine(widgetDir, "storage.json");

        string dataDir = Path.Combine(ChwPackageService.DataDir, widgetId);
        Directory.CreateDirectory(dataDir);
        _dataStorageFilePath = Path.Combine(dataDir, "storage.json");

        // Migrate if needed
        if (!File.Exists(_storageFilePath) && File.Exists(_dataStorageFilePath))
        {
            try { File.Copy(_dataStorageFilePath, _storageFilePath, true); } catch { }
        }
        else if (File.Exists(_storageFilePath) && !File.Exists(_dataStorageFilePath))
        {
            try { File.Copy(_storageFilePath, _dataStorageFilePath, true); } catch { }
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
            string readPath = File.Exists(_storageFilePath) ? _storageFilePath : _dataStorageFilePath;
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
            File.WriteAllText(_storageFilePath, output);
            File.WriteAllText(_dataStorageFilePath, output);
        }
        catch { }
    }

    public void storageRemove(string key)
    {
        try
        {
            string readPath = File.Exists(_storageFilePath) ? _storageFilePath : _dataStorageFilePath;
            if (File.Exists(readPath))
            {
                string json = File.ReadAllText(readPath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (dict != null && dict.Remove(key))
                {
                    string output = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_storageFilePath, output);
                    File.WriteAllText(_dataStorageFilePath, output);
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
                    string widgetDir = Path.Combine(ChwPackageService.WidgetsDir, _widgetId);
                    Directory.CreateDirectory(widgetDir);
                    string targetPath = Path.Combine(widgetDir, targetFileName);

                    File.Copy(dlg.FileName, targetPath, overwrite: true);
                    selected = targetFileName;
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
                string widgetDir = Path.Combine(ChwPackageService.WidgetsDir, _widgetId);
                Directory.CreateDirectory(widgetDir);
                string targetPath = Path.Combine(widgetDir, targetFileName);

                File.Copy(sourceFilePath, targetPath, overwrite: true);
                result = targetFileName;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChattyBridge] copyDroppedFile error: {ex.Message}");
        }
        return result;
    }

    // --- WINDOW API ---

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

    public void setLocked(bool locked)
    {
        Application.Current.Dispatcher.Invoke(() => _lockAction?.Invoke(locked));
    }

    public bool isLocked() => _isLockedFunc?.Invoke() ?? false;

    public bool isClickThrough() => _isClickThroughFunc?.Invoke() ?? false;

    public void addMenuItem(string id, string title)
    {
        Application.Current.Dispatcher.Invoke(() => _addMenuItemAction?.Invoke(id, title));
    }

    public void resize(double width, double height)
    {
        Application.Current.Dispatcher.Invoke(() => _resizeAction?.Invoke(width, height));
    }

    public void log(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[Widget:{_widgetId}] {message}");
    }
}
