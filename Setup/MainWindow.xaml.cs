using System.IO;
using System.Windows;
using System.Windows.Input;
using ChattyWidgets.Setup.Services;

namespace ChattyWidgets.Setup;

public partial class MainWindow : Window
{
    private readonly InstallService _installService = new();
    private InstallOptions? _options;

    public MainWindow()
    {
        InitializeComponent();

        string defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "ChattyWidgets"
        );
        InputTargetDir.Text = defaultPath;

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
            Description = "Выберите папку для установки ChattyWidgets",
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

        TxtFinishPath.Text = $"Программа установлена в: {_options.TargetDirectory}";
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
