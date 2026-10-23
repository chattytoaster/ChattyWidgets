using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ChattyWidgets.Setup.Services;

public static class SetupLog
{
    public static void Write(string msg)
    {
        try
        {
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChattyWidgets");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(Path.Combine(logDir, "setup.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}\r\n");
        }
        catch { }
    }
}

public class InstallOptions
{
    public string TargetDirectory { get; set; } = string.Empty;
    public bool CreateDesktopShortcut { get; set; } = true;
    public bool CreateStartMenuShortcut { get; set; } = true;
    public bool StartWithWindows { get; set; } = true;
    public bool RegisterFileAssociations { get; set; } = true;
    public bool InstallAgentSkill { get; set; } = true;
    public bool OpenWebsite { get; set; } = true;
    public bool LaunchAppAfterInstall { get; set; } = true;
}

public class UninstallOptions
{
    public string TargetDirectory { get; set; } = string.Empty;
    public bool RemoveUserData { get; set; } = false;
    public bool RemoveAgentSkill { get; set; } = true;
}

public class InstallService
{
    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    public string Language { get; set; } = "ru";
    public string? LastErrorMessage { get; private set; }

    private string Msg(string ru, string en) => Language == "ru" ? ru : en;

    public event Action<double, string>? ProgressChanged;

    public async Task<bool> InstallAsync(InstallOptions options)
    {
        LastErrorMessage = null;
        return await Task.Run(() =>
        {
            try
            {
                ReportProgress(5, Msg("Подготовка к установке...", "Preparing for installation..."));

                // Ensure all running instances of ChattyWidgets are cleanly closed first so no binaries are locked
                ReportProgress(8, Msg("Закрытие запущенных процессов ChattyWidgets...", "Closing running ChattyWidgets processes..."));
                KillChattyProcesses();

                string targetDir = options.TargetDirectory;
                Directory.CreateDirectory(targetDir);

                // 1. Locate and deploy payload
                ReportProgress(15, Msg("Копирование исполняемых файлов и библиотек...", "Copying application binaries and libraries..."));

                SetupLog.Write($"InstallAsync deploy started into: {targetDir}");
                bool hasEmbedded = HasEmbeddedPayload();
                var asm = Assembly.GetExecutingAssembly();
                SetupLog.Write($"HasEmbeddedPayload: {hasEmbedded}, Assembly: {asm.FullName}, ManifestResources: {string.Join(", ", asm.GetManifestResourceNames())}");

                bool deployed = false;

                // Priority 1: Embedded payload in Setup assembly (the standard single-file installer)
                if (HasEmbeddedPayload())
                {
                    deployed = ExtractEmbeddedPayload(targetDir, 15, 65);
                }

                // Priority 2: External zip payload in installer directory (app.zip or ChattyWidgets-Update.zip)
                if (!deployed)
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string[] zipCandidates = new[]
                    {
                        Path.Combine(baseDir, "app.zip"),
                        Path.Combine(baseDir, "ChattyWidgets-Update.zip"),
                        Path.Combine(baseDir, "update.zip")
                    };
                    string? zipFile = zipCandidates.FirstOrDefault(File.Exists);
                    if (zipFile != null)
                    {
                        deployed = ExtractZipFile(zipFile, targetDir, 15, 65);
                    }
                }

                // Priority 3: Loose binaries from local project build directories (dev mode only)
                if (!deployed)
                {
                    string? sourceDir = FindSourceDirectory(targetDir);
                    if (sourceDir != null && Directory.Exists(sourceDir))
                    {
                        CopyDirectoryWithProgress(sourceDir, targetDir, 15, 65);
                        deployed = true;
                    }
                }

                if (!deployed)
                {
                    throw new FileNotFoundException(Msg(
                        "Не удалось найти компоненты приложения для установки (архив app.zip или исполняемые файлы не найдены).",
                        "Failed to locate application files for installation (app.zip archive or binaries not found)."
                    ));
                }

                string targetExe = Path.Combine(targetDir, "ChattyWidgets.exe");
                if (!File.Exists(targetExe))
                {
                    throw new FileNotFoundException(Msg(
                        $"Исполняемый файл программы ChattyWidgets.exe не обнаружен в целевой папке: {targetDir}",
                        $"Application executable ChattyWidgets.exe was not found in target folder: {targetDir}"
                    ));
                }

                // 2. Register file associations (.chw and .chwp)
                if (options.RegisterFileAssociations && File.Exists(targetExe))
                {
                    ReportProgress(70, Msg("Регистрация ассоциаций файлов .chw и .chwp...", "Registering .chw and .chwp file associations..."));
                    RegisterFileAssociations(targetExe);
                }

                // 3. Install Agent Skill for AI assistants (~/.agents/skills/chw)
                if (options.InstallAgentSkill)
                {
                    ReportProgress(80, Msg("Установка скилла /chw для AI-ассистентов...", "Installing /chw skill for AI assistants..."));
                    InstallAgentSkillFiles(targetDir);
                }

                // 4. Create Shortcuts (Desktop & Start Menu)
                if (File.Exists(targetExe))
                {
                    ReportProgress(88, Msg("Создание ярлыков...", "Creating desktop and start menu shortcuts..."));
                    if (options.CreateDesktopShortcut)
                    {
                        string desktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ChattyWidgets.lnk");
                        CreateShortcut(desktopPath, targetExe, targetDir, "ChattyWidgets Desktop Hub");
                    }

                    if (options.CreateStartMenuShortcut)
                    {
                        string startMenuPrograms = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                        string startMenuShortcut = Path.Combine(startMenuPrograms, "ChattyWidgets.lnk");
                        CreateShortcut(startMenuShortcut, targetExe, targetDir, "ChattyWidgets Desktop Hub");
                    }
                }

                // 5. Windows Autostart
                if (options.StartWithWindows && File.Exists(targetExe))
                {
                    ReportProgress(94, Msg("Настройка автозапуска с Windows...", "Configuring Windows autostart..."));
                    SetAutostart(targetExe, true);
                }

                // 6. Copy self as uninstaller & register in Windows Add/Remove Programs
                ReportProgress(97, Msg("Регистрация деинсталлятора в Windows...", "Registering uninstaller in Windows..."));
                CopyUninstaller(targetDir);
                RegisterUninstall(targetDir, targetExe);

                ReportProgress(100, Msg("Установка успешно завершена!", "Installation completed successfully!"));
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InstallService] Error: {ex}");
                LastErrorMessage = ex.Message;
                ReportProgress(100, $"{Msg("Ошибка установки", "Installation error")}: {ex.Message}");
                return false;
            }
        });
    }

    public async Task<bool> UninstallAsync(UninstallOptions options)
    {
        return await Task.Run(() =>
        {
            try
            {
                ReportProgress(10, Msg("Завершение процессов ChattyWidgets...", "Closing ChattyWidgets processes..."));
                KillChattyProcesses();

                ReportProgress(30, Msg("Удаление из автозагрузки Windows...", "Removing from Windows autostart..."));
                SetAutostart(string.Empty, false);

                ReportProgress(45, Msg("Удаление ассоциаций файлов .chw и .chwp...", "Removing file associations..."));
                UnregisterFileAssociations();

                ReportProgress(60, Msg("Удаление ярлыков программы...", "Removing shortcuts..."));
                RemoveShortcuts();

                ReportProgress(75, Msg("Удаление записи программы из Windows...", "Removing uninstall registration..."));
                UnregisterUninstall();

                if (options.RemoveAgentSkill)
                {
                    ReportProgress(85, Msg("Удаление скилла для AI-ассистентов...", "Removing AI assistant skill..."));
                    RemoveAgentSkill();
                }

                if (options.RemoveUserData)
                {
                    ReportProgress(90, Msg("Удаление пользовательских данных и виджетов...", "Removing user data and widgets..."));
                    RemoveUserData();
                }

                ReportProgress(95, Msg("Удаление файлов приложения...", "Deleting application files..."));
                DeleteTargetDirectoryFiles(options.TargetDirectory);

                ReportProgress(100, Msg("ChattyWidgets успешно удален!", "ChattyWidgets was successfully uninstalled!"));
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InstallService] Uninstall error: {ex}");
                ReportProgress(100, $"{Msg("Ошибка при удалении", "Uninstall error")}: {ex.Message}");
                return false;
            }
        });
    }

    public void FinalizeInstall(InstallOptions options)
    {
        string targetExe = Path.Combine(options.TargetDirectory, "ChattyWidgets.exe");

        // 1. Open website in default browser if requested
        if (options.OpenWebsite)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://chattyapps.dev") { UseShellExecute = true });
            }
            catch { }
        }

        // 2. Launch ChattyWidgets if requested
        if (options.LaunchAppAfterInstall && File.Exists(targetExe))
        {
            try
            {
                Process.Start(new ProcessStartInfo(targetExe)
                {
                    WorkingDirectory = options.TargetDirectory,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    private void ReportProgress(double percent, string message)
    {
        ProgressChanged?.Invoke(percent, message);
    }

    public static bool HasEmbeddedPayload()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetManifestResourceNames()
            .Any(r => r.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
    }

    private string? FindSourceDirectory(string targetDir)
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string[] candidates = new[]
        {
            Path.Combine(baseDir, "app"),
            Path.Combine(baseDir, "..", "..", "..", "..", "bin", "Publish", "app"),
            Path.Combine(baseDir, "..", "..", "..", "..", "bin", "Release", "net8.0-windows10.0.19041.0"),
            Path.Combine(baseDir, "..", "..", "..", "..", "bin", "Debug", "net8.0-windows10.0.19041.0"),
            Path.Combine(baseDir, "..", "..", "..", "bin", "Publish", "app"),
            Path.Combine(baseDir, "..", "..", "..", "bin", "Release", "net8.0-windows10.0.19041.0"),
            Path.Combine(baseDir, "..", "..", "..", "bin", "Debug", "net8.0-windows10.0.19041.0")
        };

        foreach (var candidate in candidates)
        {
            try
            {
                string full = Path.GetFullPath(candidate);
                if (string.Equals(full.TrimEnd('\\', '/'), targetDir.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                    continue;

                if (File.Exists(Path.Combine(full, "ChattyWidgets.dll")) && File.Exists(Path.Combine(full, "ChattyWidgets.exe")))
                {
                    return full;
                }
            }
            catch { }
        }

        return null;
    }

    private void CopyDirectoryWithProgress(string sourceDir, string targetDir, double startPercent, double endPercent)
    {
        var allFiles = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).Equals("ChattyWidgetsSetup.exe", StringComparison.OrdinalIgnoreCase) &&
                        !Path.GetFileName(f).Equals("Uninstall.exe", StringComparison.OrdinalIgnoreCase))
            .ToList();

        int total = allFiles.Count;
        for (int i = 0; i < total; i++)
        {
            string file = allFiles[i];
            string rel = Path.GetRelativePath(sourceDir, file);
            string dest = Path.Combine(targetDir, rel);

            string? parent = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

            CopyFileWithRetry(file, dest);

            double pct = startPercent + ((double)(i + 1) / total) * (endPercent - startPercent);
            ReportProgress(pct, $"{Msg("Копирование", "Copying")}: {Path.GetFileName(file)}");
        }
    }

    private static void CopyFileWithRetry(string sourceFile, string destFile, int maxRetries = 10)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (File.Exists(destFile))
                {
                    var attrs = File.GetAttributes(destFile);
                    if (attrs.HasFlag(FileAttributes.ReadOnly))
                    {
                        File.SetAttributes(destFile, attrs & ~FileAttributes.ReadOnly);
                    }
                }

                File.Copy(sourceFile, destFile, true);
                return;
            }
            catch (IOException) when (attempt < maxRetries)
            {
                Thread.Sleep(200);
            }
            catch (UnauthorizedAccessException) when (attempt < maxRetries)
            {
                Thread.Sleep(200);
            }
        }

        File.Copy(sourceFile, destFile, true);
    }

    private bool ExtractEmbeddedPayload(string targetDir, double startPercent, double endPercent)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(r => r.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;

            if (string.IsNullOrEmpty(resourceName))
                return false;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return false;

            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            int total = archive.Entries.Count;
            if (total == 0) return false;

            for (int i = 0; i < total; i++)
            {
                var entry = archive.Entries[i];
                string rel = entry.FullName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                string destPath = Path.Combine(targetDir, rel);

                if (string.IsNullOrEmpty(entry.Name) || entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    Directory.CreateDirectory(destPath);
                    continue;
                }

                string? dir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                ExtractEntryWithRetry(entry, destPath);

                double pct = startPercent + ((double)(i + 1) / total) * (endPercent - startPercent);
                ReportProgress(pct, $"{Msg("Извлечение", "Extracting")}: {entry.Name}");
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InstallService] ExtractEmbeddedPayload error: {ex}");
            throw;
        }
    }

    private bool ExtractZipFile(string zipPath, string targetDir, double startPercent, double endPercent)
    {
        try
        {
            if (!File.Exists(zipPath)) return false;

            using var stream = File.OpenRead(zipPath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            int total = archive.Entries.Count;
            if (total == 0) return false;

            for (int i = 0; i < total; i++)
            {
                var entry = archive.Entries[i];
                string rel = entry.FullName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                string destPath = Path.Combine(targetDir, rel);

                if (string.IsNullOrEmpty(entry.Name) || entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    Directory.CreateDirectory(destPath);
                    continue;
                }

                string? dir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                ExtractEntryWithRetry(entry, destPath);

                double pct = startPercent + ((double)(i + 1) / total) * (endPercent - startPercent);
                ReportProgress(pct, $"{Msg("Извлечение", "Extracting")}: {entry.Name}");
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InstallService] ExtractZipFile error: {ex}");
            throw;
        }
    }

    private static void ExtractEntryWithRetry(ZipArchiveEntry entry, string destPath, int maxRetries = 10)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (File.Exists(destPath))
                {
                    var attrs = File.GetAttributes(destPath);
                    if (attrs.HasFlag(FileAttributes.ReadOnly))
                    {
                        File.SetAttributes(destPath, attrs & ~FileAttributes.ReadOnly);
                    }
                }

                entry.ExtractToFile(destPath, true);
                return;
            }
            catch (IOException) when (attempt < maxRetries)
            {
                Thread.Sleep(200);
            }
            catch (UnauthorizedAccessException) when (attempt < maxRetries)
            {
                Thread.Sleep(200);
            }
        }

        entry.ExtractToFile(destPath, true);
    }

    public static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string description)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell != null)
                {
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = targetPath;
                    shortcut.WorkingDirectory = workingDir;
                    shortcut.IconLocation = targetPath + ",0";
                    shortcut.Description = description;
                    shortcut.Save();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InstallService] Shortcut error: {ex.Message}");
        }
    }

    public static void RegisterFileAssociations(string targetExePath)
    {
        try
        {
            // Register .chw extension
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.chw"))
            {
                key.SetValue("", "ChattyWidgets.Package");
            }
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\ChattyWidgets.Package"))
            {
                key.SetValue("", "ChattyWidgets Widget Package");
                using (var iconKey = key.CreateSubKey("DefaultIcon"))
                {
                    iconKey.SetValue("", $"{targetExePath},0");
                }
                using (var shellKey = key.CreateSubKey(@"shell\open\command"))
                {
                    shellKey.SetValue("", $"\"{targetExePath}\" \"%1\"");
                }
            }

            // Register .chwp extension
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.chwp"))
            {
                key.SetValue("", "ChattyWidgets.Preset");
            }
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\ChattyWidgets.Preset"))
            {
                key.SetValue("", "ChattyWidgets Preset Package");
                using (var iconKey = key.CreateSubKey("DefaultIcon"))
                {
                    iconKey.SetValue("", $"{targetExePath},0");
                }
                using (var shellKey = key.CreateSubKey(@"shell\open\command"))
                {
                    shellKey.SetValue("", $"\"{targetExePath}\" \"%1\"");
                }
            }

            // Notify Explorer of association change
            SHChangeNotify(0x08000000 /*SHCNE_ASSOCCHANGED*/, 0x0000 /*SHCNF_IDLIST*/, IntPtr.Zero, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InstallService] Registry error: {ex.Message}");
        }
    }

    public static void SetAutostart(string targetExePath, bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                if (enabled)
                    key.SetValue("ChattyWidgets", $"\"{targetExePath}\" --minimized");
                else
                    key.DeleteValue("ChattyWidgets", false);
            }
        }
        catch { }
    }

    public static void InstallAgentSkillFiles(string sourceRoot)
    {
        try
        {
            string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string targetSkillDir = Path.Combine(userHome, ".agents", "skills", "chw");
            Directory.CreateDirectory(targetSkillDir);

            // Locate .agents/skills/chw in source or fallback
            string[] possibleSources = new[]
            {
                Path.Combine(sourceRoot, ".agents", "skills", "chw"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".agents", "skills", "chw"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".agents", "skills", "chw"),
                Path.Combine(userHome, "Projects", "chattywidgets", ".agents", "skills", "chw")
            };

            string? found = possibleSources.FirstOrDefault(d => Directory.Exists(d) && File.Exists(Path.Combine(d, "SKILL.md")));
            if (found != null)
            {
                foreach (string file in Directory.GetFiles(found, "*.*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(found, file);
                    string dest = Path.Combine(targetSkillDir, rel);
                    string? dir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.Copy(file, dest, true);
                }
            }
            else
            {
                // Fallback: write embedded SKILL.md
                string skillMd = @"---
name: chw
description: >-
  Create, scaffold, implement, test, and publish desktop widgets for ChattyWidgets.
  Trigger with `/chw` or when requested to develop a new widget, create glassmorphic XAML UI,
  implement widget ViewModels, configure manifest.json, wire up custom settings cards,
  package .chw files, and publish signed packages to ChattyWidgetsLibrary.
---

# ChattyWidgets Widget Development & Publishing Skill (`/chw`)

Use this skill to autonomously scaffold, develop, wire up, locally test, and officially publish desktop widgets for the ChattyWidgets platform.
";
                File.WriteAllText(Path.Combine(targetSkillDir, "SKILL.md"), skillMd);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InstallService] Agent skill error: {ex.Message}");
        }
    }

    public static void CopyUninstaller(string targetDir)
    {
        try
        {
            string currentExe = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? string.Empty;

            if (!string.IsNullOrEmpty(currentExe) && File.Exists(currentExe))
            {
                string dest = Path.Combine(targetDir, "Uninstall.exe");
                File.Copy(currentExe, dest, true);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InstallService] CopyUninstaller error: {ex.Message}");
        }
    }

    public static void RegisterUninstall(string targetDir, string targetExe)
    {
        try
        {
            string uninstallExe = Path.Combine(targetDir, "Uninstall.exe");
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\ChattyWidgets");
            if (key != null)
            {
                string version = "1.0.2";
                if (File.Exists(targetExe))
                {
                    try
                    {
                        var vi = FileVersionInfo.GetVersionInfo(targetExe);
                        if (!string.IsNullOrWhiteSpace(vi.FileVersion))
                            version = vi.FileVersion;
                        else if (!string.IsNullOrWhiteSpace(vi.ProductVersion))
                            version = vi.ProductVersion;
                    }
                    catch { }
                }

                key.SetValue("DisplayName", "ChattyWidgets");
                key.SetValue("DisplayVersion", version);
                key.SetValue("Publisher", "ChattyApps");
                key.SetValue("DisplayIcon", $"{targetExe},0");
                key.SetValue("InstallLocation", targetDir);
                key.SetValue("UninstallString", $"\"{uninstallExe}\" --uninstall");
                key.SetValue("QuietUninstallString", $"\"{uninstallExe}\" --uninstall --silent");
                key.SetValue("URLInfoAbout", "https://chattyapps.dev");
                key.SetValue("HelpLink", "https://chattyapps.dev");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", 40960, RegistryValueKind.DWord);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InstallService] RegisterUninstall error: {ex.Message}");
        }
    }

    public static void UnregisterUninstall()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\ChattyWidgets", false);
        }
        catch { }
    }

    public static void KillChattyProcesses()
    {
        try
        {
            int currentPid = Environment.ProcessId;
            var running = Process.GetProcessesByName("ChattyWidgets")
                .Where(p => p.Id != currentPid)
                .ToList();

            if (running.Count == 0) return;

            // 0. Send graceful shutdown command via SingleInstance named pipe
            try
            {
                using var client = new System.IO.Pipes.NamedPipeClientStream(".", "ChattyWidgets_SingleInstance_Pipe", System.IO.Pipes.PipeDirection.Out);
                client.Connect(350);
                using var writer = new StreamWriter(client) { AutoFlush = true };
                writer.WriteLine("--shutdown");
            }
            catch { }

            // 1. Send graceful close to main windows
            foreach (var proc in running)
            {
                try
                {
                    proc.CloseMainWindow();
                }
                catch { }
            }

            // Wait up to 1.5 seconds for graceful exit
            for (int i = 0; i < 15; i++)
            {
                Thread.Sleep(100);
                if (!Process.GetProcessesByName("ChattyWidgets").Any(p => p.Id != currentPid))
                    return;
            }

            // 2. Force terminate any lingering processes to release file locks completely
            foreach (var proc in Process.GetProcessesByName("ChattyWidgets"))
            {
                if (proc.Id == currentPid) continue;
                try
                {
                    proc.Kill();
                    proc.WaitForExit(3000);
                }
                catch { }
            }

            Thread.Sleep(250);
        }
        catch { }
    }

    public static void UnregisterFileAssociations()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\.chw", false);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\ChattyWidgets.Package", false);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\.chwp", false);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\ChattyWidgets.Preset", false);
            SHChangeNotify(0x08000000 /*SHCNE_ASSOCCHANGED*/, 0x0000, IntPtr.Zero, IntPtr.Zero);
        }
        catch { }
    }

    public static void RemoveShortcuts()
    {
        try
        {
            string desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ChattyWidgets.lnk");
            if (File.Exists(desktopShortcut)) File.Delete(desktopShortcut);

            string startMenuPrograms = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            string startMenuShortcut = Path.Combine(startMenuPrograms, "ChattyWidgets.lnk");
            if (File.Exists(startMenuShortcut)) File.Delete(startMenuShortcut);
        }
        catch { }
    }

    public static void RemoveAgentSkill()
    {
        try
        {
            string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string targetSkillDir = Path.Combine(userHome, ".agents", "skills", "chw");
            if (Directory.Exists(targetSkillDir))
            {
                Directory.Delete(targetSkillDir, true);
            }
        }
        catch { }
    }

    public static void RemoveUserData()
    {
        try
        {
            string localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChattyWidgets");
            if (Directory.Exists(localAppData))
            {
                Directory.Delete(localAppData, true);
            }
        }
        catch { }
    }

    public static void DeleteTargetDirectoryFiles(string targetDir)
    {
        try
        {
            if (Directory.Exists(targetDir))
            {
                string currentExe = Environment.ProcessPath ?? string.Empty;
                foreach (var file in Directory.GetFiles(targetDir, "*.*", SearchOption.AllDirectories))
                {
                    if (!string.Equals(file, currentExe, StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }

                foreach (var dir in Directory.GetDirectories(targetDir, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
                {
                    try { Directory.Delete(dir, false); } catch { }
                }
            }
        }
        catch { }
    }

    public static void ScheduleDelayedDirectoryCleanup(string targetDir)
    {
        try
        {
            if (Directory.Exists(targetDir))
            {
                string cmd = $"/c timeout /t 1 /nobreak > NUL & rmdir /s /q \"{targetDir}\"";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = cmd,
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
        }
        catch { }
    }

    public static string GetInstalledDirectory()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\ChattyWidgets");
            if (key != null)
            {
                var loc = key.GetValue("InstallLocation") as string;
                if (!string.IsNullOrEmpty(loc) && Directory.Exists(loc))
                    return loc;
            }
        }
        catch { }

        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        if (File.Exists(Path.Combine(appDir, "ChattyWidgets.exe")))
            return appDir;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "ChattyWidgets"
        );
    }
}
