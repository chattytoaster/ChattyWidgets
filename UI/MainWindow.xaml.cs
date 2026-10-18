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
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;
using RadioButton = System.Windows.Controls.RadioButton;
using CheckBox = System.Windows.Controls.CheckBox;
using Border = System.Windows.Controls.Border;
using ColorConverter = System.Windows.Media.ColorConverter;
using ChattyWidgets.UI.Controls;

namespace ChattyWidgets.UI;

public partial class MainWindow : Window
{
    private TrayIconManager? _trayIcon;
    private bool _isExplicitExit = false;
    private bool _isSuppressingSettingsEvents = false;
    private System.Drawing.Icon? _hIconBigHolder;
    private System.Drawing.Icon? _hIconSmallHolder;

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetClassLongPtr")]
    private static extern IntPtr SetClassLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetClassLong")]
    private static extern IntPtr SetClassLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static void SetClassIcon(IntPtr hwnd, IntPtr hIconBig, IntPtr hIconSmall)
    {
        try
        {
            if (IntPtr.Size == 8)
            {
                SetClassLongPtr64(hwnd, -14 /*GCLP_HICON*/, hIconBig);
                SetClassLongPtr64(hwnd, -34 /*GCLP_HICONSM*/, hIconSmall);
            }
            else
            {
                SetClassLong32(hwnd, -14 /*GCLP_HICON*/, hIconBig);
                SetClassLong32(hwnd, -34 /*GCLP_HICONSM*/, hIconSmall);
            }
        }
        catch { }
    }

    public MainWindow()
    {
        _isSuppressingSettingsEvents = true;
        InitializeComponent();

        try
        {
            string icoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (System.IO.File.Exists(icoPath))
            {
                var decoder = new System.Windows.Media.Imaging.IconBitmapDecoder(
                    new Uri(icoPath, UriKind.Absolute),
                    System.Windows.Media.Imaging.BitmapCreateOptions.None,
                    System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                var bestFrame = decoder.Frames.OrderByDescending(f => f.PixelWidth).FirstOrDefault();
                if (bestFrame != null) Icon = bestFrame;
            }
            else
            {
                string pngPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.png");
                if (System.IO.File.Exists(pngPath))
                {
                    Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(pngPath, UriKind.Absolute));
                }
            }
        }
        catch { }

        SourceInitialized += (s, e) =>
        {
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                string icoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (System.IO.File.Exists(icoPath))
                {
                    _hIconBigHolder = new System.Drawing.Icon(icoPath, 64, 64);
                    _hIconSmallHolder = new System.Drawing.Icon(icoPath, 32, 32);
                    ChattyWidgets.Core.Win32.Win32Native.SendMessage(hwnd, 0x0080 /*WM_SETICON*/, (IntPtr)1 /*ICON_BIG*/, _hIconBigHolder.Handle);
                    ChattyWidgets.Core.Win32.Win32Native.SendMessage(hwnd, 0x0080 /*WM_SETICON*/, (IntPtr)0 /*ICON_SMALL*/, _hIconSmallHolder.Handle);
                    SetClassIcon(hwnd, _hIconBigHolder.Handle, _hIconSmallHolder.Handle);
                }
            }
            catch { }
        };

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

        // Preload settings state into UI safely without firing change handlers
        _isSuppressingSettingsEvents = true;
        try
        {
            CheckAutostart.IsChecked = WidgetManager.Instance.Settings.StartWithWindows;
            CheckSnapGrid.IsChecked = WidgetManager.Instance.Settings.SnapToGrid;
            CheckHotkeys.IsChecked = WidgetManager.Instance.Settings.EnableHotkeys;
            CheckAcrylic.IsChecked = WidgetManager.Instance.Settings.AcrylicBlur;
            CheckHotReload.IsChecked = WidgetManager.Instance.Settings.EnableHotReload;
            CheckDevMode.IsChecked = WidgetManager.Instance.Settings.EnableDevMode;
            CheckAlwaysResizable.IsChecked = WidgetManager.Instance.Settings.AlwaysResizable;
            int currentGrid = WidgetManager.Instance.Settings.GridSize > 0 ? WidgetManager.Instance.Settings.GridSize : 10;
            SliderGridSize.Value = currentGrid;
            TxtGridSizeVal.Text = $"{currentGrid} px";
            SliderDefaultOpacity.Value = 100;
            TxtOpacityVal.Text = "100%";
        }
        finally
        {
            _isSuppressingSettingsEvents = false;
        }

        _ = CheckForUpdatesSilentlyAsync();

        ApplyTheme(WidgetManager.Instance.Settings.Theme);
        LocalizationService.Instance.Initialize(WidgetManager.Instance.Settings.Language);
        LocalizationService.Instance.LanguageChanged += () => Dispatcher.Invoke(() => ApplyLanguage(LocalizationService.Instance.CurrentLanguage));
        ApplyLanguage(LocalizationService.Instance.CurrentLanguage);

        HotkeyService.Instance.Initialize(this);
        RenderPresetChips();
        PresetService.Instance.PresetsChanged += () => Dispatcher.Invoke(RenderPresetChips);

        WidgetManager.Instance.WidgetsChanged += () => Dispatcher.Invoke(RefreshInstalledWidgets);

        RefreshInstalledWidgets();
        RefreshStoreCatalog();
        UpdateAgentSkillStatus();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (!_isExplicitExit)
        {
            e.Cancel = true;
            Hide();
            _trayIcon?.ShowNotification("ChattyWidgets свернут в трей", "Виджеты продолжают работать на рабочем столе.");
        }
        else
        {
            try { _hIconBigHolder?.Dispose(); } catch {}
            try { _hIconSmallHolder?.Dispose(); } catch {}
        }
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        UpdateMaximizeButtonIcon();
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
        else if (TabWorkshop.IsChecked == true)
        {
            UpdateAgentSkillStatus();
        }
        else if (TabSettings.IsChecked == true)
        {
            SyncSettingsTab();
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

            string launchText = LocalizationService.Instance.T("Hub.BtnLaunch", "Запустить");
            string stopText = LocalizationService.Instance.T("Hub.BtnStop", "Остановить");
            viewModels.Add(new InstalledWidgetViewModel
            {
                Manifest = manifest,
                DirectoryPath = dir,
                IsActive = isActive,
                ActiveCopiesCount = copies,
                TypeBadge = manifest.Type == "native" ? "⚡" : "🌐",
                ToggleButtonText = isActive ? stopText : launchText,
                ToggleButtonBg = isActive
                    ? new SolidColorBrush(Color.FromRgb(239, 68, 68)) // Red
                    : new SolidColorBrush(Color.FromRgb(16, 185, 129)), // Green
                Trust = trust,
                HasUpdate = hasUpdate,
                NewVersion = newVer
            });
        }

        bool hasWidgets = installed.Count > 0;
        if (InstalledEmptyState != null)
        {
            InstalledEmptyState.Visibility = hasWidgets ? Visibility.Collapsed : Visibility.Visible;
        }
        if (InstalledWidgetsList != null)
        {
            InstalledWidgetsList.Visibility = hasWidgets ? Visibility.Visible : Visibility.Collapsed;
        }

        if (InstalledWidgetsList != null)
        {
            InstalledWidgetsList.ItemsSource = viewModels;
        }
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            bool isLight = WidgetManager.Instance?.Settings?.Theme?.Equals("light", StringComparison.OrdinalIgnoreCase) ?? false;
            ApplyHubThemeToTree(InstalledWidgetsList, isLight);
            if (InstalledEmptyState != null) ApplyHubThemeToTree(InstalledEmptyState, isLight);
        }), System.Windows.Threading.DispatcherPriority.Loaded);

        bool isRu = LocalizationService.Instance.IsRussian;
        TxtActiveCount.Text = isRu 
            ? $"{totalActiveCopies} активно из {installed.Count}" 
            : $"{totalActiveCopies} active of {installed.Count}";
        if (updatesCount > 0)
        {
            TxtStatus.Text = isRu 
                ? $"Загружено {installed.Count} виджетов. Доступно обновлений: {updatesCount}!" 
                : $"Loaded {installed.Count} widgets. Updates available: {updatesCount}!";
        }
        else
        {
            TxtStatus.Text = isRu 
                ? $"Загружено {installed.Count} виджетов. Активно экземпляров: {totalActiveCopies}." 
                : $"Loaded {installed.Count} widgets. Active instances: {totalActiveCopies}.";
        }
    }

    private void OnGoToStoreClick(object sender, RoutedEventArgs e)
    {
        SwitchToTab("store");
    }

    private async void OnRefreshInstalledClick(object sender, RoutedEventArgs e)
    {
        TxtStatus.Text = "Проверка обновлений виджетов...";
        var catalog = await StoreService.Instance.GetStoreCatalogAsync(WidgetManager.Instance.Settings.RemoteCatalogUrl);
        RefreshInstalledWidgets();
        int updates = catalog.Count(x => x.HasUpdate);
        if (updates > 0)
        {
            _trayIcon?.ShowNotification("Обновления виджетов", $"Найдено {updates} обновлений. Нажмите «Обновить» на карточке виджета.");
        }
        else
        {
            _trayIcon?.ShowNotification("ChattyWidgets", "Все установленные виджеты актуальны.");
        }
    }

    private void OnConfigureWidgetFromHubClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string id)
        {
            try
            {
                var activeWindow = WidgetManager.Instance.GetActiveWindows().FirstOrDefault(w => w.BaseWidgetId == id || w.InstanceId == id);
                object? vm = activeWindow != null
                    ? (activeWindow.WidgetContentHost.Content as FrameworkElement)?.DataContext
                    : NativeWidgetContextProvider.GetContextFor(id, id);

                var settingsWin = new WidgetSettingsWindow(id, activeWindow?.InstanceId ?? id, vm, () =>
                {
                    if (activeWindow != null && activeWindow.WidgetContentHost.Content is ChattyWidgets.UI.Views.SystemGaugesView gaugesView)
                    {
                        gaugesView.ReloadConfig();
                    }
                });
                settingsWin.Owner = this;
                settingsWin.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Error opening settings for widget {id}: {ex.Message}");
            }
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
                    await RefreshStoreCatalogAsync();
                    TxtStatus.Text = $"Виджет '{item.Name}' успешно обновлен до v{item.Version}!";
                    _trayIcon?.ShowNotification("Виджет обновлен", $"'{item.Name}' успешно обновлен до версии v{item.Version}!");
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Ошибка обновления: {ex.Message}";
                _trayIcon?.ShowNotification("Ошибка обновления", ex.Message);
            }
        }
    }

    private void RenderPresetChips()
    {
        PresetChipsPanel.Children.Clear();
        var presets = WidgetManager.Instance.Settings.Presets;
        string activeId = WidgetManager.Instance.Settings.ActivePresetId;
        bool isRu = LocalizationService.Instance.IsRussian;

        if (presets == null || presets.Count == 0)
        {
            var hint = new TextBlock
            {
                Text = isRu 
                    ? "Пресеты не созданы. Расположите виджеты и нажмите «+ Сохранить текущий вид» или «📥 Импорт .chwp»"
                    : "No presets yet. Arrange widgets and click \"+ Save current layout\" or \"📥 Import .chwp\"",
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
                ToolTip = isRu
                    ? $"ЛКМ: применить пресет\nПКМ: экспортировать в .chwp или удалить\nВиджетов в пресете: {p.Widgets.Count}"
                    : $"Left-click: apply preset\nRight-click: export to .chwp or delete\nWidgets in preset: {p.Widgets.Count}",
                Style = isCurrent ? (Style)FindResource("PrimaryBtn") : (Style)FindResource("SecondaryBtn")
            };
            btn.Click += OnPresetChipClick;

            // Context Menu: Export to .chwp / Delete
            var contextMenu = new ContextMenu();
            var exportItem = new MenuItem { Header = isRu ? "💾 Экспортировать в файл .chwp..." : "💾 Export to .chwp file..." };
            exportItem.Click += (s, e) => ExportPreset(p);
            contextMenu.Items.Add(exportItem);

            var deleteItem = new MenuItem { Header = isRu ? "🗑 Удалить пресет" : "🗑 Delete preset" };
            deleteItem.Click += (s, e) => DeletePreset(p);
            contextMenu.Items.Add(deleteItem);

            btn.ContextMenu = contextMenu;
            PresetChipsPanel.Children.Add(btn);
        }
    }

    private async void OnPresetChipClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DesktopPreset preset)
        {
            await CheckAndApplyPresetAsync(preset, isNewImport: false);
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

    public async void ImportPresetFile(string filePath)
    {
        try
        {
            var preset = PresetService.Instance.ImportPresetFromFile(filePath);
            RenderPresetChips();
            await CheckAndApplyPresetAsync(preset, isNewImport: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка импорта пресета: {ex.Message}", "Ошибка импорта", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task CheckAndApplyPresetAsync(DesktopPreset preset, bool isNewImport)
    {
        bool isRu = LocalizationService.Instance.IsRussian;

        // Check if any widgets referenced in this preset are not yet installed
        var requestedWidgetIds = preset.Widgets.Values
            .Select(w => w.BaseWidgetId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var installed = ChwPackageService.GetInstalledWidgets();
        var installedIds = new HashSet<string>(installed.Select(i => i.Manifest.Id), StringComparer.OrdinalIgnoreCase);
        var missingIds = requestedWidgetIds.Where(id => !installedIds.Contains(id)).ToList();

        if (missingIds.Count > 0)
        {
            TxtStatus.Text = isRu 
                ? "Загрузка каталога для недостающих виджетов..." 
                : "Loading store catalog for missing widgets...";

            var catalog = StoreService.Instance.LastLoadedCatalog 
                          ?? await StoreService.Instance.GetStoreCatalogAsync(WidgetManager.Instance.Settings.RemoteCatalogUrl);

            var dialog = new MissingWidgetsDialog(preset, missingIds, catalog)
            {
                Owner = this
            };

            bool? res = dialog.ShowDialog();
            if (res == true && dialog.UserAccepted)
            {
                PresetService.Instance.ApplyPreset(preset.Id);
                RefreshInstalledWidgets();
                RenderPresetChips();

                int installedCount = dialog.InstalledWidgetsCount;
                TxtStatus.Text = isRu
                    ? (installedCount > 0
                        ? $"Пресет «{preset.Name}» успешно применен! Установлено новых виджетов: {installedCount}."
                        : $"Пресет «{preset.Name}» применен для установленных виджетов.")
                    : (installedCount > 0
                        ? $"Preset «{preset.Name}» applied! Installed {installedCount} new widgets."
                        : $"Preset «{preset.Name}» applied for installed widgets.");
            }
        }
        else
        {
            if (isNewImport)
            {
                var res = MessageBox.Show(
                    isRu 
                        ? $"Пресет «{preset.Name}» успешно импортирован!\n(Все виджеты {preset.Widgets.Count} шт. уже установлены)\n\nПрименить этот пресет на рабочий стол прямо сейчас?"
                        : $"Preset «{preset.Name}» imported!\n(All {preset.Widgets.Count} widgets installed)\n\nApply this preset to the desktop now?",
                    isRu ? "Импорт пресета .chwp" : "Import Preset .chwp",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (res == MessageBoxResult.Yes)
                {
                    PresetService.Instance.ApplyPreset(preset.Id);
                    RefreshInstalledWidgets();
                    RenderPresetChips();
                    TxtStatus.Text = isRu 
                        ? $"Пресет «{preset.Name}» успешно применен на рабочий стол." 
                        : $"Preset «{preset.Name}» applied to desktop.";
                }
            }
            else
            {
                PresetService.Instance.ApplyPreset(preset.Id);
                RefreshInstalledWidgets();
                RenderPresetChips();
                TxtStatus.Text = isRu 
                    ? $"Пресет «{preset.Name}» применен на рабочий стол." 
                    : $"Preset «{preset.Name}» applied to desktop.";
            }
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
        bool isRu = LocalizationService.Instance.IsRussian;
        TxtStatus.Text = isRu ? "Загрузка каталога магазина..." : "Loading store catalog...";
        var catalog = await StoreService.Instance.GetStoreCatalogAsync(WidgetManager.Instance.Settings.RemoteCatalogUrl);

        var viewModels = catalog.Select(item =>
        {
            bool isOfficial = item.IsOfficial || item.Id.StartsWith(SecurityService.OfficialPrefix, StringComparison.OrdinalIgnoreCase);
            string badgeText = isOfficial 
                ? (isRu ? "✦ Официальный" : "✦ Official") 
                : $"{item.Author} (GitHub)";
            var badgeBg = isOfficial
                ? new SolidColorBrush(Color.FromRgb(30, 42, 74))
                : new SolidColorBrush(Color.FromRgb(6, 78, 59));
            var badgeFg = isOfficial
                ? new SolidColorBrush(Color.FromRgb(96, 165, 250))
                : new SolidColorBrush(Color.FromRgb(52, 211, 153));

            bool canAction = !item.IsInstalled || item.HasUpdate;
            string actionText = item.HasUpdate
                ? (isRu ? $"Обновить до v{item.Version} ⬆" : $"Update to v{item.Version} ⬆")
                : (item.IsInstalled 
                    ? (isRu ? "Установлен ✓" : "Installed ✓") 
                    : (isRu ? "Установить ⬇" : "Install ⬇"));

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
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            bool isLight = WidgetManager.Instance?.Settings?.Theme?.Equals("light", StringComparison.OrdinalIgnoreCase) ?? false;
            ApplyHubThemeToTree(StoreItemsList, isLight);
        }), System.Windows.Threading.DispatcherPriority.Loaded);
        int updatesCount = catalog.Count(x => x.HasUpdate);
        if (updatesCount > 0)
        {
            TxtStatus.Text = isRu 
                ? $"Магазин: доступно {catalog.Count} виджетов. Доступно обновлений: {updatesCount}!" 
                : $"Store: {catalog.Count} widgets available. Updates available: {updatesCount}!";
        }
        else
        {
            TxtStatus.Text = isRu 
                ? $"Магазин: доступно {catalog.Count} виджетов." 
                : $"Store: {catalog.Count} widgets available.";
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
            _trayIcon?.ShowNotification("Обновления каталога", $"Найдено {updates} обновлений. Доступны в Магазине и Хабе.");
        }
        else
        {
            _trayIcon?.ShowNotification("ChattyWidgets", "Все виджеты обновлены до актуальной версии.");
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
            }
            catch (Exception ex)
            {
                _trayIcon?.ShowNotification("Ошибка", ex.Message);
                TxtStatus.Text = $"Ошибка: {ex.Message}";
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
                else if (Path.GetExtension(file).Equals(".chwp", StringComparison.OrdinalIgnoreCase))
                {
                    ImportPresetFile(file);
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

    private static string GetAgentSkillTargetPath()
    {
        string userDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userDir, ".agents", "skills", "chw");
    }

    public void UpdateAgentSkillStatus()
    {
        if (BadgeSkillStatus == null || TxtSkillStatusBadge == null || BtnInstallAgentSkill == null)
            return;

        bool isRu = LocalizationService.Instance.IsRussian;
        string targetDir = GetAgentSkillTargetPath();
        string skillFile = Path.Combine(targetDir, "SKILL.md");
        bool isInstalled = File.Exists(skillFile);

        if (isInstalled)
        {
            BadgeSkillStatus.Background = new SolidColorBrush(Color.FromRgb(6, 78, 59)); // Emerald dark
            TxtSkillStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153)); // Emerald light
            TxtSkillStatusBadge.Text = isRu ? "✓ Скилл установлен" : "✓ Skill Installed";
            BtnInstallAgentSkill.Content = isRu ? "🔄 Обновить скилл" : "🔄 Reinstall / Update";
            if (BtnOpenSkillFolder != null) BtnOpenSkillFolder.Visibility = Visibility.Visible;
        }
        else
        {
            BadgeSkillStatus.Background = new SolidColorBrush(Color.FromRgb(69, 26, 3)); // Amber dark
            TxtSkillStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36)); // Amber light
            TxtSkillStatusBadge.Text = isRu ? "⚠ Не установлен" : "⚠ Not Installed";
            BtnInstallAgentSkill.Content = isRu ? "⚡ Установить скилл /chw" : "⚡ Install /chw Skill";
            if (BtnOpenSkillFolder != null) BtnOpenSkillFolder.Visibility = Visibility.Collapsed;
        }

        if (TxtSkillPath != null)
        {
            TxtSkillPath.Text = targetDir;
        }
    }

    private void OnInstallAgentSkillClick(object sender, RoutedEventArgs e)
    {
        try
        {
            bool isRu = LocalizationService.Instance.IsRussian;
            string targetDir = GetAgentSkillTargetPath();
            string userDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            string[] possibleSources = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".agents", "skills", "chw"),
                Path.Combine(Directory.GetCurrentDirectory(), ".agents", "skills", "chw"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".agents", "skills", "chw"),
                Path.Combine(userDir, "Projects", "chattywidgets", ".agents", "skills", "chw")
            };

            string? foundSource = possibleSources.FirstOrDefault(d => Directory.Exists(d) && File.Exists(Path.Combine(d, "SKILL.md")));

            Directory.CreateDirectory(targetDir);

            if (foundSource != null)
            {
                CopyDirectoryRecursive(foundSource, targetDir);
            }
            else
            {
                WriteEmbeddedSkillFiles(targetDir);
            }

            UpdateAgentSkillStatus();

            string title = isRu ? "Скилл /chw установлен" : "Skill /chw Installed";
            string msg = isRu 
                ? "Скилл для автономной разработки виджетов успешно установлен в ~/.agents/skills/chw!\n\nТеперь любой ИИ-ассистент (Antigravity, Claude Code, Cursor, Codex) умеет создавать виджеты по команде /chw." 
                : "Widget development skill installed to ~/.agents/skills/chw!\n\nAI assistants can now build widgets via /chw command.";

            _trayIcon?.ShowNotification(title, msg);
            TxtStatus.Text = isRu ? "Скилл для агента /chw успешно установлен!" : "Agent skill /chw successfully installed!";
            MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Ошибка установки скилла: {ex.Message}";
            MessageBox.Show($"Не удалось установить скилл: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnOpenSkillFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            string targetDir = GetAgentSkillTargetPath();
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }
            Process.Start(new ProcessStartInfo("explorer.exe", targetDir) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Ошибка открытия папки: {ex.Message}";
        }
    }

    private static void CopyDirectoryRecursive(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (string subDir in Directory.GetDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(destinationDir, Path.GetFileName(subDir));
            CopyDirectoryRecursive(subDir, destSubDir);
        }
    }

    private static void WriteEmbeddedSkillFiles(string targetDir)
    {
        string skillMd = @"---
name: chw
description: >-
  Create, scaffold, implement, test, and publish desktop widgets for ChattyWidgets.
  Trigger with `/chw` or when requested to develop a new widget, create glassmorphic XAML UI,
  implement widget ViewModels, configure manifest.json, wire up custom settings cards,
  package .chw files, and publish signed packages to ChattyWidgetsLibrary.
---

# ChattyWidgets Widget Development & Publishing Skill (`/chw`)

Use this skill to autonomously scaffold, develop, wire up, locally test, and officially publish desktop widgets for the ChattyWidgets platform.

## Workflow Overview
[1. Define ID & Manifest] -> [2. Implement ViewModel & Loc] -> [3. Design Glass XAML UI] -> [4. Build Settings Card] -> [5. Pack, Build & Local Test] -> [6. User Approval & Publish]
";
        File.WriteAllText(Path.Combine(targetDir, "SKILL.md"), skillMd);
    }

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

    private void SyncSettingsTab()
    {
        _isSuppressingSettingsEvents = true;
        try
        {
            if (CheckAutostart != null) CheckAutostart.IsChecked = WidgetManager.Instance.Settings.StartWithWindows;
            if (CheckSnapGrid != null) CheckSnapGrid.IsChecked = WidgetManager.Instance.Settings.SnapToGrid;
            if (CheckHotkeys != null) CheckHotkeys.IsChecked = WidgetManager.Instance.Settings.EnableHotkeys;
            if (CheckAcrylic != null) CheckAcrylic.IsChecked = WidgetManager.Instance.Settings.AcrylicBlur;
            if (CheckHotReload != null) CheckHotReload.IsChecked = WidgetManager.Instance.Settings.EnableHotReload;
            if (CheckDevMode != null) CheckDevMode.IsChecked = WidgetManager.Instance.Settings.EnableDevMode;
            if (CheckAlwaysResizable != null) CheckAlwaysResizable.IsChecked = WidgetManager.Instance.Settings.AlwaysResizable;
            if (SliderGridSize != null)
            {
                int currentGrid = WidgetManager.Instance.Settings.GridSize > 0 ? WidgetManager.Instance.Settings.GridSize : 10;
                SliderGridSize.Value = currentGrid;
                if (TxtGridSizeVal != null) TxtGridSizeVal.Text = $"{currentGrid} px";
            }
        }
        finally
        {
            _isSuppressingSettingsEvents = false;
        }
    }

    private void OnAutostartChanged(object sender, RoutedEventArgs e)
    {
        if (_isSuppressingSettingsEvents) return;
        bool enabled = CheckAutostart.IsChecked == true;
        WidgetManager.Instance.SetAutostart(enabled);
        TxtStatus.Text = LocalizationService.Instance.IsRussian 
            ? (enabled ? "Автозапуск с Windows включен" : "Автозапуск с Windows выключен")
            : (enabled ? "Start with Windows enabled" : "Start with Windows disabled");
    }

    private void OnSnapGridChanged(object sender, RoutedEventArgs e)
    {
        if (_isSuppressingSettingsEvents) return;
        bool snap = CheckSnapGrid.IsChecked == true;
        WidgetManager.Instance.Settings.SnapToGrid = snap;
        WidgetManager.Instance.SaveSettings();
        TxtStatus.Text = LocalizationService.Instance.IsRussian 
            ? (snap ? "Прилипание к сетке включено" : "Прилипание к сетке выключено")
            : (snap ? "Snap to grid enabled" : "Snap to grid disabled");
    }

    private void OnHotkeysChanged(object sender, RoutedEventArgs e)
    {
        if (_isSuppressingSettingsEvents) return;
        bool hotkeys = CheckHotkeys.IsChecked == true;
        WidgetManager.Instance.Settings.EnableHotkeys = hotkeys;
        WidgetManager.Instance.SaveSettings();
        if (hotkeys)
            HotkeyService.Instance.RegisterHotkeys();
        else
            HotkeyService.Instance.UnregisterHotkeys();
        TxtStatus.Text = LocalizationService.Instance.IsRussian 
            ? (hotkeys ? "Глобальные хоткеи включены" : "Глобальные хоткеи выключены")
            : (hotkeys ? "Global hotkeys enabled" : "Global hotkeys disabled");
    }

    private void OnAcrylicChanged(object sender, RoutedEventArgs e)
    {
        if (_isSuppressingSettingsEvents) return;
        bool acrylic = CheckAcrylic.IsChecked == true;
        WidgetManager.Instance.Settings.AcrylicBlur = acrylic;
        WidgetManager.Instance.SaveSettings();
        foreach (var win in WidgetManager.Instance.GetActiveWindows())
        {
            try { win.ApplyTheme(); } catch { }
        }
        TxtStatus.Text = LocalizationService.Instance.IsRussian 
            ? (acrylic ? "Размытие Acrylic включено" : "Размытие Acrylic выключено")
            : (acrylic ? "Acrylic blur enabled" : "Acrylic blur disabled");
    }

    private void OnHotReloadChanged(object sender, RoutedEventArgs e)
    {
        if (_isSuppressingSettingsEvents) return;
        bool hr = CheckHotReload.IsChecked == true;
        WidgetManager.Instance.Settings.EnableHotReload = hr;
        WidgetManager.Instance.SaveSettings();
        if (hr)
            HotReloadService.Instance.Start();
        else
            HotReloadService.Instance.Stop();
        TxtStatus.Text = LocalizationService.Instance.IsRussian 
            ? (hr ? "Hot Reload включен" : "Hot Reload выключен")
            : (hr ? "Hot Reload enabled" : "Hot Reload disabled");
    }

    private void OnDevModeChanged(object sender, RoutedEventArgs e)
    {
        if (_isSuppressingSettingsEvents) return;
        bool devMode = CheckDevMode.IsChecked == true;
        WidgetManager.Instance.Settings.EnableDevMode = devMode;
        WidgetManager.Instance.SaveSettings();
        foreach (var win in WidgetManager.Instance.GetActiveWindows())
        {
            try { win.UpdateDevMode(devMode); } catch { }
        }
        TxtStatus.Text = LocalizationService.Instance.IsRussian 
            ? (devMode ? "Режим разработчика (F12) включен" : "Режим разработчика (F12) выключен")
            : (devMode ? "Developer Mode (F12) enabled" : "Developer Mode (F12) disabled");
    }

    private void OnAlwaysResizableChanged(object sender, RoutedEventArgs e)
    {
        if (_isSuppressingSettingsEvents) return;
        bool resizable = CheckAlwaysResizable.IsChecked == true;
        WidgetManager.Instance.UpdateAlwaysResizable(resizable);
        TxtStatus.Text = LocalizationService.Instance.IsRussian 
            ? (resizable ? "Свободное изменение размера включено для всех виджетов" : "Свободное изменение размера выключено")
            : (resizable ? "Free resize enabled for all widgets" : "Free resize disabled");
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

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    private void OnAboutGithubClick(object sender, RoutedEventArgs e) => OpenUrl("https://github.com/chattytoaster/ChattyWidgets");
    private void OnAboutSiteClick(object sender, RoutedEventArgs e) => OpenUrl("https://chattyapps.dev");
    private void OnAboutTelegramChannelClick(object sender, RoutedEventArgs e) => OpenUrl("https://t.me/chattyappsdev");
    private void OnAboutTelegramBotClick(object sender, RoutedEventArgs e) => OpenUrl("https://t.me/chattyappsbot");

    private async Task CheckForUpdatesSilentlyAsync()
    {
        try
        {
            var release = await UpdateService.Instance.CheckForUpdatesAsync();
            if (release != null && release.IsNewer)
            {
                Dispatcher.Invoke(() => ShowUpdateBanner(release));
            }
        }
        catch { }
    }

    private void ShowUpdateBanner(GitHubReleaseInfo release)
    {
        if (TxtUpdateTag != null) TxtUpdateTag.Text = release.TagName;
        if (TxtUpdateNotice != null)
        {
            TxtUpdateNotice.Text = LocalizationService.Instance.IsRussian
                ? "Доступно новое обновление ChattyWidgets!"
                : "A new version of ChattyWidgets is available!";
        }
        if (BannerNewUpdate != null) BannerNewUpdate.Visibility = Visibility.Visible;
        if (TxtStatus != null)
        {
            TxtStatus.Text = LocalizationService.Instance.IsRussian
                ? $"Доступна новая версия: {release.TagName}"
                : $"New version available: {release.TagName}";
        }
    }

    private void OnDownloadUpdateClick(object sender, RoutedEventArgs e)
    {
        UpdateService.Instance.OpenReleasePage();
    }

    private void OnDismissUpdateClick(object sender, RoutedEventArgs e)
    {
        if (BannerNewUpdate != null) BannerNewUpdate.Visibility = Visibility.Collapsed;
    }

    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        if (BtnCheckUpdates == null || TxtUpdateStatus == null) return;

        BtnCheckUpdates.IsEnabled = false;
        TxtUpdateStatus.Visibility = Visibility.Visible;
        TxtUpdateStatus.Text = LocalizationService.Instance.T("Hub.CheckingUpdates", "Проверка обновлений...");
        TxtUpdateStatus.Foreground = (Brush)new BrushConverter().ConvertFrom("#818CF8")!;

        var release = await UpdateService.Instance.CheckForUpdatesAsync();
        BtnCheckUpdates.IsEnabled = true;

        if (release != null && release.IsNewer)
        {
            ShowUpdateBanner(release);
            string msg = string.Format(LocalizationService.Instance.T("Hub.UpdateFound", "Доступна новая версия {0}! Нажмите кнопку выше для скачивания."), release.TagName);
            TxtUpdateStatus.Text = msg;
            TxtUpdateStatus.Foreground = (Brush)new BrushConverter().ConvertFrom("#38BDF8")!;
        }
        else
        {
            TxtUpdateStatus.Text = LocalizationService.Instance.T("Hub.LatestVersion", "У вас установлена последняя актуальная версия ✓");
            TxtUpdateStatus.Foreground = (Brush)new BrushConverter().ConvertFrom("#34D399")!;
        }
    }

    private void OnThemeToggleClick(object sender, RoutedEventArgs e)
    {
        string cur = WidgetManager.Instance.Settings.Theme;
        string nextTheme = cur.Equals("light", StringComparison.OrdinalIgnoreCase) ? "dark" : "light";
        ApplyTheme(nextTheme);
        WidgetManager.Instance.ApplyGlobalTheme(nextTheme);
    }

    private void OnComboHubThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboHubTheme?.SelectedItem is ComboBoxItem item && item.Tag is string t)
        {
            if (WidgetManager.Instance?.Settings != null && WidgetManager.Instance.Settings.Theme != t)
            {
                ApplyTheme(t);
                WidgetManager.Instance.ApplyGlobalTheme(t);
            }
        }
    }

    private void OnLanguageToggleClick(object sender, RoutedEventArgs e)
    {
        string nextLang = LocalizationService.Instance.IsRussian ? "en" : "ru";
        LocalizationService.Instance.SetLanguage(nextLang);
    }

    private void OnComboHubLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboHubLanguage?.SelectedItem is ComboBoxItem item && item.Tag is string l)
        {
            if (LocalizationService.Instance.CurrentLanguage != l)
            {
                LocalizationService.Instance.SetLanguage(l);
            }
        }
    }

    public void ApplyLanguage(string lang)
    {
        bool isRu = lang.Equals("ru", StringComparison.OrdinalIgnoreCase);
        if (BtnLanguageToggle != null)
        {
            BtnLanguageToggle.Content = isRu ? "RU" : "EN";
            BtnLanguageToggle.ToolTip = isRu ? "Язык: Русский (Нажмите для переключения на EN)" : "Language: English (Click to switch to RU)";
        }
        if (ComboHubLanguage != null)
        {
            ComboHubLanguage.SelectedIndex = isRu ? 0 : 1;
        }

        // Navigation Tabs
        if (TabMyWidgets != null) TabMyWidgets.Content = LocalizationService.Instance.T("Hub.TabMyWidgets");
        if (TabStore != null) TabStore.Content = LocalizationService.Instance.T("Hub.TabStore");
        if (TabWorkshop != null) TabWorkshop.Content = LocalizationService.Instance.T("Hub.TabWorkshop");
        if (TabSettings != null) TabSettings.Content = LocalizationService.Instance.T("Hub.TabSettings");

        // Tab 1: My Widgets
        if (TxtInstalledTitle != null) TxtInstalledTitle.Text = LocalizationService.Instance.T("Hub.InstalledTitle", "Установленные виджеты");
        if (BtnRefreshInstalled != null) BtnRefreshInstalled.Content = LocalizationService.Instance.T("Hub.RefreshBtn", "Обновить");
        if (TxtPresetsLabel != null) TxtPresetsLabel.Text = LocalizationService.Instance.T("Hub.PresetsLabel", "🎨 Пресеты стола:");
        if (BtnImportPreset != null)
        {
            BtnImportPreset.Content = LocalizationService.Instance.T("Hub.ImportPresetBtn", "📥 Импорт .chwp");
            BtnImportPreset.ToolTip = LocalizationService.Instance.T("Hub.ImportPresetTip", "Загрузить пресет рабочего стола из файла .chwp");
        }
        if (BtnSavePreset != null)
        {
            BtnSavePreset.Content = LocalizationService.Instance.T("Hub.SavePresetBtn", "+ Сохранить текущий вид");
            BtnSavePreset.ToolTip = LocalizationService.Instance.T("Hub.SavePresetTip", "Сохранить текущее расположение виджетов на экране как пресет");
        }
        if (TxtEmptyInstalledTitle != null) TxtEmptyInstalledTitle.Text = isRu 
            ? "У вас пока нет установленных виджетов" 
            : "No widgets installed yet";
        if (TxtEmptyInstalledDesc != null) TxtEmptyInstalledDesc.Text = isRu
            ? "Откройте каталог виджетов, чтобы выбрать и установить часы, монитор ресурсов, плеер, заметки и другие виджеты."
            : "Open the Widget Store to discover and install clocks, system gauges, media player, notes and more.";
        if (BtnGoToStore != null) BtnGoToStore.Content = isRu
            ? "🛍️ Открыть Каталог Виджетов"
            : "🛍️ Open Widget Store";

        // Tab 2: Store
        if (TxtStoreTitle != null) TxtStoreTitle.Text = isRu ? "Магазин Виджетов" : "Widget Store";
        if (BtnRefreshStore != null) BtnRefreshStore.Content = isRu ? "Проверить обновления" : "Check Updates";
        if (TxtStoreHintTitle != null) TxtStoreHintTitle.Text = isRu ? "📦 Подсказка: " : "📦 Tip: ";
        if (TxtStoreHintDesc != null) TxtStoreHintDesc.Text = isRu 
            ? "Перетащите любой файл .chw прямо в это окно для мгновенной установки виджета." 
            : "Drag and drop any .chw file directly into this window to install the widget instantly.";

        // Tab 3: Workshop
        if (TxtWorkshopTitle != null) TxtWorkshopTitle.Text = isRu ? "Мастерская Разработчика (.chw)" : "Developer Workshop (.chw)";
        if (TxtSkillCardTitle != null) TxtSkillCardTitle.Text = isRu ? "Скилл для ИИ-Агентов (/chw)" : "AI Agent Skill (/chw)";
        if (TxtSkillDesc != null) TxtSkillDesc.Text = isRu
            ? "Позволяет AI-ассистентам (Antigravity, Claude Code, Cursor, Codex) автономно генерировать новые виджеты по команде /chw: верстать стекловидный XAML UI, писать ViewModel, настраивать manifest.json, упаковывать в .chw, тестировать на рабочем столе и публиковать в каталог."
            : "Allows AI assistants (Antigravity, Claude Code, Cursor, Codex) to autonomously generate new widgets via /chw command: build glassmorphic XAML UI, write ViewModels, configure manifest.json, package into .chw, test on desktop, and publish to catalog.";
        if (TxtSkillPathLabel != null) TxtSkillPathLabel.Text = isRu ? "📁 Расположение:" : "📁 Location:";
        if (TxtSkillCommandLabel != null) TxtSkillCommandLabel.Text = isRu ? "Команда в чате: " : "Chat command: ";
        if (BtnOpenSkillFolder != null) BtnOpenSkillFolder.Content = isRu ? "📂 Открыть папку" : "📂 Open Folder";
        UpdateAgentSkillStatus();

        if (TxtCreateTemplateTitle != null) TxtCreateTemplateTitle.Text = isRu ? "Создать новый виджет из шаблона" : "Create new widget from template";
        if (TxtWidgetIdLabel != null) TxtWidgetIdLabel.Text = isRu ? "ID виджета (латиница без пробелов):" : "Widget ID (lowercase, no spaces):";
        if (TxtWidgetNameLabel != null) TxtWidgetNameLabel.Text = isRu ? "Название виджета:" : "Widget Name:";
        if (TxtAuthorLabel != null) TxtAuthorLabel.Text = isRu ? "Имя автора:" : "Author Name:";
        if (TxtRuntimeTypeLabel != null) TxtRuntimeTypeLabel.Text = isRu ? "Тип рантайма:" : "Runtime Type:";
        if (TxtDescLabel != null) TxtDescLabel.Text = isRu ? "Краткое описание:" : "Short Description:";
        if (BtnCreateWidgetTemplate != null) BtnCreateWidgetTemplate.Content = isRu ? "✨ Создать каркас проекта виджета" : "✨ Scaffold Widget Project";
        if (TxtPackTitle != null) TxtPackTitle.Text = isRu ? "Экспорт папки в пакет .chw (Сборщик)" : "Export Folder to .chw Package (Builder)";
        if (TxtPackDesc != null) TxtPackDesc.Text = isRu 
            ? "Выберите папку с manifest.json и ресурсами виджета для упаковки в распространяемый файл .chw:" 
            : "Select a folder containing manifest.json and widget resources to package into a .chw file:";
        if (BtnBrowseFolderToPack != null) BtnBrowseFolderToPack.Content = isRu ? "Обзор папки..." : "Browse folder...";
        if (BtnExportPackage != null) BtnExportPackage.Content = isRu ? "📦 Собрать пакет .chw" : "📦 Build .chw Package";

        // Tab 4: Settings items
        if (TxtSettingsTitle != null) TxtSettingsTitle.Text = LocalizationService.Instance.T("Hub.SettingsTitle", "Настройки движка");
        if (CheckAutostart != null) CheckAutostart.Content = LocalizationService.Instance.T("Hub.Autostart");
        if (CheckSnapGrid != null) CheckSnapGrid.Content = LocalizationService.Instance.T("Hub.SnapGrid");
        if (CheckHotkeys != null) CheckHotkeys.Content = LocalizationService.Instance.T("Hub.Hotkeys");
        if (CheckAcrylic != null) CheckAcrylic.Content = LocalizationService.Instance.T("Hub.Acrylic");
        if (CheckHotReload != null) CheckHotReload.Content = LocalizationService.Instance.T("Hub.HotReload");
        if (CheckDevMode != null) CheckDevMode.Content = LocalizationService.Instance.T("Hub.DevMode");
        if (CheckAlwaysResizable != null) CheckAlwaysResizable.Content = LocalizationService.Instance.T("Hub.AlwaysResizable", "Виджеты расширяемые все время");
        if (TxtHubThemeLabel != null) TxtHubThemeLabel.Text = LocalizationService.Instance.T("Hub.ThemeLabel");
        if (TxtHubThemeHint != null) TxtHubThemeHint.Text = LocalizationService.Instance.T("Hub.ThemeHint");
        if (TxtHubLanguageLabel != null) TxtHubLanguageLabel.Text = LocalizationService.Instance.T("Hub.LanguageLabel");
        if (TxtHubLanguageHint != null) TxtHubLanguageHint.Text = LocalizationService.Instance.T("Hub.LanguageHint");
        if (TxtSnapGridLabel != null) TxtSnapGridLabel.Text = LocalizationService.Instance.T("Hub.GridStep", "Шаг сетки прилипания виджетов:");
        if (TxtDefaultOpacityLabel != null) TxtDefaultOpacityLabel.Text = LocalizationService.Instance.T("Hub.OpacityLabel", "Прозрачность фона виджетов по умолчанию:");
        if (BtnImportChw != null) BtnImportChw.Content = LocalizationService.Instance.T("Hub.ImportWidgetBtn", "➕ Импортировать виджет (.chw)");
        if (BtnOpenAppData != null) BtnOpenAppData.Content = LocalizationService.Instance.T("Hub.OpenAppDataBtn", "Открыть папку данных виджетов (%LocalAppData%)");
        if (TxtAboutTitle != null) TxtAboutTitle.Text = LocalizationService.Instance.T("Hub.AboutTitle", "О движке ChattyWidgets");
        if (TxtAboutVersion != null) TxtAboutVersion.Text = isRu ? "Версия: 1.0.0 (.NET 8.0 Windows x64)" : "Version: 1.0.0 (.NET 8.0 Windows x64)";
        if (TxtAboutDesc != null) TxtAboutDesc.Text = LocalizationService.Instance.T("Hub.AboutDesc", "Движок оптимизирован для фоновой работы: нативные виджеты не нагружают процессор (0% CPU) и занимают минимум памяти.");
        if (BtnDownloadUpdate != null) BtnDownloadUpdate.Content = LocalizationService.Instance.T("Hub.DownloadUpdate", "🚀 Скачать обновление");
        if (BtnCheckUpdates != null) BtnCheckUpdates.Content = LocalizationService.Instance.T("Hub.CheckUpdates", "🔄 Проверить обновления");
        if (TxtUpdateNotice != null && BannerNewUpdate?.Visibility == Visibility.Visible)
            TxtUpdateNotice.Text = LocalizationService.Instance.T("Hub.UpdateAvailable", "Доступно новое обновление ChattyWidgets!");

        // Window caption controls
        if (BtnThemeToggle != null) BtnThemeToggle.ToolTip = isRu ? "Тема оформления (Тёмная / Светлая)" : "Theme (Dark / Light)";
        if (BtnMinimizeWindow != null) BtnMinimizeWindow.ToolTip = isRu ? "Свернуть" : "Minimize";
        if (BtnMaximizeRestore != null) BtnMaximizeRestore.ToolTip = isRu ? "Развернуть" : "Maximize";
        if (BtnCloseWindow != null) BtnCloseWindow.ToolTip = isRu ? "Закрыть в трей" : "Close to tray";

        // Workshop inputs default text
        if (InputNewWidgetName != null && (InputNewWidgetName.Text == "Мой Новый Виджет" || InputNewWidgetName.Text == "My New Widget"))
            InputNewWidgetName.Text = isRu ? "Мой Новый Виджет" : "My New Widget";
        if (InputNewWidgetDesc != null && (InputNewWidgetDesc.Text == "Полезный виджет для рабочего стола Windows" || InputNewWidgetDesc.Text == "A useful desktop widget for Windows"))
            InputNewWidgetDesc.Text = isRu ? "Полезный виджет для рабочего стола Windows" : "A useful desktop widget for Windows";

        // Bottom status bar
        if (TxtEngineVersion != null) TxtEngineVersion.Text = isRu ? "Движок ChattyWidgets v1.0.0" : "ChattyWidgets Engine v1.0.0";

        RenderPresetChips();
        RefreshInstalledWidgets();
        RefreshStoreCatalog();
    }

    private static readonly SolidColorBrush LightTextPrimary = new(Color.FromRgb(15, 23, 42));
    private static readonly SolidColorBrush LightTextSecondary = new(Color.FromRgb(71, 85, 105));
    private static readonly SolidColorBrush LightTextMuted = new(Color.FromRgb(100, 116, 139));
    private static readonly SolidColorBrush LightCardBg = new(Color.FromRgb(255, 255, 255));
    private static readonly SolidColorBrush LightCardBorder = new(Color.FromRgb(226, 232, 240));
    private static readonly SolidColorBrush LightSecondaryBtnBg = new(Color.FromRgb(241, 245, 249));
    private static readonly SolidColorBrush LightSecondaryBtnBorder = new(Color.FromRgb(203, 213, 225));

    private static readonly SolidColorBrush DarkTextPrimary = new(Color.FromRgb(248, 250, 252));
    private static readonly SolidColorBrush DarkTextSecondary = new(Color.FromRgb(148, 163, 184));
    private static readonly SolidColorBrush DarkTextMuted = new(Color.FromRgb(100, 116, 139));
    private static readonly SolidColorBrush DarkCardBg = new(Color.FromRgb(22, 25, 40));
    private static readonly SolidColorBrush DarkCardBorder = new(Color.FromRgb(37, 42, 63));
    private static readonly SolidColorBrush DarkSecondaryBtnBg = new(Color.FromRgb(30, 35, 51));
    private static readonly SolidColorBrush DarkSecondaryBtnBorder = new(Color.FromRgb(51, 59, 82));

    public void ApplyTheme(string theme)
    {
        bool isLight = theme.Equals("light", StringComparison.OrdinalIgnoreCase);

        if (BtnThemeToggle != null)
        {
            BtnThemeToggle.Content = isLight ? "☀️" : "🌙";
            BtnThemeToggle.ToolTip = isLight ? "Тема: Светлая (Нажмите для переключения на тёмную)" : "Тема: Тёмная (Нажмите для переключения на светлую)";
        }

        if (ComboHubTheme != null)
        {
            ComboHubTheme.SelectedIndex = isLight ? 1 : 0;
        }

        var bgBrush = isLight ? new SolidColorBrush(Color.FromRgb(241, 245, 249)) : new SolidColorBrush(Color.FromRgb(15, 17, 26));
        var borderBrush = isLight ? new SolidColorBrush(Color.FromRgb(226, 232, 240)) : new SolidColorBrush(Color.FromRgb(37, 42, 63));
        var topBarBrush = isLight ? new SolidColorBrush(Color.FromRgb(255, 255, 255)) : new SolidColorBrush(Color.FromRgb(18, 21, 34));
        var topBarBorder = isLight ? new SolidColorBrush(Color.FromRgb(226, 232, 240)) : new SolidColorBrush(Color.FromRgb(30, 35, 54));
        var textPrimary = isLight ? LightTextPrimary : DarkTextPrimary;
        var statusBg = isLight ? new SolidColorBrush(Color.FromRgb(241, 245, 249)) : new SolidColorBrush(Color.FromRgb(12, 14, 21));
        var statusBorder = isLight ? new SolidColorBrush(Color.FromRgb(226, 232, 240)) : new SolidColorBrush(Color.FromRgb(26, 30, 44));

        Background = bgBrush;
        if (MainRootBorder != null)
        {
            MainRootBorder.Background = bgBrush;
            MainRootBorder.BorderBrush = borderBrush;
        }

        if (TopHeaderBorder != null)
        {
            TopHeaderBorder.Background = topBarBrush;
            TopHeaderBorder.BorderBrush = topBarBorder;
        }

        if (TxtAppTitle != null) TxtAppTitle.Foreground = textPrimary;
        if (BottomStatusBar != null)
        {
            BottomStatusBar.Background = statusBg;
            BottomStatusBar.BorderBrush = statusBorder;
        }

        ApplyHubThemeToTree(this, isLight);
        RefreshInstalledWidgets();
    }

    private void ApplyHubThemeToTree(DependencyObject? node, bool isLight)
    {
        if (node == null) return;
        int count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);

            if (child is TextBlock tb)
            {
                if (tb.Foreground is SolidColorBrush scb && scb.Color != Colors.Transparent)
                {
                    byte r = scb.Color.R, g = scb.Color.G, b = scb.Color.B;
                    int max = Math.Max(r, Math.Max(g, b));
                    int min = Math.Min(r, Math.Min(g, b));
                    bool isAccent = (max - min) > 75; // Preserve colored accents (red, green, cyan)
                    if (!isAccent)
                    {
                        if (isLight)
                        {
                            if (tb.FontSize >= 14 || tb.FontWeight == FontWeights.Bold || tb.FontWeight == FontWeights.SemiBold)
                                tb.Foreground = LightTextPrimary;
                            else
                                tb.Foreground = LightTextSecondary;
                        }
                        else
                        {
                            if (tb.FontSize >= 14 || tb.FontWeight == FontWeights.Bold || tb.FontWeight == FontWeights.SemiBold)
                                tb.Foreground = DarkTextPrimary;
                            else
                                tb.Foreground = DarkTextSecondary;
                        }
                    }
                }
            }
            else if (child is ChattyWidgets.UI.Controls.MarqueeText mt)
            {
                if (isLight)
                {
                    if (mt.FontSize >= 14 || mt.FontWeight == FontWeights.Bold || mt.FontWeight == FontWeights.SemiBold)
                        mt.Foreground = LightTextPrimary;
                    else
                        mt.Foreground = LightTextSecondary;
                }
                else
                {
                    if (mt.FontSize >= 14 || mt.FontWeight == FontWeights.Bold || mt.FontWeight == FontWeights.SemiBold)
                        mt.Foreground = DarkTextPrimary;
                    else
                        mt.Foreground = DarkTextSecondary;
                }
            }
            else if (child is Button btn)
            {
                if (btn != BtnThemeToggle && btn != BtnMaximizeRestore && btn != BtnCloseWindow && btn != BtnMinimizeWindow)
                {
                    // Check if primary launch/stop button
                    bool isPrimaryLaunchStop = false;
                    if (btn.Background is SolidColorBrush bgScb)
                    {
                        if ((bgScb.Color.R > 200 && bgScb.Color.G < 100) || // Red Stop
                            (bgScb.Color.G > 150 && bgScb.Color.R < 100) || // Green Launch
                            (bgScb.Color.R == 99 && bgScb.Color.G == 102 && bgScb.Color.B == 241)) // Primary Indigo
                        {
                            isPrimaryLaunchStop = true;
                        }
                    }

                    if (!isPrimaryLaunchStop)
                    {
                        bool isDangerRed = btn.Foreground is SolidColorBrush fgScb && fgScb.Color.R > 220 && fgScb.Color.G < 100;
                        if (isLight)
                        {
                            if (!isDangerRed) btn.Foreground = LightTextPrimary;
                            btn.Background = LightSecondaryBtnBg;
                            btn.BorderBrush = LightSecondaryBtnBorder;
                        }
                        else
                        {
                            if (!isDangerRed) btn.Foreground = DarkTextSecondary;
                            btn.Background = DarkSecondaryBtnBg;
                            btn.BorderBrush = DarkSecondaryBtnBorder;
                        }
                    }
                }
            }
            else if (child is Border b && b != MainRootBorder && b != TopHeaderBorder && b != BottomStatusBar)
            {
                if (b.Background is SolidColorBrush bgScb && bgScb.Color != Colors.Transparent)
                {
                    byte r = bgScb.Color.R, g = bgScb.Color.G, bCol = bgScb.Color.B;
                    int max = Math.Max(r, Math.Max(g, bCol));
                    int min = Math.Min(r, Math.Min(g, bCol));
                    bool isAccent = (max - min) > 60;
                    if (!isAccent)
                    {
                        if (isLight)
                        {
                            if (b.CornerRadius.TopLeft >= 10)
                            {
                                b.Background = LightCardBg;
                                b.BorderBrush = LightCardBorder;
                            }
                            else
                            {
                                b.Background = LightSecondaryBtnBg;
                                b.BorderBrush = LightSecondaryBtnBorder;
                            }
                        }
                        else
                        {
                            if (b.CornerRadius.TopLeft >= 10)
                            {
                                b.Background = DarkCardBg;
                                b.BorderBrush = DarkCardBorder;
                            }
                            else
                            {
                                b.Background = new SolidColorBrush(Color.FromRgb(30, 35, 56));
                                b.BorderBrush = new SolidColorBrush(Color.FromRgb(42, 49, 70));
                            }
                        }
                    }
                }
            }
            else if (child is RadioButton rb)
            {
                if (isLight)
                {
                    rb.Foreground = rb.IsChecked == true ? new SolidColorBrush(Color.FromRgb(2, 132, 199)) : LightTextSecondary;
                }
                else
                {
                    rb.Foreground = rb.IsChecked == true ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) : DarkTextSecondary;
                }
            }
            else if (child is CheckBox cb)
            {
                cb.Foreground = isLight ? LightTextPrimary : DarkTextPrimary;
            }

            ApplyHubThemeToTree(child, isLight);
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
    public string ActiveCopiesText => ActiveCopiesCount > 1 
        ? (LocalizationService.Instance.IsRussian ? $"({ActiveCopiesCount} шт.)" : $"({ActiveCopiesCount} copies)") 
        : "";
    public Visibility ActiveCopiesVisibility => ActiveCopiesCount > 1 ? Visibility.Visible : Visibility.Collapsed;
    public string TypeBadge { get; set; } = "🌐";
    public string ToggleButtonText { get; set; } = "Запустить";
    public Brush ToggleButtonBg { get; set; } = Brushes.SlateGray;
    public WidgetTrustInfo Trust { get; set; } = new();

    public bool HasUpdate { get; set; }
    public string NewVersion { get; set; } = string.Empty;
    public string UpdateBadgeText => LocalizationService.Instance.IsRussian 
        ? $"▲ Доступно обновление до v{NewVersion}" 
        : $"▲ Update available to v{NewVersion}";
    public Visibility UpdateBadgeVisibility => HasUpdate ? Visibility.Visible : Visibility.Collapsed;

    public Brush BadgeBg => (Brush)new BrushConverter().ConvertFromString(Trust.BadgeBg)!;
    public Brush BadgeFg => (Brush)new BrushConverter().ConvertFromString(Trust.BadgeFg)!;
    public string DisplayAuthor => Trust.DisplayAuthor;
    public string AuthorPrefix => LocalizationService.Instance.IsRussian ? "Автор:" : "Author:";
    public bool IsVerifiedAuthor => Trust.Level == WidgetTrustLevel.Official || (DisplayAuthor != null && DisplayAuthor.StartsWith("ChattyWidgets", StringComparison.OrdinalIgnoreCase));
    public Visibility VerifiedBadgeVisibility => IsVerifiedAuthor ? Visibility.Visible : Visibility.Collapsed;
    public string AuthorLabel => $"{AuthorPrefix} {DisplayAuthor}";
    public string AddInstanceButtonText => "+";
    public string AddInstanceTooltip => LocalizationService.Instance.IsRussian ? "Добавить еще один экземпляр виджета на рабочий стол" : "Add another instance of this widget to the desktop";
    public string FolderButtonText => LocalizationService.Instance.IsRussian ? "Папка" : "Folder";
    public string DeleteButtonText => "🗑";
    public string DeleteTooltip => LocalizationService.Instance.IsRussian ? "Удалить этот виджет" : "Delete this widget";
    public string ConfigureTooltip => LocalizationService.Instance.IsRussian ? "Настройки виджета" : "Widget settings";
    public string UpdateButtonText => LocalizationService.Instance.IsRussian ? "Обновить" : "Update";

    public string DisplayDescription => Manifest.DisplayDescription;
    public Visibility DescriptionVisibility => string.IsNullOrWhiteSpace(DisplayDescription) ? Visibility.Collapsed : Visibility.Visible;

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
    public string DisplayName => Item.DisplayName;
    public string DisplayDescription => Item.DisplayDescription;
    public Visibility DescriptionVisibility => string.IsNullOrWhiteSpace(DisplayDescription) ? Visibility.Collapsed : Visibility.Visible;
    public string Author { get; set; } = string.Empty;
    public string DisplayAuthor => Author;
    public string AuthorPrefix => LocalizationService.Instance.IsRussian ? "Разработчик:" : "Developer:";
    public bool IsVerifiedAuthor => Author.Equals("ChattyWidgets", StringComparison.OrdinalIgnoreCase) || (Item != null && Item.IsOfficial);
    public Visibility VerifiedBadgeVisibility => IsVerifiedAuthor ? Visibility.Visible : Visibility.Collapsed;
    public string AuthorLabel => $"{AuthorPrefix} {DisplayAuthor}";
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
    public string UpdateBadgeText => LocalizationService.Instance.IsRussian ? $"▲ Доступно обновление v{Item.Version}" : $"▲ Update available v{Item.Version}";
    public string DisplayVersion => Item.IsInstalled ? $"v{Item.InstalledVersion}" : $"v{Item.Version}";
}
