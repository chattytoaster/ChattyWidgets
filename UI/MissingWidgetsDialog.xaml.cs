using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ChattyWidgets.Core.Models;
using ChattyWidgets.Core.Services;

namespace ChattyWidgets.UI;

public partial class MissingWidgetsDialog : Window
{
    public DesktopPreset Preset { get; }
    public ObservableCollection<MissingWidgetItemViewModel> MissingItems { get; } = new();
    public bool UserAccepted { get; private set; } = false;
    public bool ShouldInstallMissing { get; private set; } = true;
    public int InstalledWidgetsCount { get; private set; } = 0;

    public MissingWidgetsDialog(DesktopPreset preset, List<string> missingWidgetIds, List<StoreItem> storeCatalog)
    {
        InitializeComponent();
        Preset = preset;

        bool isRu = LocalizationService.Instance.IsRussian;

        // Custom Title & Icon
        TxtPresetIcon.Text = !string.IsNullOrWhiteSpace(preset.Icon) ? preset.Icon : "🎨";
        TxtDialogTitle.Text = isRu 
            ? $"Импорт пресета: «{preset.Name}»" 
            : $"Import Preset: «{preset.Name}»";

        TxtNoticeHeader.Text = isRu
            ? $"В пресете используются виджеты, которые еще не установлены ({missingWidgetIds.Count})"
            : $"This preset uses widgets that are not yet installed ({missingWidgetIds.Count})";

        TxtNoticeDesc.Text = isRu
            ? "Выберите виджеты, которые хотите автоматически загрузить из каталога и установить:"
            : "Select widgets you want to automatically download from catalog and install:";

        BtnCancel.Content = isRu ? "Отмена" : "Cancel";
        BtnApplyInstalledOnly.Content = isRu ? "Применить без них" : "Apply without them";

        // Build item view models
        foreach (var id in missingWidgetIds)
        {
            var storeItem = storeCatalog.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            var vm = new MissingWidgetItemViewModel(id, storeItem, isRu);
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MissingWidgetItemViewModel.IsSelected))
                {
                    UpdateSelectionSummary();
                }
            };
            MissingItems.Add(vm);
        }

        MissingWidgetsItemsControl.ItemsSource = MissingItems;
        UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        bool isRu = LocalizationService.Instance.IsRussian;
        int total = MissingItems.Count;
        int selected = MissingItems.Count(x => x.IsSelected && x.CanSelect);

        TxtSelectionCount.Text = isRu 
            ? $"Выбрано к установке: {selected} из {total}" 
            : $"Selected for install: {selected} of {total}";

        if (selected > 0)
        {
            BtnInstallAndApply.Content = isRu 
                ? $"✨ Установить ({selected}) и применить" 
                : $"✨ Install ({selected}) & Apply";
            BtnInstallAndApply.IsEnabled = true;
        }
        else
        {
            BtnInstallAndApply.Content = isRu 
                ? "✨ Установить и применить" 
                : "✨ Install & Apply";
            BtnInstallAndApply.IsEnabled = false;
        }
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
        DialogResult = false;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnApplyInstalledOnlyClick(object sender, RoutedEventArgs e)
    {
        UserAccepted = true;
        ShouldInstallMissing = false;
        DialogResult = true;
        Close();
    }

    private async void OnInstallAndApplyClick(object sender, RoutedEventArgs e)
    {
        var toInstall = MissingItems.Where(x => x.IsSelected && x.CanSelect && x.StoreItem != null).ToList();
        if (toInstall.Count == 0)
        {
            OnApplyInstalledOnlyClick(sender, e);
            return;
        }

        // Lock UI controls during download & install
        BtnInstallAndApply.IsEnabled = false;
        BtnApplyInstalledOnly.IsEnabled = false;
        BtnCancel.IsEnabled = false;
        PanelProgress.Visibility = Visibility.Visible;
        ProgressBarInstall.Maximum = toInstall.Count;
        ProgressBarInstall.Value = 0;

        bool isRu = LocalizationService.Instance.IsRussian;
        int installed = 0;

        for (int i = 0; i < toInstall.Count; i++)
        {
            var item = toInstall[i];
            ProgressBarInstall.Value = i;
            TxtProgressStatus.Text = isRu 
                ? $"Загрузка и установка ({i + 1}/{toInstall.Count}): {item.DisplayName}..." 
                : $"Downloading & installing ({i + 1}/{toInstall.Count}): {item.DisplayName}...";

            item.SetInstalling();

            try
            {
                if (item.StoreItem != null)
                {
                    await StoreService.Instance.InstallStoreItemAsync(item.StoreItem);
                    item.SetInstalled();
                    installed++;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MissingWidgetsDialog] Install error for {item.WidgetId}: {ex.Message}");
                item.SetError(isRu ? "Ошибка загрузки" : "Download error");
            }

            ProgressBarInstall.Value = i + 1;
            await Task.Delay(100);
        }

        InstalledWidgetsCount = installed;
        TxtProgressStatus.Text = isRu 
            ? $"Установка завершена! Установлено виджетов: {installed}." 
            : $"Installation complete! Installed {installed} widgets.";

        await Task.Delay(400);

        UserAccepted = true;
        ShouldInstallMissing = true;
        DialogResult = true;
        Close();
    }
}

public class MissingWidgetItemViewModel : INotifyPropertyChanged
{
    public string WidgetId { get; }
    public StoreItem? StoreItem { get; }
    public string DisplayName { get; }
    public string DisplayDescription { get; }
    public string DisplayAuthor { get; }
    public string Category { get; }
    public string Version { get; }
    public string IconEmoji { get; }
    public bool IsAvailableInStore => StoreItem != null;
    public bool IsVerifiedAuthor => DisplayAuthor.StartsWith("ChattyWidgets", StringComparison.OrdinalIgnoreCase) || (StoreItem != null && StoreItem.IsOfficial);
    public Visibility VerifiedBadgeVisibility => IsVerifiedAuthor ? Visibility.Visible : Visibility.Collapsed;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _canSelect = true;
    public bool CanSelect
    {
        get => _canSelect && IsAvailableInStore;
        private set
        {
            if (_canSelect != value)
            {
                _canSelect = value;
                OnPropertyChanged();
            }
        }
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    private Brush _statusBg = Brushes.Transparent;
    public Brush StatusBg
    {
        get => _statusBg;
        private set { _statusBg = value; OnPropertyChanged(); }
    }

    private Brush _statusFg = Brushes.White;
    public Brush StatusFg
    {
        get => _statusFg;
        private set { _statusFg = value; OnPropertyChanged(); }
    }

    private Brush _statusBorder = Brushes.Transparent;
    public Brush StatusBorder
    {
        get => _statusBorder;
        private set { _statusBorder = value; OnPropertyChanged(); }
    }

    public MissingWidgetItemViewModel(string widgetId, StoreItem? storeItem, bool isRu)
    {
        WidgetId = widgetId;
        StoreItem = storeItem;

        if (storeItem != null)
        {
            DisplayName = storeItem.DisplayName;
            DisplayDescription = !string.IsNullOrWhiteSpace(storeItem.DisplayDescription) 
                ? storeItem.DisplayDescription 
                : (isRu ? "Официальный виджет рабочего стола ChattyWidgets" : "Official ChattyWidgets desktop widget");
            DisplayAuthor = !string.IsNullOrWhiteSpace(storeItem.Author) ? storeItem.Author : "ChattyWidgets";
            Category = !string.IsNullOrWhiteSpace(storeItem.Category) ? storeItem.Category : "General";
            Version = storeItem.Version;
            IsSelected = true;
            CanSelect = true;

            StatusText = isRu ? "⬇ В каталоге" : "⬇ Available";
            StatusBg = new SolidColorBrush(Color.FromRgb(30, 42, 74));
            StatusFg = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            StatusBorder = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        }
        else
        {
            DisplayName = widgetId;
            DisplayDescription = isRu 
                ? "Пользовательский или сторонний виджет (не найден в официальном каталоге)" 
                : "Custom or third-party widget (not found in official store catalog)";
            DisplayAuthor = isRu ? "Неизвестен" : "Unknown";
            Category = "Custom";
            Version = "1.0.0";
            IsSelected = false;
            CanSelect = false;

            StatusText = isRu ? "⚠️ Нет в каталоге" : "⚠️ Not in catalog";
            StatusBg = new SolidColorBrush(Color.FromRgb(53, 10, 10));
            StatusFg = new SolidColorBrush(Color.FromRgb(248, 113, 113));
            StatusBorder = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }

        IconEmoji = GetEmojiForWidget(widgetId);
    }

    public void SetInstalling()
    {
        bool isRu = LocalizationService.Instance.IsRussian;
        CanSelect = false;
        StatusText = isRu ? "⏳ Загрузка..." : "⏳ Downloading...";
        StatusBg = new SolidColorBrush(Color.FromRgb(49, 46, 129));
        StatusFg = new SolidColorBrush(Color.FromRgb(165, 180, 252));
        StatusBorder = new SolidColorBrush(Color.FromRgb(99, 102, 241));
    }

    public void SetInstalled()
    {
        bool isRu = LocalizationService.Instance.IsRussian;
        CanSelect = false;
        IsSelected = false;
        StatusText = isRu ? "✓ Установлен" : "✓ Installed";
        StatusBg = new SolidColorBrush(Color.FromRgb(6, 78, 59));
        StatusFg = new SolidColorBrush(Color.FromRgb(52, 211, 153));
        StatusBorder = new SolidColorBrush(Color.FromRgb(16, 185, 129));
    }

    public void SetError(string errorMsg)
    {
        CanSelect = true;
        StatusText = $"❌ {errorMsg}";
        StatusBg = new SolidColorBrush(Color.FromRgb(53, 10, 10));
        StatusFg = new SolidColorBrush(Color.FromRgb(248, 113, 113));
        StatusBorder = new SolidColorBrush(Color.FromRgb(239, 68, 68));
    }

    private static string GetEmojiForWidget(string id)
    {
        string lower = id.ToLowerInvariant();
        if (lower.Contains("clock")) return "🕒";
        if (lower.Contains("crypto") || lower.Contains("gram") || lower.Contains("ton")) return "💎";
        if (lower.Contains("weather")) return "⛅";
        if (lower.Contains("gauge") || lower.Contains("system")) return "⚡";
        if (lower.Contains("gpu")) return "🎮";
        if (lower.Contains("pomodoro") || lower.Contains("timer")) return "⏳";
        if (lower.Contains("media") || lower.Contains("photo") || lower.Contains("frame")) return "🖼️";
        if (lower.Contains("music") || lower.Contains("player")) return "🎵";
        if (lower.Contains("rss") || lower.Contains("news")) return "📰";
        if (lower.Contains("notes") || lower.Contains("task") || lower.Contains("sticky")) return "📝";
        if (lower.Contains("dock") || lower.Contains("quick")) return "🚀";
        if (lower.Contains("calendar")) return "📅";
        return "📦";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
