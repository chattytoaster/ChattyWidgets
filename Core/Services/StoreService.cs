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

        // Mark installed items and detect updates
        var installed = ChwPackageService.GetInstalledWidgets();
        foreach (var item in items)
        {
            var match = installed.FirstOrDefault(x => x.Manifest.Id == item.Id);
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

        return items;
    }

    public static bool IsNewerVersion(string remoteVer, string localVer)
    {
        if (Version.TryParse(remoteVer, out var rVer) && Version.TryParse(localVer, out var lVer))
        {
            return rVer > lVer;
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
