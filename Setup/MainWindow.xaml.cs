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
    private string _currentLang = "ru";

    public MainWindow()
    {
        InitializeComponent();

        string defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "ChattyWidgets"
        );
        InputTargetDir.Text = defaultPath;

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

            Title = "Установка ChattyWidgets";
            TxtAppTitle.Text = "ChattyWidgets Setup";
            TxtHeaderTitle.Text = "Установка ChattyWidgets";
            TxtHeaderSubtitle.Text = "Платформа ультра-легковесных нативных виджетов для рабочего стола Windows";

            TxtFolderLabel.Text = "📁 Папка для установки:";
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
            BtnStartInstall.Content = "✨ Установить";

            TxtProgressTitle.Text = "Установка компонентов ChattyWidgets";
            TxtProgressSubtitle.Text = "Пожалуйста, подождите, выполняется распаковка и настройка системы...";
            TxtProgressFooter.Text = "Интеграция с оболочкой Windows, регистрация протоколов и настройка скилла...";

            TxtFinishedTitle.Text = "Установка успешно завершена!";
            TxtFinishedSubtitle.Text = "ChattyWidgets и все выбранные компоненты настроены и готовы к работе.";
            TxtFinishResultsHeader.Text = "Результаты установки:";
            TxtFinishAssoc.Text = "Ассоциации пакетов .chw и .chwp зарегистрированы в реестре";
            TxtFinishSkillText.Text = "Скилл /chw активирован в ~/.agents/skills/chw для всех AI-ассистентов";
            TxtFinishWebsiteText.Text = "Будет открыт официальный сайт https://chattyapps.dev";
            BtnFinish.Content = "🚀 Завершить и запустить";

            if (_options != null)
            {
                TxtFinishPath.Text = $"Программа установлена в: {_options.TargetDirectory}";
            }
        }
        else
        {
            BtnLangRu.Background = inactiveBg;
            BtnLangRu.Foreground = inactiveFg;
            BtnLangEn.Background = activeBg;
            BtnLangEn.Foreground = activeFg;

            Title = "ChattyWidgets Setup";
            TxtAppTitle.Text = "ChattyWidgets Setup";
            TxtHeaderTitle.Text = "ChattyWidgets Setup";
            TxtHeaderSubtitle.Text = "Ultra-lightweight native glassmorphic desktop widget engine for Windows";

            TxtFolderLabel.Text = "📁 Installation directory:";
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
            BtnStartInstall.Content = "✨ Install";

            TxtProgressTitle.Text = "Installing ChattyWidgets Components";
            TxtProgressSubtitle.Text = "Please wait while files are being unpacked and configured...";
            TxtProgressFooter.Text = "Windows shell integration, file associations, and agent skill setup...";

            TxtFinishedTitle.Text = "Installation Completed Successfully!";
            TxtFinishedSubtitle.Text = "ChattyWidgets and all selected components are ready to use.";
            TxtFinishResultsHeader.Text = "Installation summary:";
            TxtFinishAssoc.Text = ".chw and .chwp file associations registered in Windows";
            TxtFinishSkillText.Text = "/chw skill activated in ~/.agents/skills/chw for all AI agents";
            TxtFinishWebsiteText.Text = "Opening official website https://chattyapps.dev";
            BtnFinish.Content = "🚀 Finish & Launch";

            if (_options != null)
            {
                TxtFinishPath.Text = $"Application installed to: {_options.TargetDirectory}";
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

        // Switch to finished view
        ViewProgress.Visibility = Visibility.Collapsed;
        ViewFinished.Visibility = Visibility.Visible;

        TxtFinishPath.Text = _currentLang == "ru"
            ? $"Программа установлена в: {_options.TargetDirectory}"
            : $"Application installed to: {_options.TargetDirectory}";

        ItemFinishSkill.Visibility = _options.InstallAgentSkill ? Visibility.Visible : Visibility.Collapsed;
        ItemFinishWebsite.Visibility = _options.OpenWebsite ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnFinishClick(object sender, RoutedEventArgs e)
    {
        if (_options != null)
        {
            _installService.FinalizeInstall(_options);
        }
        Close();
    }
}
