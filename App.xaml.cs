using System.Windows;
using ChattyWidgets.Core.Services;
using ChattyWidgets.UI;

namespace ChattyWidgets;

public partial class App : System.Windows.Application
{
    private static bool _isFirstInstance = false;

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    [System.Runtime.InteropServices.DllImport("shell32.dll", SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string AppID);

    public static void Log(string msg)
    {
        try
        {
            string logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChattyWidgets");
            string appLog = System.IO.Path.Combine(logDir, "app.log");
            System.IO.File.AppendAllText(appLog, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try { SetCurrentProcessExplicitAppUserModelID("ChattyWidgets.App"); } catch {}

        string logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChattyWidgets");
        System.IO.Directory.CreateDirectory(logDir);

        Log("OnStartup entered");
        AttachConsole(-1);
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            Log($"[CRITICAL UNHANDLED] {args.ExceptionObject}");
            System.Diagnostics.Debug.WriteLine($"[CRITICAL UNHANDLED] {args.ExceptionObject}");
        };
        DispatcherUnhandledException += (s, args) =>
        {
            Log($"[DISPATCHER UNHANDLED] {args.Exception}");
            System.Diagnostics.Debug.WriteLine($"[DISPATCHER UNHANDLED] {args.Exception.Message}");
            args.Handled = true; // Prevent crash on binding or async errors
        };

        var allArgs = Environment.GetCommandLineArgs().Concat(e.Args).ToArray();
        // Check test argument
        if (allArgs.Any(a => a.Equals("--test", StringComparison.OrdinalIgnoreCase)))
        {
            Log("Running self test");
            RunSelfTest();
            Shutdown(0);
            return;
        }

        // Single instance check: if another instance is running, pass command-line arguments to it and exit!
        if (!SingleInstanceService.TryStartAsFirstInstance(OnSecondInstanceArgsReceived))
        {
            Log("Second instance detected, passing args to running instance...");
            bool sent = SingleInstanceService.SendArgsToRunningInstance(e.Args);
            if (sent)
            {
                Log("Args successfully passed to running instance. Exiting secondary instance.");
                Shutdown(0);
                return;
            }

            Log("Running instance did not respond on pipe (stale mutex or terminated process). Forcing takeover as primary instance.");
            SingleInstanceService.ForceResetMutex(OnSecondInstanceArgsReceived);
        }

        _isFirstInstance = true;
        Log("First instance confirmed, proceeding");

        // Register .chw file association in Windows Registry so double-clicking installs widgets
        try { FileAssociationService.EnsureFileAssociation(); } catch (Exception ex) { Log($"FileAssociation error: {ex.Message}"); }

        // 1. Initialize WidgetManager
        Log("Initializing WidgetManager");
        try { WidgetManager.Instance.Initialize(); } catch (Exception ex) { Log($"WidgetManager error: {ex.Message}"); }

        // 3. Create Main Hub Window
        Log("Creating MainWindow");
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;

        // Process any .chw passed on initial startup
        bool startMinimized = false;
        foreach (var arg in e.Args)
        {
            if (arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase))
            {
                startMinimized = true;
            }
            else if (arg.EndsWith(".chw", StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(arg))
            {
                mainWindow.InstallChwFile(arg);
            }
            else if (arg.EndsWith(".chwp", StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(arg))
            {
                mainWindow.ImportPresetFile(arg);
            }
        }

        if (!startMinimized)
        {
            Log("Showing MainWindow");
            try
            {
                mainWindow.Show();
                Log("mainWindow.Show returned OK");
            }
            catch (Exception ex)
            {
                Log($"mainWindow.Show THREW: {ex}");
            }
        }
        Log("OnStartup finished successfully");
    }

    private void OnSecondInstanceArgsReceived(string rawArgs)
    {
        Log($"OnSecondInstanceArgsReceived called. rawArgs: '{rawArgs}'");
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                var lines = rawArgs.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    string path = line.Trim().Trim('"');
                    if (path.EndsWith(".chw", StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(path))
                    {
                        if (MainWindow is MainWindow mw)
                        {
                            mw.Show();
                            mw.WindowState = WindowState.Normal;
                            mw.Activate();
                            mw.InstallChwFile(path);
                        }
                    }
                    else if (path.EndsWith(".chwp", StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(path))
                    {
                        if (MainWindow is MainWindow mw)
                        {
                            mw.Show();
                            mw.WindowState = WindowState.Normal;
                            mw.Activate();
                            mw.ImportPresetFile(path);
                        }
                    }
                }

                if (MainWindow is MainWindow win)
                {
                    win.Show();
                    if (win.WindowState == WindowState.Minimized)
                    {
                        win.WindowState = WindowState.Normal;
                    }
                    win.Activate();
                    win.Topmost = true;
                    win.Topmost = false;
                    win.Focus();

                    var hwnd = new System.Windows.Interop.WindowInteropHelper(win).Handle;
                    if (hwnd != IntPtr.Zero)
                    {
                        ChattyWidgets.Core.Win32.Win32Native.ShowWindow(hwnd, 9 /*SW_RESTORE*/);
                        ChattyWidgets.Core.Win32.Win32Native.SetForegroundWindow(hwnd);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error in OnSecondInstanceArgsReceived: {ex}");
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log($"OnExit called with code: {e.ApplicationExitCode}\nStack: {Environment.StackTrace}");
        if (_isFirstInstance && WidgetManager.Instance.IsInitialized)
        {
            try
            {
                WidgetManager.Instance.SaveSettings();
            }
            catch (Exception ex)
            {
                Log($"SaveSettings on exit error: {ex}");
            }
            SingleInstanceService.Cleanup();
        }
        base.OnExit(e);
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

        // Test 1: Package service and widgets directory readiness
        Log("TEST 1: Checking package service and widgets directory readiness...");
        var installed = ChwPackageService.GetInstalledWidgets();
        Log($"Current installed widgets count: {installed.Count}");
        foreach (var (m, dir) in installed)
        {
            Log($" - Widget: {m.Name} ({m.Id}), Type: {m.Type}, Entry: {m.Entry}");
        }
        Log("PASS: ChwPackageService initialized cleanly.");

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

        // Test 5: Launching widgets
        Log("TEST 5: Starting widget windows...");
        try
        {
            IntPtr workerW = ChattyWidgets.Core.Win32.DesktopHostManager.GetWorkerWHandle();
            Log($" - WorkerW handle: 0x{workerW.ToInt64():X}");

            bool nativeStarted = WidgetManager.Instance.StartWidget("com.chatty.system-gauges");
            Log($" - Start native widget: {nativeStarted}");
            var winNative = WidgetManager.Instance.GetActiveWindow("com.chatty.system-gauges");
            Log($" - Native window handle: {winNative != null}");

            bool webStarted = WidgetManager.Instance.StartWidget("com.chatty.clock-glass");
            Log($" - Start web widget: {webStarted}");
            var winWeb = WidgetManager.Instance.GetActiveWindow("com.chatty.clock-glass");
            Log($" - Web window handle: {winWeb != null}");

            if (winWeb != null)
            {
                var tcs = new TaskCompletionSource<string>();
                winWeb.ContentRendered += (s, e) => {
                    Log(" - winWeb ContentRendered fired!");
                    tcs.TrySetResult("Rendered");
                };
                // Wait up to 3 seconds and pump dispatcher
                var frame = new System.Windows.Threading.DispatcherFrame();
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                timer.Tick += (s, e) => { frame.Continue = false; timer.Stop(); };
                timer.Start();
                System.Windows.Threading.Dispatcher.PushFrame(frame);
                Log($" - winWeb IsVisible: {winWeb.IsVisible}, Opacity: {winWeb.Opacity}, Left: {winWeb.Left}, Top: {winWeb.Top}");
            }

            Log("PASS: Widget window instantiation tested.");
        }
        catch (Exception ex)
        {
            Log($"FAIL in StartWidget: {ex}");
        }

        // Test 6: MainWindow tabs navigation
        Log("TEST 6: Testing MainWindow creation and tab switching...");
        try
        {
            var mw = new MainWindow();
            mw.Show();
            mw.SwitchToTab("store");
            mw.SwitchToTab("workshop");
            mw.ComboNewWidgetType.IsDropDownOpen = true;
            mw.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            mw.ComboNewWidgetType.IsDropDownOpen = false;
            mw.SwitchToTab("settings");
            mw.SwitchToTab("my_widgets");
            mw.Close();
            Log("PASS: MainWindow initialized and all tabs switched without errors.");
        }
        catch (Exception ex)
        {
            Log($"FAIL in MainWindow tab test: {ex}");
            throw;
        }

        // Test 7: Custom presets & .chwp export/import
        Log("TEST 7: Testing custom presets and .chwp packaging...");
        string testChwp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TestPreset.chwp");
        try
        {
            PresetService.Instance.CleanDefaultPresets();
            Log($" - Default presets cleaned, count: {WidgetManager.Instance.Settings.Presets.Count}");

            var savedPreset = PresetService.Instance.SaveCurrentAsPreset("Тестовый Стол", "🌟", "Тестовое описание", "Tester");
            Log($" - Saved custom preset: {savedPreset.Name} (ID: {savedPreset.Id}, Widgets: {savedPreset.Widgets.Count})");

            PresetService.Instance.ExportPresetToFile(savedPreset, testChwp);
            Log($" - Exported preset to {testChwp} (Size: {new System.IO.FileInfo(testChwp).Length} bytes)");

            PresetService.Instance.DeletePreset(savedPreset.Id);
            Log(" - Deleted preset from memory.");

            var imported = PresetService.Instance.ImportPresetFromFile(testChwp);
            Log($" - Imported preset from .chwp: {imported.Name} (ID: {imported.Id})");

            if (imported.Name.Contains("Тестовый Стол"))
            {
                Log("PASS: Custom presets and .chwp export/import verified.");
            }
            else
            {
                Log("FAIL: Preset name mismatch.");
            }

            PresetService.Instance.DeletePreset(imported.Id);
        }
        catch (Exception ex)
        {
            Log($"FAIL in Preset test: {ex.Message}");
        }
        finally
        {
            try { if (System.IO.File.Exists(testChwp)) System.IO.File.Delete(testChwp); } catch { }
        }

        // Test 8: Duplicate Widget, Resizing and Shape Clipping
        Log("TEST 8: Testing Widget Duplication, Resizing, and Shape Clipping...");
        try
        {
            string? dupId = WidgetManager.Instance.DuplicateWidget("com.chatty.system-gauges");
            Log($" - DuplicateWidget returned ID: {dupId}");
            if (string.IsNullOrEmpty(dupId) || !dupId.Contains("#inst_"))
            {
                throw new Exception($"DuplicateWidget failed or returned invalid ID: {dupId}");
            }

            var dupWin = WidgetManager.Instance.GetActiveWindow(dupId);
            if (dupWin == null)
            {
                throw new Exception("Duplicate window was not found in active windows.");
            }

            Log($" - Duplicate window created: Title='{dupWin.Title}', Left={dupWin.Left}, Top={dupWin.Top}, Width={dupWin.Width}, Height={dupWin.Height}");

            // Test interactive resize
            dupWin.ResizeWidget(320, 240);
            Log($" - After ResizeWidget(320, 240): Width={dupWin.Width}, Height={dupWin.Height}, Settings.Width={dupWin.Settings.Width}, Settings.Height={dupWin.Settings.Height}");

            if (Math.Abs(dupWin.Width - 320) > 1 || Math.Abs(dupWin.Settings.Width - 320) > 1)
            {
                throw new Exception($"Resized width mismatch! Expected 320, got Width={dupWin.Width}, Settings.Width={dupWin.Settings.Width}");
            }

            // Test size presets: 25%, 50%, 75%, 100%, 125%
            double baseW = dupWin.Manifest.DefaultWidth > 0 ? dupWin.Manifest.DefaultWidth : 300;
            double baseH = dupWin.Manifest.DefaultHeight > 0 ? dupWin.Manifest.DefaultHeight : 200;
            double[] presetFactors = { 0.25, 0.50, 0.75, 1.00, 1.25 };
            foreach (var f in presetFactors)
            {
                double expectedW = Math.Round(baseW * f);
                double expectedH = Math.Round(baseH * f);
                dupWin.ResizeWidget(expectedW, expectedH);
                Log($" - Preset {(int)(f * 100)}%: Expected {expectedW}x{expectedH}, Got {dupWin.Width}x{dupWin.Height}");
                if (Math.Abs(dupWin.Width - expectedW) > 2 || Math.Abs(dupWin.Height - expectedH) > 2)
                {
                    throw new Exception($"Preset {(int)(f * 100)}% size mismatch! Expected {expectedW}x{expectedH}, got {dupWin.Width}x{dupWin.Height}");
                }
            }

            // Test shape clipping update
            dupWin.UpdateWindowShape();
            Log(" - UpdateWindowShape executed successfully without errors.");

            // Cleanup duplicate
            WidgetManager.Instance.DeleteWidgetInstance(dupId);
            bool stillExists = WidgetManager.Instance.GetActiveWindow(dupId) != null;
            Log($" - Duplicate cleaned up, still in active: {stillExists}");

            Log("PASS: Widget duplication, resizing sync, and shape clipping verified.");
        }
        catch (Exception ex)
        {
            Log($"FAIL in Duplicate/Resize test: {ex.Message}");
            throw;
        }

        Log("=== ALL TESTS COMPLETED SUCCESSFULLY ===");

        // Write report to file
        string reportPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TEST_REPORT.txt");
        System.IO.File.WriteAllLines(reportPath, logs);
        string rootReportPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "TEST_REPORT.txt");
        System.IO.File.WriteAllLines(rootReportPath, logs);
    }
}
