#nullable enable
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChattyWidgets.Core.Services;

public class GitHubAssetInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

public class GitHubReleaseInfo
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAssetInfo>? Assets { get; set; }

    [JsonIgnore]
    public bool IsNewer { get; set; }

    [JsonIgnore]
    public string DisplayVersion => TagName.TrimStart('v', 'V');

    [JsonIgnore]
    public string? ExeDownloadUrl => Assets?.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))?.BrowserDownloadUrl;

    [JsonIgnore]
    public string? UpdateZipDownloadUrl => Assets?.FirstOrDefault(a => a.Name.EndsWith("-Update.zip", StringComparison.OrdinalIgnoreCase) || a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))?.BrowserDownloadUrl;

    [JsonIgnore]
    public string? OtaDownloadUrl => UpdateZipDownloadUrl ?? ExeDownloadUrl;
}

public class UpdateService
{
    private static readonly Lazy<UpdateService> _instance = new(() => new UpdateService());
    public static UpdateService Instance => _instance.Value;

    private readonly HttpClient _httpClient;
    private const string GitHubApiUrl = "https://api.github.com/repos/chattytoaster/ChattyWidgets/releases/latest";
    private const string GitHubReleasesPage = "https://github.com/chattytoaster/ChattyWidgets/releases";

    public event Action<GitHubReleaseInfo>? UpdateAvailable;
    public GitHubReleaseInfo? LatestRelease { get; private set; }

    public bool IsDownloadingUpdate { get; private set; }
    public bool IsUpdateReadyToApply { get; private set; }
    public string? StagedUpdatePath { get; private set; }
    public bool IsPayloadZip { get; private set; }

    public event Action<double, string>? OtaProgressChanged;
    public event Action? OtaUpdateReady;
    public event Action<string>? OtaUpdateFailed;

    public string CurrentVersion
    {
        get
        {
            try
            {
                var ver = Assembly.GetExecutingAssembly().GetName().Version;
                return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.0.0";
            }
            catch
            {
                return "1.0.0";
            }
        }
    }

    public UpdateService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ChattyWidgets-App");
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<GitHubReleaseInfo?> CheckForUpdatesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(GitHubApiUrl);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string json = await response.Content.ReadAsStringAsync();
            var release = JsonSerializer.Deserialize<GitHubReleaseInfo>(json);

            if (release != null && !string.IsNullOrWhiteSpace(release.TagName))
            {
                release.IsNewer = IsVersionNewer(release.DisplayVersion, CurrentVersion);
                LatestRelease = release;

                if (release.IsNewer)
                {
                    UpdateAvailable?.Invoke(release);
                }

                return release;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateService] Error checking for updates: {ex.Message}");
        }

        return null;
    }

    public static bool IsVersionNewer(string remoteVerStr, string currentVerStr)
    {
        if (Version.TryParse(remoteVerStr, out var remoteVer) && Version.TryParse(currentVerStr, out var currentVer))
        {
            return remoteVer > currentVer;
        }

        // Fallback simple semver comparison
        var rParts = remoteVerStr.Split('.');
        var cParts = currentVerStr.Split('.');
        int length = Math.Max(rParts.Length, cParts.Length);

        for (int i = 0; i < length; i++)
        {
            int r = i < rParts.Length && int.TryParse(rParts[i], out var rp) ? rp : 0;
            int c = i < cParts.Length && int.TryParse(cParts[i], out var cp) ? cp : 0;
            if (r > c) return true;
            if (r < c) return false;
        }

        return false;
    }

    public void OpenReleasePage(string? url = null)
    {
        try
        {
            string targetUrl = !string.IsNullOrWhiteSpace(url) ? url : (LatestRelease?.HtmlUrl ?? GitHubReleasesPage);
            Process.Start(new ProcessStartInfo(targetUrl) { UseShellExecute = true });
        }
        catch { }
    }

    public void DownloadLatestInstaller()
    {
        try
        {
            string targetUrl = LatestRelease?.ExeDownloadUrl ?? LatestRelease?.HtmlUrl ?? GitHubReleasesPage;
            Process.Start(new ProcessStartInfo(targetUrl) { UseShellExecute = true });
        }
        catch { }
    }

    public async Task<bool> DownloadAndStageUpdateAsync(GitHubReleaseInfo release)
    {
        if (IsDownloadingUpdate) return false;
        IsDownloadingUpdate = true;
        IsUpdateReadyToApply = false;

        string updateDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChattyWidgets", "update");
        string stagedDir = Path.Combine(updateDir, "staged");

        try
        {
            Directory.CreateDirectory(updateDir);

            // Determine what to download: prefer update zip, or fallback to setup exe
            string? downloadUrl = release.OtaDownloadUrl;
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                OtaUpdateFailed?.Invoke("Не найдена ссылка для скачивания обновления.");
                IsDownloadingUpdate = false;
                return false;
            }

            bool isZip = downloadUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
            IsPayloadZip = isZip;
            string fileName = isZip ? "update.zip" : "ChattyWidgetsSetup.exe";
            string downloadPath = Path.Combine(updateDir, fileName);

            OtaProgressChanged?.Invoke(5, "Подключение к GitHub...");

            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? 32 * 1024 * 1024;
            using var sourceStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalRead += bytesRead;

                double pct = Math.Min(95, 5 + ((double)totalRead / totalBytes) * 90);
                string mbStr = $"{(totalRead / (1024.0 * 1024.0)):F1} MB / {(totalBytes / (1024.0 * 1024.0)):F1} MB";
                OtaProgressChanged?.Invoke(pct, $"Загрузка обновления: {mbStr} ({(int)pct}%)");
            }

            fileStream.Flush();
            fileStream.Close();

            if (isZip)
            {
                OtaProgressChanged?.Invoke(96, "Распаковка обновления...");
                if (Directory.Exists(stagedDir))
                {
                    try { Directory.Delete(stagedDir, true); } catch { }
                }
                Directory.CreateDirectory(stagedDir);
                System.IO.Compression.ZipFile.ExtractToDirectory(downloadPath, stagedDir, true);
                StagedUpdatePath = stagedDir;
            }
            else
            {
                StagedUpdatePath = downloadPath;
            }

            IsUpdateReadyToApply = true;
            IsDownloadingUpdate = false;
            OtaProgressChanged?.Invoke(100, "Обновление готово к установке!");
            OtaUpdateReady?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            IsDownloadingUpdate = false;
            IsUpdateReadyToApply = false;
            Debug.WriteLine($"[UpdateService] Error downloading update: {ex.Message}");
            OtaUpdateFailed?.Invoke($"Ошибка скачивания: {ex.Message}");
            return false;
        }
    }

    public void RestartAndApplyUpdate()
    {
        if (!IsUpdateReadyToApply || string.IsNullOrEmpty(StagedUpdatePath))
            return;

        try
        {
            // 1. Capture exact coordinates of all active windows and save
            foreach (var win in WidgetManager.Instance.GetActiveWindows())
            {
                try
                {
                    if (win.WindowState == System.Windows.WindowState.Normal)
                    {
                        win.Settings.Left = win.Left;
                        win.Settings.Top = win.Top;
                        win.Settings.Width = win.Width;
                        win.Settings.Height = win.Height;
                    }
                }
                catch { }
            }
            WidgetManager.Instance.SaveSettings();

            int currentPid = Environment.ProcessId;
            string targetDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            string targetExe = Path.Combine(targetDir, "ChattyWidgets.exe");
            string updateDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChattyWidgets", "update");
            string runnerScript = Path.Combine(updateDir, "apply_update.cmd");

            string scriptContent;
            if (IsPayloadZip)
            {
                scriptContent = $@"@echo off
chcp 65001 >nul
set OLD_PID={currentPid}
set TARGET_DIR={targetDir}
set STAGED_DIR={StagedUpdatePath}
set EXE_PATH={targetExe}

:wait_loop
tasklist /fi ""pid eq %OLD_PID%"" | find ""%OLD_PID%"" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto wait_loop
)

taskkill /f /im ChattyWidgets.exe >nul 2>&1
timeout /t 1 /nobreak >nul

robocopy ""%STAGED_DIR%"" ""%TARGET_DIR%"" /E /IS /IT /NP /R:3 /W:1 >nul

start """" ""%EXE_PATH%""
";
            }
            else
            {
                scriptContent = $@"@echo off
chcp 65001 >nul
set OLD_PID={currentPid}
set SETUP_EXE={StagedUpdatePath}
set TARGET_DIR={targetDir}
set EXE_PATH={targetExe}

:wait_loop
tasklist /fi ""pid eq %OLD_PID%"" | find ""%OLD_PID%"" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto wait_loop
)

taskkill /f /im ChattyWidgets.exe >nul 2>&1
timeout /t 1 /nobreak >nul

start /wait """" ""%SETUP_EXE%"" --silent --update

start """" ""%EXE_PATH%""
";
            }

            File.WriteAllText(runnerScript, scriptContent, System.Text.Encoding.UTF8);

            var psi = new ProcessStartInfo("cmd.exe", $"/c \"\"{runnerScript}\"\"")
            {
                CreateNoWindow = true,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = updateDir
            };
            Process.Start(psi);

            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateService] Error in RestartAndApplyUpdate: {ex.Message}");
        }
    }
}
