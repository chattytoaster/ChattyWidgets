using System.Runtime.InteropServices;
using ChattyWidgets.Core.Win32;

namespace ChattyWidgets.Core.Services;

public class MonitorInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsPrimary { get; set; }

    public override string ToString() => $"{Name} ({Width}x{Height}){(IsPrimary ? " [Основной]" : "")}";
}

public static class ScreenHelper
{
    public static List<MonitorInfo> GetMonitors()
    {
        var list = new List<MonitorInfo>();
        int index = 0;

        // Obtain DPI scale to translate physical pixels to WPF DIPs
        double dpiScale = 1.0;
        double wpfPrimaryW = System.Windows.SystemParameters.PrimaryScreenWidth;

        var rawList = new List<(Win32Native.MONITORINFOEX mi, int idx)>();
        Win32Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref Win32Native.RECT lprcMonitor, IntPtr dwData) =>
        {
            var mi = new Win32Native.MONITORINFOEX { cbSize = Marshal.SizeOf(typeof(Win32Native.MONITORINFOEX)) };
            if (Win32Native.GetMonitorInfo(hMonitor, ref mi))
            {
                rawList.Add((mi, index++));
            }
            return true;
        }, IntPtr.Zero);

        var primaryRaw = rawList.FirstOrDefault(m => (m.mi.dwFlags & 1) != 0);
        if (primaryRaw.mi.cbSize > 0 && wpfPrimaryW > 0)
        {
            double physW = primaryRaw.mi.rcMonitor.Right - primaryRaw.mi.rcMonitor.Left;
            if (physW > 0) dpiScale = physW / wpfPrimaryW;
        }
        if (dpiScale <= 0.2) dpiScale = 1.0;

        foreach (var (mi, idx) in rawList)
        {
            bool isPrimary = (mi.dwFlags & 1) != 0;
            list.Add(new MonitorInfo
            {
                Index = idx,
                Name = $"Монитор {idx + 1}",
                Left = Math.Round(mi.rcMonitor.Left / dpiScale),
                Top = Math.Round(mi.rcMonitor.Top / dpiScale),
                Width = Math.Round((mi.rcMonitor.Right - mi.rcMonitor.Left) / dpiScale),
                Height = Math.Round((mi.rcMonitor.Bottom - mi.rcMonitor.Top) / dpiScale),
                IsPrimary = isPrimary
            });
        }


        if (list.Count == 0)
        {
            list.Add(new MonitorInfo
            {
                Index = 0,
                Name = "Монитор 1",
                Left = 0,
                Top = 0,
                Width = System.Windows.SystemParameters.PrimaryScreenWidth,
                Height = System.Windows.SystemParameters.PrimaryScreenHeight,
                IsPrimary = true
            });
        }

        return list;
    }

    public static MonitorInfo GetMonitor(int index)
    {
        var monitors = GetMonitors();
        if (index >= 0 && index < monitors.Count)
            return monitors[index];
        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
    }
}
