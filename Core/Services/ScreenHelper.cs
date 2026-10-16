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

        Win32Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref Win32Native.RECT lprcMonitor, IntPtr dwData) =>
        {
            var mi = new Win32Native.MONITORINFOEX { cbSize = Marshal.SizeOf(typeof(Win32Native.MONITORINFOEX)) };
            if (Win32Native.GetMonitorInfo(hMonitor, ref mi))
            {
                bool isPrimary = (mi.dwFlags & 1) != 0;
                list.Add(new MonitorInfo
                {
                    Index = index,
                    Name = $"Монитор {index + 1}",
                    Left = mi.rcMonitor.Left,
                    Top = mi.rcMonitor.Top,
                    Width = mi.rcMonitor.Right - mi.rcMonitor.Left,
                    Height = mi.rcMonitor.Bottom - mi.rcMonitor.Top,
                    IsPrimary = isPrimary
                });
                index++;
            }
            return true;
        }, IntPtr.Zero);

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
