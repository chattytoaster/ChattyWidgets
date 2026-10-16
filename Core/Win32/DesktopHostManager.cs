using System.Windows;
using ChattyWidgets.Core.Services;

namespace ChattyWidgets.Core.Win32;

public enum WidgetLayer
{
    Desktop, // Pinned to desktop behind all windows
    Normal,  // Floating window on desktop
    Top      // Always on top of all windows
}

public class DesktopHostManager
{
    private static IntPtr _workerWHandle = IntPtr.Zero;

    /// <summary>
    /// Finds or creates the WorkerW handle behind desktop icons (Rainmeter/Wallpaper Engine technique).
    /// </summary>
    public static IntPtr GetWorkerWHandle()
    {
        if (_workerWHandle != IntPtr.Zero)
            return _workerWHandle;

        IntPtr progman = Win32Native.FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
            return IntPtr.Zero;

        IntPtr result = IntPtr.Zero;
        // Send 0x052C to Progman. This spawns a WorkerW behind desktop icons.
        Win32Native.SendMessageTimeout(
            progman,
            Win32Native.WM_SPAWN_WORKER,
            new IntPtr(0x0000000D),
            new IntPtr(0x00000001),
            Win32Native.SMTO_NORMAL,
            1000,
            out result);

        IntPtr workerW = IntPtr.Zero;
        Win32Native.EnumWindows((tophandle, topparamhandle) =>
        {
            IntPtr p = Win32Native.FindWindowEx(tophandle, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (p != IntPtr.Zero)
            {
                // The WorkerW immediately following the one with SHELLDLL_DefView is the wallpaper surface
                workerW = Win32Native.FindWindowEx(IntPtr.Zero, tophandle, "WorkerW", null);
            }
            return true;
        }, IntPtr.Zero);

        _workerWHandle = workerW != IntPtr.Zero ? workerW : progman;
        return _workerWHandle;
    }

    /// <summary>
    /// Attaches window to Windows Desktop (Progman) as owner so it never hides when other apps or Hub minimize.
    /// </summary>
    public static void AttachToDesktop(IntPtr hwnd)
    {
        try
        {
            IntPtr progman = Win32Native.FindWindow("Progman", null);
            if (progman != IntPtr.Zero)
            {
                Win32Native.SetWindowLongPtr(hwnd, Win32Native.GWL_HWNDPARENT, progman);
            }
        }
        catch { }
    }

    /// <summary>
    /// Configures window for desktop layering.
    /// </summary>
    public static void ApplyLayer(IntPtr hwnd, WidgetLayer layer)
    {
        if (hwnd == IntPtr.Zero) return;

        MakeToolWindow(hwnd);

        // Always ensure top-level window without owner interference
        Win32Native.SetParent(hwnd, IntPtr.Zero);

        switch (layer)
        {
            case WidgetLayer.Desktop:
                // Sits on desktop behind active applications, but stays in front of desktop wallpaper/icons and is 100% clickable
                Win32Native.SetWindowPos(hwnd, Win32Native.HWND_NOTOPMOST, 0, 0, 0, 0,
                    Win32Native.SWP_NOMOVE | Win32Native.SWP_NOSIZE | Win32Native.SWP_NOACTIVATE | Win32Native.SWP_SHOWWINDOW);
                break;

            case WidgetLayer.Normal:
                Win32Native.SetWindowPos(hwnd, Win32Native.HWND_NOTOPMOST, 0, 0, 0, 0,
                    Win32Native.SWP_NOMOVE | Win32Native.SWP_NOSIZE | Win32Native.SWP_SHOWWINDOW);
                break;

            case WidgetLayer.Top:
                Win32Native.SetWindowPos(hwnd, Win32Native.HWND_TOPMOST, 0, 0, 0, 0,
                    Win32Native.SWP_NOMOVE | Win32Native.SWP_NOSIZE | Win32Native.SWP_SHOWWINDOW);
                break;
        }
    }

    /// <summary>
    /// Makes window a tool window (hides from Alt+Tab and taskbar).
    /// </summary>
    public static void MakeToolWindow(IntPtr hwnd)
    {
        long exStyle = (long)Win32Native.GetWindowLongPtr(hwnd, Win32Native.GWL_EXSTYLE);
        exStyle |= Win32Native.WS_EX_TOOLWINDOW;
        Win32Native.SetWindowLongPtr(hwnd, Win32Native.GWL_EXSTYLE, (IntPtr)exStyle);
    }

    /// <summary>
    /// Toggles click-through mode (WS_EX_TRANSPARENT).
    /// </summary>
    public static void SetClickThrough(IntPtr hwnd, bool clickThrough)
    {
        if (hwnd == IntPtr.Zero) return;

        long exStyle = (long)Win32Native.GetWindowLongPtr(hwnd, Win32Native.GWL_EXSTYLE);
        if (clickThrough)
        {
            exStyle |= (Win32Native.WS_EX_TRANSPARENT | Win32Native.WS_EX_LAYERED);
        }
        else
        {
            exStyle &= ~Win32Native.WS_EX_TRANSPARENT;
        }
        Win32Native.SetWindowLongPtr(hwnd, Win32Native.GWL_EXSTYLE, (IntPtr)exStyle);
    }

    /// <summary>
    /// Clamps a window position strictly within monitor screen boundaries so widgets never go off-screen.
    /// </summary>
    public static Point ClampToScreen(double left, double top, double width, double height)
    {
        var monitors = ScreenHelper.GetMonitors();
        if (monitors.Count == 0) return new Point(left, top);

        double centerX = left + width / 2;
        double centerY = top + height / 2;

        var currentMon = monitors.FirstOrDefault(m =>
            centerX >= m.Left && centerX <= m.Left + m.Width &&
            centerY >= m.Top && centerY <= m.Top + m.Height);

        if (currentMon == null)
        {
            currentMon = monitors.OrderBy(m => {
                double dx = Math.Max(0, Math.Max(m.Left - centerX, centerX - (m.Left + m.Width)));
                double dy = Math.Max(0, Math.Max(m.Top - centerY, centerY - (m.Top + m.Height)));
                return dx * dx + dy * dy;
            }).FirstOrDefault() ?? monitors[0];
        }

        if (width <= currentMon.Width)
        {
            left = Math.Max(currentMon.Left, Math.Min(left, currentMon.Left + currentMon.Width - width));
        }
        else
        {
            left = currentMon.Left;
        }

        if (height <= currentMon.Height)
        {
            top = Math.Max(currentMon.Top, Math.Min(top, currentMon.Top + currentMon.Height - height));
        }
        else
        {
            top = currentMon.Top;
        }

        return new Point(left, top);
    }

    /// <summary>
    /// Snaps a position to screen borders and grid, and strictly clamps within screen bounds.
    /// </summary>
    public static Point SnapToScreenAndGrid(double left, double top, double width, double height, double snapDistance = 15, int gridStep = 10)
    {
        var monitors = ScreenHelper.GetMonitors();
        var currentMon = monitors.FirstOrDefault(m =>
            left + width / 2 >= m.Left && left + width / 2 <= m.Left + m.Width &&
            top + height / 2 >= m.Top && top + height / 2 <= m.Top + m.Height)
            ?? monitors.FirstOrDefault(m => m.IsPrimary)
            ?? (monitors.Count > 0 ? monitors[0] : null);

        if (currentMon != null)
        {
            // Screen border snapping on the active monitor
            if (Math.Abs(left - currentMon.Left) < snapDistance) left = currentMon.Left;
            if (Math.Abs(top - currentMon.Top) < snapDistance) top = currentMon.Top;
            if (Math.Abs((currentMon.Left + currentMon.Width) - (left + width)) < snapDistance) left = currentMon.Left + currentMon.Width - width;
            if (Math.Abs((currentMon.Top + currentMon.Height) - (top + height)) < snapDistance) top = currentMon.Top + currentMon.Height - height;
        }

        // Grid snapping
        if (gridStep > 1)
        {
            left = Math.Round(left / gridStep) * gridStep;
            top = Math.Round(top / gridStep) * gridStep;
        }

        // Strictly clamp within the screen boundaries
        return ClampToScreen(left, top, width, height);
    }
}
