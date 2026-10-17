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
                Id = "com.chatty.calendar-glass",
                Name = "Glass Calendar",
                NameRu = "Календарь",
                Version = "1.1.0",
                Author = "ChattyWidgets",
                Category = "General",
                Description = "Elegant desktop month calendar with smooth navigation, today highlight, and customizable first day of week.",
                DescriptionRu = "Элегантный настольный календарь с переключением месяцев, подсветкой сегодня и нативным плавным рендерингом.",
                Type = "native",
                Size = "9 KB",
                IsOfficial = true,
                TrustLevel = "official",
                DownloadUrl = "https://raw.githubusercontent.com/chattytoaster/ChattyWidgetsLibrary/main/packages/com.chatty.calendar-glass.chw"
            },
            new StoreItem
            {
                Id = "com.chatty.clock-glass",
                Name = "Glass Clock",
                NameRu = "Стеклянные Часы",
                Version = "2.0.0",
                Author = "ChattyWidgets",
                Category = "General",
                Description = "Elegant digital frosted glass clock with international timezones, world cities, and date.",
                DescriptionRu = "Элегантные цифровые часы с неоморфизмом, эффектом матового стекла и актуальной датой.",
                Type = "native",
                Size = "8 KB",
                IsOfficial = true,
                TrustLevel = "official",
                DownloadUrl = "https://raw.githubusercontent.com/chattytoaster/ChattyWidgetsLibrary/main/packages/com.chatty.clock-glass.chw"
            },
            new StoreItem
            {
                Id = "com.chatty.crypto-tracker",
                Name = "Crypto & Currency Tracker",
                NameRu = "Котировки & Крипта",
                Version = "1.1.0",
                Author = "ChattyWidgets",
                Category = "Finance",
                Description = "Real-time cryptocurrency & fiat currency exchange rates tracker with 30+ assets and custom tickers.",
                DescriptionRu = "Курсы криптовалют и фиатных валют в реальном времени с поддержкой 30+ популярных активов и произвольных пар.",
                Type = "native",
                Size = "9 KB",
                IsOfficial = true,
                TrustLevel = "official",
                DownloadUrl = "https://raw.githubusercontent.com/chattytoaster/ChattyWidgetsLibrary/main/packages/com.chatty.crypto-tracker.chw"
            },
            new StoreItem
            {
                Id = "com.chatty.gpu-monitor",
                Name = "GPU Monitor",
                NameRu = "GPU Монитор",
                Version = "1.1.0",
                Author = "ChattyWidgets",
                Category = "System",
                Description = "Lightweight GPU performance monitor displaying 3D load, dedicated VRAM, temperature, and hardware counters.",
                DescriptionRu = "Монитор видеокарты с отображением нагрузки 3D ядра, видеопамяти VRAM, температуры и аппаратных счетчиков.",
                Type = "native",
                Size = "10 KB",
                IsOfficial = true,
                TrustLevel = "official",
                DownloadUrl = "https://raw.githubusercontent.com/chattytoaster/ChattyWidgetsLibrary/main/packages/com.chatty.gpu-monitor.chw"
            },
            new StoreItem
            {
                Id = "com.chatty.media-frame",
                Name = "Photo Frame",
                NameRu = "Медиа-Фоторамка",
                Version = "1.1.0",
                Author = "ChattyWidgets",
                Category = "Media",
                Description = "Desktop photo frame with slideshow support, single photo mode, folder picker, and drag-and-drop support.",
                DescriptionRu = "Настольная фоторамка для снимков и слайд-шоу с выбором папок, поддержкой драг-н-дроп и идеальным скруглением углов.",
                Type = "native",
                Size = "10 KB",
                IsOfficial = true,
                TrustLevel = "official",
                DownloadUrl = "https://raw.githubusercontent.com/chattytoaster/ChattyWidgetsLibrary/main/packages/com.chatty.media-frame.chw"
            },
            new StoreItem
            {
                Id = "com.chatty.music-player",
                Name = "Mini Music Player",
                NameRu = "Мини-Медиаплеер",
                Version = "1.1.0",
                Author = "ChattyWidgets",
                Category = "Media",
                Description = "Mini desktop media controller with spinning vinyl, play/pause controls, volume, and track marquee ticker.",
                DescriptionRu = "Компактный медиаплеер с крутящейся виниловой пластинкой, кнопками управления и бегущей строкой названия трека.",
                Type = "native",
                Size = "10 KB",
                IsOfficial = true,
                TrustLevel = "official",
                DownloadUrl = "https://raw.githubusercontent.com/chattytoaster/ChattyWidgetsLibrary/main/packages/com.chatty.music-player.chw"
            },
            new StoreItem
            {
                Id = "com.chatty.pomodoro-timer",
                Name = "Focus Timer & Pomodoro",
                NameRu = "Таймер & Фокус",
                Version = "1.1.0",
                Author = "ChattyWidgets",
                Category = "Productivity",
                Description = "Productivity timer featuring standard countdown timer, focus intervals, break mode, and audio alerts.",
                DescriptionRu = "Таймер продуктивности с обычным обратным отсчетом на 1 месте, режимами фокуса, отдыха и звуковыми оповещениями.",
                Type = "native",
                Size = "9 KB",
                IsOfficial = true,
                TrustLevel = "official",
                DownloadUrl = "https://raw.githubusercontent.com/chattytoaster/ChattyWidgetsLibrary/main/packages/com.chatty.pomodoro-timer.chw"
            },
            new StoreItem
            {
                Id = "com.chatty.quick-dock",
                Name = "Quick Dock",
                NameRu = "Панель Дока",
                Version = "1.1.0",
                Author = "ChattyWidgets",
                Category = "Utilities",
                Description = "Floating dock for quick app launching, folder navigation, web shortcuts, and system tools in macOS style.",
                DescriptionRu = "Плавающая панель быстрого запуска программ, папок, терминала и проводника в стиле macOS с выбором приложений.",
                Type = "native",
                Size = "9 KB",
                IsOfficial = true,
                TrustLevel = "official",
                DownloadUrl = "https://raw.githubusercontent.com/chattytoaster/ChattyWidgetsLibrary/main/packages/com.chatty.quick-dock.chw"
            },
            new StoreItem
            {
                Id = "com.chatty.rss-reader",
                Name = "RSS News Reader",
                NameRu = "Лента Новостей & RSS",
                Version = "1.1.0",
                Author = "ChattyWidgets",
                Category = "News",
                Description = "Live news reader with international UN News in Russian, Euronews, TechCrunch, Hacker News, and custom RSS feeds.",
                DescriptionRu = "Живая лента новостей с Новостями ООН на русском, Euronews, Habr, IT, криптой и поддержкой произвольных RSS-ссылок.",
                Type = "native",
                Size = "9 KB",
                IsOfficial = true,
                TrustLevel = "official",
                DownloadUrl = "https://raw.githubusercontent.com/chattytoaster/ChattyWidgetsLibrary/main/packages/com.chatty.rss-reader.chw"
            },
            new StoreItem
            {
                Id = "com.chatty.sticky-notes",
                Name = "Notes & Tasks",
                NameRu = "Заметки и Задачи",
                Version = "1.1.0",
                Author = "ChattyWidgets",
                Category = "Productivity",
                Description = "Desktop sticky notes & task checklist with instant state persistence, smart empty state, and 5 color accents.",
                DescriptionRu = "Интерактивный список дел и стикер заметок прямо на рабочем столе с автосохранением и цветовыми акцентами.",
                Type = "native",
                Size = "10 KB",
                IsOfficial = true,
                TrustLevel = "official",
                DownloadUrl = "https://raw.githubusercontent.com/chattytoaster/ChattyWidgetsLibrary/main/packages/com.chatty.sticky-notes.chw"
            },
            new StoreItem
            {
                Id = "com.chatty.system-gauges",
                Name = "System Gauges",
                NameRu = "Системный Монитор",
                Version = "1.1.0",
                Author = "ChattyWidgets",
                Category = "System",
                Description = "Ultra-lightweight native hardware monitor for CPU, RAM, SSD, and Network with linear bars, rings, and 2-column grid.",
                DescriptionRu = "Ультра-легковесный нативный монитор процессора, памяти, SSD и сети с кольцевыми датчиками и сеткой 2 в ряд.",
                Type = "native",
                Size = "10 KB",
                IsOfficial = true,
                TrustLevel = "official",
                DownloadUrl = "https://raw.githubusercontent.com/chattytoaster/ChattyWidgetsLibrary/main/packages/com.chatty.system-gauges.chw"
            },
            new StoreItem
            {
                Id = "com.chatty.weather-card",
                Name = "Weather Card",
                NameRu = "Прогноз Погоды",
                Version = "1.1.0",
                Author = "ChattyWidgets",
                Category = "Weather",
                Description = "Real-time weather conditions and 5-day forecast powered by Open-Meteo with city search and standard units.",
                DescriptionRu = "Информативная погодная карточка с прогнозом на 5 дней, поиском городов по всему миру и стандартными единицами.",
                Type = "native",
                Size = "27 KB",
                IsOfficial = true,
                TrustLevel = "official",
                DownloadUrl = "https://raw.githubusercontent.com/chattytoaster/ChattyWidgetsLibrary/main/packages/com.chatty.weather-card.chw"
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
