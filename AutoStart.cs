using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Plink
{
    // Manages the per-user "run at logon" entry.
    internal static class AutoStart
    {
        private const string RunKeyPath =
            "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string ValueName = "Plink";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    return key != null && key.GetValue(ValueName) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void RefreshPath()
        {
            if (!IsEnabled())
                return;
            try
            {
                string expected = "\"" + Application.ExecutablePath + "\"";
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null)
                        return;
                    string current = key.GetValue(ValueName) as string;
                    if (current != null && !current.Equals(expected, System.StringComparison.OrdinalIgnoreCase))
                        key.SetValue(ValueName, expected, RegistryValueKind.String);
                }
            }
            catch
            {
            }
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null)
                        return;
                    if (enabled)
                    {
                        key.SetValue(ValueName,
                            "\"" + Application.ExecutablePath + "\"", RegistryValueKind.String);
                    }
                    else if (key.GetValue(ValueName) != null)
                    {
                        key.DeleteValue(ValueName, false);
                    }
                }
            }
            catch
            {
            }
        }
    }
}
