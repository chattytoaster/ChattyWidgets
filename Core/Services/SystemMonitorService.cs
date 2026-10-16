using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using ChattyWidgets.Core.Win32;

namespace ChattyWidgets.Core.Services;

public class SystemMetrics
{
    public double CpuPercent { get; set; }
    public double MemoryPercent { get; set; }
    public double TotalMemoryGb { get; set; }
    public double UsedMemoryGb { get; set; }
    public double FreeMemoryGb { get; set; }
    public double DiskPercent { get; set; }
    public double TotalDiskGb { get; set; }
    public double FreeDiskGb { get; set; }
    public double NetworkDownloadKbps { get; set; }
    public double NetworkUploadKbps { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class SystemMonitorService : IDisposable
{
    private static SystemMonitorService? _instance;
    public static SystemMonitorService Instance => _instance ??= new SystemMonitorService();

    private readonly System.Timers.Timer _timer;
    private long _prevIdleTime;
    private long _prevKernelTime;
    private long _prevUserTime;
    private long _prevBytesReceived;
    private long _prevBytesSent;
    private DateTime _prevTime;

    public SystemMetrics CurrentMetrics { get; private set; } = new();
    public event Action<SystemMetrics>? MetricsUpdated;

    private SystemMonitorService()
    {
        _timer = new System.Timers.Timer(1500); // Poll every 1.5s
        _timer.Elapsed += (s, e) => UpdateMetrics();
        _timer.AutoReset = true;

        InitCpuTimes();
        InitNetworkStats();
        _prevTime = DateTime.UtcNow;

        UpdateMetrics();
        _timer.Start();
    }

    private void InitCpuTimes()
    {
        if (Win32Native.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            _prevIdleTime = ToLong(idle);
            _prevKernelTime = ToLong(kernel);
            _prevUserTime = ToLong(user);
        }
    }

    private void InitNetworkStats()
    {
        try
        {
            var stats = GetTotalNetworkBytes();
            _prevBytesReceived = stats.Received;
            _prevBytesSent = stats.Sent;
        }
        catch { }
    }

    public void UpdateMetrics()
    {
        try
        {
            var metrics = new SystemMetrics();

            // CPU calculation via GetSystemTimes
            if (Win32Native.GetSystemTimes(out var idle, out var kernel, out var user))
            {
                long idleTime = ToLong(idle);
                long kernelTime = ToLong(kernel);
                long userTime = ToLong(user);

                long usrDiff = userTime - _prevUserTime;
                long kerDiff = kernelTime - _prevKernelTime;
                long idlDiff = idleTime - _prevIdleTime;

                long sysTotal = usrDiff + kerDiff;
                if (sysTotal > 0)
                {
                    double cpu = (double)(sysTotal - idlDiff) * 100.0 / sysTotal;
                    metrics.CpuPercent = Math.Clamp(Math.Round(cpu, 1), 0, 100);
                }

                _prevIdleTime = idleTime;
                _prevKernelTime = kernelTime;
                _prevUserTime = userTime;
            }

            // RAM calculation via GlobalMemoryStatusEx
            var memStatus = new Win32Native.MEMORYSTATUSEX();
            if (Win32Native.GlobalMemoryStatusEx(ref memStatus))
            {
                metrics.MemoryPercent = memStatus.dwMemoryLoad;
                metrics.TotalMemoryGb = Math.Round((double)memStatus.ullTotalPhys / (1024 * 1024 * 1024), 1);
                metrics.FreeMemoryGb = Math.Round((double)memStatus.ullAvailPhys / (1024 * 1024 * 1024), 1);
                metrics.UsedMemoryGb = Math.Round(metrics.TotalMemoryGb - metrics.FreeMemoryGb, 1);
            }

            // Disk calculation
            try
            {
                var systemDrive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\");
                if (systemDrive.IsReady)
                {
                    metrics.TotalDiskGb = Math.Round((double)systemDrive.TotalSize / (1024 * 1024 * 1024), 1);
                    metrics.FreeDiskGb = Math.Round((double)systemDrive.AvailableFreeSpace / (1024 * 1024 * 1024), 1);
                    double used = metrics.TotalDiskGb - metrics.FreeDiskGb;
                    metrics.DiskPercent = Math.Round((used / metrics.TotalDiskGb) * 100, 1);
                }
            }
            catch { }

            // Network calculation
            try
            {
                var now = DateTime.UtcNow;
                double seconds = (now - _prevTime).TotalSeconds;
                if (seconds > 0)
                {
                    var net = GetTotalNetworkBytes();
                    long diffRecv = Math.Max(0, net.Received - _prevBytesReceived);
                    long diffSent = Math.Max(0, net.Sent - _prevBytesSent);

                    metrics.NetworkDownloadKbps = Math.Round((diffRecv / 1024.0) / seconds, 1);
                    metrics.NetworkUploadKbps = Math.Round((diffSent / 1024.0) / seconds, 1);

                    _prevBytesReceived = net.Received;
                    _prevBytesSent = net.Sent;
                    _prevTime = now;
                }
            }
            catch { }

            CurrentMetrics = metrics;
            MetricsUpdated?.Invoke(metrics);
        }
        catch
        {
            // Safeguard against background thread exceptions
        }
    }

    private static (long Received, long Sent) GetTotalNetworkBytes()
    {
        long r = 0, s = 0;
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus == OperationalStatus.Up &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            {
                var stats = ni.GetIPStatistics();
                r += stats.BytesReceived;
                s += stats.BytesSent;
            }
        }
        return (r, s);
    }

    private static long ToLong(System.Runtime.InteropServices.ComTypes.FILETIME ft)
    {
        return ((long)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
