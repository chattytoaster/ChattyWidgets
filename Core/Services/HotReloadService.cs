using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace ChattyWidgets.Core.Services;

public class HotReloadService : IDisposable
{
    private static HotReloadService? _instance;
    public static HotReloadService Instance => _instance ??= new HotReloadService();

    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly Dictionary<string, DispatcherTimer> _debounceTimers = new();

    private HotReloadService()
    {
    }

    public void Start()
    {
        Stop();
        if (!WidgetManager.Instance.Settings.EnableHotReload) return;

        string widgetsDir = ChwPackageService.WidgetsDir;
        if (Directory.Exists(widgetsDir))
        {
            WatchDirectory(widgetsDir);
        }

        string workshopDir = WorkshopService.WorkshopDir;
        if (Directory.Exists(workshopDir))
        {
            WatchDirectory(workshopDir);
        }
    }

    public void Stop()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();

        foreach (var timer in _debounceTimers.Values)
        {
            timer.Stop();
        }
        _debounceTimers.Clear();
    }

    private void WatchDirectory(string path)
    {
        try
        {
            var watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                Filter = "*.*"
            };

            watcher.Changed += OnFileChanged;
            watcher.Created += OnFileChanged;
            watcher.EnableRaisingEvents = true;

            _watchers.Add(watcher);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HotReload] Failed to watch {path}: {ex.Message}");
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        string ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
        if (ext is not (".html" or ".css" or ".js" or ".json" or ".xaml"))
            return;

        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            // Identify which widget directory was touched
            string? widgetDir = FindParentWidgetDir(e.FullPath);
            if (string.IsNullOrEmpty(widgetDir)) return;

            string dirName = Path.GetFileName(widgetDir);

            if (_debounceTimers.TryGetValue(dirName, out var existingTimer))
            {
                existingTimer.Stop();
                existingTimer.Start();
                return;
            }

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                _debounceTimers.Remove(dirName);
                ReloadWidgetByDir(dirName);
            };
            _debounceTimers[dirName] = timer;
            timer.Start();
        });
    }

    private static string? FindParentWidgetDir(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "widget.json")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static void ReloadWidgetByDir(string widgetDirName)
    {
        foreach (var win in WidgetManager.Instance.GetActiveWindows())
        {
            if (win.Manifest.Id.Equals(widgetDirName, StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(win.WidgetDirectory).Equals(widgetDirName, StringComparison.OrdinalIgnoreCase))
            {
                win.ReloadWidget();
                System.Diagnostics.Debug.WriteLine($"[HotReload] Reloaded widget: {win.Manifest.Name}");
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
