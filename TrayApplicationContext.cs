using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Media;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Plink
{
    // Tray-only application: wires the clipboard and recycle bin monitors to
    // sound playback, and exposes settings through the tray context menu.
    internal sealed class TrayApplicationContext : ApplicationContext
    {
        // Collapses bursts of events (an app writing the clipboard several
        // times, or a multi-file delete) into a single sound.
        private const int CopyDebounceMs = 200;
        private const int DeleteDebounceMs = 500;

        private readonly AppSettings _settings;
        private readonly Icon _appIcon;
        private readonly NotifyIcon _trayIcon;
        private readonly ClipboardMonitor _clipboardMonitor;
        private readonly RecycleBinMonitor _recycleBinMonitor;
        private readonly Win11MenuRenderer _menuRenderer;

        private readonly object _soundLock = new object();
        private DateTime _lastCopy = DateTime.MinValue;
        private DateTime _lastDelete = DateTime.MinValue;

        private SoundPlayer _copyPlayer;
        private string _copyPlayerPath;
        private SoundPlayer _deletePlayer;
        private string _deletePlayerPath;

        private ToolStripMenuItem _copyItem;
        private ToolStripMenuItem _deleteItem;
        private ToolStripMenuItem _autoStartItem;

        public TrayApplicationContext()
        {
            _settings = AppSettings.Load();
            DebugLog.Write("starting; copyEnabled=" + _settings.CopyEnabled
                + " deleteEnabled=" + _settings.DeleteEnabled);

            _appIcon = LoadAppIcon();
            _menuRenderer = new Win11MenuRenderer();

            _trayIcon = new NotifyIcon();
            _trayIcon.Icon = _appIcon;
            _trayIcon.Text = "Plink";
            _trayIcon.ContextMenuStrip = BuildMenu();
            _trayIcon.Visible = true;

            _clipboardMonitor = new ClipboardMonitor();
            _clipboardMonitor.ClipboardUpdated += OnClipboardUpdated;

            _recycleBinMonitor = new RecycleBinMonitor();
            _recycleBinMonitor.FileDeleted += OnFileDeleted;
            _recycleBinMonitor.Start();

            AutoStart.RefreshPath();
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            MaybeStartDebugPreview();
        }

        private static Icon LoadAppIcon()
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream("plink.ico"))
                {
                    if (s != null)
                    {
                        Size desired = SystemInformation.SmallIconSize;
                        return new Icon(s, desired.Width, desired.Height);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog.Write("icon load failed: " + ex.Message);
            }
            return (Icon)SystemIcons.Application.Clone();
        }

        private ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = _menuRenderer;
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = true;
            menu.Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
            menu.ImageScalingSize = new Size(16, 16);
            menu.Padding = new Padding(0, 6, 0, 6);
            menu.DropShadowEnabled = true;
            menu.Opening += OnMenuOpening;
            menu.Opened += OnMenuOpened;

            _copyItem = MakeItem("复制时播放声音", OnToggleCopy);
            _copyItem.Checked = _settings.CopyEnabled;

            _deleteItem = MakeItem("删除到回收站时播放声音", OnToggleDelete);
            _deleteItem.Checked = _settings.DeleteEnabled;

            _autoStartItem = MakeItem("开机自动启动", OnToggleAutoStart);
            _autoStartItem.Checked = AutoStart.IsEnabled();

            menu.Items.Add(_copyItem);
            menu.Items.Add(_deleteItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(MakeItem("选择复制音效…", OnChooseCopySound));
            menu.Items.Add(MakeItem("选择删除音效…", OnChooseDeleteSound));
            menu.Items.Add(MakeItem("恢复默认音效", OnResetSounds));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_autoStartItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(MakeItem("退出 Plink", OnExit));

            return menu;
        }

        private static ToolStripMenuItem MakeItem(string text, EventHandler onClick)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text, null, onClick);
            item.Padding = new Padding(4, 6, 16, 6);
            return item;
        }

        private void OnMenuOpening(object sender, CancelEventArgs e)
        {
            _menuRenderer.Theme = Win11Theme.Resolve();
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General)
                return;
            _menuRenderer.Theme = Win11Theme.Resolve();
            ContextMenuStrip menu = _trayIcon.ContextMenuStrip;
            if (menu != null && menu.Visible)
                menu.Invalidate(true);
        }

        private void OnMenuOpened(object sender, EventArgs e)
        {
            ContextMenuStrip menu = sender as ContextMenuStrip;
            if (menu != null)
                WindowEffects.RoundCorners(menu.Handle);
        }

        private void OnToggleCopy(object sender, EventArgs e)
        {
            _settings.CopyEnabled = !_settings.CopyEnabled;
            _copyItem.Checked = _settings.CopyEnabled;
            _settings.Save();
        }

        private void OnToggleDelete(object sender, EventArgs e)
        {
            _settings.DeleteEnabled = !_settings.DeleteEnabled;
            _deleteItem.Checked = _settings.DeleteEnabled;
            _settings.Save();
        }

        private void OnToggleAutoStart(object sender, EventArgs e)
        {
            AutoStart.SetEnabled(!_autoStartItem.Checked);
            _autoStartItem.Checked = AutoStart.IsEnabled();
        }

        private void OnChooseCopySound(object sender, EventArgs e)
        {
            string picked = PickWav(_settings.CopySound);
            if (picked != null)
            {
                _settings.CopySound = picked;
                _settings.Save();
                PlaySound(ref _copyPlayer, ref _copyPlayerPath, picked);
            }
        }

        private void OnChooseDeleteSound(object sender, EventArgs e)
        {
            string picked = PickWav(_settings.DeleteSound);
            if (picked != null)
            {
                _settings.DeleteSound = picked;
                _settings.Save();
                PlaySound(ref _deletePlayer, ref _deletePlayerPath, picked);
            }
        }

        private void OnResetSounds(object sender, EventArgs e)
        {
            _settings.CopySound = AppSettings.DefaultCopySound;
            _settings.DeleteSound = AppSettings.DefaultDeleteSound;
            _settings.Save();
        }

        private static string PickWav(string current)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择音效文件";
                dialog.Filter = "WAV 音频 (*.wav)|*.wav";
                try
                {
                    if (!string.IsNullOrEmpty(current) && File.Exists(current))
                        dialog.InitialDirectory = Path.GetDirectoryName(current);
                }
                catch
                {
                }
                if (dialog.ShowDialog() == DialogResult.OK)
                    return dialog.FileName;
            }
            return null;
        }

        private void OnClipboardUpdated(object sender, EventArgs e)
        {
            if (!_settings.CopyEnabled)
                return;
            if (!ShouldPlay(ref _lastCopy, CopyDebounceMs))
            {
                DebugLog.Write("clipboard update (debounced)");
                return;
            }
            DebugLog.Write("clipboard update -> copy sound");
            PlaySound(ref _copyPlayer, ref _copyPlayerPath, _settings.CopySound);
        }

        private void OnFileDeleted(object sender, EventArgs e)
        {
            if (!_settings.DeleteEnabled)
                return;
            if (!ShouldPlay(ref _lastDelete, DeleteDebounceMs))
            {
                DebugLog.Write("delete event (debounced)");
                return;
            }
            DebugLog.Write("delete event -> delete sound");
            PlaySound(ref _deletePlayer, ref _deletePlayerPath, _settings.DeleteSound);
        }

        private bool ShouldPlay(ref DateTime last, int debounceMs)
        {
            lock (_soundLock)
            {
                DateTime now = DateTime.UtcNow;
                if ((now - last).TotalMilliseconds < debounceMs)
                    return false;
                last = now;
                return true;
            }
        }

        private void PlaySound(ref SoundPlayer player, ref string playerPath, string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    DebugLog.Write("sound file missing: " + (path ?? "(null)"));
                    return;
                }
                if (player == null || playerPath != path)
                {
                    if (player != null)
                        player.Dispose();
                    player = new SoundPlayer(path);
                    playerPath = path;
                }
                player.Play();
            }
            catch (Exception ex)
            {
                DebugLog.Write("play failed: " + ex.Message);
            }
        }

        private void OnExit(object sender, EventArgs e)
        {
            ExitThread();
        }

        // Debug aid: pop the menu and capture it so its styling can be inspected.
        // Active only when the PLINK_DEBUG environment variable is set.
        private void MaybeStartDebugPreview()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLINK_DEBUG")))
                return;
            Timer timer = new Timer();
            timer.Interval = 900;
            timer.Tick += OnDebugPreviewTick;
            timer.Start();
        }

        private void OnDebugPreviewTick(object sender, EventArgs e)
        {
            Timer timer = (Timer)sender;
            timer.Stop();
            timer.Dispose();
            try
            {
                ContextMenuStrip menu = _trayIcon.ContextMenuStrip;
                menu.Show(260, 200);
                Application.DoEvents();
                if (menu.Items.Count > 3)
                    menu.Items[3].Select();
                Application.DoEvents();
                System.Threading.Thread.Sleep(220);
                Application.DoEvents();
                Rectangle area = new Rectangle(200, 150, 520, 520);
                using (Bitmap bmp = new Bitmap(area.Width, area.Height))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                        g.CopyFromScreen(area.Location, Point.Empty, area.Size);
                    bmp.Save(Path.Combine(Path.GetTempPath(), "Plink-menu.png"), ImageFormat.Png);
                }
                DebugLog.Write("debug menu preview captured");
            }
            catch (Exception ex)
            {
                DebugLog.Write("debug preview failed: " + ex.Message);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                if (_clipboardMonitor != null)
                    _clipboardMonitor.Dispose();
                if (_recycleBinMonitor != null)
                    _recycleBinMonitor.Dispose();
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
                if (_appIcon != null)
                    _appIcon.Dispose();
                if (_copyPlayer != null)
                    _copyPlayer.Dispose();
                if (_deletePlayer != null)
                    _deletePlayer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
