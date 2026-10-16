using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ChattyWidgets.Core.Services;

public class NativeRelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public NativeRelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public NativeRelayCommand(Action execute) : this(_ => execute(), null) { }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}

public abstract class NativeViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}

public static class NativeWidgetContextProvider
{
    public static object GetContextFor(string baseWidgetId, string instanceId, FrameworkElement? rootElement = null)
    {
        return baseWidgetId switch
        {
            "com.chatty.calendar-glass" => new CalendarWidgetViewModel(),
            "com.chatty.crypto-tracker" => new CryptoWidgetViewModel(),
            "com.chatty.gpu-monitor" => new GpuMonitorWidgetViewModel(),
            "com.chatty.media-frame" => new MediaFrameWidgetViewModel(),
            "com.chatty.music-player" => new MusicPlayerWidgetViewModel(),
            "com.chatty.pomodoro-timer" => new PomodoroWidgetViewModel(),
            "com.chatty.quick-dock" => new QuickDockWidgetViewModel(),
            "com.chatty.rss-reader" => new RssWidgetViewModel(),
            "com.chatty.sticky-notes" => new StickyNotesWidgetViewModel(instanceId, rootElement),
            "com.chatty.weather-card" => new WeatherWidgetViewModel(),
            _ => SystemMonitorService.Instance
        };
    }
}

// 1. Calendar ViewModel
public class CalendarDayItem
{
    public int Day { get; set; }
    public string DayText => Day > 0 ? Day.ToString() : "";
    public bool IsToday { get; set; }
    public bool IsCurrentMonth { get; set; }
    public string BgColor => IsToday ? "#3B82F6" : "Transparent";
    public string FgColor => IsToday ? "#FFFFFF" : (IsCurrentMonth ? "#E2E8F0" : "#475569");
    public FontWeight Weight => IsToday ? FontWeights.Bold : FontWeights.Normal;
}

public class CalendarWidgetViewModel : NativeViewModelBase
{
    private DateTime _displayDate = DateTime.Today;

    public string MonthYear => _displayDate.ToString("MMMM yyyy", new System.Globalization.CultureInfo("ru-RU")).ToUpper();
    public string TodayFull => DateTime.Today.ToString("d MMMM, dddd", new System.Globalization.CultureInfo("ru-RU"));
    public string CurrentDayNumber => DateTime.Today.Day.ToString();
    public string CurrentWeekday => DateTime.Today.ToString("dddd", new System.Globalization.CultureInfo("ru-RU"));

    public ObservableCollection<CalendarDayItem> Days { get; } = new();

    public ICommand PrevMonthCommand { get; }
    public ICommand NextMonthCommand { get; }
    public ICommand TodayCommand { get; }

    public CalendarWidgetViewModel()
    {
        PrevMonthCommand = new NativeRelayCommand(() => { _displayDate = _displayDate.AddMonths(-1); BuildMonth(); });
        NextMonthCommand = new NativeRelayCommand(() => { _displayDate = _displayDate.AddMonths(1); BuildMonth(); });
        TodayCommand = new NativeRelayCommand(() => { _displayDate = DateTime.Today; BuildMonth(); });
        BuildMonth();
    }

    private void BuildMonth()
    {
        Days.Clear();
        var firstDay = new DateTime(_displayDate.Year, _displayDate.Month, 1);
        int daysInMonth = DateTime.DaysInMonth(_displayDate.Year, _displayDate.Month);
        int startDayOffset = ((int)firstDay.DayOfWeek + 6) % 7; // Monday = 0

        for (int i = 0; i < startDayOffset; i++)
        {
            Days.Add(new CalendarDayItem { Day = 0, IsCurrentMonth = false });
        }

        for (int d = 1; d <= daysInMonth; d++)
        {
            bool isToday = (_displayDate.Year == DateTime.Today.Year &&
                            _displayDate.Month == DateTime.Today.Month &&
                            d == DateTime.Today.Day);
            Days.Add(new CalendarDayItem { Day = d, IsToday = isToday, IsCurrentMonth = true });
        }

        OnPropertyChanged(nameof(MonthYear));
    }
}

// 2. Crypto & Currencies ViewModel
public class CryptoItem
{
    public string Name { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string Price { get; set; } = "";
    public string Change { get; set; } = "";
    public bool IsPositive { get; set; }
    public string ChangeColor => IsPositive ? "#10B981" : "#EF4444";
    public string Icon { get; set; } = "●";
}

public class CryptoWidgetViewModel : NativeViewModelBase
{
    public ObservableCollection<CryptoItem> Items { get; } = new();

    public CryptoWidgetViewModel()
    {
        Items.Add(new CryptoItem { Name = "Bitcoin", Symbol = "BTC", Price = "$68,450", Change = "+2.4%", IsPositive = true, Icon = "₿" });
        Items.Add(new CryptoItem { Name = "Ethereum", Symbol = "ETH", Price = "$3,520", Change = "+1.8%", IsPositive = true, Icon = "Ξ" });
        Items.Add(new CryptoItem { Name = "Solana", Symbol = "SOL", Price = "$182.40", Change = "-0.7%", IsPositive = false, Icon = "◎" });
        Items.Add(new CryptoItem { Name = "Toncoin", Symbol = "TON", Price = "$5.35", Change = "+3.6%", IsPositive = true, Icon = "💎" });
        Items.Add(new CryptoItem { Name = "Доллар США", Symbol = "USD/RUB", Price = "96.20 ₽", Change = "+0.15", IsPositive = true, Icon = "$" });
        Items.Add(new CryptoItem { Name = "Евро", Symbol = "EUR/RUB", Price = "104.50 ₽", Change = "-0.22", IsPositive = false, Icon = "€" });
    }
}

// 3. GPU Monitor ViewModel
public class GpuMonitorWidgetViewModel : NativeViewModelBase
{
    public SystemMonitorService Monitor => SystemMonitorService.Instance;
    public string GpuName => Monitor.CurrentMetrics.GpuName;
    public double GpuPercent => Monitor.CurrentMetrics.GpuPercent;
    public double GpuMemoryPercent => Monitor.CurrentMetrics.GpuMemoryPercent;
    public double GpuTemp => Monitor.CurrentMetrics.GpuTemperature;
}

// 4. Media Frame ViewModel
public class MediaFrameWidgetViewModel : NativeViewModelBase
{
    private string _currentImage = "";
    public string CurrentImage
    {
        get => _currentImage;
        set { _currentImage = value; OnPropertyChanged(); }
    }

    private string _caption = "ChattyWidgets Gallery";
    public string Caption
    {
        get => _caption;
        set { _caption = value; OnPropertyChanged(); }
    }

    private readonly List<string> _images = new();
    private int _index = 0;

    public ICommand NextCommand { get; }
    public ICommand PrevCommand { get; }

    public MediaFrameWidgetViewModel()
    {
        NextCommand = new NativeRelayCommand(NextImage);
        PrevCommand = new NativeRelayCommand(PrevImage);

        try
        {
            string pics = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (Directory.Exists(pics))
            {
                foreach (var ext in new[] { "*.jpg", "*.png", "*.jpeg", "*.webp" })
                {
                    _images.AddRange(Directory.GetFiles(pics, ext));
                }
            }
        }
        catch { }

        if (_images.Count > 0)
        {
            ShowImage(0);
        }
        else
        {
            Caption = "Поместите изображения в папку «Изображения»";
        }
    }

    private void ShowImage(int idx)
    {
        if (_images.Count == 0) return;
        _index = (idx + _images.Count) % _images.Count;
        CurrentImage = _images[_index];
        Caption = Path.GetFileNameWithoutExtension(_images[_index]);
    }

    private void NextImage() => ShowImage(_index + 1);
    private void PrevImage() => ShowImage(_index - 1);
}

// 5. Music Player ViewModel
public class MusicPlayerWidgetViewModel : NativeViewModelBase
{
    private bool _isPlaying = false;
    public bool IsPlaying
    {
        get => _isPlaying;
        set { _isPlaying = value; OnPropertyChanged(); OnPropertyChanged(nameof(PlayPauseIcon)); }
    }

    public string PlayPauseIcon => IsPlaying ? "⏸" : "▶";

    private string _trackTitle = "Chatty Lounge";
    public string TrackTitle
    {
        get => _trackTitle;
        set { _trackTitle = value; OnPropertyChanged(); }
    }

    private string _artist = "Lo-Fi Desktop Chill";
    public string Artist
    {
        get => _artist;
        set { _artist = value; OnPropertyChanged(); }
    }

    private double _progressPercent = 35;
    public double ProgressPercent
    {
        get => _progressPercent;
        set { _progressPercent = value; OnPropertyChanged(); }
    }

    public ICommand PlayPauseCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand PrevCommand { get; }

    public MusicPlayerWidgetViewModel()
    {
        PlayPauseCommand = new NativeRelayCommand(() =>
        {
            MediaMonitorService.Instance.PlayPause();
            IsPlaying = !IsPlaying;
        });
        NextCommand = new NativeRelayCommand(() =>
        {
            MediaMonitorService.Instance.Next();
        });
        PrevCommand = new NativeRelayCommand(() =>
        {
            MediaMonitorService.Instance.Previous();
        });

        // Subscribe to live media sessions if available
        try
        {
            MediaMonitorService.Instance.MediaUpdated += (info) =>
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    if (!string.IsNullOrEmpty(info.Title)) TrackTitle = info.Title;
                    if (!string.IsNullOrEmpty(info.Artist)) Artist = info.Artist;
                    IsPlaying = info.IsPlaying;
                });
            };
            var cur = MediaMonitorService.Instance.CurrentMedia;
            if (cur != null)
            {
                if (!string.IsNullOrEmpty(cur.Title)) TrackTitle = cur.Title;
                if (!string.IsNullOrEmpty(cur.Artist)) Artist = cur.Artist;
                IsPlaying = cur.IsPlaying;
            }
        }
        catch { }
    }
}

// 6. Pomodoro Timer ViewModel
public class PomodoroWidgetViewModel : NativeViewModelBase
{
    private readonly DispatcherTimer _timer;
    private int _remainingSeconds = 25 * 60;
    private bool _isRunning = false;
    private bool _isFocusMode = true;

    public string TimeText => $"{_remainingSeconds / 60:D2}:{_remainingSeconds % 60:D2}";
    public string ModeText => _isFocusMode ? "🎯 ФОКУС" : "☕ ОТДЫХ";
    public string ActionButtonText => _isRunning ? "Пауза" : "Старт";
    public string AccentColor => _isFocusMode ? "#EF4444" : "#10B981";

    public double ProgressPercent
    {
        get
        {
            int total = (_isFocusMode ? 25 : 5) * 60;
            return total > 0 ? ((double)(total - _remainingSeconds) / total) * 100 : 0;
        }
    }

    public ICommand ToggleTimerCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand SwitchModeCommand { get; }

    public PomodoroWidgetViewModel()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (s, e) =>
        {
            if (_remainingSeconds > 0)
            {
                _remainingSeconds--;
                OnPropertyChanged(nameof(TimeText));
                OnPropertyChanged(nameof(ProgressPercent));
            }
            else
            {
                _timer.Stop();
                _isRunning = false;
                _isFocusMode = !_isFocusMode;
                _remainingSeconds = (_isFocusMode ? 25 : 5) * 60;
                OnPropertyChanged(nameof(TimeText));
                OnPropertyChanged(nameof(ModeText));
                OnPropertyChanged(nameof(ActionButtonText));
                OnPropertyChanged(nameof(AccentColor));
                OnPropertyChanged(nameof(ProgressPercent));
            }
        };

        ToggleTimerCommand = new NativeRelayCommand(() =>
        {
            _isRunning = !_isRunning;
            if (_isRunning) _timer.Start(); else _timer.Stop();
            OnPropertyChanged(nameof(ActionButtonText));
        });

        ResetCommand = new NativeRelayCommand(() =>
        {
            _timer.Stop();
            _isRunning = false;
            _remainingSeconds = (_isFocusMode ? 25 : 5) * 60;
            OnPropertyChanged(nameof(TimeText));
            OnPropertyChanged(nameof(ActionButtonText));
            OnPropertyChanged(nameof(ProgressPercent));
        });

        SwitchModeCommand = new NativeRelayCommand(() =>
        {
            _timer.Stop();
            _isRunning = false;
            _isFocusMode = !_isFocusMode;
            _remainingSeconds = (_isFocusMode ? 25 : 5) * 60;
            OnPropertyChanged(nameof(TimeText));
            OnPropertyChanged(nameof(ModeText));
            OnPropertyChanged(nameof(ActionButtonText));
            OnPropertyChanged(nameof(AccentColor));
            OnPropertyChanged(nameof(ProgressPercent));
        });
    }
}

// 7. Quick Dock ViewModel
public class DockAppItem
{
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Tooltip { get; set; } = "";
    public ICommand LaunchCommand { get; set; } = null!;
}

public class QuickDockWidgetViewModel : NativeViewModelBase
{
    public ObservableCollection<DockAppItem> Apps { get; } = new();

    public QuickDockWidgetViewModel()
    {
        AddApp("Проводник", "📁", "Открыть Проводник", () => Process.Start("explorer.exe"));
        AddApp("Терминал", "💻", "PowerShell / Terminal", () => Process.Start(new ProcessStartInfo("wt.exe") { UseShellExecute = true, ErrorDialog = false }) );
        AddApp("Браузер", "🌐", "Открыть браузер", () => Process.Start(new ProcessStartInfo("https://google.com") { UseShellExecute = true }));
        AddApp("Блокнот", "📝", "Быстрый блокнот", () => Process.Start("notepad.exe"));
        AddApp("Калькулятор", "🔢", "Калькулятор Windows", () => Process.Start("calc.exe"));
        AddApp("Диспетчер", "⚡", "Диспетчер задач", () => Process.Start("taskmgr.exe"));
    }

    private void AddApp(string name, string icon, string tip, Action action)
    {
        Apps.Add(new DockAppItem
        {
            Name = name,
            Icon = icon,
            Tooltip = tip,
            LaunchCommand = new NativeRelayCommand(() => {
                try { action(); } catch { }
            })
        });
    }
}

// 8. RSS Reader ViewModel
public class RssArticleItem
{
    public string Title { get; set; } = "";
    public string Source { get; set; } = "";
    public string Time { get; set; } = "";
    public string Url { get; set; } = "";
    public ICommand OpenCommand { get; set; } = null!;
}

public class RssWidgetViewModel : NativeViewModelBase
{
    public string FeedTitle => "Лента Новостей & IT";
    public ObservableCollection<RssArticleItem> Articles { get; } = new();

    public RssWidgetViewModel()
    {
        Articles.Add(new RssArticleItem { Title = "Релиз ChattyWidgets 2.0: Полный переход на нативный стек и сверхбыстрые виджеты", Source = "ChattyWidgets News", Time = "10 мин назад", Url = "https://github.com/chattytoaster/ChattyWidgets" });
        Articles.Add(new RssArticleItem { Title = "Новый стандарт производительности рабочего стола без нагрузки на CPU", Source = "Habr", Time = "25 мин назад", Url = "https://habr.com" });
        Articles.Add(new RssArticleItem { Title = "WPF и Windows 11: Создание красивых стеклянных интерфейсов следующего поколения", Source = "Microsoft Dev", Time = "1 час назад", Url = "https://learn.microsoft.com" });
        Articles.Add(new RssArticleItem { Title = "Открытый каталог .chw виджетов: расширяй свой рабочий стол в 1 клик", Source = "GitHub Community", Time = "2 часа назад", Url = "https://github.com/chattytoaster/ChattyWidgetsLibrary" });

        foreach (var a in Articles)
        {
            a.OpenCommand = new NativeRelayCommand(() => {
                try { Process.Start(new ProcessStartInfo(a.Url) { UseShellExecute = true }); } catch { }
            });
        }
    }
}

// 9. Sticky Notes ViewModel
public class StickyNotesWidgetViewModel : NativeViewModelBase
{
    private readonly string _storageFile;
    private string _noteText = "✦ Заметки ChattyWidgets\n- Напишите сюда ваши задачи или мысли.\n- Заметка сохраняется автоматически!";

    public string NoteText
    {
        get => _noteText;
        set
        {
            if (_noteText != value)
            {
                _noteText = value;
                OnPropertyChanged();
                SaveNote();
            }
        }
    }

    public ICommand ClearCommand { get; }

    public StickyNotesWidgetViewModel(string instanceId, FrameworkElement? rootElement)
    {
        ClearCommand = new NativeRelayCommand(() => NoteText = "");

        string dir = Path.Combine(ChwPackageService.DataDir, instanceId);
        Directory.CreateDirectory(dir);
        _storageFile = Path.Combine(dir, "note.txt");

        if (File.Exists(_storageFile))
        {
            try { _noteText = File.ReadAllText(_storageFile); } catch { }
        }
    }

    private void SaveNote()
    {
        try { File.WriteAllText(_storageFile, _noteText); } catch { }
    }
}

// 10. Weather Widget ViewModel
public class ForecastDayItem
{
    public string DayName { get; set; } = "";
    public string Icon { get; set; } = "⛅";
    public string Temp { get; set; } = "+15°";
}

public class WeatherWidgetViewModel : NativeViewModelBase
{
    public string City => "Москва";
    public string Temperature => "+14°C";
    public string Condition => "Переменная облачность";
    public string WeatherIcon => "⛅";
    public string Humidity => "64%";
    public string WindSpeed => "3.4 м/с";
    public string Pressure => "752 мм";

    public ObservableCollection<ForecastDayItem> Forecast { get; } = new();

    public WeatherWidgetViewModel()
    {
        Forecast.Add(new ForecastDayItem { DayName = "СБ", Icon = "☀️", Temp = "+16°" });
        Forecast.Add(new ForecastDayItem { DayName = "ВС", Icon = "⛅", Temp = "+14°" });
        Forecast.Add(new ForecastDayItem { DayName = "ПН", Icon = "🌧️", Temp = "+11°" });
        Forecast.Add(new ForecastDayItem { DayName = "ВТ", Icon = "⛅", Temp = "+13°" });
    }
}
