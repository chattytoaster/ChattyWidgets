using System.IO;
using System.IO.Compression;
using System.Text.Json;
using ChattyWidgets.Core.Models;
using Microsoft.Win32;

namespace ChattyWidgets.Core.Services;

public class ChwPackageService
{
    public static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ChattyWidgets"
    );

    public static readonly string WidgetsDir = Path.Combine(AppDataDir, "widgets");
    public static readonly string SettingsFile = Path.Combine(AppDataDir, "settings.json");

    static ChwPackageService()
    {
        Directory.CreateDirectory(AppDataDir);
        Directory.CreateDirectory(WidgetsDir);
    }

    /// <summary>
    /// Installs a .chw package file into local widgets directory.
    /// </summary>
    public static WidgetManifest InstallPackage(string chwFilePath)
    {
        if (!File.Exists(chwFilePath))
            throw new FileNotFoundException("Файл пакета не найден", chwFilePath);

        string tempExtract = Path.Combine(Path.GetTempPath(), "ChattyWidget_" + Guid.NewGuid().ToString("N"));
        try
        {
            ZipFile.ExtractToDirectory(chwFilePath, tempExtract);

            string manifestPath = Path.Combine(tempExtract, "manifest.json");
            if (!File.Exists(manifestPath))
                throw new InvalidOperationException("Пакет не содержит обязательный файл manifest.json");

            string manifestJson = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<WidgetManifest>(manifestJson);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Id))
                throw new InvalidOperationException("Файл manifest.json поврежден или не содержит ID виджета");

            // Destination directory
            string destDir = Path.Combine(WidgetsDir, manifest.Id);
            if (Directory.Exists(destDir))
            {
                Directory.Delete(destDir, true);
            }
            Directory.CreateDirectory(destDir);

            // Copy all files
            CopyDirectory(tempExtract, destDir);

            return manifest;
        }
        finally
        {
            if (Directory.Exists(tempExtract))
            {
                try { Directory.Delete(tempExtract, true); } catch { }
            }
        }
    }

    /// <summary>
    /// Creates a .chw package from a development directory.
    /// </summary>
    public static void CreatePackage(string sourceDirectory, string outputChwPath)
    {
        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException("Исходная папка не найдена");

        string manifestPath = Path.Combine(sourceDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new InvalidOperationException("В папке отсутствует manifest.json");

        string manifestJson = File.ReadAllText(manifestPath);
        var manifest = JsonSerializer.Deserialize<WidgetManifest>(manifestJson);
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Id))
            throw new InvalidOperationException("Некорректный manifest.json");

        string entryPath = Path.Combine(sourceDirectory, manifest.Entry);
        if (!File.Exists(entryPath))
            throw new InvalidOperationException($"Входной файл '{manifest.Entry}' не найден в папке виджета");

        if (File.Exists(outputChwPath))
            File.Delete(outputChwPath);

        string? dir = Path.GetDirectoryName(outputChwPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        ZipFile.CreateFromDirectory(sourceDirectory, outputChwPath, CompressionLevel.Optimal, false);
    }

    /// <summary>
    /// Scans installed widgets and returns manifests paired with their directories.
    /// </summary>
    public static List<(WidgetManifest Manifest, string DirectoryPath)> GetInstalledWidgets()
    {
        var list = new List<(WidgetManifest, string)>();
        if (!Directory.Exists(WidgetsDir))
            return list;

        foreach (var dir in Directory.GetDirectories(WidgetsDir))
        {
            string manifestPath = Path.Combine(dir, "manifest.json");
            if (File.Exists(manifestPath))
            {
                try
                {
                    string json = File.ReadAllText(manifestPath);
                    var manifest = JsonSerializer.Deserialize<WidgetManifest>(json);
                    if (manifest != null && !string.IsNullOrWhiteSpace(manifest.Id))
                    {
                        list.Add((manifest, dir));
                    }
                }
                catch
                {
                    // Ignore corrupted widgets
                }
            }
        }
        return list;
    }

    /// <summary>
    /// Uninstalls widget by removing its directory.
    /// </summary>
    public static void UninstallWidget(string id)
    {
        string dir = Path.Combine(WidgetsDir, id);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// Registers .chw file association in Windows Registry for 1-click install.
    /// </summary>
    public static void RegisterFileAssociation()
    {
        try
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;

            using var keyExt = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.chw");
            keyExt.SetValue("", "ChattyWidgets.Package");

            using var keyType = Registry.CurrentUser.CreateSubKey(@"Software\Classes\ChattyWidgets.Package");
            keyType.SetValue("", "Chatty Desktop Widget Package");

            using var keyIcon = Registry.CurrentUser.CreateSubKey(@"Software\Classes\ChattyWidgets.Package\DefaultIcon");
            keyIcon.SetValue("", $"\"{exePath}\",0");

            using var keyCommand = Registry.CurrentUser.CreateSubKey(@"Software\Classes\ChattyWidgets.Package\shell\open\command");
            keyCommand.SetValue("", $"\"{exePath}\" --install \"%1\"");
        }
        catch
        {
            // Non-critical, ignore if permissions are restricted
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            Directory.CreateDirectory(destSubDir);
            CopyDirectory(dir, destSubDir);
        }
    }
}
