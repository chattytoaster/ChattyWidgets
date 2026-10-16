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

        // Window itself stays fully opaque; opacity affects the background surfaces only
        Opacity = 1.0;

        MinWidth = manifest.MinWidth > 0 ? manifest.MinWidth : 50;
        MinHeight = manifest.MinHeight > 0 ? manifest.MinHeight : 50;

        MenuHeader.Header = $"{manifest.Name} (v{manifest.Version})";
        UpdateMenuChecks();
        UpdateAnchorDotStyle();

        Loaded += OnLoaded;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        LocationChanged += OnLocationChanged;
        SizeChanged += OnSizeChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;

        var source = HwndSource.FromHwnd(_hwnd);
        source?.AddHook(WndProc);

        DesktopHostManager.ApplyLayer(_hwnd, Settings.Layer);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32Native.WM_NCHITTEST)
        {
            if (Settings.IsClickThrough)
            {
                int x = (short)(lParam.ToInt64() & 0xFFFF);
                int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                var windowPt = PointFromScreen(new Point(x, y));

                // Check if cursor is over AnchorDot
                if (AnchorDot.IsVisible)
                {
                    var anchorPos = AnchorDot.TranslatePoint(new Point(0, 0), this);
                    var anchorRect = new Rect(anchorPos.X - 4, anchorPos.Y - 4, AnchorDot.ActualWidth + 8, AnchorDot.ActualHeight + 8);

                    if (anchorRect.Contains(windowPt))
                    {
                        handled = true;
                        return (IntPtr)Win32Native.HTCLIENT;
                    }
                }

                // Everywhere else on the widget: pass through clicks
                handled = true;
                return (IntPtr)Win32Native.HTTRANSPARENT;
            }
        }
        return IntPtr.Zero;
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
            TxtLoadingTitle.Text = $"✦ Загрузка {Manifest.Name}...";
            LoadingIndicator.Visibility = Visibility.Visible;

            _webView = new WebView2
            {
                DefaultBackgroundColor = System.Drawing.Color.Transparent
            };
            // Put in visual tree so HWND is created for EnsureCoreWebView2Async
            WidgetContentHost.Content = _webView;

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

            string initScript = @"
                window.chatty = chrome.webview.hostObjects.sync.chatty;
                console.log('ChattyWidgets Bridge Initialized');
            ";
            await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(initScript);

            _webView.NavigationCompleted += (s, e) =>
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
                ApplyBackgroundOpacity(Settings.Opacity);
            };

            string entryPath = Path.Combine(WidgetDirectory, Manifest.Entry);
            if (File.Exists(entryPath))
            {
                _webView.CoreWebView2.Navigate(new Uri(entryPath).AbsoluteUri);
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
                    ApplyBackgroundOpacity(Settings.Opacity);
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
        LoadingIndicator.Visibility = Visibility.Collapsed;
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(200, 30, 30, 40)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16)
        };
        var tb = new TextBlock
        {
            Text = message,
            Foreground = Brushes.OrangeRed,
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

    private void OnAnchorDotLeftDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
            SnapToScreen();
            e.Handled = true;
        }
    }

    private void OnAnchorDotRightClick(object sender, MouseButtonEventArgs e)
    {
        WidgetContextMenu.PlacementTarget = AnchorDot;
        WidgetContextMenu.IsOpen = true;
        e.Handled = true;
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
        UpdateMenuChecks();
        UpdateAnchorDotStyle();
        SettingsChanged?.Invoke(this);
    }

    private void UpdateAnchorDotStyle()
    {
        if (AnchorDot == null) return;

        if (Settings.IsClickThrough)
        {
            AnchorDot.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red when ClickThrough is active
            AnchorDot.ToolTip = "Режим клика насквозь ВКЛЮЧЕН (ПКМ — контекстное меню, ЛКМ — переместить)";
        }
        else
        {
            AnchorDot.Background = new SolidColorBrush(Color.FromRgb(99, 102, 241)); // Indigo when normal
            AnchorDot.ToolTip = "Точка управления ChattyWidgets (ПКМ — меню, ЛКМ — переместить)";
        }
    }

    public void SetWidgetOpacity(double opacity)
    {
        Settings.Opacity = Math.Clamp(opacity, 0.15, 1.0);
        ApplyBackgroundOpacity(Settings.Opacity);
        SettingsChanged?.Invoke(this);
    }

    private void ApplyBackgroundOpacity(double opacity)
    {
        // 1. For Native XAML Widgets (adjust background brush alpha, keep text sharp)
        if (WidgetContentHost.Content is Border border)
        {
            byte alpha = (byte)Math.Clamp((int)(opacity * 255), 30, 255);
            border.Background = new SolidColorBrush(Color.FromArgb(alpha, 18, 22, 34));
        }

        // 2. For Web Widgets (adjust CSS card background opacity, keep text sharp)
        if (_webView?.CoreWebView2 != null)
        {
            string alphaStr = opacity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            string js = $@"
                try {{
                    const cards = document.querySelectorAll('.widget-card, .player-card, .note-card, .crypto-card, body > div');
                    cards.forEach(c => {{
                        c.style.backgroundColor = 'rgba(18, 22, 34, {alphaStr})';
                    }});
                }} catch(e) {{}}
            ";
            _webView.CoreWebView2.ExecuteScriptAsync(js);
        }
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
    private void OnOpacity85Click(object sender, RoutedEventArgs e) => SetWidgetOpacity(0.75);
    private void OnOpacity70Click(object sender, RoutedEventArgs e) => SetWidgetOpacity(0.50);
    private void OnOpacity50Click(object sender, RoutedEventArgs e) => SetWidgetOpacity(0.25);

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
