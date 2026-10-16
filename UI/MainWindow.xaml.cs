using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using ChattyWidgets.Core.Models;
using ChattyWidgets.Core.Services;
using Microsoft.Win32;
using Button = System.Windows.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;

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
        CheckHotkeys.IsChecked = WidgetManager.Instance.Settings.EnableHotkeys;
        CheckAcrylic.IsChecked = WidgetManager.Instance.Settings.AcrylicBlur;
        CheckHotReload.IsChecked = WidgetManager.Instance.Settings.EnableHotReload;
        CheckDevMode.IsChecked = WidgetManager.Instance.Settings.EnableDevMode;
        int currentGrid = WidgetManager.Instance.Settings.GridSize > 0 ? WidgetManager.Instance.Settings.GridSize : 10;
        SliderGridSize.Value = currentGrid;
        TxtGridSizeVal.Text = $"{currentGrid} px";
        SliderDefaultOpacity.Value = 100;
        TxtOpacityVal.Text = "100%";

        HotkeyService.Instance.Initialize(this);
        RenderPresetChips();
        PresetService.Instance.PresetsChanged += () => Dispatcher.Invoke(RenderPresetChips);

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
        var compareCatalog = StoreService.Instance.LastLoadedCatalog ?? StoreService.Instance.GetStarterWidgetsCatalog();
        var viewModels = new List<InstalledWidgetViewModel>();

        int totalActiveCopies = 0;
        int updatesCount = 0;
        foreach (var (manifest, dir) in installed)
        {
            int copies = WidgetManager.Instance.GetActiveInstancesCount(manifest.Id);
            bool isActive = copies > 0;
            if (isActive) totalActiveCopies += copies;

            var trust = SecurityService.Instance.EvaluateWidget(manifest, dir);

            bool hasUpdate = false;
            string newVer = "";
            var catItem = compareCatalog.FirstOrDefault(x => x.Id.Equals(manifest.Id, StringComparison.OrdinalIgnoreCase));
            if (catItem != null && StoreService.IsNewerVersion(catItem.Version, manifest.Version))
            {
                hasUpdate = true;
                newVer = catItem.Version;
                updatesCount++;
            }

            viewModels.Add(new InstalledWidgetViewModel
            {
                Manifest = manifest,
                DirectoryPath = dir,
                IsActive = isActive,
                ActiveCopiesCount = copies,
                TypeBadge = manifest.Type == "native" ? "⚡" : "🌐",
                ToggleButtonText = isActive ? "Остановить" : "Запустить",
                ToggleButtonBg = isActive
                    ? new SolidColorBrush(Color.FromRgb(239, 68, 68)) // Red
                    : new SolidColorBrush(Color.FromRgb(16, 185, 129)), // Green
                Trust = trust,
                HasUpdate = hasUpdate,
                NewVersion = newVer
            });
        }

        InstalledWidgetsList.ItemsSource = viewModels;
        TxtActiveCount.Text = $"{totalActiveCopies} активно из {installed.Count}";
        if (updatesCount > 0)
        {
            TxtStatus.Text = $"Загружено {installed.Count} виджетов. Доступно обновлений: {updatesCount}!";
        }
        else
        {
            TxtStatus.Text = $"Загружено {installed.Count} виджетов. Активно экземпляров: {totalActiveCopies}.";
        }
    }

    private async void OnRefreshInstalledClick(object sender, RoutedEventArgs e)
    {
        TxtStatus.Text = "Проверка обновлений виджетов...";
        var catalog = await StoreService.Instance.GetStoreCatalogAsync(WidgetManager.Instance.Settings.RemoteCatalogUrl);
        RefreshInstalledWidgets();
        int updates = catalog.Count(x => x.HasUpdate);
        if (updates > 0)
        {
            MessageBox.Show($"Найдено обновлений: {updates}!\nНажмите кнопку «Обновить» на карточке виджета для установки.", "Обновления виджетов", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("Все установленные виджеты обновлены до последней версии.", "Обновления виджетов", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnDuplicateFromHubClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string id)
        {
            try
            {
                TxtStatus.Text = $"Создание экземпляра виджета '{id}'...";
                var newId = WidgetManager.Instance.DuplicateWidget(id);
                RefreshInstalledWidgets();
                if (newId != null)
                {
                    TxtStatus.Text = $"Экземпляр виджета успешно добавлен на рабочий стол!";
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Ошибка добавления виджета: {ex.Message}";
                MessageBox.Show($"Не удалось добавить виджет: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
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
                $"Вы уверены, что хотите полностью удалить виджет '{id}' и все его копии?",
                "Удаление виджета",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                // Delete all instances
                var instances = WidgetManager.Instance.Settings.Widgets.Keys
                    .Where(k => k == id || k.StartsWith(id + "#"))
                    .ToList();
                foreach (var instId in instances)
                {
                    WidgetManager.Instance.DeleteWidgetInstance(instId);
                }

                WidgetManager.Instance.StopWidget(id);
                ChwPackageService.UninstallWidget(id);
                RefreshInstalledWidgets();
                RefreshStoreCatalog();
            }
        }
    }

    private async void OnUpdateWidgetClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string id)
        {
            TxtStatus.Text = $"Обновление виджета '{id}'...";
            try
            {
                var catalog = await StoreService.Instance.GetStoreCatalogAsync(WidgetManager.Instance.Settings.RemoteCatalogUrl);
                var item = catalog.FirstOrDefault(x => x.Id == id);
                if (item != null)
                {
                    await StoreService.Instance.InstallStoreItemAsync(item);
                    RefreshInstalledWidgets();
                    TxtStatus.Text = $"Виджет '{item.Name}' успешно обновлен!";
                    MessageBox.Show($"Виджет '{item.Name}' успешно обновлен до версии v{item.Version}!", "Обновление", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Ошибка обновления: {ex.Message}";
                MessageBox.Show($"Ошибка обновления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void RenderPresetChips()
    {
        PresetChipsPanel.Children.Clear();
        var presets = WidgetManager.Instance.Settings.Presets;
        string activeId = WidgetManager.Instance.Settings.ActivePresetId;

        if (presets == null || presets.Count == 0)
        {
            var hint = new TextBlock
            {
                Text = "Пресеты не созданы. Расположите виджеты и нажмите «+ Сохранить текущий вид» или «📥 Импорт .chwp»",
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };
            PresetChipsPanel.Children.Add(hint);
            return;
        }

        foreach (var p in presets)
        {
            bool isCurrent = (p.Id == activeId);
            var btn = new Button
            {
                Content = $"{p.Icon} {p.Name}",
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(12, 6, 12, 6),
                FontSize = 12,
                FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal,
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = p,
                ToolTip = $"ЛКМ: применить пресет\nПКМ: экспортировать в .chwp или удалить\nВиджетов в пресете: {p.Widgets.Count}",
                Style = isCurrent ? (Style)FindResource("PrimaryBtn") : (Style)FindResource("SecondaryBtn")
            };
            btn.Click += OnPresetChipClick;

            // Context Menu: Export to .chwp / Delete
            var contextMenu = new ContextMenu();
            var exportItem = new MenuItem { Header = "💾 Экспортировать в файл .chwp..." };
            exportItem.Click += (s, e) => ExportPreset(p);
            contextMenu.Items.Add(exportItem);

            var deleteItem = new MenuItem { Header = "🗑 Удалить пресет" };
            deleteItem.Click += (s, e) => DeletePreset(p);
            contextMenu.Items.Add(deleteItem);

            btn.ContextMenu = contextMenu;
            PresetChipsPanel.Children.Add(btn);
        }
    }

    private void OnPresetChipClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DesktopPreset preset)
        {
            PresetService.Instance.ApplyPreset(preset.Id);
            RefreshInstalledWidgets();
            RenderPresetChips();
        }
    }

    private void OnSaveCurrentPresetClick(object sender, RoutedEventArgs e)
    {
        var activeWindows = WidgetManager.Instance.GetActiveWindows().ToList();
        if (activeWindows.Count == 0)
        {
            MessageBox.Show("Запустите хотя бы один виджет перед сохранением пресета.", "Сохранение пресета", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string defaultName = $"Стол {DateTime.Now:dd.MM HH:mm}";
        string? presetName = InputDialog.Show(this, "Новый пресет стола", "Введите название пресета:", defaultName);
        if (string.IsNullOrWhiteSpace(presetName))
            return;

        var preset = PresetService.Instance.SaveCurrentAsPreset(presetName, "⭐", "Пользовательский набор виджетов");
        RenderPresetChips();

        var res = MessageBox.Show(
            $"Пресет «{preset.Name}» успешно сохранён!\n\nЭкспортировать его в файл .chwp прямо сейчас, чтобы поделиться с другими?",
            "Пресет сохранён",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (res == MessageBoxResult.Yes)
        {
            ExportPreset(preset);
        }
    }

    private void OnImportPresetFileClick(object sender, RoutedEventArgs e)
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Импорт пресета рабочего стола .chwp",
            Filter = "Пакет пресета ChattyWidgets (*.chwp)|*.chwp|Все файлы (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            ImportPresetFile(ofd.FileName);
        }
    }

    public void ImportPresetFile(string filePath)
    {
        try
        {
            var preset = PresetService.Instance.ImportPresetFromFile(filePath);
            RenderPresetChips();

            var res = MessageBox.Show(
                $"Пресет «{preset.Name}» успешно импортирован!\n(Виджетов: {preset.Widgets.Count})\n\nПрименить этот пресет на рабочий стол прямо сейчас?",
                "Импорт пресета .chwp",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                PresetService.Instance.ApplyPreset(preset.Id);
                RefreshInstalledWidgets();
                RenderPresetChips();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка импорта пресета: {ex.Message}", "Ошибка импорта", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportPreset(DesktopPreset preset)
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Экспорт пресета рабочего стола .chwp",
            Filter = "Пакет пресета ChattyWidgets (*.chwp)|*.chwp",
            FileName = $"{SanitizeFileName(preset.Name)}.chwp"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                PresetService.Instance.ExportPresetToFile(preset, sfd.FileName);
                TxtStatus.Text = $"Пресет '{preset.Name}' успешно экспортирован!";
                MessageBox.Show($"Файл пресета сохранен:\n{sfd.FileName}\n\nВы можете отправить его друзьям или поделиться в сообществе!", "Экспорт .chwp", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта пресета: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void DeletePreset(DesktopPreset preset)
    {
        var res = MessageBox.Show($"Удалить пресет «{preset.Name}»?", "Удаление пресета", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == MessageBoxResult.Yes)
        {
            PresetService.Instance.DeletePreset(preset.Id);
            RenderPresetChips();
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder();
        foreach (char c in name)
        {
            sb.Append(invalid.Contains(c) ? '_' : c);
        }
        string clean = sb.ToString().Trim();
        return string.IsNullOrEmpty(clean) ? "Preset" : clean;
    }

    // --- TAB: STORE ---

    public async void RefreshStoreCatalog()
    {
        await RefreshStoreCatalogAsync();
    }

    public async Task RefreshStoreCatalogAsync()
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

            bool canAction = !item.IsInstalled || item.HasUpdate;
            string actionText = item.HasUpdate
                ? $"Обновить до v{item.Version} ⬆"
                : (item.IsInstalled ? "Установлен ✓" : "Установить ⬇");

            var actionBg = item.HasUpdate
                ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) // Emerald green for update
                : (item.IsInstalled
                    ? new SolidColorBrush(Color.FromRgb(51, 65, 85)) // Slate
                    : new SolidColorBrush(Color.FromRgb(99, 102, 241))); // Indigo

            return new StoreItemViewModel
            {
                Item = item,
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Author = item.Author,
                Category = item.Category,
                Size = item.Size,
                CanInstall = canAction,
                ActionButtonText = actionText,
                ActionButtonBg = actionBg,
                BadgeText = badgeText,
                BadgeBg = badgeBg,
                BadgeFg = badgeFg
            };
        }).ToList();

        StoreItemsList.ItemsSource = viewModels;
        int updatesCount = catalog.Count(x => x.HasUpdate);
        if (updatesCount > 0)
        {
            TxtStatus.Text = $"Магазин: доступно {catalog.Count} виджетов. Доступно обновлений: {updatesCount}!";
        }
        else
        {
            TxtStatus.Text = $"Магазин: доступно {catalog.Count} виджетов.";
        }
    }

    private async void OnRefreshStoreClick(object sender, RoutedEventArgs e)
    {
        TxtStatus.Text = "Проверка обновлений каталога...";
        await RefreshStoreCatalogAsync();
        RefreshInstalledWidgets();

        var catalog = StoreService.Instance.LastLoadedCatalog;
        int updates = catalog?.Count(x => x.HasUpdate) ?? 0;
        if (updates > 0)
        {
            MessageBox.Show($"Проверка завершена! Найдено обновлений: {updates}.\nКнопки «Обновить» активны в Магазине и на вкладке «Мои виджеты».", "Проверка обновлений", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("Все виджеты обновлены до актуальной версии из каталога.", "Проверка обновлений", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void OnInstallStoreItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is StoreItemViewModel vm)
        {
            try
            {
                bool isUpdating = vm.Item.HasUpdate;
                TxtStatus.Text = isUpdating ? $"Обновление {vm.Name}..." : $"Установка {vm.Name}...";
                var manifest = await StoreService.Instance.InstallStoreItemAsync(vm.Item);

                // Auto start after fresh install if not yet running
                if (!isUpdating)
                {
                    WidgetManager.Instance.StartWidget(manifest.Id);
                }

                RefreshInstalledWidgets();
                await RefreshStoreCatalogAsync();

                string title = isUpdating ? "Виджет обновлен" : "Виджет установлен";
                string msg = isUpdating
                    ? $"'{manifest.Name}' успешно обновлен до версии v{manifest.Version}!"
                    : $"'{manifest.Name}' успешно добавлен на рабочий стол.";

                _trayIcon?.ShowNotification(title, msg);
                MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Магазин", MessageBoxButton.OK, MessageBoxImage.Error);
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

    private void OnHotkeysChanged(object sender, RoutedEventArgs e)
    {
        WidgetManager.Instance.Settings.EnableHotkeys = CheckHotkeys.IsChecked == true;
        WidgetManager.Instance.SaveSettings();
        if (CheckHotkeys.IsChecked == true)
            HotkeyService.Instance.RegisterHotkeys();
        else
            HotkeyService.Instance.UnregisterHotkeys();
    }

    private void OnAcrylicChanged(object sender, RoutedEventArgs e)
    {
        WidgetManager.Instance.Settings.AcrylicBlur = CheckAcrylic.IsChecked == true;
        WidgetManager.Instance.SaveSettings();
    }

    private void OnHotReloadChanged(object sender, RoutedEventArgs e)
    {
        WidgetManager.Instance.Settings.EnableHotReload = CheckHotReload.IsChecked == true;
        WidgetManager.Instance.SaveSettings();
        if (CheckHotReload.IsChecked == true)
            HotReloadService.Instance.Start();
        else
            HotReloadService.Instance.Stop();
    }

    private void OnDevModeChanged(object sender, RoutedEventArgs e)
    {
        WidgetManager.Instance.Settings.EnableDevMode = CheckDevMode.IsChecked == true;
        WidgetManager.Instance.SaveSettings();
    }

    private void OnGridSizeSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtGridSizeVal != null)
        {
            TxtGridSizeVal.Text = $"{Math.Round(e.NewValue)} px";
        }
        if (WidgetManager.Instance?.Settings != null)
        {
            WidgetManager.Instance.Settings.GridSize = (int)Math.Round(e.NewValue);
            WidgetManager.Instance.SaveSettings();
        }
    }

    private void OnDefaultOpacitySliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtOpacityVal != null)
        {
            TxtOpacityVal.Text = $"{Math.Round(e.NewValue)}%";
        }
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
    public int ActiveCopiesCount { get; set; }
    public string ActiveCopiesText => ActiveCopiesCount > 1 ? $"({ActiveCopiesCount} шт.)" : "";
    public Visibility ActiveCopiesVisibility => ActiveCopiesCount > 1 ? Visibility.Visible : Visibility.Collapsed;
    public string TypeBadge { get; set; } = "🌐";
    public string ToggleButtonText { get; set; } = "Запустить";
    public Brush ToggleButtonBg { get; set; } = Brushes.SlateGray;
    public WidgetTrustInfo Trust { get; set; } = new();

    public bool HasUpdate { get; set; }
    public string NewVersion { get; set; } = string.Empty;
    public string UpdateBadgeText => $"▲ Доступно обновление до v{NewVersion}";
    public Visibility UpdateBadgeVisibility => HasUpdate ? Visibility.Visible : Visibility.Collapsed;

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

    public bool HasUpdate => Item.HasUpdate;
    public Visibility UpdateBadgeVisibility => HasUpdate ? Visibility.Visible : Visibility.Collapsed;
    public string UpdateBadgeText => $"▲ Доступно обновление v{Item.Version}";
    public string DisplayVersion => Item.IsInstalled ? $"v{Item.InstalledVersion}" : $"v{Item.Version}";
}
