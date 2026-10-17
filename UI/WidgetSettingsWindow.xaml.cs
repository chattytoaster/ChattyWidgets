using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ChattyWidgets.Core.Models;
using ChattyWidgets.Core.Services;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using Cursors = System.Windows.Input.Cursors;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using Image = System.Windows.Controls.Image;

namespace ChattyWidgets.UI;

public partial class WidgetSettingsWindow : Window
{
    private readonly string _baseWidgetId;
    private readonly string _instanceId;
    private readonly object? _viewModel;
    private readonly Action? _onSettingsChanged;

    public WidgetSettingsWindow(string baseWidgetId, string instanceId, object? viewModel, Action? onSettingsChanged = null)
    {
        InitializeComponent();
        _baseWidgetId = baseWidgetId;
        _instanceId = instanceId;
        _viewModel = viewModel;
        _onSettingsChanged = onSettingsChanged;

        BuildSettingsUI();
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged() => Dispatcher.Invoke(BuildSettingsUI);

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BuildSettingsUI()
    {
        SettingsPanel.Children.Clear();
        var loc = LocalizationService.Instance;
        Title = loc.T("SettingsWindow.Title", "Настройки виджета");
        if (TxtStatus != null) TxtStatus.Text = loc.T("SettingsWindow.Status", "Изменения применяются мгновенно");
        if (BtnDone != null) BtnDone.Content = loc.T("SettingsWindow.Done", "Готово");

        switch (_baseWidgetId)
        {
            case "com.chatty.weather-card":
                BuildWeatherSettings();
                break;
            case "com.chatty.clock-glass":
                BuildClockSettings();
                break;
            case "com.chatty.crypto-tracker":
                BuildCryptoSettings();
                break;
            case "com.chatty.pomodoro-timer":
                BuildPomodoroSettings();
                break;
            case "com.chatty.music-player":
                BuildMusicSettings();
                break;
            case "com.chatty.gpu-monitor":
                BuildGpuSettings();
                break;
            case "com.chatty.sticky-notes":
                BuildStickyNotesSettings();
                break;
            case "com.chatty.calendar-glass":
                BuildCalendarSettings();
                break;
            case "com.chatty.rss-reader":
                BuildRssSettings();
                break;
            case "com.chatty.media-frame":
                BuildMediaFrameSettings();
                break;
            case "com.chatty.system-gauges":
                BuildSystemGaugesSettings();
                break;
            case "com.chatty.quick-dock":
                BuildQuickDockSettings();
                break;
            default:
                BuildGenericSettings();
                break;
        }

        AddThemeSelectorCard();
    }

    private void AddThemeSelectorCard()
    {
        var loc = LocalizationService.Instance;
        var card = CreateCard();
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = loc.T("Settings.ThemeTitle", "🎨 Тема оформления"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            Margin = new Thickness(0, 0, 0, 4)
        });
        sp.Children.Add(new TextBlock
        {
            Text = loc.T("Settings.ThemeSubtitle", "Выберите тему для этого виджета или используйте глобальную настройку Хаба:"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        });

        var cb = new ComboBox
        {
            Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(22, 25, 40)),
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255))
        };
        cb.Items.Add(new ComboBoxItem { Content = loc.T("Settings.ThemeAuto", "✦ Как в Хабе (Автоматически)"), Tag = "auto" });
        cb.Items.Add(new ComboBoxItem { Content = loc.T("Settings.ThemeDark", "🌙 Тёмная тема (Dark)"), Tag = "dark" });
        cb.Items.Add(new ComboBoxItem { Content = loc.T("Settings.ThemeLight", "☀️ Светлая тема (Light)"), Tag = "light" });

        var win = WidgetManager.Instance.GetActiveWindow(_instanceId);
        string curTheme = win?.Settings.Theme ?? "auto";
        cb.SelectedIndex = curTheme switch
        {
            "dark" => 1,
            "light" => 2,
            _ => 0
        };

        cb.SelectionChanged += (s, e) =>
        {
            if (cb.SelectedItem is ComboBoxItem item && item.Tag is string t)
            {
                if (win != null)
                {
                    win.Settings.Theme = t;
                    win.ApplyTheme();
                    WidgetManager.Instance.SaveSettings();
                }
            }
        };

        sp.Children.Add(cb);
        card.Child = sp;
        SettingsPanel.Children.Add(card);
    }

    private void AddSectionHeader(string title, string subtitle)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        sp.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252))
        });
        sp.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            Margin = new Thickness(0, 3, 0, 0)
        });
        SettingsPanel.Children.Add(sp);
    }

    private Border CreateCard()
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(16, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 12)
        };
    }

    // 1. Weather
    private void BuildWeatherSettings()
    {
        var loc = LocalizationService.Instance;
        bool isRu = loc.IsRussian;
        TxtWindowTitle.Text = loc.T("Weather.Title", "Настройки Погоды");
        AddSectionHeader(loc.T("Weather.Header", "🌤 Настройки прогноза погоды"),
                         loc.T("Weather.HeaderSub", "Выберите город или страну для загрузки данных через Open-Meteo API"));

        var vm = _viewModel as WeatherWidgetViewModel;

        // CARD 1: Curated World Cities
        var cardCities = CreateCard();
        var spCities = new StackPanel();

        spCities.Children.Add(new TextBlock
        {
            Text = loc.T("Weather.QuickCity", "Выбор города мира:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var comboCities = new ComboBox
        {
            Height = 32,
            Margin = new Thickness(0, 0, 0, 6),
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White
        };

        var cityList = WeatherConstants.CuratedCities;
        int selectedIndex = 0;
        for (int i = 0; i < cityList.Count; i++)
        {
            var c = cityList[i];
            comboCities.Items.Add(c.DisplayName(isRu));
            if (vm != null && (vm.City.Equals(c.Name, StringComparison.OrdinalIgnoreCase) || vm.City.Equals(c.NameRu, StringComparison.OrdinalIgnoreCase)))
            {
                selectedIndex = i;
            }
        }
        comboCities.SelectedIndex = selectedIndex;

        comboCities.SelectionChanged += (s, e) =>
        {
            if (comboCities.SelectedIndex >= 0 && comboCities.SelectedIndex < cityList.Count && vm != null)
            {
                var sel = cityList[comboCities.SelectedIndex];
                string name = isRu ? sel.NameRu : sel.Name;
                vm.SetCity(name, sel.Lat, sel.Lon, sel.CountryCode, sel.Timezone);
                TxtStatus.Text = string.Format(loc.T("Weather.CityChanged", "Город изменен на {0}. Загрузка прогноза..."), name);
                _onSettingsChanged?.Invoke();
            }
        };

        spCities.Children.Add(comboCities);
        cardCities.Child = spCities;
        SettingsPanel.Children.Add(cardCities);

        // CARD 2: Online City Search (Open-Meteo Geocoding)
        var cardSearch = CreateCard();
        var spSearch = new StackPanel();

        spSearch.Children.Add(new TextBlock
        {
            Text = loc.T("Weather.SearchOnline", "Онлайн поиск любого города (Open-Meteo):"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var dockSearch = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var btnSearch = new Button
        {
            Content = loc.T("Weather.SearchBtn", "🔍 Найти"),
            Width = 84,
            Height = 30,
            Margin = new Thickness(6, 0, 0, 0),
            Background = new SolidColorBrush(Color.FromArgb(30, 56, 189, 248)),
            Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        DockPanel.SetDock(btnSearch, Dock.Right);

        var txtSearch = new TextBox
        {
            Height = 30,
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            Padding = new Thickness(6, 4, 6, 4),
            VerticalContentAlignment = VerticalAlignment.Center
        };

        dockSearch.Children.Add(btnSearch);
        dockSearch.Children.Add(txtSearch);
        spSearch.Children.Add(dockSearch);

        var comboResults = new ComboBox
        {
            Height = 30,
            Visibility = Visibility.Collapsed,
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White,
            Margin = new Thickness(0, 4, 0, 0)
        };

        var searchResults = new List<WorldCityInfo>();

        async void DoSearch()
        {
            string q = txtSearch.Text.Trim();
            if (string.IsNullOrWhiteSpace(q)) return;

            btnSearch.IsEnabled = false;
            TxtStatus.Text = loc.T("Weather.SearchOnline", "Поиск городов...");

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
                string lang = isRu ? "ru" : "en";
                string url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(q)}&count=6&language={lang}&format=json";
                string json = await client.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);

                searchResults.Clear();
                comboResults.Items.Clear();

                if (doc.RootElement.TryGetProperty("results", out var resArr))
                {
                    foreach (var item in resArr.EnumerateArray())
                    {
                        string name = item.GetProperty("name").GetString() ?? "";
                        string country = item.TryGetProperty("country", out var cElem) ? cElem.GetString() ?? "" : "";
                        string countryCode = item.TryGetProperty("country_code", out var ccElem) ? ccElem.GetString() ?? "" : "";
                        string tz = item.TryGetProperty("timezone", out var tzElem) ? tzElem.GetString() ?? "auto" : "auto";
                        double lat = item.GetProperty("latitude").GetDouble();
                        double lon = item.GetProperty("longitude").GetDouble();

                        var info = new WorldCityInfo
                        {
                            Name = name,
                            NameRu = isRu ? name : "",
                            Country = country,
                            CountryRu = isRu ? country : "",
                            CountryCode = countryCode,
                            Lat = lat,
                            Lon = lon,
                            Timezone = tz
                        };
                        searchResults.Add(info);
                        comboResults.Items.Add($"📍 {name}, {country} ({lat:F2}, {lon:F2})");
                    }
                }

                if (searchResults.Count > 0)
                {
                    comboResults.Visibility = Visibility.Visible;
                    comboResults.SelectedIndex = 0;
                    TxtStatus.Text = $"Найдено результатов: {searchResults.Count}";
                }
                else
                {
                    TxtStatus.Text = "Город не найден.";
                    comboResults.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
                TxtStatus.Text = "Ошибка сети при поиске города.";
            }
            finally
            {
                btnSearch.IsEnabled = true;
            }
        }

        btnSearch.Click += (s, e) => DoSearch();
        txtSearch.KeyDown += (s, e) => { if (e.Key == Key.Enter) DoSearch(); };

        comboResults.SelectionChanged += (s, e) =>
        {
            if (comboResults.SelectedIndex >= 0 && comboResults.SelectedIndex < searchResults.Count && vm != null)
            {
                var sel = searchResults[comboResults.SelectedIndex];
                vm.SetCity(sel.Name, sel.Lat, sel.Lon, sel.CountryCode, sel.Timezone);
                TxtStatus.Text = string.Format(loc.T("Weather.CityChanged", "Город изменен на {0}. Загрузка прогноза..."), sel.Name);
                _onSettingsChanged?.Invoke();
            }
        };

        spSearch.Children.Add(comboResults);
        cardSearch.Child = spSearch;
        SettingsPanel.Children.Add(cardSearch);

        // CARD 3: Manual Lat / Lon Input
        var cardCoords = CreateCard();
        var spCoords = new StackPanel();

        spCoords.Children.Add(new TextBlock
        {
            Text = loc.T("Weather.ManualCoords", "Точные координаты (Широта / Долгота):"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var gridCoords = new Grid();
        gridCoords.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gridCoords.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gridCoords.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var txtLat = new TextBox
        {
            Text = vm?.Lat.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) ?? "55.7558",
            Height = 30,
            Margin = new Thickness(0, 0, 4, 0),
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(txtLat, 0);

        var txtLon = new TextBox
        {
            Text = vm?.Lon.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) ?? "37.6173",
            Height = 30,
            Margin = new Thickness(4, 0, 6, 0),
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(txtLon, 1);

        var btnApplyCoords = new Button
        {
            Content = loc.T("Weather.ApplyCoordsBtn", "Применить"),
            Width = 84,
            Height = 30,
            Background = new SolidColorBrush(Color.FromArgb(30, 56, 189, 248)),
            Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        Grid.SetColumn(btnApplyCoords, 2);

        btnApplyCoords.Click += (s, e) =>
        {
            if (double.TryParse(txtLat.Text.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var nLat) &&
                double.TryParse(txtLon.Text.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var nLon))
            {
                if (nLat >= -90.0 && nLat <= 90.0 && nLon >= -180.0 && nLon <= 180.0 && vm != null)
                {
                    vm.SetCity($"{nLat:F2}°, {nLon:F2}°", nLat, nLon, "Custom", "auto");
                    TxtStatus.Text = "Координаты применены успешно.";
                    _onSettingsChanged?.Invoke();
                    return;
                }
            }
            TxtStatus.Text = loc.T("Weather.InvalidCoords", "Неверные координаты.");
        };

        gridCoords.Children.Add(txtLat);
        gridCoords.Children.Add(txtLon);
        gridCoords.Children.Add(btnApplyCoords);
        spCoords.Children.Add(gridCoords);

        cardCoords.Child = spCoords;
        SettingsPanel.Children.Add(cardCoords);

        // CARD 4: Measurement Units (°C/°F, m/s/kmh/mph, mmHg/hPa)
        var cardUnits = CreateCard();
        var spUnits = new StackPanel();

        spUnits.Children.Add(new TextBlock
        {
            Text = loc.T("Weather.UnitsTitle", "Единицы измерения и форматы:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // 4.1 Temperature
        spUnits.Children.Add(new TextBlock { Text = loc.T("Weather.UnitTemp", "Температура:"), FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), Margin = new Thickness(0, 0, 0, 3) });
        var comboTemp = new ComboBox { Height = 28, Margin = new Thickness(0, 0, 0, 8), Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)), Foreground = Brushes.White };
        comboTemp.Items.Add(new ComboBoxItem { Content = loc.T("Weather.UnitCelsius", "Градусы Цельсия (°C)"), Tag = "C" });
        comboTemp.Items.Add(new ComboBoxItem { Content = loc.T("Weather.UnitFahrenheit", "Градусы Фаренгейта (°F)"), Tag = "F" });
        comboTemp.SelectedIndex = vm?.UnitTemp == "F" ? 1 : 0;

        // 4.2 Wind
        spUnits.Children.Add(new TextBlock { Text = loc.T("Weather.UnitWind", "Скорость ветра:"), FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), Margin = new Thickness(0, 0, 0, 3) });
        var comboWind = new ComboBox { Height = 28, Margin = new Thickness(0, 0, 0, 8), Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)), Foreground = Brushes.White };
        comboWind.Items.Add(new ComboBoxItem { Content = loc.T("Weather.UnitMsLabel", "Метры в секунду (м/с)"), Tag = "ms" });
        comboWind.Items.Add(new ComboBoxItem { Content = loc.T("Weather.UnitKmhLabel", "Километры в час (км/ч)"), Tag = "kmh" });
        comboWind.Items.Add(new ComboBoxItem { Content = loc.T("Weather.UnitMphLabel", "Мили в час (mph)"), Tag = "mph" });
        comboWind.SelectedIndex = vm?.UnitWind switch { "kmh" => 1, "mph" => 2, _ => 0 };

        // 4.3 Pressure
        spUnits.Children.Add(new TextBlock { Text = loc.T("Weather.UnitPressure", "Атмосферное давление:"), FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), Margin = new Thickness(0, 0, 0, 3) });
        var comboPressure = new ComboBox { Height = 28, Margin = new Thickness(0, 0, 0, 8), Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)), Foreground = Brushes.White };
        comboPressure.Items.Add(new ComboBoxItem { Content = loc.T("Weather.UnitMmhgLabel", "Миллиметры рт. ст. (мм)"), Tag = "mmhg" });
        comboPressure.Items.Add(new ComboBoxItem { Content = loc.T("Weather.UnitHpaLabel", "Гектопаскали / мбар (гПа / hPa)"), Tag = "hpa" });
        comboPressure.SelectedIndex = vm?.UnitPressure == "hpa" ? 1 : 0;

        void OnUnitChanged()
        {
            if (vm == null) return;
            string t = (comboTemp.SelectedItem as ComboBoxItem)?.Tag as string ?? "C";
            string w = (comboWind.SelectedItem as ComboBoxItem)?.Tag as string ?? "ms";
            string p = (comboPressure.SelectedItem as ComboBoxItem)?.Tag as string ?? "mmhg";
            vm.SetUnits(t, w, p);
            _onSettingsChanged?.Invoke();
        }

        comboTemp.SelectionChanged += (s, e) => OnUnitChanged();
        comboWind.SelectionChanged += (s, e) => OnUnitChanged();
        comboPressure.SelectionChanged += (s, e) => OnUnitChanged();

        spUnits.Children.Add(comboTemp);
        spUnits.Children.Add(comboWind);
        spUnits.Children.Add(comboPressure);

        cardUnits.Child = spUnits;
        SettingsPanel.Children.Add(cardUnits);

        // CARD 5: Refresh Button
        var btnRefresh = new Button
        {
            Content = loc.T("Weather.RefreshBtn", "⟳ Обновить прогноз погоды сейчас"),
            Height = 34,
            Margin = new Thickness(0, 0, 0, 10),
            Background = new SolidColorBrush(Color.FromArgb(28, 56, 189, 248)),
            Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnRefresh.Click += async (s, e) =>
        {
            if (vm != null)
            {
                TxtStatus.Text = loc.T("Weather.SearchOnline", "Обновление погоды...");
                await vm.RefreshWeatherAsync();
                TxtStatus.Text = loc.T("Weather.Refreshed", "Погода успешно обновлена!");
                _onSettingsChanged?.Invoke();
            }
        };
        SettingsPanel.Children.Add(btnRefresh);
    }

    // 2. Clock
    private void BuildClockSettings()
    {
        var loc = LocalizationService.Instance;
        TxtWindowTitle.Text = loc.T("Clock.Title", "Настройки Стеклянных Часов");
        AddSectionHeader(loc.T("Clock.Header", "🕒 Настройки отображения времени"),
                         loc.T("Clock.HeaderSub", "Параметры часового пояса, формата времени и индикаторов"));

        var clockVm = _viewModel as ClockWidgetViewModel;

        // Card 1: Timezone / World City
        var cardTz = CreateCard();
        var spTz = new StackPanel();

        spTz.Children.Add(new TextBlock
        {
            Text = loc.T("Clock.Timezone", "Часовой пояс / Мировое время:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var comboTz = new ComboBox
        {
            Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White
        };

        var timezones = new (string Id, string City, string DisplayKey, string Fallback)[]
        {
            ("local", "", "Clock.TzLocal", "Текущее местное время (Система)"),
            ("UTC", "UTC", "Clock.TzUtc", "🌐 UTC / GMT (Всемирное время)"),
            ("Europe/London", "London", "Clock.TzLondon", "🇬🇧 Лондон (London, GMT/BST)"),
            ("Europe/Paris", "Paris", "Clock.TzParis", "🇫🇷 Париж / Берлин (CET/CEST)"),
            ("Europe/Moscow", "Moscow", "Clock.TzMoscow", "🇷🇺 Москва (MSK, UTC+3)"),
            ("Asia/Dubai", "Dubai", "Clock.TzDubai", "🇦🇪 Дубай (Dubai, UTC+4)"),
            ("Asia/Tokyo", "Tokyo", "Clock.TzTokyo", "🇯🇵 Токио (Tokyo, JST, UTC+9)"),
            ("Asia/Hong_Kong", "Hong Kong", "Clock.TzHongKong", "🇭🇰 Гонконг / Сингапур (UTC+8)"),
            ("Australia/Sydney", "Sydney", "Clock.TzSydney", "🇦🇺 Сидней (Sydney, AEST)"),
            ("America/New_York", "New York", "Clock.TzNewYork", "🇺🇸 Нью-Йорк (New York, EST)"),
            ("America/Los_Angeles", "Los Angeles", "Clock.TzLosAngeles", "🇺🇸 Лос-Анджелес (LA, PST)")
        };

        int selTzIdx = 0;
        string curTz = clockVm?.TimezoneId ?? "local";

        for (int i = 0; i < timezones.Length; i++)
        {
            var tz = timezones[i];
            comboTz.Items.Add(new ComboBoxItem
            {
                Content = loc.T(tz.DisplayKey, tz.Fallback),
                Tag = tz.Id
            });
            if (string.Equals(tz.Id, curTz, StringComparison.OrdinalIgnoreCase))
            {
                selTzIdx = i;
            }
        }
        comboTz.SelectedIndex = selTzIdx;

        comboTz.SelectionChanged += (s, e) =>
        {
            if (comboTz.SelectedIndex >= 0 && comboTz.SelectedIndex < timezones.Length && clockVm != null)
            {
                var sel = timezones[comboTz.SelectedIndex];
                clockVm.SetTimezone(sel.Id, sel.City);
                TxtStatus.Text = loc.T("Common.ChangesApplied", "Изменения применяются мгновенно");
                _onSettingsChanged?.Invoke();
            }
        };

        spTz.Children.Add(comboTz);
        cardTz.Child = spTz;
        SettingsPanel.Children.Add(cardTz);

        // Card 2: Formats & Toggles
        var card = CreateCard();
        var sp = new StackPanel();

        var cb24 = new CheckBox
        {
            Content = loc.T("Clock.24Hour", "24-часовой формат времени (14:30)"),
            Foreground = Brushes.White,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8),
            IsChecked = clockVm?.Is24Hour ?? true
        };

        var cbSec = new CheckBox
        {
            Content = loc.T("Clock.ShowSeconds", "Отображать секунды (:42)"),
            Foreground = Brushes.White,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8),
            IsChecked = clockVm?.ShowSeconds ?? true
        };

        var cbDay = new CheckBox
        {
            Content = loc.T("Clock.ShowDayName", "Отображать день недели и дату"),
            Foreground = Brushes.White,
            FontSize = 12,
            IsChecked = clockVm?.ShowDayName ?? true
        };

        if (clockVm != null)
        {
            cb24.Checked += (s, e) => { clockVm.Is24Hour = true; _onSettingsChanged?.Invoke(); };
            cb24.Unchecked += (s, e) => { clockVm.Is24Hour = false; _onSettingsChanged?.Invoke(); };

            cbSec.Checked += (s, e) => { clockVm.ShowSeconds = true; _onSettingsChanged?.Invoke(); };
            cbSec.Unchecked += (s, e) => { clockVm.ShowSeconds = false; _onSettingsChanged?.Invoke(); };

            cbDay.Checked += (s, e) => { clockVm.ShowDayName = true; _onSettingsChanged?.Invoke(); };
            cbDay.Unchecked += (s, e) => { clockVm.ShowDayName = false; _onSettingsChanged?.Invoke(); };
        }

        sp.Children.Add(cb24);
        sp.Children.Add(cbSec);
        sp.Children.Add(cbDay);

        card.Child = sp;
        SettingsPanel.Children.Add(card);
    }

    // 3. Crypto
    private void BuildCryptoSettings()
    {
        var loc = LocalizationService.Instance;
        bool isRu = loc.IsRussian;
        TxtWindowTitle.Text = loc.T("Crypto.Title", "Настройки Котировок & Валют");
        AddSectionHeader(loc.T("Crypto.Header", "💎 Котировки криптовалют и курсы валют"),
                         loc.T("Crypto.HeaderSub", "Актуальные цены Binance API, ЦБ РФ и мировые валюты"));

        var cryptoVm = _viewModel as CryptoWidgetViewModel;
        var win = WidgetManager.Instance.GetActiveWindow(_instanceId);

        // CARD 1: Base Currency Selector
        var cardBase = CreateCard();
        var spBase = new StackPanel();

        spBase.Children.Add(new TextBlock
        {
            Text = loc.T("Crypto.BaseCurrency", "Базовая валюта отображения:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 4)
        });
        spBase.Children.Add(new TextBlock
        {
            Text = loc.T("Crypto.BaseCurrencyHint", "Конвертировать криптовалюты и котировки в выбранную валюту"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            Margin = new Thickness(0, 0, 0, 8)
        });

        var comboBase = new ComboBox
        {
            Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White
        };

        var currencies = new[]
        {
            ("USD", "$ USD — Доллар США (US Dollar)"),
            ("EUR", "€ EUR — Евро (Euro)"),
            ("RUB", "₽ RUB — Российский рубль (Russian Ruble)"),
            ("GBP", "£ GBP — Британский фунт (British Pound)"),
            ("JPY", "¥ JPY — Японская иена (Japanese Yen)"),
            ("CNY", "¥ CNY — Китайский юань (Chinese Yuan)"),
            ("KZT", "₸ KZT — Казахстанский тенге (Kazakhstani Tenge)"),
            ("TRY", "₺ TRY — Турецкая лира (Turkish Lira)"),
            ("CHF", "Fr CHF — Швейцарский франк (Swiss Franc)"),
            ("CAD", "C$ CAD — Канадский доллар (Canadian Dollar)"),
            ("AUD", "A$ AUD — Австралийский доллар (Australian Dollar)")
        };

        foreach (var (code, display) in currencies)
        {
            comboBase.Items.Add(new ComboBoxItem { Content = display, Tag = code });
        }

        string currentBase = cryptoVm?.BaseCurrency ?? "USD";
        int selIdx = Array.FindIndex(currencies, c => string.Equals(c.Item1, currentBase, StringComparison.OrdinalIgnoreCase));
        comboBase.SelectedIndex = selIdx >= 0 ? selIdx : 0;

        comboBase.SelectionChanged += (s, e) =>
        {
            if (comboBase.SelectedItem is ComboBoxItem item && item.Tag is string curCode)
            {
                if (cryptoVm != null)
                {
                    cryptoVm.BaseCurrency = curCode;
                }
                if (win != null)
                {
                    win.Settings.CustomSettings["cryptoBaseCurrency"] = curCode;
                    WidgetManager.Instance.SaveSettings();
                }
                TxtStatus.Text = loc.T("Crypto.Refreshed", "Котировки успешно обновлены!");
                _onSettingsChanged?.Invoke();
            }
        };

        spBase.Children.Add(comboBase);
        cardBase.Child = spBase;
        SettingsPanel.Children.Add(cardBase);

        // CARD 2: Add Custom Currency or Crypto Pair / Ticker
        var cardAdd = CreateCard();
        var spAdd = new StackPanel();

        spAdd.Children.Add(new TextBlock
        {
            Text = loc.T("Crypto.AddCustomTitle", "➕ Добавить любую валютную пару или тикер:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 4)
        });
        spAdd.Children.Add(new TextBlock
        {
            Text = loc.T("Crypto.AddCustomHint", "Введите любой тикер криптовалюты (DOGE, AVAX, NEAR, SUI, PEPE...) или валютную пару (USD/CNY, KZT/RUB, EUR/USD, ETH/BTC):"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var dockAdd = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };

        var btnAddCustom = new Button
        {
            Content = "+ " + loc.T("Crypto.AddBtn", "Добавить"),
            Height = 32,
            Padding = new Thickness(14, 0, 14, 0),
            Background = new SolidColorBrush(Color.FromRgb(14, 165, 233)), // Sky blue
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Margin = new Thickness(8, 0, 0, 0)
        };
        DockPanel.SetDock(btnAddCustom, Dock.Right);

        var txtCustomInput = new TextBox
        {
            Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            Padding = new Thickness(8, 4, 8, 4),
            VerticalContentAlignment = VerticalAlignment.Center
        };

        Action? refreshCatalogUI = null;

        Action doAddCustom = () =>
        {
            string raw = txtCustomInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(raw)) return;

            string norm = CryptoWidgetViewModel.NormalizeKey(raw);
            if (cryptoVm != null)
            {
                cryptoVm.AddCustomToken(raw);
                if (win != null)
                {
                    win.Settings.CustomSettings["cryptoEnabledTokens"] = string.Join(",", cryptoVm.EnabledTokens);
                    win.Settings.CustomSettings["cryptoCustomTokens"] = string.Join(",", cryptoVm.CustomTokens);
                    WidgetManager.Instance.SaveSettings();
                }
                TxtStatus.Text = string.Format(loc.T("Crypto.AddedSuccess", "Тикер/пара '{0}' успешно добавлен(а)!"), norm);
                txtCustomInput.Text = "";
                refreshCatalogUI?.Invoke();
                _onSettingsChanged?.Invoke();
            }
        };

        btnAddCustom.Click += (s, e) => doAddCustom();
        txtCustomInput.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                doAddCustom();
            }
        };

        dockAdd.Children.Add(btnAddCustom);
        dockAdd.Children.Add(txtCustomInput);
        spAdd.Children.Add(dockAdd);

        cardAdd.Child = spAdd;
        SettingsPanel.Children.Add(cardAdd);

        // CARD 3: Assets Catalog & Toggles with Search & Categories
        var cardAssets = CreateCard();
        var spAssets = new StackPanel();

        spAssets.Children.Add(new TextBlock
        {
            Text = loc.T("Crypto.CatalogTitle", "Каталог активов и отслеживаемые пары:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        // Search Filter Box
        var txtSearchFilter = new TextBox
        {
            Height = 30,
            Background = new SolidColorBrush(Color.FromRgb(22, 25, 40)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            Padding = new Thickness(8, 4, 8, 4),
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        };

        // Category Filter Chips
        string currentCategory = "All"; // "All", "Active", "Crypto", "Fiat"
        var spCategories = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

        var catButtons = new List<(string Cat, Button Btn)>();
        var categories = new[]
        {
            ("All", loc.T("Crypto.CatAll", "Все")),
            ("Active", loc.T("Crypto.CatActive", "Отслеживаемые")),
            ("Crypto", loc.T("Crypto.CatCrypto", "Криптовалюты")),
            ("Fiat", loc.T("Crypto.CatFiat", "Фиат & Форекс"))
        };

        foreach (var (catKey, catLabel) in categories)
        {
            var btnCat = new Button
            {
                Content = catLabel,
                Height = 24,
                Padding = new Thickness(10, 0, 10, 0),
                Background = catKey == currentCategory ? new SolidColorBrush(Color.FromRgb(14, 165, 233)) : new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)),
                Foreground = Brushes.White,
                FontSize = 10.5,
                FontWeight = catKey == currentCategory ? FontWeights.Bold : FontWeights.Normal,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0)
            };
            string localCat = catKey;
            btnCat.Click += (s, e) =>
            {
                currentCategory = localCat;
                foreach (var (k, b) in catButtons)
                {
                    b.Background = k == currentCategory ? new SolidColorBrush(Color.FromRgb(14, 165, 233)) : new SolidColorBrush(Color.FromArgb(25, 255, 255, 255));
                    b.FontWeight = k == currentCategory ? FontWeights.Bold : FontWeights.Normal;
                }
                refreshCatalogUI?.Invoke();
            };
            catButtons.Add((catKey, btnCat));
            spCategories.Children.Add(btnCat);
        }

        spAssets.Children.Add(txtSearchFilter);
        spAssets.Children.Add(spCategories);

        // Container for Asset Items
        var scrollContainer = new ScrollViewer
        {
            MaxHeight = 280,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 0, 0, 10)
        };
        var pnlAssetItems = new StackPanel();
        scrollContainer.Content = pnlAssetItems;
        spAssets.Children.Add(scrollContainer);

        refreshCatalogUI = () =>
        {
            pnlAssetItems.Children.Clear();
            if (cryptoVm == null) return;

            string search = txtSearchFilter.Text.Trim().ToUpperInvariant();
            var catalog = cryptoVm.GetCatalog();

            var filtered = catalog.Where(item =>
            {
                if (currentCategory == "Active" && !cryptoVm.IsTokenEnabled(item.Key)) return false;
                if (currentCategory == "Crypto" && item.Category != "Crypto") return false;
                if (currentCategory == "Fiat" && item.Category != "Fiat") return false;

                if (!string.IsNullOrEmpty(search))
                {
                    bool matchKey = item.Key.ToUpperInvariant().Contains(search);
                    bool matchRu = item.NameRu.ToUpperInvariant().Contains(search);
                    bool matchEn = item.NameEn.ToUpperInvariant().Contains(search);
                    bool matchSym = item.Symbol.ToUpperInvariant().Contains(search);
                    if (!matchKey && !matchRu && !matchEn && !matchSym) return false;
                }
                return true;
            }).ToList();

            if (filtered.Count == 0)
            {
                pnlAssetItems.Children.Add(new TextBlock
                {
                    Text = loc.T("Crypto.NoMatches", "Ничего не найдено. Нажмите 'Добавить' выше для создания новой пары."),
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    FontSize = 11,
                    Margin = new Thickness(4, 8, 4, 8)
                });
                return;
            }

            var gridTokens = new Grid();
            gridTokens.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridTokens.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < filtered.Count; i++)
            {
                if (i % 2 == 0)
                    gridTokens.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var def = filtered[i];
                bool isChecked = cryptoVm.IsTokenEnabled(def.Key);
                bool isCustom = cryptoVm.IsCustomToken(def.Key);

                var dockRow = new DockPanel { Margin = new Thickness(3, 3, 3, 3) };

                if (isCustom)
                {
                    var btnDelete = new Button
                    {
                        Content = "✕",
                        Width = 18,
                        Height = 18,
                        Background = Brushes.Transparent,
                        Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                        BorderThickness = new Thickness(0),
                        Cursor = Cursors.Hand,
                        ToolTip = loc.T("Crypto.DeleteCustom", "Удалить пользовательский тикер"),
                        Margin = new Thickness(4, 0, 0, 0),
                        FontSize = 9.5
                    };
                    string delKey = def.Key;
                    btnDelete.Click += (s, e) =>
                    {
                        cryptoVm.RemoveCustomToken(delKey);
                        if (win != null)
                        {
                            win.Settings.CustomSettings["cryptoEnabledTokens"] = string.Join(",", cryptoVm.EnabledTokens);
                            win.Settings.CustomSettings["cryptoCustomTokens"] = string.Join(",", cryptoVm.CustomTokens);
                            WidgetManager.Instance.SaveSettings();
                        }
                        refreshCatalogUI?.Invoke();
                        _onSettingsChanged?.Invoke();
                    };
                    DockPanel.SetDock(btnDelete, Dock.Right);
                    dockRow.Children.Add(btnDelete);
                }

                string displayName = isRu ? def.NameRu : def.NameEn;
                string labelText = $"{def.Icon}  {def.Symbol}" + (displayName != def.Symbol ? $" ({displayName})" : "");

                var cb = new CheckBox
                {
                    Content = labelText,
                    Foreground = Brushes.White,
                    FontSize = 11.5,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    IsChecked = isChecked,
                    Tag = def.Key
                };

                cb.Checked += (s, e) =>
                {
                    cryptoVm.SetTokenEnabled(def.Key, true);
                    if (win != null)
                    {
                        win.Settings.CustomSettings["cryptoEnabledTokens"] = string.Join(",", cryptoVm.EnabledTokens);
                        WidgetManager.Instance.SaveSettings();
                    }
                    _onSettingsChanged?.Invoke();
                };

                cb.Unchecked += (s, e) =>
                {
                    cryptoVm.SetTokenEnabled(def.Key, false);
                    if (win != null)
                    {
                        win.Settings.CustomSettings["cryptoEnabledTokens"] = string.Join(",", cryptoVm.EnabledTokens);
                        WidgetManager.Instance.SaveSettings();
                    }
                    _onSettingsChanged?.Invoke();
                };

                dockRow.Children.Add(cb);

                Grid.SetRow(dockRow, i / 2);
                Grid.SetColumn(dockRow, i % 2);
                gridTokens.Children.Add(dockRow);
            }

            pnlAssetItems.Children.Add(gridTokens);
        };

        txtSearchFilter.TextChanged += (s, e) => refreshCatalogUI();

        // Quick Actions Row
        var spQuick = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
        var btnSelectAll = new Button
        {
            Content = loc.T("Crypto.SelectAll", "Выбрать все"),
            Height = 26,
            Padding = new Thickness(10, 0, 10, 0),
            Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            Foreground = Brushes.White,
            FontSize = 11,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 6, 0)
        };
        btnSelectAll.Click += (s, e) =>
        {
            if (cryptoVm != null)
            {
                cryptoVm.SelectAllTokens(cryptoVm.GetCatalog().Select(d => d.Key));
                if (win != null)
                {
                    win.Settings.CustomSettings["cryptoEnabledTokens"] = string.Join(",", cryptoVm.EnabledTokens);
                    WidgetManager.Instance.SaveSettings();
                }
                refreshCatalogUI();
                _onSettingsChanged?.Invoke();
            }
        };

        var btnDeselectAll = new Button
        {
            Content = loc.T("Crypto.DeselectAll", "Снять все"),
            Height = 26,
            Padding = new Thickness(10, 0, 10, 0),
            Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            Foreground = Brushes.White,
            FontSize = 11,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 6, 0)
        };
        btnDeselectAll.Click += (s, e) =>
        {
            if (cryptoVm != null)
            {
                cryptoVm.DeselectAllTokens();
                if (win != null)
                {
                    win.Settings.CustomSettings["cryptoEnabledTokens"] = string.Join(",", cryptoVm.EnabledTokens);
                    WidgetManager.Instance.SaveSettings();
                }
                refreshCatalogUI();
                _onSettingsChanged?.Invoke();
            }
        };

        var btnReset = new Button
        {
            Content = loc.T("Crypto.ResetDefaults", "По умолчанию"),
            Height = 26,
            Padding = new Thickness(10, 0, 10, 0),
            Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            Foreground = Brushes.White,
            FontSize = 11,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnReset.Click += (s, e) =>
        {
            if (cryptoVm != null)
            {
                cryptoVm.ResetToDefaults();
                if (win != null)
                {
                    win.Settings.CustomSettings["cryptoBaseCurrency"] = cryptoVm.BaseCurrency;
                    win.Settings.CustomSettings["cryptoRefreshInterval"] = cryptoVm.RefreshInterval;
                    win.Settings.CustomSettings["cryptoEnabledTokens"] = string.Join(",", cryptoVm.EnabledTokens);
                    WidgetManager.Instance.SaveSettings();
                }
                int defaultBaseIdx = Array.FindIndex(currencies, c => string.Equals(c.Item1, cryptoVm.BaseCurrency, StringComparison.OrdinalIgnoreCase));
                if (defaultBaseIdx >= 0) comboBase.SelectedIndex = defaultBaseIdx;
                refreshCatalogUI();
                _onSettingsChanged?.Invoke();
            }
        };

        spQuick.Children.Add(btnSelectAll);
        spQuick.Children.Add(btnDeselectAll);
        spQuick.Children.Add(btnReset);
        spAssets.Children.Add(spQuick);

        refreshCatalogUI();

        cardAssets.Child = spAssets;
        SettingsPanel.Children.Add(cardAssets);

        // CARD 4: Auto-refresh interval
        var cardInterval = CreateCard();
        var spInterval = new StackPanel();

        spInterval.Children.Add(new TextBlock
        {
            Text = loc.T("Crypto.RefreshInterval", "Интервал автообновления:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var comboInterval = new ComboBox
        {
            Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White
        };

        var intervals = new[]
        {
            ("15s", loc.T("Crypto.Interval15s", "15 секунд (Сверхбыстро)")),
            ("30s", loc.T("Crypto.Interval30s", "30 секунд (Быстро)")),
            ("60s", loc.T("Crypto.Interval60s", "60 секунд (По умолчанию)")),
            ("5m", loc.T("Crypto.Interval5m", "5 минут")),
            ("15m", loc.T("Crypto.Interval15m", "15 минут (Энергосбережение)"))
        };

        foreach (var (code, display) in intervals)
        {
            comboInterval.Items.Add(new ComboBoxItem { Content = display, Tag = code });
        }

        string curInterval = cryptoVm?.RefreshInterval ?? "60s";
        int intervalIdx = Array.FindIndex(intervals, iv => string.Equals(iv.Item1, curInterval, StringComparison.OrdinalIgnoreCase));
        comboInterval.SelectedIndex = intervalIdx >= 0 ? intervalIdx : 2;

        comboInterval.SelectionChanged += (s, e) =>
        {
            if (comboInterval.SelectedItem is ComboBoxItem item && item.Tag is string code)
            {
                if (cryptoVm != null)
                {
                    cryptoVm.RefreshInterval = code;
                }
                if (win != null)
                {
                    win.Settings.CustomSettings["cryptoRefreshInterval"] = code;
                    WidgetManager.Instance.SaveSettings();
                }
                _onSettingsChanged?.Invoke();
            }
        };

        spInterval.Children.Add(comboInterval);
        cardInterval.Child = spInterval;
        SettingsPanel.Children.Add(cardInterval);

        // CARD 5: Manual Refresh Action & Live Feed
        var cardAction = CreateCard();
        var spAction = new StackPanel();

        var btnRefresh = new Button
        {
            Content = loc.T("Crypto.RefreshBtn", "⟳ Обновить котировки с биржи сейчас"),
            Height = 34,
            Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnRefresh.Click += async (s, e) =>
        {
            if (cryptoVm != null)
            {
                TxtStatus.Text = loc.T("Crypto.Loading", "Загрузка котировок с Binance и ЦБ...");
                await cryptoVm.FetchPricesAsync();
                TxtStatus.Text = loc.T("Crypto.Refreshed", "Котировки успешно обновлены!");
                _onSettingsChanged?.Invoke();
            }
        };
        spAction.Children.Add(btnRefresh);

        cardAction.Child = spAction;
        SettingsPanel.Children.Add(cardAction);
    }

    // 4. Pomodoro
    private void BuildPomodoroSettings()
    {
        var loc = LocalizationService.Instance;
        TxtWindowTitle.Text = loc.T("Pomodoro.Title", "Настройки Pomodoro");
        AddSectionHeader(loc.T("Pomodoro.Header", "🍅 Параметры таймера фокуса"), 
                         loc.T("Pomodoro.HeaderSub", "Интервалы продуктивности, отдыха и звуковые оповещения"));

        // Current values
        int currentFocus = 25;
        int currentBreak = 5;
        int currentLongBreak = 15;
        int currentTimer = 15;
        bool currentAutoStart = false;
        bool currentSound = true;

        var win = WidgetManager.Instance.GetActiveWindow(_instanceId);
        var customSettings = win?.Settings.CustomSettings;

        if (customSettings != null)
        {
            if (customSettings.TryGetValue("pomoFocusMins", out var fVal) && int.TryParse(fVal, out var f))
                currentFocus = f;
            if (customSettings.TryGetValue("pomoBreakMins", out var bVal) && int.TryParse(bVal, out var b))
                currentBreak = b;
            if (customSettings.TryGetValue("pomoLongBreakMins", out var lbVal) && int.TryParse(lbVal, out var lb))
                currentLongBreak = lb;
            if (customSettings.TryGetValue("pomoTimerMins", out var tVal) && int.TryParse(tVal, out var t))
                currentTimer = t;
            if (customSettings.TryGetValue("pomoAutoStart", out var asVal) && bool.TryParse(asVal, out var a))
                currentAutoStart = a;
            if (customSettings.TryGetValue("pomoSound", out var sVal) && bool.TryParse(sVal, out var s))
                currentSound = s;
        }

        var pomodoroVm = _viewModel as PomodoroWidgetViewModel;
        if (pomodoroVm != null)
        {
            currentFocus = pomodoroVm.FocusMinutes;
            currentBreak = pomodoroVm.BreakMinutes;
            currentLongBreak = pomodoroVm.LongBreakMinutes;
            currentTimer = pomodoroVm.TimerMinutes;
            currentAutoStart = pomodoroVm.AutoStart;
            currentSound = pomodoroVm.SoundEnabled;
        }

        void SaveAndApply(int focus, int sBreak, int lBreak, int timer, bool auto, bool sound)
        {
            currentFocus = focus;
            currentBreak = sBreak;
            currentLongBreak = lBreak;
            currentTimer = timer;
            currentAutoStart = auto;
            currentSound = sound;

            if (win != null)
            {
                win.Settings.CustomSettings["pomoFocusMins"] = focus.ToString();
                win.Settings.CustomSettings["pomoBreakMins"] = sBreak.ToString();
                win.Settings.CustomSettings["pomoLongBreakMins"] = lBreak.ToString();
                win.Settings.CustomSettings["pomoTimerMins"] = timer.ToString();
                win.Settings.CustomSettings["pomoAutoStart"] = auto ? "true" : "false";
                win.Settings.CustomSettings["pomoSound"] = sound ? "true" : "false";
                WidgetManager.Instance.SaveSettings();
            }

            pomodoroVm?.ApplyCustomSettings(focus, sBreak, lBreak, timer, auto, sound);
            _onSettingsChanged?.Invoke();
        }

        // 1. Focus duration card
        var cardWork = CreateCard();
        var spWork = new StackPanel();

        spWork.Children.Add(new TextBlock
        {
            Text = loc.T("Pomodoro.WorkDuration", "Длительность интервала фокуса:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var comboWork = new ComboBox
        {
            Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White
        };

        var focusOptions = new (int Minutes, string Key, string DefaultLabel)[]
        {
            (15, "Pomodoro.Min15", "15 минут"),
            (20, "Pomodoro.Min20", "20 минут"),
            (25, "Pomodoro.Min25", "25 минут (Классика)"),
            (30, "Pomodoro.Min30", "30 минут"),
            (45, "Pomodoro.Min45", "45 минут (Учеба)"),
            (50, "Pomodoro.Min50", "50 минут"),
            (60, "Pomodoro.Min60", "60 минут (Глубокая работа)")
        };

        int selectedFocusIndex = 2; // Default 25
        for (int i = 0; i < focusOptions.Length; i++)
        {
            var opt = focusOptions[i];
            comboWork.Items.Add(new ComboBoxItem
            {
                Content = loc.T(opt.Key, opt.DefaultLabel),
                Tag = opt.Minutes
            });
            if (opt.Minutes == currentFocus)
                selectedFocusIndex = i;
        }
        comboWork.SelectedIndex = selectedFocusIndex;

        comboWork.SelectionChanged += (s, e) =>
        {
            if (comboWork.SelectedItem is ComboBoxItem item && item.Tag is int mins)
            {
                SaveAndApply(mins, currentBreak, currentLongBreak, currentTimer, currentAutoStart, currentSound);
            }
        };

        spWork.Children.Add(comboWork);
        cardWork.Child = spWork;
        SettingsPanel.Children.Add(cardWork);

        // 2. Breaks card (Short & Long break)
        var cardBreaks = CreateCard();
        var spBreaks = new StackPanel();

        // Short break
        spBreaks.Children.Add(new TextBlock
        {
            Text = loc.T("Pomodoro.BreakDuration", "Длительность короткого отдыха:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var comboShortBreak = new ComboBox
        {
            Height = 32,
            Margin = new Thickness(0, 0, 0, 12),
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White
        };

        var shortBreakOptions = new (int Minutes, string Key, string DefaultLabel)[]
        {
            (3, "Pomodoro.Break3m", "3 минуты"),
            (5, "Pomodoro.Break5m", "5 минут (Классика)"),
            (10, "Pomodoro.Break10m", "10 минут")
        };

        int selectedShortBreakIndex = 1; // Default 5
        for (int i = 0; i < shortBreakOptions.Length; i++)
        {
            var opt = shortBreakOptions[i];
            comboShortBreak.Items.Add(new ComboBoxItem
            {
                Content = loc.T(opt.Key, opt.DefaultLabel),
                Tag = opt.Minutes
            });
            if (opt.Minutes == currentBreak)
                selectedShortBreakIndex = i;
        }
        comboShortBreak.SelectedIndex = selectedShortBreakIndex;

        comboShortBreak.SelectionChanged += (s, e) =>
        {
            if (comboShortBreak.SelectedItem is ComboBoxItem item && item.Tag is int mins)
            {
                SaveAndApply(currentFocus, mins, currentLongBreak, currentTimer, currentAutoStart, currentSound);
            }
        };
        spBreaks.Children.Add(comboShortBreak);

        // Long break
        spBreaks.Children.Add(new TextBlock
        {
            Text = loc.T("Pomodoro.LongBreakDuration", "Длительность длинного отдыха:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var comboLongBreak = new ComboBox
        {
            Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White
        };

        var longBreakOptions = new (int Minutes, string Key, string DefaultLabel)[]
        {
            (15, "Pomodoro.LongBreak15m", "15 минут (Классика)"),
            (20, "Pomodoro.LongBreak20m", "20 минут"),
            (30, "Pomodoro.LongBreak30m", "30 минут")
        };

        int selectedLongBreakIndex = 0; // Default 15
        for (int i = 0; i < longBreakOptions.Length; i++)
        {
            var opt = longBreakOptions[i];
            comboLongBreak.Items.Add(new ComboBoxItem
            {
                Content = loc.T(opt.Key, opt.DefaultLabel),
                Tag = opt.Minutes
            });
            if (opt.Minutes == currentLongBreak)
                selectedLongBreakIndex = i;
        }
        comboLongBreak.SelectedIndex = selectedLongBreakIndex;

        comboLongBreak.SelectionChanged += (s, e) =>
        {
            if (comboLongBreak.SelectedItem is ComboBoxItem item && item.Tag is int mins)
            {
                SaveAndApply(currentFocus, currentBreak, mins, currentTimer, currentAutoStart, currentSound);
            }
        };
        spBreaks.Children.Add(comboLongBreak);

        cardBreaks.Child = spBreaks;
        SettingsPanel.Children.Add(cardBreaks);

        // 3. Regular Countdown Timer Card
        var cardTimer = CreateCard();
        var spTimer = new StackPanel();

        spTimer.Children.Add(new TextBlock
        {
            Text = loc.T("Pomodoro.TimerDuration", "Длительность обычного таймера:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var comboTimer = new ComboBox
        {
            Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White
        };

        var timerOptions = new (int Minutes, string Key, string DefaultLabel)[]
        {
            (1, "Pomodoro.Timer1m", "1 минута"),
            (3, "Pomodoro.Timer3m", "3 минуты"),
            (5, "Pomodoro.Timer5m", "5 минут"),
            (10, "Pomodoro.Timer10m", "10 минут (Классика)"),
            (15, "Pomodoro.Timer15m", "15 минут"),
            (20, "Pomodoro.Timer20m", "20 минут"),
            (30, "Pomodoro.Timer30m", "30 минут"),
            (45, "Pomodoro.Timer45m", "45 минут"),
            (60, "Pomodoro.Timer60m", "60 минут (1 час)")
        };

        int selectedTimerIndex = 4; // Default 15 min
        for (int i = 0; i < timerOptions.Length; i++)
        {
            var opt = timerOptions[i];
            comboTimer.Items.Add(new ComboBoxItem
            {
                Content = loc.T(opt.Key, opt.DefaultLabel),
                Tag = opt.Minutes
            });
            if (opt.Minutes == currentTimer)
                selectedTimerIndex = i;
        }
        comboTimer.SelectedIndex = selectedTimerIndex;

        comboTimer.SelectionChanged += (s, e) =>
        {
            if (comboTimer.SelectedItem is ComboBoxItem item && item.Tag is int mins)
            {
                SaveAndApply(currentFocus, currentBreak, currentLongBreak, mins, currentAutoStart, currentSound);
            }
        };

        spTimer.Children.Add(comboTimer);
        cardTimer.Child = spTimer;
        SettingsPanel.Children.Add(cardTimer);

        // 4. Automation and Sound Alert Card
        var cardOptions = CreateCard();
        var spOptions = new StackPanel();

        var cbAutoStart = new CheckBox
        {
            Content = loc.T("Pomodoro.AutoStart", "Авто-старт следующего интервала"),
            Foreground = Brushes.White,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 3),
            IsChecked = currentAutoStart
        };
        var txtAutoStartHint = new TextBlock
        {
            Text = loc.T("Pomodoro.AutoStartHint", "Автоматически запускать таймер при переходе между фокусом и отдыхом"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            Margin = new Thickness(20, 0, 0, 10),
            TextWrapping = TextWrapping.Wrap
        };

        var cbSound = new CheckBox
        {
            Content = loc.T("Pomodoro.SoundAlert", "Воспроизводить звуковой сигнал при завершении"),
            Foreground = Brushes.White,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 3),
            IsChecked = currentSound
        };
        var txtSoundHint = new TextBlock
        {
            Text = loc.T("Pomodoro.SoundAlertHint", "Воспроизводить системный сигнал при смене интервала"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            Margin = new Thickness(20, 0, 0, 4),
            TextWrapping = TextWrapping.Wrap
        };

        cbAutoStart.Checked += (s, e) => SaveAndApply(currentFocus, currentBreak, currentLongBreak, currentTimer, true, currentSound);
        cbAutoStart.Unchecked += (s, e) => SaveAndApply(currentFocus, currentBreak, currentLongBreak, currentTimer, false, currentSound);

        cbSound.Checked += (s, e) => SaveAndApply(currentFocus, currentBreak, currentLongBreak, currentTimer, currentAutoStart, true);
        cbSound.Unchecked += (s, e) => SaveAndApply(currentFocus, currentBreak, currentLongBreak, currentTimer, currentAutoStart, false);

        spOptions.Children.Add(cbAutoStart);
        spOptions.Children.Add(txtAutoStartHint);
        spOptions.Children.Add(cbSound);
        spOptions.Children.Add(txtSoundHint);

        cardOptions.Child = spOptions;
        SettingsPanel.Children.Add(cardOptions);
    }

    // 5. Music Player
    private void BuildMusicSettings()
    {
        var loc = LocalizationService.Instance;
        TxtWindowTitle.Text = loc.T("Music.Title", "Настройки Медиаплеера");
        AddSectionHeader(loc.T("Music.Header", "🎵 Параметры мини-плеера"),
                         loc.T("Music.HeaderSub", "Управление виниловым диском, источниками медиа и громкостью"));

        var musicVm = _viewModel as MusicPlayerWidgetViewModel;

        // Card 1: Vinyl Rotation
        var cardVinyl = CreateCard();
        var spVinyl = new StackPanel();

        spVinyl.Children.Add(new TextBlock
        {
            Text = loc.T("Music.VinylRotation", "Вращение винилового диска:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var comboSpeed = new ComboBox
        {
            Height = 32,
            Margin = new Thickness(0, 0, 0, 10),
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White
        };
        comboSpeed.Items.Add(new ComboBoxItem { Content = loc.T("Music.VinylNormal", "Обычная (33 об/мин)"), Tag = "normal" });
        comboSpeed.Items.Add(new ComboBoxItem { Content = loc.T("Music.VinylFast", "Быстрая (45 об/мин)"), Tag = "fast" });
        comboSpeed.Items.Add(new ComboBoxItem { Content = loc.T("Music.VinylSlow", "Плавная (Slow)"), Tag = "slow" });
        comboSpeed.Items.Add(new ComboBoxItem { Content = loc.T("Music.VinylDisabled", "Отключено (Статичный диск)"), Tag = "disabled" });

        comboSpeed.SelectedIndex = musicVm?.VinylSpeed switch
        {
            "fast" => 1,
            "slow" => 2,
            "disabled" => 3,
            _ => 0
        };

        comboSpeed.SelectionChanged += (s, e) =>
        {
            if (comboSpeed.SelectedItem is ComboBoxItem item && item.Tag is string sp && musicVm != null)
            {
                musicVm.VinylSpeed = sp;
                TxtStatus.Text = loc.T("Common.ChangesApplied", "Изменения применяются мгновенно");
                _onSettingsChanged?.Invoke();
            }
        };
        spVinyl.Children.Add(comboSpeed);

        var cbSpinOnly = new CheckBox
        {
            Content = loc.T("Music.VinylSpinOnlyPlaying", "Крутить диск только когда играет музыка"),
            Foreground = Brushes.White,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4),
            IsChecked = musicVm?.SpinOnlyPlaying ?? true
        };
        cbSpinOnly.Checked += (s, e) => { if (musicVm != null) musicVm.SpinOnlyPlaying = true; _onSettingsChanged?.Invoke(); };
        cbSpinOnly.Unchecked += (s, e) => { if (musicVm != null) musicVm.SpinOnlyPlaying = false; _onSettingsChanged?.Invoke(); };
        spVinyl.Children.Add(cbSpinOnly);

        spVinyl.Children.Add(new TextBlock
        {
            Text = loc.T("Music.VinylSpinOnlyPlayingHint", "Диск останавливается на паузе и плавно крутится при воспроизведении"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            Margin = new Thickness(22, 0, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });

        cardVinyl.Child = spVinyl;
        SettingsPanel.Children.Add(cardVinyl);

        // Card 2: Sources & Reconnect
        var card = CreateCard();
        var sp = new StackPanel();

        sp.Children.Add(new TextBlock
        {
            Text = loc.T("Music.SourceHint", "Плеер автоматически считывает активные треки из Spotify, Браузеров, Apple Music и плееров Windows."),
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var btnRefresh = new Button
        {
            Content = loc.T("Music.BtnReconnect", "⟳ Переподключиться к сессиям медиа"),
            Height = 32,
            Background = new SolidColorBrush(Color.FromArgb(28, 99, 102, 241)),
            Foreground = new SolidColorBrush(Color.FromRgb(129, 140, 248)),
            FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnRefresh.Click += (s, e) =>
        {
            MediaMonitorService.Instance.PlayPause();
            MediaMonitorService.Instance.PlayPause();
            TxtStatus.Text = loc.T("Music.Reconnected", "Сессии Windows Media обновлены");
            _onSettingsChanged?.Invoke();
        };
        sp.Children.Add(btnRefresh);

        card.Child = sp;
        SettingsPanel.Children.Add(card);
    }

    // 6. GPU
    private void BuildGpuSettings()
    {
        var loc = LocalizationService.Instance;
        TxtWindowTitle.Text = loc.T("GpuMonitor.Title", "Настройки GPU Монитора");
        AddSectionHeader(loc.T("GpuMonitor.Header", "🎮 Настройки датчиков видеокарты"), loc.T("GpuMonitor.HeaderSub", "Мониторинг DirectX 3D Engine, VRAM и температуры"));

        var gpuVm = _viewModel as GpuMonitorWidgetViewModel;

        // Card 1: Display Modes & Units
        var cardDisplay = CreateCard();
        var spDisplay = new StackPanel();

        // Gauge Display Style (Bars / Circular)
        spDisplay.Children.Add(new TextBlock
        {
            Text = loc.T("GpuMonitor.GaugeStyle", "Стиль отображения датчиков:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var comboGaugeStyle = new ComboBox
        {
            Height = 32,
            Margin = new Thickness(0, 0, 0, 12),
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White
        };
        comboGaugeStyle.Items.Add(new ComboBoxItem { Content = loc.T("GpuMonitor.GaugeStyleBars", "▬ Линейные полоски (Bars)"), Tag = "bars" });
        comboGaugeStyle.Items.Add(new ComboBoxItem { Content = loc.T("GpuMonitor.GaugeStyleCircular", "◯ Круговые диаграммы (Circular Rings)"), Tag = "circular" });
        comboGaugeStyle.SelectedIndex = (gpuVm?.GaugeStyle == "circular") ? 1 : 0;

        comboGaugeStyle.SelectionChanged += (s, e) =>
        {
            if (comboGaugeStyle.SelectedItem is ComboBoxItem item && item.Tag is string style)
            {
                gpuVm?.SetGaugeStyle(style);
                TxtStatus.Text = loc.T("Common.ChangesApplied", "Изменения применяются мгновенно");
                _onSettingsChanged?.Invoke();
            }
        };
        spDisplay.Children.Add(comboGaugeStyle);

        // VRAM Display Mode
        spDisplay.Children.Add(new TextBlock
        {
            Text = loc.T("GpuMonitor.VramDisplayMode", "Режим отображения видеопамяти:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var comboVram = new ComboBox
        {
            Height = 32,
            Margin = new Thickness(0, 0, 0, 12),
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White
        };
        comboVram.Items.Add(new ComboBoxItem { Content = loc.T("GpuMonitor.VramGb", "В гигабайтах (ГБ) — e.g. 2.4 GB"), Tag = "gb" });
        comboVram.Items.Add(new ComboBoxItem { Content = loc.T("GpuMonitor.VramPercent", "В процентах (%) — e.g. 45%"), Tag = "percent" });
        comboVram.Items.Add(new ComboBoxItem { Content = loc.T("GpuMonitor.VramBoth", "ГБ и процент (ГБ / %) — e.g. 2.4 GB (45%)"), Tag = "both" });

        string currentVramMode = gpuVm?.VramDisplayMode ?? "gb";
        comboVram.SelectedIndex = currentVramMode switch
        {
            "percent" => 1,
            "both" => 2,
            _ => 0
        };

        comboVram.SelectionChanged += (s, e) =>
        {
            if (comboVram.SelectedItem is ComboBoxItem item && item.Tag is string mode)
            {
                gpuVm?.SetVramDisplayMode(mode);
                TxtStatus.Text = loc.T("Common.ChangesApplied", "Изменения применяются мгновенно");
                _onSettingsChanged?.Invoke();
            }
        };
        spDisplay.Children.Add(comboVram);

        // Temperature Unit
        spDisplay.Children.Add(new TextBlock
        {
            Text = loc.T("GpuMonitor.TempUnit", "Единицы измерения температуры:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var comboTemp = new ComboBox
        {
            Height = 32,
            Margin = new Thickness(0, 0, 0, 12),
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White
        };
        comboTemp.Items.Add(new ComboBoxItem { Content = loc.T("GpuMonitor.TempUnitCelsius", "Градусы Цельсия (°C)"), Tag = "C" });
        comboTemp.Items.Add(new ComboBoxItem { Content = loc.T("GpuMonitor.TempUnitFahrenheit", "Градусы Фаренгейта (°F)"), Tag = "F" });

        string currentTempUnit = gpuVm?.TempUnit ?? "C";
        comboTemp.SelectedIndex = string.Equals(currentTempUnit, "F", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        comboTemp.SelectionChanged += (s, e) =>
        {
            if (comboTemp.SelectedItem is ComboBoxItem item && item.Tag is string unit)
            {
                gpuVm?.SetTempUnit(unit);
                TxtStatus.Text = loc.T("Common.ChangesApplied", "Изменения применяются мгновенно");
                _onSettingsChanged?.Invoke();
            }
        };
        spDisplay.Children.Add(comboTemp);

        // Adapter Name Mode
        spDisplay.Children.Add(new TextBlock
        {
            Text = loc.T("GpuMonitor.AdapterNameMode", "Режим имени видеокарты:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var comboName = new ComboBox
        {
            Height = 32,
            Margin = new Thickness(0, 0, 0, 4),
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White
        };
        comboName.Items.Add(new ComboBoxItem { Content = loc.T("GpuMonitor.AdapterNameFull", "Полное имя DirectX адаптера"), Tag = "full" });
        comboName.Items.Add(new ComboBoxItem { Content = loc.T("GpuMonitor.AdapterNameShort", "Короткое имя GPU"), Tag = "short" });

        string currentNameMode = gpuVm?.AdapterNameMode ?? "full";
        comboName.SelectedIndex = currentNameMode == "short" ? 1 : 0;

        comboName.SelectionChanged += (s, e) =>
        {
            if (comboName.SelectedItem is ComboBoxItem item && item.Tag is string mode)
            {
                gpuVm?.SetAdapterNameMode(mode);
                TxtStatus.Text = loc.T("Common.ChangesApplied", "Изменения применяются мгновенно");
                _onSettingsChanged?.Invoke();
            }
        };
        spDisplay.Children.Add(comboName);

        cardDisplay.Child = spDisplay;
        SettingsPanel.Children.Add(cardDisplay);

        // Card 2: Alert Thresholds
        var cardAlert = CreateCard();
        var spAlert = new StackPanel();

        spAlert.Children.Add(new TextBlock
        {
            Text = loc.T("GpuMonitor.AlertThreshold", "Порог тревоги температуры:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var comboAlert = new ComboBox
        {
            Height = 32,
            Margin = new Thickness(0, 0, 0, 8),
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White
        };
        comboAlert.Items.Add(new ComboBoxItem { Content = loc.T("GpuMonitor.Alert75", "75°C (Осторожный порог)"), Tag = 75.0 });
        comboAlert.Items.Add(new ComboBoxItem { Content = loc.T("GpuMonitor.Alert80", "80°C (Рекомендуемый стандарт)"), Tag = 80.0 });
        comboAlert.Items.Add(new ComboBoxItem { Content = loc.T("GpuMonitor.Alert85", "85°C (Производительный режим)"), Tag = 85.0 });
        comboAlert.Items.Add(new ComboBoxItem { Content = loc.T("GpuMonitor.Alert90", "90°C (Экстремальный порог)"), Tag = 90.0 });
        comboAlert.Items.Add(new ComboBoxItem { Content = loc.T("GpuMonitor.AlertDisabled", "Отключить предупреждения"), Tag = 999.0 });

        double currentAlert = gpuVm?.AlertThreshold ?? 80.0;
        comboAlert.SelectedIndex = currentAlert switch
        {
            <= 75.0 => 0,
            <= 80.0 => 1,
            <= 85.0 => 2,
            <= 90.0 => 3,
            _ => 4
        };

        comboAlert.SelectionChanged += (s, e) =>
        {
            if (comboAlert.SelectedItem is ComboBoxItem item && item.Tag is double thresh)
            {
                gpuVm?.SetAlertThreshold(thresh);
                TxtStatus.Text = loc.T("Common.ChangesApplied", "Изменения применяются мгновенно");
                _onSettingsChanged?.Invoke();
            }
        };
        spAlert.Children.Add(comboAlert);

        spAlert.Children.Add(new TextBlock
        {
            Text = loc.T("GpuMonitor.AlertThresholdHint", "Подсвечивать сенсоры красным цветом при превышении порога или нагрузке ядра > 95%"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            TextWrapping = TextWrapping.Wrap
        });

        cardAlert.Child = spAlert;
        SettingsPanel.Children.Add(cardAlert);

        // Card 3: Hardware DWM Info & Diagnostics
        var cardInfo = CreateCard();
        var spInfo = new StackPanel();

        spInfo.Children.Add(new TextBlock
        {
            Text = loc.T("GpuMonitor.InfoDesc", "Данные считываются в реальном времени с аппаратных счетчиков производительности Windows DWM."),
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        string detectedAdapter = SystemMonitorService.Instance.CurrentMetrics.GpuName;
        double detectedVram = SystemMonitorService.Instance.CurrentMetrics.TotalGpuMemoryGb;

        spInfo.Children.Add(new TextBlock
        {
            Text = $"📟 Адаптер: {detectedAdapter} ({detectedVram:0.1} GB)",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(129, 140, 248)),
            FontWeight = FontWeights.Medium
        });

        cardInfo.Child = spInfo;
        SettingsPanel.Children.Add(cardInfo);
    }

    // 7. Sticky Notes
    private void BuildStickyNotesSettings()
    {
        var loc = LocalizationService.Instance;
        TxtWindowTitle.Text = loc.T("StickyNotes.Title", "Настройки Заметок & Задач");
        AddSectionHeader(loc.T("StickyNotes.Header", "📌 Управление списком задач"),
                         loc.T("StickyNotes.HeaderSub", "Быстрые действия с чек-листом, стили и фильтры"));

        var notesVm = _viewModel as StickyNotesWidgetViewModel;

        // Card 1: Accent Color
        var cardColor = CreateCard();
        var spColor = new StackPanel();

        spColor.Children.Add(new TextBlock
        {
            Text = loc.T("StickyNotes.AccentColor", "Цвет фона заметки:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var comboColor = new ComboBox
        {
            Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White
        };
        comboColor.Items.Add(new ComboBoxItem { Content = "🟡 " + loc.T("StickyNotes.ColorAmber", "Классический янтарный"), Tag = "amber" });
        comboColor.Items.Add(new ComboBoxItem { Content = "🔵 " + loc.T("StickyNotes.ColorSky", "Небесно-голубой"), Tag = "sky" });
        comboColor.Items.Add(new ComboBoxItem { Content = "🟢 " + loc.T("StickyNotes.ColorEmerald", "Изумрудный"), Tag = "emerald" });
        comboColor.Items.Add(new ComboBoxItem { Content = "🟣 " + loc.T("StickyNotes.ColorViolet", "Фиолетовый"), Tag = "violet" });
        comboColor.Items.Add(new ComboBoxItem { Content = "⚫ " + loc.T("StickyNotes.ColorSlate", "Тёмный грифель"), Tag = "slate" });

        comboColor.SelectedIndex = notesVm?.AccentColor switch
        {
            "sky" => 1,
            "emerald" => 2,
            "violet" => 3,
            "slate" => 4,
            _ => 0
        };

        comboColor.SelectionChanged += (s, e) =>
        {
            if (comboColor.SelectedItem is ComboBoxItem item && item.Tag is string col && notesVm != null)
            {
                notesVm.AccentColor = col;
                TxtStatus.Text = loc.T("Common.ChangesApplied", "Изменения применяются мгновенно");
                _onSettingsChanged?.Invoke();
            }
        };
        spColor.Children.Add(comboColor);
        cardColor.Child = spColor;
        SettingsPanel.Children.Add(cardColor);

        // Card 2: Quick Actions
        var card = CreateCard();
        var sp = new StackPanel();

        var btnClearDone = new Button
        {
            Content = loc.T("StickyNotes.BtnClearDone", "✓ Удалить все выполненные задачи"),
            Height = 32,
            Background = new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)),
            Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 0, 8)
        };
        btnClearDone.Click += (s, e) =>
        {
            if (notesVm != null)
            {
                notesVm.ClearCompletedTasks();
                TxtStatus.Text = loc.T("StickyNotes.Cleared", "Выполненные задачи очищены");
                _onSettingsChanged?.Invoke();
            }
        };
        sp.Children.Add(btnClearDone);

        var btnAddSamples = new Button
        {
            Content = loc.T("StickyNotes.BtnAddSamples", "+ Добавить примеры задач"),
            Height = 32,
            Background = new SolidColorBrush(Color.FromArgb(25, 99, 102, 241)),
            Foreground = new SolidColorBrush(Color.FromRgb(129, 140, 248)),
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnAddSamples.Click += (s, e) =>
        {
            if (notesVm != null)
            {
                notesVm.AddSampleTasks();
                TxtStatus.Text = loc.T("Common.ChangesApplied", "Изменения применяются мгновенно");
                _onSettingsChanged?.Invoke();
            }
        };
        sp.Children.Add(btnAddSamples);

        card.Child = sp;
        SettingsPanel.Children.Add(card);
    }

    // 8. Calendar
    private void BuildCalendarSettings()
    {
        var loc = LocalizationService.Instance;
        TxtWindowTitle.Text = loc.T("Calendar.Title", "Настройки Календаря");
        AddSectionHeader(loc.T("Calendar.Header", "📅 Параметры стеклянного календаря"),
                         loc.T("Calendar.HeaderSub", "Первый день недели, подсветка дат и номера недель"));

        var win = WidgetManager.Instance.GetActiveWindow(_instanceId);
        var calVm = _viewModel as CalendarWidgetViewModel;

        // Card 1: First Day of Week
        var cardFirstDay = CreateCard();
        var spFirstDay = new StackPanel();

        spFirstDay.Children.Add(new TextBlock
        {
            Text = loc.T("Calendar.FirstDay", "Первый день недели:"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 12.5,
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            Margin = new Thickness(0, 0, 0, 4)
        });

        spFirstDay.Children.Add(new TextBlock
        {
            Text = loc.IsRussian
                ? "Понедельник принят в РФ, странах СНГ и Европе (ISO 8601). Воскресенье — в США, Канаде и Японии."
                : "Monday is standard in Europe & ISO 8601. Sunday is standard in the USA, Canada, and Japan.",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        });

        var comboFirstDay = new ComboBox
        {
            Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(22, 25, 40)),
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255))
        };
        comboFirstDay.Items.Add(new ComboBoxItem
        {
            Content = loc.T("Calendar.FirstDayMon", "Понедельник (Россия, Европа, ISO 8601)"),
            Tag = "monday"
        });
        comboFirstDay.Items.Add(new ComboBoxItem
        {
            Content = loc.T("Calendar.FirstDaySun", "Воскресенье (США, Канада, Япония)"),
            Tag = "sunday"
        });

        string currentFirstDay = "monday";
        if (win != null && win.Settings.CustomSettings.TryGetValue("calFirstDayOfWeek", out var savedFdw) && !string.IsNullOrEmpty(savedFdw))
        {
            currentFirstDay = savedFdw.ToLowerInvariant();
        }
        else if (calVm != null)
        {
            currentFirstDay = calVm.FirstDayOfWeek.ToLowerInvariant();
        }
        comboFirstDay.SelectedIndex = currentFirstDay == "sunday" ? 1 : 0;

        comboFirstDay.SelectionChanged += (s, e) =>
        {
            if (comboFirstDay.SelectedItem is ComboBoxItem item && item.Tag is string val)
            {
                if (win != null)
                {
                    win.Settings.CustomSettings["calFirstDayOfWeek"] = val;
                    WidgetManager.Instance.SaveSettings();
                }
                if (calVm != null)
                {
                    calVm.FirstDayOfWeek = val;
                }
                TxtStatus.Text = loc.T("Calendar.SettingsSaved", "Настройки календаря сохранены");
                _onSettingsChanged?.Invoke();
            }
        };

        spFirstDay.Children.Add(comboFirstDay);
        cardFirstDay.Child = spFirstDay;
        SettingsPanel.Children.Add(cardFirstDay);

        // Card 2: Display Options (Highlight Today, Show Week Numbers)
        var cardOptions = CreateCard();
        var spOptions = new StackPanel();

        spOptions.Children.Add(new TextBlock
        {
            Text = loc.IsRussian ? "Опции отображения:" : "Display Options:",
            FontWeight = FontWeights.SemiBold,
            FontSize = 12.5,
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            Margin = new Thickness(0, 0, 0, 8)
        });

        var cbHighlightToday = new CheckBox
        {
            Content = loc.T("Calendar.HighlightToday", "Выделять текущий день (Сегодня)"),
            Foreground = Brushes.White,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8),
            IsChecked = true
        };

        bool currentHighlight = true;
        if (win != null && win.Settings.CustomSettings.TryGetValue("calHighlightToday", out var savedHt) && bool.TryParse(savedHt, out var bHt))
        {
            currentHighlight = bHt;
        }
        else if (calVm != null)
        {
            currentHighlight = calVm.HighlightToday;
        }
        cbHighlightToday.IsChecked = currentHighlight;

        cbHighlightToday.Checked += (s, e) =>
        {
            if (win != null) { win.Settings.CustomSettings["calHighlightToday"] = "true"; WidgetManager.Instance.SaveSettings(); }
            if (calVm != null) calVm.HighlightToday = true;
            TxtStatus.Text = loc.T("Calendar.SettingsSaved", "Настройки календаря сохранены");
            _onSettingsChanged?.Invoke();
        };
        cbHighlightToday.Unchecked += (s, e) =>
        {
            if (win != null) { win.Settings.CustomSettings["calHighlightToday"] = "false"; WidgetManager.Instance.SaveSettings(); }
            if (calVm != null) calVm.HighlightToday = false;
            TxtStatus.Text = loc.T("Calendar.SettingsSaved", "Настройки календаря сохранены");
            _onSettingsChanged?.Invoke();
        };
        spOptions.Children.Add(cbHighlightToday);

        var cbWeekNumbers = new CheckBox
        {
            Content = loc.T("Calendar.ShowWeekNumbers", "Отображать номера недель (№ / Wk)"),
            Foreground = Brushes.White,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 4),
            IsChecked = false
        };

        bool currentWeekNumbers = false;
        if (win != null && win.Settings.CustomSettings.TryGetValue("calShowWeekNumbers", out var savedSwn) && bool.TryParse(savedSwn, out var bSwn))
        {
            currentWeekNumbers = bSwn;
        }
        else if (calVm != null)
        {
            currentWeekNumbers = calVm.ShowWeekNumbers;
        }
        cbWeekNumbers.IsChecked = currentWeekNumbers;

        cbWeekNumbers.Checked += (s, e) =>
        {
            if (win != null) { win.Settings.CustomSettings["calShowWeekNumbers"] = "true"; WidgetManager.Instance.SaveSettings(); }
            if (calVm != null) calVm.ShowWeekNumbers = true;
            TxtStatus.Text = loc.T("Calendar.SettingsSaved", "Настройки календаря сохранены");
            _onSettingsChanged?.Invoke();
        };
        cbWeekNumbers.Unchecked += (s, e) =>
        {
            if (win != null) { win.Settings.CustomSettings["calShowWeekNumbers"] = "false"; WidgetManager.Instance.SaveSettings(); }
            if (calVm != null) calVm.ShowWeekNumbers = false;
            TxtStatus.Text = loc.T("Calendar.SettingsSaved", "Настройки календаря сохранены");
            _onSettingsChanged?.Invoke();
        };
        spOptions.Children.Add(cbWeekNumbers);

        cardOptions.Child = spOptions;
        SettingsPanel.Children.Add(cardOptions);

        // Card 3: Quick Navigation Action
        var cardActions = CreateCard();
        var spActions = new StackPanel();

        var btnToday = new Button
        {
            Content = loc.T("Calendar.BtnToday", "📅 Вернуться к текущему месяцу"),
            Height = 32,
            Background = new SolidColorBrush(Color.FromArgb(25, 99, 102, 241)),
            Foreground = new SolidColorBrush(Color.FromRgb(129, 140, 248)),
            FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnToday.Click += (s, e) =>
        {
            if (calVm != null)
            {
                calVm.TodayCommand.Execute(null);
                TxtStatus.Text = loc.T("Calendar.TodayApplied", "Календарь переведен на текущий месяц");
                _onSettingsChanged?.Invoke();
            }
        };
        spActions.Children.Add(btnToday);

        cardActions.Child = spActions;
        SettingsPanel.Children.Add(cardActions);
    }

    // 9. RSS
    private void BuildRssSettings()
    {
        var loc = LocalizationService.Instance;
        TxtWindowTitle.Text = loc.T("Rss.Title", "Настройки Новостей");
        AddSectionHeader(
            loc.T("Rss.Header", "📰 Параметры новостной ленты"),
            loc.T("Rss.HeaderSub", "Выбор международных каналов, IT, технологий и скорости листания"));

        var rssVm = _viewModel as RssWidgetViewModel;

        // Card 1: Feed Preset
        var cardPreset = CreateCard();
        var spPreset = new StackPanel();

        spPreset.Children.Add(new TextBlock
        {
            Text = loc.T("Rss.FeedPreset", "Категория и источник новостей:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var comboPreset = new ComboBox
        {
            Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var presets = new (string Key, string Label)[]
        {
            ("world", loc.T("Rss.PresetWorld", "🌍 Мировые новости (BBC World / Reuters)")),
            ("un_ru", loc.T("Rss.PresetUnRu", "🌐 Новости ООН на русском (UN News)")),
            ("euronews", loc.T("Rss.PresetEuronews", "🇪🇺 Euronews на русском (Euronews RU)")),
            ("tech", loc.T("Rss.PresetTech", "💻 Технологии и AI (Hacker News / TechCrunch)")),
            ("habr", loc.T("Rss.PresetHabr", "🚀 Хабр и Разработка (Habr IT)")),
            ("crypto", loc.T("Rss.PresetCrypto", "💎 Криптовалюты (CoinDesk)")),
            ("science", loc.T("Rss.PresetScience", "🔬 Наука и Космос (NASA & Science)")),
            ("custom", loc.IsRussian ? "🔗 Пользовательский RSS URL" : "🔗 Custom RSS Feed URL")
        };

        foreach (var (k, label) in presets)
        {
            comboPreset.Items.Add(new ComboBoxItem { Content = label, Tag = k });
        }

        string currentPreset = rssVm?.Preset ?? "world";
        int selIdx = Array.FindIndex(presets, p => string.Equals(p.Key, currentPreset, StringComparison.OrdinalIgnoreCase));
        comboPreset.SelectedIndex = selIdx >= 0 ? selIdx : 0;

        // Custom URL input container (visible when custom selected)
        var spCustomUrl = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        spCustomUrl.Visibility = (currentPreset == "custom") ? Visibility.Visible : Visibility.Collapsed;

        spCustomUrl.Children.Add(new TextBlock
        {
            Text = loc.T("Rss.CustomUrl", "Пользовательский RSS URL:"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            Margin = new Thickness(0, 0, 0, 4)
        });

        var txtCustomUrl = new TextBox
        {
            Height = 30,
            Background = new SolidColorBrush(Color.FromRgb(22, 25, 40)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
            Padding = new Thickness(6, 4, 6, 4),
            Text = rssVm?.CustomUrl ?? "",
            Margin = new Thickness(0, 0, 0, 6)
        };
        spCustomUrl.Children.Add(txtCustomUrl);

        var btnApplyCustom = new Button
        {
            Content = loc.T("Rss.ApplyUrl", "Применить URL"),
            Height = 28,
            Width = 140,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(Color.FromArgb(28, 56, 189, 248)),
            Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnApplyCustom.Click += (s, e) =>
        {
            if (rssVm != null && !string.IsNullOrWhiteSpace(txtCustomUrl.Text))
            {
                rssVm.CustomUrl = txtCustomUrl.Text.Trim();
                rssVm.Preset = "custom";
                TxtStatus.Text = loc.T("Common.ChangesApplied", "Изменения применяются мгновенно");
                _onSettingsChanged?.Invoke();
            }
        };
        spCustomUrl.Children.Add(btnApplyCustom);

        comboPreset.SelectionChanged += (s, e) =>
        {
            if (comboPreset.SelectedItem is ComboBoxItem item && item.Tag is string key && rssVm != null)
            {
                spCustomUrl.Visibility = (key == "custom") ? Visibility.Visible : Visibility.Collapsed;
                rssVm.Preset = key;
                TxtStatus.Text = loc.T("Common.ChangesApplied", "Изменения применяются мгновенно");
                _onSettingsChanged?.Invoke();
            }
        };

        spPreset.Children.Add(comboPreset);
        spPreset.Children.Add(spCustomUrl);
        cardPreset.Child = spPreset;
        SettingsPanel.Children.Add(cardPreset);

        // Card 2: Auto-Slide Interval
        var cardInterval = CreateCard();
        var spInterval = new StackPanel();

        spInterval.Children.Add(new TextBlock
        {
            Text = loc.T("Rss.AutoSlide", "Интервал авто-переключения:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var comboInterval = new ComboBox
        {
            Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White
        };

        var intervals = new (int Sec, string Label)[]
        {
            (8, loc.T("Rss.Slide8s", "8 секунд")),
            (12, loc.T("Rss.Slide12s", "12 секунд (По умолчанию)")),
            (20, loc.T("Rss.Slide20s", "20 секунд")),
            (30, loc.T("Rss.Slide30s", "30 секунд")),
            (0, loc.T("Rss.SlideManual", "Только вручную"))
        };

        int curSec = rssVm?.SlideIntervalSec ?? 12;
        int intervalIdx = 1;
        for (int i = 0; i < intervals.Length; i++)
        {
            comboInterval.Items.Add(new ComboBoxItem { Content = intervals[i].Label, Tag = intervals[i].Sec });
            if (intervals[i].Sec == curSec) intervalIdx = i;
        }
        comboInterval.SelectedIndex = intervalIdx;

        comboInterval.SelectionChanged += (s, e) =>
        {
            if (comboInterval.SelectedItem is ComboBoxItem item && item.Tag is int sec && rssVm != null)
            {
                rssVm.SlideIntervalSec = sec;
                TxtStatus.Text = loc.T("Common.ChangesApplied", "Изменения применяются мгновенно");
                _onSettingsChanged?.Invoke();
            }
        };

        spInterval.Children.Add(comboInterval);
        cardInterval.Child = spInterval;
        SettingsPanel.Children.Add(cardInterval);

        // Card 3: Refresh Button
        var cardRefresh = CreateCard();
        var spRefresh = new StackPanel();

        var btnRefresh = new Button
        {
            Content = loc.T("Rss.RefreshBtn", "⟳ Обновить ленту новостей"),
            Height = 34,
            Background = new SolidColorBrush(Color.FromArgb(28, 56, 189, 248)),
            Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnRefresh.Click += (s, e) =>
        {
            if (rssVm != null)
            {
                rssVm.RefreshCommand.Execute(null);
                TxtStatus.Text = loc.T("Rss.FeedUpdated", "Лента новостей обновлена");
                _onSettingsChanged?.Invoke();
            }
        };
        spRefresh.Children.Add(btnRefresh);

        cardRefresh.Child = spRefresh;
        SettingsPanel.Children.Add(cardRefresh);
    }

    // 10. Media Frame
    private void BuildMediaFrameSettings()
    {
        TxtWindowTitle.Text = LocalizationService.Instance.T("MediaFrame.Title", "Настройки Фоторамки");
        AddSectionHeader(
            LocalizationService.Instance.T("MediaFrame.Header", "🖼️ Выбор медиафайлов и режим показа"),
            LocalizationService.Instance.T("MediaFrame.HeaderSub", "Перетащите фото прямо на виджет или укажите папку на диске"));

        if (_viewModel is not MediaFrameWidgetViewModel frameVm)
        {
            return;
        }

        // 1. Folder & Single Photo Selection & Drag-and-Drop Card
        var folderCard = CreateCard();
        var folderSp = new StackPanel();

        var lblSource = new TextBlock
        {
            Text = LocalizationService.Instance.T("MediaFrame.CurrentSource", "Текущий источник:"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            Margin = new Thickness(0, 0, 0, 4)
        };
        folderSp.Children.Add(lblSource);

        var txtCurrentSource = new TextBlock
        {
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 0, 10)
        };
        void UpdateSourceLabel()
        {
            if (frameVm.IsSingleFileMode && !string.IsNullOrEmpty(frameVm.SingleFilePath))
            {
                txtCurrentSource.Text = string.Format(LocalizationService.Instance.T("MediaFrame.SourceSinglePhoto", "Одно фото: {0}"), frameVm.SingleFilePath);
            }
            else if (!string.IsNullOrEmpty(frameVm.CustomFolder))
            {
                txtCurrentSource.Text = string.Format(LocalizationService.Instance.T("MediaFrame.SourceFolder", "Папка: {0}"), frameVm.CustomFolder);
            }
            else
            {
                txtCurrentSource.Text = LocalizationService.Instance.T("MediaFrame.DefaultPictures", "Стандартная папка «Изображения»");
            }
        }
        UpdateSourceLabel();
        folderSp.Children.Add(txtCurrentSource);

        // Buttons Grid for Folder and Single Photo
        var buttonsGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        buttonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        buttonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
        buttonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var btnFolder = new Button
        {
            Content = LocalizationService.Instance.T("MediaFrame.BtnFolder", "📁 Выбрать папку..."),
            Height = 34,
            Background = new SolidColorBrush(Color.FromArgb(25, 56, 189, 248)),
            Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnFolder.Click += (s, e) =>
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = LocalizationService.Instance.T("MediaFrame.SelectFolderDialog", "Выберите папку с изображениями")
                };
                if (!string.IsNullOrEmpty(frameVm.CustomFolder) && Directory.Exists(frameVm.CustomFolder))
                {
                    dlg.InitialDirectory = frameVm.CustomFolder;
                }

                if (dlg.ShowDialog() == true)
                {
                    frameVm.SetCustomFolder(dlg.FolderName);
                    UpdateSourceLabel();
                    TxtStatus.Text = string.Format(LocalizationService.Instance.T("MediaFrame.FolderLoaded", "Загружены фотографии из папки '{0}'"), Path.GetFileName(dlg.FolderName));
                    _onSettingsChanged?.Invoke();
                }
            }
            catch
            {
                using var fbd = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = LocalizationService.Instance.T("MediaFrame.SelectFolderDialog", "Выберите папку с изображениями"),
                    UseDescriptionForTitle = true
                };
                if (!string.IsNullOrEmpty(frameVm.CustomFolder) && Directory.Exists(frameVm.CustomFolder))
                {
                    fbd.SelectedPath = frameVm.CustomFolder;
                }
                if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    frameVm.SetCustomFolder(fbd.SelectedPath);
                    UpdateSourceLabel();
                    TxtStatus.Text = string.Format(LocalizationService.Instance.T("MediaFrame.FolderLoaded", "Загружены фотографии из папки '{0}'"), Path.GetFileName(fbd.SelectedPath));
                    _onSettingsChanged?.Invoke();
                }
            }
        };
        Grid.SetColumn(btnFolder, 0);
        buttonsGrid.Children.Add(btnFolder);

        var btnSinglePhoto = new Button
        {
            Content = LocalizationService.Instance.T("MediaFrame.BtnSingleFileShort", "🖼️ Одно фото..."),
            Height = 34,
            Background = new SolidColorBrush(Color.FromArgb(25, 168, 85, 247)),
            Foreground = new SolidColorBrush(Color.FromRgb(192, 132, 252)),
            FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnSinglePhoto.Click += (s, e) =>
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = LocalizationService.Instance.T("MediaFrame.SelectPhotoDialog", "Выберите фотографию"),
                Filter = "Изображения (*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.gif|Все файлы (*.*)|*.*"
            };
            if (!string.IsNullOrEmpty(frameVm.SingleFilePath) && File.Exists(frameVm.SingleFilePath))
            {
                ofd.InitialDirectory = Path.GetDirectoryName(frameVm.SingleFilePath);
                ofd.FileName = Path.GetFileName(frameVm.SingleFilePath);
            }
            else if (!string.IsNullOrEmpty(frameVm.CustomFolder) && Directory.Exists(frameVm.CustomFolder))
            {
                ofd.InitialDirectory = frameVm.CustomFolder;
            }

            if (ofd.ShowDialog() == true)
            {
                frameVm.SetSingleFile(ofd.FileName);
                UpdateSourceLabel();
                TxtStatus.Text = string.Format(LocalizationService.Instance.T("MediaFrame.SinglePhotoLoaded", "Выбрано фото '{0}'"), Path.GetFileName(ofd.FileName));
                _onSettingsChanged?.Invoke();
            }
        };
        Grid.SetColumn(btnSinglePhoto, 2);
        buttonsGrid.Children.Add(btnSinglePhoto);

        folderSp.Children.Add(buttonsGrid);

        // Drag & Drop Indicator Box
        var dndBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(20, 56, 189, 248)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(50, 56, 189, 248)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 10, 8)
        };
        var dndSp = new StackPanel();
        dndSp.Children.Add(new TextBlock
        {
            Text = "📥 " + LocalizationService.Instance.T("MediaFrame.DropHint", "Перетащите сюда фото или папку"),
            FontSize = 11.5,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            Margin = new Thickness(0, 0, 0, 2)
        });
        dndSp.Children.Add(new TextBlock
        {
            Text = LocalizationService.Instance.T("MediaFrame.DragDropSupported", "Поддерживается Drag & Drop: перетащите файлы или папки прямо на виджет"),
            FontSize = 10.5,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            TextWrapping = TextWrapping.Wrap
        });
        dndBorder.Child = dndSp;
        folderSp.Children.Add(dndBorder);

        folderCard.Child = folderSp;
        SettingsPanel.Children.Add(folderCard);

        // 2. Navigation Bar & Arrows Card (AutoHide)
        var navCard = CreateCard();
        var navSp = new StackPanel();

        var cbAutoHide = new CheckBox
        {
            Content = "👀 " + LocalizationService.Instance.T("MediaFrame.AutoHideControls", "Скрывать панель (показывать только при наведении)"),
            Foreground = Brushes.White,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            IsChecked = frameVm.AutoHideControls,
            Margin = new Thickness(0, 0, 0, 4)
        };
        cbAutoHide.Checked += (s, e) => { frameVm.AutoHideControls = true; _onSettingsChanged?.Invoke(); };
        cbAutoHide.Unchecked += (s, e) => { frameVm.AutoHideControls = false; _onSettingsChanged?.Invoke(); };
        navSp.Children.Add(cbAutoHide);

        navSp.Children.Add(new TextBlock
        {
            Text = LocalizationService.Instance.T("MediaFrame.AutoHideControlsHint", "Стрелки навигации и подпись плавно появляются при наведении курсора на фоторамку"),
            FontSize = 10.5,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(24, 0, 0, 0)
        });

        navCard.Child = navSp;
        SettingsPanel.Children.Add(navCard);

        // 2. Slideshow Speed / Timer Card
        var timerCard = CreateCard();
        var timerSp = new StackPanel();
        timerSp.Children.Add(new TextBlock
        {
            Text = "⏱️ " + LocalizationService.Instance.T("MediaFrame.SlideshowSpeed", "Скорость слайд-шоу"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            Margin = new Thickness(0, 0, 0, 4)
        });
        timerSp.Children.Add(new TextBlock
        {
            Text = LocalizationService.Instance.T("MediaFrame.SlideInterval", "Интервал смены фото:"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            Margin = new Thickness(0, 0, 0, 8)
        });

        var cbTimer = new ComboBox
        {
            Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(22, 25, 40)),
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255))
        };
        cbTimer.Items.Add(new ComboBoxItem { Content = LocalizationService.Instance.T("MediaFrame.Slide5s", "5 секунд"), Tag = 5 });
        cbTimer.Items.Add(new ComboBoxItem { Content = LocalizationService.Instance.T("MediaFrame.Slide10s", "10 секунд"), Tag = 10 });
        cbTimer.Items.Add(new ComboBoxItem { Content = LocalizationService.Instance.T("MediaFrame.Slide30s", "30 секунд"), Tag = 30 });
        cbTimer.Items.Add(new ComboBoxItem { Content = LocalizationService.Instance.T("MediaFrame.Slide1m", "1 минута"), Tag = 60 });
        cbTimer.Items.Add(new ComboBoxItem { Content = LocalizationService.Instance.T("MediaFrame.Slide5m", "5 минут"), Tag = 300 });
        cbTimer.Items.Add(new ComboBoxItem { Content = LocalizationService.Instance.T("MediaFrame.SlideManual", "Только вручную стрелками"), Tag = 0 });

        cbTimer.SelectedIndex = frameVm.SlideIntervalSec switch
        {
            5 => 0,
            10 => 1,
            30 => 2,
            60 => 3,
            300 => 4,
            0 => 5,
            _ => 1
        };

        cbTimer.SelectionChanged += (s, e) =>
        {
            if (cbTimer.SelectedItem is ComboBoxItem item && item.Tag is int sec)
            {
                frameVm.SlideIntervalSec = sec;
                _onSettingsChanged?.Invoke();
            }
        };
        timerSp.Children.Add(cbTimer);
        timerCard.Child = timerSp;
        SettingsPanel.Children.Add(timerCard);

        // 3. Image Fit / Stretch Mode Card
        var fitCard = CreateCard();
        var fitSp = new StackPanel();
        fitSp.Children.Add(new TextBlock
        {
            Text = "📐 " + LocalizationService.Instance.T("MediaFrame.FitMode", "Режим заполнения кадра:"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            Margin = new Thickness(0, 0, 0, 4)
        });

        var cbFit = new ComboBox
        {
            Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(22, 25, 40)),
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255))
        };
        cbFit.Items.Add(new ComboBoxItem { Content = LocalizationService.Instance.T("MediaFrame.FitCover", "Заполнить с обрезкой (Cover / UniformToFill)"), Tag = "UniformToFill" });
        cbFit.Items.Add(new ComboBoxItem { Content = LocalizationService.Instance.T("MediaFrame.FitContain", "Вписать целиком (Contain / Uniform)"), Tag = "Uniform" });
        cbFit.Items.Add(new ComboBoxItem { Content = LocalizationService.Instance.T("MediaFrame.FitStretch", "Растянуть на весь кадр (Stretch / Fill)"), Tag = "Fill" });

        cbFit.SelectedIndex = frameVm.StretchMode switch
        {
            System.Windows.Media.Stretch.UniformToFill => 0,
            System.Windows.Media.Stretch.Uniform => 1,
            System.Windows.Media.Stretch.Fill => 2,
            _ => 0
        };

        cbFit.SelectionChanged += (s, e) =>
        {
            if (cbFit.SelectedItem is ComboBoxItem item && item.Tag is string fitStr)
            {
                frameVm.StretchModeString = fitStr;
                _onSettingsChanged?.Invoke();
            }
        };
        fitSp.Children.Add(cbFit);
        fitCard.Child = fitSp;
        SettingsPanel.Children.Add(fitCard);

        // 4. Shuffle / Random Order Card
        var shuffleCard = CreateCard();
        var shuffleSp = new StackPanel();

        var cbShuffle = new CheckBox
        {
            Content = "🔀 " + LocalizationService.Instance.T("MediaFrame.Shuffle", "Случайный порядок (Shuffle)"),
            Foreground = Brushes.White,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            IsChecked = frameVm.IsShuffle,
            Margin = new Thickness(0, 0, 0, 4)
        };
        cbShuffle.Checked += (s, e) => { frameVm.IsShuffle = true; _onSettingsChanged?.Invoke(); };
        cbShuffle.Unchecked += (s, e) => { frameVm.IsShuffle = false; _onSettingsChanged?.Invoke(); };
        shuffleSp.Children.Add(cbShuffle);

        shuffleSp.Children.Add(new TextBlock
        {
            Text = LocalizationService.Instance.T("MediaFrame.ShuffleHint", "Показывать фотографии в случайной последовательности"),
            FontSize = 10.5,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(24, 0, 0, 0)
        });

        shuffleCard.Child = shuffleSp;
        SettingsPanel.Children.Add(shuffleCard);

        // 5. Opacity Card (Прозрачность фоторамки и фото)
        var opacityCard = CreateCard();
        var opacitySp = new StackPanel();

        var win = WidgetManager.Instance.GetActiveWindow(_instanceId);
        double currentOpacity = win?.Settings.Opacity ?? frameVm.WidgetOpacity;
        if (currentOpacity < 0.15) currentOpacity = 1.0;

        opacitySp.Children.Add(new TextBlock
        {
            Text = "🎨 " + LocalizationService.Instance.T("MediaFrame.OpacityTitle", "Прозрачность фоторамки и фото"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            Margin = new Thickness(0, 0, 0, 4)
        });

        var spOpacityHeader = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        spOpacityHeader.Children.Add(new TextBlock
        {
            Text = LocalizationService.Instance.T("MediaFrame.OpacityLevel", "Уровень непрозрачности (от 15% до 100%):"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            HorizontalAlignment = HorizontalAlignment.Left
        });

        var txtOpacityValue = new TextBlock
        {
            Text = $"{Math.Round(currentOpacity * 100)}%",
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        spOpacityHeader.Children.Add(txtOpacityValue);
        opacitySp.Children.Add(spOpacityHeader);

        var sliderOpacity = new Slider
        {
            Minimum = 15,
            Maximum = 100,
            Value = Math.Round(currentOpacity * 100),
            TickFrequency = 5,
            IsSnapToTickEnabled = true,
            SmallChange = 5,
            LargeChange = 10,
            Margin = new Thickness(0, 0, 0, 8),
            Cursor = Cursors.Hand
        };

        sliderOpacity.ValueChanged += (s, e) =>
        {
            int val = (int)Math.Round(sliderOpacity.Value);
            txtOpacityValue.Text = $"{val}%";
            double opFraction = val / 100.0;
            frameVm.ApplyOpacity(opFraction);
            if (win != null)
            {
                win.SetWidgetOpacity(opFraction);
            }
            _onSettingsChanged?.Invoke();
        };
        opacitySp.Children.Add(sliderOpacity);

        // Preset buttons: 100%, 75%, 50%, 25%
        var presetsGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        for (int i = 0; i < 4; i++)
        {
            presetsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            if (i < 3)
                presetsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6, GridUnitType.Pixel) });
        }

        var opacityPresets = new (int Percent, string Label)[]
        {
            (100, "100%"),
            (75, "75%"),
            (50, "50%"),
            (25, "25%")
        };

        for (int i = 0; i < opacityPresets.Length; i++)
        {
            var p = opacityPresets[i];
            var btnPreset = new Button
            {
                Content = p.Label,
                Height = 26,
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnPreset.Click += (s, e) =>
            {
                sliderOpacity.Value = p.Percent;
            };
            Grid.SetColumn(btnPreset, i * 2);
            presetsGrid.Children.Add(btnPreset);
        }
        opacitySp.Children.Add(presetsGrid);

        opacitySp.Children.Add(new TextBlock
        {
            Text = LocalizationService.Instance.T("MediaFrame.OpacityHint", "Прозрачность плавно применяется как к стеклянной рамке, так и к самой фотографии"),
            FontSize = 10.5,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            TextWrapping = TextWrapping.Wrap
        });

        opacityCard.Child = opacitySp;
        SettingsPanel.Children.Add(opacityCard);
    }

    // 11. System Gauges
    private void BuildSystemGaugesSettings()
    {
        var loc = LocalizationService.Instance;
        TxtWindowTitle.Text = loc.T("SystemGauges.Title", "Настройки Датчиков Ресурсов");
        AddSectionHeader(
            loc.T("SystemGauges.Header", "⚡ Формат и сенсоры системы"),
            loc.T("SystemGauges.HeaderSub", "Настройка визуального представления сенсоров (полоски / круги / GPU / SSD)"));

        var vm = _viewModel as SystemGaugesWidgetViewModel;
        var config = SystemGaugesConfig.Load(_instanceId);

        // Card 1: Layout & Style Options
        var cardLayout = CreateCard();
        var spLayout = new StackPanel();

        // 1. Circular Gauges Columns (2 in a row vs 1)
        spLayout.Children.Add(new TextBlock
        {
            Text = loc.T("SystemGauges.CircleColumnsTitle", "Расположение круговых диаграмм:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var comboCols = new ComboBox
        {
            Height = 32,
            Margin = new Thickness(0, 0, 0, 12),
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White
        };
        comboCols.Items.Add(new ComboBoxItem { Content = loc.T("SystemGauges.CircleColumns2", "2 в один ряд (Сетка 2x2)"), Tag = 2 });
        comboCols.Items.Add(new ComboBoxItem { Content = loc.T("SystemGauges.CircleColumns1", "1 в ряд (Список)"), Tag = 1 });
        comboCols.SelectedIndex = config.CircleColumns == 1 ? 1 : 0;

        comboCols.SelectionChanged += (s, e) =>
        {
            if (comboCols.SelectedItem is ComboBoxItem item && item.Tag is int cols)
            {
                config.CircleColumns = cols;
                SystemGaugesConfig.Save(_instanceId, config);
                vm?.SetCircleColumns(cols);
                TxtStatus.Text = loc.T("Common.ChangesApplied", "Изменения применяются мгновенно");
                _onSettingsChanged?.Invoke();
            }
        };
        spLayout.Children.Add(comboCols);

        // 2. Quick Gauge Style Switcher
        spLayout.Children.Add(new TextBlock
        {
            Text = loc.T("SystemGauges.StyleSection", "Стиль отображения шкал:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var comboStyle = new ComboBox
        {
            Height = 32,
            Margin = new Thickness(0, 0, 0, 12),
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White
        };
        comboStyle.Items.Add(new ComboBoxItem { Content = loc.T("SystemGauges.StyleCircles", "Круговые диаграммы (Rings)"), Tag = "circular" });
        comboStyle.Items.Add(new ComboBoxItem { Content = loc.T("SystemGauges.StyleBars", "Линейные полоски (Bars)"), Tag = "bars" });

        bool allCircles = config.Items.Where(i => i.IsEnabled).All(i => i.DisplayType == GaugeDisplayType.Circle);
        comboStyle.SelectedIndex = allCircles ? 0 : 1;

        comboStyle.SelectionChanged += (s, e) =>
        {
            if (comboStyle.SelectedItem is ComboBoxItem item && item.Tag is string style)
            {
                var targetType = style == "circular" ? GaugeDisplayType.Circle : GaugeDisplayType.Bar;
                foreach (var gi in config.Items)
                {
                    gi.DisplayType = targetType;
                }
                SystemGaugesConfig.Save(_instanceId, config);
                vm?.SetGaugeStyle(style);
                TxtStatus.Text = loc.T("Common.ChangesApplied", "Изменения применяются мгновенно");
                _onSettingsChanged?.Invoke();
            }
        };
        spLayout.Children.Add(comboStyle);

        cardLayout.Child = spLayout;
        SettingsPanel.Children.Add(cardLayout);

        // Card 2: Visual Constructor Button
        var cardConstructor = CreateCard();
        var spConstructor = new StackPanel();

        spConstructor.Children.Add(new TextBlock
        {
            Text = loc.T("SystemGauges.BtnConstructorHint", "Индивидуальная сортировка, выбор шкал и цвета сенсоров"),
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var btnConstructor = new Button
        {
            Content = loc.T("SystemGauges.BtnConstructor", "🎨 Открыть визуальный конструктор сенсоров..."),
            Height = 36,
            Background = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnConstructor.Click += (s, e) =>
        {
            var constructorWin = new GaugesConstructorWindow(_instanceId, () =>
            {
                TxtStatus.Text = loc.T("Common.ChangesApplied", "Изменения применяются мгновенно");
                _onSettingsChanged?.Invoke();
            });
            constructorWin.Owner = this;
            constructorWin.ShowDialog();
        };
        spConstructor.Children.Add(btnConstructor);

        cardConstructor.Child = spConstructor;
        SettingsPanel.Children.Add(cardConstructor);
    }

    // 12. Quick Dock
    private void BuildQuickDockSettings()
    {
        var loc = LocalizationService.Instance;
        TxtWindowTitle.Text = loc.T("QuickDock.Title", "Настройки Панели Дока");
        AddSectionHeader(
            loc.T("QuickDock.Header", "⚡ Панель быстрого запуска"),
            loc.T("QuickDock.HeaderSub", "Ярлыки приложений, терминала и утилит рабочего стола"));

        var dockVm = _viewModel as QuickDockWidgetViewModel;

        // Card 1: Icon Size
        var cardSize = CreateCard();
        var spSize = new StackPanel();

        spSize.Children.Add(new TextBlock
        {
            Text = loc.T("QuickDock.IconSize", "Размер иконок дока:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var comboSize = new ComboBox
        {
            Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White
        };

        var sizes = new (double Px, string Label)[]
        {
            (32, loc.T("QuickDock.SizeSmall", "Компактный (32 px)")),
            (40, loc.T("QuickDock.SizeMedium", "Стандартный (40 px)")),
            (48, loc.T("QuickDock.SizeLarge", "Большой (48 px)"))
        };

        double curPx = dockVm?.IconSize ?? 40;
        int sizeIdx = 1;
        for (int i = 0; i < sizes.Length; i++)
        {
            comboSize.Items.Add(new ComboBoxItem { Content = sizes[i].Label, Tag = sizes[i].Px });
            if (Math.Abs(sizes[i].Px - curPx) < 1) sizeIdx = i;
        }
        comboSize.SelectedIndex = sizeIdx;

        comboSize.SelectionChanged += (s, e) =>
        {
            if (comboSize.SelectedItem is ComboBoxItem item && item.Tag is double px && dockVm != null)
            {
                dockVm.IconSize = px;
                TxtStatus.Text = loc.T("Common.ChangesApplied", "Изменения применяются мгновенно");
                _onSettingsChanged?.Invoke();
            }
        };

        spSize.Children.Add(comboSize);
        cardSize.Child = spSize;
        SettingsPanel.Children.Add(cardSize);

        // Card 2: Add Custom Shortcut
        var cardAdd = CreateCard();
        var spAdd = new StackPanel();

        spAdd.Children.Add(new TextBlock
        {
            Text = loc.T("QuickDock.CustomTitle", "Добавить свой ярлык:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var txtName = new TextBox
        {
            Height = 30,
            Background = new SolidColorBrush(Color.FromRgb(22, 25, 40)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 0, 0, 6)
        };
        spAdd.Children.Add(new TextBlock
        {
            Text = loc.T("QuickDock.NamePlaceholder", "Название приложения"),
            FontSize = 10.5,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            Margin = new Thickness(0, 0, 0, 2)
        });
        spAdd.Children.Add(txtName);

        // Path field with Explorer Browse button
        spAdd.Children.Add(new TextBlock
        {
            Text = loc.T("QuickDock.PathPlaceholder", "Путь к файлу / URL (напр. https://...)"),
            FontSize = 10.5,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            Margin = new Thickness(0, 0, 0, 2)
        });

        var gridPath = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        gridPath.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gridPath.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var txtPath = new TextBox
        {
            Height = 30,
            Background = new SolidColorBrush(Color.FromRgb(22, 25, 40)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
            Padding = new Thickness(6, 4, 6, 4)
        };
        Grid.SetColumn(txtPath, 0);
        gridPath.Children.Add(txtPath);

        var btnBrowsePath = new Button
        {
            Content = loc.T("QuickDock.BrowseFile", "📁 Обзор..."),
            Height = 30,
            Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Padding = new Thickness(10, 0, 10, 0),
            Margin = new Thickness(6, 0, 0, 0)
        };
        Grid.SetColumn(btnBrowsePath, 1);
        gridPath.Children.Add(btnBrowsePath);
        spAdd.Children.Add(gridPath);

        // Icon field with Explorer Browse button
        spAdd.Children.Add(new TextBlock
        {
            Text = loc.T("QuickDock.IconPlaceholder", "Иконка / Emoji (напр. 🚀)"),
            FontSize = 10.5,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            Margin = new Thickness(0, 0, 0, 2)
        });

        var gridIcon = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        gridIcon.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gridIcon.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var txtIcon = new TextBox
        {
            Height = 30,
            Background = new SolidColorBrush(Color.FromRgb(22, 25, 40)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
            Padding = new Thickness(6, 4, 6, 4),
            Text = "🚀"
        };
        Grid.SetColumn(txtIcon, 0);
        gridIcon.Children.Add(txtIcon);

        var btnBrowseIcon = new Button
        {
            Content = loc.T("QuickDock.BrowseIcon", "🖼️ Обзор..."),
            Height = 30,
            Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Padding = new Thickness(10, 0, 10, 0),
            Margin = new Thickness(6, 0, 0, 0)
        };
        Grid.SetColumn(btnBrowseIcon, 1);
        gridIcon.Children.Add(btnBrowseIcon);
        spAdd.Children.Add(gridIcon);

        btnBrowsePath.Click += (s, e) =>
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = loc.T("QuickDock.SelectFileTitle", "Выберите исполняемый файл или документ"),
                    Filter = loc.T("QuickDock.FileFilter", "Приложения и файлы (*.exe;*.lnk;*.bat;*.cmd;*.*)|*.exe;*.lnk;*.bat;*.cmd;*.*|Приложения (*.exe)|*.exe|Все файлы (*.*)|*.*")
                };
                if (dlg.ShowDialog(this) == true)
                {
                    txtPath.Text = dlg.FileName;
                    if (string.IsNullOrWhiteSpace(txtName.Text))
                    {
                        txtName.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
                    }
                    if (string.IsNullOrWhiteSpace(txtIcon.Text) || txtIcon.Text == "🚀")
                    {
                        txtIcon.Text = dlg.FileName;
                    }
                }
            }
            catch { }
        };

        btnBrowseIcon.Click += (s, e) =>
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = loc.T("QuickDock.SelectIconTitle", "Выберите иконку или изображение"),
                    Filter = loc.T("QuickDock.IconFilter", "Иконки и изображения (*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.exe)|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.exe|Иконки (*.ico)|*.ico|Изображения (*.png;*.jpg)|*.png;*.jpg|Приложения (*.exe)|*.exe|Все файлы (*.*)|*.*")
                };
                if (dlg.ShowDialog(this) == true)
                {
                    txtIcon.Text = dlg.FileName;
                }
            }
            catch { }
        };

        var btnAdd = new Button
        {
            Content = loc.T("QuickDock.BtnAdd", "+ Добавить в Док"),
            Height = 32,
            Background = new SolidColorBrush(Color.FromArgb(28, 99, 102, 241)),
            Foreground = new SolidColorBrush(Color.FromRgb(129, 140, 248)),
            FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        spAdd.Children.Add(btnAdd);

        cardAdd.Child = spAdd;
        SettingsPanel.Children.Add(cardAdd);

        // Card 3: Current apps list
        var cardApps = CreateCard();
        var spApps = new StackPanel();

        spApps.Children.Add(new TextBlock
        {
            Text = loc.T("QuickDock.CurrentApps", "Текущие приложения в Доке:"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var spItemsContainer = new StackPanel();
        spApps.Children.Add(spItemsContainer);

        Action populateItemsList = null!;
        populateItemsList = () =>
        {
            spItemsContainer.Children.Clear();
            if (dockVm == null) return;

            var items = dockVm.GetCustomItems();
            for (int i = 0; i < items.Count; i++)
            {
                int idx = i;
                var item = items[i];

                var row = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(8, 6, 8, 6),
                    Margin = new Thickness(0, 0, 0, 6)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var iconImg = QuickDockWidgetViewModel.ResolveIconImage(item.Icon, item.Path);
                if (iconImg != null)
                {
                    var img = new Image
                    {
                        Source = iconImg,
                        Width = 20,
                        Height = 20,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(img, 0);
                    grid.Children.Add(img);
                }
                else
                {
                    var tbIcon = new TextBlock
                    {
                        Text = string.IsNullOrEmpty(item.Icon) ? "🚀" : item.Icon,
                        FontSize = 14,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(tbIcon, 0);
                    grid.Children.Add(tbIcon);
                }

                var textSp = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 4, 0)
                };
                textSp.Children.Add(new TextBlock
                {
                    Text = item.Name,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 11.5,
                    Foreground = Brushes.White,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                textSp.Children.Add(new TextBlock
                {
                    Text = item.Path,
                    FontSize = 9.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                Grid.SetColumn(textSp, 1);
                grid.Children.Add(textSp);

                var btnDel = new Button
                {
                    Content = "✕",
                    ToolTip = loc.T("QuickDock.DeleteApp", "Удалить"),
                    Width = 24,
                    Height = 24,
                    Background = new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)),
                    Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    FontWeight = FontWeights.Bold,
                    FontSize = 11
                };
                btnDel.Click += (s, e) =>
                {
                    dockVm.RemoveCustomApp(idx);
                    populateItemsList();
                    TxtStatus.Text = loc.T("Common.ChangesApplied", "Изменения применяются мгновенно");
                    _onSettingsChanged?.Invoke();
                };
                Grid.SetColumn(btnDel, 2);
                grid.Children.Add(btnDel);

                row.Child = grid;
                spItemsContainer.Children.Add(row);
            }
        };

        populateItemsList();
        cardApps.Child = spApps;
        SettingsPanel.Children.Add(cardApps);

        btnAdd.Click += (s, e) =>
        {
            if (dockVm != null && !string.IsNullOrWhiteSpace(txtName.Text) && !string.IsNullOrWhiteSpace(txtPath.Text))
            {
                string icon = string.IsNullOrWhiteSpace(txtIcon.Text) ? "🚀" : txtIcon.Text.Trim();
                dockVm.AddCustomApp(txtName.Text.Trim(), icon, txtPath.Text.Trim());
                txtName.Text = "";
                txtPath.Text = "";
                txtIcon.Text = "🚀";
                populateItemsList();
                TxtStatus.Text = loc.T("Common.ChangesApplied", "Изменения применяются мгновенно");
                _onSettingsChanged?.Invoke();
            }
        };

        // Card 4: Reset
        var cardReset = CreateCard();
        var spReset = new StackPanel();

        var btnReset = new Button
        {
            Content = loc.T("QuickDock.BtnReset", "Сбросить к стандартным"),
            Height = 32,
            Background = new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)),
            Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnReset.Click += (s, e) =>
        {
            if (dockVm != null)
            {
                dockVm.ResetDefaults();
                populateItemsList();
                TxtStatus.Text = loc.T("Common.ChangesApplied", "Изменения применяются мгновенно");
                _onSettingsChanged?.Invoke();
            }
        };
        spReset.Children.Add(btnReset);

        cardReset.Child = spReset;
        SettingsPanel.Children.Add(cardReset);
    }

    private void BuildGenericSettings()
    {
        AddSectionHeader("⚙️ Настройки виджета", "Параметры экземпляра на рабочем столе");
    }
}
