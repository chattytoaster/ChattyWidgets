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

        if (manifest.MenuItems != null && manifest.MenuItems.Count > 0)
        {
            foreach (var item in manifest.MenuItems)
            {
                AddCustomMenuItem(item.Id, item.Title);
            }
        }

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
        if (source != null)
        {
            source.AddHook(WndProc);
            if (source.CompositionTarget != null)
            {
                source.CompositionTarget.BackgroundColor = System.Windows.Media.Colors.Transparent;
            }
        }

        // Enable DWM glass/transparent frame for frameless transparency without blocking WebView2 input
        var margins = new Win32Native.MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        Win32Native.DwmExtendFrameIntoClientArea(_hwnd, ref margins);

        DesktopHostManager.ApplyLayer(_hwnd, Settings.Layer);
    }

    private Win32Native.SubclassProc? _subclassProc;
    private readonly List<IntPtr> _subclassedHwnds = new();

    private void AttachClickThroughSubclass()
    {
        if (_hwnd == IntPtr.Zero) return;
        _subclassProc ??= SubclassCallback;

        Win32Native.EnumChildWindows(_hwnd, (childHwnd, lParam) =>
        {
            if (!_subclassedHwnds.Contains(childHwnd))
            {
                _subclassedHwnds.Add(childHwnd);
                Win32Native.SetWindowSubclass(childHwnd, _subclassProc, (UIntPtr)101, UIntPtr.Zero);
            }
            return true;
        }, IntPtr.Zero);
    }

    private IntPtr SubclassCallback(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData)
    {
        if (uMsg == (uint)Win32Native.WM_NCHITTEST)
        {
            if (Settings.IsClickThrough)
            {
                int x = (short)(lParam.ToInt64() & 0xFFFF);
                int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                var pt = PointFromScreen(new Point(x, y));

                // Anchor dot hotspot (top-right corner 36x36)
                if (pt.X >= (Width - 36) && pt.X <= Width && pt.Y >= 0 && pt.Y <= 36)
                {
                    return (IntPtr)Win32Native.HTCLIENT;
                }

                // Pass through clicks to windows underneath
                return (IntPtr)Win32Native.HTTRANSPARENT;
            }
        }
        else if (uMsg == (uint)Win32Native.WM_RBUTTONUP)
        {
            if (Settings.IsClickThrough)
            {
                int x = (short)(lParam.ToInt64() & 0xFFFF);
                int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                var pt = PointFromScreen(new Point(x, y));
                if (pt.X >= (Width - 36) && pt.X <= Width && pt.Y >= 0 && pt.Y <= 36)
                {
                    ShowWidgetContextMenu();
                    return IntPtr.Zero;
                }
            }
        }

        return Win32Native.DefSubclassProc(hWnd, uMsg, wParam, lParam);
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

                // Check if cursor is over AnchorDot (top-right corner 36x36)
                if (windowPt.X >= (Width - 36) && windowPt.X <= Width && windowPt.Y >= 0 && windowPt.Y <= 36)
                {
                    handled = true;
                    return (IntPtr)Win32Native.HTCLIENT;
                }

                // Everywhere else on the widget: pass through clicks
                handled = true;
                return (IntPtr)Win32Native.HTTRANSPARENT;
            }
        }
        else if (msg == Win32Native.WM_RBUTTONUP)
        {
            ShowWidgetContextMenu();
            handled = true;
            return IntPtr.Zero;
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
            // Web widgets host an HWND, so hide the WPF AnchorDot and inject an HTML control dot instead
            AnchorDot.Visibility = Visibility.Collapsed;

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
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

            _webView.CoreWebView2.ContextMenuRequested += (s, args) =>
            {
                args.Handled = true;
                ShowWidgetContextMenu();
            };

            // Expose the ChattyBridge to JavaScript
            var bridge = new ChattyBridge(
                Manifest.Id,
                closeAction: () => CloseWidget(),
                changeLayerAction: (layer) => SetLayer(layer),
                clickThroughAction: (ct) => SetClickThrough(ct),
                lockAction: (locked) => SetLocked(locked),
                opacityAction: (op) => SetWidgetOpacity(op),
                dragMoveAction: () => StartDragMove(),
                contextMenuAction: () => ShowWidgetContextMenu(),
                moveByAction: (dx, dy) => MoveBy(dx, dy),
                snapAction: () => SnapToScreen(),
                addMenuItemAction: (id, title) => AddCustomMenuItem(id, title),
                resizeAction: (w, h) => ResizeWidget(w, h),
                isLockedFunc: () => Settings.IsLocked,
                isClickThroughFunc: () => Settings.IsClickThrough
            );

            _webView.CoreWebView2.AddHostObjectToScript("chatty", bridge);

            string initScript = @"
                window.chatty = chrome.webview.hostObjects.sync.chatty;

                // Open ChattyWidgets context menu on right click
                window.addEventListener('contextmenu', function(e) {
                    e.preventDefault();
                    try { window.chatty.showContextMenu(); } catch(ex) {}
                });

                // Smooth non-blocking window dragging with movement threshold
                var isDragging = false;
                var hasMoved = false;
                var dragStartX = 0, dragStartY = 0;

                function beginDrag(e) {
                    if (e.button === 0) {
                        isDragging = true;
                        hasMoved = false;
                        dragStartX = e.screenX;
                        dragStartY = e.screenY;
                    }
                }

                window.addEventListener('mousedown', function(e) {
                    if (e.button === 0) {
                        var target = e.target;
                        var isInteractive = target && (target.tagName === 'BUTTON' || target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.tagName === 'A' || target.tagName === 'SELECT' || (target.closest && target.closest('button, input, textarea, a, select, [contenteditable=""true""]')));
                        if (isInteractive) return;

                        // Allow drag on dot, top bar, header, or general widget card
                        var isHeaderOrDot = target.closest && target.closest('.top-bar, .header, .chatty-anchor-dot, #chatty-control-dot, .widget-card, .crypto-card, .player-card, .note-card');
                        if (isHeaderOrDot) {
                            beginDrag(e);
                        }
                    }
                });

                window.addEventListener('mousemove', function(e) {
                    if (isDragging) {
                        var dx = e.screenX - dragStartX;
                        var dy = e.screenY - dragStartY;
                        if (!hasMoved && (Math.abs(dx) > 3 || Math.abs(dy) > 3)) {
                            hasMoved = true;
                        }
                        if (hasMoved && (dx !== 0 || dy !== 0)) {
                            dragStartX = e.screenX;
                            dragStartY = e.screenY;
                            try { window.chatty.moveBy(dx, dy); } catch(ex) {}
                        }
                    }
                });

                window.addEventListener('mouseup', function(e) {
                    if (isDragging) {
                        isDragging = false;
                        if (hasMoved) {
                            try { window.chatty.snapToScreen(); } catch(ex) {}
                        }
                    }
                });

                // Ensure the control dot is wired up
                function setupChattyDot() {
                    var dot = document.querySelector('.chatty-anchor-dot') || document.getElementById('chatty-control-dot');
                    if (!dot && document.body) {
                        dot = document.createElement('div');
                        dot.className = 'chatty-anchor-dot';
                        dot.id = 'chatty-control-dot';
                        dot.title = 'ChattyWidgets: ЛКМ — переместить, ПКМ — меню';
                        dot.style.cssText = 'position:fixed!important;top:12px!important;right:12px!important;width:12px!important;height:12px!important;background:#3B82F6!important;border-radius:50%!important;box-shadow:0 0 8px #60A5FA!important;cursor:grab!important;z-index:999999!important;display:block!important;box-sizing:border-box!important;';
                        var inner = document.createElement('div');
                        inner.style.cssText = 'position:absolute!important;top:4px!important;left:4px!important;width:4px!important;height:4px!important;background:#FFFFFF!important;border-radius:50%!important;pointer-events:none!important;';
                        dot.appendChild(inner);
                        document.body.appendChild(dot);
                    }

                    if (dot) {
                        dot.addEventListener('mousedown', function(e) {
                            if (e.button === 0) {
                                beginDrag(e);
                                dot.style.cursor = 'grabbing';
                                e.stopPropagation();
                            }
                        });
                        dot.addEventListener('contextmenu', function(e) {
                            e.preventDefault();
                            e.stopPropagation();
                            try { window.chatty.showContextMenu(); } catch(ex) {}
                        });
                    }
                }

                // Click-through CSS rules
                var ctStyle = document.createElement('style');
                ctStyle.textContent = `
                    body.chatty-click-through {
                        pointer-events: none !important;
                    }
                    body.chatty-click-through .chatty-anchor-dot,
                    body.chatty-click-through #chatty-control-dot {
                        pointer-events: auto !important;
                    }
                `;
                if (document.head) {
                    document.head.appendChild(ctStyle);
                } else {
                    document.addEventListener('DOMContentLoaded', function() { document.head.appendChild(ctStyle); });
                }

                if (document.readyState === 'loading') {
                    document.addEventListener('DOMContentLoaded', setupChattyDot);
                } else {
                    setupChattyDot();
                }
            ";
            await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(initScript);

            _webView.NavigationCompleted += (s, e) =>
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
                ApplyBackgroundOpacity(Settings.Opacity);
                AttachClickThroughSubclass();
                if (Settings.IsClickThrough)
                {
                    ApplyClickThroughDomState(true);
                }
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
        AnchorDot.Visibility = Visibility.Visible;
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
        if (e.ButtonState == MouseButtonState.Pressed && !Settings.IsClickThrough && !Settings.IsLocked)
        {
            DragMove();
            SnapToScreen();
        }
    }

    private void OnAnchorDotLeftDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && !Settings.IsLocked)
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

    private void OnWindowRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        ShowWidgetContextMenu();
        e.Handled = true;
    }

    public void StartDragMove()
    {
        Dispatcher.Invoke(() =>
        {
            if (Settings.IsClickThrough || Settings.IsLocked) return;
            Win32Native.ReleaseCapture();
            Win32Native.SendMessage(_hwnd, Win32Native.WM_NCLBUTTONDOWN, (IntPtr)Win32Native.HTCAPTION, IntPtr.Zero);
            SnapToScreen();
        });
    }

    public void ShowWidgetContextMenu()
    {
        Dispatcher.Invoke(() =>
        {
            WidgetContextMenu.PlacementTarget = AnchorDot != null && AnchorDot.IsVisible ? AnchorDot : this;
            WidgetContextMenu.IsOpen = true;
        });
    }

    public void MoveBy(double deltaX, double deltaY)
    {
        if (Settings.IsClickThrough || Settings.IsLocked) return;
        Left += deltaX;
        Top += deltaY;
        Settings.Left = Left;
        Settings.Top = Top;
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
        AttachClickThroughSubclass();
        ApplyClickThroughDomState(clickThrough);
        UpdateMenuChecks();
        UpdateAnchorDotStyle();
        SettingsChanged?.Invoke(this);
    }

    private void ApplyClickThroughDomState(bool clickThrough)
    {
        if (_webView?.CoreWebView2 != null)
        {
            string script = clickThrough
                ? "document.body.classList.add('chatty-click-through');"
                : "document.body.classList.remove('chatty-click-through');";
            _webView.CoreWebView2.ExecuteScriptAsync(script);
        }
    }

    public void SetLocked(bool locked)
    {
        Settings.IsLocked = locked;
        UpdateMenuChecks();
        UpdateAnchorDotStyle();
        SettingsChanged?.Invoke(this);
    }

    private void UpdateAnchorDotStyle()
    {
        if (AnchorDot == null) return;

        // AnchorDot is ALWAYS blue (never red) as requested by user
        AnchorDot.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)); // Vibrant blue #3B82F6
        if (AnchorDot.Effect is System.Windows.Media.Effects.DropShadowEffect shadow)
        {
            shadow.Color = Color.FromRgb(96, 165, 250); // Glow #60A5FA
        }

        string status = "";
        if (Settings.IsLocked && Settings.IsClickThrough) status = " [Зафиксирован + Клик насквозь]";
        else if (Settings.IsLocked) status = " [Зафиксирован]";
        else if (Settings.IsClickThrough) status = " [Клик насквозь]";

        AnchorDot.ToolTip = $"ChattyWidgets{status}: ПКМ — меню настроек" + (Settings.IsLocked ? "" : ", ЛКМ — переместить");
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

    private readonly Dictionary<string, MenuItem> _customMenuItems = new();

    public void AddCustomMenuItem(string id, string title)
    {
        Dispatcher.Invoke(() =>
        {
            if (_customMenuItems.TryGetValue(id, out var existingItem))
            {
                existingItem.Header = title;
                return;
            }

            MenuCustomSeparator.Visibility = Visibility.Visible;
            MenuCustomSettings.Visibility = Visibility.Visible;

            var menuItem = new MenuItem
            {
                Header = title,
                Tag = id
            };
            menuItem.Click += (s, e) => TriggerCustomMenuAction(id);

            _customMenuItems[id] = menuItem;
            MenuCustomSettings.Items.Add(menuItem);
        });
    }

    public void TriggerCustomMenuAction(string id)
    {
        if (_webView?.CoreWebView2 != null)
        {
            string escapedId = id.Replace("\\", "\\\\").Replace("'", "\\'");
            string js = $@"
                try {{
                    window.dispatchEvent(new CustomEvent('chatty:menu', {{ detail: {{ id: '{escapedId}' }} }}));
                    if (typeof window.onChattyMenu === 'function') {{
                        window.onChattyMenu('{escapedId}');
                    }}
                }} catch(e) {{
                    console.error('Custom menu error:', e);
                }}
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
        MenuLockPosition.IsChecked = Settings.IsLocked;
        MenuFreeResizeToggle.IsChecked = ResizeMode == ResizeMode.CanResizeWithGrip || ResizeMode == ResizeMode.CanResize;
    }

    // Context menu handlers
    private void OnLayerDesktopClick(object sender, RoutedEventArgs e) => SetLayer(WidgetLayer.Desktop);
    private void OnLayerNormalClick(object sender, RoutedEventArgs e) => SetLayer(WidgetLayer.Normal);
    private void OnLayerTopClick(object sender, RoutedEventArgs e) => SetLayer(WidgetLayer.Top);

    private void OnLockPositionClick(object sender, RoutedEventArgs e)
    {
        SetLocked(!Settings.IsLocked);
    }

    private void OnClickThroughClick(object sender, RoutedEventArgs e)
    {
        SetClickThrough(!Settings.IsClickThrough);
    }

    private void OnResize75Click(object sender, RoutedEventArgs e) => ApplyResizePreset(0.75);
    private void OnResize100Click(object sender, RoutedEventArgs e) => ApplyResizePreset(1.0);
    private void OnResize125Click(object sender, RoutedEventArgs e) => ApplyResizePreset(1.25);
    private void OnResize150Click(object sender, RoutedEventArgs e) => ApplyResizePreset(1.50);

    private void ApplyResizePreset(double factor)
    {
        double baseW = Manifest.DefaultWidth > 0 ? Manifest.DefaultWidth : 300;
        double baseH = Manifest.DefaultHeight > 0 ? Manifest.DefaultHeight : 200;
        ResizeWidget(Math.Round(baseW * factor), Math.Round(baseH * factor));
    }

    public void ResizeWidget(double newWidth, double newHeight)
    {
        Dispatcher.Invoke(() =>
        {
            Width = Math.Max(MinWidth, newWidth);
            Height = Math.Max(MinHeight, newHeight);
            Settings.Width = Width;
            Settings.Height = Height;
            SettingsChanged?.Invoke(this);
        });
    }

    private void OnToggleFreeResizeClick(object sender, RoutedEventArgs e)
    {
        bool isFree = MenuFreeResizeToggle.IsChecked;
        ResizeMode = isFree ? ResizeMode.CanResizeWithGrip : ResizeMode.NoResize;
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
