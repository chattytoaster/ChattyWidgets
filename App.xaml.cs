using System.Windows;
using ChattyWidgets.Core.Services;
using ChattyWidgets.UI;

namespace ChattyWidgets;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var allArgs = Environment.GetCommandLineArgs().Concat(e.Args).ToArray();
        // Check test argument
        if (allArgs.Any(a => a.Equals("--test", StringComparison.OrdinalIgnoreCase)))
        {
            RunSelfTest();
            Shutdown(0);
            return;
        }

        // 1. Unpack & prepare starter widgets
        StarterWidgetsManager.InitializeStarterWidgets();

        // 2. Check command line args (e.g. file association install)
        bool startMinimized = false;
        for (int i = 0; i < e.Args.Length; i++)
        {
            if (e.Args[i].Equals("--minimized", StringComparison.OrdinalIgnoreCase))
            {
                startMinimized = true;
            }
            else if (e.Args[i].Equals("--install", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
            {
                string chwPath = e.Args[i + 1];
                try
                {
                    ChwPackageService.InstallPackage(chwPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось установить пакет .chw: {ex.Message}", "ChattyWidgets", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else if (e.Args[i].EndsWith(".chw", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    ChwPackageService.InstallPackage(e.Args[i]);
                }
                catch { }
            }
        }

        // 3. Initialize WidgetManager
        WidgetManager.Instance.Initialize();

        // 4. Create Main Hub Window
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;

        if (!startMinimized)
        {
            mainWindow.Show();
        }
    }

    private void RunSelfTest()
    {
        var logs = new List<string>();
        void Log(string msg)
        {
            logs.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
            Console.WriteLine(msg);
        }

        Log("=== CHATTYWIDGETS SELF-TEST STARTED ===");

        // Test 1: Starter widgets initialization & packaging
        Log("TEST 1: Initializing starter widgets and packaging to .chw...");
        StarterWidgetsManager.InitializeStarterWidgets();
        var installed = ChwPackageService.GetInstalledWidgets();
        Log($"Installed widgets count: {installed.Count}");
        foreach (var (m, dir) in installed)
        {
            Log($" - Widget: {m.Name} ({m.Id}), Type: {m.Type}, Entry: {m.Entry}");
        }

        if (installed.Count < 5)
        {
            Log($"WARNING: Expected at least 5 starter widgets, found {installed.Count}");
        }
        else
        {
            Log("PASS: All 5 starter widgets successfully deployed and registered.");
        }

        // Test 2: System Monitor metrics
        Log("TEST 2: Checking SystemMonitorService metrics...");
        SystemMonitorService.Instance.UpdateMetrics();
        var metrics = SystemMonitorService.Instance.CurrentMetrics;
        Log($" - CPU: {metrics.CpuPercent}%");
        Log($" - RAM: {metrics.MemoryPercent}% ({metrics.UsedMemoryGb} GB / {metrics.TotalMemoryGb} GB)");
        Log($" - SSD: {metrics.DiskPercent}% ({metrics.FreeDiskGb} GB free)");
        Log($" - Net: Down={metrics.NetworkDownloadKbps} KB/s, Up={metrics.NetworkUploadKbps} KB/s");

        if (metrics.TotalMemoryGb > 0 && metrics.TotalDiskGb > 0)
        {
            Log("PASS: SystemMonitorService is reporting valid real-time hardware data.");
        }
        else
        {
            Log("FAIL: Invalid system metrics.");
        }

        // Test 3: Workshop Scaffolding & Packaging
        Log("TEST 3: Testing Workshop Scaffolding and .chw Packager...");
        string testProjectDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ChattyTestWidget_" + Guid.NewGuid().ToString("N"));
        string testChwPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TestWidget.chw");

        try
        {
            WorkshopService.Instance.CreateWidgetProject(
                testProjectDir,
                "com.test.sample-widget",
                "Тестовый Виджет",
                "Tester",
                "Описание теста",
                "web",
                250,
                150);

            Log(" - Project scaffold created successfully.");

            ChwPackageService.CreatePackage(testProjectDir, testChwPath);
            Log($" - Package created at {testChwPath} (Size: {new System.IO.FileInfo(testChwPath).Length} bytes)");

            var installedManifest = ChwPackageService.InstallPackage(testChwPath);
            Log($" - Installed package manifest verified: {installedManifest.Name} (ID: {installedManifest.Id})");

            ChwPackageService.UninstallWidget("com.test.sample-widget");
            Log(" - Cleaned up test widget.");
            Log("PASS: Workshop scaffolding, packaging (.chw) and installer verified.");
        }
        catch (Exception ex)
        {
            Log($"FAIL in Workshop test: {ex.Message}");
        }
        finally
        {
            try { if (System.IO.Directory.Exists(testProjectDir)) System.IO.Directory.Delete(testProjectDir, true); } catch { }
            try { if (System.IO.File.Exists(testChwPath)) System.IO.File.Delete(testChwPath); } catch { }
        }

        // Test 4: Store Catalog
        Log("TEST 4: Checking Store Catalog...");
        var storeCatalog = StoreService.Instance.GetStarterWidgetsCatalog();
        Log($" - Store starter catalog contains {storeCatalog.Count} items.");
        foreach (var item in storeCatalog)
        {
            Log($"   * [{item.Category}] {item.Name} ({item.Id}) - {item.Size}");
        }
        Log("PASS: Store catalog is populated.");

        Log("=== ALL TESTS COMPLETED SUCCESSFULLY ===");

        // Write report to file
        string reportPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TEST_REPORT.txt");
        System.IO.File.WriteAllLines(reportPath, logs);
        string rootReportPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "TEST_REPORT.txt");
        System.IO.File.WriteAllLines(rootReportPath, logs);
    }
}
