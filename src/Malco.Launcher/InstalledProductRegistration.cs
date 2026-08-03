using System;
using Microsoft.Win32;

namespace Malco.Launcher
{
    internal static class InstalledProductRegistration
    {
        private const string UninstallKey =
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{76D40A9B-231C-4FA4-9274-607E7A9A76E4}_is1";

        public static void TrySetVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return;
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(UninstallKey, writable: true))
                {
                    if (key == null) return;
                    key.SetValue("DisplayName", "Malco " + version, RegistryValueKind.String);
                    key.SetValue("DisplayVersion", version, RegistryValueKind.String);
                }
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException ||
                exception is System.Security.SecurityException ||
                exception is System.IO.IOException)
            {
                // The app update remains valid even if Windows' optional display
                // metadata cannot be refreshed for this user.
            }
        }
    }
}
