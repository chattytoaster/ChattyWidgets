using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ChattyWidgets.Core.Services;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using Cursors = System.Windows.Input.Cursors;

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
            default:
                BuildGenericSettings();
                break;
        }
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
        TxtWindowTitle.Text = "Настройки Погоды";
        AddSectionHeader("🌤 Настройки прогноза погоды", "Выберите город или введите вручную для загрузки данных через Open-Meteo API");

        var card = CreateCard();
        var sp = new StackPanel();

        sp.Children.Add(new TextBlock
        {
            Text = "Быстрый выбор города:",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var combo = new ComboBox
        {
            Height = 32,
            Margin = new Thickness(0, 0, 0, 10),
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White
        };

        var cities = new[]
        {
            ("Москва", 55.7558, 37.6173),
            ("Санкт-Петербург", 59.9343, 30.3351),
            ("Новосибирск", 55.0084, 82.9357),
            ("Екатеринбург", 56.8389, 60.6057),
            ("Казань", 55.8304, 49.0661),
            ("Нижний Новгород", 56.2965, 43.9361),
            ("Сочи", 43.6028, 39.7342),
            ("Владивосток", 43.1198, 131.8869),
            ("Калининград", 54.7104, 20.4522)
        };

        foreach (var (c, _, _) in cities) combo.Items.Add(c);

        if (_viewModel is WeatherWidgetViewModel weatherVm)
        {
            combo.SelectedItem = weatherVm.City;
        }
        else
        {
            combo.SelectedIndex = 0;
        }

        combo.SelectionChanged += (s, e) =>
        {
            if (combo.SelectedIndex >= 0 && combo.SelectedIndex < cities.Length && _viewModel is WeatherWidgetViewModel vm)
            {
                var sel = cities[combo.SelectedIndex];
                vm.SetCity(sel.Item1, sel.Item2, sel.Item3);
                TxtStatus.Text = $"Город изменен на {sel.Item1}. Загрузка прогноза...";
                _onSettingsChanged?.Invoke();
            }
        };

        sp.Children.Add(combo);

        var btnRefresh = new Button
        {
            Content = "⟳ Обновить прогноз погоды сейчас",
            Height = 32,
            Background = new SolidColorBrush(Color.FromArgb(28, 56, 189, 248)),
            Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnRefresh.Click += async (s, e) =>
        {
            if (_viewModel is WeatherWidgetViewModel vm)
            {
                TxtStatus.Text = "Обновление погоды...";
                await vm.RefreshWeatherAsync();
                TxtStatus.Text = "Погода успешно обновлена!";
                _onSettingsChanged?.Invoke();
            }
        };
        sp.Children.Add(btnRefresh);

        card.Child = sp;
        SettingsPanel.Children.Add(card);
    }

    // 2. Clock
    private void BuildClockSettings()
    {
        TxtWindowTitle.Text = "Настройки Стеклянных Часов";
        AddSectionHeader("🕒 Настройки отображения времени", "Параметры циферблата, формата времени и индикаторов");

        var card = CreateCard();
        var sp = new StackPanel();

        var cb24 = new CheckBox
        {
            Content = "24-часовой формат времени (14:30)",
            Foreground = Brushes.White,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8),
            IsChecked = true
        };

        var cbSec = new CheckBox
        {
            Content = "Отображать секунды (:42)",
            Foreground = Brushes.White,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8),
            IsChecked = true
        };

        var cbDay = new CheckBox
        {
            Content = "Отображать день недели внизу",
            Foreground = Brushes.White,
            FontSize = 12,
            IsChecked = true
        };

        if (_viewModel is ClockWidgetViewModel clockVm)
        {
            cb24.IsChecked = clockVm.Is24Hour;
            cbSec.IsChecked = clockVm.ShowSeconds;
            cbDay.IsChecked = clockVm.ShowDayName;

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
        TxtWindowTitle.Text = "Настройки Котировок";
        AddSectionHeader("💎 Котировки & Курсы Валют", "Обновление курсов через Binance API и ЦБ РФ");

        var card = CreateCard();
        var sp = new StackPanel();

        var btnRefresh = new Button
        {
            Content = "⟳ Обновить котировки с биржи сейчас",
            Height = 34,
            Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnRefresh.Click += async (s, e) =>
        {
            if (_viewModel is CryptoWidgetViewModel cryptoVm)
            {
                TxtStatus.Text = "Загрузка котировок с Binance и ЦБ РФ...";
                await cryptoVm.FetchPricesAsync();
                TxtStatus.Text = "Котировки успешно обновлены!";
                _onSettingsChanged?.Invoke();
            }
        };
        sp.Children.Add(btnRefresh);

        card.Child = sp;
        SettingsPanel.Children.Add(card);
    }

    // 4. Pomodoro
    private void BuildPomodoroSettings()
    {
        TxtWindowTitle.Text = "Настройки Pomodoro";
        AddSectionHeader("🍅 Параметры таймера фокуса", "Настройте интервалы продуктивности и отдыха");

        var card = CreateCard();
        var sp = new StackPanel();

        sp.Children.Add(new TextBlock
        {
            Text = "Длительность фокуса:",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var comboWork = new ComboBox
        {
            Height = 32,
            Margin = new Thickness(0, 0, 0, 10),
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Foreground = Brushes.White
        };
        comboWork.Items.Add("15 минут");
        comboWork.Items.Add("20 минут");
        comboWork.Items.Add("25 минут (Классика)");
        comboWork.Items.Add("30 минут");
        comboWork.Items.Add("45 минут (Учеба)");
        comboWork.Items.Add("50 минут");
        comboWork.SelectedIndex = 2;

        if (_viewModel is PomodoroWidgetViewModel pomodoroVm)
        {
            comboWork.SelectionChanged += (s, e) =>
            {
                int mins = comboWork.SelectedIndex switch
                {
                    0 => 15,
                    1 => 20,
                    2 => 25,
                    3 => 30,
                    4 => 45,
                    5 => 50,
                    _ => 25
                };
                pomodoroVm.SetWorkMinutes(mins);
                _onSettingsChanged?.Invoke();
            };
        }

        sp.Children.Add(comboWork);

        card.Child = sp;
        SettingsPanel.Children.Add(card);
    }

    // 5. Music Player
    private void BuildMusicSettings()
    {
        TxtWindowTitle.Text = "Настройки Медиаплеера";
        AddSectionHeader("🎵 Параметры мини-плеера", "Управление виниловым диском и отображением трека");

        var card = CreateCard();
        var sp = new StackPanel();

        sp.Children.Add(new TextBlock
        {
            Text = "Плеер автоматически считывает активные треки из Spotify, Яндекс Музыки, Браузера и плееров Windows.",
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var btnRefresh = new Button
        {
            Content = "⟳ Переподключиться к сессиям медиа",
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
            TxtStatus.Text = "Сессии Windows Media обновлены";
            _onSettingsChanged?.Invoke();
        };
        sp.Children.Add(btnRefresh);

        card.Child = sp;
        SettingsPanel.Children.Add(card);
    }

    // 6. GPU
    private void BuildGpuSettings()
    {
        TxtWindowTitle.Text = "Настройки GPU Монитора";
        AddSectionHeader("🎮 Настройки датчиков видеокарты", "Мониторинг DirectX 3D Engine, VRAM и температуры");

        var card = CreateCard();
        var sp = new StackPanel();

        sp.Children.Add(new TextBlock
        {
            Text = "Данные считываются в реальном времени с аппаратных счетчиков производительности Windows DWM.",
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        card.Child = sp;
        SettingsPanel.Children.Add(card);
    }

    // 7. Sticky Notes
    private void BuildStickyNotesSettings()
    {
        TxtWindowTitle.Text = "Настройки Заметок & Задач";
        AddSectionHeader("📌 Управление списком задач", "Быстрые действия с заметками и чек-листом");

        var card = CreateCard();
        var sp = new StackPanel();

        var btnClearDone = new Button
        {
            Content = "✓ Удалить все выполненные задачи",
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
            if (_viewModel is StickyNotesWidgetViewModel notesVm)
            {
                notesVm.ClearCompletedTasks();
                TxtStatus.Text = "Выполненные задачи очищены";
                _onSettingsChanged?.Invoke();
            }
        };
        sp.Children.Add(btnClearDone);

        card.Child = sp;
        SettingsPanel.Children.Add(card);
    }

    // 8. Calendar
    private void BuildCalendarSettings()
    {
        TxtWindowTitle.Text = "Настройки Календаря";
        AddSectionHeader("📅 Параметры стеклянного календаря", "Навигация и подсветка дат");

        var card = CreateCard();
        var sp = new StackPanel();

        var btnToday = new Button
        {
            Content = "📅 Вернуться к текущему месяцу",
            Height = 32,
            Background = new SolidColorBrush(Color.FromArgb(25, 99, 102, 241)),
            Foreground = new SolidColorBrush(Color.FromRgb(129, 140, 248)),
            FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnToday.Click += (s, e) =>
        {
            if (_viewModel is CalendarWidgetViewModel calVm)
            {
                calVm.TodayCommand.Execute(null);
                TxtStatus.Text = "Календарь переведен на текущий месяц";
                _onSettingsChanged?.Invoke();
            }
        };
        sp.Children.Add(btnToday);

        card.Child = sp;
        SettingsPanel.Children.Add(card);
    }

    // 9. RSS
    private void BuildRssSettings()
    {
        TxtWindowTitle.Text = "Настройки Новостей";
        AddSectionHeader("📰 Параметры новостной ленты", "Управление категориями и листанием");

        var card = CreateCard();
        var sp = new StackPanel();

        var btnRefresh = new Button
        {
            Content = "⟳ Обновить ленту новостей",
            Height = 32,
            Background = new SolidColorBrush(Color.FromArgb(28, 56, 189, 248)),
            Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnRefresh.Click += (s, e) =>
        {
            if (_viewModel is RssWidgetViewModel rssVm)
            {
                rssVm.RefreshCommand.Execute(null);
                TxtStatus.Text = "Лента обновлена";
                _onSettingsChanged?.Invoke();
            }
        };
        sp.Children.Add(btnRefresh);

        card.Child = sp;
        SettingsPanel.Children.Add(card);
    }

    // 10. Media Frame
    private void BuildMediaFrameSettings()
    {
        TxtWindowTitle.Text = "Настройки Фоторамки";
        AddSectionHeader("🖼️ Выбор медиафайлов", "Укажите папку с изображениями для показа в рамке");

        var card = CreateCard();
        var sp = new StackPanel();

        var btnFolder = new Button
        {
            Content = "📁 Выбрать папку с фотографиями...",
            Height = 34,
            Background = new SolidColorBrush(Color.FromArgb(25, 56, 189, 248)),
            Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        btnFolder.Click += (s, e) =>
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Выберите папку с изображениями"
            };
            if (dlg.ShowDialog() == true && _viewModel is MediaFrameWidgetViewModel frameVm)
            {
                frameVm.SetCustomFolder(dlg.FolderName);
                TxtStatus.Text = $"Загружены фотографии из папки '{Path.GetFileName(dlg.FolderName)}'";
                _onSettingsChanged?.Invoke();
            }
        };
        sp.Children.Add(btnFolder);

        card.Child = sp;
        SettingsPanel.Children.Add(card);
    }

    // 11. System Gauges
    private void BuildSystemGaugesSettings()
    {
        TxtWindowTitle.Text = "Настройки Датчиков Ресурсов";
        AddSectionHeader("⚡ Формат и сенсоры системы", "Настройка визуального представления сенсоров (полоски / круги / GPU / SSD)");

        var card = CreateCard();
        var sp = new StackPanel();

        var btnConstructor = new Button
        {
            Content = "🎨 Открыть визуальный конструктор сенсоров...",
            Height = 34,
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
                _onSettingsChanged?.Invoke();
            });
            constructorWin.Owner = this;
            constructorWin.ShowDialog();
        };
        sp.Children.Add(btnConstructor);

        card.Child = sp;
        SettingsPanel.Children.Add(card);
    }

    private void BuildGenericSettings()
    {
        AddSectionHeader("⚙️ Настройки виджета", "Параметры экземпляра на рабочем столе");
    }
}
