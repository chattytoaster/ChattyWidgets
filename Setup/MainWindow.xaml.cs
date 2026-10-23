using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ChattyWidgets.Setup.Services;

namespace ChattyWidgets.Setup;

public partial class MainWindow : Window
{
    private readonly InstallService _installService = new();
    private InstallOptions? _options;
    private UninstallOptions? _uninstallOptions;
    private string _currentLang = "ru";
    private readonly bool _isUninstallMode = false;
    private readonly bool _isUpdateMode = false;

    public MainWindow()
    {
        InitializeComponent();

        string[] args = Environment.GetCommandLineArgs();
        SetupLog.Write($"Setup started. args: {string.Join(" ", args)}");
        _isUninstallMode = args.Any(a =>
            a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("-u", StringComparison.OrdinalIgnoreCase)) ||
            string.Equals(Path.GetFileName(Environment.ProcessPath ?? ""), "Uninstall.exe", StringComparison.OrdinalIgnoreCase);

        bool isSilent = args.Any(a =>
            a.Equals("--silent", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/silent", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("-s", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/s", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--update", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/update", StringComparison.OrdinalIgnoreCase));

        string? customTargetDir = null;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--dir", StringComparison.OrdinalIgnoreCase) ||
                args[i].Equals("-d", StringComparison.OrdinalIgnoreCase) ||
                args[i].Equals("/dir", StringComparison.OrdinalIgnoreCase))
            {
                customTargetDir = args[i + 1].Trim('"');
                break;
            }
        }

        string installedDir = InstallService.GetInstalledDirectory();
        string targetDir = !string.IsNullOrEmpty(customTargetDir)
            ? customTargetDir
            : (File.Exists(Path.Combine(installedDir, "ChattyWidgets.exe"))
                ? installedDir
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ChattyWidgets"));

        _isUpdateMode = File.Exists(Path.Combine(targetDir, "ChattyWidgets.exe")) && !_isUninstallMode;
        InputTargetDir.Text = targetDir;

        // Auto-detect system language
        string sysLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        _currentLang = (sysLang == "ru" || sysLang == "be" || sysLang == "uk") ? "ru" : "en";
        ApplyLanguage(_currentLang);

        _installService.ProgressChanged += (percent, action) =>
        {
            Dispatcher.Invoke(() =>
            {
                ProgressBarSetup.Value = percent;
                TxtProgressPercent.Text = $"{(int)percent}%";
                TxtCurrentAction.Text = action;
            });
        };

        if (!_isUninstallMode && isSilent)
        {
            SetupLog.Write($"Entering silent install mode for target: {targetDir}");
            Visibility = Visibility.Hidden;
            ShowInTaskbar = false;
            var silentOpts = new InstallOptions
            {
                TargetDirectory = targetDir,
                CreateDesktopShortcut = true,
                CreateStartMenuShortcut = true,
                StartWithWindows = true,
                RegisterFileAssociations = true,
                InstallAgentSkill = true,
                OpenWebsite = false,
                LaunchAppAfterInstall = false
            };
            Task.Run(async () =>
            {
                try
                {
                    bool ok = await _installService.InstallAsync(silentOpts);
                    SetupLog.Write($"Silent install completed. Success: {ok}");
                    if (ok)
                    {
                        _installService.FinalizeInstall(silentOpts);
                    }
                }
                catch (Exception ex)
                {
                    SetupLog.Write($"Silent install exception: {ex}");
                }
                finally
                {
                    Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
                }
            });
            return;
        }

        if (_isUninstallMode)
        {
            TxtUninstallPath.Text = installedDir;

            if (isSilent)
            {
                var silentOpts = new UninstallOptions { TargetDirectory = installedDir, RemoveUserData = false, RemoveAgentSkill = true };
                Task.Run(async () =>
                {
                    await _installService.UninstallAsync(silentOpts);
                    InstallService.ScheduleDelayedDirectoryCleanup(installedDir);
                    Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
                });
                return;
            }

            ViewOptions.Visibility = Visibility.Collapsed;
            ViewUninstallConfirm.Visibility = Visibility.Visible;
        }
    }

    private void OnLangRuClick(object sender, RoutedEventArgs e)
    {
        ApplyLanguage("ru");
    }

    private void OnLangEnClick(object sender, RoutedEventArgs e)
    {
        ApplyLanguage("en");
    }

    private void ApplyLanguage(string lang)
    {
        _currentLang = lang;
        _installService.Language = lang;

        var activeBg = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6366F1"));
        var activeFg = System.Windows.Media.Brushes.White;
        var inactiveBg = System.Windows.Media.Brushes.Transparent;
        var inactiveFg = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8"));

        if (lang == "ru")
        {
            BtnLangRu.Background = activeBg;
            BtnLangRu.Foreground = activeFg;
            BtnLangEn.Background = inactiveBg;
            BtnLangEn.Foreground = inactiveFg;

            if (_isUpdateMode)
            {
                Title = "Обновление ChattyWidgets";
                TxtAppTitle.Text = "ChattyWidgets Update";
                TxtHeaderTitle.Text = "Обновление ChattyWidgets";
                TxtHeaderSubtitle.Text = "Обновление исполняемых файлов и библиотек приложения до новой версии";
                TxtFolderLabel.Text = "📁 Папка установленной программы:";
                BtnStartInstall.Content = "✨ Обновить";
                CheckOpenWebsite.IsChecked = false;
                TxtProgressTitle.Text = "Обновление компонентов ChattyWidgets";
                TxtProgressSubtitle.Text = "Пожалуйста, подождите, выполняется распаковка и обновление компонентов...";
                TxtProgressFooter.Text = "Обновление исполняемых файлов, библиотек и AI-скилла...";
                TxtFinishedTitle.Text = "Обновление успешно завершено!";
                TxtFinishedSubtitle.Text = "ChattyWidgets успешно обновлен и готов к работе.";
                TxtFinishResultsHeader.Text = "Результаты обновления:";
                TxtFinishAssoc.Text = "Ассоциации пакетов .chw и .chwp обновлены";
                TxtFinishSkillText.Text = "Скилл /chw обновлен в ~/.agents/skills/chw для всех AI-ассистентов";
                BtnFinish.Content = "🚀 Запустить обновленный ChattyWidgets";
            }
            else
            {
                Title = "Установка ChattyWidgets";
                TxtAppTitle.Text = "ChattyWidgets Setup";
                TxtHeaderTitle.Text = "Установка ChattyWidgets";
                TxtHeaderSubtitle.Text = "Платформа ультра-легковесных нативных виджетов для рабочего стола Windows";
                TxtFolderLabel.Text = "📁 Папка для установки:";
                BtnStartInstall.Content = "✨ Установить";
                TxtProgressTitle.Text = "Установка компонентов ChattyWidgets";
                TxtProgressSubtitle.Text = "Пожалуйста, подождите, выполняется распаковка и настройка системы...";
                TxtProgressFooter.Text = "Интеграция с оболочкой Windows, регистрация протоколов и настройка скилла...";
                TxtFinishedTitle.Text = "Установка успешно завершена!";
                TxtFinishedSubtitle.Text = "ChattyWidgets и все выбранные компоненты настроены и готовы к работе.";
                TxtFinishResultsHeader.Text = "Результаты установки:";
                TxtFinishAssoc.Text = "Ассоциации пакетов .chw и .chwp зарегистрированы в реестре";
                TxtFinishSkillText.Text = "Скилл /chw активирован в ~/.agents/skills/chw для всех AI-ассистентов";
                BtnFinish.Content = "🚀 Завершить и запустить";
            }

            BtnBrowseFolder.Content = "Обзор...";

            TxtOptionsLabel.Text = "⚙️ Параметры и интеграции:";
            CheckDesktopShortcut.Content = "Создать ярлык на рабочем столе Windows";
            CheckStartMenuShortcut.Content = "Создать ярлык в меню «Пуск»";
            CheckStartWithWindows.Content = "Запускать ChattyWidgets автоматически при старте Windows";
            CheckRegisterAssoc.Content = "Ассоциировать пакеты виджетов (.chw) и пресетов (.chwp)";

            CheckInstallSkill.Content = "🤖 Установить скилл для AI-ассистентов (/chw в ~/.agents/skills/chw)";
            TxtSkillDesc.Text = "Позволяет Antigravity, Claude Code, Cursor и Codex автономно разрабатывать виджеты по команде /chw";

            CheckOpenWebsite.Content = "🌐 Открыть официальный сайт chattyapps.dev после завершения";
            TxtWebsiteDesc.Text = "Каталог виджетов, документация, ссылки на Telegram-канал и экосистема ChattyApps";

            CheckLaunchApp.Content = "Запустить ChattyWidgets сразу после установки";

            BtnCancel.Content = "Отмена";

            if (_options != null)
            {
                TxtFinishPath.Text = $"Программа установлена в: {_options.TargetDirectory}";
            }

            if (_isUninstallMode)
            {
                Title = "Удаление ChattyWidgets";
                TxtAppTitle.Text = "ChattyWidgets Uninstall";
                TxtUninstallTitle.Text = "Удаление ChattyWidgets";
                TxtUninstallSubtitle.Text = "Вы действительно хотите удалить ChattyWidgets с вашего компьютера?";
                TxtUninstallPathLabel.Text = "📁 Программа будет удалена из:";
                TxtUninstallOptionsLabel.Text = "Параметры очистки:";
                CheckUninstallRemoveSkill.Content = "Удалить скилл /chw для AI-ассистентов (~/.agents/skills/chw)";
                CheckUninstallRemoveUserData.Content = "Удалить пользовательские виджеты и настройки (%LOCALAPPDATA%\\ChattyWidgets)";
                TxtUninstallDataWarn.Text = "⚠️ Внимание: если вы отметите эту опцию, все ваши скачанные виджеты и персональные настройки будут удалены.";
                BtnUninstallCancel.Content = "Отмена";
                BtnStartUninstall.Content = "🗑️ Удалить";
                TxtUninstallDoneTitle.Text = "ChattyWidgets успешно удален";
                TxtUninstallDoneSubtitle.Text = "Все компоненты приложения, ярлыки и ассоциации были удалены с компьютера.";
                TxtUninstallDoneSummary.Text = "Результаты удаления:";
                TxtUninstallDoneItem1.Text = "✓ Исполняемые файлы программы удалены";
                TxtUninstallDoneItem2.Text = "✓ Ярлыки на Рабочем столе и в Пуске удалены";
                TxtUninstallDoneItem3.Text = "✓ Записи реестра и ассоциации файлов очищены";
                BtnUninstallFinish.Content = "Закрыть";
            }
        }
        else
        {
            BtnLangRu.Background = inactiveBg;
            BtnLangRu.Foreground = inactiveFg;
            BtnLangEn.Background = activeBg;
            BtnLangEn.Foreground = activeFg;

            if (_isUpdateMode)
            {
                Title = "ChattyWidgets Update";
                TxtAppTitle.Text = "ChattyWidgets Update";
                TxtHeaderTitle.Text = "Update ChattyWidgets";
                TxtHeaderSubtitle.Text = "Updating application binaries and libraries to the latest version";
                TxtFolderLabel.Text = "📁 Installed application directory:";
                BtnStartInstall.Content = "✨ Update";
                CheckOpenWebsite.IsChecked = false;
                TxtProgressTitle.Text = "Updating ChattyWidgets Components";
                TxtProgressSubtitle.Text = "Please wait while files and components are being updated...";
                TxtProgressFooter.Text = "Updating binaries, libraries, and AI assistant skill...";
                TxtFinishedTitle.Text = "Update Completed Successfully!";
                TxtFinishedSubtitle.Text = "ChattyWidgets has been successfully updated and is ready to use.";
                TxtFinishResultsHeader.Text = "Update summary:";
                TxtFinishAssoc.Text = ".chw and .chwp file associations updated";
                TxtFinishSkillText.Text = "/chw skill updated in ~/.agents/skills/chw for all AI agents";
                BtnFinish.Content = "🚀 Launch Updated ChattyWidgets";
            }
            else
            {
                Title = "ChattyWidgets Setup";
                TxtAppTitle.Text = "ChattyWidgets Setup";
                TxtHeaderTitle.Text = "ChattyWidgets Setup";
                TxtHeaderSubtitle.Text = "Ultra-lightweight native glassmorphic desktop widget engine for Windows";
                TxtFolderLabel.Text = "📁 Installation directory:";
                BtnStartInstall.Content = "✨ Install";
                TxtProgressTitle.Text = "Installing ChattyWidgets Components";
                TxtProgressSubtitle.Text = "Please wait while files are being unpacked and configured...";
                TxtProgressFooter.Text = "Windows shell integration, file associations, and agent skill setup...";
                TxtFinishedTitle.Text = "Installation Completed Successfully!";
                TxtFinishedSubtitle.Text = "ChattyWidgets and all selected components are ready to use.";
                TxtFinishResultsHeader.Text = "Installation summary:";
                TxtFinishAssoc.Text = ".chw and .chwp file associations registered in Windows";
                TxtFinishSkillText.Text = "/chw skill activated in ~/.agents/skills/chw for all AI agents";
                BtnFinish.Content = "🚀 Finish & Launch";
            }

            BtnBrowseFolder.Content = "Browse...";

            TxtOptionsLabel.Text = "⚙️ Setup options and integrations:";
            CheckDesktopShortcut.Content = "Create Desktop shortcut";
            CheckStartMenuShortcut.Content = "Create Start Menu shortcut";
            CheckStartWithWindows.Content = "Start ChattyWidgets on Windows startup";
            CheckRegisterAssoc.Content = "Register file associations (.chw packages and .chwp presets)";

            CheckInstallSkill.Content = "🤖 Install AI Agent Skill (/chw in ~/.agents/skills/chw)";
            TxtSkillDesc.Text = "Enables Antigravity, Claude Code, Cursor, and Codex to autonomously build widgets via /chw";

            CheckOpenWebsite.Content = "🌐 Open official website chattyapps.dev after installation";
            TxtWebsiteDesc.Text = "Widget catalog, documentation, Telegram community, and ChattyApps ecosystem";

            CheckLaunchApp.Content = "Launch ChattyWidgets immediately after installation";

            BtnCancel.Content = "Cancel";

            if (_options != null)
            {
                TxtFinishPath.Text = $"Application installed to: {_options.TargetDirectory}";
            }

            if (_isUninstallMode)
            {
                Title = "Uninstall ChattyWidgets";
                TxtAppTitle.Text = "ChattyWidgets Uninstall";
                TxtUninstallTitle.Text = "Uninstall ChattyWidgets";
                TxtUninstallSubtitle.Text = "Are you sure you want to uninstall ChattyWidgets from your computer?";
                TxtUninstallPathLabel.Text = "📁 Application will be removed from:";
                TxtUninstallOptionsLabel.Text = "Cleanup options:";
                CheckUninstallRemoveSkill.Content = "Remove /chw skill for AI assistants (~/.agents/skills/chw)";
                CheckUninstallRemoveUserData.Content = "Remove user widgets and settings (%LOCALAPPDATA%\\ChattyWidgets)";
                TxtUninstallDataWarn.Text = "⚠️ Warning: if checked, all downloaded widgets and personal settings will be deleted.";
                BtnUninstallCancel.Content = "Cancel";
                BtnStartUninstall.Content = "🗑️ Uninstall";
                TxtUninstallDoneTitle.Text = "ChattyWidgets Successfully Uninstalled";
                TxtUninstallDoneSubtitle.Text = "All application binaries, shortcuts, and file associations have been removed.";
                TxtUninstallDoneSummary.Text = "Uninstallation summary:";
                TxtUninstallDoneItem1.Text = "✓ Application binaries removed";
                TxtUninstallDoneItem2.Text = "✓ Desktop and Start Menu shortcuts removed";
                TxtUninstallDoneItem3.Text = "✓ Registry entries and file associations cleaned";
                BtnUninstallFinish.Content = "Close";
            }
        }
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnBrowseFolderClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = _currentLang == "ru" ? "Выберите папку для установки ChattyWidgets" : "Select folder to install ChattyWidgets",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(InputTargetDir.Text) ? InputTargetDir.Text : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            InputTargetDir.Text = dialog.SelectedPath;
        }
    }

    private async void OnStartInstallClick(object sender, RoutedEventArgs e)
    {
        string targetDir = InputTargetDir.Text.Trim();
        if (string.IsNullOrEmpty(targetDir))
        {
            targetDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "ChattyWidgets"
            );
            InputTargetDir.Text = targetDir;
        }

        _options = new InstallOptions
        {
            TargetDirectory = targetDir,
            CreateDesktopShortcut = CheckDesktopShortcut.IsChecked == true,
            CreateStartMenuShortcut = CheckStartMenuShortcut.IsChecked == true,
            StartWithWindows = CheckStartWithWindows.IsChecked == true,
            RegisterFileAssociations = CheckRegisterAssoc.IsChecked == true,
            InstallAgentSkill = CheckInstallSkill.IsChecked == true,
            OpenWebsite = CheckOpenWebsite.IsChecked == true,
            LaunchAppAfterInstall = CheckLaunchApp.IsChecked == true
        };

        // Switch to progress view
        ViewOptions.Visibility = Visibility.Collapsed;
        ViewProgress.Visibility = Visibility.Visible;

        bool success = await _installService.InstallAsync(_options);

        // Switch to finished or error view
        ViewProgress.Visibility = Visibility.Collapsed;
        ViewFinished.Visibility = Visibility.Visible;

        if (success)
        {
            BorderFinishedIcon.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#064E3B"));
            BorderFinishedIcon.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981"));
            ShadowFinishedIcon.Color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981");
            TxtFinishedIcon.Text = "✓";
            TxtFinishedIcon.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#34D399"));

            TxtFinishedTitle.Text = _isUpdateMode
                ? (_currentLang == "ru" ? "Обновление успешно завершено!" : "Update Completed Successfully!")
                : (_currentLang == "ru" ? "Установка успешно завершена!" : "Installation Completed Successfully!");

            TxtFinishedSubtitle.Text = _isUpdateMode
                ? (_currentLang == "ru" ? "ChattyWidgets успешно обновлен и готов к работе." : "ChattyWidgets has been updated and is ready to use.")
                : (_currentLang == "ru" ? "ChattyWidgets и все выбранные компоненты настроены и готовы к работе." : "ChattyWidgets and all selected components are ready to use.");

            TxtFinishResultsHeader.Text = _currentLang == "ru" ? "Результаты:" : "Results:";
            TxtFinishPath.Text = _currentLang == "ru"
                ? $"Папка программы: {_options.TargetDirectory}"
                : $"Application folder: {_options.TargetDirectory}";

            TxtFinishAssoc.Visibility = Visibility.Visible;
            ItemFinishSkill.Visibility = _options.InstallAgentSkill ? Visibility.Visible : Visibility.Collapsed;
            ItemFinishWebsite.Visibility = _options.OpenWebsite ? Visibility.Visible : Visibility.Collapsed;

            BtnFinish.Content = _isUpdateMode
                ? (_currentLang == "ru" ? "🚀 Запустить ChattyWidgets" : "🚀 Launch ChattyWidgets")
                : (_currentLang == "ru" ? "🚀 Завершить и запустить" : "🚀 Finish & Launch");
        }
        else
        {
            BorderFinishedIcon.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#450A0A"));
            BorderFinishedIcon.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF4444"));
            ShadowFinishedIcon.Color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF4444");
            TxtFinishedIcon.Text = "✕";
            TxtFinishedIcon.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F87171"));

            TxtFinishedTitle.Text = _currentLang == "ru" ? "Ошибка установки / обновления" : "Installation / Update Error";
            TxtFinishedSubtitle.Text = _installService.LastErrorMessage ?? (_currentLang == "ru"
                ? "Произошла ошибка при распаковке файлов программы."
                : "An error occurred while deploying application files.");

            TxtFinishResultsHeader.Text = _currentLang == "ru" ? "Сведения об ошибке:" : "Error details:";
            TxtFinishPath.Text = _installService.LastErrorMessage ?? (_currentLang == "ru" ? "Не удалось завершить установку." : "Failed to finish installation.");
            TxtFinishAssoc.Visibility = Visibility.Collapsed;
            ItemFinishSkill.Visibility = Visibility.Collapsed;
            ItemFinishWebsite.Visibility = Visibility.Collapsed;

            BtnFinish.Content = _currentLang == "ru" ? "Закрыть" : "Close";
            _options.LaunchAppAfterInstall = false;
            _options.OpenWebsite = false;
        }
    }

    private void OnFinishClick(object sender, RoutedEventArgs e)
    {
        if (_options != null)
        {
            _installService.FinalizeInstall(_options);
        }
        Close();
    }

    private async void OnStartUninstallClick(object sender, RoutedEventArgs e)
    {
        string targetDir = TxtUninstallPath.Text.Trim();
        if (string.IsNullOrEmpty(targetDir))
        {
            targetDir = InstallService.GetInstalledDirectory();
        }

        _uninstallOptions = new UninstallOptions
        {
            TargetDirectory = targetDir,
            RemoveUserData = CheckUninstallRemoveUserData.IsChecked == true,
            RemoveAgentSkill = CheckUninstallRemoveSkill.IsChecked == true
        };

        ViewUninstallConfirm.Visibility = Visibility.Collapsed;
        ViewProgress.Visibility = Visibility.Visible;

        TxtProgressTitle.Text = _currentLang == "ru" ? "Удаление ChattyWidgets" : "Uninstalling ChattyWidgets";
        TxtProgressSubtitle.Text = _currentLang == "ru"
            ? "Пожалуйста, подождите, выполняется удаление компонентов и очистка реестра..."
            : "Please wait while files and settings are being cleaned up...";
        TxtProgressFooter.Text = _currentLang == "ru"
            ? "Очистка ярлыков, реестра Windows и файлов приложения..."
            : "Cleaning shortcuts, Windows registry, and application files...";

        bool success = await _installService.UninstallAsync(_uninstallOptions);

        ViewProgress.Visibility = Visibility.Collapsed;
        ViewUninstallFinished.Visibility = Visibility.Visible;
    }

    private void OnUninstallFinishClick(object sender, RoutedEventArgs e)
    {
        if (_uninstallOptions != null)
        {
            InstallService.ScheduleDelayedDirectoryCleanup(_uninstallOptions.TargetDirectory);
        }
        Close();
    }
}
