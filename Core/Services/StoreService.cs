using System.IO;
using System.Net.Http;
using System.Text.Json;
using ChattyWidgets.Core.Models;

namespace ChattyWidgets.Core.Services;

public class StoreService
{
    private static StoreService? _instance;
    public static StoreService Instance => _instance ??= new StoreService();

    private readonly HttpClient _httpClient = new();
    public static readonly string StarterPackagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StarterWidgets");

    private StoreService()
    {
    }

    public List<StoreItem>? LastLoadedCatalog { get; private set; }

    /// <summary>
    /// Gets the list of available widgets in the store (remote catalog is the source of truth, with local starter widgets as offline fallback).
    /// </summary>
    public async Task<List<StoreItem>> GetStoreCatalogAsync(string catalogUrl)
    {
        var items = new List<StoreItem>();
        List<StoreItem>? remoteItems = null;

        // Try fetching remote items first (primary source of truth)
        try
        {
            if (!string.IsNullOrWhiteSpace(catalogUrl) && Uri.IsWellFormedUriString(catalogUrl, UriKind.Absolute))
            {
                string fetchUrl = catalogUrl.Contains('?')
                    ? $"{catalogUrl}&_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"
                    : $"{catalogUrl}?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var request = new HttpRequestMessage(HttpMethod.Get, fetchUrl);
                request.Headers.UserAgent.ParseAdd("ChattyWidgets/1.0");

                var response = await _httpClient.SendAsync(request, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cts.Token);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    remoteItems = JsonSerializer.Deserialize<List<StoreItem>>(json, options);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StoreService] Remote catalog fetch error: {ex.Message}");
        }

        if (remoteItems != null && remoteItems.Count > 0)
        {
            items.AddRange(remoteItems);

            // Add any local starter widgets that are not present in remote catalog
            foreach (var starter in GetStarterWidgetsCatalog())
            {
                if (!items.Any(x => x.Id.Equals(starter.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    items.Add(starter);
                }
            }
        }
        else
        {
            // Offline fallback
            items.AddRange(GetStarterWidgetsCatalog());
        }

        // Mark installed items and detect updates
        var installed = ChwPackageService.GetInstalledWidgets();
        foreach (var item in items)
        {
            var match = installed.FirstOrDefault(x => x.Manifest.Id.Equals(item.Id, StringComparison.OrdinalIgnoreCase));
            if (match.Manifest != null)
            {
                item.IsInstalled = true;
                item.InstalledVersion = match.Manifest.Version;
                item.HasUpdate = IsNewerVersion(item.Version, match.Manifest.Version);
            }
            else
            {
                item.IsInstalled = false;
                item.HasUpdate = false;
            }
        }

        LastLoadedCatalog = items;
        return items;
    }

    public static bool IsNewerVersion(string remoteVer, string localVer)
    {
        if (string.IsNullOrWhiteSpace(remoteVer) || string.IsNullOrWhiteSpace(localVer))
            return false;

        remoteVer = remoteVer.Trim().TrimStart('v', 'V');
        localVer = localVer.Trim().TrimStart('v', 'V');

        if (Version.TryParse(remoteVer, out var rVer) && Version.TryParse(localVer, out var lVer))
        {
            return rVer > lVer;
        }

        var rParts = remoteVer.Split('.');
        var lParts = localVer.Split('.');
        int maxLen = Math.Max(rParts.Length, lParts.Length);
        for (int i = 0; i < maxLen; i++)
        {
            int rNum = (i < rParts.Length && int.TryParse(rParts[i], out int r)) ? r : 0;
            int lNum = (i < lParts.Length && int.TryParse(lParts[i], out int l)) ? l : 0;
            if (rNum > lNum) return true;
            if (rNum < lNum) return false;
        }

        return string.Compare(remoteVer, localVer, StringComparison.OrdinalIgnoreCase) > 0;
    }

    /// <summary>
    /// Returns default catalog for bundled starter widgets.
    /// </summary>
    public List<StoreItem> GetStarterWidgetsCatalog()
    {
        return new List<StoreItem>
        {
            new StoreItem
            {
                Id = "com.chatty.clock-glass",
                Name = "Стеклянные Часы",
                Version = "1.0.0",
                Author = "ChattyWidgets",
                Category = "Часы и Время",
                Description = "Элегантные цифровые и аналоговые часы с неоморфизмом, эффектом матового стекла и датой.",
                Type = "web",
                Size = "18 KB",
                IsOfficial = true,
                TrustLevel = "official"
            },
            new StoreItem
            {
                Id = "com.chatty.system-gauges",
                Name = "Датчики Ресурсов",
                Version = "1.0.0",
                Author = "ChattyWidgets",
                Category = "Система",
                Description = "Ультра-легковесный нативный монитор CPU, RAM, SSD и сетевой активности с мгновенным откликом.",
                Type = "native",
                Size = "8 KB",
                IsOfficial = true,
                TrustLevel = "official"
            },
            new StoreItem
            {
                Id = "com.chatty.music-player",
                Name = "Мини-Медиаплеер",
                Version = "1.0.0",
                Author = "ChattyWidgets",
                Category = "Медиа",
                Description = "Стильный настольный контроллер для Spotify, Яндекс.Музыки, браузера и системных аудиоплееров.",
                Type = "web",
                Size = "24 KB",
                IsOfficial = true,
                TrustLevel = "official"
            },
            new StoreItem
            {
                Id = "com.chatty.sticky-notes",
                Name = "Быстрые Заметки / ToDo",
                Version = "1.0.0",
                Author = "ChattyWidgets",
                Category = "Утилиты",
                Description = "Компактный стикер для важных заметок и чек-листа задач прямо на рабочем столе с автосохранением.",
                Type = "web",
                Size = "16 KB",
                IsOfficial = true,
                TrustLevel = "official"
            },
            new StoreItem
            {
                Id = "com.chatty.crypto-tracker",
                Name = "Курсы Валют и Крипты",
                Version = "1.0.0",
                Author = "ChattyWidgets",
                Category = "Финансы",
                Description = "Котировки BTC, ETH, GRAM, SOL, а также курсы USD, EUR и RUB в реальном времени.",
                Type = "web",
                Size = "22 KB",
                IsOfficial = true,
                TrustLevel = "official"
            },
            new StoreItem
            {
                Id = "com.chatty.weather-card",
                Name = "Прогноз Погоды",
                Version = "1.0.0",
                Author = "ChattyWidgets",
                Category = "Погода",
                Description = "Детальный погодный информер с текущей температурой, ощущением, влажностью, ветром и прогнозом.",
                Type = "web",
                Size = "20 KB",
                IsOfficial = true,
                TrustLevel = "official"
            },
            new StoreItem
            {
                Id = "com.chatty.calendar-glass",
                Name = "Календарь",
                Version = "1.0.0",
                Author = "ChattyWidgets",
                Category = "Утилиты",
                Description = "Элегантный настольный календарь с переключением месяцев, подсветкой сегодня и быстрыми заметками на дни.",
                Type = "web",
                Size = "18 KB",
                IsOfficial = true,
                TrustLevel = "official"
            },
            new StoreItem
            {
                Id = "com.chatty.media-frame",
                Name = "Медиа-Фоторамка",
                Version = "1.0.0",
                Author = "ChattyWidgets",
                Category = "Медиа",
                Description = "Настольная фоторамка и видеоплеер для фоток, гифок или зацикленных видео (MP4, WebM, PNG, JPG, GIF).",
                Type = "web",
                Size = "20 KB",
                IsOfficial = true,
                TrustLevel = "official"
            },
            new StoreItem
            {
                Id = "com.chatty.gpu-monitor",
                Name = "Монитор GPU & Графика",
                Version = "1.0.0",
                Author = "ChattyWidgets",
                Category = "Система",
                Description = "Мониторинг видеокарты: загрузка GPU %, видеопамять VRAM, температура и модель адаптера.",
                Type = "web",
                Size = "20 KB",
                IsOfficial = true,
                TrustLevel = "official"
            },
            new StoreItem
            {
                Id = "com.chatty.pomodoro-timer",
                Name = "Фокус-Таймер Pomodoro",
                Version = "1.0.0",
                Author = "ChattyWidgets",
                Category = "Продуктивность",
                Description = "Таймер 25/5 мин для фокуса на делах с системными тост-уведомлениями и приятным звуковым сигналом.",
                Type = "web",
                Size = "18 KB",
                IsOfficial = true,
                TrustLevel = "official"
            },
            new StoreItem
            {
                Id = "com.chatty.quick-dock",
                Name = "Быстрый Док / Лаунчер",
                Version = "1.0.0",
                Author = "ChattyWidgets",
                Category = "Утилиты",
                Description = "Плавающая панель быстрого запуска любимых программ, сайтов, проводника и терминала в стиле macOS.",
                Type = "web",
                Size = "22 KB",
                IsOfficial = true,
                TrustLevel = "official"
            },
            new StoreItem
            {
                Id = "com.chatty.rss-reader",
                Name = "Лента Новостей & RSS",
                Version = "1.0.0",
                Author = "ChattyWidgets",
                Category = "Новости",
                Description = "Живая лента новостей технологий, крипты и Telegram-каналов с автообновлением и карточками.",
                Type = "web",
                Size = "24 KB",
                IsOfficial = true,
                TrustLevel = "official"
            }
        };
    }

    /// <summary>
    /// Installs a store item either from bundled starter packages or remote URL.
    /// </summary>
    public async Task<WidgetManifest> InstallStoreItemAsync(StoreItem item)
    {
        // 1. If remote download URL is provided, try downloading the latest version
        if (!string.IsNullOrWhiteSpace(item.DownloadUrl))
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"{item.Id}_{Guid.NewGuid():N}.chw");
            try
            {
                string fetchUrl = item.DownloadUrl.Contains('?') 
                    ? $"{item.DownloadUrl}&_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}" 
                    : $"{item.DownloadUrl}?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

                using var request = new HttpRequestMessage(HttpMethod.Get, fetchUrl);
                request.Headers.UserAgent.ParseAdd("ChattyWidgets/1.0");
                using var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var bytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(tempFile, bytes);
                var manifest = ChwPackageService.InstallPackage(tempFile);

                // If instances are currently running, restart them with the new files
                WidgetManager.Instance.RestartWidgetInstances(item.Id);

                return manifest;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StoreService] Remote download error: {ex.Message}. Falling back to bundled if available.");
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }

        // 2. Offline fallback: check if bundled .chw exists in StarterPackagesDir or app assets
        string bundledPath = Path.Combine(StarterPackagesDir, $"{item.Id}.chw");
        if (File.Exists(bundledPath))
        {
            var manifest = ChwPackageService.InstallPackage(bundledPath);
            WidgetManager.Instance.RestartWidgetInstances(item.Id);
            return manifest;
        }

        throw new FileNotFoundException($"Пакет виджета {item.Name} не найден для установки");
    }
}
