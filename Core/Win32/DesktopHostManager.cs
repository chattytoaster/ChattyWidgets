using System.Windows;

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
    /// Configures window for desktop layering.
    /// </summary>
    public static void ApplyLayer(IntPtr hwnd, WidgetLayer layer)
    {
        if (hwnd == IntPtr.Zero) return;

        MakeToolWindow(hwnd);

        switch (layer)
        {
            case WidgetLayer.Desktop:
                IntPtr workerW = GetWorkerWHandle();
                if (workerW != IntPtr.Zero)
                {
                    Win32Native.SetParent(hwnd, workerW);
                }
                else
                {
                    Win32Native.SetWindowPos(hwnd, Win32Native.HWND_BOTTOM, 0, 0, 0, 0,
                        Win32Native.SWP_NOMOVE | Win32Native.SWP_NOSIZE | Win32Native.SWP_NOACTIVATE);
                }
                break;

            case WidgetLayer.Normal:
                Win32Native.SetParent(hwnd, IntPtr.Zero);
                Win32Native.SetWindowPos(hwnd, Win32Native.HWND_NOTOPMOST, 0, 0, 0, 0,
                    Win32Native.SWP_NOMOVE | Win32Native.SWP_NOSIZE | Win32Native.SWP_NOACTIVATE);
                break;

            case WidgetLayer.Top:
                Win32Native.SetParent(hwnd, IntPtr.Zero);
                Win32Native.SetWindowPos(hwnd, Win32Native.HWND_TOPMOST, 0, 0, 0, 0,
                    Win32Native.SWP_NOMOVE | Win32Native.SWP_NOSIZE | Win32Native.SWP_NOACTIVATE);
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
    /// Snaps a position to screen borders and grid.
    /// </summary>
    public static Point SnapToScreenAndGrid(double left, double top, double width, double height, double snapDistance = 15, int gridStep = 10)
    {
        double screenWidth = SystemParameters.PrimaryScreenWidth;
        double screenHeight = SystemParameters.PrimaryScreenHeight;

        // Screen border snapping
        if (Math.Abs(left) < snapDistance) left = 0;
        if (Math.Abs(top) < snapDistance) top = 0;
        if (Math.Abs(screenWidth - (left + width)) < snapDistance) left = screenWidth - width;
        if (Math.Abs(screenHeight - (top + height)) < snapDistance) top = screenHeight - height;

        // Grid snapping
        if (gridStep > 1)
        {
            left = Math.Round(left / gridStep) * gridStep;
            top = Math.Round(top / gridStep) * gridStep;
        }

        return new Point(left, top);
    }
}
