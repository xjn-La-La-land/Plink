using System;
using System.IO;
using Microsoft.Win32;

namespace Plink
{
    // Settings persisted under HKCU\Software\Plink.
    internal sealed class AppSettings
    {
        private const string KeyPath = "Software\\Plink";
        public const string EmbeddedTypingSound = "resource://typewriter.wav";

        public bool CopyEnabled = true;
        public bool DeleteEnabled = true;
        public bool TypingEnabled = false;
        public string CopySound = DefaultCopySound;
        public string DeleteSound = DefaultDeleteSound;
        public string TypingSound = DefaultTypingSound;

        private static string MediaDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media");
            }
        }

        public static string DefaultCopySound
        {
            get { return Path.Combine(MediaDir, "Windows Balloon.wav"); }
        }

        public static string DefaultDeleteSound
        {
            get { return Path.Combine(MediaDir, "Windows Recycle.wav"); }
        }

        public static string DefaultTypingSound
        {
            get { return EmbeddedTypingSound; }
        }

        public static AppSettings Load()
        {
            AppSettings settings = new AppSettings();
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath, false))
                {
                    if (key != null)
                    {
                        settings.CopyEnabled = ReadBool(key, "CopyEnabled", settings.CopyEnabled);
                        settings.DeleteEnabled = ReadBool(key, "DeleteEnabled", settings.DeleteEnabled);
                        settings.TypingEnabled = ReadBool(key, "TypingEnabled", settings.TypingEnabled);
                        settings.CopySound = ReadString(key, "CopySound", settings.CopySound);
                        settings.DeleteSound = ReadString(key, "DeleteSound", settings.DeleteSound);
                        settings.TypingSound = ReadString(key, "TypingSound", settings.TypingSound);
                    }
                }
            }
            catch
            {
            }
            return settings;
        }

        public void Save()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    if (key == null)
                        return;
                    key.SetValue("CopyEnabled", CopyEnabled ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("DeleteEnabled", DeleteEnabled ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("TypingEnabled", TypingEnabled ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("CopySound", CopySound ?? "", RegistryValueKind.String);
                    key.SetValue("DeleteSound", DeleteSound ?? "", RegistryValueKind.String);
                    key.SetValue("TypingSound", TypingSound ?? "", RegistryValueKind.String);
                }
            }
            catch
            {
            }
        }

        private static bool ReadBool(RegistryKey key, string name, bool fallback)
        {
            object value = key.GetValue(name);
            if (value == null)
                return fallback;
            try
            {
                return Convert.ToInt32(value) != 0;
            }
            catch
            {
                return fallback;
            }
        }

        private static string ReadString(RegistryKey key, string name, string fallback)
        {
            string value = key.GetValue(name) as string;
            if (string.IsNullOrEmpty(value))
                return fallback;
            return value;
        }
    }
}
