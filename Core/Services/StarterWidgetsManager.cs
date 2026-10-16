using System.IO;

namespace ChattyWidgets.Core.Services;

public static class StarterWidgetsManager
{
    /// <summary>
    /// Bundles all starter widget directories into .chw files and installs them on first launch.
    /// </summary>
    public static void InitializeStarterWidgets()
    {
        string projectOrAppStarterDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StarterWidgets");
        
        // Also check development workspace directory if running in debug
        if (!Directory.Exists(projectOrAppStarterDir))
        {
            string devPath = Path.Combine(Directory.GetCurrentDirectory(), "StarterWidgets");
            if (Directory.Exists(devPath))
            {
                projectOrAppStarterDir = devPath;
            }
        }

        if (!Directory.Exists(projectOrAppStarterDir))
            return;

        // Find all subdirectories that contain a manifest.json
        foreach (var widgetDir in Directory.GetDirectories(projectOrAppStarterDir))
        {
            string manifestFile = Path.Combine(widgetDir, "manifest.json");
            if (!File.Exists(manifestFile))
                continue;

            string id = Path.GetFileName(widgetDir);
            string chwFile = Path.Combine(projectOrAppStarterDir, $"{id}.chw");

            try
            {
                // Create .chw package
                ChwPackageService.CreatePackage(widgetDir, chwFile);

                // Auto-install into %LocalAppData%\ChattyWidgets\widgets\<id>
                string installedDir = Path.Combine(ChwPackageService.WidgetsDir, id);
                if (!Directory.Exists(installedDir))
                {
                    ChwPackageService.InstallPackage(chwFile);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StarterWidgets] Ошибка упаковки/установки {id}: {ex.Message}");
            }
        }
    }
}
