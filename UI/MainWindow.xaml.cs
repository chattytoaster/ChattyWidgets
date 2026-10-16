using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using ChattyWidgets.Core.Models;
using ChattyWidgets.Core.Services;
using Microsoft.Win32;

namespace ChattyWidgets.UI;

public partial class MainWindow : Window
{
    private TrayIconManager? _trayIcon;
    private bool _isExplicitExit = false;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += OnWindowLoaded;
        Closing += OnWindowClosing;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        // Initialize Tray icon
        _trayIcon = new TrayIconManager(
            showMainWindowAction: () =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            },
            openTabAction: (tabKey) =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
                SwitchToTab(tabKey);
            }
        );

        // Preload settings state into UI
        CheckAutostart.IsChecked = WidgetManager.Instance.Settings.StartWithWindows;
        CheckSnapGrid.IsChecked = WidgetManager.Instance.Settings.SnapToGrid;

        WidgetManager.Instance.WidgetsChanged += () => Dispatcher.Invoke(RefreshInstalledWidgets);

        RefreshInstalledWidgets();
        RefreshStoreCatalog();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (!_isExplicitExit)
        {
            e.Cancel = true;
            Hide();
            _trayIcon?.ShowNotification("ChattyWidgets свернут в трей", "Виджеты продолжают работать на рабочем столе.");
        }
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        UpdateMaximizeButtonIcon();
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void OnMinimizeWindowClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreWindowClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateMaximizeButtonIcon();
    }

    private void OnCloseWindowClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UpdateMaximizeButtonIcon()
    {
        if (BtnMaximizeRestore != null)
        {
            BtnMaximizeRestore.Content = WindowState == WindowState.Maximized ? "❐" : "▢";
            BtnMaximizeRestore.ToolTip = WindowState == WindowState.Maximized ? "Восстановить" : "Развернуть";
        }
        if (MainRootBorder != null)
        {
            MainRootBorder.BorderThickness = WindowState == WindowState.Maximized ? new Thickness(0) : new Thickness(1);
        }
    }

    public void SwitchToTab(string tabKey)
    {
        switch (tabKey.ToLowerInvariant())
        {
            case "store":
                TabStore.IsChecked = true;
                break;
            case "workshop":
                TabWorkshop.IsChecked = true;
                break;
            case "settings":
                TabSettings.IsChecked = true;
                break;
            default:
                TabMyWidgets.IsChecked = true;
                break;
        }
    }

    private void OnTabChanged(object sender, RoutedEventArgs e)
    {
        if (ViewMyWidgets == null || ViewStore == null || ViewWorkshop == null || ViewSettings == null)
            return;

        ViewMyWidgets.Visibility = TabMyWidgets.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewStore.Visibility = TabStore.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewWorkshop.Visibility = TabWorkshop.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewSettings.Visibility = TabSettings.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

        if (TabStore.IsChecked == true)
        {
            RefreshStoreCatalog();
        }
    }

    // --- TAB: MY WIDGETS ---

    public void RefreshInstalledWidgets()
    {
        var installed = ChwPackageService.GetInstalledWidgets();
        var viewModels = new List<InstalledWidgetViewModel>();

        int activeCount = 0;
        foreach (var (manifest, dir) in installed)
        {
            bool isActive = WidgetManager.Instance.IsWidgetActive(manifest.Id);
            if (isActive) activeCount++;

            var trust = SecurityService.Instance.EvaluateWidget(manifest, dir);

            viewModels.Add(new InstalledWidgetViewModel
            {
                Manifest = manifest,
                DirectoryPath = dir,
                IsActive = isActive,
                TypeBadge = manifest.Type == "native" ? "⚡" : "🌐",
                ToggleButtonText = isActive ? "Остановить" : "Запустить",
                ToggleButtonBg = isActive
                    ? new SolidColorBrush(Color.FromRgb(239, 68, 68)) // Red
                    : new SolidColorBrush(Color.FromRgb(16, 185, 129)), // Green
                Trust = trust
            });
        }

        InstalledWidgetsList.ItemsSource = viewModels;
        TxtActiveCount.Text = $"{activeCount} активно из {installed.Count}";
        TxtStatus.Text = $"Загружено {installed.Count} виджетов. Активно: {activeCount}.";
    }

    private void OnRefreshInstalledClick(object sender, RoutedEventArgs e)
    {
        RefreshInstalledWidgets();
    }

    private void OnToggleWidgetClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string id)
        {
            WidgetManager.Instance.ToggleWidget(id);
            RefreshInstalledWidgets();
        }
    }

    private void OnOpenWidgetFolderClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string dir && Directory.Exists(dir))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
        }
    }

    private void OnDeleteWidgetClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string id)
        {
            var res = MessageBox.Show(
                $"Вы уверены, что хотите удалить виджет '{id}'?",
                "Удаление виджета",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                WidgetManager.Instance.StopWidget(id);
                ChwPackageService.UninstallWidget(id);
                RefreshInstalledWidgets();
                RefreshStoreCatalog();
            }
        }
    }

    // --- TAB: STORE ---

    public async void RefreshStoreCatalog()
    {
        TxtStatus.Text = "Загрузка каталога магазина...";
        var catalog = await StoreService.Instance.GetStoreCatalogAsync(WidgetManager.Instance.Settings.RemoteCatalogUrl);

        var viewModels = catalog.Select(item =>
        {
            bool isOfficial = item.IsOfficial || item.Id.StartsWith(SecurityService.OfficialPrefix, StringComparison.OrdinalIgnoreCase);
            string badgeText = isOfficial ? "✦ Официальный" : $"{item.Author} (GitHub)";
            var badgeBg = isOfficial
                ? new SolidColorBrush(Color.FromRgb(30, 42, 74))
                : new SolidColorBrush(Color.FromRgb(6, 78, 59));
            var badgeFg = isOfficial
                ? new SolidColorBrush(Color.FromRgb(96, 165, 250))
                : new SolidColorBrush(Color.FromRgb(52, 211, 153));

            return new StoreItemViewModel
            {
                Item = item,
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Author = item.Author,
                Category = item.Category,
                Size = item.Size,
                CanInstall = !item.IsInstalled,
                ActionButtonText = item.IsInstalled ? "Установлен ✓" : "Установить ⬇",
                ActionButtonBg = item.IsInstalled
                    ? new SolidColorBrush(Color.FromRgb(51, 65, 85)) // Slate
                    : new SolidColorBrush(Color.FromRgb(99, 102, 241)), // Indigo
                BadgeText = badgeText,
                BadgeBg = badgeBg,
                BadgeFg = badgeFg
            };
        }).ToList();

        StoreItemsList.ItemsSource = viewModels;
        TxtStatus.Text = $"Магазин: доступно {catalog.Count} виджетов.";
    }

    private void OnRefreshStoreClick(object sender, RoutedEventArgs e)
    {
        RefreshStoreCatalog();
    }

    private async void OnInstallStoreItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is StoreItemViewModel vm)
        {
            try
            {
                TxtStatus.Text = $"Установка {vm.Name}...";
                var manifest = await StoreService.Instance.InstallStoreItemAsync(vm.Item);

                // Auto start after install
                WidgetManager.Instance.StartWidget(manifest.Id);

                RefreshInstalledWidgets();
                RefreshStoreCatalog();
                _trayIcon?.ShowNotification("Виджет установлен", $"'{manifest.Name}' успешно добавлен на рабочий стол.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка установки: {ex.Message}", "Магазин", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "Ошибка установки виджета.";
            }
        }
    }

    // --- IMPORT / DRAG & DROP ---

    private void OnImportChwClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Выберите файл пакета виджета .chw",
            Filter = "Пакеты виджетов (*.chw)|*.chw|Все файлы (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            InstallChwFile(dlg.FileName);
        }
    }

    private void OnWindowDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (var file in files)
            {
                if (Path.GetExtension(file).Equals(".chw", StringComparison.OrdinalIgnoreCase))
                {
                    InstallChwFile(file);
                }
            }
        }
    }

    public void InstallChwFile(string chwPath)
    {
        try
        {
            TxtStatus.Text = $"Проверка пакета {Path.GetFileName(chwPath)}...";

            // Inspect package for security and spoofing attempts
            var (isSafe, warningMsg, meta) = SecurityService.Instance.InspectPackageFile(chwPath);
            if (!isSafe)
            {
                var confirm = MessageBox.Show(
                    $"{warningMsg}\n\nВы всё равно хотите продолжить установку на свой страх и риск?",
                    "Предупреждение безопасности ChattyWidgets",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes)
                {
                    TxtStatus.Text = "Установка стороннего пакета отменена пользователем.";
                    return;
                }
            }

            TxtStatus.Text = $"Установка пакета {Path.GetFileName(chwPath)}...";
            var manifest = ChwPackageService.InstallPackage(chwPath);
            WidgetManager.Instance.StartWidget(manifest.Id);

            RefreshInstalledWidgets();
            RefreshStoreCatalog();

            _trayIcon?.ShowNotification("Пакет .chw установлен!", $"Виджет '{manifest.Name}' активирован на рабочем столе.");
            TabMyWidgets.IsChecked = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось установить .chw: {ex.Message}", "Ошибка пакета", MessageBoxButton.OK, MessageBoxImage.Error);
            TxtStatus.Text = "Ошибка установки пакета .chw.";
        }
    }

    // --- TAB: WORKSHOP ---

    private void OnCreateWidgetTemplateClick(object sender, RoutedEventArgs e)
    {
        string id = InputNewWidgetId.Text.Trim();
        string name = InputNewWidgetName.Text.Trim();
        string author = InputNewWidgetAuthor.Text.Trim();
        string desc = InputNewWidgetDesc.Text.Trim();
        string type = ComboNewWidgetType.SelectedIndex == 1 ? "native" : "web";

        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Пожалуйста, заполните ID и название виджета.", "Мастерская", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string defaultTargetDir = Path.Combine(ChwPackageService.AppDataDir, "dev_projects", id);

        try
        {
            WorkshopService.Instance.CreateWidgetProject(defaultTargetDir, id, name, author, desc, type);
            InputPackFolderPath.Text = defaultTargetDir;

            var res = MessageBox.Show(
                $"Каркас проекта создан в:\n{defaultTargetDir}\n\nОткрыть папку в Проводнике для редактирования файлов?",
                "Проект создан",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (res == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo("explorer.exe", defaultTargetDir) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка создания шаблона: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnBrowseFolderToPackClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Выберите папку виджета, содержащую manifest.json",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            InputPackFolderPath.Text = dialog.SelectedPath;
        }
    }

    private void OnExportChwPackageClick(object sender, RoutedEventArgs e)
    {
        string folder = InputPackFolderPath.Text.Trim();
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            MessageBox.Show("Укажите существующую папку виджета.", "Сборщик", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = "Сохранить пакет .chw",
            Filter = "Пакет виджета ChattyWidgets (*.chw)|*.chw",
            FileName = Path.GetFileName(folder) + ".chw"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                ChwPackageService.CreatePackage(folder, dlg.FileName);
                MessageBox.Show($"Пакет успешно создан!\n\n{dlg.FileName}\n\nТеперь вы можете поделиться им или установить в 1 клик.", "Сборка завершена", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сборки пакета: {ex.Message}", "Ошибка сборки", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // --- TAB: SETTINGS ---

    private void OnAutostartChanged(object sender, RoutedEventArgs e)
    {
        WidgetManager.Instance.SetAutostart(CheckAutostart.IsChecked == true);
    }

    private void OnSnapGridChanged(object sender, RoutedEventArgs e)
    {
        WidgetManager.Instance.Settings.SnapToGrid = CheckSnapGrid.IsChecked == true;
        WidgetManager.Instance.SaveSettings();
    }

    private void OnOpenAppDataClick(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(ChwPackageService.AppDataDir))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", ChwPackageService.AppDataDir) { UseShellExecute = true });
        }
    }
}

// ViewModels for WPF data bindings
public class InstalledWidgetViewModel
{
    public WidgetManifest Manifest { get; set; } = new();
    public string DirectoryPath { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string TypeBadge { get; set; } = "🌐";
    public string ToggleButtonText { get; set; } = "Запустить";
    public Brush ToggleButtonBg { get; set; } = Brushes.SlateGray;
    public WidgetTrustInfo Trust { get; set; } = new();

    public Brush BadgeBg => (Brush)new BrushConverter().ConvertFromString(Trust.BadgeBg)!;
    public Brush BadgeFg => (Brush)new BrushConverter().ConvertFromString(Trust.BadgeFg)!;
    public string DisplayAuthor => Trust.DisplayAuthor;
    public string BadgeText => Trust.BadgeText;
    public bool HasWarning => !string.IsNullOrEmpty(Trust.WarningMessage);
    public string WarningMessage => Trust.WarningMessage;
    public Visibility WarningVisibility => HasWarning ? Visibility.Visible : Visibility.Collapsed;
}

public class StoreItemViewModel
{
    public StoreItem Item { get; set; } = new();
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public bool CanInstall { get; set; } = true;
    public string ActionButtonText { get; set; } = "Установить ⬇";
    public Brush ActionButtonBg { get; set; } = Brushes.Indigo;
    public string BadgeText { get; set; } = string.Empty;
    public Brush BadgeBg { get; set; } = Brushes.DarkSlateBlue;
    public Brush BadgeFg { get; set; } = Brushes.LightSkyBlue;
}
