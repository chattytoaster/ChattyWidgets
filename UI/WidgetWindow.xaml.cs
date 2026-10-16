using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using ChattyWidgets.Core.Bridge;
using ChattyWidgets.Core.Models;
using ChattyWidgets.Core.Services;
using ChattyWidgets.Core.Win32;
using Microsoft.Web.WebView2.Wpf;

namespace ChattyWidgets.UI;

public partial class WidgetWindow : Window
{
    public WidgetManifest Manifest { get; }
    public string WidgetDirectory { get; }
    public WidgetInstanceSettings Settings { get; private set; }

    private IntPtr _hwnd = IntPtr.Zero;
    private WebView2? _webView;

    public event Action<WidgetWindow>? WidgetClosed;
    public event Action<WidgetWindow>? SettingsChanged;

    public WidgetWindow(WidgetManifest manifest, string directory, WidgetInstanceSettings settings)
    {
        InitializeComponent();

        Manifest = manifest;
        WidgetDirectory = directory;
        Settings = settings;

        Title = manifest.Name;
        Width = settings.Width > 0 ? settings.Width : manifest.DefaultWidth;
        Height = settings.Height > 0 ? settings.Height : manifest.DefaultHeight;

        double screenWidth = SystemParameters.PrimaryScreenWidth;
        double screenHeight = SystemParameters.PrimaryScreenHeight;
        if (settings.Left < 0 || settings.Left > screenWidth - 50)
            Left = Math.Max(20, screenWidth - Width - 40);
        else
            Left = settings.Left;

        if (settings.Top < 0 || settings.Top > screenHeight - 50)
            Top = 60;
        else
            Top = settings.Top;

        Opacity = settings.Opacity;

        MinWidth = manifest.MinWidth > 0 ? manifest.MinWidth : 50;
        MinHeight = manifest.MinHeight > 0 ? manifest.MinHeight : 50;

        MenuHeader.Header = $"{manifest.Name} (v{manifest.Version})";
        UpdateMenuChecks();

        Loaded += OnLoaded;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        LocationChanged += OnLocationChanged;
        SizeChanged += OnSizeChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;

        DesktopHostManager.ApplyLayer(_hwnd, Settings.Layer);
        if (Settings.IsClickThrough)
        {
            DesktopHostManager.SetClickThrough(_hwnd, true);
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Manifest.Type.Equals("web", StringComparison.OrdinalIgnoreCase))
        {
            await LoadWebWidget();
        }
        else
        {
            LoadNativeWidget();
        }
    }

    private async Task LoadWebWidget()
    {
        try
        {
            var loadingBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 18, 22, 34)),
                CornerRadius = new CornerRadius(16),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = $"✦ Загрузка {Manifest.Name}...",
                    Foreground = Brushes.SlateGray,
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            WidgetContentHost.Content = loadingBorder;

            _webView = new WebView2
            {
                DefaultBackgroundColor = System.Drawing.Color.Transparent
            };

            string userDataFolder = Path.Combine(ChwPackageService.AppDataDir, "webview_cache");
            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await _webView.EnsureCoreWebView2Async(env);

            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

            // Expose the ChattyBridge to JavaScript
            var bridge = new ChattyBridge(
                Manifest.Id,
                closeAction: () => CloseWidget(),
                changeLayerAction: (layer) => SetLayer(layer),
                clickThroughAction: (ct) => SetClickThrough(ct),
                opacityAction: (op) => SetWidgetOpacity(op)
            );

            _webView.CoreWebView2.AddHostObjectToScript("chatty", bridge);

            // Add helper script so JS can access `window.chatty` directly without `chrome.webview.hostObjects.chatty`
            string initScript = @"
                window.chatty = chrome.webview.hostObjects.sync.chatty;
                console.log('ChattyWidgets Bridge Initialized');
            ";
            await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(initScript);

            string entryPath = Path.Combine(WidgetDirectory, Manifest.Entry);
            if (File.Exists(entryPath))
            {
                _webView.CoreWebView2.Navigate(new Uri(entryPath).AbsoluteUri);
                WidgetContentHost.Content = _webView;
            }
            else
            {
                ShowErrorMessage($"Входной файл не найден: {Manifest.Entry}");
            }
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Ошибка инициализации WebView2: {ex.Message}");
        }
    }

    private void LoadNativeWidget()
    {
        try
        {
            string entryPath = Path.Combine(WidgetDirectory, Manifest.Entry);
            if (File.Exists(entryPath))
            {
                using var stream = File.OpenRead(entryPath);
                var element = XamlReader.Load(stream) as UIElement;
                if (element != null)
                {
                    WidgetContentHost.Content = element;

                    if (element is FrameworkElement fe)
                    {
                        fe.DataContext = SystemMonitorService.Instance;
                    }
                }
                else
                {
                    ShowErrorMessage("Не удалось загрузить корневой элемент XAML");
                }
            }
            else
            {
                ShowErrorMessage($"XAML файл не найден: {Manifest.Entry}");
            }
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Ошибка загрузки нативного виджета: {ex.Message}");
        }
    }

    private void ShowErrorMessage(string message)
    {
        var border = new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 30, 30, 40)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16)
        };
        var tb = new TextBlock
        {
            Text = message,
            Foreground = System.Windows.Media.Brushes.OrangeRed,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13
        };
        border.Child = tb;
        WidgetContentHost.Content = border;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && !Settings.IsClickThrough)
        {
            DragMove();
            SnapToScreen();
        }
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        Settings.Left = Left;
        Settings.Top = Top;
        SettingsChanged?.Invoke(this);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Settings.Width = Width;
        Settings.Height = Height;
        SettingsChanged?.Invoke(this);
    }

    private void SnapToScreen()
    {
        var pt = DesktopHostManager.SnapToScreenAndGrid(Left, Top, Width, Height, snapDistance: 15, gridStep: 10);
        Left = pt.X;
        Top = pt.Y;
        Settings.Left = Left;
        Settings.Top = Top;
        SettingsChanged?.Invoke(this);
    }

    public void SetLayer(WidgetLayer layer)
    {
        Settings.Layer = layer;
        if (_hwnd != IntPtr.Zero)
        {
            DesktopHostManager.ApplyLayer(_hwnd, layer);
        }
        UpdateMenuChecks();
        SettingsChanged?.Invoke(this);
    }

    public void SetClickThrough(bool clickThrough)
    {
        Settings.IsClickThrough = clickThrough;
        if (_hwnd != IntPtr.Zero)
        {
            DesktopHostManager.SetClickThrough(_hwnd, clickThrough);
        }
        UpdateMenuChecks();
        SettingsChanged?.Invoke(this);
    }

    public void SetWidgetOpacity(double opacity)
    {
        Settings.Opacity = Math.Clamp(opacity, 0.1, 1.0);
        Opacity = Settings.Opacity;
        SettingsChanged?.Invoke(this);
    }

    private void UpdateMenuChecks()
    {
        MenuLayerDesktop.IsChecked = Settings.Layer == WidgetLayer.Desktop;
        MenuLayerNormal.IsChecked = Settings.Layer == WidgetLayer.Normal;
        MenuLayerTop.IsChecked = Settings.Layer == WidgetLayer.Top;
        MenuClickThrough.IsChecked = Settings.IsClickThrough;
    }

    // Context menu handlers
    private void OnLayerDesktopClick(object sender, RoutedEventArgs e) => SetLayer(WidgetLayer.Desktop);
    private void OnLayerNormalClick(object sender, RoutedEventArgs e) => SetLayer(WidgetLayer.Normal);
    private void OnLayerTopClick(object sender, RoutedEventArgs e) => SetLayer(WidgetLayer.Top);

    private void OnClickThroughClick(object sender, RoutedEventArgs e)
    {
        SetClickThrough(!Settings.IsClickThrough);
    }

    private void OnOpacity100Click(object sender, RoutedEventArgs e) => SetWidgetOpacity(1.0);
    private void OnOpacity85Click(object sender, RoutedEventArgs e) => SetWidgetOpacity(0.85);
    private void OnOpacity70Click(object sender, RoutedEventArgs e) => SetWidgetOpacity(0.70);
    private void OnOpacity50Click(object sender, RoutedEventArgs e) => SetWidgetOpacity(0.50);

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(WidgetDirectory))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", WidgetDirectory) { UseShellExecute = true });
        }
    }

    private void OnOpenHubClick(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow != null)
        {
            Application.Current.MainWindow.Show();
            Application.Current.MainWindow.WindowState = WindowState.Normal;
            Application.Current.MainWindow.Activate();
        }
    }

    private void OnCloseWidgetClick(object sender, RoutedEventArgs e)
    {
        CloseWidget();
    }

    public void CloseWidget()
    {
        Settings.IsEnabled = false;
        SettingsChanged?.Invoke(this);
        WidgetClosed?.Invoke(this);
        Close();
    }
}
