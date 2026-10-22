using System.Windows;
using ChattyWidgets.Core.Models;

namespace ChattyWidgets.Core.Services;

public class LocalizationService
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    public event Action? LanguageChanged;

    private string _currentLanguage = "ru";
    public string CurrentLanguage
    {
        get => _currentLanguage;
        private set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                LanguageChanged?.Invoke();
            }
        }
    }

    public bool IsRussian => CurrentLanguage == "ru";
    public bool IsEnglish => CurrentLanguage == "en";

    private readonly Dictionary<string, string> _ru = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _en = new(StringComparer.OrdinalIgnoreCase);

    private LocalizationService()
    {
        InitializeDictionaries();
    }

    public void Initialize(string initialLanguage)
    {
        if (string.Equals(initialLanguage, "en", StringComparison.OrdinalIgnoreCase))
        {
            CurrentLanguage = "en";
        }
        else
        {
            CurrentLanguage = "ru";
        }
    }

    public void SetLanguage(string lang)
    {
        string normalized = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ru";
        if (_currentLanguage != normalized)
        {
            CurrentLanguage = normalized;
            try
            {
                WidgetManager.Instance.Settings.Language = normalized;
                WidgetManager.Instance.SaveSettings();
            }
            catch { }
        }
    }

    public string T(string key, string? defaultText = null)
    {
        var dict = _currentLanguage == "en" ? _en : _ru;
        if (dict.TryGetValue(key, out var val))
            return val;

        // Fallback to RU dictionary if EN is missing, or vice versa
        var fallbackDict = _currentLanguage == "en" ? _ru : _en;
        if (fallbackDict.TryGetValue(key, out var fallbackVal))
            return fallbackVal;

        return defaultText ?? key;
    }

    private void Add(string key, string ru, string en)
    {
        _ru[key] = ru;
        _en[key] = en;
    }

    private void InitializeDictionaries()
    {
        // Common
        Add("Common.Settings", "Настройки", "Settings");
        Add("Common.Close", "Закрыть", "Close");
        Add("Common.Pin", "📌 Закрепить на столе", "📌 Pin to desktop");
        Add("Common.Unpin", "🔓 Открепить", "🔓 Unpin");
        Add("Common.ClickThrough", "🖱 Сквозной клик", "🖱 Click-through");
        Add("Common.Scale", "🔍 Масштаб", "🔍 Scale");
        Add("Common.Opacity", "👁 Прозрачность", "👁 Opacity");
        Add("Common.Layer", "📑 Слой виджета", "📑 Widget Layer");
        Add("Common.LayerDesktop", "Рабочий стол (Фон)", "Desktop (Background)");
        Add("Common.LayerNormal", "Обычное окно", "Normal Window");
        Add("Common.LayerTopmost", "Поверх всех окон", "Always On Top");
        Add("Common.HideAnchor", "Точка управления", "Anchor Dot");
        Add("Common.HideAnchorHint", "Скрывать точку при неактивности", "Auto-hide anchor dot");
        Add("Common.Duplicate", "Создать копию виджета", "Duplicate widget instance");
        Add("Common.CloseWidget", "✕ Закрыть виджет", "✕ Close widget");
        Add("Common.Refresh", "Обновить", "Refresh");
        Add("Common.Save", "Сохранить", "Save");
        Add("Common.Done", "Готово", "Done");
        Add("Common.Cancel", "Отмена", "Cancel");
        Add("Common.Delete", "Удалить", "Delete");
        Add("Common.Add", "Добавить", "Add");
        Add("Common.Language", "Язык", "Language");
        Add("Common.Theme", "Тема оформления", "Theme");
        Add("Common.ThemeDark", "🌙 Тёмная тема (Dark)", "🌙 Dark Theme");
        Add("Common.ThemeLight", "☀️ Светлая тема (Light)", "☀️ Light Theme");
        Add("Common.ThemeAuto", "✦ Как в Хабе (Авто)", "✦ Match Hub (Auto)");
        Add("Common.ThemeHint", "Выберите тему для этого виджета или используйте глобальную настройку Хаба:", "Choose theme for this widget or use global Hub setting:");
        Add("Common.ChangesApplied", "Изменения применяются мгновенно", "Changes apply instantly");
        Add("Common.Ready", "Готово", "Ready");

        // Hub Navigation & Title
        Add("Hub.AppTitle", "ChattyWidgets", "ChattyWidgets");
        Add("Hub.AppSubtitle", "Desktop Widget Engine", "Desktop Widget Engine");

        // Context Menu (Right-click on widgets)
        Add("Menu.Settings", "⚙️ Настройки виджета...", "⚙️ Widget Settings...");
        Add("Menu.Duplicate", "📋 Скопировать виджет (Создать копию)", "📋 Duplicate Widget (Create copy)");
        Add("Menu.Delete", "🗑 Удалить этот виджет", "🗑 Delete this widget");
        Add("Menu.LayerGroup", "Слой отображения", "Display Layer");
        Add("Menu.LayerDesktop", "На рабочем столе (под всеми окнами)", "On Desktop (under all windows)");
        Add("Menu.LayerNormal", "Плавающее окно", "Floating Window");
        Add("Menu.LayerTop", "Поверх всех окон", "Always On Top");
        Add("Menu.ThemeGroup", "Тема оформления", "Theme");
        Add("Menu.ThemeAuto", "✦ Как в Хабе (Авто)", "✦ Match Hub (Auto)");
        Add("Menu.ThemeDark", "🌙 Тёмная тема", "🌙 Dark Theme");
        Add("Menu.ThemeLight", "☀️ Светлая тема", "☀️ Light Theme");
        Add("Menu.LockPosition", "Зафиксировать позицию", "Lock Position");
        Add("Menu.ClickThrough", "Клик насквозь (Click-through)", "Click-through");
        Add("Menu.AutoHideDot", "Спрятать точку управления", "Auto-hide Anchor Dot");
        Add("Menu.ResizeGroup", "Изменить размер", "Resize");
        Add("Menu.Resize25", "25% (Мини)", "25% (Mini)");
        Add("Menu.Resize50", "50% (Компактный)", "50% (Compact)");
        Add("Menu.Resize75", "75% (Уменьшенный)", "75% (Small)");
        Add("Menu.Resize100", "100% (Обычный)", "100% (Normal)");
        Add("Menu.Resize125", "125% (Увеличенный)", "125% (Large)");
        Add("Menu.Resize150", "150% (Большой)", "150% (Extra Large)");
        Add("Menu.FreeResize", "Тянуть за края (Свободный размер)", "Drag edges to resize (Free resize)");
        Add("Menu.OpacityGroup", "Прозрачность фона", "Background Opacity");
        Add("Menu.Opacity100", "100% (Непрозрачный фон)", "100% (Opaque)");
        Add("Menu.Opacity85", "75% фона", "75% opacity");
        Add("Menu.Opacity70", "50% фона", "50% opacity");
        Add("Menu.Opacity50", "25% фона (Стекло)", "25% opacity (Glass)");
        Add("Menu.Monitors", "Переместить на монитор...", "Move to Monitor...");
        Add("Menu.GaugesConstructor", "🎨 Изменить формат (Конструктор)...", "🎨 Custom Format (Constructor)...");
        Add("Menu.CustomSettings", "Настройки виджета", "Widget Settings");
        Add("Menu.Reload", "⟳ Перезагрузить виджет", "⟳ Reload Widget");
        Add("Menu.DevTools", "🛠 Открыть DevTools (F12)", "🛠 Open DevTools (F12)");
        Add("Menu.OpenFolder", "Открыть папку виджета", "Open Widget Folder");
        Add("Menu.OpenHub", "Открыть Хаб ChattyWidgets", "Open ChattyWidgets Hub");
        Add("Menu.Close", "Скрыть виджет", "Hide Widget");
        Add("Anchor.ToolTip", "Точка управления ChattyWidgets (ПКМ — меню, ЛКМ — переместить)", "ChattyWidgets Anchor (Right-click: menu, Left-click: drag)");
        Add("ResizeGrip.ToolTip", "Потяните, чтобы изменить размер виджета", "Drag to resize widget");
        Add("Loading.Text", "✦ Загрузка виджета...", "✦ Loading widget...");

        // Tray Icon Context Menu
        Add("Tray.Tooltip", "ChattyWidgets — Движок виджетов рабочего стола", "ChattyWidgets — Desktop Widget Engine");
        Add("Tray.OpenHub", "Открыть Хаб ChattyWidgets", "Open ChattyWidgets Hub");
        Add("Tray.Presets", "Пресеты рабочих столов (.chwp)", "Desktop Presets (.chwp)");
        Add("Tray.NoPresets", "(Нет сохранённых пресетов)", "(No saved presets)");
        Add("Tray.ImportPreset", "Импортировать .chwp...", "Import .chwp...");
        Add("Tray.ToggleWidgets", "Скрыть / Показать виджеты (Win+Alt+W)", "Hide / Show Widgets (Win+Alt+W)");
        Add("Tray.PeekMode", "Режим «Быстрый взгляд» (Win+Alt+P)", "Peek Mode (Win+Alt+P)");
        Add("Tray.ActiveWidgets", "Виджеты на рабочем столе:", "Active Desktop Widgets:");
        Add("Tray.NoWidgets", "Нет установленных виджетов", "No installed widgets");
        Add("Tray.Store", "Магазин виджетов...", "Widget Store...");
        Add("Tray.Workshop", "Мастерская виджетов (.chw)...", "Widget Workshop (.chw)...");
        Add("Tray.Exit", "Выход", "Exit");
        Add("Tray.ImportDialogTitle", "Импорт пресета .chwp", "Import .chwp Preset");
        Add("Tray.ImportFilter", "Пакет пресета ChattyWidgets (*.chwp)|*.chwp|Все файлы (*.*)|*.*", "ChattyWidgets Preset (*.chwp)|*.chwp|All Files (*.*)|*.*");

        // Settings Window
        Add("SettingsWindow.Title", "Настройки виджета", "Widget Settings");
        Add("SettingsWindow.Status", "Изменения применяются мгновенно", "Changes apply instantly");
        Add("SettingsWindow.Done", "Готово", "Done");

        // Widget Theme Selector
        Add("Settings.ThemeTitle", "🎨 Тема оформления", "🎨 Visual Theme");
        Add("Settings.ThemeSubtitle", "Выберите тему для этого виджета или используйте глобальную настройку Хаба:", "Select theme for this widget or use global Hub setting:");
        Add("Settings.ThemeAuto", "✦ Как в Хабе (Автоматически)", "✦ Match Hub (Auto)");
        Add("Settings.ThemeDark", "🌙 Тёмная тема (Dark)", "🌙 Dark Theme");
        Add("Settings.ThemeLight", "☀️ Светлая тема (Light)", "☀️ Light Theme");
        Add("Settings.OpacityLevel", "Уровень прозрачности (от 15% до 100%):", "Opacity level (from 15% to 100%):");

        // System Gauges View & Widget
        Add("Gauges.HeaderTitle", "СИСТЕМА", "SYSTEM");
        Add("Gauges.CpuTitle", "Процессор (CPU)", "Processor (CPU)");
        Add("Gauges.RamTitle", "Память (RAM)", "Memory (RAM)");
        Add("Gauges.DiskTitle", "Диск (C:)", "Storage (C:)");
        Add("Gauges.DiskTitleShort", "💾 ДИСК (C:)", "💾 DISK (C:)");
        Add("Gauges.NetTitle", "Сеть (Network)", "Network");
        Add("Gauges.NetTitleShort", "🌐 СЕТЬ", "🌐 NETWORK");
        Add("Gauges.GpuTitle", "Графика (GPU)", "Graphics (GPU)");
        Add("Gauges.AllDisabled", "Все датчики отключены.\nОткройте Конструктор для включения.", "All sensors disabled.\nOpen Constructor to enable.");

        // Gauges Constructor
        Add("GaugesConstructor.Title", "Конструктор формата датчиков", "Sensor Format Constructor");
        Add("GaugesConstructor.Header", "Конструктор формата виджета", "Widget Format Constructor");
        Add("GaugesConstructor.Desc1", "Настройте элементы, их порядок и вид отображения:", "Configure items, their order and display mode:");
        Add("GaugesConstructor.Desc2", "Вы можете перемещать метрики кнопками ▲ и ▼, включать GPU и выбирать между полоской и круговой диаграммой.", "You can reorder metrics with ▲ and ▼, toggle GPU, and choose between bars and circular rings.");
        Add("GaugesConstructor.CirclesLabel", "Круговые диаграммы:", "Circular Gauges:");
        Add("GaugesConstructor.Cols2", "2 в ряд (Сетка)", "2 in a row (Grid)");
        Add("GaugesConstructor.Cols1", "1 в ряд (Список)", "1 in a row (List)");
        Add("GaugesConstructor.Reset", "↺ По умолчанию", "↺ Reset Defaults");
        Add("GaugesConstructor.Cancel", "Отмена", "Cancel");
        Add("GaugesConstructor.Apply", "Применить", "Apply");

        // System Gauges Sensors
        Add("SystemGauges.Cpu", "Процессор (CPU)", "Processor (CPU)");
        Add("SystemGauges.Ram", "Память (RAM)", "Memory (RAM)");
        Add("SystemGauges.Disk", "Накопитель (Диск C:)", "Storage (Drive C:)");
        Add("SystemGauges.Gpu", "Видеокарта (GPU)", "Graphics (GPU)");
        Add("SystemGauges.Net", "Сеть (Скорость)", "Network (Speed)");
        Add("Hub.TabMyWidgets", "📱 Мои виджеты", "📱 My Widgets");
        Add("Hub.TabStore", "🛍 Магазин", "🛍 Store");
        Add("Hub.TabWorkshop", "🛠 Мастерская", "🛠 Workshop");
        Add("Hub.TabSettings", "⚙ Настройки", "⚙ Settings");
        Add("Hub.InstalledTitle", "Установленные виджеты", "Installed Widgets");
        Add("Hub.ActiveCount", "{0} активно", "{0} active");
        Add("Hub.RefreshBtn", "Обновить", "Refresh");
        Add("Hub.PresetsLabel", "🎨 Пресеты стола:", "🎨 Layout Presets:");
        Add("Hub.ImportPresetBtn", "📥 Импорт .chwp", "📥 Import .chwp");
        Add("Hub.SavePresetBtn", "+ Сохранить текущий вид", "+ Save Current Layout");
        Add("Hub.ImportPresetTip", "Загрузить пресет рабочего стола из файла .chwp", "Load desktop layout preset from .chwp file");
        Add("Hub.SavePresetTip", "Сохранить текущее расположение виджетов на экране как пресет", "Save current screen widget layout as a preset");
        Add("Hub.BtnLaunch", "Запустить", "Launch");
        Add("Hub.BtnStop", "Остановить", "Stop");
        Add("Hub.BtnSettings", "⚙ Настройки", "⚙ Settings");
        Add("Hub.BtnUninstall", "Удалить", "Uninstall");
        Add("Hub.BtnAddInstance", "+ Еще один", "+ Duplicate");
        Add("Hub.NoWidgets", "Нет установленных виджетов", "No installed widgets found");
        Add("Hub.StoreTitle", "Каталог виджетов ChattyWidgets", "ChattyWidgets Catalog");
        Add("Hub.StoreSubtitle", "Загружайте и устанавливайте нативные виджеты в 1 клик", "Download and install native widgets in 1 click");
        Add("Hub.StoreSearchPlaceholder", "Поиск по каталогу...", "Search widgets...");
        Add("Hub.StoreCheckUpdates", "🔄 Проверить обновления", "🔄 Check Updates");
        Add("Hub.BtnInstall", "Установить", "Install");
        Add("Hub.BtnUpdate", "Обновить", "Update");
        Add("Hub.WorkshopTitle", "Мастерская разработчика", "Developer Workshop");
        Add("Hub.WorkshopSubtitle", "Создавайте собственные виджеты с нативным XAML и C#", "Create your own widgets with native XAML & C#");
        Add("Hub.SettingsTitle", "Настройки движка", "Engine Settings");
        Add("Hub.Autostart", "Запускать ChattyWidgets автоматически при старте Windows", "Launch ChattyWidgets automatically on Windows startup");
        Add("Hub.SnapGrid", "Включить прилипание виджетов к краям экрана и сетке", "Enable widget snapping to screen edges and grid");
        Add("Hub.Hotkeys", "Включить глобальные хоткеи (Win+Alt+W: скрыть/показать, Win+Alt+P: «Быстрый взгляд»)", "Enable global hotkeys (Win+Alt+W: toggle show/hide, Win+Alt+P: Peek)");
        Add("Hub.Acrylic", "Включить эффект размытия Acrylic / Mica для прозрачных виджетов", "Enable Acrylic / Mica blur effect for transparent widgets");
        Add("Hub.HotReload", "Включить Hot Reload (авто-перезагрузка при изменении кода виджета)", "Enable Hot Reload (auto-reload on widget code changes)");
        Add("Hub.DevMode", "Режим разработчика (F12 DevTools и расширенная консоль)", "Developer Mode (F12 DevTools and extended console)");
        Add("Hub.AlwaysResizable", "Виджеты расширяемые все время", "Widgets always resizable");
        Add("Hub.ThemeLabel", "Тема оформления (Хаб и виджеты):", "Theme (Hub and widgets):");
        Add("Hub.ThemeHint", "При светлой теме иконки темные, при темной — светлые", "Dark icons in light mode, light icons in dark mode");
        Add("Hub.LanguageLabel", "Язык интерфейса (Хаб и виджеты):", "Interface Language (Hub & Widgets):");
        Add("Hub.LanguageHint", "Русский и английский с мгновенным переключением", "Russian and English with instant switching");
        Add("Hub.GridStep", "Шаг сетки прилипания виджетов:", "Widget snap grid size:");
        Add("Hub.OpacityLabel", "Прозрачность фона виджетов по умолчанию:", "Default widget background opacity:");
        Add("Hub.ImportWidgetBtn", "➕ Импортировать виджет (.chw)", "➕ Import Widget (.chw)");
        Add("Hub.OpenAppDataBtn", "Открыть папку данных (%LocalAppData%)", "Open Data Folder (%LocalAppData%)");
        Add("Hub.AboutTitle", "О движке ChattyWidgets", "About ChattyWidgets Engine");
        Add("Hub.AboutVersion", "Версия: 1.0.0 (.NET 8.0 Windows x64)", "Version: 1.0.0 (.NET 8.0 Windows x64)");
        Add("Hub.AboutDesc", "Движок оптимизирован для фоновой работы: нативные виджеты не нагружают процессор (0% CPU) и занимают минимум памяти.", "Engine optimized for 24/7 background operation: native widgets produce 0% CPU overhead and consume minimal memory.");
        Add("Hub.UpdateAvailable", "Доступно новое обновление ChattyWidgets!", "A new version of ChattyWidgets is available!");
        Add("Hub.DownloadUpdate", "🚀 Скачать обновление", "🚀 Download Update");
        Add("Hub.CheckUpdates", "🔄 Проверить обновления", "🔄 Check for Updates");
        Add("Hub.CheckingUpdates", "Проверка обновлений...", "Checking for updates...");
        Add("Hub.LatestVersion", "У вас установлена последняя актуальная версия ✓", "You have the latest version installed ✓");
        Add("Hub.UpdateFound", "Доступна новая версия {0}! Нажмите кнопку выше для скачивания.", "New version {0} available! Click download above.");

        // Settings Dialog generic
        Add("Settings.WindowTitle", "Настройки виджета", "Widget Settings");
        Add("Settings.InstanceSection", "Параметры экземпляра на рабочем столе", "Desktop instance parameters");

        // 1. Weather Widget
        Add("Weather.Title", "Настройки Погоды", "Weather Settings");
        Add("Weather.Header", "🌤 Настройки прогноза погоды", "🌤 Weather Forecast Settings");
        Add("Weather.HeaderSub", "Выберите город или страну для загрузки данных через Open-Meteo API", "Select city or country to fetch forecast via Open-Meteo API");
        Add("Weather.QuickCity", "Выбор города мира:", "World City Selection:");
        Add("Weather.SearchOnline", "Онлайн поиск любого города (Open-Meteo):", "Online City Search (Open-Meteo):");
        Add("Weather.SearchBtn", "🔍 Найти", "🔍 Search");
        Add("Weather.ManualCoords", "Точные координаты (Широта / Долгота):", "Precise Coordinates (Latitude / Longitude):");
        Add("Weather.ApplyCoordsBtn", "Применить", "Apply");
        Add("Weather.UnitsTitle", "Единицы измерения и форматы:", "Measurement Units & Formats:");
        Add("Weather.UnitTemp", "Единицы температуры:", "Temperature Units:");
        Add("Weather.UnitCelsius", "Градусы Цельсия (°C)", "Celsius (°C)");
        Add("Weather.UnitFahrenheit", "Градусы Фаренгейта (°F)", "Fahrenheit (°F)");
        Add("Weather.UnitWind", "Единицы скорости ветра:", "Wind Speed Units:");
        Add("Weather.UnitMs", "м/с", "m/s");
        Add("Weather.UnitKmh", "км/ч", "km/h");
        Add("Weather.UnitMph", "миль/ч", "mph");
        Add("Weather.UnitPressure", "Атмосферное давление:", "Atmospheric Pressure:");
        Add("Weather.UnitMmhg", "мм рт. ст.", "mmHg");
        Add("Weather.UnitHpa", "гПа", "hPa");
        Add("Weather.UnitMm", "мм", "mm");
        Add("Weather.UnitMsLabel", "Метры в секунду (м/с)", "Meters per second (m/s)");
        Add("Weather.UnitKmhLabel", "Километры в час (км/ч)", "Kilometers per hour (km/h)");
        Add("Weather.UnitMphLabel", "Мили в час (mph)", "Miles per hour (mph)");
        Add("Weather.UnitMmhgLabel", "Миллиметры рт. ст. (мм)", "Millimeters of mercury (mmHg)");
        Add("Weather.UnitHpaLabel", "Гектопаскали / мбар (гПа / hPa)", "Hectopascals / mbar (hPa)");
        Add("Weather.RefreshBtn", "⟳ Обновить прогноз погоды сейчас", "⟳ Refresh weather forecast now");
        Add("Weather.CityChanged", "Город изменен на {0}. Загрузка прогноза...", "City changed to {0}. Loading forecast...");
        Add("Weather.Refreshed", "Погода успешно обновлена!", "Weather updated successfully!");
        Add("Weather.InvalidCoords", "Неверные координаты. Диапазон: Lat (-90..90), Lon (-180..180)", "Invalid coordinates. Range: Lat (-90..90), Lon (-180..180)");
        Add("Weather.FeelsLike", "Ощущается как", "Feels like");
        Add("Weather.Humidity", "Влажность", "Humidity");
        Add("Weather.Wind", "Ветер", "Wind");
        Add("Weather.Pressure", "Давление", "Pressure");
        Add("Weather.MinMax", "МИН / МАКС", "MIN / MAX");
        Add("Weather.Rain", "Осадки", "Precipitation");
        Add("Weather.UpdatedAt", "Обновлено в", "Updated at");
        Add("Weather.Clear", "Ясно", "Clear");
        Add("Weather.MostlyClear", "Преимущественно ясно", "Mostly Clear");
        Add("Weather.PartlyCloudy", "Переменная облачность", "Partly Cloudy");
        Add("Weather.Overcast", "Пасмурно", "Overcast");
        Add("Weather.Fog", "Туман", "Fog");
        Add("Weather.Drizzle", "Морось", "Drizzle");
        Add("Weather.RainDesc", "Дождь", "Rain");
        Add("Weather.SnowDesc", "Снегопад", "Snow");
        Add("Weather.Showers", "Ливень", "Showers");
        Add("Weather.Thunderstorm", "Гроза", "Thunderstorm");

        // 2. Clock Widget
        Add("Clock.Title", "Настройки Стеклянных Часов", "Glass Clock Settings");
        Add("Clock.Header", "🕒 Настройки отображения времени", "🕒 Time Display Settings");
        Add("Clock.HeaderSub", "Параметры часового пояса, формата времени и индикаторов", "Timezone, world cities, time format and indicators");
        Add("Clock.Timezone", "Часовой пояс / Мировое время:", "Timezone / World City:");
        Add("Clock.LocalTime", "Текущее местное время (Система)", "Local System Time");
        Add("Clock.24Hour", "24-часовой формат времени (14:30)", "24-Hour time format (14:30)");
        Add("Clock.12Hour", "12-часовой формат (02:30 PM)", "12-Hour format (02:30 PM)");
        Add("Clock.ShowSeconds", "Отображать секунды (:42)", "Display seconds (:42)");
        Add("Clock.ShowDayName", "Отображать день недели и дату", "Display day of week and full date");
        Add("Clock.TzLocal", "Текущее местное время (Система)", "Local System Time");
        Add("Clock.TzUtc", "🌐 UTC / GMT (Всемирное время)", "🌐 UTC / GMT (Universal Time)");
        Add("Clock.TzLondon", "🇬🇧 Лондон (London, GMT/BST)", "🇬🇧 London (GMT/BST)");
        Add("Clock.TzParis", "🇫🇷 Париж / Берлин (CET/CEST)", "🇫🇷 Paris / Berlin (CET/CEST)");
        Add("Clock.TzMoscow", "🇷🇺 Москва (MSK, UTC+3)", "🇷🇺 Moscow (MSK, UTC+3)");
        Add("Clock.TzDubai", "🇦🇪 Дубай (Dubai, UTC+4)", "🇦🇪 Dubai (GST, UTC+4)");
        Add("Clock.TzTokyo", "🇯🇵 Токио (Tokyo, JST, UTC+9)", "🇯🇵 Tokyo (JST, UTC+9)");
        Add("Clock.TzHongKong", "🇭🇰 Гонконг / Сингапур (UTC+8)", "🇭🇰 Hong Kong / Singapore (UTC+8)");
        Add("Clock.TzSydney", "🇦🇺 Сидней (Sydney, AEST)", "🇦🇺 Sydney (AEST/AEDT)");
        Add("Clock.TzNewYork", "🇺🇸 Нью-Йорк (New York, EST)", "🇺🇸 New York (EST/EDT)");
        Add("Clock.TzLosAngeles", "🇺🇸 Лос-Анджелес (LA, PST)", "🇺🇸 Los Angeles (PST/PDT)");

        // 3. Crypto & Currency Widget
        Add("Crypto.Title", "Настройки Котировок & Валют", "Market Rates & Crypto Settings");
        Add("Crypto.Header", "💎 Котировки криптовалют и курсы валют", "💎 Crypto Assets & Currency Rates");
        Add("Crypto.HeaderSub", "Актуальные цены через Binance API и центробанки", "Real-time rates via Binance API and Central Banks");
        Add("Crypto.HeaderTitle", "💎 КОТИРОВКИ & ВАЛЮТЫ", "💎 CRYPTO & FIAT");
        Add("Crypto.BaseCurrency", "Базовая валюта отображения:", "Display Base Currency:");
        Add("Crypto.BaseCurrencyHint", "Конвертировать все котировки в выбранную валюту", "Convert all prices to the selected base currency");
        Add("Crypto.VisibleAssets", "Отображаемые активы и валютные пары:", "Visible Assets & Currency Pairs:");
        Add("Crypto.RefreshInterval", "Интервал автообновления:", "Auto-refresh Interval:");
        Add("Crypto.Interval30s", "30 секунд (Быстро)", "30 seconds (Fast)");
        Add("Crypto.Interval60s", "60 секунд (По умолчанию)", "60 seconds (Default)");
        Add("Crypto.Interval5m", "5 минут", "5 minutes");
        Add("Crypto.Interval15m", "15 минут (Энергосбережение)", "15 minutes (Battery Saver)");
        Add("Crypto.SelectAll", "Выбрать все", "Select All");
        Add("Crypto.ResetDefaults", "По умолчанию", "Reset Defaults");
        Add("Crypto.RefreshBtn", "⟳ Обновить котировки с биржи сейчас", "⟳ Refresh exchange rates now");
        Add("Crypto.RefreshShort", "Обновить ↻", "Refresh ↻");
        Add("Crypto.Loading", "Загрузка котировок с Binance и ЦБ...", "Loading rates from Binance and Central Bank...");
        Add("Crypto.Refreshed", "Котировки успешно обновлены!", "Exchange rates updated successfully!");
        Add("Crypto.Error", "Ошибка загрузки котировок", "Failed to load rates");
        Add("Crypto.LastUpdated", "Обновлено: {0}", "Last updated: {0}");
        Add("Crypto.Price", "Цена", "Price");
        Add("Crypto.Change24h", "Изм. 24ч", "24h Change");

        // Supported Tokens (RU and EN)
        Add("Crypto.Token.BTC", "Биткоин (BTC)", "Bitcoin (BTC)");
        Add("Crypto.Token.ETH", "Эфириум (ETH)", "Ethereum (ETH)");
        Add("Crypto.Token.SOL", "Солана (SOL)", "Solana (SOL)");
        Add("Crypto.Token.GRAM", "Грам (GRAM)", "Gram (GRAM)");
        Add("Crypto.Token.BNB", "BNB (BNB)", "BNB (BNB)");
        Add("Crypto.Token.XRP", "Риппл (XRP)", "Ripple (XRP)");

        // Fiat Pairs (RU and EN)
        Add("Crypto.Pair.USDRUB", "Доллар США / Рубль (USD/RUB)", "US Dollar / Russian Ruble (USD/RUB)");
        Add("Crypto.Pair.EURRUB", "Евро / Рубль (EUR/RUB)", "Euro / Russian Ruble (EUR/RUB)");
        Add("Crypto.Pair.EURUSD", "Евро / Доллар (EUR/USD)", "Euro / US Dollar (EUR/USD)");
        Add("Crypto.Pair.GBPUSD", "Фунт / Доллар (GBP/USD)", "British Pound / US Dollar (GBP/USD)");

        Add("Crypto.GramToken", "Грам (GRAM) — Официальный токен", "Gram (GRAM) — Official Token");
        Add("Crypto.GramName", "Грам (GRAM)", "Gram (GRAM)");
        Add("Crypto.GramDesc", "Глобальная цифровая валюта", "Global digital currency");

        // 4. RSS Reader Widget
        Add("Rss.Title", "Настройки Новостей", "RSS News Reader Settings");
        Add("Rss.Header", "📰 Параметры новостной ленты", "📰 News Feed Settings");
        Add("Rss.HeaderSub", "Выбор международных каналов, IT, технологий и скорости листания", "International channels, tech, world news and slideshow duration");
        Add("Rss.FeedPreset", "Категория и источник новостей:", "News Category & Source:");
        Add("Rss.PresetWorld", "🌍 Мировые новости (BBC World / Reuters)", "🌍 World News (BBC World / Reuters)");
        Add("Rss.PresetUnRu", "🌐 Новости ООН на русском (UN News)", "🌐 UN News in Russian");
        Add("Rss.PresetEuronews", "🇪🇺 Euronews на русском (Euronews RU)", "🇪🇺 Euronews in Russian");
        Add("Rss.PresetTech", "💻 Технологии и AI (Hacker News / TechCrunch)", "💻 Tech & AI (Hacker News / TechCrunch)");
        Add("Rss.PresetHabr", "🚀 Хабр и Разработка (Habr IT)", "🚀 Developer Community (Habr IT)");
        Add("Rss.PresetCrypto", "💎 Криптовалюты (CoinDesk)", "💎 Cryptocurrency (CoinDesk)");
        Add("Rss.PresetScience", "🔬 Наука и Космос (NASA & Science)", "🔬 Science & Space (NASA & Science)");
        Add("Rss.AutoSlide", "Интервал авто-переключения:", "Auto-slide Interval:");
        Add("Rss.Slide8s", "8 секунд", "8 seconds");
        Add("Rss.Slide12s", "12 секунд (По умолчанию)", "12 seconds (Default)");
        Add("Rss.Slide20s", "20 секунд", "20 seconds");
        Add("Rss.Slide30s", "30 секунд", "30 seconds");
        Add("Rss.SlideManual", "Только вручную", "Manual only");
        Add("Rss.RefreshBtn", "⟳ Обновить ленту новостей", "⟳ Refresh news feed");
        Add("Rss.FeedUpdated", "Лента новостей обновлена", "News feed updated");
        Add("Rss.ReadMore", "Читать источник ↗", "Read more ↗");
        Add("Rss.CategoryTech", "AI И ТЕХНОЛОГИИ", "AI & TECHNOLOGY");
        Add("Rss.CategoryWorld", "МИРОВЫЕ НОВОСТИ", "WORLD NEWS");
        Add("Rss.CategoryUnRu", "НОВОСТИ ООН", "UN NEWS");
        Add("Rss.CategoryEuronews", "EURONEWS", "EURONEWS");
        Add("Rss.CategoryCrypto", "КРИПТОВАЛЮТЫ", "CRYPTOCURRENCY");
        Add("Rss.CategoryWindows", "WINDOWS & СТЕК", "WINDOWS & TECH");
        Add("Rss.CustomUrl", "Пользовательский RSS URL:", "Custom RSS Feed URL:");
        Add("Rss.CustomUrlPlaceholder", "https://example.com/rss.xml", "https://example.com/rss.xml");
        Add("Rss.ApplyUrl", "Применить URL", "Apply URL");

        // 5. Calendar Widget
        Add("Calendar.Title", "Настройки Календаря", "Calendar Settings");
        Add("Calendar.Header", "📅 Параметры стеклянного календаря", "📅 Glass Calendar Settings");
        Add("Calendar.HeaderSub", "Первый день недели, подсветка дат и номера недель", "First day of week, date highlighting and week numbers");
        Add("Calendar.FirstDay", "Первый день недели:", "First Day of Week:");
        Add("Calendar.FirstDayMon", "Понедельник (Россия, Европа, ISO 8601)", "Monday (Europe, ISO 8601)");
        Add("Calendar.FirstDaySun", "Воскресенье (США, Канада, Япония)", "Sunday (USA, Canada, Japan)");
        Add("Calendar.HighlightToday", "Выделять текущий день (Сегодня)", "Highlight current day (Today)");
        Add("Calendar.ShowWeekNumbers", "Отображать номера недель (№ / Wk)", "Display week numbers (№ / Wk)");
        Add("Calendar.BtnToday", "📅 Вернуться к текущему месяцу", "📅 Return to Current Month");
        Add("Calendar.TodayApplied", "Календарь переведен на текущий месяц", "Calendar reset to current month");
        Add("Calendar.SettingsSaved", "Настройки календаря сохранены", "Calendar settings saved");

        // Calendar In-Widget Labels
        Add("Calendar.TodayBtn", "Сегодня", "Today");
        Add("Calendar.TodayLabel", "📅 Сегодня:", "📅 Today:");
        Add("Calendar.WeekNumHeader", "№", "Wk");

        // Calendar Months (Full / Nominative)
        Add("Calendar.Month.1", "Январь", "January");
        Add("Calendar.Month.2", "Февраль", "February");
        Add("Calendar.Month.3", "Март", "March");
        Add("Calendar.Month.4", "Апрель", "April");
        Add("Calendar.Month.5", "Май", "May");
        Add("Calendar.Month.6", "Июнь", "June");
        Add("Calendar.Month.7", "Июль", "July");
        Add("Calendar.Month.8", "Август", "August");
        Add("Calendar.Month.9", "Сентябрь", "September");
        Add("Calendar.Month.10", "Октябрь", "October");
        Add("Calendar.Month.11", "Ноябрь", "November");
        Add("Calendar.Month.12", "Декабрь", "December");

        // Calendar Weekday Abbreviations
        Add("Calendar.Weekday.Mon", "ПН", "Mon");
        Add("Calendar.Weekday.Tue", "ВТ", "Tue");
        Add("Calendar.Weekday.Wed", "СР", "Wed");
        Add("Calendar.Weekday.Thu", "ЧТ", "Thu");
        Add("Calendar.Weekday.Fri", "ПТ", "Fri");
        Add("Calendar.Weekday.Sat", "СБ", "Sat");
        Add("Calendar.Weekday.Sun", "ВС", "Sun");

        // 6. Media Frame Widget
        Add("MediaFrame.Title", "Настройки Фоторамки", "Photo Frame Settings");
        Add("MediaFrame.Header", "🖼️ Выбор медиафайлов и режим показа", "🖼️ Media Files & Display Mode");
        Add("MediaFrame.HeaderSub", "Перетащите фото прямо на виджет или укажите папку на диске", "Drag & drop photos directly onto widget or choose folder");
        Add("MediaFrame.BtnFolder", "📁 Выбрать папку с фотографиями...", "📁 Select photo folder...");
        Add("MediaFrame.SelectFolder", "Выбрать папку", "Select folder");
        Add("MediaFrame.SelectFolderDialog", "Выберите папку с изображениями", "Select folder with images");
        Add("MediaFrame.CurrentFolder", "Текущая папка:", "Current folder:");
        Add("MediaFrame.DefaultPictures", "Стандартная папка «Изображения»", "Default Pictures folder");
        Add("MediaFrame.NoFolderSelected", "Папка не выбрана", "No folder selected");
        Add("MediaFrame.FitMode", "Режим заполнения кадра:", "Image Fit Mode:");
        Add("MediaFrame.FitCover", "Заполнить с обрезкой (Cover / UniformToFill)", "Fill & crop (Cover / UniformToFill)");
        Add("MediaFrame.FitContain", "Вписать целиком (Contain / Uniform)", "Fit completely (Contain / Uniform)");
        Add("MediaFrame.FitStretch", "Растянуть на весь кадр (Stretch / Fill)", "Stretch to fill (Fill)");
        Add("MediaFrame.SlideshowSpeed", "Скорость слайд-шоу", "Slideshow speed");
        Add("MediaFrame.SlideshowTimer", "Интервал смены фото:", "Slideshow interval:");
        Add("MediaFrame.SlideInterval", "Интервал смены фото:", "Slideshow interval:");
        Add("MediaFrame.Slide5s", "5 секунд", "5 seconds");
        Add("MediaFrame.Slide10s", "10 секунд", "10 seconds");
        Add("MediaFrame.Slide15s", "15 секунд", "15 seconds");
        Add("MediaFrame.Slide30s", "30 секунд", "30 seconds");
        Add("MediaFrame.Slide1m", "1 минута", "1 minute");
        Add("MediaFrame.Slide5m", "5 минут", "5 minutes");
        Add("MediaFrame.SlideManual", "Только вручную стрелками", "Manual with arrows only");
        Add("MediaFrame.Shuffle", "Случайный порядок (Shuffle)", "Shuffle / Random order");
        Add("MediaFrame.ShuffleHint", "Показывать фотографии в случайной последовательности", "Display photos in random order");
        Add("MediaFrame.FolderLoaded", "Загружены фотографии из папки '{0}'", "Photos loaded from folder '{0}'");
        Add("MediaFrame.DropHint", "Перетащите сюда фото или папку", "Drag photos or folders here");
        Add("MediaFrame.DragDropHint", "Перетащите сюда фото или папку", "Drag photos or folders here");
        Add("MediaFrame.DragDropSub", "или укажите папку в настройках (ПКМ)", "or select a folder in settings (Right-click)");
        Add("MediaFrame.DragDropSupported", "Поддерживается Drag & Drop: перетащите файлы или папки прямо на виджет", "Drag & Drop supported: drop files or folders directly onto the widget");
        Add("MediaFrame.PrevPhoto", "Предыдущее фото", "Previous photo");
        Add("MediaFrame.NextPhoto", "Следующее фото", "Next photo");
        Add("MediaFrame.NoImagesFound", "В папке нет поддерживаемых изображений", "No supported images found in folder");
        Add("MediaFrame.PhotosFound", "Найдено фото: {0}", "Photos found: {0}");
        Add("MediaFrame.PhotoCounter", "Фото {0} из {1}", "Photo {0} of {1}");
        Add("MediaFrame.BtnSingleFile", "🖼️ Выбрать одно фото...", "🖼️ Select single photo...");
        Add("MediaFrame.BtnSingleFileShort", "🖼️ Одно фото...", "🖼️ Single photo...");
        Add("MediaFrame.SelectPhotoDialog", "Выберите фотографию", "Select photo");
        Add("MediaFrame.SinglePhotoLoaded", "Выбрано фото '{0}'", "Photo selected: '{0}'");
        Add("MediaFrame.CurrentSource", "Текущий источник:", "Current source:");
        Add("MediaFrame.SourceSinglePhoto", "Одно фото: {0}", "Single photo: {0}");
        Add("MediaFrame.SourceFolder", "Папка: {0}", "Folder: {0}");
        Add("MediaFrame.ControlsSection", "Панель навигации и стрелки", "Navigation Controls & Arrows");
        Add("MediaFrame.AutoHideControls", "Скрывать панель (показывать только при наведении)", "Auto-hide control bar (show on hover)");
        Add("MediaFrame.AutoHideControlsHint", "Стрелки навигации и подпись плавно появляются при наведении курсора на фоторамку", "Navigation arrows and caption smoothly appear when hovering over the widget");
        Add("MediaFrame.OpacityTitle", "Прозрачность фоторамки и фото", "Frame & Photo Transparency");
        Add("MediaFrame.OpacityLevel", "Уровень непрозрачности (от 15% до 100%):", "Opacity level (from 15% to 100%):");
        Add("MediaFrame.OpacityHint", "Прозрачность плавно применяется как к стеклянной рамке, так и к самой фотографии", "Transparency smoothly applies to both the glass frame and the photo itself");

        // 7. Music Player Widget
        Add("Music.Title", "Настройки Медиаплеера", "Music Player Settings");
        Add("Music.Header", "🎵 Параметры мини-плеера", "🎵 Mini Player Settings");
        Add("Music.HeaderSub", "Управление виниловым диском, источниками медиа и громкостью", "Vinyl rotation, media sources and volume control");
        Add("Music.SourceHint", "Плеер автоматически считывает активные треки из Spotify, Браузеров, Apple Music и плееров Windows.", "Player automatically reads active tracks from Spotify, Browsers, Apple Music and Windows players.");
        Add("Music.VinylSpeed", "Скорость вращения винила:", "Vinyl Rotation Speed:");
        Add("Music.VinylNormal", "Обычная (33 об/мин)", "Normal (33 RPM)");
        Add("Music.VinylFast", "Быстрая (45 об/мин)", "Fast (45 RPM)");
        Add("Music.VinylSlow", "Плавная (Slow)", "Slow");
        Add("Music.VinylDisabled", "Отключено (Статичный диск)", "Disabled (Static Vinyl)");
        Add("Music.VinylSpinOnlyPlaying", "Крутить диск только когда музыка играет", "Spin vinyl only when music is playing");
        Add("Music.VinylSpinOnlyPlayingHint", "Диск останавливается на паузе и плавно крутится при воспроизведении", "Vinyl pauses when music stops and spins when playing");
        Add("Music.BtnReconnect", "⟳ Переподключиться к сессиям медиа", "⟳ Reconnect to Windows Media Sessions");
        Add("Music.Reconnected", "Сессии Windows Media обновлены", "Windows Media sessions refreshed");
        Add("Music.Waiting", "Ожидание медиа...", "Waiting for media...");
        Add("Music.StartSource", "Запустите Spotify, Браузер...", "Start Spotify, Browser...");

        // 8. Pomodoro Timer Widget
        Add("Pomodoro.Title", "Настройки Pomodoro", "Pomodoro Settings");
        Add("Pomodoro.Header", "🍅 Параметры таймера фокуса", "🍅 Focus Timer Settings");
        Add("Pomodoro.HeaderSub", "Интервалы продуктивности, отдыха и звуковые оповещения", "Productivity intervals, breaks and sound alerts");
        
        // Focus duration
        Add("Pomodoro.WorkDuration", "Длительность интервала фокуса:", "Focus Interval Duration:");
        Add("Pomodoro.Min15", "15 минут", "15 minutes");
        Add("Pomodoro.Min20", "20 минут", "20 minutes");
        Add("Pomodoro.Min25", "25 минут (Классика)", "25 minutes (Classic)");
        Add("Pomodoro.Min30", "30 минут", "30 minutes");
        Add("Pomodoro.Min45", "45 минут (Учеба)", "45 minutes (Study)");
        Add("Pomodoro.Min50", "50 минут", "50 minutes");
        Add("Pomodoro.Min60", "60 минут (Глубокая работа)", "60 minutes (Deep Work)");

        // Short break duration
        Add("Pomodoro.BreakDuration", "Длительность короткого отдыха:", "Short Break Duration:");
        Add("Pomodoro.Break3m", "3 минуты", "3 minutes");
        Add("Pomodoro.Break5m", "5 минут (Классика)", "5 minutes (Classic)");
        Add("Pomodoro.Break10m", "10 минут", "10 minutes");

        // Long break duration
        Add("Pomodoro.LongBreakDuration", "Длительность длинного отдыха:", "Long Break Duration:");
        Add("Pomodoro.LongBreak15m", "15 минут (Классика)", "15 minutes (Classic)");
        Add("Pomodoro.LongBreak20m", "20 минут", "20 minutes");
        Add("Pomodoro.LongBreak30m", "30 минут", "30 minutes");

        // Automation & Audio settings
        Add("Pomodoro.AutoStart", "Авто-старт следующего интервала", "Auto-start next interval");
        Add("Pomodoro.AutoStartHint", "Автоматически запускать таймер при переходе между фокусом и отдыхом", "Automatically start timer when switching between focus and break");
        Add("Pomodoro.SoundAlert", "Звуковой сигнал при завершении", "Audio alert sound on finish");
        Add("Pomodoro.SoundAlertHint", "Воспроизводить системный сигнал при смене интервала", "Play system chime sound when interval completes");

        // Modes, actions and tabs
        Add("Pomodoro.Focus", "Фокус", "Focus");
        Add("Pomodoro.Break", "Отдых", "Break");
        Add("Pomodoro.LongBreak", "Длинный отдых", "Long Break");
        Add("Pomodoro.Timer", "Таймер", "Timer");
        Add("Pomodoro.Start", "Старт", "Start");
        Add("Pomodoro.Pause", "Пауза", "Pause");
        Add("Pomodoro.Reset", "Сброс", "Reset");
        Add("Pomodoro.SwitchMode", "Сменить режим", "Switch Mode");
        Add("Pomodoro.TabFocus", "🍅 Фокус ({0}м)", "🍅 Focus ({0}m)");
        Add("Pomodoro.TabBreak", "☕ Отдых ({0}м)", "☕ Break ({0}m)");
        Add("Pomodoro.TabLongBreak", "🌿 Отдых ({0}м)", "🌿 Break ({0}m)");
        Add("Pomodoro.TabTimer", "⏱ Таймер", "⏱ Timer");
        Add("Pomodoro.ModeFocus", "🎯 ФОКУС", "🎯 FOCUS");
        Add("Pomodoro.ModeBreak", "☕ ОТДЫХ", "☕ BREAK");
        Add("Pomodoro.ModeLongBreak", "🌿 ДЛИННЫЙ ОТДЫХ", "🌿 LONG BREAK");
        Add("Pomodoro.ModeTimer", "⏱ ТАЙМЕР", "⏱ TIMER");
        Add("Pomodoro.BtnStart", "Старт", "Start");
        Add("Pomodoro.BtnPause", "Пауза", "Pause");
        Add("Pomodoro.BtnReset", "Сброс", "Reset");
        Add("Pomodoro.SessionActive", "🍅 Фокус-сессия активна", "🍅 Focus session active");
        Add("Pomodoro.BreakActive", "☕ Время перерыва", "☕ Break time active");
        Add("Pomodoro.LongBreakActive", "🌿 Восстанавливающий отдых", "🌿 Long recovery break active");
        Add("Pomodoro.TimerActive", "⏱ Идёт отсчёт времени", "⏱ Countdown in progress");
        Add("Pomodoro.TimerFinished", "🔔 Время вышло!", "🔔 Time is up!");
        Add("Pomodoro.TimerReady", "Готов к запуску", "Ready to start");
        Add("Pomodoro.SessionPaused", "⏸ Таймер на паузе", "⏸ Timer paused");
        Add("Pomodoro.CompletedCount", "Выполнено сессий: {0}", "Completed sessions: {0}");
        Add("Pomodoro.TimerDuration", "Длительность обычного таймера:", "Countdown Timer Duration:");
        Add("Pomodoro.Timer1m", "1 минута", "1 minute");
        Add("Pomodoro.Timer3m", "3 минуты", "3 minutes");
        Add("Pomodoro.Timer5m", "5 минут", "5 minutes");
        Add("Pomodoro.Timer10m", "10 минут (Классика)", "10 minutes (Classic)");
        Add("Pomodoro.Timer15m", "15 минут", "15 minutes");
        Add("Pomodoro.Timer20m", "20 минут", "20 minutes");
        Add("Pomodoro.Timer30m", "30 минут", "30 minutes");
        Add("Pomodoro.Timer45m", "45 минут", "45 minutes");
        Add("Pomodoro.Timer60m", "60 минут (1 час)", "60 minutes (1 hour)");
        Add("Pomodoro.Minus1m", "-1м", "-1m");
        Add("Pomodoro.Plus1m", "+1м", "+1m");
        Add("Pomodoro.Plus5m", "+5м", "+5m");
        Add("Pomodoro.Plus10m", "+10м", "+10m");


        // 9. Sticky Notes Widget
        Add("StickyNotes.Title", "Настройки Заметок & Задач", "Sticky Notes & Tasks Settings");
        Add("StickyNotes.HeaderTitle", "Заметки и задачи", "Notes & Tasks");
        Add("StickyNotes.Header", "📌 Управление списком задач", "📌 Task List Management");
        Add("StickyNotes.HeaderSub", "Быстрые действия с чек-листом, стили и фильтры", "Checklist actions, styles and filters");
        Add("StickyNotes.BtnClearDone", "✓ Удалить все выполненные задачи", "✓ Delete all completed tasks");
        Add("StickyNotes.Cleared", "Выполненные задачи очищены", "Completed tasks cleared");
        Add("StickyNotes.Placeholder", "Новая задача или заметка...", "New task or note...");
        Add("StickyNotes.ActiveCount", "{0} активных", "{0} active");
        Add("StickyNotes.AccentColor", "Цвет фона заметки:", "Note Background Accent:");
        Add("StickyNotes.ColorAmber", "Классический янтарный", "Classic Amber");
        Add("StickyNotes.ColorSky", "Небесно-голубой", "Sky Blue");
        Add("StickyNotes.ColorEmerald", "Изумрудный", "Emerald Green");
        Add("StickyNotes.ColorViolet", "Фиолетовый", "Violet");
        Add("StickyNotes.ColorSlate", "Тёмный грифель", "Minimalist Slate");
        Add("StickyNotes.BtnAddSamples", "+ Добавить примеры задач", "+ Add Sample Tasks");
        Add("StickyNotes.EmptyNoTasks", "Задач пока нет. Добавьте первую задачу!", "No tasks yet. Add your first task!");
        Add("StickyNotes.EmptyAllDone", "Все задачи выполнены! Отличная работа ✨", "All tasks completed! Great job ✨");
        Add("StickyNotes.EmptyState", "Все задачи выполнены! Отличная работа ✨", "All tasks completed! Great job ✨");

        // 10. Quick Dock Widget
        Add("QuickDock.Title", "Настройки Панели Дока", "Quick Dock Settings");
        Add("QuickDock.Header", "⚡ Панель быстрого запуска", "⚡ Quick Dock Launcher");
        Add("QuickDock.HeaderSub", "Ярлыки приложений, терминала и утилит рабочего стола", "Application shortcuts, terminal and desktop utilities");
        Add("QuickDock.IconSize", "Размер иконок дока:", "Dock Icon Size:");
        Add("QuickDock.SizeSmall", "Компактный (32 px)", "Compact (32 px)");
        Add("QuickDock.SizeMedium", "Стандартный (40 px)", "Standard (40 px)");
        Add("QuickDock.SizeLarge", "Большой (48 px)", "Large (48 px)");
        Add("QuickDock.Explorer", "Проводник", "File Explorer");
        Add("QuickDock.Terminal", "Терминал", "PowerShell / Terminal");
        Add("QuickDock.Browser", "Браузер", "Web Browser");
        Add("QuickDock.Notepad", "Блокнот", "Notepad");
        Add("QuickDock.Calc", "Калькулятор", "Calculator");
        Add("QuickDock.TaskMgr", "Диспетчер задач", "Task Manager");
        Add("QuickDock.CustomTitle", "Добавить свой ярлык:", "Add Custom Shortcut:");
        Add("QuickDock.NamePlaceholder", "Название приложения", "App Name");
        Add("QuickDock.IconPlaceholder", "Иконка / Emoji (напр. 🚀)", "Icon / Emoji (e.g. 🚀)");
        Add("QuickDock.PathPlaceholder", "Путь к файлу / URL (напр. https://...)", "File path / URL (e.g. https://...)");
        Add("QuickDock.BrowseFile", "📁 Обзор...", "📁 Browse...");
        Add("QuickDock.BrowseIcon", "🖼️ Обзор...", "🖼️ Browse...");
        Add("QuickDock.SelectFileTitle", "Выберите исполняемый файл или документ", "Select executable file or document");
        Add("QuickDock.SelectIconTitle", "Выберите иконку или изображение", "Select icon or image");
        Add("QuickDock.FileFilter", "Приложения и файлы (*.exe;*.lnk;*.bat;*.cmd;*.*)|*.exe;*.lnk;*.bat;*.cmd;*.*|Приложения (*.exe)|*.exe|Все файлы (*.*)|*.*", "Applications and files (*.exe;*.lnk;*.bat;*.cmd;*.*)|*.exe;*.lnk;*.bat;*.cmd;*.*|Applications (*.exe)|*.exe|All files (*.*)|*.*");
        Add("QuickDock.IconFilter", "Иконки и изображения (*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.exe)|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.exe|Иконки (*.ico)|*.ico|Изображения (*.png;*.jpg)|*.png;*.jpg|Приложения (*.exe)|*.exe|Все файлы (*.*)|*.*", "Icons and images (*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.exe)|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.exe|Icons (*.ico)|*.ico|Images (*.png;*.jpg)|*.png;*.jpg|Applications (*.exe)|*.exe|All files (*.*)|*.*");
        Add("QuickDock.CurrentApps", "Текущие приложения в Доке:", "Current Apps in Dock:");
        Add("QuickDock.DeleteApp", "Удалить", "Delete");
        Add("QuickDock.BtnAdd", "+ Добавить в Док", "+ Add to Dock");
        Add("QuickDock.BtnReset", "Сбросить к стандартным", "Reset to Defaults");

        // 11. System Gauges Widget
        Add("SystemGauges.Title", "Настройки Датчиков Ресурсов", "System Gauges Settings");
        Add("SystemGauges.Header", "⚡ Формат и сенсоры системы", "⚡ Format & System Sensors");
        Add("SystemGauges.HeaderSub", "Настройка визуального представления сенсоров (полоски / круги / SSD / GPU)", "Configure sensors layout (bars / circular / SSD / GPU)");
        Add("SystemGauges.BtnConstructor", "🎨 Открыть визуальный конструктор сенсоров...", "🎨 Open Visual Gauges Constructor...");
        Add("SystemGauges.BtnConstructorHint", "Индивидуальная сортировка, выбор шкал и цвета сенсоров", "Reorder metrics, toggle styles, and customize colors");
        
        // Settings Card Labels
        Add("SystemGauges.SensorsSection", "Выбор активных сенсоров:", "Active Sensors Selection:");
        Add("SystemGauges.StyleSection", "Стиль отображения шкал:", "Gauge Display Style:");
        Add("SystemGauges.RefreshSection", "Частота обновления данных:", "Sensor Refresh Rate:");
        Add("SystemGauges.TempUnitSection", "Единицы измерения температуры:", "Temperature Unit:");

        // Sensor Labels (Short & Full)
        Add("SystemGauges.Cpu", "Процессор", "Processor");
        Add("SystemGauges.CpuFull", "⚡ Процессор (CPU)", "⚡ Processor (CPU)");
        Add("SystemGauges.Ram", "Память", "Memory");
        Add("SystemGauges.RamFull", "🧠 Память (RAM)", "🧠 Memory (RAM)");
        Add("SystemGauges.Disk", "Диск", "Storage");
        Add("SystemGauges.DiskFull", "💾 Диск / SSD (C:)", "💾 Storage / SSD (C:)");
        Add("SystemGauges.Gpu", "Видеокарта", "Graphics");
        Add("SystemGauges.GpuFull", "🎮 Видеокарта (GPU)", "🎮 Graphics (GPU)");
        Add("SystemGauges.Net", "Сеть", "Network");
        Add("SystemGauges.NetFull", "🌐 Сеть (Скорость)", "🌐 Network (Speed)");

        // Gauge Style Options
        Add("SystemGauges.StyleBars", "Линейные полоски (Bars)", "Linear Bars");
        Add("SystemGauges.StyleCircles", "Круговые диаграммы (Rings)", "Circular Rings");
        Add("SystemGauges.StyleCombined", "Комбинированный (Выбор в Конструкторе)", "Combined (Custom in Constructor)");
        Add("SystemGauges.CircleColumnsTitle", "Расположение круговых диаграмм:", "Circular Gauges Layout:");
        Add("SystemGauges.CircleColumns2", "2 в один ряд (Сетка 2x2)", "2 in a row (2-Column Grid)");
        Add("SystemGauges.CircleColumns1", "1 в ряд (Список)", "1 per row (Single Column)");

        // Refresh Rate Options
        Add("SystemGauges.Refresh500ms", "500 мс (Быстро, плавные графики)", "500 ms (Fast, smooth charts)");
        Add("SystemGauges.Refresh1s", "1 сек (Рекомендуется)", "1 sec (Recommended)");
        Add("SystemGauges.Refresh2s", "2 сек (Энергосбережение)", "2 sec (Power Saver)");

        // Temperature Unit Options
        Add("SystemGauges.TempCelsius", "Градусы Цельсия (°C)", "Celsius (°C)");
        Add("SystemGauges.TempFahrenheit", "Градусы Фаренгейта (°F)", "Fahrenheit (°F)");

        // Metrics View & Constructor UI
        Add("SystemGauges.HeaderTitle", "СИСТЕМА", "SYSTEM");
        Add("SystemGauges.CpuLoad", "Загрузка:", "Load:");
        Add("SystemGauges.DiskUsed", "Занято:", "Used:");
        Add("SystemGauges.GbUnit", "ГБ", "GB");
        Add("SystemGauges.SpeedMbs", "МБ/с", "MB/s");
        Add("SystemGauges.SpeedKbs", "КБ/с", "KB/s");
        Add("SystemGauges.EmptyNotice", "Все датчики отключены.\nОткройте Конструктор для включения.", "All sensors are disabled.\nOpen Constructor to enable.");

        Add("SystemGauges.ConstructorTitle", "Конструктор формата датчиков", "Gauges Layout Constructor");
        Add("SystemGauges.ConstructorDesc", "Настройте элементы, их порядок и вид отображения:", "Configure metrics, reorder, and choose display format:");
        Add("SystemGauges.ConstructorHint", "Вы можете перемещать метрики кнопками ▲ и ▼, включать GPU и выбирать между полоской и круговой диаграммой.", "You can reorder metrics using ▲ and ▼ buttons, toggle GPU, and switch between bars and circular rings.");
        Add("SystemGauges.FormatBar", "▬ Полоска", "▬ Bar");
        Add("SystemGauges.FormatCircle", "◯ Круг", "◯ Circle");

        // 12. GPU Monitor Widget
        Add("GpuMonitor.Title", "Настройки GPU Монитора", "GPU Monitor Settings");
        Add("GpuMonitor.Header", "🎮 Настройки датчиков видеокарты", "🎮 Graphics Card Sensors");
        Add("GpuMonitor.HeaderSub", "Мониторинг DirectX 3D Engine, VRAM и температуры", "Monitoring DirectX 3D Engine, VRAM and temperature");
        Add("GpuMonitor.InfoDesc", "Данные считываются в реальном времени с аппаратных счетчиков производительности Windows DWM.", "Data collected in real-time from Windows DWM hardware performance counters.");
        Add("GpuMonitor.WidgetName", "GPU Монитор", "GPU Monitor");
        Add("GpuMonitor.CoreLoad", "Нагрузка ядра", "Core Load");
        Add("GpuMonitor.VideoRam", "Видеопамять", "Video RAM");
        Add("GpuMonitor.Vram", "Видеопамять", "Video RAM");
        Add("GpuMonitor.Temperature", "Температура", "Temperature");
        Add("GpuMonitor.Temp", "Температура", "Temperature");
        Add("GpuMonitor.HardwareDwm", "Аппаратные счетчики DWM", "Hardware counters via Windows DWM");
        Add("GpuMonitor.GaugeStyle", "Стиль отображения датчиков:", "Sensor Display Style:");
        Add("GpuMonitor.GaugeStyleBars", "Линейные полоски (Bars)", "Linear Bars");
        Add("GpuMonitor.GaugeStyleCircular", "Круговые диаграммы (Circular Rings)", "Circular Rings");
        Add("GpuMonitor.VramDisplayMode", "Режим отображения видеопамяти:", "VRAM Display Mode:");
        Add("GpuMonitor.VramGb", "В гигабайтах (ГБ) — e.g. 2.4 GB", "In Gigabytes (GB) — e.g. 2.4 GB");
        Add("GpuMonitor.VramPercent", "В процентах (%) — e.g. 45%", "In Percentage (%) — e.g. 45%");
        Add("GpuMonitor.VramBoth", "ГБ и процент (ГБ / %) — e.g. 2.4 GB (45%)", "GB and Percentage — e.g. 2.4 GB (45%)");
        Add("GpuMonitor.TempUnit", "Единицы измерения температуры:", "Temperature Unit:");
        Add("GpuMonitor.TempUnitCelsius", "Градусы Цельсия (°C)", "Celsius (°C)");
        Add("GpuMonitor.TempUnitFahrenheit", "Градусы Фаренгейта (°F)", "Fahrenheit (°F)");
        Add("GpuMonitor.AlertThreshold", "Порог тревоги температуры:", "Temperature Alert Threshold:");
        Add("GpuMonitor.AlertThresholdHint", "Подсвечивать сенсоры красным цветом при превышении порога или нагрузке ядра > 95%", "Highlight sensors red if temp exceeds threshold or core load > 95%");
        Add("GpuMonitor.Alert75", "75°C (Осторожный порог)", "75°C (Strict threshold)");
        Add("GpuMonitor.Alert80", "80°C (Рекомендуемый стандарт)", "80°C (Recommended default)");
        Add("GpuMonitor.Alert85", "85°C (Производительный режим)", "85°C (High performance)");
        Add("GpuMonitor.Alert90", "90°C (Экстремальный порог)", "90°C (Extreme threshold)");
        Add("GpuMonitor.AlertDisabled", "Отключить предупреждения", "Disable Alerts");
        Add("GpuMonitor.AdapterNameMode", "Режим имени видеокарты:", "GPU Name Display Mode:");
        Add("GpuMonitor.AdapterNameFull", "Полное имя DirectX адаптера", "Full DirectX adapter name");
        Add("GpuMonitor.AdapterNameShort", "Короткое имя GPU", "Short GPU name");
        Add("GpuMonitor.StatusNormal", "Сенсоры в норме", "Sensors normal");
        Add("GpuMonitor.StatusAlert", "Внимание: Перегрев / Высокая нагрузка!", "Warning: High Temp / High Load!");
        Add("GpuMonitor.MemoryActive", "Память активна", "Memory active");

        // 13. RSS News Reader Widget
        Add("Rss.Title", "Настройки Новостей", "News Reader Settings");
        Add("Rss.Header", "📰 Параметры новостной ленты", "📰 News Feed Options");
        Add("Rss.HeaderSub", "Выбор международных каналов, IT, технологий и скорости листания", "Select international feeds, tech, AI, and scroll interval");
        Add("Rss.FeedPreset", "Категория и источник новостей:", "Category & News Source:");
        Add("Rss.PresetWorld", "🌍 Мировые новости (BBC World / Reuters)", "🌍 World News (BBC World / Reuters)");
        Add("Rss.PresetUnRu", "🌐 Новости ООН на русском (UN News)", "🌐 UN News in Russian (Official UN)");
        Add("Rss.PresetRbc", "📰 РБК (Главные и мировые новости)", "📰 RBC News (Top Headlines)");
        Add("Rss.PresetKommersant", "💼 Коммерсантъ (Мир и бизнес)", "💼 Kommersant News (World & Business)");
        Add("Rss.PresetEuronews", "🇪🇺 Euronews на русском (Euronews RU)", "🇪🇺 Euronews in Russian");
        Add("Rss.PresetTech", "💻 Технологии и AI (Hacker News / TechCrunch)", "💻 Tech & AI (Hacker News / TechCrunch)");
        Add("Rss.PresetHabr", "🚀 Хабр и Разработка (Habr IT)", "🚀 Habr IT & Dev (Habr)");
        Add("Rss.PresetCrypto", "💎 Криптовалюты (CoinDesk)", "💎 Crypto News (CoinDesk)");
        Add("Rss.PresetScience", "🔬 Наука и Космос (NASA & Science)", "🔬 Science & Space (NASA)");
        Add("Rss.AutoSlide", "Интервал авто-переключения:", "Auto-Slide Interval:");
        Add("Rss.Slide8s", "8 секунд", "8 seconds");
        Add("Rss.Slide12s", "12 секунд (По умолчанию)", "12 seconds (Default)");
        Add("Rss.Slide20s", "20 секунд", "20 seconds");
        Add("Rss.Slide30s", "30 секунд", "30 seconds");
        Add("Rss.SlideManual", "Только вручную", "Manual only");
        Add("Rss.CustomUrl", "Пользовательский RSS URL:", "Custom RSS URL:");
        Add("Rss.ApplyUrl", "Применить URL", "Apply URL");
        Add("Rss.CategoryTech", "AI И ТЕХНОЛОГИИ", "TECH & AI");
        Add("Rss.CategoryCrypto", "КРИПТОВАЛЮТЫ", "CRYPTO");
        Add("Rss.CategoryUnRu", "НОВОСТИ ООН", "UN NEWS");
        Add("Rss.CategoryRbc", "РБК НОВОСТИ", "RBC NEWS");
        Add("Rss.CategoryKommersant", "КОММЕРСАНТЪ", "KOMMERSANT");
        Add("Rss.CategoryEuronews", "EURONEWS", "EURONEWS");
        Add("Rss.CategoryWorld", "МИРОВЫЕ НОВОСТИ", "WORLD NEWS");
    }
}
