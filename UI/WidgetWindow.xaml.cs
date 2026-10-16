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
    public string InstanceId => Settings.Id;
    public string BaseWidgetId => Settings.BaseWidgetId;

    private IntPtr _hwnd = IntPtr.Zero;
    public IntPtr Hwnd => _hwnd;
    private WebView2? _webView;

    private static Microsoft.Web.WebView2.Core.CoreWebView2Environment? _sharedWebViewEnvironment;
    private static readonly SemaphoreSlim _webViewEnvLock = new(1, 1);

    public event Action<WidgetWindow>? WidgetClosed;
    public event Action<WidgetWindow>? SettingsChanged;

    public WidgetWindow(WidgetManifest manifest, string directory, WidgetInstanceSettings settings)
    {
        InitializeComponent();

        Manifest = manifest;
        WidgetDirectory = directory;
        Settings = settings;

        Title = !string.IsNullOrWhiteSpace(settings.Title) ? settings.Title : manifest.Name;
        Width = settings.Width > 0 ? settings.Width : manifest.DefaultWidth;
        Height = settings.Height > 0 ? settings.Height : manifest.DefaultHeight;

        double virtLeft = SystemParameters.VirtualScreenLeft;
        double virtTop = SystemParameters.VirtualScreenTop;
        double virtWidth = SystemParameters.VirtualScreenWidth;
        double virtHeight = SystemParameters.VirtualScreenHeight;

        bool isWithinBounds = (settings.Left >= virtLeft - 20)
                           && (settings.Left <= virtLeft + virtWidth - 30)
                           && (settings.Top >= virtTop - 20)
                           && (settings.Top <= virtTop + virtHeight - 30);

        if (isWithinBounds)
        {
            Left = settings.Left;
            Top = settings.Top;
        }
        else
        {
            Left = Math.Max(20, SystemParameters.PrimaryScreenWidth - Width - 40);
            Top = 60;
        }

        var clamped = DesktopHostManager.ClampToScreen(Left, Top, Width, Height);
        Left = clamped.X;
        Top = clamped.Y;
        settings.Left = Left;
        settings.Top = Top;

        // Window itself stays fully opaque; opacity affects the background surfaces only
        Opacity = 1.0;

        MinWidth = manifest.MinWidth > 0 ? manifest.MinWidth : 50;
        MinHeight = manifest.MinHeight > 0 ? manifest.MinHeight : 50;

        string displayName = !string.IsNullOrWhiteSpace(settings.Title) && settings.Title != manifest.Name
            ? $"{settings.Title} (v{manifest.Version})"
            : $"{manifest.Name} (v{manifest.Version})";
        MenuHeader.Header = displayName;
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
        MouseEnter += OnWindowMouseEnter;
        MouseLeave += OnWindowMouseLeave;
        WidgetContextMenu.Opened += OnContextMenuOpened;
        WidgetContextMenu.Closed += OnContextMenuClosed;
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

        if (WidgetManager.Instance.Settings.AcrylicBlur)
        {
            ApplyAcrylicBlur();
        }

        DesktopHostManager.ApplyLayer(_hwnd, Settings.Layer);
        UpdateWindowShape();
    }

    public void UpdateWindowShape()
    {
        if (_hwnd == IntPtr.Zero) return;
        try
        {
            // Windows 11 rounded window preference
            int cornerPref = Win32Native.DWMWCP_ROUND;
            Win32Native.DwmSetWindowAttribute(_hwnd, Win32Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

            // Clip HWND to exact rounded rectangle so DWM never draws rectangular blurred corners
            double dpiScale = 1.0;
            try
            {
                var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
                if (dpi.DpiScaleX > 0) dpiScale = dpi.DpiScaleX;
            }
            catch { }

            double currentW = ActualWidth > 0 ? ActualWidth : Width;
            double currentH = ActualHeight > 0 ? ActualHeight : Height;
            int w = (int)Math.Ceiling(currentW * dpiScale);
            int h = (int)Math.Ceiling(currentH * dpiScale);
            if (w <= 0 || h <= 0) return;

            // 20px radius in DIPs -> ellipse diameter in physical pixels
            int ellipseDiam = (int)Math.Round(40 * dpiScale);
            IntPtr hRgn = Win32Native.CreateRoundRectRgn(0, 0, w + 1, h + 1, ellipseDiam, ellipseDiam);
            if (hRgn != IntPtr.Zero)
            {
                Win32Native.SetWindowRgn(_hwnd, hRgn, true);
            }
        }
        catch { }
    }

    private void ApplyAcrylicBlur()
    {
        if (_hwnd == IntPtr.Zero) return;
        try
        {
            var accent = new Win32Native.AccentPolicy
            {
                AccentState = Win32Native.AccentState.ACCENT_ENABLE_BLURBEHIND,
                GradientColor = 0x88121622
            };
            int accentStructSize = System.Runtime.InteropServices.Marshal.SizeOf(accent);
            IntPtr accentPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(accentStructSize);
            System.Runtime.InteropServices.Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new Win32Native.WindowCompositionAttributeData
            {
                Attribute = Win32Native.WindowCompositionAttribute.WCA_ACCENT_POLICY,
                Data = accentPtr,
                SizeOfData = accentStructSize
            };

            Win32Native.SetWindowCompositionAttribute(_hwnd, ref data);
            System.Runtime.InteropServices.Marshal.FreeHGlobal(accentPtr);
            UpdateWindowShape();
        }
        catch { }
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

    private const int RESIZE_BORDER_THICKNESS = 8;

    private IntPtr CheckResizeHitTest(Point windowPt)
    {
        if (ResizeMode != ResizeMode.CanResizeWithGrip && ResizeMode != ResizeMode.CanResize)
            return IntPtr.Zero;

        double w = ActualWidth > 0 ? ActualWidth : Width;
        double h = ActualHeight > 0 ? ActualHeight : Height;

        bool left = windowPt.X >= 0 && windowPt.X <= RESIZE_BORDER_THICKNESS;
        bool right = windowPt.X >= (w - RESIZE_BORDER_THICKNESS) && windowPt.X <= w;
        bool top = windowPt.Y >= 0 && windowPt.Y <= RESIZE_BORDER_THICKNESS;
        bool bottom = windowPt.Y >= (h - RESIZE_BORDER_THICKNESS) && windowPt.Y <= h;

        if (bottom && right) return (IntPtr)Win32Native.HTBOTTOMRIGHT;
        if (bottom && left) return (IntPtr)Win32Native.HTBOTTOMLEFT;
        if (top && right) return (IntPtr)Win32Native.HTTOPRIGHT;
        if (top && left) return (IntPtr)Win32Native.HTTOPLEFT;
        if (bottom) return (IntPtr)Win32Native.HTBOTTOM;
        if (right) return (IntPtr)Win32Native.HTRIGHT;
        if (left) return (IntPtr)Win32Native.HTLEFT;
        if (top) return (IntPtr)Win32Native.HTTOP;

        return IntPtr.Zero;
    }

    private IntPtr SubclassCallback(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData)
    {
        if (uMsg == (uint)Win32Native.WM_NCHITTEST)
        {
            int x = (short)(lParam.ToInt64() & 0xFFFF);
            int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
            var pt = PointFromScreen(new Point(x, y));

            var resizeHit = CheckResizeHitTest(pt);
            if (resizeHit != IntPtr.Zero)
            {
                return (IntPtr)Win32Native.HTTRANSPARENT; // Let parent window handle the resize border
            }

            if (Settings.IsClickThrough)
            {
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
            int x = (short)(lParam.ToInt64() & 0xFFFF);
            int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
            var windowPt = PointFromScreen(new Point(x, y));

            var resizeHit = CheckResizeHitTest(windowPt);
            if (resizeHit != IntPtr.Zero)
            {
                handled = true;
                return resizeHit;
            }

            if (Settings.IsClickThrough)
            {
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
        else if (msg == Win32Native.WM_MOVING)
        {
            var rect = System.Runtime.InteropServices.Marshal.PtrToStructure<Win32Native.RECT>(lParam);
            int w = rect.Right - rect.Left;
            int h = rect.Bottom - rect.Top;
            var pt = DesktopHostManager.ClampToScreen(rect.Left, rect.Top, w, h);
            rect.Left = (int)pt.X;
            rect.Top = (int)pt.Y;
            rect.Right = rect.Left + w;
            rect.Bottom = rect.Top + h;
            System.Runtime.InteropServices.Marshal.StructureToPtr(rect, lParam, true);
            handled = true;
            return (IntPtr)1;
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
        UpdateWindowShape();
        if (Manifest.Type.Equals("web", StringComparison.OrdinalIgnoreCase))
        {
            await LoadWebWidget();
        }
        else
        {
            LoadNativeWidget();
        }
        UpdateWindowShape();
    }

    private async Task LoadWebWidget()
    {
        try
        {
            // Web widgets host an HWND, so hide the WPF AnchorContainer and inject an HTML control dot instead
            AnchorContainer.Visibility = Visibility.Collapsed;
            AnchorDot.Visibility = Visibility.Collapsed;

            TxtLoadingTitle.Text = $"✦ Загрузка {Manifest.Name}...";
            LoadingIndicator.Visibility = Visibility.Visible;

            _webView = new WebView2
            {
                DefaultBackgroundColor = System.Drawing.Color.Transparent
            };
            // Put in visual tree so HWND is created for EnsureCoreWebView2Async
            WidgetContentHost.Content = _webView;

            if (_sharedWebViewEnvironment == null)
            {
                await _webViewEnvLock.WaitAsync();
                try
                {
                    if (_sharedWebViewEnvironment == null)
                    {
                        string userDataFolder = Path.Combine(ChwPackageService.AppDataDir, "webview_cache");
                        _sharedWebViewEnvironment = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder);
                    }
                }
                finally
                {
                    _webViewEnvLock.Release();
                }
            }
            await _webView.EnsureCoreWebView2Async(_sharedWebViewEnvironment);


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
                instanceId: InstanceId,
                closeAction: () => CloseWidget(),
                duplicateAction: () => WidgetManager.Instance.DuplicateWidget(InstanceId),
                deleteInstanceAction: () => WidgetManager.Instance.DeleteWidgetInstance(InstanceId),
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
                isClickThroughFunc: () => Settings.IsClickThrough,
                autoHideDotAction: (ah) => SetAutoHideAnchorDot(ah),
                isAutoHideDotFunc: () => Settings.AutoHideAnchorDot,
                openDevToolsAction: () => OpenDevTools(),
                reloadAction: () => ReloadWidget()
            );

            _webView.CoreWebView2.AddHostObjectToScript("chatty", bridge);

            string initScript = @"
                window.chatty = chrome.webview.hostObjects.sync.chatty;

                // Sync Windows Accent Color as CSS variables
                try {
                    var accent = window.chatty.getWindowsAccentColor();
                    if (accent) {
                        document.documentElement.style.setProperty('--chatty-accent', accent);
                        document.documentElement.style.setProperty('--chatty-accent-glow', accent + '66');
                    }
                } catch(ex) {}

                // F12 -> Open WebView2 DevTools
                window.addEventListener('keydown', function(e) {
                    if (e.key === 'F12') {
                        e.preventDefault();
                        try { window.chatty.openDevTools(); } catch(ex) {}
                    }
                });

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
                        document.documentElement.classList.add('chatty-is-dragging');
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
                        document.documentElement.classList.remove('chatty-is-dragging');
                        if (hasMoved) {
                            try { window.chatty.snapToScreen(); } catch(ex) {}
                        }
                    }
                });

                // Ensure the control dot is wired up
                function setupChattyDot() {
                    try {
                        if (window.chatty && window.chatty.isAutoHideDot && window.chatty.isAutoHideDot()) {
                            document.documentElement.classList.add('chatty-autohide-dot');
                            if (document.body) document.body.classList.add('chatty-autohide-dot');
                        }
                    } catch(ex) {}

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

                // Click-through & Auto-hide CSS rules
                var ctStyle = document.createElement('style');
                ctStyle.textContent = `
                    .chatty-anchor-dot,
                    #chatty-control-dot {
                        transition: opacity 0.22s ease-in-out, transform 0.15s ease !important;
                    }
                    /* Extended hover hitbox (top-right corner) so hover activates smoothly */
                    .chatty-anchor-dot::before,
                    #chatty-control-dot::before {
                        content: '' !important;
                        position: absolute !important;
                        top: -12px !important;
                        left: -12px !important;
                        right: -12px !important;
                        bottom: -12px !important;
                    }
                    body.chatty-click-through {
                        pointer-events: none !important;
                    }
                    body.chatty-click-through .chatty-anchor-dot,
                    body.chatty-click-through #chatty-control-dot {
                        pointer-events: auto !important;
                    }
                    /* Auto-hide dot mode: hidden by default, visible on hover or when dragging / menu open */
                    html.chatty-autohide-dot .chatty-anchor-dot,
                    html.chatty-autohide-dot #chatty-control-dot,
                    body.chatty-autohide-dot .chatty-anchor-dot,
                    body.chatty-autohide-dot #chatty-control-dot {
                        opacity: 0 !important;
                    }
                    html.chatty-autohide-dot:hover .chatty-anchor-dot,
                    html.chatty-autohide-dot:hover #chatty-control-dot,
                    body.chatty-autohide-dot:hover .chatty-anchor-dot,
                    body.chatty-autohide-dot:hover #chatty-control-dot,
                    .chatty-anchor-dot:hover,
                    #chatty-control-dot:hover,
                    html.chatty-menu-open .chatty-anchor-dot,
                    html.chatty-menu-open #chatty-control-dot,
                    body.chatty-menu-open .chatty-anchor-dot,
                    body.chatty-menu-open #chatty-control-dot,
                    html.chatty-is-dragging .chatty-anchor-dot,
                    html.chatty-is-dragging #chatty-control-dot,
                    body.chatty-is-dragging .chatty-anchor-dot,
                    body.chatty-is-dragging #chatty-control-dot {
                        opacity: 1 !important;
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
                if (Settings.AutoHideAnchorDot)
                {
                    ApplyAutoHideDomState(true);
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
        AnchorContainer.Visibility = Visibility.Visible;
        AnchorDot.Visibility = Visibility.Visible;
        try
        {
            if (BaseWidgetId == "com.chatty.system-gauges")
            {
                var gaugesView = new ChattyWidgets.UI.Views.SystemGaugesView(InstanceId);
                WidgetContentHost.Content = gaugesView;
                ApplyBackgroundOpacity(Settings.Opacity);
                return;
            }

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
        if (!IsLoaded || WindowState != WindowState.Normal) return;
        Settings.Left = Left;
        Settings.Top = Top;
        SettingsChanged?.Invoke(this);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!IsLoaded || WindowState != WindowState.Normal) return;

        double newW = Math.Round(e.NewSize.Width);
        double newH = Math.Round(e.NewSize.Height);

        if (newW < MinWidth) newW = MinWidth;
        if (newH < MinHeight) newH = MinHeight;

        Width = newW;
        Height = newH;

        var pt = DesktopHostManager.ClampToScreen(Left, Top, Width, Height);
        if (Math.Abs(pt.X - Left) > 0.5 || Math.Abs(pt.Y - Top) > 0.5)
        {
            Left = Math.Round(pt.X);
            Top = Math.Round(pt.Y);
        }
        Settings.Left = Left;
        Settings.Top = Top;
        Settings.Width = Width;
        Settings.Height = Height;
        SettingsChanged?.Invoke(this);

        UpdateWindowShape();
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
        var pt = DesktopHostManager.ClampToScreen(Left + deltaX, Top + deltaY, Width, Height);
        Left = pt.X;
        Top = pt.Y;
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

        if (Settings.AutoHideAnchorDot)
        {
            if (!IsMouseOver && !(AnchorContainer?.IsMouseOver ?? false) && !WidgetContextMenu.IsOpen)
            {
                AnchorDot.BeginAnimation(UIElement.OpacityProperty, null);
                AnchorDot.Opacity = 0.0;
            }
        }
        else
        {
            AnchorDot.BeginAnimation(UIElement.OpacityProperty, null);
            AnchorDot.Opacity = 1.0;
        }

        string status = "";
        if (Settings.IsLocked && Settings.IsClickThrough) status = " [Зафиксирован + Клик насквозь]";
        else if (Settings.IsLocked) status = " [Зафиксирован]";
        else if (Settings.IsClickThrough) status = " [Клик насквозь]";
        if (Settings.AutoHideAnchorDot) status += " [Точка скрыта]";

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
        MenuAutoHideDot.IsChecked = Settings.AutoHideAnchorDot;
        MenuFreeResizeToggle.IsChecked = ResizeMode == ResizeMode.CanResizeWithGrip || ResizeMode == ResizeMode.CanResize;
        if (ResizeGripHandle != null)
        {
            ResizeGripHandle.Visibility = MenuFreeResizeToggle.IsChecked ? Visibility.Visible : Visibility.Collapsed;
        }
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

    private void OnAutoHideDotClick(object sender, RoutedEventArgs e)
    {
        SetAutoHideAnchorDot(!Settings.AutoHideAnchorDot);
    }

    public void SetAutoHideAnchorDot(bool autoHide)
    {
        Settings.AutoHideAnchorDot = autoHide;
        ApplyAutoHideDomState(autoHide);
        UpdateMenuChecks();
        UpdateAnchorDotStyle();
        SettingsChanged?.Invoke(this);
    }

    private void ApplyAutoHideDomState(bool autoHide)
    {
        if (_webView?.CoreWebView2 != null)
        {
            string script = autoHide
                ? "document.documentElement.classList.add('chatty-autohide-dot'); if (document.body) document.body.classList.add('chatty-autohide-dot');"
                : "document.documentElement.classList.remove('chatty-autohide-dot'); if (document.body) document.body.classList.remove('chatty-autohide-dot');";
            _webView.CoreWebView2.ExecuteScriptAsync(script);
        }
    }

    private void ApplyMenuOpenDomState(bool isOpen)
    {
        if (_webView?.CoreWebView2 != null)
        {
            string script = isOpen
                ? "document.documentElement.classList.add('chatty-menu-open'); if (document.body) document.body.classList.add('chatty-menu-open');"
                : "document.documentElement.classList.remove('chatty-menu-open'); if (document.body) document.body.classList.remove('chatty-menu-open');";
            _webView.CoreWebView2.ExecuteScriptAsync(script);
        }
    }

    private void OnAnchorContainerMouseEnter(object sender, MouseEventArgs e)
    {
        if (Settings.AutoHideAnchorDot && AnchorDot != null && AnchorContainer.Visibility == Visibility.Visible)
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation(1.0, TimeSpan.FromMilliseconds(180));
            AnchorDot.BeginAnimation(UIElement.OpacityProperty, anim);
        }
    }

    private void OnAnchorContainerMouseLeave(object sender, MouseEventArgs e)
    {
        if (Settings.AutoHideAnchorDot && AnchorDot != null && AnchorContainer.Visibility == Visibility.Visible && !WidgetContextMenu.IsOpen && !IsMouseOver)
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation(0.0, TimeSpan.FromMilliseconds(220));
            AnchorDot.BeginAnimation(UIElement.OpacityProperty, anim);
        }
    }

    private void OnWindowMouseEnter(object sender, MouseEventArgs e)
    {
        if (Settings.AutoHideAnchorDot && AnchorDot != null && AnchorContainer.Visibility == Visibility.Visible)
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation(1.0, TimeSpan.FromMilliseconds(180));
            AnchorDot.BeginAnimation(UIElement.OpacityProperty, anim);
        }
    }

    private void OnWindowMouseLeave(object sender, MouseEventArgs e)
    {
        if (Settings.AutoHideAnchorDot && AnchorDot != null && AnchorContainer.Visibility == Visibility.Visible && !WidgetContextMenu.IsOpen && !(AnchorContainer?.IsMouseOver ?? false))
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation(0.0, TimeSpan.FromMilliseconds(220));
            AnchorDot.BeginAnimation(UIElement.OpacityProperty, anim);
        }
    }

    private void OnContextMenuOpened(object? sender, RoutedEventArgs e)
    {
        // Populate Monitors Submenu
        MenuMonitors.Items.Clear();
        var monitors = ScreenHelper.GetMonitors();
        foreach (var mon in monitors)
        {
            int idx = mon.Index;
            var item = new MenuItem
            {
                Header = mon.ToString(),
                IsCheckable = true,
                IsChecked = (Settings.TargetMonitor == mon.Index)
            };
            item.Click += (s, ev) =>
            {
                WidgetManager.Instance.MoveWidgetToMonitor(InstanceId, idx);
            };
            MenuMonitors.Items.Add(item);
        }

        if (BaseWidgetId == "com.chatty.system-gauges")
        {
            MenuGaugesConstructor.Visibility = Visibility.Visible;
            MenuCustomSeparator.Visibility = Visibility.Visible;
        }

        if (Settings.AutoHideAnchorDot)
        {
            if (AnchorDot != null && AnchorContainer.Visibility == Visibility.Visible)
            {
                AnchorDot.BeginAnimation(UIElement.OpacityProperty, null);
                AnchorDot.Opacity = 1.0;
            }
            ApplyMenuOpenDomState(true);
        }
    }

    private void OnOpenGaugesConstructorClick(object sender, RoutedEventArgs e)
    {
        var constructorWin = new GaugesConstructorWindow(InstanceId, () =>
        {
            if (WidgetContentHost.Content is ChattyWidgets.UI.Views.SystemGaugesView gaugesView)
            {
                gaugesView.ReloadConfig();
            }
        });
        constructorWin.Owner = this;
        constructorWin.ShowDialog();
    }

    private void OnOpenDevToolsClick(object sender, RoutedEventArgs e) => OpenDevTools();
    private void OnReloadWidgetClick(object sender, RoutedEventArgs e) => ReloadWidget();

    public void OpenDevTools()
    {
        Dispatcher.Invoke(() =>
        {
            if (_webView?.CoreWebView2 != null)
            {
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                _webView.CoreWebView2.OpenDevToolsWindow();
            }
        });
    }

    public void ReloadWidget()
    {
        Dispatcher.Invoke(() =>
        {
            if (_webView?.CoreWebView2 != null)
            {
                _webView.CoreWebView2.Reload();
            }
            else if (WidgetContentHost.Content is ChattyWidgets.UI.Views.SystemGaugesView gaugesView)
            {
                gaugesView.ReloadConfig();
            }
            else
            {
                LoadNativeWidget();
            }
        });
    }

    private void OnContextMenuClosed(object? sender, RoutedEventArgs e)
    {
        if (Settings.AutoHideAnchorDot)
        {
            if (AnchorDot != null && AnchorContainer.Visibility == Visibility.Visible && !IsMouseOver && !(AnchorContainer?.IsMouseOver ?? false))
            {
                var anim = new System.Windows.Media.Animation.DoubleAnimation(0.0, TimeSpan.FromMilliseconds(220));
                AnchorDot.BeginAnimation(UIElement.OpacityProperty, anim);
            }
            ApplyMenuOpenDomState(false);
        }
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
            Width = Math.Max(MinWidth, Math.Round(newWidth));
            Height = Math.Max(MinHeight, Math.Round(newHeight));
            var clamped = DesktopHostManager.ClampToScreen(Left, Top, Width, Height);
            Left = Math.Round(clamped.X);
            Top = Math.Round(clamped.Y);
            Settings.Left = Left;
            Settings.Top = Top;
            Settings.Width = Width;
            Settings.Height = Height;
            SettingsChanged?.Invoke(this);
            UpdateWindowShape();
        });
    }

    private void OnToggleFreeResizeClick(object sender, RoutedEventArgs e)
    {
        bool isFree = MenuFreeResizeToggle.IsChecked;
        ResizeMode = isFree ? ResizeMode.CanResizeWithGrip : ResizeMode.NoResize;
        if (ResizeGripHandle != null)
        {
            ResizeGripHandle.Visibility = isFree ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void OnResizeGripLeftDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && !Settings.IsLocked)
        {
            Win32Native.ReleaseCapture();
            Win32Native.SendMessage(_hwnd, Win32Native.WM_NCLBUTTONDOWN, (IntPtr)Win32Native.HTBOTTOMRIGHT, IntPtr.Zero);
            e.Handled = true;
        }
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

    private void OnDuplicateWidgetClick(object sender, RoutedEventArgs e)
    {
        try
        {
            WidgetManager.Instance.DuplicateWidget(InstanceId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WidgetWindow] Duplicate error: {ex.Message}");
        }
    }

    private void OnDeleteInstanceClick(object sender, RoutedEventArgs e)
    {
        var res = MessageBox.Show(
            $"Удалить этот виджет '{Title}' с рабочего стола?",
            "Удаление виджета",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (res == MessageBoxResult.Yes)
        {
            WidgetManager.Instance.DeleteWidgetInstance(InstanceId);
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
