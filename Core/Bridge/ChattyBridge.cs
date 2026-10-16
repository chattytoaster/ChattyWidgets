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
    private readonly Action<double>? _opacityAction;
    private readonly Action? _dragMoveAction;
    private readonly Action? _contextMenuAction;
    private readonly Action<double, double>? _moveByAction;
    private readonly Action? _snapAction;
    private readonly string _storageFilePath;

    public ChattyBridge(
        string widgetId,
        Action? closeAction = null,
        Action<WidgetLayer>? changeLayerAction = null,
        Action<bool>? clickThroughAction = null,
        Action<double>? opacityAction = null,
        Action? dragMoveAction = null,
        Action? contextMenuAction = null,
        Action<double, double>? moveByAction = null,
        Action? snapAction = null)
    {
        _widgetId = widgetId;
        _closeAction = closeAction;
        _changeLayerAction = changeLayerAction;
        _clickThroughAction = clickThroughAction;
        _opacityAction = opacityAction;
        _dragMoveAction = dragMoveAction;
        _contextMenuAction = contextMenuAction;
        _moveByAction = moveByAction;
        _snapAction = snapAction;

        string widgetDir = Path.Combine(ChwPackageService.WidgetsDir, widgetId);
        Directory.CreateDirectory(widgetDir);
        _storageFilePath = Path.Combine(widgetDir, "storage.json");
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
            if (!File.Exists(_storageFilePath))
                return defaultValue;

            string json = File.ReadAllText(_storageFilePath);
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
            if (File.Exists(_storageFilePath))
            {
                string json = File.ReadAllText(_storageFilePath);
                dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
            else
            {
                dict = new();
            }

            dict[key] = value;
            File.WriteAllText(_storageFilePath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public void storageRemove(string key)
    {
        try
        {
            if (File.Exists(_storageFilePath))
            {
                string json = File.ReadAllText(_storageFilePath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (dict != null && dict.Remove(key))
                {
                    File.WriteAllText(_storageFilePath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
        }
        catch { }
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

    public void log(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[Widget:{_widgetId}] {message}");
    }
}
