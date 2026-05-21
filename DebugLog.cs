using System;
using System.Globalization;
using System.IO;

namespace Plink
{
    // Lightweight diagnostic log. Off unless the PLINK_DEBUG env var is set,
    // so it costs nothing in normal use.
    internal static class DebugLog
    {
        private static readonly bool Enabled =
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLINK_DEBUG"));

        private static readonly string LogPath =
            Path.Combine(Path.GetTempPath(), "Plink.log");

        private static readonly object Sync = new object();

        public static void Write(string message)
        {
            if (!Enabled)
                return;

            try
            {
                lock (Sync)
                {
                    File.AppendAllText(LogPath,
                        DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)
                        + "  " + message + Environment.NewLine);
                }
            }
            catch
            {
            }
        }
    }
}
