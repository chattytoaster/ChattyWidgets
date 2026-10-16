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
            "com.chatty.clock-glass" => new ClockWidgetViewModel(),
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

// 0. Clock ViewModel
public class ClockWidgetViewModel : NativeViewModelBase
{
    private readonly DispatcherTimer _timer;
    private bool _is24Hour = true;
    private bool _showSeconds = true;
    private bool _showDayName = true;
    private string _dateLabel = "";
    private string _timeDigits = "";
    private string _secondsBadge = "";
    private string _dayName = "";
    private string _weatherHint = "";

    public bool Is24Hour
    {
        get => _is24Hour;
        set { _is24Hour = value; OnPropertyChanged(); UpdateTime(); }
    }

    public bool ShowSeconds
    {
        get => _showSeconds;
        set { _showSeconds = value; OnPropertyChanged(); UpdateTime(); }
    }

    public bool ShowDayName
    {
        get => _showDayName;
        set { _showDayName = value; OnPropertyChanged(); UpdateTime(); }
    }

    public string DateLabel
    {
        get => _dateLabel;
        private set { _dateLabel = value; OnPropertyChanged(); }
    }

    public string TimeDigits
    {
        get => _timeDigits;
        private set { _timeDigits = value; OnPropertyChanged(); }
    }

    public string SecondsBadge
    {
        get => _secondsBadge;
        private set { _secondsBadge = value; OnPropertyChanged(); }
    }

    public string DayName
    {
        get => _dayName;
        private set { _dayName = value; OnPropertyChanged(); }
    }

    public string WeatherHint
    {
        get => _weatherHint;
        set { _weatherHint = value; OnPropertyChanged(); }
    }

    public ClockWidgetViewModel()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (s, e) => UpdateTime();
        _timer.Start();
        UpdateTime();
    }

    private void UpdateTime()
    {
        var now = DateTime.Now;
        var ru = new System.Globalization.CultureInfo("ru-RU");

        TimeDigits = _is24Hour ? now.ToString("HH:mm") : now.ToString("hh:mm tt");
        SecondsBadge = _showSeconds ? now.ToString(":ss") : "";
        DateLabel = now.ToString("d MMMM, ddd", ru);
        DayName = _showDayName ? ru.TextInfo.ToTitleCase(now.ToString("dddd", ru)) : "";
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

    public string MonthYear { get => _displayDate.ToString("MMMM yyyy", new System.Globalization.CultureInfo("ru-RU")).ToUpper(); set {} }
    public string TodayFull { get => DateTime.Today.ToString("d MMMM, dddd", new System.Globalization.CultureInfo("ru-RU")); set {} }
    public string CurrentDayNumber { get => DateTime.Today.Day.ToString(); set {} }
    public string CurrentWeekday { get => DateTime.Today.ToString("dddd", new System.Globalization.CultureInfo("ru-RU")); set {} }

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
    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
    private readonly DispatcherTimer _timer;

    public ObservableCollection<CryptoItem> Items { get; } = new();

    public CryptoWidgetViewModel()
    {
        // Initial defaults
        Items.Add(new CryptoItem { Name = "Bitcoin", Symbol = "BTC", Price = "$81,950", Change = "+1.8%", IsPositive = true, Icon = "₿" });
        Items.Add(new CryptoItem { Name = "Ethereum", Symbol = "ETH", Price = "$2,460", Change = "+3.2%", IsPositive = true, Icon = "Ξ" });
        Items.Add(new CryptoItem { Name = "Solana", Symbol = "SOL", Price = "$105.40", Change = "+2.5%", IsPositive = true, Icon = "◎" });
        Items.Add(new CryptoItem { Name = "Toncoin", Symbol = "TON", Price = "$5.35", Change = "+1.2%", IsPositive = true, Icon = "💎" });
        Items.Add(new CryptoItem { Name = "Доллар США", Symbol = "USD/RUB", Price = "83.76 ₽", Change = "-0.19", IsPositive = false, Icon = "$" });
        Items.Add(new CryptoItem { Name = "Евро", Symbol = "EUR/RUB", Price = "93.77 ₽", Change = "-0.15", IsPositive = false, Icon = "€" });

        _ = FetchPricesAsync();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _timer.Tick += (s, e) => _ = FetchPricesAsync();
        _timer.Start();
    }

    public async Task FetchPricesAsync()
    {
        try
        {
            // 1. Fetch Binance 24hr ticker
            string binanceJson = await _httpClient.GetStringAsync("https://api.binance.com/api/v3/ticker/24hr?symbols=[\"BTCUSDT\",\"ETHUSDT\",\"SOLUSDT\"]");
            using var bDoc = JsonDocument.Parse(binanceJson);
            
            decimal btcPrice = 0, btcChange = 0;
            decimal ethPrice = 0, ethChange = 0;
            decimal solPrice = 0, solChange = 0;

            foreach (var elem in bDoc.RootElement.EnumerateArray())
            {
                string sym = elem.GetProperty("symbol").GetString() ?? "";
                decimal p = decimal.TryParse(elem.GetProperty("lastPrice").GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lp) ? lp : 0;
                decimal c = decimal.TryParse(elem.GetProperty("priceChangePercent").GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var cp) ? cp : 0;

                if (sym == "BTCUSDT") { btcPrice = p; btcChange = c; }
                else if (sym == "ETHUSDT") { ethPrice = p; ethChange = c; }
                else if (sym == "SOLUSDT") { solPrice = p; solChange = c; }
            }

            // 2. Fetch CBR
            decimal usdVal = 0, usdDiff = 0;
            decimal eurVal = 0, eurDiff = 0;
            try
            {
                string cbrJson = await _httpClient.GetStringAsync("https://www.cbr-xml-daily.ru/daily_json.js");
                using var cDoc = JsonDocument.Parse(cbrJson);
                var valute = cDoc.RootElement.GetProperty("Valute");
                if (valute.TryGetProperty("USD", out var usdElem))
                {
                    usdVal = usdElem.GetProperty("Value").GetDecimal();
                    usdDiff = usdVal - usdElem.GetProperty("Previous").GetDecimal();
                }
                if (valute.TryGetProperty("EUR", out var eurElem))
                {
                    eurVal = eurElem.GetProperty("Value").GetDecimal();
                    eurDiff = eurVal - eurElem.GetProperty("Previous").GetDecimal();
                }
            }
            catch { }

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                Items.Clear();
                if (btcPrice > 0)
                {
                    Items.Add(new CryptoItem
                    {
                        Name = "Bitcoin", Symbol = "BTC",
                        Price = $"${btcPrice:N0}",
                        Change = (btcChange >= 0 ? "+" : "") + $"{btcChange:F2}%",
                        IsPositive = btcChange >= 0,
                        Icon = "₿"
                    });
                }
                if (ethPrice > 0)
                {
                    Items.Add(new CryptoItem
                    {
                        Name = "Ethereum", Symbol = "ETH",
                        Price = $"${ethPrice:N0}",
                        Change = (ethChange >= 0 ? "+" : "") + $"{ethChange:F2}%",
                        IsPositive = ethChange >= 0,
                        Icon = "Ξ"
                    });
                }
                if (solPrice > 0)
                {
                    Items.Add(new CryptoItem
                    {
                        Name = "Solana", Symbol = "SOL",
                        Price = $"${solPrice:F2}",
                        Change = (solChange >= 0 ? "+" : "") + $"{solChange:F2}%",
                        IsPositive = solChange >= 0,
                        Icon = "◎"
                    });
                }
                Items.Add(new CryptoItem { Name = "Toncoin", Symbol = "TON", Price = "$5.35", Change = "+1.4%", IsPositive = true, Icon = "💎" });
                if (usdVal > 0)
                {
                    Items.Add(new CryptoItem
                    {
                        Name = "Доллар США", Symbol = "USD/RUB",
                        Price = $"{usdVal:F2} ₽",
                        Change = (usdDiff >= 0 ? "+" : "") + $"{usdDiff:F2}",
                        IsPositive = usdDiff >= 0,
                        Icon = "$"
                    });
                }
                if (eurVal > 0)
                {
                    Items.Add(new CryptoItem
                    {
                        Name = "Евро", Symbol = "EUR/RUB",
                        Price = $"{eurVal:F2} ₽",
                        Change = (eurDiff >= 0 ? "+" : "") + $"{eurDiff:F2}",
                        IsPositive = eurDiff >= 0,
                        Icon = "€"
                    });
                }
            });
        }
        catch { }
    }
}

// 3. GPU Monitor ViewModel
public class GpuMonitorWidgetViewModel : NativeViewModelBase
{
    public SystemMonitorService Monitor => SystemMonitorService.Instance;

    private double _gpuPercent;
    public double GpuPercent
    {
        get => _gpuPercent;
        set { _gpuPercent = value; OnPropertyChanged(); }
    }

    private double _gpuMemoryPercent;
    public double GpuMemoryPercent
    {
        get => _gpuMemoryPercent;
        set { _gpuMemoryPercent = value; OnPropertyChanged(); }
    }

    private double _gpuTemp;
    public double GpuTemp
    {
        get => _gpuTemp;
        set { _gpuTemp = value; OnPropertyChanged(); }
    }

    private string _gpuName = "GPU";
    public string GpuName
    {
        get => _gpuName;
        set { _gpuName = value; OnPropertyChanged(); }
    }

    public GpuMonitorWidgetViewModel()
    {
        UpdateFrom(Monitor.CurrentMetrics);
        Monitor.MetricsUpdated += (metrics) =>
        {
            Application.Current?.Dispatcher?.Invoke(() => UpdateFrom(metrics));
        };
    }

    private void UpdateFrom(SystemMetrics metrics)
    {
        if (metrics == null) return;
        GpuName = metrics.GpuName;
        GpuPercent = metrics.GpuPercent;
        GpuMemoryPercent = metrics.GpuMemoryPercent;
        GpuTemp = metrics.GpuTemperature;
    }
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

    public void SetCustomFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;
        _images.Clear();
        foreach (var ext in new[] { "*.jpg", "*.png", "*.jpeg", "*.webp", "*.bmp" })
        {
            try { _images.AddRange(Directory.GetFiles(folderPath, ext)); } catch { }
        }
        if (_images.Count > 0)
        {
            ShowImage(0);
        }
        else
        {
            CurrentImage = "";
            Caption = "В папке нет поддерживаемых изображений";
        }
    }
}

// 5. Music Player ViewModel
public class MusicPlayerWidgetViewModel : NativeViewModelBase
{
    private readonly DispatcherTimer _spinTimer;
    private double _vinylAngle = 0;
    public double VinylAngle
    {
        get => _vinylAngle;
        set { _vinylAngle = value; OnPropertyChanged(); }
    }

    private bool _isPlaying = false;
    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (_isPlaying != value)
            {
                _isPlaying = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PlayPauseIcon));
                if (_isPlaying)
                {
                    _spinTimer.Start();
                }
                else
                {
                    _spinTimer.Stop();
                }
            }
        }
    }

    public string PlayPauseIcon => IsPlaying ? "⏸" : "▶";

    private string _trackTitle = "Ожидание медиа...";
    public string TrackTitle
    {
        get => _trackTitle;
        set { _trackTitle = value; OnPropertyChanged(); }
    }

    private string _artist = "Запустите Spotify, Браузер...";
    public string Artist
    {
        get => _artist;
        set { _artist = value; OnPropertyChanged(); }
    }

    private double _progressPercent = 40;
    public double ProgressPercent
    {
        get => _progressPercent;
        set { _progressPercent = value; OnPropertyChanged(); }
    }

    private System.Windows.Media.ImageSource? _coverBitmap;
    public System.Windows.Media.ImageSource? CoverBitmap
    {
        get => _coverBitmap;
        set { _coverBitmap = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasCover)); OnPropertyChanged(nameof(CoverVisibility)); }
    }

    public bool HasCover => _coverBitmap != null;
    public Visibility CoverVisibility => _coverBitmap != null ? Visibility.Visible : Visibility.Collapsed;

    public ICommand PlayPauseCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand PrevCommand { get; }
    public ICommand VolumeUpCommand { get; }
    public ICommand VolumeDownCommand { get; }

    public MusicPlayerWidgetViewModel()
    {
        _spinTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(30)
        };
        _spinTimer.Tick += (s, e) =>
        {
            VinylAngle = (_vinylAngle + 1.2) % 360.0;
        };
        if (_isPlaying) _spinTimer.Start();

        PlayPauseCommand = new NativeRelayCommand(() =>
        {
            MediaMonitorService.Instance.PlayPause();
            IsPlaying = !IsPlaying;
        });
        NextCommand = new NativeRelayCommand(() => MediaMonitorService.Instance.Next());
        PrevCommand = new NativeRelayCommand(() => MediaMonitorService.Instance.Previous());
        VolumeUpCommand = new NativeRelayCommand(() => MediaMonitorService.Instance.VolumeUp());
        VolumeDownCommand = new NativeRelayCommand(() => MediaMonitorService.Instance.VolumeDown());

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
                    if (!string.IsNullOrEmpty(info.ThumbnailBase64))
                    {
                        CoverBitmap = LoadBitmapFromBase64(info.ThumbnailBase64);
                    }
                    else
                    {
                        CoverBitmap = null;
                    }
                });
            };
            var cur = MediaMonitorService.Instance.CurrentMedia;
            if (cur != null)
            {
                if (!string.IsNullOrEmpty(cur.Title)) TrackTitle = cur.Title;
                if (!string.IsNullOrEmpty(cur.Artist)) Artist = cur.Artist;
                IsPlaying = cur.IsPlaying;
                if (!string.IsNullOrEmpty(cur.ThumbnailBase64))
                {
                    CoverBitmap = LoadBitmapFromBase64(cur.ThumbnailBase64);
                }
            }
        }
        catch { }
    }

    private static System.Windows.Media.ImageSource? LoadBitmapFromBase64(string base64)
    {
        if (string.IsNullOrEmpty(base64) || base64.Length < 20) return null;
        try
        {
            int comma = base64.IndexOf(',');
            if (comma >= 0) base64 = base64.Substring(comma + 1);
            byte[] bytes = Convert.FromBase64String(base64);
            using var ms = new MemoryStream(bytes);
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }
}

// 6. Pomodoro Timer ViewModel
public class PomodoroWidgetViewModel : NativeViewModelBase
{
    private readonly DispatcherTimer _timer;
    private int _focusMinutes = 25;
    private int _remainingSeconds = 25 * 60;
    private bool _isRunning = false;
    private bool _isFocusMode = true;

    public string TimeText { get => $"{_remainingSeconds / 60:D2}:{_remainingSeconds % 60:D2}"; set {} }
    public string ModeText { get => _isFocusMode ? "🎯 ФОКУС" : "☕ ОТДЫХ"; set {} }
    public string ActionButtonText { get => _isRunning ? "Пауза" : "Старт"; set {} }
    public string AccentColor { get => _isFocusMode ? "#EF4444" : "#10B981"; set {} }

    public double ProgressPercent
    {
        get
        {
            int total = (_isFocusMode ? _focusMinutes : 5) * 60;
            return total > 0 ? ((double)(total - _remainingSeconds) / total) * 100 : 0;
        }
        set {}
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
                _remainingSeconds = (_isFocusMode ? _focusMinutes : 5) * 60;
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
            _remainingSeconds = (_isFocusMode ? _focusMinutes : 5) * 60;
            OnPropertyChanged(nameof(TimeText));
            OnPropertyChanged(nameof(ActionButtonText));
            OnPropertyChanged(nameof(ProgressPercent));
        });

        SwitchModeCommand = new NativeRelayCommand(() =>
        {
            _timer.Stop();
            _isRunning = false;
            _isFocusMode = !_isFocusMode;
            _remainingSeconds = (_isFocusMode ? _focusMinutes : 5) * 60;
            OnPropertyChanged(nameof(TimeText));
            OnPropertyChanged(nameof(ModeText));
            OnPropertyChanged(nameof(ActionButtonText));
            OnPropertyChanged(nameof(AccentColor));
            OnPropertyChanged(nameof(ProgressPercent));
        });
    }

    public void SetWorkMinutes(int mins)
    {
        _focusMinutes = Math.Max(1, mins);
        if (_isFocusMode)
        {
            _timer.Stop();
            _isRunning = false;
            _remainingSeconds = _focusMinutes * 60;
            OnPropertyChanged(nameof(TimeText));
            OnPropertyChanged(nameof(ActionButtonText));
            OnPropertyChanged(nameof(ProgressPercent));
        }
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
    public string FeedTitle => "НОВОСТИ И IT";
    public ObservableCollection<RssArticleItem> Articles { get; } = new();

    private int _currentIndex = 0;
    public RssArticleItem? CurrentArticle => Articles.Count > 0 ? Articles[_currentIndex] : null;
    public string CounterText => Articles.Count > 0 ? $"{_currentIndex + 1} / {Articles.Count}" : "0 / 0";

    public ICommand NextSlideCommand { get; }
    public ICommand PrevSlideCommand { get; }
    public ICommand RefreshCommand { get; }

    public RssWidgetViewModel()
    {
        Articles.Add(new RssArticleItem { Title = "OpenAI и DeepMind анонсировали новые мультимодальные модели следующего поколения", Source = "AI & TECH", Time = "10 мин назад", Url = "https://news.ycombinator.com" });
        Articles.Add(new RssArticleItem { Title = "Microsoft представила новые визуальные API для Windows 11 с нативной поддержкой Mica", Source = "WINDOWS", Time = "42 мин назад", Url = "https://blogs.windows.com" });
        Articles.Add(new RssArticleItem { Title = "Сеть TON и Gram установили рекорд скорости транзакций и активных кошельков", Source = "КРИПТА", Time = "1 час назад", Url = "https://ton.org" });
        Articles.Add(new RssArticleItem { Title = "Релиз ChattyWidgets 2.0: Полный переход на нативный стек и сверхбыстрые виджеты", Source = "CHATTY", Time = "2 часа назад", Url = "https://github.com/chattytoaster/ChattyWidgets" });

        foreach (var a in Articles)
        {
            a.OpenCommand = new NativeRelayCommand(() => {
                try { Process.Start(new ProcessStartInfo(a.Url) { UseShellExecute = true }); } catch { }
            });
        }

        NextSlideCommand = new NativeRelayCommand(() =>
        {
            if (Articles.Count == 0) return;
            _currentIndex = (_currentIndex + 1) % Articles.Count;
            OnPropertyChanged(nameof(CurrentArticle));
            OnPropertyChanged(nameof(CounterText));
        });

        PrevSlideCommand = new NativeRelayCommand(() =>
        {
            if (Articles.Count == 0) return;
            _currentIndex = (_currentIndex - 1 + Articles.Count) % Articles.Count;
            OnPropertyChanged(nameof(CurrentArticle));
            OnPropertyChanged(nameof(CounterText));
        });

        RefreshCommand = new NativeRelayCommand(() =>
        {
            _currentIndex = 0;
            OnPropertyChanged(nameof(CurrentArticle));
            OnPropertyChanged(nameof(CounterText));
        });

        var autoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        autoTimer.Tick += (s, e) =>
        {
            if (Articles.Count == 0) return;
            _currentIndex = (_currentIndex + 1) % Articles.Count;
            OnPropertyChanged(nameof(CurrentArticle));
            OnPropertyChanged(nameof(CounterText));
        };
        autoTimer.Start();
    }
}

// 9. Sticky Notes ViewModel (Tasks checklist exactly matching original design)
public class NoteTaskItem : NativeViewModelBase
{
    private string _text = "";
    public string Text { get => _text; set { _text = value; OnPropertyChanged(); } }

    private bool _isDone;
    public bool IsDone
    {
        get => _isDone;
        set
        {
            _isDone = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TextDecoration));
            OnPropertyChanged(nameof(TextColor));
            Toggled?.Invoke();
        }
    }

    public TextDecorationCollection? TextDecoration => IsDone ? TextDecorations.Strikethrough : null;
    public string TextColor => IsDone ? "#64748B" : "#E2E8F0";

    public ICommand DeleteCommand { get; set; } = null!;
    public Action? Toggled { get; set; }
}

public class StickyNotesWidgetViewModel : NativeViewModelBase
{
    private readonly string _storageFile;
    public ObservableCollection<NoteTaskItem> Tasks { get; } = new();

    private string _newTaskText = "";
    public string NewTaskText
    {
        get => _newTaskText;
        set { _newTaskText = value; OnPropertyChanged(); }
    }

    public string ActiveCountText => $"{Tasks.Count(t => !t.IsDone)} активных";

    public ICommand AddTaskCommand { get; }

    public StickyNotesWidgetViewModel(string instanceId, FrameworkElement? rootElement)
    {
        string dir = Path.Combine(ChwPackageService.DataDir, instanceId);
        Directory.CreateDirectory(dir);
        _storageFile = Path.Combine(dir, "tasks.json");

        AddTaskCommand = new NativeRelayCommand(() =>
        {
            if (string.IsNullOrWhiteSpace(NewTaskText)) return;
            var item = new NoteTaskItem { Text = NewTaskText.Trim(), IsDone = false };
            item.DeleteCommand = new NativeRelayCommand(() => RemoveTask(item));
            item.Toggled = () => { Save(); OnPropertyChanged(nameof(ActiveCountText)); };
            Tasks.Add(item);
            NewTaskText = "";
            Save();
            OnPropertyChanged(nameof(ActiveCountText));
        });

        Load();
    }

    private void RemoveTask(NoteTaskItem item)
    {
        Tasks.Remove(item);
        Save();
        OnPropertyChanged(nameof(ActiveCountText));
    }

    public void ClearCompletedTasks()
    {
        var doneItems = Tasks.Where(t => t.IsDone).ToList();
        foreach (var item in doneItems)
        {
            Tasks.Remove(item);
        }
        Save();
        OnPropertyChanged(nameof(ActiveCountText));
    }

    private void Save()
    {
        try
        {
            var raw = Tasks.Select(t => new { text = t.Text, done = t.IsDone }).ToList();
            File.WriteAllText(_storageFile, JsonSerializer.Serialize(raw));
        }
        catch { }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_storageFile))
            {
                string json = File.ReadAllText(_storageFile);
                var list = JsonSerializer.Deserialize<List<JsonElement>>(json);
                if (list != null)
                {
                    foreach (var elem in list)
                    {
                        string text = elem.TryGetProperty("text", out var pText) ? pText.GetString() ?? "" : "";
                        bool done = elem.TryGetProperty("done", out var pDone) && pDone.GetBoolean();
                        if (!string.IsNullOrEmpty(text))
                        {
                            var item = new NoteTaskItem { Text = text, IsDone = done };
                            item.DeleteCommand = new NativeRelayCommand(() => RemoveTask(item));
                            item.Toggled = () => { Save(); OnPropertyChanged(nameof(ActiveCountText)); };
                            Tasks.Add(item);
                        }
                    }
                }
            }
        }
        catch { }

        if (Tasks.Count == 0)
        {
            var t1 = new NoteTaskItem { Text = "Создать свой виджет (.chw)", IsDone = false };
            t1.DeleteCommand = new NativeRelayCommand(() => RemoveTask(t1));
            t1.Toggled = () => { Save(); OnPropertyChanged(nameof(ActiveCountText)); };
            Tasks.Add(t1);

            var t2 = new NoteTaskItem { Text = "Открыть магазин ChattyWidgets", IsDone = true };
            t2.DeleteCommand = new NativeRelayCommand(() => RemoveTask(t2));
            t2.Toggled = () => { Save(); OnPropertyChanged(nameof(ActiveCountText)); };
            Tasks.Add(t2);

            var t3 = new NoteTaskItem { Text = "Настроить слой на рабочем столе", IsDone = false };
            t3.DeleteCommand = new NativeRelayCommand(() => RemoveTask(t3));
            t3.Toggled = () => { Save(); OnPropertyChanged(nameof(ActiveCountText)); };
            Tasks.Add(t3);
        }
        OnPropertyChanged(nameof(ActiveCountText));
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
    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
    private readonly DispatcherTimer _timer;

    private string _city = "Москва";
    private double _lat = 55.7558;
    private double _lon = 37.6173;

    private string _temperature = "+14°";
    private string _condition = "Ясно";
    private string _weatherIcon = "☀️";
    private string _feelsLike = "Ощущается как +12°";
    private string _humidity = "64%";
    private string _windSpeed = "3.4 м/с";
    private string _pressure = "752 мм";
    private string _minMax = "+8° / +16°";
    private string _rain = "0 мм";
    private string _updateTime = "Обновлено в " + DateTime.Now.ToString("HH:mm");

    public string City { get => _city; set { _city = value; OnPropertyChanged(); } }
    public string Temperature { get => _temperature; set { _temperature = value; OnPropertyChanged(); } }
    public string Condition { get => _condition; set { _condition = value; OnPropertyChanged(); } }
    public string WeatherIcon { get => _weatherIcon; set { _weatherIcon = value; OnPropertyChanged(); } }
    public string FeelsLike { get => _feelsLike; set { _feelsLike = value; OnPropertyChanged(); } }
    public string Humidity { get => _humidity; set { _humidity = value; OnPropertyChanged(); } }
    public string WindSpeed { get => _windSpeed; set { _windSpeed = value; OnPropertyChanged(); } }
    public string Pressure { get => _pressure; set { _pressure = value; OnPropertyChanged(); } }
    public string MinMax { get => _minMax; set { _minMax = value; OnPropertyChanged(); } }
    public string Rain { get => _rain; set { _rain = value; OnPropertyChanged(); } }
    public string UpdateTime { get => _updateTime; set { _updateTime = value; OnPropertyChanged(); } }

    public ObservableCollection<ForecastDayItem> Forecast { get; } = new();

    public WeatherWidgetViewModel()
    {
        Forecast.Add(new ForecastDayItem { DayName = "СБ", Icon = "☀️", Temp = "+16°" });
        Forecast.Add(new ForecastDayItem { DayName = "ВС", Icon = "⛅", Temp = "+14°" });
        Forecast.Add(new ForecastDayItem { DayName = "ПН", Icon = "🌧️", Temp = "+11°" });
        Forecast.Add(new ForecastDayItem { DayName = "ВТ", Icon = "⛅", Temp = "+13°" });

        _ = RefreshWeatherAsync();

        // Periodic refresh every 15 minutes
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
        _timer.Tick += (s, e) => _ = RefreshWeatherAsync();
        _timer.Start();
    }

    public void SetCity(string cityName, double lat, double lon)
    {
        City = cityName;
        _lat = lat;
        _lon = lon;
        _ = RefreshWeatherAsync();
    }

    public async Task RefreshWeatherAsync()
    {
        try
        {
            string url = FormattableString.Invariant(
                $"https://api.open-meteo.com/v1/forecast?latitude={_lat:F4}&longitude={_lon:F4}&current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,surface_pressure,wind_speed_10m&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=auto"
            );

            string json = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("current", out var cur))
            {
                double temp = cur.GetProperty("temperature_2m").GetDouble();
                double apparent = cur.GetProperty("apparent_temperature").GetDouble();
                int wCode = cur.GetProperty("weather_code").GetInt32();
                int hum = cur.GetProperty("relative_humidity_2m").GetInt32();
                double wind = cur.GetProperty("wind_speed_10m").GetDouble();
                double pressHpa = cur.GetProperty("surface_pressure").GetDouble();
                double prec = cur.GetProperty("precipitation").GetDouble();

                var (wDesc, wIcon) = GetWmoInfo(wCode);
                double windMs = wind / 3.6;
                int pressMm = (int)Math.Round(pressHpa * 0.750062);

                string minMaxStr = "";
                var newForecast = new List<ForecastDayItem>();

                if (root.TryGetProperty("daily", out var daily))
                {
                    var times = daily.GetProperty("time").EnumerateArray().ToList();
                    var codes = daily.GetProperty("weather_code").EnumerateArray().ToList();
                    var maxs = daily.GetProperty("temperature_2m_max").EnumerateArray().ToList();
                    var mins = daily.GetProperty("temperature_2m_min").EnumerateArray().ToList();

                    if (maxs.Count > 0 && mins.Count > 0)
                    {
                        double todayMin = mins[0].GetDouble();
                        double todayMax = maxs[0].GetDouble();
                        minMaxStr = $"{(todayMin > 0 ? "+" : "")}{Math.Round(todayMin)}° / {(todayMax > 0 ? "+" : "")}{Math.Round(todayMax)}°";
                    }

                    for (int i = 1; i < Math.Min(5, times.Count); i++)
                    {
                        if (DateTime.TryParse(times[i].GetString(), out var dt))
                        {
                            string dayName = dt.ToString("ddd", new System.Globalization.CultureInfo("ru-RU")).ToUpper();
                            int c = codes[i].GetInt32();
                            var (_, icon) = GetWmoInfo(c);
                            double mx = maxs[i].GetDouble();
                            newForecast.Add(new ForecastDayItem
                            {
                                DayName = dayName,
                                Icon = icon,
                                Temp = $"{(mx > 0 ? "+" : "")}{Math.Round(mx)}°"
                            });
                        }
                    }
                }

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    Temperature = $"{(temp > 0 ? "+" : "")}{Math.Round(temp)}°";
                    FeelsLike = $"Ощущается как {(apparent > 0 ? "+" : "")}{Math.Round(apparent)}°";
                    Condition = wDesc;
                    WeatherIcon = wIcon;
                    Humidity = $"{hum}%";
                    WindSpeed = $"{windMs:F1} м/с";
                    Pressure = $"{pressMm} мм";
                    Rain = $"{prec:F1} мм";
                    if (!string.IsNullOrEmpty(minMaxStr)) MinMax = minMaxStr;
                    UpdateTime = "Обновлено в " + DateTime.Now.ToString("HH:mm");

                    if (newForecast.Count > 0)
                    {
                        Forecast.Clear();
                        foreach (var f in newForecast) Forecast.Add(f);
                    }
                });
            }
        }
        catch { }
    }

    private static (string desc, string icon) GetWmoInfo(int code)
    {
        return code switch
        {
            0 => ("Ясно", "☀️"),
            1 => ("Преимущественно ясно", "🌤️"),
            2 => ("Переменная облачность", "⛅"),
            3 => ("Пасмурно", "☁️"),
            45 or 48 => ("Туман", "🌫️"),
            51 or 53 or 55 => ("Морось", "🌦️"),
            61 or 63 or 65 => ("Дождь", "🌧️"),
            71 or 73 or 75 => ("Снегопад", "❄️"),
            77 => ("Снег", "❄️"),
            80 or 81 or 82 => ("Ливневый дождь", "🌧️"),
            85 or 86 => ("Снежный ливень", "🌨️"),
            95 => ("Гроза", "⛈️"),
            96 or 99 => ("Гроза с градом", "⛈️"),
            _ => ("Облачно", "⛅")
        };
    }
}
