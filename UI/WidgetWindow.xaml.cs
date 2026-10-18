using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using ChattyWidgets.Core.Bridge;
using ChattyWidgets.Core.Models;
using ChattyWidgets.Core.Services;
using ChattyWidgets.Core.Win32;
using Microsoft.Web.WebView2.Wpf;
using DragDropEffects = System.Windows.DragDropEffects;
using UserControl = System.Windows.Controls.UserControl;
using Button = System.Windows.Controls.Button;
using VisualTreeHelper = System.Windows.Media.VisualTreeHelper;
using ChattyWidgets.UI.Controls;

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
    private AnchorDotOverlayWindow? _overlayDotWindow;

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

        string displayName = !string.IsNullOrWhiteSpace(settings.Title) && settings.Title != manifest.Name && settings.Title != manifest.DisplayName
            ? $"{settings.Title} (v{manifest.Version})"
            : $"{manifest.DisplayName} (v{manifest.Version})";
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

        ApplyLocalization();
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged() => Dispatcher.Invoke(ApplyLocalization);

    private void ApplyLocalization()
    {
        var loc = LocalizationService.Instance;
        string displayName = !string.IsNullOrWhiteSpace(Settings.Title) && Settings.Title != Manifest.Name && Settings.Title != Manifest.DisplayName
            ? $"{Settings.Title} (v{Manifest.Version})"
            : $"{Manifest.DisplayName} (v{Manifest.Version})";
        if (MenuHeader != null) MenuHeader.Header = displayName;
        Title = !string.IsNullOrWhiteSpace(Settings.Title) ? Settings.Title : Manifest.DisplayName;

        if (MenuWidgetSettings != null) MenuWidgetSettings.Header = loc.T("Menu.Settings", "⚙️ Настройки виджета...");
        if (MenuDuplicateWidget != null) MenuDuplicateWidget.Header = loc.T("Menu.Duplicate", "📋 Скопировать виджет (Создать копию)");
        if (MenuDeleteInstance != null) MenuDeleteInstance.Header = loc.T("Menu.Delete", "🗑 Удалить этот виджет");
        if (MenuGroupLayer != null) MenuGroupLayer.Header = loc.T("Menu.LayerGroup", "Слой отображения");
        if (MenuLayerDesktop != null) MenuLayerDesktop.Header = loc.T("Menu.LayerDesktop", "На рабочем столе (под всеми окнами)");
        if (MenuLayerNormal != null) MenuLayerNormal.Header = loc.T("Menu.LayerNormal", "Плавающее окно");
        if (MenuLayerTop != null) MenuLayerTop.Header = loc.T("Menu.LayerTop", "Поверх всех окон");
        if (MenuGroupTheme != null) MenuGroupTheme.Header = loc.T("Menu.ThemeGroup", "Тема оформления");
        if (MenuThemeAuto != null) MenuThemeAuto.Header = loc.T("Menu.ThemeAuto", "✦ Как в Хабе (Авто)");
        if (MenuThemeDark != null) MenuThemeDark.Header = loc.T("Menu.ThemeDark", "🌙 Тёмная тема");
        if (MenuThemeLight != null) MenuThemeLight.Header = loc.T("Menu.ThemeLight", "☀️ Светлая тема");
        if (MenuLockPosition != null) MenuLockPosition.Header = loc.T("Menu.LockPosition", "Зафиксировать позицию");
        if (MenuClickThrough != null) MenuClickThrough.Header = loc.T("Menu.ClickThrough", "Клик насквозь (Click-through)");
        if (MenuAutoHideDot != null) MenuAutoHideDot.Header = loc.T("Menu.AutoHideDot", "Спрятать точку управления");
        if (MenuGroupResize != null) MenuGroupResize.Header = loc.T("Menu.ResizeGroup", "Изменить размер");
        if (MenuResize75 != null) MenuResize75.Header = loc.T("Menu.Resize75", "75% (Компактный)");
        if (MenuResize100 != null) MenuResize100.Header = loc.T("Menu.Resize100", "100% (Обычный)");
        if (MenuResize125 != null) MenuResize125.Header = loc.T("Menu.Resize125", "125% (Увеличенный)");
        if (MenuResize150 != null) MenuResize150.Header = loc.T("Menu.Resize150", "150% (Большой)");
        if (MenuFreeResizeToggle != null) MenuFreeResizeToggle.Header = loc.T("Menu.FreeResize", "Тянуть за края (Свободный размер)");
        if (MenuGroupOpacity != null) MenuGroupOpacity.Header = loc.T("Menu.OpacityGroup", "Прозрачность фона");
        if (MenuOpacity100 != null) MenuOpacity100.Header = loc.T("Menu.Opacity100", "100% (Непрозрачный фон)");
        if (MenuOpacity85 != null) MenuOpacity85.Header = loc.T("Menu.Opacity85", "75% фона");
        if (MenuOpacity70 != null) MenuOpacity70.Header = loc.T("Menu.Opacity70", "50% фона");
        if (MenuOpacity50 != null) MenuOpacity50.Header = loc.T("Menu.Opacity50", "25% фона (Стекло)");
        if (MenuMonitors != null) MenuMonitors.Header = loc.T("Menu.Monitors", "Переместить на монитор...");
        if (MenuGaugesConstructor != null) MenuGaugesConstructor.Header = loc.T("Menu.GaugesConstructor", "🎨 Изменить формат (Конструктор)...");
        if (MenuCustomSettings != null) MenuCustomSettings.Header = loc.T("Menu.CustomSettings", "Настройки виджета");
        if (MenuReloadWidget != null) MenuReloadWidget.Header = loc.T("Menu.Reload", "⟳ Перезагрузить виджет");
        if (MenuDevTools != null)
        {
            MenuDevTools.Header = loc.T("Menu.DevTools", "🛠 Открыть DevTools (F12)");
            MenuDevTools.Visibility = WidgetManager.Instance.Settings.EnableDevMode ? Visibility.Visible : Visibility.Collapsed;
        }
        if (MenuOpenFolder != null) MenuOpenFolder.Header = loc.T("Menu.OpenFolder", "Открыть папку виджета");
        if (MenuOpenHub != null) MenuOpenHub.Header = loc.T("Menu.OpenHub", "Открыть Хаб ChattyWidgets");
        if (MenuCloseWidget != null) MenuCloseWidget.Header = loc.T("Menu.Close", "Скрыть виджет");
        if (AnchorDot != null) AnchorDot.ToolTip = loc.T("Anchor.ToolTip", "Точка управления ChattyWidgets (ПКМ — меню, ЛКМ — переместить)");
        if (ResizeGripHandle != null) ResizeGripHandle.ToolTip = loc.T("ResizeGrip.ToolTip", "Потяните, чтобы изменить размер виджета");
        if (TxtLoadingTitle != null) TxtLoadingTitle.Text = loc.T("Loading.Text", "✦ Загрузка виджета...");
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _overlayDotWindow?.Close();
        _overlayDotWindow = null;
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private static void LogW(string msg)
    {
        try
        {
            string logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChattyWidgets");
            System.IO.File.AppendAllText(System.IO.Path.Combine(logDir, "app.log"), $"[{DateTime.Now:HH:mm:ss.fff}] [WW] {msg}\n");
        }
        catch { }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        LogW($"OnSourceInitialized start for {InstanceId}");
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        LogW($"OnSourceInitialized hwnd={_hwnd} for {InstanceId}");

        var source = HwndSource.FromHwnd(_hwnd);
        if (source != null)
        {
            source.AddHook(WndProc);
            if (source.CompositionTarget != null)
            {
                source.CompositionTarget.BackgroundColor = System.Windows.Media.Color.FromArgb(0, 0, 0, 0);
            }
        }
        LogW($"OnSourceInitialized calling ApplyLayer for {InstanceId}");
        DesktopHostManager.ApplyLayer(_hwnd, Settings.Layer);
        LogW($"OnSourceInitialized calling UpdateWindowShape for {InstanceId}");
        UpdateWindowShape();
        LogW($"OnSourceInitialized done for {InstanceId}");
    }

    public void UpdateWindowShape()
    {
        if (_hwnd == IntPtr.Zero) return;
        try
        {
            // Do not round HWND edges - WPF handles beautiful anti-aliased rounded borders via DirectX
            int cornerPref = Win32Native.DWMWCP_DONOTROUND;
            Win32Native.DwmSetWindowAttribute(_hwnd, Win32Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

            // Suppress Windows 11 DWM frame border completely (DWMWA_COLOR_NONE = 0xFFFFFFFE)
            int noneColor = unchecked((int)0xFFFFFFFE);
            Win32Native.DwmSetWindowAttribute(_hwnd, 34 /* DWMWA_BORDER_COLOR */, ref noneColor, sizeof(int));

            // Disable DWM non-client rendering (completely eliminates rectangular DWM window shadows)
            int ncrpDisabled = 1; // DWMNCRP_DISABLED
            Win32Native.DwmSetWindowAttribute(_hwnd, 2 /* DWMWA_NCRENDERING_POLICY */, ref ncrpDisabled, sizeof(int));

            DesktopHostManager.DisableAeroSnap(_hwnd);
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

    private bool _isDragMoving = false;
    private bool _isFreeResizeEnabled = false;
    private const int RESIZE_BORDER_THICKNESS = 10;

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                if (WidgetContentHost.Content is FrameworkElement fe && fe.DataContext is MediaFrameWidgetViewModel photoVm)
                {
                    photoVm.HandleDropFiles(files);
                    e.Handled = true;
                    return;
                }
            }
        }
    }

    private IntPtr CheckResizeHitTest(Point windowPt)
    {
        if (Settings.IsLocked)
            return IntPtr.Zero;

        bool isFree = Settings.IsFreeResize || _isFreeResizeEnabled || (WidgetManager.Instance?.Settings?.AlwaysResizable ?? true);
        if (!isFree)
            return IntPtr.Zero;

        double w = ActualWidth > 0 ? ActualWidth : Width;
        double h = ActualHeight > 0 ? ActualHeight : Height;

        // Skip anchor dot area (top right 36x36) so it can be clicked
        if (windowPt.X >= (w - 36) && windowPt.Y <= 36)
            return IntPtr.Zero;

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

        else if (msg == Win32Native.WM_RBUTTONUP)
        {
            ShowWidgetContextMenu();
            handled = true;
            return IntPtr.Zero;
        }
        else if (msg == Win32Native.WM_WINDOWPOSCHANGING && _isDragMoving)
        {
            _overlayDotWindow?.UpdatePositionDirect();
            try
            {
                var wp = Marshal.PtrToStructure<Win32Native.WINDOWPOS>(lParam);
                int targetW = (int)Math.Round(Settings.Width > 0 ? Settings.Width : (Width > 0 ? Width : 200));
                int targetH = (int)Math.Round(Settings.Height > 0 ? Settings.Height : (Height > 0 ? Height : 200));
                if (targetW > 0 && targetH > 0)
                {
                    if ((wp.flags & Win32Native.SWP_NOSIZE) == 0 && (wp.cx != targetW || wp.cy != targetH))
                    {
                        wp.cx = targetW;
                        wp.cy = targetH;
                        wp.flags |= Win32Native.SWP_NOSIZE;
                        Marshal.StructureToPtr(wp, lParam, false);
                    }
                }
            }
            catch { }
        }
        else if (msg == 0x0047 /* WM_WINDOWPOSCHANGED */)
        {
            _overlayDotWindow?.UpdatePositionDirect();
        }
        else if (msg == Win32Native.WM_GETMINMAXINFO)
        {
            try
            {
                var mmi = Marshal.PtrToStructure<Win32Native.MINMAXINFO>(lParam);
                int targetW = (int)Math.Round(Settings.Width > 0 ? Settings.Width : (Width > 0 ? Width : 200));
                int targetH = (int)Math.Round(Settings.Height > 0 ? Settings.Height : (Height > 0 ? Height : 200));
                if (targetW > 0 && targetH > 0)
                {
                    mmi.ptMaxSize.X = targetW;
                    mmi.ptMaxSize.Y = targetH;
                    mmi.ptMaxPosition.X = (int)Math.Round(Left);
                    mmi.ptMaxPosition.Y = (int)Math.Round(Top);
                    Marshal.StructureToPtr(mmi, lParam, false);
                }
            }
            catch { }
        }
        else if (msg == Win32Native.WM_SYSCOMMAND)
        {
            int command = (int)(wParam.ToInt64() & 0xFFF0);
            if (command == Win32Native.SC_MINIMIZE || command == Win32Native.SC_MAXIMIZE)
            {
                // Prevent Windows / Explorer from minimizing or maximizing widgets on Show Desktop / Snap
                handled = true;
                return IntPtr.Zero;
            }
        }
        return IntPtr.Zero;
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState != WindowState.Normal)
        {
            WindowState = WindowState.Normal;
            if (_hwnd != IntPtr.Zero)
            {
                DesktopHostManager.ApplyLayer(_hwnd, Settings.Layer);
            }
            EnsureWidgetSize();
        }
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
        ApplyTheme();
        UpdateWindowShape();
    }

    private async Task LoadWebWidget()
    {
        try
        {
            // Web widgets host an HWND, so hide the WPF visual AnchorContainer and use native AnchorDotOverlayWindow
            AnchorContainer.Visibility = Visibility.Collapsed;
            AnchorDot.Visibility = Visibility.Collapsed;
            ResizeGripHandle.Visibility = Visibility.Collapsed;

            if (_overlayDotWindow == null)
            {
                _overlayDotWindow = new AnchorDotOverlayWindow(this);
            }

            TxtLoadingTitle.Text = $"✦ Загрузка {Manifest.Name}...";
            LoadingIndicator.Visibility = Visibility.Visible;

            System.Drawing.Color bgColor = System.Drawing.Color.FromArgb(0, 0, 0, 0);
            if (!string.IsNullOrWhiteSpace(Manifest.BackgroundColor) && !Manifest.BackgroundColor.Equals("transparent", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    bgColor = System.Drawing.ColorTranslator.FromHtml(Manifest.BackgroundColor);
                }
                catch { }
            }

            _webView = new WebView2
            {
                DefaultBackgroundColor = bgColor
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

            if (!string.IsNullOrWhiteSpace(Manifest.UserAgent))
            {
                if (Manifest.UserAgent.Equals("mobile", StringComparison.OrdinalIgnoreCase))
                {
                    _webView.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Mobile Safari/537.36";
                }
                else if (!Manifest.UserAgent.Equals("desktop", StringComparison.OrdinalIgnoreCase))
                {
                    _webView.CoreWebView2.Settings.UserAgent = Manifest.UserAgent;
                }
            }

            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = WidgetManager.Instance.Settings.EnableDevMode;
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
                getOpacityFunc: () => Settings.Opacity,
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
                reloadAction: () => ReloadWidget(),
                hoverAction: (h) => _overlayDotWindow?.NotifyWidgetHover(h)
            );

            _webView.CoreWebView2.AddHostObjectToScript("chatty", bridge);

            string initScript = @"
                window.chatty = chrome.webview.hostObjects.sync.chatty;

                // Sync background opacity (never whole widget opacity)
                try {
                    var op = window.chatty.getOpacity();
                    if (op && op > 0) {
                        document.documentElement.style.removeProperty('opacity');
                        document.documentElement.style.setProperty('--chatty-bg-opacity', op);
                        document.documentElement.style.setProperty('--chatty-opacity', op);
                    }
                } catch(ex) {}

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

                        // Allow drag on dot, top bar, header, clock container, svg, or general widget card
                        var isHeaderOrDot = target.closest && target.closest('.top-bar, .header, .chatty-anchor-dot, #chatty-control-dot, .widget-card, .crypto-card, .player-card, .note-card, .clock-container, svg, [data-chatty-drag]');
                        if (isHeaderOrDot) {
                            try {
                                window.chatty.dragMove();
                            } catch(ex) {
                                beginDrag(e);
                            }
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

                // Mouse activity tracking to notify native engine overlay dot
                window.addEventListener('mousemove', function() {
                    try { window.chatty.notifyHover(true); } catch(ex) {}
                });
                window.addEventListener('mouseenter', function() {
                    try { window.chatty.notifyHover(true); } catch(ex) {}
                });
                window.addEventListener('mouseleave', function(e) {
                    if (!e.relatedTarget && !e.toElement) {
                        try { window.chatty.notifyHover(false); } catch(ex) {}
                    }
                });
            ";
            await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(initScript);

            if (!string.IsNullOrWhiteSpace(Manifest.InjectedScript))
            {
                string scriptPath = Path.Combine(WidgetDirectory, Manifest.InjectedScript);
                if (File.Exists(scriptPath))
                {
                    try
                    {
                        string scriptContent = await File.ReadAllTextAsync(scriptPath);
                        await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(scriptContent);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WW] Failed to load InjectedScript '{Manifest.InjectedScript}': {ex.Message}");
                    }
                }
            }

            _webView.NavigationCompleted += (s, e) =>
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
                string opStr = Settings.Opacity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                _ = _webView.CoreWebView2.ExecuteScriptAsync($@"
                    try {{
                        document.documentElement.style.removeProperty('opacity');
                        document.documentElement.style.setProperty('--chatty-bg-opacity', '{opStr}');
                        document.documentElement.style.setProperty('--chatty-opacity', '{opStr}');
                    }} catch(ex) {{}}
                ");
                ApplyBackgroundOpacity(Settings.Opacity);
                AttachClickThroughSubclass();
                if (Settings.IsClickThrough)
                {
                    ApplyClickThroughDomState(true);
                }
                ApplyAutoHideDomState(true);
            };

            string targetUrl = Manifest.Entry;
            if (targetUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                targetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                _webView.CoreWebView2.Navigate(targetUrl);
            }
            else
            {
                string entryPath = Path.Combine(WidgetDirectory, targetUrl);
                if (File.Exists(entryPath))
                {
                    _webView.CoreWebView2.Navigate(new Uri(entryPath).AbsoluteUri);
                }
                else
                {
                    ShowErrorMessage($"Входной файл не найден: {targetUrl}");
                }
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
                        fe.DataContext = NativeWidgetContextProvider.GetContextFor(BaseWidgetId, InstanceId, fe);
                    }

                    // Ensure root border has no DropShadowEffect to prevent straight corner artifacts
                    if (element is Border rootB) rootB.Effect = null;
                    else if (element is FrameworkElement feB) FindVisualChild<Border>(feB)?.SetValue(UIElement.EffectProperty, null);

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

    private void EnsureWidgetSize()
    {
        if (WindowState != WindowState.Normal)
        {
            WindowState = WindowState.Normal;
        }

        double targetW = Settings.Width > 0 ? Settings.Width : (Width > 0 ? Width : 200);
        double targetH = Settings.Height > 0 ? Settings.Height : (Height > 0 ? Height : 200);

        if (Math.Abs(Width - targetW) > 1 || Math.Abs(Height - targetH) > 1)
        {
            Width = targetW;
            Height = targetH;
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && !Settings.IsClickThrough && !Settings.IsLocked)
        {
            Point pt = e.GetPosition(this);
            if (CheckResizeHitTest(pt) != IntPtr.Zero)
            {
                return;
            }

            try
            {
                _isDragMoving = true;
                DragMove();
            }
            catch { }
            finally
            {
                _isDragMoving = false;
                EnsureWidgetSize();
                SnapToScreen();
            }
        }
    }

    private void OnAnchorDotLeftDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && !Settings.IsLocked)
        {
            try
            {
                _isDragMoving = true;
                DragMove();
            }
            catch { }
            finally
            {
                _isDragMoving = false;
                EnsureWidgetSize();
                SnapToScreen();
                e.Handled = true;
            }
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
        if (_isDragMoving) return;

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
            try
            {
                _isDragMoving = true;
                Win32Native.ReleaseCapture();
                Win32Native.SendMessage(_hwnd, (uint)Win32Native.WM_NCLBUTTONDOWN, (IntPtr)Win32Native.HTCAPTION, IntPtr.Zero);
            }
            catch { }
            finally
            {
                _isDragMoving = false;
                EnsureWidgetSize();
                SnapToScreen();
            }
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
        bool snap = WidgetManager.Instance.Settings.SnapToGrid;
        var pt = DesktopHostManager.SnapToScreenAndGrid(Left, Top, Width, Height, snapDistance: snap ? 15 : 0, gridStep: snap ? 10 : 1);
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
        if (_overlayDotWindow != null)
        {
            _overlayDotWindow.Topmost = this.Topmost;
            _overlayDotWindow.UpdatePositionDirect();
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

        bool isHovered = IsMouseOver || (AnchorContainer?.IsMouseOver ?? false) || WidgetContextMenu.IsOpen;
        AnchorDot.BeginAnimation(UIElement.OpacityProperty, null);
        AnchorDot.Opacity = isHovered ? 1.0 : 0.0;

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
        if (_webView?.CoreWebView2 != null)
        {
            string opStr = Settings.Opacity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            string script = $@"
                try {{
                    document.documentElement.style.removeProperty('opacity');
                    document.documentElement.style.setProperty('--chatty-bg-opacity', '{opStr}');
                    document.documentElement.style.setProperty('--chatty-opacity', '{opStr}');
                    const isLight = document.documentElement.classList.contains('light-theme') || (document.body && document.body.classList.contains('light-theme'));
                    const cards = document.querySelectorAll('.widget-card, .player-card, .note-card, .crypto-card, .card-bg');
                    cards.forEach(c => {{
                        if (c.classList.contains('clock-container')) return;
                        c.style.backgroundColor = (isLight ? 'rgba(248, 250, 252, ' : 'rgba(18, 22, 34, ') + '{opStr}' + ')';
                    }});
                    const dialFaces = document.querySelectorAll('.dial-glass-face, .clock-dial, .dial-bg');
                    dialFaces.forEach(df => {{
                        df.style.fillOpacity = '{opStr}';
                        df.setAttribute('fill-opacity', '{opStr}');
                    }});
                    const gradStops = document.querySelectorAll('#dial-gradient stop, radialGradient stop');
                    gradStops.forEach(s => {{
                        s.setAttribute('stop-opacity', '{opStr}');
                    }});
                }} catch(e) {{}}
            ";
            _ = _webView.CoreWebView2.ExecuteScriptAsync(script);
        }
        if (WidgetContentHost.Content is FrameworkElement hostFe && hostFe.DataContext is NativeViewModelBase nativeVm)
        {
            nativeVm.ApplyOpacity(Settings.Opacity);
        }
        SettingsChanged?.Invoke(this);
    }

    private void ApplyBackgroundOpacity(double opacity)
    {
        ApplyTheme();
    }

    public void ApplyTheme(string? themeOverride = null)
    {
        string activeTheme = themeOverride ?? Settings.Theme;
        if (activeTheme == "auto" || string.IsNullOrWhiteSpace(activeTheme))
        {
            activeTheme = WidgetManager.Instance.Settings.Theme;
        }
        bool isLight = activeTheme.Equals("light", StringComparison.OrdinalIgnoreCase);

        UpdateMenuChecks();

        // 1. Locate root border (for native XAML widgets only)
        Border? rootBorder = null;
        bool isWebWidget = Manifest.Type.Equals("web", StringComparison.OrdinalIgnoreCase) || _webView != null;
        if (!isWebWidget)
        {
            if (WidgetContentHost.Content is Border b) rootBorder = b;
            else if (WidgetContentHost.Content is UserControl uc && uc.Content is Border ucb) rootBorder = ucb;
            else if (WidgetContentHost.Content is FrameworkElement elemFe)
            {
                rootBorder = FindVisualChild<Border>(elemFe);
            }

            if (rootBorder != null)
            {
                // Strictly eliminate any DropShadowEffect on the root border to completely eliminate transparent straight corner artifacts
                rootBorder.Effect = null;

                if (isLight)
                {
                    byte alpha = (byte)Math.Clamp((int)(Settings.Opacity * 245), 30, 248);
                    rootBorder.Background = new SolidColorBrush(Color.FromArgb(alpha, 248, 250, 252));
                    rootBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(45, 0, 0, 0));
                }
                else
                {
                    byte alpha = (byte)Math.Clamp((int)(Settings.Opacity * 225), 30, 225);
                    rootBorder.Background = new SolidColorBrush(Color.FromArgb(alpha, 18, 22, 34));
                    rootBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                }
            }
        }
        else
        {
            // For Web widgets: the host container and WebView2 MUST remain 100% transparent.
            // Any background or shape (e.g. circular dial, rounded card) is defined exclusively by HTML/CSS.
            if (_webView != null)
            {
                var wvBorder = FindVisualChild<Border>(_webView);
                if (wvBorder != null)
                {
                    wvBorder.Background = Brushes.Transparent;
                    wvBorder.BorderBrush = Brushes.Transparent;
                    wvBorder.BorderThickness = new Thickness(0);
                    wvBorder.Effect = null;
                }
            }
        }

        // 2. If SystemGaugesView, apply theme to it directly
        if (WidgetContentHost.Content is ChattyWidgets.UI.Views.SystemGaugesView gaugesView)
        {
            gaugesView.ApplyTheme(isLight);
        }

        if (WidgetContentHost.Content is FrameworkElement hostElem && hostElem.DataContext is NativeViewModelBase nativeViewModel)
        {
            nativeViewModel.ApplyTheme(isLight);
            nativeViewModel.ApplyOpacity(Settings.Opacity);
        }

        // 3. Traverse visual tree for all native elements (TextBlocks, Buttons, Shapes, inner Borders)
        if (!isWebWidget && WidgetContentHost.Content is DependencyObject depObj)
        {
            ApplyThemeToVisualTree(depObj, isLight, rootBorder);
        }

        // 4. For Web widgets
        if (_webView?.CoreWebView2 != null)
        {
            string alphaStr = Settings.Opacity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            string js = $@"
                try {{
                    document.documentElement.style.removeProperty('opacity');
                    document.documentElement.style.setProperty('--chatty-bg-opacity', '{alphaStr}');
                    document.documentElement.style.setProperty('--chatty-opacity', '{alphaStr}');
                    document.documentElement.classList.toggle('light-theme', {isLight.ToString().ToLower()});
                    document.documentElement.classList.toggle('dark-theme', {(!isLight).ToString().ToLower()});
                    if (document.body) {{
                        document.body.classList.toggle('light-theme', {isLight.ToString().ToLower()});
                        document.body.classList.toggle('dark-theme', {(!isLight).ToString().ToLower()});
                    }}
                    const cards = document.querySelectorAll('.widget-card, .player-card, .note-card, .crypto-card, .card-bg');
                    cards.forEach(c => {{
                        if (c.classList.contains('clock-container')) return;
                        c.style.backgroundColor = '{(isLight ? "rgba(248, 250, 252, " : "rgba(18, 22, 34, ")}' + {alphaStr} + ')';
                    }});
                    const dialFaces = document.querySelectorAll('.dial-glass-face, .clock-dial, .dial-bg');
                    dialFaces.forEach(df => {{
                        df.style.fillOpacity = '{alphaStr}';
                        df.setAttribute('fill-opacity', '{alphaStr}');
                    }});
                    const gradStops = document.querySelectorAll('#dial-gradient stop, radialGradient stop');
                    gradStops.forEach(s => {{
                        s.setAttribute('stop-opacity', '{alphaStr}');
                    }});
                }} catch(e) {{}}
            ";
            _webView.CoreWebView2.ExecuteScriptAsync(js);
        }
    }

    private void ApplyThemeToVisualTree(DependencyObject? node, bool isLight, Border? rootBorder)
    {
        if (node == null) return;

        int childrenCount = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < childrenCount; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);

            if (child is TextBlock tb)
            {
                var fgBinding = System.Windows.Data.BindingOperations.GetBindingExpression(tb, TextBlock.ForegroundProperty);
                if (fgBinding == null)
                {
                    if (isLight)
                    {
                        if (tb.Tag == null) tb.Tag = tb.Foreground;
                        if (tb.Foreground is SolidColorBrush scb && scb.Color != Colors.Transparent)
                        {
                            int r = scb.Color.R, g = scb.Color.G, b = scb.Color.B;
                            int max = Math.Max(r, Math.Max(g, b));
                            int min = Math.Min(r, Math.Min(g, b));
                            bool isAccent = (max - min) > 70;
                            if (!isAccent)
                            {
                                int brightness = (r * 299 + g * 587 + b * 114) / 1000;
                                if (brightness >= 180)
                                {
                                    tb.Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)); // Dark primary / dark icon
                                }
                                else if (brightness >= 100)
                                {
                                    tb.Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)); // Dark secondary
                                }
                            }
                        }
                    }
                    else
                    {
                        if (tb.Tag is Brush origBrush)
                        {
                            tb.Foreground = origBrush;
                        }
                        else if (tb.Foreground is SolidColorBrush scb && scb.Color.R < 80 && scb.Color.G < 80 && scb.Color.B < 80)
                        {
                            tb.Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                        }
                        else if (tb.Foreground == null || tb.Foreground == System.Windows.SystemColors.ControlTextBrush)
                        {
                            tb.Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                        }
                    }
                }
            }
            else if (child is ChattyWidgets.UI.Controls.MarqueeText mt)
            {
                var fgBinding = System.Windows.Data.BindingOperations.GetBindingExpression(mt, ChattyWidgets.UI.Controls.MarqueeText.ForegroundProperty);
                if (fgBinding == null)
                {
                    if (isLight)
                    {
                        if (mt.Tag == null) mt.Tag = mt.Foreground;
                        if (mt.Foreground is SolidColorBrush scb && scb.Color != Colors.Transparent)
                        {
                            int r = scb.Color.R, g = scb.Color.G, b = scb.Color.B;
                            int max = Math.Max(r, Math.Max(g, b));
                            int min = Math.Min(r, Math.Min(g, b));
                            bool isAccent = (max - min) > 70;
                            if (!isAccent)
                            {
                                int brightness = (r * 299 + g * 587 + b * 114) / 1000;
                                if (brightness >= 180)
                                {
                                    mt.Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42));
                                }
                                else if (brightness >= 100)
                                {
                                    mt.Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105));
                                }
                            }
                        }
                    }
                    else
                    {
                        if (mt.Tag is Brush origBrush)
                        {
                            mt.Foreground = origBrush;
                        }
                        else if (mt.Foreground is SolidColorBrush scb && scb.Color.R < 80 && scb.Color.G < 80 && scb.Color.B < 80)
                        {
                            mt.Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                        }
                    }
                }
            }
            else if (child is System.Windows.Controls.TextBox txtBox)
            {
                var fgBinding = System.Windows.Data.BindingOperations.GetBindingExpression(txtBox, System.Windows.Controls.TextBox.ForegroundProperty);
                var bgBinding = System.Windows.Data.BindingOperations.GetBindingExpression(txtBox, System.Windows.Controls.TextBox.BackgroundProperty);
                if (isLight)
                {
                    if (txtBox.Tag == null) txtBox.Tag = (txtBox.Foreground, txtBox.Background, txtBox.BorderBrush, txtBox.CaretBrush);
                    if (fgBinding == null)
                    {
                        txtBox.Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42));
                        txtBox.CaretBrush = new SolidColorBrush(Color.FromRgb(15, 23, 42));
                    }
                    if (bgBinding == null && txtBox.Background is SolidColorBrush bgScb && bgScb.Color != Colors.Transparent)
                    {
                        if (bgScb.Color.R > 200 && bgScb.Color.G > 200 && bgScb.Color.B > 200)
                        {
                            txtBox.Background = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0));
                        }
                    }
                }
                else
                {
                    if (txtBox.Tag is ValueTuple<Brush, Brush, Brush, Brush> tuple)
                    {
                        if (fgBinding == null) txtBox.Foreground = tuple.Item1;
                        if (bgBinding == null) txtBox.Background = tuple.Item2;
                        txtBox.BorderBrush = tuple.Item3;
                        txtBox.CaretBrush = tuple.Item4;
                    }
                }
            }
            else if (child is Button btn)
            {
                var bgBinding = System.Windows.Data.BindingOperations.GetBindingExpression(btn, Button.BackgroundProperty);
                var fgBinding = System.Windows.Data.BindingOperations.GetBindingExpression(btn, Button.ForegroundProperty);

                if (isLight)
                {
                    if (btn.Tag == null) btn.Tag = (btn.Foreground, btn.Background, btn.BorderBrush);
                    if (fgBinding == null)
                    {
                        btn.Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)); // DARK ICONS!
                    }
                    if (bgBinding == null && btn.Background is SolidColorBrush bgScb && bgScb.Color != Colors.Transparent)
                    {
                        if (bgScb.Color.R > 200 && bgScb.Color.G > 200 && bgScb.Color.B > 200)
                        {
                            btn.Background = new SolidColorBrush(Color.FromArgb(Math.Min((byte)35, bgScb.Color.A), 0, 0, 0));
                        }
                    }
                    if (btn.BorderBrush is SolidColorBrush bbScb && bbScb.Color != Colors.Transparent)
                    {
                        btn.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
                    }
                }
                else
                {
                    if (btn.Tag is ValueTuple<Brush, Brush, Brush> tuple)
                    {
                        if (fgBinding == null) btn.Foreground = tuple.Item1;
                        if (bgBinding == null) btn.Background = tuple.Item2;
                        btn.BorderBrush = tuple.Item3;
                    }
                }
            }
            else if (child is Border innerB && innerB != rootBorder)
            {
                var bgBinding = System.Windows.Data.BindingOperations.GetBindingExpression(innerB, Border.BackgroundProperty);
                var bbBinding = System.Windows.Data.BindingOperations.GetBindingExpression(innerB, Border.BorderBrushProperty);

                if (isLight)
                {
                    if (innerB.Tag == null) innerB.Tag = (innerB.Background, innerB.BorderBrush);
                    if (bgBinding == null)
                    {
                        if (innerB.Background is SolidColorBrush bgScb && bgScb.Color != Colors.Transparent)
                        {
                            if (bgScb.Color.R > 200 && bgScb.Color.G > 200 && bgScb.Color.B > 200)
                            {
                                innerB.Background = new SolidColorBrush(Color.FromArgb(Math.Min((byte)35, bgScb.Color.A), 0, 0, 0));
                            }
                        }
                        else if (innerB.Background is LinearGradientBrush lgb)
                        {
                            bool isDarkGradient = false;
                            foreach (var stop in lgb.GradientStops)
                            {
                                int brightness = (stop.Color.R * 299 + stop.Color.G * 587 + stop.Color.B * 114) / 1000;
                                if (brightness < 80) isDarkGradient = true;
                            }
                            if (isDarkGradient)
                            {
                                var lightGrad = new LinearGradientBrush
                                {
                                    StartPoint = lgb.StartPoint,
                                    EndPoint = lgb.EndPoint
                                };
                                lightGrad.GradientStops.Add(new GradientStop(Color.FromArgb(28, 2, 132, 199), 0.0));
                                lightGrad.GradientStops.Add(new GradientStop(Color.FromArgb(40, 56, 189, 248), 1.0));
                                innerB.Background = lightGrad;
                            }
                        }
                    }
                    if (bbBinding == null && innerB.BorderBrush is SolidColorBrush bbScb && bbScb.Color != Colors.Transparent)
                    {
                        if (bbScb.Color.R > 200 && bbScb.Color.G > 200 && bbScb.Color.B > 200)
                        {
                            innerB.BorderBrush = new SolidColorBrush(Color.FromArgb(Math.Min((byte)35, bbScb.Color.A), 0, 0, 0));
                        }
                    }
                }
                else
                {
                    if (innerB.Tag is ValueTuple<Brush, Brush> orig)
                    {
                        if (bgBinding == null) innerB.Background = orig.Item1;
                        if (bbBinding == null) innerB.BorderBrush = orig.Item2;
                    }
                }
            }
            else if (child is System.Windows.Shapes.Shape shape)
            {
                if (isLight)
                {
                    if (shape.Tag == null) shape.Tag = (shape.Fill, shape.Stroke);
                    if (shape.Fill is SolidColorBrush f && f.Color.R > 200 && f.Color.G > 200 && f.Color.B > 200)
                        shape.Fill = new SolidColorBrush(Color.FromRgb(15, 23, 42));
                    if (shape.Stroke is SolidColorBrush s && s.Color.R > 200 && s.Color.G > 200 && s.Color.B > 200)
                        shape.Stroke = new SolidColorBrush(Color.FromRgb(15, 23, 42));
                }
                else
                {
                    if (shape.Tag is ValueTuple<Brush, Brush> orig)
                    {
                        shape.Fill = orig.Item1;
                        shape.Stroke = orig.Item2;
                    }
                }
            }

            ApplyThemeToVisualTree(child, isLight, rootBorder);
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed) return typed;
            var sub = FindVisualChild<T>(child);
            if (sub != null) return sub;
        }
        return null;
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
        if (MenuThemeAuto != null) MenuThemeAuto.IsChecked = Settings.Theme == "auto";
        if (MenuThemeDark != null) MenuThemeDark.IsChecked = Settings.Theme == "dark";
        if (MenuThemeLight != null) MenuThemeLight.IsChecked = Settings.Theme == "light";

        bool freeResizeActive = !Settings.IsLocked && (Settings.IsFreeResize || _isFreeResizeEnabled || (WidgetManager.Instance?.Settings?.AlwaysResizable ?? true));
        if (MenuFreeResizeToggle != null) MenuFreeResizeToggle.IsChecked = Settings.IsFreeResize || _isFreeResizeEnabled;
        if (ResizeGripHandle != null)
        {
            ResizeGripHandle.Visibility = (_webView == null && freeResizeActive) ? Visibility.Visible : Visibility.Collapsed;
        }
        if (MenuDevTools != null)
        {
            MenuDevTools.Visibility = (WidgetManager.Instance?.Settings?.EnableDevMode ?? true) ? Visibility.Visible : Visibility.Collapsed;
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
        if (AnchorDot != null && AnchorContainer.Visibility == Visibility.Visible)
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation(1.0, TimeSpan.FromMilliseconds(180));
            AnchorDot.BeginAnimation(UIElement.OpacityProperty, anim);
        }
    }

    private void OnAnchorContainerMouseLeave(object sender, MouseEventArgs e)
    {
        if (AnchorDot != null && AnchorContainer.Visibility == Visibility.Visible && !WidgetContextMenu.IsOpen && !IsMouseOver)
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation(0.0, TimeSpan.FromMilliseconds(220));
            AnchorDot.BeginAnimation(UIElement.OpacityProperty, anim);
        }
    }

    private void OnWindowMouseEnter(object sender, MouseEventArgs e)
    {
        if (AnchorDot != null && AnchorContainer.Visibility == Visibility.Visible)
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation(1.0, TimeSpan.FromMilliseconds(180));
            AnchorDot.BeginAnimation(UIElement.OpacityProperty, anim);
        }
    }

    private void OnWindowMouseLeave(object sender, MouseEventArgs e)
    {
        if (AnchorDot != null && AnchorContainer.Visibility == Visibility.Visible && !WidgetContextMenu.IsOpen && !(AnchorContainer?.IsMouseOver ?? false))
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation(0.0, TimeSpan.FromMilliseconds(220));
            AnchorDot.BeginAnimation(UIElement.OpacityProperty, anim);
        }
    }

    private void OnContextMenuOpened(object? sender, RoutedEventArgs e)
    {
        UpdateMenuChecks();

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

        if (AnchorDot != null && AnchorContainer.Visibility == Visibility.Visible)
        {
            AnchorDot.BeginAnimation(UIElement.OpacityProperty, null);
            AnchorDot.Opacity = 1.0;
        }
        ApplyMenuOpenDomState(true);
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

    private void OnOpenWidgetSettingsClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var vm = (WidgetContentHost.Content as FrameworkElement)?.DataContext;
            var settingsWin = new WidgetSettingsWindow(BaseWidgetId, InstanceId, vm, () =>
            {
                if (WidgetContentHost.Content is ChattyWidgets.UI.Views.SystemGaugesView gaugesView)
                {
                    gaugesView.ReloadConfig();
                }
                if (_webView?.CoreWebView2 != null)
                {
                    _ = _webView.CoreWebView2.ExecuteScriptAsync("window.dispatchEvent(new CustomEvent('chatty:settingsChanged'));");
                }
            });
            settingsWin.Show();
            settingsWin.Activate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WidgetWindow] Failed to open widget settings: {ex.Message}");
        }
    }

    private void OnOpenDevToolsClick(object sender, RoutedEventArgs e) => OpenDevTools();
    private void OnReloadWidgetClick(object sender, RoutedEventArgs e) => ReloadWidget();

    public void OpenDevTools()
    {
        if (!WidgetManager.Instance.Settings.EnableDevMode) return;
        Dispatcher.Invoke(() =>
        {
            if (_webView?.CoreWebView2 != null)
            {
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                _webView.CoreWebView2.OpenDevToolsWindow();
            }
        });
    }

    public void UpdateDevMode(bool enableDevMode)
    {
        Dispatcher.Invoke(() =>
        {
            if (MenuDevTools != null)
            {
                MenuDevTools.Visibility = enableDevMode ? Visibility.Visible : Visibility.Collapsed;
            }
            if (_webView?.CoreWebView2 != null)
            {
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = enableDevMode;
            }
        });
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == System.Windows.Input.Key.F12)
        {
            if (WidgetManager.Instance.Settings.EnableDevMode)
            {
                OpenDevTools();
            }
            e.Handled = true;
        }
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
        if (AnchorDot != null && AnchorContainer.Visibility == Visibility.Visible && !IsMouseOver && !(AnchorContainer?.IsMouseOver ?? false))
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation(0.0, TimeSpan.FromMilliseconds(220));
            AnchorDot.BeginAnimation(UIElement.OpacityProperty, anim);
        }
        ApplyMenuOpenDomState(false);
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
        _isFreeResizeEnabled = MenuFreeResizeToggle.IsChecked;
        Settings.IsFreeResize = _isFreeResizeEnabled;
        SettingsChanged?.Invoke(this);
        UpdateMenuChecks();
    }

    public void UpdateResizeState()
    {
        Dispatcher.Invoke(UpdateMenuChecks);
    }

    private void OnThemeAutoClick(object sender, RoutedEventArgs e)
    {
        Settings.Theme = "auto";
        ApplyTheme();
        SettingsChanged?.Invoke(this);
    }

    private void OnThemeDarkClick(object sender, RoutedEventArgs e)
    {
        Settings.Theme = "dark";
        ApplyTheme();
        SettingsChanged?.Invoke(this);
    }

    private void OnThemeLightClick(object sender, RoutedEventArgs e)
    {
        Settings.Theme = "light";
        ApplyTheme();
        SettingsChanged?.Invoke(this);
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
