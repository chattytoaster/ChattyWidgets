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

    /// <summary>
    /// Gets the list of available widgets in the store (merging remote and bundled starter widgets).
    /// </summary>
    public async Task<List<StoreItem>> GetStoreCatalogAsync(string catalogUrl)
    {
        var items = new List<StoreItem>();

        // First add starter bundled widgets
        items.AddRange(GetStarterWidgetsCatalog());

        // Then try fetching remote items
        try
        {
            if (!string.IsNullOrWhiteSpace(catalogUrl) && Uri.IsWellFormedUriString(catalogUrl, UriKind.Absolute))
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var response = await _httpClient.GetStringAsync(catalogUrl, cts.Token);
                var remoteItems = JsonSerializer.Deserialize<List<StoreItem>>(response);
                if (remoteItems != null)
                {
                    foreach (var remote in remoteItems)
                    {
                        if (!items.Any(x => x.Id == remote.Id))
                        {
                            items.Add(remote);
                        }
                    }
                }
            }
        }
        catch
        {
            // Offline fallback is already in `items`
        }

        // Mark installed items
        var installed = ChwPackageService.GetInstalledWidgets();
        foreach (var item in items)
        {
            item.IsInstalled = installed.Any(x => x.Manifest.Id == item.Id);
        }

        return items;
    }

    /// <summary>
    /// Returns default catalog for the 5 built-in starter widgets.
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
                Size = "18 KB"
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
                Size = "8 KB"
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
                Size = "24 KB"
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
                Size = "16 KB"
            },
            new StoreItem
            {
                Id = "com.chatty.crypto-tracker",
                Name = "Курсы Валют и Крипты",
                Version = "1.0.0",
                Author = "ChattyWidgets",
                Category = "Финансы",
                Description = "Котировки BTC, ETH, TON, SOL, а также курсы USD, EUR и RUB в реальном времени.",
                Type = "web",
                Size = "22 KB"
            }
        };
    }

    /// <summary>
    /// Installs a store item either from bundled starter packages or remote URL.
    /// </summary>
    public async Task<WidgetManifest> InstallStoreItemAsync(StoreItem item)
    {
        // Check if bundled .chw exists in StarterPackagesDir or app assets
        string bundledPath = Path.Combine(StarterPackagesDir, $"{item.Id}.chw");
        if (File.Exists(bundledPath))
        {
            return ChwPackageService.InstallPackage(bundledPath);
        }

        // If remote download URL is provided
        if (!string.IsNullOrWhiteSpace(item.DownloadUrl))
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"{item.Id}_{Guid.NewGuid():N}.chw");
            try
            {
                var bytes = await _httpClient.GetByteArrayAsync(item.DownloadUrl);
                await File.WriteAllBytesAsync(tempFile, bytes);
                return ChwPackageService.InstallPackage(tempFile);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }

        throw new FileNotFoundException($"Пакет виджета {item.Name} не найден для установки");
    }
}
