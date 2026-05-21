using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;

namespace Plink
{
    // Raises FileDeleted whenever an item is moved to a Recycle Bin. Detection
    // works by watching each drive's $Recycle.Bin for the "$I" index file that
    // Windows creates for every recycled item.
    internal sealed class RecycleBinMonitor : IDisposable
    {
        private readonly List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();

        public event EventHandler FileDeleted;

        public void Start()
        {
            string sid = GetCurrentUserSid();

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady)
                        continue;
                    if (drive.DriveType != DriveType.Fixed &&
                        drive.DriveType != DriveType.Removable)
                        continue;

                    string watchPath = ResolveWatchPath(drive, sid);
                    if (watchPath == null)
                        continue;

                    FileSystemWatcher watcher = new FileSystemWatcher(watchPath);
                    watcher.IncludeSubdirectories = true;
                    watcher.Filter = "$I*";
                    watcher.NotifyFilter = NotifyFilters.FileName;
                    watcher.InternalBufferSize = 65536;
                    watcher.Created += OnCreated;
                    watcher.EnableRaisingEvents = true;
                    _watchers.Add(watcher);
                    DebugLog.Write("watching: " + watchPath);
                }
                catch (Exception ex)
                {
                    DebugLog.Write("skip drive " + drive.Name + ": " + ex.Message);
                }
            }

            DebugLog.Write("recycle bin watchers active: " + _watchers.Count);
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            DebugLog.Write("recycle bin item created: " + e.Name);
            EventHandler handler = FileDeleted;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        // Prefer the current user's own SID folder (guaranteed accessible); fall
        // back to the $Recycle.Bin root if that folder does not exist yet.
        private static string ResolveWatchPath(DriveInfo drive, string sid)
        {
            string binPath = Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin");

            if (sid != null)
            {
                string sidPath = Path.Combine(binPath, sid);
                if (Directory.Exists(sidPath))
                    return sidPath;
            }

            if (Directory.Exists(binPath))
                return binPath;

            return null;
        }

        private static string GetCurrentUserSid()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                if (identity != null && identity.User != null)
                    return identity.User.Value;
            }
            catch
            {
            }
            return null;
        }

        public void Dispose()
        {
            foreach (FileSystemWatcher watcher in _watchers)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Created -= OnCreated;
                    watcher.Dispose();
                }
                catch
                {
                }
            }
            _watchers.Clear();
        }
    }
}
