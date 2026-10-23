#nullable enable
using System.Diagnostics;
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
}
