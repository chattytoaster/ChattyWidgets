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
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;

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
            "com.chatty.calendar-glass" => new CalendarWidgetViewModel(instanceId),
            "com.chatty.clock-glass" => new ClockWidgetViewModel(instanceId),
            "com.chatty.crypto-tracker" => new CryptoWidgetViewModel(instanceId),
            "com.chatty.gpu-monitor" => new GpuMonitorWidgetViewModel(instanceId),
            "com.chatty.media-frame" => new MediaFrameWidgetViewModel(instanceId),
            "com.chatty.music-player" => new MusicPlayerWidgetViewModel(instanceId),
            "com.chatty.pomodoro-timer" => new PomodoroWidgetViewModel(instanceId),
            "com.chatty.quick-dock" => new QuickDockWidgetViewModel(instanceId),
            "com.chatty.rss-reader" => new RssWidgetViewModel(instanceId),
            "com.chatty.sticky-notes" => new StickyNotesWidgetViewModel(instanceId, rootElement),
            "com.chatty.weather-card" => new WeatherWidgetViewModel(instanceId),
            _ => SystemMonitorService.Instance
        };
    }
}

// 0. Clock ViewModel
public class ClockWidgetViewModel : NativeViewModelBase
{
    private readonly DispatcherTimer _timer;
    private readonly string? _instanceId;
    private bool _is24Hour = true;
    private bool _showSeconds = true;
    private bool _showDayName = true;
    private string _timezoneId = "local";
    private string _cityName = "";
    private string _dateLabel = "";
    private string _timeDigits = "";
    private string _secondsBadge = "";
    private string _dayName = "";
    private string _weatherHint = "";

    public string TimezoneId
    {
        get => _timezoneId;
        set { _timezoneId = value; OnPropertyChanged(); SaveCustomSettings(); UpdateTime(); }
    }

    public string CityName
    {
        get => _cityName;
        set { _cityName = value; OnPropertyChanged(); SaveCustomSettings(); UpdateTime(); }
    }

    public bool Is24Hour
    {
        get => _is24Hour;
        set { _is24Hour = value; OnPropertyChanged(); SaveCustomSettings(); UpdateTime(); }
    }

    public bool ShowSeconds
    {
        get => _showSeconds;
        set { _showSeconds = value; OnPropertyChanged(); SaveCustomSettings(); UpdateTime(); }
    }

    public bool ShowDayName
    {
        get => _showDayName;
        set { _showDayName = value; OnPropertyChanged(); SaveCustomSettings(); UpdateTime(); }
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

    public ClockWidgetViewModel() : this(null) { }

    public ClockWidgetViewModel(string? instanceId)
    {
        _instanceId = instanceId;
        LoadCustomSettings();

        LocalizationService.Instance.LanguageChanged += () =>
        {
            Application.Current?.Dispatcher?.Invoke(UpdateTime);
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (s, e) => UpdateTime();
        _timer.Start();
        UpdateTime();
    }

    public void SetTimezone(string tzId, string cityName)
    {
        _timezoneId = tzId;
        _cityName = cityName;
        SaveCustomSettings();
        UpdateTime();
    }

    public void LoadCustomSettings()
    {
        if (!string.IsNullOrEmpty(_instanceId) &&
            WidgetManager.Instance.Settings.Widgets.TryGetValue(_instanceId, out var s))
        {
            if (s.CustomSettings.TryGetValue("clockTimezone", out var tz) && !string.IsNullOrWhiteSpace(tz))
                _timezoneId = tz;
            if (s.CustomSettings.TryGetValue("clockCityName", out var cn) && !string.IsNullOrWhiteSpace(cn))
                _cityName = cn;
            if (s.CustomSettings.TryGetValue("clock24Hour", out var h24) && bool.TryParse(h24, out var b24))
                _is24Hour = b24;
            if (s.CustomSettings.TryGetValue("clockShowSeconds", out var ss) && bool.TryParse(ss, out var bSs))
                _showSeconds = bSs;
            if (s.CustomSettings.TryGetValue("clockShowDayName", out var sdn) && bool.TryParse(sdn, out var bSdn))
                _showDayName = bSdn;
        }
    }

    public void SaveCustomSettings()
    {
        if (string.IsNullOrEmpty(_instanceId)) return;
        if (!WidgetManager.Instance.Settings.Widgets.TryGetValue(_instanceId, out var s)) return;

        s.CustomSettings["clockTimezone"] = _timezoneId;
        s.CustomSettings["clockCityName"] = _cityName;
        s.CustomSettings["clock24Hour"] = _is24Hour ? "true" : "false";
        s.CustomSettings["clockShowSeconds"] = _showSeconds ? "true" : "false";
        s.CustomSettings["clockShowDayName"] = _showDayName ? "true" : "false";
        WidgetManager.Instance.SaveSettings();
    }

    private void UpdateTime()
    {
        DateTime now;
        if (!string.IsNullOrEmpty(_timezoneId) && !string.Equals(_timezoneId, "local", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                TimeZoneInfo tzi;
                try
                {
                    tzi = TimeZoneInfo.FindSystemTimeZoneById(_timezoneId);
                }
                catch
                {
                    string winId = _timezoneId switch
                    {
                        "Europe/London" => "GMT Standard Time",
                        "Europe/Paris" => "W. Europe Standard Time",
                        "Europe/Berlin" => "W. Europe Standard Time",
                        "Europe/Moscow" => "Russian Standard Time",
                        "Asia/Dubai" => "Arabian Standard Time",
                        "Asia/Tokyo" => "Tokyo Standard Time",
                        "Asia/Hong_Kong" => "China Standard Time",
                        "Asia/Singapore" => "Singapore Standard Time",
                        "Australia/Sydney" => "AUS Eastern Standard Time",
                        "America/New_York" => "Eastern Standard Time",
                        "America/Los_Angeles" => "Pacific Standard Time",
                        "UTC" => "UTC",
                        _ => _timezoneId
                    };
                    tzi = TimeZoneInfo.FindSystemTimeZoneById(winId);
                }
                now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tzi);
            }
            catch
            {
                now = DateTime.Now;
            }
        }
        else
        {
            now = DateTime.Now;
        }

        bool isRu = LocalizationService.Instance.IsRussian;
        var culture = isRu ? new System.Globalization.CultureInfo("ru-RU") : System.Globalization.CultureInfo.InvariantCulture;

        TimeDigits = _is24Hour ? now.ToString("HH:mm") : now.ToString("hh:mm tt");
        SecondsBadge = _showSeconds ? now.ToString(":ss") : "";

        string baseDate = now.ToString(isRu ? "d MMMM, ddd" : "dddd, MMMM d", culture);
        if (!string.IsNullOrEmpty(_cityName) && !string.Equals(_timezoneId, "local", StringComparison.OrdinalIgnoreCase))
        {
            DateLabel = $"{_cityName.ToUpper()} • {baseDate}";
        }
        else
        {
            DateLabel = baseDate;
        }

        DayName = _showDayName ? culture.TextInfo.ToTitleCase(now.ToString("dddd", culture)) : "";
    }
}

// 1. Calendar ViewModel
public class CalendarDayItem
{
    public int Day { get; set; }
    public string DayText => Day > 0 ? Day.ToString() : (IsWeekNumber ? WeekNumberText : "");
    public string WeekNumberText { get; set; } = "";
    public bool IsToday { get; set; }
    public bool IsCurrentMonth { get; set; }
    public bool IsWeekNumber { get; set; }
    public bool IsWeekend { get; set; }
    public bool HighlightToday { get; set; } = true;

    public string BgColor => (IsToday && HighlightToday) ? "#3B82F6" : "Transparent";
    public string FgColor
    {
        get
        {
            if (IsToday && HighlightToday) return "#FFFFFF";
            if (IsWeekNumber) return "#64748B";
            if (!IsCurrentMonth) return "#334155";
            if (IsWeekend) return "#FDA4AF";
            return "#E2E8F0";
        }
    }
    public FontWeight Weight => (IsToday && HighlightToday) ? FontWeights.Bold : FontWeights.Normal;
}

public class CalendarHeaderItem
{
    public string Text { get; set; } = "";
    public bool IsWeekend { get; set; }
    public bool IsWeekNumber { get; set; }
    public string FgColor => IsWeekNumber ? "#64748B" : (IsWeekend ? "#FDA4AF" : "#94A3B8");
}

public class CalendarWidgetViewModel : NativeViewModelBase
{
    private readonly string _instanceId;
    private DateTime _displayDate = DateTime.Today;
    private string _firstDayOfWeek = "monday";
    private bool _showWeekNumbers = false;
    private bool _highlightToday = true;

    public string FirstDayOfWeek
    {
        get => _firstDayOfWeek;
        set
        {
            string val = value?.ToLowerInvariant() == "sunday" ? "sunday" : "monday";
            if (_firstDayOfWeek != val)
            {
                _firstDayOfWeek = val;
                OnPropertyChanged();
                BuildMonth();
            }
        }
    }

    public bool ShowWeekNumbers
    {
        get => _showWeekNumbers;
        set
        {
            if (_showWeekNumbers != value)
            {
                _showWeekNumbers = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ColumnCount));
                BuildMonth();
            }
        }
    }

    public bool HighlightToday
    {
        get => _highlightToday;
        set
        {
            if (_highlightToday != value)
            {
                _highlightToday = value;
                OnPropertyChanged();
                BuildMonth();
            }
        }
    }

    public int ColumnCount => _showWeekNumbers ? 8 : 7;

    public string MonthYear
    {
        get
        {
            string monthName = LocalizationService.Instance.T($"Calendar.Month.{_displayDate.Month}");
            return $"{monthName} {_displayDate.Year}".ToUpper();
        }
    }

    public string TodayFull
    {
        get
        {
            var culture = LocalizationService.Instance.IsRussian
                ? new System.Globalization.CultureInfo("ru-RU")
                : new System.Globalization.CultureInfo("en-US");
            return DateTime.Today.ToString("d MMMM, dddd", culture);
        }
    }

    public string TodayButtonText => LocalizationService.Instance.T("Calendar.TodayBtn", "Сегодня");
    public string TodayLabel => LocalizationService.Instance.T("Calendar.TodayLabel", "📅 Сегодня:");
    public string CurrentDayNumber => DateTime.Today.Day.ToString();
    public string CurrentWeekday
    {
        get
        {
            var culture = LocalizationService.Instance.IsRussian
                ? new System.Globalization.CultureInfo("ru-RU")
                : new System.Globalization.CultureInfo("en-US");
            return DateTime.Today.ToString("dddd", culture);
        }
    }

    public ObservableCollection<CalendarHeaderItem> WeekHeaders { get; } = new();
    public ObservableCollection<CalendarDayItem> Days { get; } = new();

    public ICommand PrevMonthCommand { get; }
    public ICommand NextMonthCommand { get; }
    public ICommand TodayCommand { get; }

    public CalendarWidgetViewModel() : this(string.Empty) { }

    public CalendarWidgetViewModel(string instanceId)
    {
        _instanceId = instanceId;
        LoadSettings();

        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;

        PrevMonthCommand = new NativeRelayCommand(() => { _displayDate = _displayDate.AddMonths(-1); BuildMonth(); });
        NextMonthCommand = new NativeRelayCommand(() => { _displayDate = _displayDate.AddMonths(1); BuildMonth(); });
        TodayCommand = new NativeRelayCommand(() => { _displayDate = DateTime.Today; BuildMonth(); });

        BuildMonth();
    }

    private void LoadSettings()
    {
        if (!string.IsNullOrEmpty(_instanceId) &&
            WidgetManager.Instance.Settings.Widgets.TryGetValue(_instanceId, out var s))
        {
            if (s.CustomSettings.TryGetValue("calFirstDayOfWeek", out var fdw) && !string.IsNullOrEmpty(fdw))
            {
                _firstDayOfWeek = fdw.Equals("sunday", StringComparison.OrdinalIgnoreCase) ? "sunday" : "monday";
            }
            if (s.CustomSettings.TryGetValue("calShowWeekNumbers", out var swn) && bool.TryParse(swn, out var bSwn))
            {
                _showWeekNumbers = bSwn;
            }
            if (s.CustomSettings.TryGetValue("calHighlightToday", out var ht) && bool.TryParse(ht, out var bHt))
            {
                _highlightToday = bHt;
            }
        }
    }

    private void OnLanguageChanged()
    {
        BuildMonth();
        OnPropertyChanged(nameof(MonthYear));
        OnPropertyChanged(nameof(TodayFull));
        OnPropertyChanged(nameof(TodayButtonText));
        OnPropertyChanged(nameof(TodayLabel));
        OnPropertyChanged(nameof(CurrentWeekday));
    }

    private int GetWeekNumber(DateTime date)
    {
        bool isSundayFirst = _firstDayOfWeek.Equals("sunday", StringComparison.OrdinalIgnoreCase);
        if (isSundayFirst)
        {
            return System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                date,
                System.Globalization.CalendarWeekRule.FirstDay,
                DayOfWeek.Sunday);
        }
        else
        {
            return System.Globalization.ISOWeek.GetWeekOfYear(date);
        }
    }

    private void BuildMonth()
    {
        WeekHeaders.Clear();
        if (_showWeekNumbers)
        {
            string weekHdr = LocalizationService.Instance.T("Calendar.WeekNumHeader", "№");
            WeekHeaders.Add(new CalendarHeaderItem { Text = weekHdr, IsWeekNumber = true });
        }

        bool isSundayFirst = _firstDayOfWeek.Equals("sunday", StringComparison.OrdinalIgnoreCase);
        if (isSundayFirst)
        {
            WeekHeaders.Add(new CalendarHeaderItem { Text = LocalizationService.Instance.T("Calendar.Weekday.Sun"), IsWeekend = true });
            WeekHeaders.Add(new CalendarHeaderItem { Text = LocalizationService.Instance.T("Calendar.Weekday.Mon"), IsWeekend = false });
            WeekHeaders.Add(new CalendarHeaderItem { Text = LocalizationService.Instance.T("Calendar.Weekday.Tue"), IsWeekend = false });
            WeekHeaders.Add(new CalendarHeaderItem { Text = LocalizationService.Instance.T("Calendar.Weekday.Wed"), IsWeekend = false });
            WeekHeaders.Add(new CalendarHeaderItem { Text = LocalizationService.Instance.T("Calendar.Weekday.Thu"), IsWeekend = false });
            WeekHeaders.Add(new CalendarHeaderItem { Text = LocalizationService.Instance.T("Calendar.Weekday.Fri"), IsWeekend = false });
            WeekHeaders.Add(new CalendarHeaderItem { Text = LocalizationService.Instance.T("Calendar.Weekday.Sat"), IsWeekend = true });
        }
        else
        {
            WeekHeaders.Add(new CalendarHeaderItem { Text = LocalizationService.Instance.T("Calendar.Weekday.Mon"), IsWeekend = false });
            WeekHeaders.Add(new CalendarHeaderItem { Text = LocalizationService.Instance.T("Calendar.Weekday.Tue"), IsWeekend = false });
            WeekHeaders.Add(new CalendarHeaderItem { Text = LocalizationService.Instance.T("Calendar.Weekday.Wed"), IsWeekend = false });
            WeekHeaders.Add(new CalendarHeaderItem { Text = LocalizationService.Instance.T("Calendar.Weekday.Thu"), IsWeekend = false });
            WeekHeaders.Add(new CalendarHeaderItem { Text = LocalizationService.Instance.T("Calendar.Weekday.Fri"), IsWeekend = false });
            WeekHeaders.Add(new CalendarHeaderItem { Text = LocalizationService.Instance.T("Calendar.Weekday.Sat"), IsWeekend = true });
            WeekHeaders.Add(new CalendarHeaderItem { Text = LocalizationService.Instance.T("Calendar.Weekday.Sun"), IsWeekend = true });
        }

        Days.Clear();
        var firstDay = new DateTime(_displayDate.Year, _displayDate.Month, 1);
        int daysInMonth = DateTime.DaysInMonth(_displayDate.Year, _displayDate.Month);

        int startDayOffset = isSundayFirst
            ? (int)firstDay.DayOfWeek
            : ((int)firstDay.DayOfWeek + 6) % 7;

        int daysInRow = 0;

        if (_showWeekNumbers)
        {
            int weekNum = GetWeekNumber(firstDay);
            Days.Add(new CalendarDayItem
            {
                Day = 0,
                WeekNumberText = weekNum.ToString(),
                IsWeekNumber = true,
                IsCurrentMonth = true,
                HighlightToday = false
            });
        }

        for (int i = 0; i < startDayOffset; i++)
        {
            Days.Add(new CalendarDayItem { Day = 0, IsCurrentMonth = false, HighlightToday = _highlightToday });
            daysInRow++;
        }

        for (int d = 1; d <= daysInMonth; d++)
        {
            if (daysInRow == 7)
            {
                daysInRow = 0;
                if (_showWeekNumbers)
                {
                    var currentDate = new DateTime(_displayDate.Year, _displayDate.Month, d);
                    int weekNum = GetWeekNumber(currentDate);
                    Days.Add(new CalendarDayItem
                    {
                        Day = 0,
                        WeekNumberText = weekNum.ToString(),
                        IsWeekNumber = true,
                        IsCurrentMonth = true,
                        HighlightToday = false
                    });
                }
            }

            var dayDate = new DateTime(_displayDate.Year, _displayDate.Month, d);
            bool isWeekend = dayDate.DayOfWeek == DayOfWeek.Saturday || dayDate.DayOfWeek == DayOfWeek.Sunday;
            bool isToday = (_displayDate.Year == DateTime.Today.Year &&
                            _displayDate.Month == DateTime.Today.Month &&
                            d == DateTime.Today.Day);

            Days.Add(new CalendarDayItem
            {
                Day = d,
                IsToday = isToday,
                IsCurrentMonth = true,
                IsWeekend = isWeekend,
                HighlightToday = _highlightToday
            });
            daysInRow++;
        }

        if (daysInRow > 0 && daysInRow < 7)
        {
            int trailingBlanks = 7 - daysInRow;
            for (int i = 0; i < trailingBlanks; i++)
            {
                Days.Add(new CalendarDayItem { Day = 0, IsCurrentMonth = false, HighlightToday = _highlightToday });
            }
        }

        OnPropertyChanged(nameof(MonthYear));
        OnPropertyChanged(nameof(TodayFull));
        OnPropertyChanged(nameof(TodayButtonText));
        OnPropertyChanged(nameof(TodayLabel));
    }
}

// 2. Crypto & Currencies ViewModel
public class CryptoItem
{
    public string Key { get; set; } = "";
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
    public const string DefaultBaseCurrency = "USD";
    public const string DefaultEnabledTokens = "BTC,ETH,SOL,GRAM,USD/RUB,EUR/RUB";
    public const string DefaultRefreshInterval = "60s";

    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
    private readonly DispatcherTimer _timer;
    private readonly string? _instanceId;

    public ObservableCollection<CryptoItem> Items { get; } = new();

    private string _baseCurrency = DefaultBaseCurrency;
    public string BaseCurrency
    {
        get => _baseCurrency;
        set
        {
            if (_baseCurrency != value)
            {
                _baseCurrency = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BaseCurrencyBadge));
                SaveCustomSettings();
                _ = FetchPricesAsync();
            }
        }
    }

    public string BaseCurrencyBadge => _baseCurrency.ToUpperInvariant();

    private string _refreshInterval = DefaultRefreshInterval;
    public string RefreshInterval
    {
        get => _refreshInterval;
        set
        {
            if (_refreshInterval != value)
            {
                _refreshInterval = value;
                OnPropertyChanged();
                UpdateTimerInterval();
                SaveCustomSettings();
            }
        }
    }

    private readonly HashSet<string> _enabledTokens = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<string> EnabledTokens => _enabledTokens;

    public string HeaderTitle => LocalizationService.Instance.T("Crypto.HeaderTitle", "💎 КОТИРОВКИ & ВАЛЮТЫ");
    public string RefreshButtonText => LocalizationService.Instance.T("Crypto.RefreshShort", "Обновить ↻");

    private string _lastUpdatedText = "";
    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        set { _lastUpdatedText = value; OnPropertyChanged(); }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    public ICommand RefreshCommand { get; }

    public CryptoWidgetViewModel() : this(string.Empty) { }

    public CryptoWidgetViewModel(string? instanceId = null)
    {
        _instanceId = instanceId;
        RefreshCommand = new NativeRelayCommand(() => _ = FetchPricesAsync());

        LoadCustomSettings();

        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;

        _timer = new DispatcherTimer();
        UpdateTimerInterval();
        _timer.Tick += (s, e) => _ = FetchPricesAsync();
        _timer.Start();

        PopulateInitialItems();
        _ = FetchPricesAsync();
    }

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(RefreshButtonText));
        _ = FetchPricesAsync();
    }

    public void LoadCustomSettings()
    {
        _baseCurrency = DefaultBaseCurrency;
        _refreshInterval = DefaultRefreshInterval;
        _enabledTokens.Clear();

        if (!string.IsNullOrEmpty(_instanceId) &&
            WidgetManager.Instance.Settings.Widgets.TryGetValue(_instanceId, out var s))
        {
            if (s.CustomSettings.TryGetValue("cryptoBaseCurrency", out var baseCur) && !string.IsNullOrWhiteSpace(baseCur))
            {
                _baseCurrency = baseCur.Trim().ToUpperInvariant();
            }
            if (s.CustomSettings.TryGetValue("cryptoRefreshInterval", out var interval) && !string.IsNullOrWhiteSpace(interval))
            {
                _refreshInterval = interval.Trim();
            }
            if (s.CustomSettings.TryGetValue("cryptoEnabledTokens", out var tokensStr) && !string.IsNullOrWhiteSpace(tokensStr))
            {
                var parts = tokensStr.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in parts)
                {
                    _enabledTokens.Add(p.Trim().ToUpperInvariant());
                }
            }
        }

        if (_enabledTokens.Count == 0)
        {
            foreach (var tok in DefaultEnabledTokens.Split(','))
            {
                _enabledTokens.Add(tok.Trim().ToUpperInvariant());
            }
        }
    }

    public void SaveCustomSettings()
    {
        if (string.IsNullOrEmpty(_instanceId)) return;
        if (!WidgetManager.Instance.Settings.Widgets.TryGetValue(_instanceId, out var s)) return;

        s.CustomSettings["cryptoBaseCurrency"] = _baseCurrency;
        s.CustomSettings["cryptoRefreshInterval"] = _refreshInterval;
        s.CustomSettings["cryptoEnabledTokens"] = string.Join(",", _enabledTokens);
        WidgetManager.Instance.SaveSettings();
    }

    public bool IsTokenEnabled(string tokenKey)
    {
        return _enabledTokens.Contains(tokenKey.ToUpperInvariant());
    }

    public void SetTokenEnabled(string tokenKey, bool enabled)
    {
        string norm = tokenKey.ToUpperInvariant();
        if (enabled)
            _enabledTokens.Add(norm);
        else
            _enabledTokens.Remove(norm);

        SaveCustomSettings();
        _ = FetchPricesAsync();
    }

    public void SelectAllTokens(IEnumerable<string> allTokens)
    {
        _enabledTokens.Clear();
        foreach (var t in allTokens) _enabledTokens.Add(t.ToUpperInvariant());
        SaveCustomSettings();
        _ = FetchPricesAsync();
    }

    public void ResetToDefaults()
    {
        _baseCurrency = DefaultBaseCurrency;
        _refreshInterval = DefaultRefreshInterval;
        _enabledTokens.Clear();
        foreach (var tok in DefaultEnabledTokens.Split(','))
        {
            _enabledTokens.Add(tok.Trim().ToUpperInvariant());
        }
        UpdateTimerInterval();
        SaveCustomSettings();
        OnPropertyChanged(nameof(BaseCurrency));
        OnPropertyChanged(nameof(BaseCurrencyBadge));
        OnPropertyChanged(nameof(RefreshInterval));
        _ = FetchPricesAsync();
    }

    private void UpdateTimerInterval()
    {
        int seconds = 60;
        string lower = _refreshInterval.ToLowerInvariant().Trim();
        if (lower.EndsWith("s") && int.TryParse(lower[..^1], out var sVal))
            seconds = sVal;
        else if (lower.EndsWith("m") && int.TryParse(lower[..^1], out var mVal))
            seconds = mVal * 60;
        else if (int.TryParse(lower, out var numVal))
            seconds = numVal;

        if (seconds < 10) seconds = 10;
        if (_timer != null)
        {
            _timer.Interval = TimeSpan.FromSeconds(seconds);
        }
    }

    private void PopulateInitialItems()
    {
        Items.Clear();
        var loc = LocalizationService.Instance;
        bool isRu = loc.IsRussian;

        if (IsTokenEnabled("BTC"))
            Items.Add(new CryptoItem { Key = "BTC", Name = isRu ? "Биткоин" : "Bitcoin", Symbol = "BTC", Price = FormatPrice(81950m, _baseCurrency), Change = "+1.8%", IsPositive = true, Icon = "₿" });
        if (IsTokenEnabled("ETH"))
            Items.Add(new CryptoItem { Key = "ETH", Name = isRu ? "Эфириум" : "Ethereum", Symbol = "ETH", Price = FormatPrice(2460m, _baseCurrency), Change = "+3.2%", IsPositive = true, Icon = "Ξ" });
        if (IsTokenEnabled("SOL"))
            Items.Add(new CryptoItem { Key = "SOL", Name = isRu ? "Солана" : "Solana", Symbol = "SOL", Price = FormatPrice(105.40m, _baseCurrency), Change = "+2.5%", IsPositive = true, Icon = "◎" });
        if (IsTokenEnabled("GRAM"))
            Items.Add(new CryptoItem { Key = "GRAM", Name = isRu ? "Грам" : "Gram", Symbol = "GRAM", Price = FormatPrice(5.35m, _baseCurrency), Change = "+1.4%", IsPositive = true, Icon = "💎" });
        if (IsTokenEnabled("BNB"))
            Items.Add(new CryptoItem { Key = "BNB", Name = "BNB", Symbol = "BNB", Price = FormatPrice(620m, _baseCurrency), Change = "+0.8%", IsPositive = true, Icon = "🟡" });
        if (IsTokenEnabled("XRP"))
            Items.Add(new CryptoItem { Key = "XRP", Name = isRu ? "Риппл" : "Ripple", Symbol = "XRP", Price = FormatPrice(0.55m, _baseCurrency), Change = "-0.4%", IsPositive = false, Icon = "✕" });
        if (IsTokenEnabled("USD/RUB"))
            Items.Add(new CryptoItem { Key = "USD/RUB", Name = isRu ? "Доллар США" : "US Dollar", Symbol = "USD/RUB", Price = "83.76 ₽", Change = "-0.19", IsPositive = false, Icon = "$" });
        if (IsTokenEnabled("EUR/RUB"))
            Items.Add(new CryptoItem { Key = "EUR/RUB", Name = isRu ? "Евро" : "Euro", Symbol = "EUR/RUB", Price = "93.77 ₽", Change = "-0.15", IsPositive = false, Icon = "€" });
    }

    private static string FormatPrice(decimal val, string baseCurrency)
    {
        return baseCurrency switch
        {
            "RUB" => val >= 1000 ? $"{val:N0} ₽" : $"{val:F2} ₽",
            "EUR" => val >= 1000 ? $"€{val:N0}" : (val >= 1 ? $"€{val:F2}" : $"€{val:F4}"),
            "GBP" => val >= 1000 ? $"£{val:N0}" : (val >= 1 ? $"£{val:F2}" : $"£{val:F4}"),
            "JPY" => $"¥{val:N0}",
            _ => val >= 1000 ? $"${val:N0}" : (val >= 1 ? $"${val:F2}" : $"${val:F4}")
        };
    }

    public async Task FetchPricesAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            // 1. Fetch Binance 24hr tickers
            // Supported tokens: BTC, ETH, SOL, GRAM (mapped from TONUSDT on Binance API), BNB, XRP
            string binanceUrl = "https://api.binance.com/api/v3/ticker/24hr?symbols=[\"BTCUSDT\",\"ETHUSDT\",\"SOLUSDT\",\"TONUSDT\",\"BNBUSDT\",\"XRPUSDT\"]";
            string binanceJson = await _httpClient.GetStringAsync(binanceUrl);
            using var bDoc = JsonDocument.Parse(binanceJson);

            var cryptoData = new Dictionary<string, (decimal Price, decimal Change)>(StringComparer.OrdinalIgnoreCase);

            foreach (var elem in bDoc.RootElement.EnumerateArray())
            {
                string sym = elem.GetProperty("symbol").GetString() ?? "";
                decimal p = decimal.TryParse(elem.GetProperty("lastPrice").GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lp) ? lp : 0;
                decimal c = decimal.TryParse(elem.GetProperty("priceChangePercent").GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var cp) ? cp : 0;

                if (sym == "BTCUSDT") cryptoData["BTC"] = (p, c);
                else if (sym == "ETHUSDT") cryptoData["ETH"] = (p, c);
                else if (sym == "SOLUSDT") cryptoData["SOL"] = (p, c);
                else if (sym == "TONUSDT") cryptoData["GRAM"] = (p, c); // Rebrand TON to Gram!
                else if (sym == "BNBUSDT") cryptoData["BNB"] = (p, c);
                else if (sym == "XRPUSDT") cryptoData["XRP"] = (p, c);
            }

            // Fallback for GRAM if API was unavailable
            if (!cryptoData.ContainsKey("GRAM") || cryptoData["GRAM"].Price == 0)
            {
                cryptoData["GRAM"] = (5.35m, 1.4m);
            }

            // 2. Fetch Central Bank (CBR) rates for fiat conversion
            decimal usdRub = 83.76m, usdDiff = -0.19m;
            decimal eurRub = 93.77m, eurDiff = -0.15m;
            decimal gbpRub = 110.50m, gbpDiff = +0.25m;
            decimal jpyRub = 0.58m;

            try
            {
                string cbrJson = await _httpClient.GetStringAsync("https://www.cbr-xml-daily.ru/daily_json.js");
                using var cDoc = JsonDocument.Parse(cbrJson);
                var valute = cDoc.RootElement.GetProperty("Valute");

                if (valute.TryGetProperty("USD", out var usdElem))
                {
                    usdRub = usdElem.GetProperty("Value").GetDecimal();
                    usdDiff = usdRub - usdElem.GetProperty("Previous").GetDecimal();
                }
                if (valute.TryGetProperty("EUR", out var eurElem))
                {
                    eurRub = eurElem.GetProperty("Value").GetDecimal();
                    eurDiff = eurRub - eurElem.GetProperty("Previous").GetDecimal();
                }
                if (valute.TryGetProperty("GBP", out var gbpElem))
                {
                    gbpRub = gbpElem.GetProperty("Value").GetDecimal();
                    gbpDiff = gbpRub - gbpElem.GetProperty("Previous").GetDecimal();
                }
                if (valute.TryGetProperty("JPY", out var jpyElem))
                {
                    int nominal = jpyElem.TryGetProperty("Nominal", out var nom) ? nom.GetInt32() : 100;
                    if (nominal <= 0) nominal = 100;
                    jpyRub = jpyElem.GetProperty("Value").GetDecimal() / nominal;
                }
            }
            catch { }

            // Conversion factor relative to USD: 1 USD = factor units of BaseCurrency
            decimal conversionRate = 1.0m;
            switch (_baseCurrency)
            {
                case "RUB":
                    conversionRate = usdRub > 0 ? usdRub : 83.76m;
                    break;
                case "EUR":
                    conversionRate = (usdRub > 0 && eurRub > 0) ? (usdRub / eurRub) : 0.89m;
                    break;
                case "GBP":
                    conversionRate = (usdRub > 0 && gbpRub > 0) ? (usdRub / gbpRub) : 0.76m;
                    break;
                case "JPY":
                    conversionRate = (usdRub > 0 && jpyRub > 0) ? (usdRub / jpyRub) : 150.0m;
                    break;
                default: // USD
                    conversionRate = 1.0m;
                    break;
            }

            var loc = LocalizationService.Instance;
            bool isRu = loc.IsRussian;

            var newItems = new List<CryptoItem>();

            // Crypto definitions: Gram is now Gram / Грам with 💎
            var tokenDefs = new (string Key, string Symbol, string NameRu, string NameEn, string Icon)[]
            {
                ("BTC", "BTC", "Биткоин", "Bitcoin", "₿"),
                ("ETH", "ETH", "Эфириум", "Ethereum", "Ξ"),
                ("SOL", "SOL", "Солана", "Solana", "◎"),
                ("GRAM", "GRAM", "Грам", "Gram", "💎"),
                ("BNB", "BNB", "BNB", "BNB", "🟡"),
                ("XRP", "XRP", "Риппл", "Ripple", "✕")
            };

            foreach (var t in tokenDefs)
            {
                if (!IsTokenEnabled(t.Key)) continue;

                if (cryptoData.TryGetValue(t.Key, out var val) && val.Price > 0)
                {
                    decimal convertedPrice = val.Price * conversionRate;
                    newItems.Add(new CryptoItem
                    {
                        Key = t.Key,
                        Name = isRu ? t.NameRu : t.NameEn,
                        Symbol = t.Symbol,
                        Price = FormatPrice(convertedPrice, _baseCurrency),
                        Change = (val.Change >= 0 ? "+" : "") + $"{val.Change:F2}%",
                        IsPositive = val.Change >= 0,
                        Icon = t.Icon
                    });
                }
            }

            // Fiat pairs
            if (IsTokenEnabled("USD/RUB") && usdRub > 0)
            {
                newItems.Add(new CryptoItem
                {
                    Key = "USD/RUB",
                    Name = isRu ? "Доллар США" : "US Dollar",
                    Symbol = "USD/RUB",
                    Price = $"{usdRub:F2} ₽",
                    Change = (usdDiff >= 0 ? "+" : "") + $"{usdDiff:F2}",
                    IsPositive = usdDiff >= 0,
                    Icon = "$"
                });
            }

            if (IsTokenEnabled("EUR/RUB") && eurRub > 0)
            {
                newItems.Add(new CryptoItem
                {
                    Key = "EUR/RUB",
                    Name = isRu ? "Евро" : "Euro",
                    Symbol = "EUR/RUB",
                    Price = $"{eurRub:F2} ₽",
                    Change = (eurDiff >= 0 ? "+" : "") + $"{eurDiff:F2}",
                    IsPositive = eurDiff >= 0,
                    Icon = "€"
                });
            }

            if (IsTokenEnabled("EUR/USD") && usdRub > 0 && eurRub > 0)
            {
                decimal eurUsd = eurRub / usdRub;
                newItems.Add(new CryptoItem
                {
                    Key = "EUR/USD",
                    Name = isRu ? "Евро / Доллар" : "Euro / USD",
                    Symbol = "EUR/USD",
                    Price = $"${eurUsd:F4}",
                    Change = "+0.12%",
                    IsPositive = true,
                    Icon = "€"
                });
            }

            if (IsTokenEnabled("GBP/USD") && usdRub > 0 && gbpRub > 0)
            {
                decimal gbpUsd = gbpRub / usdRub;
                newItems.Add(new CryptoItem
                {
                    Key = "GBP/USD",
                    Name = isRu ? "Фунт / Доллар" : "Pound / USD",
                    Symbol = "GBP/USD",
                    Price = $"${gbpUsd:F4}",
                    Change = (gbpDiff >= 0 ? "+" : "") + $"{gbpDiff:F2}",
                    IsPositive = gbpDiff >= 0,
                    Icon = "£"
                });
            }

            string updatedTime = DateTime.Now.ToString("HH:mm:ss");

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                Items.Clear();
                foreach (var item in newItems) Items.Add(item);
                LastUpdatedText = string.Format(loc.T("Crypto.LastUpdated", "Обновлено: {0}"), updatedTime);
                OnPropertyChanged(nameof(HeaderTitle));
                OnPropertyChanged(nameof(RefreshButtonText));
                OnPropertyChanged(nameof(BaseCurrencyBadge));
            });
        }
        catch { }
        finally
        {
            IsLoading = false;
        }
    }
}

// 3. GPU Monitor ViewModel
public class GpuMonitorWidgetViewModel : NativeViewModelBase
{
    private readonly string? _instanceId;
    public SystemMonitorService Monitor => SystemMonitorService.Instance;

    private double _gpuPercent;
    public double GpuPercent
    {
        get => _gpuPercent;
        set { _gpuPercent = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedCoreLoad)); OnPropertyChanged(nameof(CoreLoadBrush)); CheckAlertState(); }
    }

    private double _gpuMemoryPercent;
    public double GpuMemoryPercent
    {
        get => _gpuMemoryPercent;
        set { _gpuMemoryPercent = value; OnPropertyChanged(); UpdateVramDisplay(); OnPropertyChanged(nameof(VramBrush)); }
    }

    private double _gpuTemp;
    public double GpuTemp
    {
        get => _gpuTemp;
        set { _gpuTemp = value; OnPropertyChanged(); UpdateTempDisplay(); CheckAlertState(); }
    }

    private double _totalGpuMemoryGb = 2.0;
    public double TotalGpuMemoryGb
    {
        get => _totalGpuMemoryGb;
        set { _totalGpuMemoryGb = value; OnPropertyChanged(); UpdateVramDisplay(); }
    }

    private double _usedGpuMemoryGb = 0.0;
    public double UsedGpuMemoryGb
    {
        get => _usedGpuMemoryGb;
        set { _usedGpuMemoryGb = value; OnPropertyChanged(); UpdateVramDisplay(); }
    }

    private string _rawGpuName = "GPU";
    private string _gpuName = "GPU";
    public string GpuName
    {
        get => _gpuName;
        set { _gpuName = value; OnPropertyChanged(); }
    }

    // CustomSettings
    private string _gpuShowVram = "gb"; // "gb", "percent", "both"
    public string VramDisplayMode => _gpuShowVram;

    private string _gpuTempUnit = "C"; // "C" or "F"
    public string TempUnit => _gpuTempUnit;

    private double _gpuAlertThreshold = 80.0; // °C
    public double AlertThreshold => _gpuAlertThreshold;

    private string _gpuAdapterNameMode = "full"; // "full" or "short"
    public string AdapterNameMode => _gpuAdapterNameMode;

    // Derived Display Properties
    public string FormattedCoreLoad => $"{Math.Round(GpuPercent)}%";

    private string _vramDisplayText = "0 GB";
    public string VramDisplayText
    {
        get => _vramDisplayText;
        private set { _vramDisplayText = value; OnPropertyChanged(); }
    }

    private string _vramSubText = "";
    public string VramSubText
    {
        get => _vramSubText;
        private set { _vramSubText = value; OnPropertyChanged(); }
    }

    private string _formattedTemp = "0°C";
    public string FormattedTemp
    {
        get => _formattedTemp;
        private set { _formattedTemp = value; OnPropertyChanged(); }
    }

    private bool _isAlertActive = false;
    public bool IsAlertActive
    {
        get => _isAlertActive;
        private set
        {
            if (_isAlertActive != value)
            {
                _isAlertActive = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AlertBadgeBackground));
                OnPropertyChanged(nameof(AlertBadgeBorderBrush));
                OnPropertyChanged(nameof(AlertTextColor));
                OnPropertyChanged(nameof(AlertIcon));
                OnPropertyChanged(nameof(AlertStatusText));
            }
        }
    }

    public string AlertBadgeBackground => IsAlertActive ? "#38EF4444" : "#26EF4444";
    public string AlertBadgeBorderBrush => IsAlertActive ? "#B0EF4444" : "#40EF4444";
    public string AlertTextColor => IsAlertActive ? "#EF4444" : "#F87171";
    public string AlertIcon => IsAlertActive ? "⚠️ " : "🔥 ";
    public string CoreLoadBrush => (GpuPercent >= 95.0) ? "#EF4444" : "#10B981";
    public string VramBrush => (GpuMemoryPercent >= 90.0) ? "#EF4444" : "#38BDF8";

    // Internationalized Labels
    public string WidgetTitle => LocalizationService.Instance.T("GpuMonitor.WidgetName", "GPU Монитор");
    public string CoreLoadLabel => LocalizationService.Instance.T("GpuMonitor.CoreLoad", "Нагрузка ядра");
    public string VideoRamLabel => LocalizationService.Instance.T("GpuMonitor.VideoRam", "Видеопамять");
    public string TemperatureLabel => LocalizationService.Instance.T("GpuMonitor.Temperature", "Температура");
    public string HardwareDwmLabel => LocalizationService.Instance.T("GpuMonitor.HardwareDwm", "Аппаратные счетчики DWM");
    public string AlertStatusText => IsAlertActive
        ? LocalizationService.Instance.T("GpuMonitor.StatusAlert", "Внимание: Высокая нагрузка!")
        : LocalizationService.Instance.T("GpuMonitor.MemoryActive", "Память активна");

    public GpuMonitorWidgetViewModel(string? instanceId = null)
    {
        _instanceId = instanceId;
        LoadSettings();

        LocalizationService.Instance.LanguageChanged += () =>
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                OnPropertyChanged(nameof(WidgetTitle));
                OnPropertyChanged(nameof(CoreLoadLabel));
                OnPropertyChanged(nameof(VideoRamLabel));
                OnPropertyChanged(nameof(TemperatureLabel));
                OnPropertyChanged(nameof(HardwareDwmLabel));
                OnPropertyChanged(nameof(AlertStatusText));
            });
        };

        UpdateFrom(Monitor.CurrentMetrics);
        Monitor.MetricsUpdated += (metrics) =>
        {
            Application.Current?.Dispatcher?.Invoke(() => UpdateFrom(metrics));
        };
    }

    private void LoadSettings()
    {
        if (!string.IsNullOrEmpty(_instanceId) &&
            WidgetManager.Instance.Settings.Widgets.TryGetValue(_instanceId, out var s))
        {
            if (s.CustomSettings.TryGetValue("gpuShowVram", out var vram) && !string.IsNullOrWhiteSpace(vram))
                _gpuShowVram = vram;
            if (s.CustomSettings.TryGetValue("gpuTempUnit", out var unit) && !string.IsNullOrWhiteSpace(unit))
                _gpuTempUnit = unit;
            if (s.CustomSettings.TryGetValue("gpuAlertThreshold", out var thresh) && double.TryParse(thresh, out var val))
                _gpuAlertThreshold = val;
            if (s.CustomSettings.TryGetValue("gpuAdapterNameMode", out var mode) && !string.IsNullOrWhiteSpace(mode))
                _gpuAdapterNameMode = mode;
        }
    }

    private void SaveSetting(string key, string value)
    {
        if (!string.IsNullOrEmpty(_instanceId) &&
            WidgetManager.Instance.Settings.Widgets.TryGetValue(_instanceId, out var s))
        {
            s.CustomSettings[key] = value;
            WidgetManager.Instance.SaveSettings();
        }
    }

    public void SetVramDisplayMode(string mode)
    {
        _gpuShowVram = mode;
        SaveSetting("gpuShowVram", mode);
        UpdateVramDisplay();
        OnPropertyChanged(nameof(VramDisplayMode));
    }

    public void SetTempUnit(string unit)
    {
        _gpuTempUnit = unit;
        SaveSetting("gpuTempUnit", unit);
        UpdateTempDisplay();
        OnPropertyChanged(nameof(TempUnit));
    }

    public void SetAlertThreshold(double threshold)
    {
        _gpuAlertThreshold = threshold;
        SaveSetting("gpuAlertThreshold", threshold.ToString());
        CheckAlertState();
        OnPropertyChanged(nameof(AlertThreshold));
    }

    public void SetAdapterNameMode(string mode)
    {
        _gpuAdapterNameMode = mode;
        SaveSetting("gpuAdapterNameMode", mode);
        UpdateAdapterName();
        OnPropertyChanged(nameof(AdapterNameMode));
    }

    private void UpdateAdapterName()
    {
        if (_gpuAdapterNameMode == "short")
        {
            GpuName = GetShortGpuName(_rawGpuName);
        }
        else
        {
            GpuName = _rawGpuName;
        }
    }

    public static string GetShortGpuName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "GPU";
        string name = fullName
            .Replace("(R)", "", StringComparison.OrdinalIgnoreCase)
            .Replace("(TM)", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Graphics", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Corporation", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Laptop GPU", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (name.StartsWith("NVIDIA GeForce ", StringComparison.OrdinalIgnoreCase))
            name = name.Substring("NVIDIA GeForce ".Length).Trim();
        else if (name.StartsWith("NVIDIA ", StringComparison.OrdinalIgnoreCase))
            name = name.Substring("NVIDIA ".Length).Trim();
        else if (name.StartsWith("AMD Radeon ", StringComparison.OrdinalIgnoreCase))
            name = name.Substring("AMD Radeon ".Length).Trim();
        else if (name.StartsWith("AMD ", StringComparison.OrdinalIgnoreCase))
            name = name.Substring("AMD ".Length).Trim();
        else if (name.StartsWith("Intel ", StringComparison.OrdinalIgnoreCase))
            name = name.Substring("Intel ".Length).Trim();

        return string.IsNullOrWhiteSpace(name) ? fullName : name;
    }

    private void UpdateTempDisplay()
    {
        double val = _gpuTemp;
        string symbol = "°C";
        if (string.Equals(_gpuTempUnit, "F", StringComparison.OrdinalIgnoreCase))
        {
            val = (val * 9.0 / 5.0) + 32.0;
            symbol = "°F";
        }
        FormattedTemp = $"{Math.Round(val)}{symbol}";
    }

    private void UpdateVramDisplay()
    {
        if (_gpuShowVram == "percent")
        {
            VramDisplayText = $"{Math.Round(_gpuMemoryPercent)}%";
            VramSubText = $"{_usedGpuMemoryGb:0.1} / {_totalGpuMemoryGb:0.1} GB";
        }
        else if (_gpuShowVram == "both")
        {
            VramDisplayText = $"{_usedGpuMemoryGb:0.1} GB";
            VramSubText = $"{Math.Round(_gpuMemoryPercent)}% ({_totalGpuMemoryGb:0.1} GB)";
        }
        else // "gb" (default)
        {
            VramDisplayText = $"{_usedGpuMemoryGb:0.1} GB";
            VramSubText = $"{Math.Round(_gpuMemoryPercent)}%";
        }
    }

    private void CheckAlertState()
    {
        IsAlertActive = (_gpuTemp >= _gpuAlertThreshold) || (_gpuPercent >= 95.0);
    }

    private void UpdateFrom(SystemMetrics metrics)
    {
        if (metrics == null) return;
        _rawGpuName = metrics.GpuName;
        UpdateAdapterName();

        _gpuPercent = metrics.GpuPercent;
        _gpuMemoryPercent = metrics.GpuMemoryPercent;
        _totalGpuMemoryGb = metrics.TotalGpuMemoryGb;
        _usedGpuMemoryGb = metrics.UsedGpuMemoryGb;
        _gpuTemp = metrics.GpuTemperature;

        OnPropertyChanged(nameof(GpuPercent));
        OnPropertyChanged(nameof(FormattedCoreLoad));
        OnPropertyChanged(nameof(CoreLoadBrush));
        OnPropertyChanged(nameof(GpuMemoryPercent));
        OnPropertyChanged(nameof(VramBrush));
        OnPropertyChanged(nameof(TotalGpuMemoryGb));
        OnPropertyChanged(nameof(UsedGpuMemoryGb));
        OnPropertyChanged(nameof(GpuTemp));

        UpdateTempDisplay();
        UpdateVramDisplay();
        CheckAlertState();
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

    private string _caption = "";
    public string Caption
    {
        get => _caption;
        set { _caption = value; OnPropertyChanged(); }
    }

    private Stretch _stretchMode = Stretch.UniformToFill;
    public Stretch StretchMode
    {
        get => _stretchMode;
        set
        {
            if (_stretchMode != value)
            {
                _stretchMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StretchModeString));
                SaveCustomSetting("mediaStretchMode", value.ToString());
            }
        }
    }

    public string StretchModeString
    {
        get => _stretchMode.ToString();
        set
        {
            if (Enum.TryParse<Stretch>(value, true, out var mode))
            {
                StretchMode = mode;
            }
        }
    }

    private int _slideIntervalSec = 10;
    public int SlideIntervalSec
    {
        get => _slideIntervalSec;
        set
        {
            if (_slideIntervalSec != value)
            {
                _slideIntervalSec = value;
                OnPropertyChanged();
                UpdateTimerInterval();
                SaveCustomSetting("mediaSlideIntervalSec", value.ToString());
            }
        }
    }

    private bool _isShuffle = false;
    public bool IsShuffle
    {
        get => _isShuffle;
        set
        {
            if (_isShuffle != value)
            {
                _isShuffle = value;
                OnPropertyChanged();
                ApplyShuffle();
                SaveCustomSetting("mediaShuffle", value ? "true" : "false");
            }
        }
    }

    private string _customFolder = "";
    public string CustomFolder
    {
        get => _customFolder;
        private set { _customFolder = value; OnPropertyChanged(); }
    }

    public string DragDropHintText => LocalizationService.Instance.T("MediaFrame.DragDropHint", "Перетащите сюда фото или папку");
    public string DragDropSubText => LocalizationService.Instance.T("MediaFrame.DragDropSub", "или укажите папку в настройках (ПКМ)");
    public string PrevTooltip => LocalizationService.Instance.T("MediaFrame.PrevPhoto", "Предыдущее фото");
    public string NextTooltip => LocalizationService.Instance.T("MediaFrame.NextPhoto", "Следующее фото");

    private readonly DispatcherTimer _slideTimer;
    private readonly string? _instanceId;
    private readonly List<string> _originalImages = new();
    private readonly List<string> _images = new();
    private int _index = 0;

    public ICommand NextCommand { get; }
    public ICommand PrevCommand { get; }

    public MediaFrameWidgetViewModel(string? instanceId = null)
    {
        _instanceId = instanceId;
        NextCommand = new NativeRelayCommand(NextImage);
        PrevCommand = new NativeRelayCommand(PrevImage);

        _slideTimer = new DispatcherTimer();
        _slideTimer.Tick += (s, e) => NextImage();

        LocalizationService.Instance.LanguageChanged += RefreshLocalization;

        string? savedFolder = null;
        int savedInterval = 10;
        string savedStretch = "UniformToFill";
        bool savedShuffle = false;

        if (!string.IsNullOrEmpty(_instanceId) &&
            WidgetManager.Instance.Settings.Widgets.TryGetValue(_instanceId, out var s))
        {
            if (s.CustomSettings.TryGetValue("mediaFolder", out var f) && Directory.Exists(f))
            {
                savedFolder = f;
            }

            if (s.CustomSettings.TryGetValue("mediaSlideIntervalSec", out var secStr) && int.TryParse(secStr, out var secVal))
            {
                savedInterval = secVal;
            }

            if (s.CustomSettings.TryGetValue("mediaStretchMode", out var sm))
            {
                savedStretch = sm;
            }

            if (s.CustomSettings.TryGetValue("mediaShuffle", out var shStr) && bool.TryParse(shStr, out var shVal))
            {
                savedShuffle = shVal;
            }
        }

        _isShuffle = savedShuffle;
        _slideIntervalSec = savedInterval;
        if (Enum.TryParse<Stretch>(savedStretch, true, out var st))
        {
            _stretchMode = st;
        }

        if (savedFolder != null)
        {
            SetCustomFolder(savedFolder, saveToSettings: false);
        }
        else
        {
            try
            {
                string pics = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                if (Directory.Exists(pics))
                {
                    _customFolder = pics;
                    LoadImagesFromDirectory(pics);
                }
            }
            catch { }

            ShowImage(0);
        }

        UpdateTimerInterval();
    }

    private void RefreshLocalization()
    {
        OnPropertyChanged(nameof(DragDropHintText));
        OnPropertyChanged(nameof(DragDropSubText));
        OnPropertyChanged(nameof(PrevTooltip));
        OnPropertyChanged(nameof(NextTooltip));
        if (_images.Count > 0 && _index >= 0 && _index < _images.Count)
        {
            UpdateCaption();
        }
        else
        {
            Caption = LocalizationService.Instance.T("MediaFrame.NoImagesFound", "В папке нет поддерживаемых изображений");
        }
    }

    private void UpdateTimerInterval()
    {
        if (_slideIntervalSec > 0)
        {
            _slideTimer.Interval = TimeSpan.FromSeconds(_slideIntervalSec);
            if (!_slideTimer.IsEnabled && _images.Count > 1)
            {
                _slideTimer.Start();
            }
        }
        else
        {
            _slideTimer.Stop();
        }
    }

    private void SaveCustomSetting(string key, string value)
    {
        if (!string.IsNullOrEmpty(_instanceId) &&
            WidgetManager.Instance.Settings.Widgets.TryGetValue(_instanceId, out var s))
        {
            s.CustomSettings[key] = value;
            WidgetManager.Instance.SaveSettings();
        }
    }

    public bool HasImage => !string.IsNullOrEmpty(_currentImage) && File.Exists(_currentImage);
    public Visibility ImageVisibility => HasImage ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PlaceholderVisibility => HasImage ? Visibility.Collapsed : Visibility.Visible;

    private void UpdateCaption()
    {
        if (_images.Count == 0 || _index < 0 || _index >= _images.Count) return;
        string fileName = Path.GetFileNameWithoutExtension(_images[_index]);
        Caption = $"{_index + 1}/{_images.Count}: {fileName}";
    }

    private void ShowImage(int idx)
    {
        if (_images.Count == 0)
        {
            CurrentImage = "";
            Caption = LocalizationService.Instance.T("MediaFrame.DragDropHint", "Перетащите сюда фото или папку");
            OnPropertyChanged(nameof(HasImage));
            OnPropertyChanged(nameof(ImageVisibility));
            OnPropertyChanged(nameof(PlaceholderVisibility));
            _slideTimer.Stop();
            return;
        }

        _index = (idx + _images.Count) % _images.Count;
        CurrentImage = _images[_index];
        UpdateCaption();
        OnPropertyChanged(nameof(HasImage));
        OnPropertyChanged(nameof(ImageVisibility));
        OnPropertyChanged(nameof(PlaceholderVisibility));

        if (_slideIntervalSec > 0 && _images.Count > 1 && !_slideTimer.IsEnabled)
        {
            _slideTimer.Start();
        }
    }

    private void NextImage() => ShowImage(_index + 1);
    private void PrevImage() => ShowImage(_index - 1);

    private void ApplyShuffle()
    {
        if (_originalImages.Count <= 1) return;

        string? current = (_index >= 0 && _index < _images.Count) ? _images[_index] : null;
        _images.Clear();

        if (_isShuffle)
        {
            var shuffled = new List<string>(_originalImages);
            var rng = new Random();
            int n = shuffled.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                (shuffled[k], shuffled[n]) = (shuffled[n], shuffled[k]);
            }
            _images.AddRange(shuffled);
        }
        else
        {
            _images.AddRange(_originalImages);
        }

        if (current != null && _images.Contains(current))
        {
            _index = _images.IndexOf(current);
        }
        else
        {
            _index = 0;
        }

        UpdateCaption();
    }

    private void LoadImagesFromDirectory(string folderPath)
    {
        _originalImages.Clear();
        _images.Clear();
        var validExts = new[] { "*.jpg", "*.jpeg", "*.png", "*.webp", "*.bmp", "*.gif" };
        var found = new List<string>();

        foreach (var ext in validExts)
        {
            try { found.AddRange(Directory.GetFiles(folderPath, ext)); } catch { }
        }

        found.Sort(StringComparer.OrdinalIgnoreCase);
        _originalImages.AddRange(found);

        if (_isShuffle)
        {
            ApplyShuffle();
        }
        else
        {
            _images.AddRange(_originalImages);
        }
    }

    public void HandleDropFiles(string[] filePaths)
    {
        if (filePaths == null || filePaths.Length == 0) return;
        var validExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif"
        };

        var newImages = new List<string>();
        string? targetImage = null;
        string? rememberedFolder = null;

        foreach (var path in filePaths)
        {
            if (Directory.Exists(path))
            {
                rememberedFolder ??= path;
                foreach (var ext in validExts)
                {
                    try { newImages.AddRange(Directory.GetFiles(path, "*" + ext)); } catch { }
                }
            }
            else if (File.Exists(path) && validExts.Contains(Path.GetExtension(path)))
            {
                if (targetImage == null)
                {
                    targetImage = path;
                    rememberedFolder ??= Path.GetDirectoryName(path);
                }
                newImages.Add(path);
            }
        }

        // If a single image was dropped, also scan its folder so user can flip through
        if (targetImage != null && filePaths.Length == 1 && rememberedFolder != null && Directory.Exists(rememberedFolder))
        {
            try
            {
                var siblingImages = new List<string>();
                foreach (var ext in validExts)
                {
                    try { siblingImages.AddRange(Directory.GetFiles(rememberedFolder, "*" + ext)); } catch { }
                }
                if (siblingImages.Count > 0)
                {
                    newImages = siblingImages.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                }
            }
            catch { }
        }

        if (newImages.Count > 0)
        {
            newImages.Sort(StringComparer.OrdinalIgnoreCase);
            _originalImages.Clear();
            _originalImages.AddRange(newImages);

            _images.Clear();
            if (_isShuffle)
            {
                ApplyShuffle();
            }
            else
            {
                _images.AddRange(_originalImages);
            }

            if (targetImage != null && _images.Contains(targetImage))
            {
                _index = _images.IndexOf(targetImage);
            }
            else
            {
                _index = 0;
            }

            if (rememberedFolder != null)
            {
                CustomFolder = rememberedFolder;
                SaveCustomSetting("mediaFolder", rememberedFolder);
            }

            ShowImage(_index);
        }
    }

    public void SetCustomFolder(string folderPath, bool saveToSettings = true)
    {
        if (!Directory.Exists(folderPath)) return;
        CustomFolder = folderPath;
        LoadImagesFromDirectory(folderPath);

        if (_images.Count > 0)
        {
            ShowImage(0);
        }
        else
        {
            CurrentImage = "";
            Caption = LocalizationService.Instance.T("MediaFrame.NoImagesFound", "В папке нет поддерживаемых изображений");
            OnPropertyChanged(nameof(HasImage));
            OnPropertyChanged(nameof(ImageVisibility));
            OnPropertyChanged(nameof(PlaceholderVisibility));
            _slideTimer.Stop();
        }

        if (saveToSettings)
        {
            SaveCustomSetting("mediaFolder", folderPath);
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

    private readonly string? _instanceId;
    private string _vinylSpeed = "normal";
    private bool _spinOnlyPlaying = true;

    public string VinylSpeed
    {
        get => _vinylSpeed;
        set
        {
            if (_vinylSpeed != value)
            {
                _vinylSpeed = value;
                OnPropertyChanged();
                SaveCustomSettings();
                UpdateSpinState();
            }
        }
    }

    public bool SpinOnlyPlaying
    {
        get => _spinOnlyPlaying;
        set
        {
            if (_spinOnlyPlaying != value)
            {
                _spinOnlyPlaying = value;
                OnPropertyChanged();
                SaveCustomSettings();
                UpdateSpinState();
            }
        }
    }

    public ICommand PlayPauseCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand PrevCommand { get; }
    public ICommand VolumeUpCommand { get; }
    public ICommand VolumeDownCommand { get; }

    public MusicPlayerWidgetViewModel() : this(null) { }

    public MusicPlayerWidgetViewModel(string? instanceId)
    {
        _instanceId = instanceId;
        LoadCustomSettings();

        _trackTitle = LocalizationService.Instance.T("Music.Waiting", "Ожидание медиа...");
        _artist = LocalizationService.Instance.T("Music.StartSource", "Запустите Spotify, Браузер...");

        LocalizationService.Instance.LanguageChanged += () =>
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                var cur = MediaMonitorService.Instance.CurrentMedia;
                if (cur == null || string.IsNullOrEmpty(cur.Title))
                {
                    TrackTitle = LocalizationService.Instance.T("Music.Waiting", "Ожидание медиа...");
                    Artist = LocalizationService.Instance.T("Music.StartSource", "Запустите Spotify, Браузер...");
                }
            });
        };

        _spinTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(30)
        };
        _spinTimer.Tick += (s, e) =>
        {
            double step = _vinylSpeed switch
            {
                "fast" => 1.8,
                "slow" => 0.6,
                "disabled" => 0.0,
                _ => 1.2
            };
            if (step > 0)
            {
                VinylAngle = (_vinylAngle + step) % 360.0;
            }
        };

        UpdateSpinState();

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
                    UpdateSpinState();
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
                UpdateSpinState();
                if (!string.IsNullOrEmpty(cur.ThumbnailBase64))
                {
                    CoverBitmap = LoadBitmapFromBase64(cur.ThumbnailBase64);
                }
            }
        }
        catch { }
    }

    private void UpdateSpinState()
    {
        if (_vinylSpeed == "disabled")
        {
            _spinTimer.Stop();
            return;
        }

        if (!_spinOnlyPlaying || _isPlaying)
        {
            if (!_spinTimer.IsEnabled) _spinTimer.Start();
        }
        else
        {
            if (_spinTimer.IsEnabled) _spinTimer.Stop();
        }
    }

    public void LoadCustomSettings()
    {
        if (!string.IsNullOrEmpty(_instanceId) &&
            WidgetManager.Instance.Settings.Widgets.TryGetValue(_instanceId, out var s))
        {
            if (s.CustomSettings.TryGetValue("musicVinylSpeed", out var sp) && !string.IsNullOrWhiteSpace(sp))
                _vinylSpeed = sp;
            if (s.CustomSettings.TryGetValue("musicSpinOnlyPlaying", out var sop) && bool.TryParse(sop, out var bSop))
                _spinOnlyPlaying = bSop;
        }
    }

    public void SaveCustomSettings()
    {
        if (string.IsNullOrEmpty(_instanceId)) return;
        if (!WidgetManager.Instance.Settings.Widgets.TryGetValue(_instanceId, out var s)) return;

        s.CustomSettings["musicVinylSpeed"] = _vinylSpeed;
        s.CustomSettings["musicSpinOnlyPlaying"] = _spinOnlyPlaying ? "true" : "false";
        WidgetManager.Instance.SaveSettings();
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
public enum PomodoroPhase
{
    Focus,
    ShortBreak,
    LongBreak
}

public class PomodoroWidgetViewModel : NativeViewModelBase
{
    private readonly string? _instanceId;
    private readonly DispatcherTimer _timer;

    private int _focusMinutes = 25;
    private int _breakMinutes = 5;
    private int _longBreakMinutes = 15;
    private bool _autoStart = false;
    private bool _soundEnabled = true;

    private int _remainingSeconds = 25 * 60;
    private bool _isRunning = false;
    private PomodoroPhase _currentPhase = PomodoroPhase.Focus;
    private int _completedSessions = 0;

    public int FocusMinutes => _focusMinutes;
    public int BreakMinutes => _breakMinutes;
    public int LongBreakMinutes => _longBreakMinutes;
    public bool AutoStart => _autoStart;
    public bool SoundEnabled => _soundEnabled;
    public bool IsRunning => _isRunning;
    public PomodoroPhase CurrentPhase => _currentPhase;
    public int CompletedSessions => _completedSessions;

    public string TimeText { get => $"{_remainingSeconds / 60:D2}:{_remainingSeconds % 60:D2}"; set {} }

    public string ModeText
    {
        get
        {
            var loc = LocalizationService.Instance;
            return _currentPhase switch
            {
                PomodoroPhase.ShortBreak => loc.T("Pomodoro.ModeBreak", "☕ ОТДЫХ"),
                PomodoroPhase.LongBreak => loc.T("Pomodoro.ModeLongBreak", "🌿 ДЛИННЫЙ ОТДЫХ"),
                _ => loc.T("Pomodoro.ModeFocus", "🎯 ФОКУС")
            };
        }
        set {}
    }

    public string ActionButtonText
    {
        get
        {
            var loc = LocalizationService.Instance;
            return _isRunning ? loc.T("Pomodoro.BtnPause", "Пауза") : loc.T("Pomodoro.BtnStart", "Старт");
        }
        set {}
    }

    public string ResetButtonText => LocalizationService.Instance.T("Pomodoro.BtnReset", "Сброс");

    public string SwitchModeText => LocalizationService.Instance.T("Pomodoro.SwitchMode", "Сменить режим");

    public string FocusTabTitle => string.Format(LocalizationService.Instance.T("Pomodoro.TabFocus", "🍅 Фокус ({0}м)"), _focusMinutes);

    public string BreakTabTitle => string.Format(
        LocalizationService.Instance.T(_currentPhase == PomodoroPhase.LongBreak ? "Pomodoro.TabLongBreak" : "Pomodoro.TabBreak", 
            _currentPhase == PomodoroPhase.LongBreak ? "🌿 Отдых ({0}м)" : "☕ Отдых ({0}м)"),
        _currentPhase == PomodoroPhase.LongBreak ? _longBreakMinutes : _breakMinutes);

    public string AccentColor
    {
        get => _currentPhase switch
        {
            PomodoroPhase.ShortBreak => "#10B981",
            PomodoroPhase.LongBreak => "#3B82F6",
            _ => "#EF4444"
        };
        set {}
    }

    public Brush FocusTabBackground => _currentPhase == PomodoroPhase.Focus 
        ? new SolidColorBrush(Color.FromArgb(55, 255, 255, 255)) 
        : Brushes.Transparent;

    public Brush FocusTabForeground => _currentPhase == PomodoroPhase.Focus 
        ? Brushes.White 
        : new SolidColorBrush(Color.FromRgb(148, 163, 184));

    public Brush BreakTabBackground => _currentPhase != PomodoroPhase.Focus 
        ? new SolidColorBrush(Color.FromArgb(55, 255, 255, 255)) 
        : Brushes.Transparent;

    public Brush BreakTabForeground => _currentPhase != PomodoroPhase.Focus 
        ? Brushes.White 
        : new SolidColorBrush(Color.FromRgb(148, 163, 184));

    public string StatusText
    {
        get
        {
            var loc = LocalizationService.Instance;
            if (_isRunning)
            {
                return _currentPhase switch
                {
                    PomodoroPhase.ShortBreak => loc.T("Pomodoro.BreakActive", "☕ Время перерыва"),
                    PomodoroPhase.LongBreak => loc.T("Pomodoro.LongBreakActive", "🌿 Восстанавливающий отдых"),
                    _ => loc.T("Pomodoro.SessionActive", "🍅 Фокус-сессия активна")
                };
            }

            int total = GetPhaseDurationSeconds(_currentPhase);
            if (_remainingSeconds < total)
            {
                return loc.T("Pomodoro.SessionPaused", "⏸ Таймер на паузе");
            }

            return string.Format(loc.T("Pomodoro.CompletedCount", "Выполнено сессий: {0}"), _completedSessions);
        }
    }

    public double ProgressPercent
    {
        get
        {
            int total = GetPhaseDurationSeconds(_currentPhase);
            return total > 0 ? ((double)(total - _remainingSeconds) / total) * 100 : 0;
        }
        set {}
    }

    public ICommand ToggleTimerCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand SwitchModeCommand { get; }
    public ICommand SetFocusModeCommand { get; }
    public ICommand SetBreakModeCommand { get; }
    public ICommand SetLongBreakModeCommand { get; }

    public PomodoroWidgetViewModel(string? instanceId = null)
    {
        _instanceId = instanceId;
        LoadSettings();

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
                OnIntervalCompleted();
            }
        };

        ToggleTimerCommand = new NativeRelayCommand(() =>
        {
            _isRunning = !_isRunning;
            if (_isRunning) _timer.Start(); else _timer.Stop();
            NotifyStateChanged();
        });

        ResetCommand = new NativeRelayCommand(() =>
        {
            _timer.Stop();
            _isRunning = false;
            _remainingSeconds = GetPhaseDurationSeconds(_currentPhase);
            NotifyStateChanged();
        });

        SwitchModeCommand = new NativeRelayCommand(() =>
        {
            if (_currentPhase == PomodoroPhase.Focus)
            {
                SwitchToPhase(PomodoroPhase.ShortBreak);
            }
            else
            {
                SwitchToPhase(PomodoroPhase.Focus);
            }
        });

        SetFocusModeCommand = new NativeRelayCommand(() => SwitchToPhase(PomodoroPhase.Focus));
        SetBreakModeCommand = new NativeRelayCommand(() => SwitchToPhase(PomodoroPhase.ShortBreak));
        SetLongBreakModeCommand = new NativeRelayCommand(() => SwitchToPhase(PomodoroPhase.LongBreak));

        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        NotifyAllProperties();
    }

    private void SwitchToPhase(PomodoroPhase phase)
    {
        _timer.Stop();
        _isRunning = false;
        _currentPhase = phase;
        _remainingSeconds = GetPhaseDurationSeconds(_currentPhase);
        NotifyStateChanged();
    }

    private void OnIntervalCompleted()
    {
        if (_soundEnabled)
        {
            try
            {
                System.Media.SystemSounds.Exclamation.Play();
            }
            catch { }
        }

        if (_currentPhase == PomodoroPhase.Focus)
        {
            _completedSessions++;
            if (_completedSessions % 4 == 0)
            {
                _currentPhase = PomodoroPhase.LongBreak;
            }
            else
            {
                _currentPhase = PomodoroPhase.ShortBreak;
            }
        }
        else
        {
            _currentPhase = PomodoroPhase.Focus;
        }

        _remainingSeconds = GetPhaseDurationSeconds(_currentPhase);

        if (_autoStart)
        {
            _isRunning = true;
            _timer.Start();
        }
        else
        {
            _isRunning = false;
            _timer.Stop();
        }

        NotifyStateChanged();
    }

    public int GetPhaseDurationSeconds(PomodoroPhase phase) => phase switch
    {
        PomodoroPhase.ShortBreak => _breakMinutes * 60,
        PomodoroPhase.LongBreak => _longBreakMinutes * 60,
        _ => _focusMinutes * 60
    };

    public void ApplyCustomSettings(int focusMins, int breakMins, int longBreakMins, bool autoStart, bool sound)
    {
        _focusMinutes = Math.Clamp(focusMins, 1, 180);
        _breakMinutes = Math.Clamp(breakMins, 1, 60);
        _longBreakMinutes = Math.Clamp(longBreakMins, 1, 90);
        _autoStart = autoStart;
        _soundEnabled = sound;

        SaveSettings();

        if (!_isRunning)
        {
            _remainingSeconds = GetPhaseDurationSeconds(_currentPhase);
        }

        NotifyAllProperties();
    }

    public void SetWorkMinutes(int mins)
    {
        ApplyCustomSettings(mins, _breakMinutes, _longBreakMinutes, _autoStart, _soundEnabled);
    }

    private void LoadSettings()
    {
        if (!string.IsNullOrEmpty(_instanceId) &&
            WidgetManager.Instance.Settings.Widgets.TryGetValue(_instanceId, out var s))
        {
            if (s.CustomSettings.TryGetValue("pomoFocusMins", out var fVal) && int.TryParse(fVal, out var f))
                _focusMinutes = f;
            if (s.CustomSettings.TryGetValue("pomoBreakMins", out var bVal) && int.TryParse(bVal, out var b))
                _breakMinutes = b;
            if (s.CustomSettings.TryGetValue("pomoLongBreakMins", out var lbVal) && int.TryParse(lbVal, out var lb))
                _longBreakMinutes = lb;
            if (s.CustomSettings.TryGetValue("pomoAutoStart", out var asVal) && bool.TryParse(asVal, out var a))
                _autoStart = a;
            if (s.CustomSettings.TryGetValue("pomoSound", out var sndVal) && bool.TryParse(sndVal, out var snd))
                _soundEnabled = snd;
        }

        _remainingSeconds = GetPhaseDurationSeconds(_currentPhase);
    }

    private void SaveSettings()
    {
        if (!string.IsNullOrEmpty(_instanceId) &&
            WidgetManager.Instance.Settings.Widgets.TryGetValue(_instanceId, out var s))
        {
            s.CustomSettings["pomoFocusMins"] = _focusMinutes.ToString();
            s.CustomSettings["pomoBreakMins"] = _breakMinutes.ToString();
            s.CustomSettings["pomoLongBreakMins"] = _longBreakMinutes.ToString();
            s.CustomSettings["pomoAutoStart"] = _autoStart ? "true" : "false";
            s.CustomSettings["pomoSound"] = _soundEnabled ? "true" : "false";
            WidgetManager.Instance.SaveSettings();
        }
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(ModeText));
        OnPropertyChanged(nameof(ActionButtonText));
        OnPropertyChanged(nameof(AccentColor));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(FocusTabTitle));
        OnPropertyChanged(nameof(BreakTabTitle));
        OnPropertyChanged(nameof(FocusTabBackground));
        OnPropertyChanged(nameof(FocusTabForeground));
        OnPropertyChanged(nameof(BreakTabBackground));
        OnPropertyChanged(nameof(BreakTabForeground));
    }

    public void NotifyAllProperties()
    {
        NotifyStateChanged();
        OnPropertyChanged(nameof(ResetButtonText));
        OnPropertyChanged(nameof(SwitchModeText));
    }
}

// 7. Quick Dock ViewModel
public class DockAppItem
{
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Tooltip { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public ICommand LaunchCommand { get; set; } = null!;
}

public class QuickDockWidgetViewModel : NativeViewModelBase
{
    private readonly string? _instanceId;
    private readonly string _storageFile;
    private double _iconSize = 40;

    public double IconSize
    {
        get => _iconSize;
        set
        {
            if (_iconSize != value)
            {
                _iconSize = value;
                OnPropertyChanged();
                SaveCustomSettings();
            }
        }
    }

    public ObservableCollection<DockAppItem> Apps { get; } = new();

    public QuickDockWidgetViewModel() : this(null) { }

    public QuickDockWidgetViewModel(string? instanceId)
    {
        _instanceId = instanceId;
        string dir = Path.Combine(ChwPackageService.DataDir, instanceId ?? "quick-dock");
        Directory.CreateDirectory(dir);
        _storageFile = Path.Combine(dir, "dock_items.json");

        LoadCustomSettings();
        PopulateApps();

        LocalizationService.Instance.LanguageChanged += () =>
        {
            Application.Current?.Dispatcher?.Invoke(PopulateApps);
        };
    }

    public void LoadCustomSettings()
    {
        if (!string.IsNullOrEmpty(_instanceId) &&
            WidgetManager.Instance.Settings.Widgets.TryGetValue(_instanceId, out var s))
        {
            if (s.CustomSettings.TryGetValue("dockIconSize", out var sizeStr) && double.TryParse(sizeStr, out var sz))
            {
                _iconSize = sz;
            }
        }
    }

    public void SaveCustomSettings()
    {
        if (string.IsNullOrEmpty(_instanceId)) return;
        if (!WidgetManager.Instance.Settings.Widgets.TryGetValue(_instanceId, out var s)) return;

        s.CustomSettings["dockIconSize"] = _iconSize.ToString();
        WidgetManager.Instance.SaveSettings();
    }

    public void PopulateApps()
    {
        Apps.Clear();
        var loc = LocalizationService.Instance;

        var custom = LoadCustomItems();
        if (custom.Count > 0)
        {
            foreach (var c in custom)
            {
                AddApp(c.Name, c.Icon, c.Name, c.Path);
            }
            return;
        }

        AddApp(loc.T("QuickDock.Explorer", "Проводник"), "📁", loc.T("QuickDock.Explorer", "Проводник"), "explorer.exe");
        AddApp(loc.T("QuickDock.Terminal", "Терминал"), "💻", loc.T("QuickDock.Terminal", "PowerShell / Terminal"), "wt.exe", () => Process.Start(new ProcessStartInfo("wt.exe") { UseShellExecute = true, ErrorDialog = false }));
        AddApp(loc.T("QuickDock.Browser", "Браузер"), "🌐", loc.T("QuickDock.Browser", "Открыть браузер"), "https://google.com");
        AddApp(loc.T("QuickDock.Notepad", "Блокнот"), "📝", loc.T("QuickDock.Notepad", "Быстрый блокнот"), "notepad.exe");
        AddApp(loc.T("QuickDock.Calc", "Калькулятор"), "🔢", loc.T("QuickDock.Calc", "Калькулятор Windows"), "calc.exe");
        AddApp(loc.T("QuickDock.TaskMgr", "Диспетчер"), "⚡", loc.T("QuickDock.TaskMgr", "Диспетчер задач"), "taskmgr.exe");
    }

    public void AddCustomApp(string name, string icon, string targetPath)
    {
        var items = LoadCustomItems();
        items.Add(new CustomDockItem { Name = name, Icon = icon, Path = targetPath });
        SaveCustomItems(items);
        PopulateApps();
    }

    public void ResetDefaults()
    {
        try { if (File.Exists(_storageFile)) File.Delete(_storageFile); } catch { }
        PopulateApps();
    }

    private class CustomDockItem
    {
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Path { get; set; } = "";
    }

    private List<CustomDockItem> LoadCustomItems()
    {
        try
        {
            if (File.Exists(_storageFile))
            {
                string json = File.ReadAllText(_storageFile);
                return JsonSerializer.Deserialize<List<CustomDockItem>>(json) ?? new List<CustomDockItem>();
            }
        }
        catch { }
        return new List<CustomDockItem>();
    }

    private void SaveCustomItems(List<CustomDockItem> items)
    {
        try
        {
            File.WriteAllText(_storageFile, JsonSerializer.Serialize(items));
        }
        catch { }
    }

    private void AddApp(string name, string icon, string tip, string path, Action? customAction = null)
    {
        Apps.Add(new DockAppItem
        {
            Name = name,
            Icon = icon,
            Tooltip = tip,
            TargetPath = path,
            LaunchCommand = new NativeRelayCommand(() =>
            {
                try
                {
                    if (customAction != null) customAction();
                    else Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
                catch { }
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
    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
    private readonly string? _instanceId;
    private readonly DispatcherTimer _slideTimer;

    private string _preset = "world";
    private string _customUrl = "";
    private int _slideIntervalSec = 12;
    private int _currentIndex = 0;

    public string Preset
    {
        get => _preset;
        set
        {
            if (_preset != value)
            {
                _preset = value;
                OnPropertyChanged();
                SaveCustomSettings();
                _ = FetchFeedAsync();
            }
        }
    }

    public string CustomUrl
    {
        get => _customUrl;
        set
        {
            if (_customUrl != value)
            {
                _customUrl = value;
                OnPropertyChanged();
                SaveCustomSettings();
            }
        }
    }

    public int SlideIntervalSec
    {
        get => _slideIntervalSec;
        set
        {
            if (_slideIntervalSec != value)
            {
                _slideIntervalSec = value;
                OnPropertyChanged();
                UpdateSlideTimer();
                SaveCustomSettings();
            }
        }
    }

    public string FeedTitle
    {
        get
        {
            var loc = LocalizationService.Instance;
            return _preset switch
            {
                "tech" => loc.T("Rss.CategoryTech", "AI И ТЕХНОЛОГИИ"),
                "crypto" => loc.T("Rss.CategoryCrypto", "ГРАМ И КРИПТА"),
                "habr" => "HABR IT",
                "science" => "NASA & SCIENCE",
                "custom" => "CUSTOM RSS",
                _ => loc.T("Rss.CategoryWorld", "МИРОВЫЕ НОВОСТИ")
            };
        }
    }

    public ObservableCollection<RssArticleItem> Articles { get; } = new();

    public RssArticleItem? CurrentArticle => Articles.Count > 0 && _currentIndex >= 0 && _currentIndex < Articles.Count ? Articles[_currentIndex] : null;
    public string CounterText => Articles.Count > 0 ? $"{_currentIndex + 1} / {Articles.Count}" : "0 / 0";

    public ICommand NextSlideCommand { get; }
    public ICommand PrevSlideCommand { get; }
    public ICommand RefreshCommand { get; }

    public RssWidgetViewModel() : this(null) { }

    public RssWidgetViewModel(string? instanceId)
    {
        _instanceId = instanceId;
        LoadCustomSettings();

        NextSlideCommand = new NativeRelayCommand(() => NextSlide());
        PrevSlideCommand = new NativeRelayCommand(() => PrevSlide());
        RefreshCommand = new NativeRelayCommand(() => _ = FetchFeedAsync());

        _slideTimer = new DispatcherTimer();
        _slideTimer.Tick += (s, e) => NextSlide();
        UpdateSlideTimer();

        LocalizationService.Instance.LanguageChanged += () =>
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                OnPropertyChanged(nameof(FeedTitle));
                OnPropertyChanged(nameof(CurrentArticle));
                OnPropertyChanged(nameof(CounterText));
            });
        };

        PopulateFallbackArticles();
        _ = FetchFeedAsync();
    }

    private void NextSlide()
    {
        if (Articles.Count == 0) return;
        _currentIndex = (_currentIndex + 1) % Articles.Count;
        OnPropertyChanged(nameof(CurrentArticle));
        OnPropertyChanged(nameof(CounterText));
    }

    private void PrevSlide()
    {
        if (Articles.Count == 0) return;
        _currentIndex = (_currentIndex - 1 + Articles.Count) % Articles.Count;
        OnPropertyChanged(nameof(CurrentArticle));
        OnPropertyChanged(nameof(CounterText));
    }

    private void UpdateSlideTimer()
    {
        if (_slideIntervalSec > 0)
        {
            _slideTimer.Interval = TimeSpan.FromSeconds(_slideIntervalSec);
            if (!_slideTimer.IsEnabled) _slideTimer.Start();
        }
        else
        {
            _slideTimer.Stop();
        }
    }

    public void LoadCustomSettings()
    {
        if (!string.IsNullOrEmpty(_instanceId) &&
            WidgetManager.Instance.Settings.Widgets.TryGetValue(_instanceId, out var s))
        {
            if (s.CustomSettings.TryGetValue("rssFeedPreset", out var p) && !string.IsNullOrWhiteSpace(p)) _preset = p;
            if (s.CustomSettings.TryGetValue("rssCustomUrl", out var u)) _customUrl = u;
            if (s.CustomSettings.TryGetValue("rssAutoSlideSec", out var secStr) && int.TryParse(secStr, out var sec)) _slideIntervalSec = sec;
        }
    }

    public void SaveCustomSettings()
    {
        if (string.IsNullOrEmpty(_instanceId)) return;
        if (!WidgetManager.Instance.Settings.Widgets.TryGetValue(_instanceId, out var s)) return;

        s.CustomSettings["rssFeedPreset"] = _preset;
        s.CustomSettings["rssCustomUrl"] = _customUrl;
        s.CustomSettings["rssAutoSlideSec"] = _slideIntervalSec.ToString();
        WidgetManager.Instance.SaveSettings();
    }

    public async Task FetchFeedAsync()
    {
        string url = _preset switch
        {
            "tech" => "https://news.ycombinator.com/rss",
            "crypto" => "https://www.coindesk.com/arc/outboundfeeds/rss/",
            "habr" => "https://habr.com/ru/rss/all/all/",
            "science" => "https://www.nasa.gov/rss/dyn/breaking_news.rss",
            "custom" => !string.IsNullOrWhiteSpace(_customUrl) ? _customUrl : "https://feeds.bbci.co.uk/news/world/rss.xml",
            _ => "https://feeds.bbci.co.uk/news/world/rss.xml"
        };

        try
        {
            string xml = await _httpClient.GetStringAsync(url);
            var doc = XDocument.Parse(xml);
            var items = doc.Descendants("item").Take(15).ToList();
            if (items.Count == 0) items = doc.Descendants(XName.Get("entry", "http://www.w3.org/2005/Atom")).Take(15).ToList();

            if (items.Count > 0)
            {
                var newArticles = new List<RssArticleItem>();
                foreach (var it in items)
                {
                    string title = it.Element("title")?.Value?.Trim() ?? "";
                    string link = it.Element("link")?.Value?.Trim() ?? it.Element(XName.Get("link", "http://www.w3.org/2005/Atom"))?.Attribute("href")?.Value ?? "";
                    string dateStr = it.Element("pubDate")?.Value ?? it.Element("pubdate")?.Value ?? it.Element(XName.Get("updated", "http://www.w3.org/2005/Atom"))?.Value ?? "";

                    string displayTime = "сегодня";
                    if (DateTime.TryParse(dateStr, out var dt))
                    {
                        var diff = DateTime.UtcNow - dt.ToUniversalTime();
                        if (diff.TotalMinutes < 60) displayTime = $"{(int)diff.TotalMinutes} мин назад";
                        else if (diff.TotalHours < 24) displayTime = $"{(int)diff.TotalHours} ч назад";
                        else displayTime = dt.ToString("dd.MM");
                    }

                    if (!string.IsNullOrEmpty(title))
                    {
                        var article = new RssArticleItem
                        {
                            Title = title,
                            Source = _preset.ToUpperInvariant(),
                            Time = displayTime,
                            Url = link
                        };
                        article.OpenCommand = new NativeRelayCommand(() =>
                        {
                            try { if (!string.IsNullOrEmpty(article.Url)) Process.Start(new ProcessStartInfo(article.Url) { UseShellExecute = true }); } catch { }
                        });
                        newArticles.Add(article);
                    }
                }

                if (newArticles.Count > 0)
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        Articles.Clear();
                        foreach (var a in newArticles) Articles.Add(a);
                        _currentIndex = 0;
                        OnPropertyChanged(nameof(FeedTitle));
                        OnPropertyChanged(nameof(CurrentArticle));
                        OnPropertyChanged(nameof(CounterText));
                    });
                    return;
                }
            }
        }
        catch { }

        PopulateFallbackArticles();
    }

    private void PopulateFallbackArticles()
    {
        bool isRu = LocalizationService.Instance.IsRussian;
        Articles.Clear();

        if (isRu)
        {
            Articles.Add(new RssArticleItem { Title = "OpenAI и DeepMind анонсировали новые мультимодальные модели следующего поколения", Source = "AI & TECH", Time = "10 мин назад", Url = "https://news.ycombinator.com" });
            Articles.Add(new RssArticleItem { Title = "Microsoft представила новые визуальные API для Windows 11 с нативной поддержкой Mica", Source = "WINDOWS", Time = "42 мин назад", Url = "https://blogs.windows.com" });
            Articles.Add(new RssArticleItem { Title = "Сеть Грам (GRAM) установила рекорд скорости транзакций и активных кошельков", Source = "ГРАМ", Time = "1 час назад", Url = "https://ton.org" });
            Articles.Add(new RssArticleItem { Title = "Релиз ChattyWidgets 2.0: Полный переход на нативный стек и сверхбыстрые виджеты", Source = "CHATTY", Time = "2 часа назад", Url = "https://github.com/chattytoaster/ChattyWidgets" });
        }
        else
        {
            Articles.Add(new RssArticleItem { Title = "DeepMind and OpenAI unveil next-generation multimodal agent models", Source = "AI & TECH", Time = "10m ago", Url = "https://news.ycombinator.com" });
            Articles.Add(new RssArticleItem { Title = "Microsoft introduces refreshed Mica visual APIs for Windows desktop apps", Source = "WINDOWS", Time = "42m ago", Url = "https://blogs.windows.com" });
            Articles.Add(new RssArticleItem { Title = "Gram (GRAM) network hits all-time record throughput and active addresses", Source = "GRAM", Time = "1h ago", Url = "https://ton.org" });
            Articles.Add(new RssArticleItem { Title = "ChattyWidgets 2.0 release: Full native DirectComposition desktop engine", Source = "CHATTY", Time = "2h ago", Url = "https://github.com/chattytoaster/ChattyWidgets" });
        }

        foreach (var a in Articles)
        {
            a.OpenCommand = new NativeRelayCommand(() =>
            {
                try { Process.Start(new ProcessStartInfo(a.Url) { UseShellExecute = true }); } catch { }
            });
        }

        _currentIndex = 0;
        OnPropertyChanged(nameof(FeedTitle));
        OnPropertyChanged(nameof(CurrentArticle));
        OnPropertyChanged(nameof(CounterText));
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
    private readonly string? _instanceId;
    public ObservableCollection<NoteTaskItem> Tasks { get; } = new();

    private string _newTaskText = "";
    public string NewTaskText
    {
        get => _newTaskText;
        set
        {
            _newTaskText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PlaceholderVisibility));
            OnPropertyChanged(nameof(CounterBadgeText));
            OnPropertyChanged(nameof(CanAddTask));
        }
    }

    private string _accentColor = "amber";
    public string AccentColor
    {
        get => _accentColor;
        set
        {
            if (_accentColor != value)
            {
                _accentColor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackgroundBrush));
                OnPropertyChanged(nameof(HeaderBrush));
                OnPropertyChanged(nameof(AccentBrush));
                OnPropertyChanged(nameof(AccentSoftBrush));
                OnPropertyChanged(nameof(BorderAccentBrush));
                SaveCustomSettings();
            }
        }
    }

    public string HeaderTitle => LocalizationService.Instance.T("StickyNotes.Title", "Заметки и задачи");

    public Brush HeaderBrush => _accentColor switch
    {
        "sky" => new SolidColorBrush(Color.FromRgb(56, 189, 248)),
        "emerald" => new SolidColorBrush(Color.FromRgb(52, 211, 153)),
        "violet" => new SolidColorBrush(Color.FromRgb(192, 132, 252)),
        "slate" => new SolidColorBrush(Color.FromRgb(203, 213, 225)),
        _ => new SolidColorBrush(Color.FromRgb(251, 191, 36)) // amber
    };

    public Brush AccentBrush => _accentColor switch
    {
        "sky" => new SolidColorBrush(Color.FromRgb(56, 189, 248)),
        "emerald" => new SolidColorBrush(Color.FromRgb(52, 211, 153)),
        "violet" => new SolidColorBrush(Color.FromRgb(192, 132, 252)),
        "slate" => new SolidColorBrush(Color.FromRgb(148, 163, 184)),
        _ => new SolidColorBrush(Color.FromRgb(251, 191, 36))
    };

    public Brush AccentSoftBrush => _accentColor switch
    {
        "sky" => new SolidColorBrush(Color.FromArgb(40, 56, 189, 248)),
        "emerald" => new SolidColorBrush(Color.FromArgb(40, 52, 211, 153)),
        "violet" => new SolidColorBrush(Color.FromArgb(40, 192, 132, 252)),
        "slate" => new SolidColorBrush(Color.FromArgb(40, 148, 163, 184)),
        _ => new SolidColorBrush(Color.FromArgb(40, 251, 191, 36))
    };

    public Brush BorderAccentBrush => _accentColor switch
    {
        "sky" => new SolidColorBrush(Color.FromArgb(60, 56, 189, 248)),
        "emerald" => new SolidColorBrush(Color.FromArgb(60, 52, 211, 153)),
        "violet" => new SolidColorBrush(Color.FromArgb(60, 192, 132, 252)),
        "slate" => new SolidColorBrush(Color.FromArgb(50, 148, 163, 184)),
        _ => new SolidColorBrush(Color.FromArgb(60, 251, 191, 36))
    };

    public Brush BackgroundBrush => _accentColor switch
    {
        "sky" => new SolidColorBrush(Color.FromArgb(220, 15, 30, 50)),
        "emerald" => new SolidColorBrush(Color.FromArgb(220, 10, 40, 25)),
        "violet" => new SolidColorBrush(Color.FromArgb(220, 35, 15, 50)),
        "slate" => new SolidColorBrush(Color.FromArgb(220, 25, 30, 40)),
        _ => new SolidColorBrush(Color.FromArgb(220, 30, 25, 15)) // amber
    };

    public string ActiveCountText
    {
        get
        {
            int count = Tasks.Count(t => !t.IsDone);
            return string.Format(LocalizationService.Instance.T("StickyNotes.ActiveCount", "{0} активных"), count);
        }
    }

    public string PlaceholderText => LocalizationService.Instance.T("StickyNotes.Placeholder", "Новая задача или заметка...");
    public string ClearCompletedTooltip => LocalizationService.Instance.T("StickyNotes.ClearCompletedTooltip", "Очистить выполненные задачи");
    public string AddTaskTooltip => LocalizationService.Instance.T("StickyNotes.AddTaskTooltip", "Добавить задачу (Enter)");
    public string EmptyStateText => LocalizationService.Instance.T("StickyNotes.EmptyState", "Все задачи выполнены! Отличная работа ✨");
    public Visibility PlaceholderVisibility => string.IsNullOrEmpty(NewTaskText) ? Visibility.Visible : Visibility.Collapsed;
    public string CounterBadgeText => $"{NewTaskText?.Length ?? 0}/120";
    public bool CanAddTask => !string.IsNullOrWhiteSpace(NewTaskText);
    public double NoteFontSize => 12.0;

    public ICommand AddTaskCommand { get; }
    public ICommand ClearCompletedCommand { get; }

    public StickyNotesWidgetViewModel(string instanceId, FrameworkElement? rootElement)
    {
        _instanceId = instanceId;
        string dir = Path.Combine(ChwPackageService.DataDir, instanceId);
        Directory.CreateDirectory(dir);
        _storageFile = Path.Combine(dir, "tasks.json");

        LoadCustomSettings();

        ClearCompletedCommand = new NativeRelayCommand(ClearCompletedTasks);

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

        LocalizationService.Instance.LanguageChanged += () =>
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                OnPropertyChanged(nameof(HeaderTitle));
                OnPropertyChanged(nameof(ActiveCountText));
                OnPropertyChanged(nameof(PlaceholderText));
                OnPropertyChanged(nameof(ClearCompletedTooltip));
                OnPropertyChanged(nameof(AddTaskTooltip));
                OnPropertyChanged(nameof(EmptyStateText));
            });
        };

        Load();
    }

    public void AddSampleTasks()
    {
        bool isRu = LocalizationService.Instance.IsRussian;
        string[] samples = isRu
            ? new[] { "Создать свой виджет (.chw)", "Открыть каталог ChattyWidgets", "Настроить параметры на рабочем столе" }
            : new[] { "Create custom widget (.chw)", "Explore ChattyWidgets store", "Customize desktop widgets layer" };

        foreach (var text in samples)
        {
            var item = new NoteTaskItem { Text = text, IsDone = false };
            item.DeleteCommand = new NativeRelayCommand(() => RemoveTask(item));
            item.Toggled = () => { Save(); OnPropertyChanged(nameof(ActiveCountText)); };
            Tasks.Add(item);
        }
        Save();
        OnPropertyChanged(nameof(ActiveCountText));
    }

    public void LoadCustomSettings()
    {
        if (!string.IsNullOrEmpty(_instanceId) &&
            WidgetManager.Instance.Settings.Widgets.TryGetValue(_instanceId, out var s))
        {
            if (s.CustomSettings.TryGetValue("notesAccentColor", out var col) && !string.IsNullOrWhiteSpace(col))
                _accentColor = col;
        }
    }

    public void SaveCustomSettings()
    {
        if (string.IsNullOrEmpty(_instanceId)) return;
        if (!WidgetManager.Instance.Settings.Widgets.TryGetValue(_instanceId, out var s)) return;

        s.CustomSettings["notesAccentColor"] = _accentColor;
        WidgetManager.Instance.SaveSettings();
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
            AddSampleTasks();
        }
        OnPropertyChanged(nameof(ActiveCountText));
    }
}

// 10. Weather Widget Models & International Curated Presets
public class WorldCityInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string NameRu { get; set; } = "";
    public string Country { get; set; } = "";
    public string CountryRu { get; set; } = "";
    public string CountryCode { get; set; } = "";
    public string Flag { get; set; } = "🌍";
    public double Lat { get; set; }
    public double Lon { get; set; }
    public string Timezone { get; set; } = "auto";

    public string DisplayName(bool isRussian)
    {
        string city = isRussian && !string.IsNullOrWhiteSpace(NameRu) ? NameRu : Name;
        string country = isRussian && !string.IsNullOrWhiteSpace(CountryRu) ? CountryRu : Country;
        return $"{Flag} {city}, {country}";
    }
}

public static class WeatherConstants
{
    public static readonly List<WorldCityInfo> CuratedCities = new()
    {
        // Russia & CIS
        new() { Id = "mow-ru", Name = "Moscow", NameRu = "Москва", Country = "Russia", CountryRu = "Россия", CountryCode = "RU", Flag = "🇷🇺", Lat = 55.7558, Lon = 37.6173, Timezone = "Europe/Moscow" },
        new() { Id = "spb-ru", Name = "Saint Petersburg", NameRu = "Санкт-Петербург", Country = "Russia", CountryRu = "Россия", CountryCode = "RU", Flag = "🇷🇺", Lat = 59.9343, Lon = 30.3351, Timezone = "Europe/Moscow" },
        new() { Id = "nsk-ru", Name = "Novosibirsk", NameRu = "Новосибирск", Country = "Russia", CountryRu = "Россия", CountryCode = "RU", Flag = "🇷🇺", Lat = 55.0084, Lon = 82.9357, Timezone = "Asia/Novosibirsk" },
        new() { Id = "ekb-ru", Name = "Yekaterinburg", NameRu = "Екатеринбург", Country = "Russia", CountryRu = "Россия", CountryCode = "RU", Flag = "🇷🇺", Lat = 56.8389, Lon = 60.6057, Timezone = "Asia/Yekaterinburg" },
        new() { Id = "kzn-ru", Name = "Kazan", NameRu = "Казань", Country = "Russia", CountryRu = "Россия", CountryCode = "RU", Flag = "🇷🇺", Lat = 55.8304, Lon = 49.0661, Timezone = "Europe/Moscow" },
        new() { Id = "aer-ru", Name = "Sochi", NameRu = "Сочи", Country = "Russia", CountryRu = "Россия", CountryCode = "RU", Flag = "🇷🇺", Lat = 43.6028, Lon = 39.7342, Timezone = "Europe/Moscow" },
        new() { Id = "vvo-ru", Name = "Vladivostok", NameRu = "Владивосток", Country = "Russia", CountryRu = "Россия", CountryCode = "RU", Flag = "🇷🇺", Lat = 43.1198, Lon = 131.8869, Timezone = "Asia/Vladivostok" },
        new() { Id = "ala-kz", Name = "Almaty", NameRu = "Алматы", Country = "Kazakhstan", CountryRu = "Казахстан", CountryCode = "KZ", Flag = "🇰🇿", Lat = 43.2389, Lon = 76.8897, Timezone = "Asia/Almaty" },
        new() { Id = "tse-kz", Name = "Astana", NameRu = "Астана", Country = "Kazakhstan", CountryRu = "Казахстан", CountryCode = "KZ", Flag = "🇰🇿", Lat = 51.1694, Lon = 71.4491, Timezone = "Asia/Almaty" },
        new() { Id = "tas-uz", Name = "Tashkent", NameRu = "Ташкент", Country = "Uzbekistan", CountryRu = "Узбекистан", CountryCode = "UZ", Flag = "🇺🇿", Lat = 41.2995, Lon = 69.2401, Timezone = "Asia/Tashkent" },
        new() { Id = "msq-by", Name = "Minsk", NameRu = "Минск", Country = "Belarus", CountryRu = "Беларусь", CountryCode = "BY", Flag = "🇧🇾", Lat = 53.9045, Lon = 27.5615, Timezone = "Europe/Minsk" },
        new() { Id = "evn-am", Name = "Yerevan", NameRu = "Ереван", Country = "Armenia", CountryRu = "Армения", CountryCode = "AM", Flag = "🇦🇲", Lat = 40.1792, Lon = 44.4991, Timezone = "Asia/Yerevan" },
        new() { Id = "tbs-ge", Name = "Tbilisi", NameRu = "Тбилиси", Country = "Georgia", CountryRu = "Грузия", CountryCode = "GE", Flag = "🇬🇪", Lat = 41.7151, Lon = 44.8271, Timezone = "Asia/Tbilisi" },
        new() { Id = "gyd-az", Name = "Baku", NameRu = "Баку", Country = "Azerbaijan", CountryRu = "Азербайджан", CountryCode = "AZ", Flag = "🇦🇿", Lat = 40.4093, Lon = 49.8671, Timezone = "Asia/Baku" },

        // Europe
        new() { Id = "lon-uk", Name = "London", NameRu = "Лондон", Country = "United Kingdom", CountryRu = "Великобритания", CountryCode = "GB", Flag = "🇬🇧", Lat = 51.5074, Lon = -0.1278, Timezone = "Europe/London" },
        new() { Id = "par-fr", Name = "Paris", NameRu = "Париж", Country = "France", CountryRu = "Франция", CountryCode = "FR", Flag = "🇫🇷", Lat = 48.8566, Lon = 2.3522, Timezone = "Europe/Paris" },
        new() { Id = "ber-de", Name = "Berlin", NameRu = "Берлин", Country = "Germany", CountryRu = "Германия", CountryCode = "DE", Flag = "🇩🇪", Lat = 52.5200, Lon = 13.4050, Timezone = "Europe/Berlin" },
        new() { Id = "rom-it", Name = "Rome", NameRu = "Рим", Country = "Italy", CountryRu = "Италия", CountryCode = "IT", Flag = "🇮🇹", Lat = 41.9028, Lon = 12.4964, Timezone = "Europe/Rome" },
        new() { Id = "mad-es", Name = "Madrid", NameRu = "Мадрид", Country = "Spain", CountryRu = "Испания", CountryCode = "ES", Flag = "🇪🇸", Lat = 40.4168, Lon = -3.7038, Timezone = "Europe/Madrid" },
        new() { Id = "ams-nl", Name = "Amsterdam", NameRu = "Амстердам", Country = "Netherlands", CountryRu = "Нидерланды", CountryCode = "NL", Flag = "🇳🇱", Lat = 52.3676, Lon = 4.9041, Timezone = "Europe/Amsterdam" },
        new() { Id = "vie-at", Name = "Vienna", NameRu = "Вена", Country = "Austria", CountryRu = "Австрия", CountryCode = "AT", Flag = "🇦🇹", Lat = 48.2082, Lon = 16.3738, Timezone = "Europe/Vienna" },
        new() { Id = "waw-pl", Name = "Warsaw", NameRu = "Варшава", Country = "Poland", CountryRu = "Польша", CountryCode = "PL", Flag = "🇵🇱", Lat = 52.2297, Lon = 21.0122, Timezone = "Europe/Warsaw" },
        new() { Id = "prg-cz", Name = "Prague", NameRu = "Прага", Country = "Czech Republic", CountryRu = "Чехия", CountryCode = "CZ", Flag = "🇨🇿", Lat = 50.0755, Lon = 14.4378, Timezone = "Europe/Prague" },
        new() { Id = "ist-tr", Name = "Istanbul", NameRu = "Стамбул", Country = "Turkey", CountryRu = "Турция", CountryCode = "TR", Flag = "🇹🇷", Lat = 41.0082, Lon = 28.9784, Timezone = "Europe/Istanbul" },

        // North America
        new() { Id = "nyc-us", Name = "New York", NameRu = "Нью-Йорк", Country = "United States", CountryRu = "США", CountryCode = "US", Flag = "🇺🇸", Lat = 40.7128, Lon = -74.0060, Timezone = "America/New_York" },
        new() { Id = "lax-us", Name = "Los Angeles", NameRu = "Лос-Анджелес", Country = "United States", CountryRu = "США", CountryCode = "US", Flag = "🇺🇸", Lat = 34.0522, Lon = -118.2437, Timezone = "America/Los_Angeles" },
        new() { Id = "chi-us", Name = "Chicago", NameRu = "Чикаго", Country = "United States", CountryRu = "США", CountryCode = "US", Flag = "🇺🇸", Lat = 41.8781, Lon = -87.6298, Timezone = "America/Chicago" },
        new() { Id = "mia-us", Name = "Miami", NameRu = "Майами", Country = "United States", CountryRu = "США", CountryCode = "US", Flag = "🇺🇸", Lat = 25.7617, Lon = -80.1918, Timezone = "America/New_York" },
        new() { Id = "yyz-ca", Name = "Toronto", NameRu = "Торонто", Country = "Canada", CountryRu = "Канада", CountryCode = "CA", Flag = "🇨🇦", Lat = 43.6532, Lon = -79.3832, Timezone = "America/Toronto" },
        new() { Id = "yvr-ca", Name = "Vancouver", NameRu = "Ванкувер", Country = "Canada", CountryRu = "Канада", CountryCode = "CA", Flag = "🇨🇦", Lat = 49.2827, Lon = -123.1207, Timezone = "America/Vancouver" },

        // Asia & Pacific
        new() { Id = "tok-jp", Name = "Tokyo", NameRu = "Токио", Country = "Japan", CountryRu = "Япония", CountryCode = "JP", Flag = "🇯🇵", Lat = 35.6762, Lon = 139.6503, Timezone = "Asia/Tokyo" },
        new() { Id = "bjs-cn", Name = "Beijing", NameRu = "Пекин", Country = "China", CountryRu = "Китай", CountryCode = "CN", Flag = "🇨🇳", Lat = 39.9042, Lon = 116.4074, Timezone = "Asia/Shanghai" },
        new() { Id = "sha-cn", Name = "Shanghai", NameRu = "Шанхай", Country = "China", CountryRu = "Китай", CountryCode = "CN", Flag = "🇨🇳", Lat = 31.2304, Lon = 121.4737, Timezone = "Asia/Shanghai" },
        new() { Id = "sin-sg", Name = "Singapore", NameRu = "Сингапур", Country = "Singapore", CountryRu = "Сингапур", CountryCode = "SG", Flag = "🇸🇬", Lat = 1.3521, Lon = 103.8198, Timezone = "Asia/Singapore" },
        new() { Id = "sel-kr", Name = "Seoul", NameRu = "Сеул", Country = "South Korea", CountryRu = "Южная Корея", CountryCode = "KR", Flag = "🇰🇷", Lat = 37.5665, Lon = 126.9780, Timezone = "Asia/Seoul" },
        new() { Id = "bkk-th", Name = "Bangkok", NameRu = "Бангкок", Country = "Thailand", CountryRu = "Таиланд", CountryCode = "TH", Flag = "🇹🇭", Lat = 13.7563, Lon = 100.5018, Timezone = "Asia/Bangkok" },
        new() { Id = "del-in", Name = "New Delhi", NameRu = "Нью-Дели", Country = "India", CountryRu = "Индия", CountryCode = "IN", Flag = "🇮🇳", Lat = 28.6139, Lon = 77.2090, Timezone = "Asia/Kolkata" },

        // Middle East & Africa
        new() { Id = "dxb-ae", Name = "Dubai", NameRu = "Дубай", Country = "United Arab Emirates", CountryRu = "ОАЭ", CountryCode = "AE", Flag = "🇦🇪", Lat = 25.2048, Lon = 55.2708, Timezone = "Asia/Dubai" },
        new() { Id = "doh-qa", Name = "Doha", NameRu = "Доха", Country = "Qatar", CountryRu = "Катар", CountryCode = "QA", Flag = "🇶🇦", Lat = 25.2854, Lon = 51.5310, Timezone = "Asia/Qatar" },
        new() { Id = "ruh-sa", Name = "Riyadh", NameRu = "Эр-Рияд", Country = "Saudi Arabia", CountryRu = "Саудовская Аравия", CountryCode = "SA", Flag = "🇸🇦", Lat = 24.7136, Lon = 46.6753, Timezone = "Asia/Riyadh" },
        new() { Id = "tlv-il", Name = "Tel Aviv", NameRu = "Тель-Авив", Country = "Israel", CountryRu = "Израиль", CountryCode = "IL", Flag = "🇮🇱", Lat = 32.0853, Lon = 34.7818, Timezone = "Asia/Jerusalem" },
        new() { Id = "cai-eg", Name = "Cairo", NameRu = "Каир", Country = "Egypt", CountryRu = "Египет", CountryCode = "EG", Flag = "🇪🇬", Lat = 30.0444, Lon = 31.2357, Timezone = "Africa/Cairo" },
        new() { Id = "cpt-za", Name = "Cape Town", NameRu = "Кейптаун", Country = "South Africa", CountryRu = "ЮАР", CountryCode = "ZA", Flag = "🇿🇦", Lat = -33.9249, Lon = 18.4241, Timezone = "Africa/Johannesburg" },

        // South America & Oceania
        new() { Id = "sao-br", Name = "Sao Paulo", NameRu = "Сан-Паулу", Country = "Brazil", CountryRu = "Бразилия", CountryCode = "BR", Flag = "🇧🇷", Lat = -23.5505, Lon = -46.6333, Timezone = "America/Sao_Paulo" },
        new() { Id = "bue-ar", Name = "Buenos Aires", NameRu = "Буэнос-Айрес", Country = "Argentina", CountryRu = "Аргентина", CountryCode = "AR", Flag = "🇦🇷", Lat = -34.6037, Lon = -58.3816, Timezone = "America/Argentina/Buenos_Aires" },
        new() { Id = "syd-au", Name = "Sydney", NameRu = "Сидней", Country = "Australia", CountryRu = "Австралия", CountryCode = "AU", Flag = "🇦🇺", Lat = -33.8688, Lon = 151.2093, Timezone = "Australia/Sydney" },
        new() { Id = "mel-au", Name = "Melbourne", NameRu = "Мельбурн", Country = "Australia", CountryRu = "Австралия", CountryCode = "AU", Flag = "🇦🇺", Lat = -37.8136, Lon = 144.9631, Timezone = "Australia/Melbourne" },
        new() { Id = "akl-nz", Name = "Auckland", NameRu = "Окленд", Country = "New Zealand", CountryRu = "Новая Зеландия", CountryCode = "NZ", Flag = "🇳🇿", Lat = -36.8485, Lon = 174.7633, Timezone = "Pacific/Auckland" }
    };
}

public class ForecastDayItem
{
    public string DayName { get; set; } = "";
    public string Icon { get; set; } = "⛅";
    public string Temp { get; set; } = "+15°";
}

public class WeatherWidgetViewModel : NativeViewModelBase
{
    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(9) };
    private readonly DispatcherTimer _timer;
    private readonly string? _instanceId;

    private string _city = "Москва";
    private double _lat = 55.7558;
    private double _lon = 37.6173;
    private string _country = "RU";
    private string _timezone = "Europe/Moscow";

    private string _unitTemp = "C";
    private string _unitWind = "ms";
    private string _unitPressure = "mmhg";

    private double _rawTempC = 14.0;
    private double _rawFeelsLikeC = 12.0;
    private double _rawWindKmh = 12.2;
    private double _rawPressureHpa = 1002.5;
    private int _rawWeatherCode = 0;
    private int _rawHumidity = 64;
    private double _rawRainMm = 0.0;
    private double _rawMinC = 8.0;
    private double _rawMaxC = 16.0;

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

    private string _windLabel = "ВЕТЕР";
    private string _humidityLabel = "ВЛАЖНОСТЬ";
    private string _pressureLabel = "ДАВЛЕНИЕ";
    private string _minMaxLabel = "МИН / МАКС";

    public string City { get => _city; set { _city = value; OnPropertyChanged(); } }
    public double Lat => _lat;
    public double Lon => _lon;
    public string Country => _country;
    public string Timezone => _timezone;

    public string UnitTemp { get => _unitTemp; set { _unitTemp = value; OnPropertyChanged(); ApplyUnits(); } }
    public string UnitWind { get => _unitWind; set { _unitWind = value; OnPropertyChanged(); ApplyUnits(); } }
    public string UnitPressure { get => _unitPressure; set { _unitPressure = value; OnPropertyChanged(); ApplyUnits(); } }

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

    public string WindLabel { get => _windLabel; set { _windLabel = value; OnPropertyChanged(); } }
    public string HumidityLabel { get => _humidityLabel; set { _humidityLabel = value; OnPropertyChanged(); } }
    public string PressureLabel { get => _pressureLabel; set { _pressureLabel = value; OnPropertyChanged(); } }
    public string MinMaxLabel { get => _minMaxLabel; set { _minMaxLabel = value; OnPropertyChanged(); } }

    public ObservableCollection<ForecastDayItem> Forecast { get; } = new();

    public WeatherWidgetViewModel() : this(null) { }

    public WeatherWidgetViewModel(string? instanceId)
    {
        _instanceId = instanceId;
        LoadCustomSettings();

        UpdateLocalizedLabels();
        LocalizationService.Instance.LanguageChanged += () =>
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                UpdateLocalizedLabels();
                ApplyUnits();
            });
        };

        _ = RefreshWeatherAsync();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
        _timer.Tick += (s, e) => _ = RefreshWeatherAsync();
        _timer.Start();
    }

    public void SetCity(string cityName, double lat, double lon, string country = "RU", string timezone = "auto")
    {
        City = cityName;
        _lat = lat;
        _lon = lon;
        _country = country;
        _timezone = timezone;
        SaveCustomSettings();
        _ = RefreshWeatherAsync();
    }

    public void SetUnits(string tempUnit, string windUnit, string pressureUnit)
    {
        _unitTemp = tempUnit;
        _unitWind = windUnit;
        _unitPressure = pressureUnit;
        SaveCustomSettings();
        ApplyUnits();
    }

    public void LoadCustomSettings()
    {
        if (!string.IsNullOrEmpty(_instanceId) &&
            WidgetManager.Instance.Settings.Widgets.TryGetValue(_instanceId, out var s))
        {
            if (s.CustomSettings.TryGetValue("weatherCity", out var c) && !string.IsNullOrWhiteSpace(c)) _city = c;
            if (s.CustomSettings.TryGetValue("weatherLat", out var latStr) && double.TryParse(latStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat)) _lat = lat;
            if (s.CustomSettings.TryGetValue("weatherLon", out var lonStr) && double.TryParse(lonStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lon)) _lon = lon;
            if (s.CustomSettings.TryGetValue("weatherCountry", out var co)) _country = co;
            if (s.CustomSettings.TryGetValue("weatherTimezone", out var tz)) _timezone = tz;
            if (s.CustomSettings.TryGetValue("weatherUnitTemp", out var ut)) _unitTemp = ut;
            if (s.CustomSettings.TryGetValue("weatherUnitWind", out var uw)) _unitWind = uw;
            if (s.CustomSettings.TryGetValue("weatherUnitPressure", out var up)) _unitPressure = up;
        }
        else
        {
            bool isEn = LocalizationService.Instance.IsEnglish;
            if (isEn)
            {
                _city = "London";
                _lat = 51.5074;
                _lon = -0.1278;
                _country = "GB";
                _timezone = "Europe/London";
                _unitWind = "kmh";
                _unitPressure = "hpa";
            }
        }
    }

    public void SaveCustomSettings()
    {
        if (string.IsNullOrEmpty(_instanceId)) return;
        if (!WidgetManager.Instance.Settings.Widgets.TryGetValue(_instanceId, out var s)) return;

        s.CustomSettings["weatherCity"] = _city;
        s.CustomSettings["weatherLat"] = _lat.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
        s.CustomSettings["weatherLon"] = _lon.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
        s.CustomSettings["weatherCountry"] = _country;
        s.CustomSettings["weatherTimezone"] = _timezone;
        s.CustomSettings["weatherUnitTemp"] = _unitTemp;
        s.CustomSettings["weatherUnitWind"] = _unitWind;
        s.CustomSettings["weatherUnitPressure"] = _unitPressure;
        WidgetManager.Instance.SaveSettings();
    }

    private void UpdateLocalizedLabels()
    {
        var loc = LocalizationService.Instance;
        WindLabel = loc.T("Weather.Wind", "ВЕТЕР").ToUpperInvariant();
        HumidityLabel = loc.T("Weather.Humidity", "ВЛАЖНОСТЬ").ToUpperInvariant();
        PressureLabel = loc.T("Weather.Pressure", "ДАВЛЕНИЕ").ToUpperInvariant();
        MinMaxLabel = loc.T("Weather.MinMax", "МИН / МАКС").ToUpperInvariant();
    }

    private void ApplyUnits()
    {
        var loc = LocalizationService.Instance;

        // Temperature conversion
        double tVal = _unitTemp.Equals("F", StringComparison.OrdinalIgnoreCase)
            ? (_rawTempC * 9.0 / 5.0) + 32.0
            : _rawTempC;
        string tSign = tVal > 0 ? "+" : "";
        Temperature = $"{tSign}{Math.Round(tVal)}°{(_unitTemp == "F" ? "F" : "")}";

        double flVal = _unitTemp.Equals("F", StringComparison.OrdinalIgnoreCase)
            ? (_rawFeelsLikeC * 9.0 / 5.0) + 32.0
            : _rawFeelsLikeC;
        string flSign = flVal > 0 ? "+" : "";
        FeelsLike = $"{loc.T("Weather.FeelsLike", "Ощущается как")} {flSign}{Math.Round(flVal)}°";

        double minVal = _unitTemp.Equals("F", StringComparison.OrdinalIgnoreCase)
            ? (_rawMinC * 9.0 / 5.0) + 32.0
            : _rawMinC;
        double maxVal = _unitTemp.Equals("F", StringComparison.OrdinalIgnoreCase)
            ? (_rawMaxC * 9.0 / 5.0) + 32.0
            : _rawMaxC;
        string minSign = minVal > 0 ? "+" : "";
        string maxSign = maxVal > 0 ? "+" : "";
        MinMax = $"{minSign}{Math.Round(minVal)}° / {maxSign}{Math.Round(maxVal)}°";

        // Wind conversion
        switch (_unitWind.ToLowerInvariant())
        {
            case "kmh":
                WindSpeed = $"{_rawWindKmh:F1} {loc.T("Weather.UnitKmh", "км/ч")}";
                break;
            case "mph":
                double mph = _rawWindKmh * 0.621371;
                WindSpeed = $"{mph:F1} {loc.T("Weather.UnitMph", "mph")}";
                break;
            default: // ms
                double ms = _rawWindKmh / 3.6;
                WindSpeed = $"{ms:F1} {loc.T("Weather.UnitMs", "м/с")}";
                break;
        }

        // Pressure conversion
        if (_unitPressure.Equals("hpa", StringComparison.OrdinalIgnoreCase))
        {
            Pressure = $"{Math.Round(_rawPressureHpa)} {loc.T("Weather.UnitHpa", "hPa")}";
        }
        else
        {
            int pressMm = (int)Math.Round(_rawPressureHpa * 0.750062);
            Pressure = $"{pressMm} {loc.T("Weather.UnitMmhg", "мм")}";
        }

        var (wDesc, wIcon) = GetWmoInfo(_rawWeatherCode);
        Condition = wDesc;
        WeatherIcon = wIcon;
        Humidity = $"{_rawHumidity}%";
        Rain = $"{_rawRainMm:F1} мм";
    }

    public async Task RefreshWeatherAsync()
    {
        try
        {
            string tzParam = string.IsNullOrWhiteSpace(_timezone) || _timezone == "auto" ? "auto" : Uri.EscapeDataString(_timezone);
            string url = FormattableString.Invariant(
                $"https://api.open-meteo.com/v1/forecast?latitude={_lat:F4}&longitude={_lon:F4}&current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,surface_pressure,wind_speed_10m&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone={tzParam}"
            );

            string json = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("current", out var cur))
            {
                _rawTempC = cur.GetProperty("temperature_2m").GetDouble();
                _rawFeelsLikeC = cur.GetProperty("apparent_temperature").GetDouble();
                _rawWeatherCode = cur.GetProperty("weather_code").GetInt32();
                _rawHumidity = cur.GetProperty("relative_humidity_2m").GetInt32();
                _rawWindKmh = cur.GetProperty("wind_speed_10m").GetDouble();
                _rawPressureHpa = cur.GetProperty("surface_pressure").GetDouble();
                _rawRainMm = cur.GetProperty("precipitation").GetDouble();

                var newForecast = new List<ForecastDayItem>();

                if (root.TryGetProperty("daily", out var daily))
                {
                    var times = daily.GetProperty("time").EnumerateArray().ToList();
                    var codes = daily.GetProperty("weather_code").EnumerateArray().ToList();
                    var maxs = daily.GetProperty("temperature_2m_max").EnumerateArray().ToList();
                    var mins = daily.GetProperty("temperature_2m_min").EnumerateArray().ToList();

                    if (maxs.Count > 0 && mins.Count > 0)
                    {
                        _rawMinC = mins[0].GetDouble();
                        _rawMaxC = maxs[0].GetDouble();
                    }

                    bool isRu = LocalizationService.Instance.IsRussian;
                    var culture = isRu ? new System.Globalization.CultureInfo("ru-RU") : System.Globalization.CultureInfo.InvariantCulture;

                    for (int i = 1; i < Math.Min(5, times.Count); i++)
                    {
                        if (DateTime.TryParse(times[i].GetString(), out var dt))
                        {
                            string dayName = dt.ToString("ddd", culture).ToUpper();
                            int c = codes[i].GetInt32();
                            var (_, icon) = GetWmoInfo(c);
                            double mxC = maxs[i].GetDouble();
                            double mxVal = _unitTemp.Equals("F", StringComparison.OrdinalIgnoreCase)
                                ? (mxC * 9.0 / 5.0) + 32.0
                                : mxC;
                            string mxSign = mxVal > 0 ? "+" : "";

                            newForecast.Add(new ForecastDayItem
                            {
                                DayName = dayName,
                                Icon = icon,
                                Temp = $"{mxSign}{Math.Round(mxVal)}°"
                            });
                        }
                    }
                }

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    ApplyUnits();
                    var loc = LocalizationService.Instance;
                    UpdateTime = $"{loc.T("Weather.UpdatedAt", "Обновлено в")} {DateTime.Now:HH:mm}";

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
        var loc = LocalizationService.Instance;
        return code switch
        {
            0 => (loc.T("Weather.Clear", "Ясно"), "☀️"),
            1 => (loc.T("Weather.MostlyClear", "Преимущественно ясно"), "🌤️"),
            2 => (loc.T("Weather.PartlyCloudy", "Переменная облачность"), "⛅"),
            3 => (loc.T("Weather.Overcast", "Пасмурно"), "☁️"),
            45 or 48 => (loc.T("Weather.Fog", "Туман"), "🌫️"),
            51 or 53 or 55 => (loc.T("Weather.Drizzle", "Морось"), "🌦️"),
            61 or 63 or 65 => (loc.T("Weather.RainDesc", "Дождь"), "🌧️"),
            71 or 73 or 75 => (loc.T("Weather.SnowDesc", "Снегопад"), "❄️"),
            77 => (loc.T("Weather.SnowDesc", "Снег"), "❄️"),
            80 or 81 or 82 => (loc.T("Weather.Showers", "Ливень"), "🌧️"),
            85 or 86 => (loc.T("Weather.SnowDesc", "Снежный ливень"), "🌨️"),
            95 => (loc.T("Weather.Thunderstorm", "Гроза"), "⛈️"),
            96 or 99 => (loc.T("Weather.Thunderstorm", "Гроза с градом"), "⛈️"),
            _ => (loc.T("Weather.PartlyCloudy", "Облачно"), "⛅")
        };
    }
}
