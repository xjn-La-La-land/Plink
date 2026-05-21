using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Plink
{
    // Raises ClipboardUpdated whenever the clipboard content changes, via a
    // message-only window subscribed to WM_CLIPBOARDUPDATE.
    internal sealed class ClipboardMonitor : NativeWindow, IDisposable
    {
        private const int WM_CLIPBOARDUPDATE = 0x031D;
        private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        public event EventHandler ClipboardUpdated;

        public ClipboardMonitor()
        {
            CreateParams cp = new CreateParams();
            cp.Parent = HWND_MESSAGE;
            CreateHandle(cp);
            AddClipboardFormatListener(Handle);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_CLIPBOARDUPDATE)
            {
                EventHandler handler = ClipboardUpdated;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                RemoveClipboardFormatListener(Handle);
                DestroyHandle();
            }
        }
    }
}
