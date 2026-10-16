using System.Windows;
using System.Windows.Interop;
using ChattyWidgets.Core.Win32;

namespace ChattyWidgets.Core.Services;

public class HotkeyService : IDisposable
{
    private static HotkeyService? _instance;
    public static HotkeyService Instance => _instance ??= new HotkeyService();

    public const int HOTKEY_TOGGLE_ALL = 9001;
    public const int HOTKEY_PEEK_MODE = 9002;

    private const uint VK_W = 0x57;
    private const uint VK_P = 0x50;

    private IntPtr _hwnd = IntPtr.Zero;
    private HwndSource? _source;
    private bool _isRegistered = false;

    public bool IsWidgetsHidden { get; private set; } = false;
    public bool IsPeekModeActive { get; private set; } = false;

    public event Action<bool>? WidgetsVisibilityChanged;
    public event Action<bool>? PeekModeChanged;

    private HotkeyService()
    {
    }

    public void Initialize(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _hwnd = helper.EnsureHandle();

        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(HwndHook);

        if (WidgetManager.Instance.Settings.EnableHotkeys)
        {
            RegisterHotkeys();
        }
    }

    public void RegisterHotkeys()
    {
        if (_hwnd == IntPtr.Zero || _isRegistered) return;

        // Win + Alt + W -> Toggle all widgets
        Win32Native.RegisterHotKey(_hwnd, HOTKEY_TOGGLE_ALL, Win32Native.MOD_WIN | Win32Native.MOD_ALT | Win32Native.MOD_NOREPEAT, VK_W);

        // Win + Alt + P -> Peek Mode (bring to top)
        Win32Native.RegisterHotKey(_hwnd, HOTKEY_PEEK_MODE, Win32Native.MOD_WIN | Win32Native.MOD_ALT | Win32Native.MOD_NOREPEAT, VK_P);

        _isRegistered = true;
    }

    public void UnregisterHotkeys()
    {
        if (_hwnd == IntPtr.Zero || !_isRegistered) return;

        Win32Native.UnregisterHotKey(_hwnd, HOTKEY_TOGGLE_ALL);
        Win32Native.UnregisterHotKey(_hwnd, HOTKEY_PEEK_MODE);

        _isRegistered = false;
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32Native.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == HOTKEY_TOGGLE_ALL)
            {
                ToggleWidgetsVisibility();
                handled = true;
            }
            else if (id == HOTKEY_PEEK_MODE)
            {
                TogglePeekMode();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void ToggleWidgetsVisibility()
    {
        IsWidgetsHidden = !IsWidgetsHidden;
        foreach (var window in WidgetManager.Instance.GetActiveWindows())
        {
            if (IsWidgetsHidden)
                window.Hide();
            else
                window.Show();
        }
        WidgetsVisibilityChanged?.Invoke(IsWidgetsHidden);
    }

    public void TogglePeekMode()
    {
        IsPeekModeActive = !IsPeekModeActive;
        foreach (var window in WidgetManager.Instance.GetActiveWindows())
        {
            var layer = IsPeekModeActive ? WidgetLayer.Top : window.Settings.Layer;
            DesktopHostManager.ApplyLayer(window.Hwnd, layer);
            if (IsPeekModeActive)
            {
                window.Topmost = true;
                window.Activate();
            }
            else
            {
                window.Topmost = (window.Settings.Layer == WidgetLayer.Top);
            }
        }
        PeekModeChanged?.Invoke(IsPeekModeActive);
    }

    public void Dispose()
    {
        UnregisterHotkeys();
        _source?.RemoveHook(HwndHook);
    }
}
