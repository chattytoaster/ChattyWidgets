using System.Diagnostics;
using System.IO;
using ChattyWidgets.Core.Win32;
using Microsoft.Win32;

namespace ChattyWidgets.Core.Services;

public static class FileAssociationService
{
    private const string ChwExtension = ".chw";
    private const string ChwProgId = "ChattyWidgets.Package";
    private const string ChwDescription = "Пакет виджета ChattyWidgets (.chw)";

    private const string ChwpExtension = ".chwp";
    private const string ChwpProgId = "ChattyWidgets.Preset";
    private const string ChwpDescription = "Пакет пресета рабочего стола ChattyWidgets (.chwp)";

    /// <summary>
    /// Ensures that .chw and .chwp files are associated with this executable in HKCU.
    /// </summary>
    public static void EnsureFileAssociation()
    {
        try
        {
            string exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return;

            RegisterExtension(ChwExtension, ChwProgId, ChwDescription, "application/x-chattywidgets-package", exePath);
            RegisterExtension(ChwpExtension, ChwpProgId, ChwpDescription, "application/x-chattywidgets-preset", exePath);

            // Notify Windows Explorer that associations have changed
            Win32Native.SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero); // SHCNE_ASSOCCHANGED
            Debug.WriteLine("[FileAssociationService] .chw and .chwp file associations verified.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FileAssociationService] Error updating file association: {ex.Message}");
        }
    }

    private static void RegisterExtension(string extension, string progId, string description, string contentType, string exePath)
    {
        // 1. HKCU\Software\Classes\<ext> -> ProgId
        using (var extKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + extension))
        {
            extKey?.SetValue(string.Empty, progId);
            extKey?.SetValue("Content Type", contentType);
        }

        // 2. HKCU\Software\Classes\<ProgId>
        using (var progIdKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + progId))
        {
            progIdKey?.SetValue(string.Empty, description);

            // DefaultIcon
            using (var iconKey = progIdKey?.CreateSubKey("DefaultIcon"))
            {
                iconKey?.SetValue(string.Empty, $"\"{exePath}\",0");
            }

            // shell\open\command
            using (var cmdKey = progIdKey?.CreateSubKey(@"shell\open\command"))
            {
                cmdKey?.SetValue(string.Empty, $"\"{exePath}\" \"%1\"");
            }
        }
    }
}
