using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ChattyWidgets.Setup.Services;

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

    private string Msg(string ru, string en) => Language == "ru" ? ru : en;

    public event Action<double, string>? ProgressChanged;

    public async Task<bool> InstallAsync(InstallOptions options)
    {
        return await Task.Run(() =>
        {
            try
            {
                ReportProgress(5, Msg("Подготовка к установке...", "Preparing for installation..."));

                string targetDir = options.TargetDirectory;
                Directory.CreateDirectory(targetDir);

                // 1. Locate source files or embedded payload
                string? sourceDir = FindSourceDirectory();
                ReportProgress(15, Msg("Копирование исполняемых файлов и библиотек...", "Copying application binaries and libraries..."));

                if (sourceDir != null && Directory.Exists(sourceDir))
                {
                    CopyDirectoryWithProgress(sourceDir, targetDir, 15, 65);
                }
                else
                {
                    // Check if an embedded zip resource exists
                    ExtractEmbeddedPayload(targetDir, 15, 65);
                }

                string targetExe = Path.Combine(targetDir, "ChattyWidgets.exe");

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
                    InstallAgentSkillFiles(sourceDir ?? targetDir);
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

    private string? FindSourceDirectory()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] candidates = new[]
        {
            Path.Combine(baseDir, "app"),
            Path.Combine(baseDir, "..", "..", "..", "..", "bin", "Debug", "net8.0-windows10.0.19041.0"),
            Path.Combine(baseDir, "..", "..", "..", "..", "bin", "Release", "net8.0-windows10.0.19041.0"),
            Path.Combine(baseDir, "..", "..", "..", "bin", "Debug", "net8.0-windows10.0.19041.0"),
            Path.Combine(baseDir, "..", "..", "..", "bin", "Release", "net8.0-windows10.0.19041.0"),
            Path.Combine(userHome, "Projects", "chattywidgets", "bin", "Debug", "net8.0-windows10.0.19041.0"),
            Path.Combine(userHome, "Projects", "chattywidgets", "bin", "Release", "net8.0-windows10.0.19041.0"),
            baseDir
        };

        foreach (var candidate in candidates)
        {
            try
            {
                string full = Path.GetFullPath(candidate);
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
            .Where(f => !Path.GetFileName(f).Equals("ChattyWidgetsSetup.exe", StringComparison.OrdinalIgnoreCase))
            .ToList();

        int total = allFiles.Count;
        for (int i = 0; i < total; i++)
        {
            string file = allFiles[i];
            string rel = Path.GetRelativePath(sourceDir, file);
            string dest = Path.Combine(targetDir, rel);

            string? parent = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

            File.Copy(file, dest, true);

            double pct = startPercent + ((double)(i + 1) / total) * (endPercent - startPercent);
            ReportProgress(pct, $"{Msg("Копирование", "Copying")}: {Path.GetFileName(file)}");
        }
    }

    private void ExtractEmbeddedPayload(string targetDir, double startPercent, double endPercent)
    {
        var assembly = Assembly.GetExecutingAssembly();
        string resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(r => r.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;

        if (string.IsNullOrEmpty(resourceName))
            return;

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return;

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        int total = archive.Entries.Count;

        for (int i = 0; i < total; i++)
        {
            var entry = archive.Entries[i];
            string destPath = Path.Combine(targetDir, entry.FullName);

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destPath);
                continue;
            }

            string? dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            entry.ExtractToFile(destPath, true);

            double pct = startPercent + ((double)(i + 1) / total) * (endPercent - startPercent);
            ReportProgress(pct, $"{Msg("Извлечение", "Extracting")}: {entry.Name}");
        }
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
                key.SetValue("DisplayName", "ChattyWidgets");
                key.SetValue("DisplayVersion", "1.0.0");
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
            foreach (var proc in Process.GetProcessesByName("ChattyWidgets"))
            {
                try
                {
                    proc.Kill();
                    proc.WaitForExit(3000);
                }
                catch { }
            }
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
