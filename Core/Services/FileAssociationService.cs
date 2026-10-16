using System.Diagnostics;
using System.IO;
using ChattyWidgets.Core.Win32;
using Microsoft.Win32;

namespace ChattyWidgets.Core.Services;

public static class FileAssociationService
{
    private const string Extension = ".chw";
    private const string ProgId = "ChattyWidgets.Package";
    private const string Description = "Пакет виджета ChattyWidgets (.chw)";

    /// <summary>
    /// Ensures that .chw files are associated with this executable in HKCU so double-clicking installs them.
    /// </summary>
    public static void EnsureFileAssociation()
    {
        try
        {
            string exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return;

            // 1. HKCU\Software\Classes\.chw -> ProgId
            using (var extKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + Extension))
            {
                extKey?.SetValue(string.Empty, ProgId);
                extKey?.SetValue("Content Type", "application/x-chattywidgets-package");
            }

            // 2. HKCU\Software\Classes\ChattyWidgets.Package
            using (var progIdKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProgId))
            {
                progIdKey?.SetValue(string.Empty, Description);

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

            // 3. Notify Windows Explorer that associations have changed
            Win32Native.SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero); // SHCNE_ASSOCCHANGED
            Debug.WriteLine("[FileAssociationService] .chw file association verified.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FileAssociationService] Error updating file association: {ex.Message}");
        }
    }
}
