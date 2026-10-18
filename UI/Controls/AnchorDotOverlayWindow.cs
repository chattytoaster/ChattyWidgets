using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using ChattyWidgets.Core.Win32;

namespace ChattyWidgets.UI.Controls;

public class AnchorDotOverlayWindow : Window
{
    private readonly WidgetWindow _owner;
    private IntPtr _hwnd = IntPtr.Zero;
    private readonly Border _dotBorder;
    private readonly Grid _hitbox;
    private readonly DispatcherTimer _hoverCheckTimer;
    private double _currentOpacity = 0.0;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Win32Native.POINT lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out Win32Native.RECT lpRect);

    public AnchorDotOverlayWindow(WidgetWindow owner)
    {
        _owner = owner;

        Width = 36;
        Height = 36;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Focusable = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        Topmost = owner.Topmost;
        Owner = owner;

        UpdatePositionDirect();

        var root = new Grid
        {
            Width = 36,
            Height = 36,
            Background = Brushes.Transparent
        };

        // Hitbox in top right
        _hitbox = new Grid
        {
            Width = 28,
            Height = 28,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        _dotBorder = new Border
        {
            Width = 14,
            Height = 14,
            Margin = new Thickness(0, 10, 10, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)), // #3B82F6 Vibrant Blue
            CornerRadius = new CornerRadius(7),
            BorderBrush = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255)),
            BorderThickness = new Thickness(1.5),
            Opacity = 0.0,
            ToolTip = "Точка управления ChattyWidgets (ПКМ — меню, ЛКМ — переместить)",
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 0,
                Color = Color.FromRgb(96, 165, 250),
                Opacity = 0.95
            },
            Child = new Ellipse
            {
                Width = 4,
                Height = 4,
                Fill = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        _hitbox.Children.Add(_dotBorder);
        root.Children.Add(_hitbox);
        Content = root;

        _hitbox.MouseLeftButtonDown += (s, e) =>
        {
            e.Handled = true;
            _owner.StartDragMove();
        };

        _hitbox.MouseRightButtonUp += (s, e) =>
        {
            e.Handled = true;
            _owner.ShowWidgetContextMenu();
        };

        // Synchronize owner events
        _owner.LocationChanged += (s, e) => UpdatePositionDirect();
        _owner.SizeChanged += (s, e) => UpdatePositionDirect();
        _owner.IsVisibleChanged += (s, e) =>
        {
            Visibility = _owner.IsVisible ? Visibility.Visible : Visibility.Collapsed;
            if (Visibility == Visibility.Visible) UpdatePositionDirect();
        };
        _owner.Closed += (s, e) =>
        {
            _hoverCheckTimer?.Stop();
            try { Close(); } catch { }
        };

        Loaded += (s, e) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            UpdatePositionDirect();
        };

        // Win32 Hardware cursor hover check (100% reliable across any webview/transparency/iframes)
        _hoverCheckTimer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(40)
        };
        _hoverCheckTimer.Tick += (s, e) => CheckCursorHover();
        _hoverCheckTimer.Start();

        // Show window immediately
        Show();
    }

    private void CheckCursorHover()
    {
        if (_owner.Hwnd == IntPtr.Zero || !_owner.IsVisible)
        {
            SetDotVisible(false);
            return;
        }

        if (GetCursorPos(out var curPos) && GetWindowRect(_owner.Hwnd, out var ownerRect))
        {
            bool isOverWidget = curPos.X >= ownerRect.Left && curPos.X <= ownerRect.Right &&
                                curPos.Y >= ownerRect.Top && curPos.Y <= ownerRect.Bottom;

            bool isOverDot = false;
            if (_hwnd != IntPtr.Zero && GetWindowRect(_hwnd, out var dotRect))
            {
                isOverDot = curPos.X >= dotRect.Left && curPos.X <= dotRect.Right &&
                            curPos.Y >= dotRect.Top && curPos.Y <= dotRect.Bottom;
            }

            bool menuOpen = _owner.WidgetContextMenu?.IsOpen ?? false;
            SetDotVisible(isOverWidget || isOverDot || menuOpen);
        }
    }

    public void NotifyWidgetHover(bool hovered)
    {
        SetDotVisible(hovered);
    }

    private void SetDotVisible(bool visible)
    {
        double target = visible ? 1.0 : 0.0;
        if (Math.Abs(_currentOpacity - target) < 0.01) return;
        _currentOpacity = target;

        var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(target > 0 ? 140 : 200))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        _dotBorder.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    public void UpdatePositionDirect()
    {
        Left = _owner.Left + _owner.Width - Width;
        Top = _owner.Top;
    }
}
