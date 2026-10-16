using System.IO;
using System.Text.Json;
using ChattyWidgets.Core.Models;

namespace ChattyWidgets.Core.Services;

public class WorkshopService
{
    private static WorkshopService? _instance;
    public static WorkshopService Instance => _instance ??= new WorkshopService();

    private FileSystemWatcher? _watcher;
    private System.Timers.Timer? _debounceTimer;

    private WorkshopService() { }

    /// <summary>
    /// Scaffolds a new widget project from template.
    /// </summary>
    public string CreateWidgetProject(
        string targetDirectory,
        string id,
        string name,
        string author,
        string description,
        string type,
        double defaultWidth = 300,
        double defaultHeight = 200)
    {
        Directory.CreateDirectory(targetDirectory);

        string entryFile = type == "native" ? "Widget.xaml" : "index.html";

        var manifest = new WidgetManifest
        {
            Id = id,
            Name = name,
            Version = "1.0.0",
            Author = string.IsNullOrWhiteSpace(author) ? "Developer" : author,
            Description = description,
            Type = type,
            Entry = entryFile,
            DefaultWidth = defaultWidth,
            DefaultHeight = defaultHeight,
            MinWidth = 160,
            MinHeight = 120,
            Resizable = true,
            DefaultLayer = "desktop",
            ClickThrough = false,
            Permissions = new List<string> { "system", "storage" }
        };

        // Write manifest.json
        string manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(targetDirectory, "manifest.json"), manifestJson);

        // Write entry template
        if (type == "native")
        {
            string xamlTemplate = @"<Border xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        Background=""#E0161A26""
        BorderBrush=""#40FFFFFF""
        BorderThickness=""1""
        CornerRadius=""16""
        Padding=""20"">
    <StackPanel VerticalAlignment=""Center"" HorizontalAlignment=""Center"">
        <TextBlock Text=""Привет, " + name + @"!"" FontSize=""18"" FontWeight=""Bold"" Foreground=""#FFFFFF"" HorizontalAlignment=""Center""/>
        <TextBlock Text=""Нативный XAML виджет готов к кастомизации."" FontSize=""12"" Foreground=""#94A3B8"" Margin=""0,8,0,0"" HorizontalAlignment=""Center""/>
        <TextBlock Text=""0% нагрузки CPU"" FontSize=""11"" FontWeight=""SemiBold"" Foreground=""#10B981"" Margin=""0,12,0,0"" HorizontalAlignment=""Center""/>
    </StackPanel>
</Border>";
            File.WriteAllText(Path.Combine(targetDirectory, entryFile), xamlTemplate);
        }
        else
        {
            string htmlTemplate = @"<!DOCTYPE html>
<html lang=""ru"">
<head>
  <meta charset=""UTF-8"">
  <title>" + name + @"</title>
  <style>
    * { box-sizing: border-box; margin: 0; padding: 0; user-select: none; }
    body {
      font-family: 'Segoe UI', system-ui, sans-serif;
      background: transparent;
      height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 10px;
    }
    .card {
      width: 100%;
      height: 100%;
      background: rgba(18, 22, 34, 0.8);
      backdrop-filter: blur(20px);
      border: 1px solid rgba(255, 255, 255, 0.15);
      border-radius: 20px;
      padding: 20px;
      color: #F8FAFC;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
    }
    h2 { font-size: 16px; color: #818CF8; }
    p { font-size: 12px; color: #94A3B8; margin-top: 4px; }
    .badge {
      display: inline-block;
      background: rgba(99, 102, 241, 0.2);
      color: #818CF8;
      padding: 4px 10px;
      border-radius: 12px;
      font-size: 11px;
      font-weight: 600;
    }
  </style>
</head>
<body>
  <div class=""card"">
    <div>
      <h2>" + name + @"</h2>
      <p>" + description + @"</p>
    </div>
    <div>
      <span class=""badge"" id=""cpuInfo"">CPU: ...</span>
    </div>
  </div>

  <script>
    function update() {
      if (window.chatty) {
        try {
          const cpu = Math.round(window.chatty.getCpu());
          document.getElementById('cpuInfo').textContent = 'CPU: ' + cpu + '%';
        } catch(e) {}
      }
    }
    update();
    setInterval(update, 1500);
  </script>
</body>
</html>";
            File.WriteAllText(Path.Combine(targetDirectory, entryFile), htmlTemplate);
        }

        return targetDirectory;
    }

    /// <summary>
    /// Starts monitoring a development folder for Live Reload.
    /// </summary>
    public void StartLiveReload(string folderPath, Action onChanged)
    {
        StopLiveReload();

        if (!Directory.Exists(folderPath))
            return;

        _debounceTimer = new System.Timers.Timer(400) { AutoReset = false };
        _debounceTimer.Elapsed += (s, e) => onChanged();

        _watcher = new FileSystemWatcher(folderPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Changed += (s, e) => ResetDebounce();
        _watcher.Created += (s, e) => ResetDebounce();
        _watcher.Deleted += (s, e) => ResetDebounce();
    }

    private void ResetDebounce()
    {
        if (_debounceTimer != null)
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }
    }

    public void StopLiveReload()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        if (_debounceTimer != null)
        {
            _debounceTimer.Dispose();
            _debounceTimer = null;
        }
    }
}
