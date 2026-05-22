using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Plink
{
    // Resolved color set for the menu, derived from the current Windows theme.
    internal sealed class MenuTheme
    {
        public Color Background;
        public Color Text;
        public Color DisabledText;
        public Color Hover;
        public Color Separator;
        public Color Border;
    }

    // Reads the current Windows light/dark setting.
    internal static class Win11Theme
    {
        public static MenuTheme Resolve()
        {
            MenuTheme t = new MenuTheme();
            if (IsLightTheme())
            {
                t.Background = Color.FromArgb(249, 249, 249);
                t.Text = Color.FromArgb(26, 26, 26);
                t.DisabledText = Color.FromArgb(156, 156, 156);
                t.Hover = Color.FromArgb(237, 237, 237);
                t.Separator = Color.FromArgb(229, 229, 229);
                t.Border = Color.FromArgb(219, 219, 219);
            }
            else
            {
                t.Background = Color.FromArgb(44, 44, 44);
                t.Text = Color.FromArgb(245, 245, 245);
                t.DisabledText = Color.FromArgb(125, 125, 125);
                t.Hover = Color.FromArgb(59, 59, 59);
                t.Separator = Color.FromArgb(64, 64, 64);
                t.Border = Color.FromArgb(64, 64, 64);
            }
            return t;
        }

        private static bool IsLightTheme()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    "Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", false))
                {
                    if (key != null)
                    {
                        object v = key.GetValue("AppsUseLightTheme");
                        if (v != null)
                            return Convert.ToInt32(v) != 0;
                    }
                }
            }
            catch
            {
            }
            return true;
        }
    }

    // A context menu that reports a slimmer preferred width. ToolStripDropDownMenu
    // reserves a submenu-arrow gutter on the right of every item, and none of ours
    // open a submenu, so GutterTrim shaves that dead width off the menu.
    // Win11MenuRenderer.OnRenderItemText widens each label's text rectangle by the
    // same amount, so the text is drawn into the reclaimed space and never clipped.
    internal sealed class CompactContextMenu : ContextMenuStrip
    {
        public int GutterTrim;

        public override Size GetPreferredSize(Size proposedSize)
        {
            Size s = base.GetPreferredSize(proposedSize);
            if (GutterTrim > 0)
                s.Width = Math.Max(16, s.Width - GutterTrim);
            return s;
        }
    }

    // Flat, Windows 11-styled renderer for the tray context menu.
    internal sealed class Win11MenuRenderer : ToolStripRenderer
    {
        public MenuTheme Theme;

        public Win11MenuRenderer()
        {
            Theme = Win11Theme.Resolve();
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (SolidBrush b = new SolidBrush(Theme.Background))
                e.Graphics.FillRectangle(b, e.ToolStrip.ClientRectangle);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            Rectangle r = e.ToolStrip.ClientRectangle;
            r.Width -= 1;
            r.Height -= 1;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen p = new Pen(Theme.Border))
            using (GraphicsPath path = RoundedRect(r, 8))
                e.Graphics.DrawPath(p, path);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(Theme.Background))
                e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            Rectangle r = new Rectangle(Point.Empty, e.Item.Size);
            using (SolidBrush b = new SolidBrush(Theme.Background))
                e.Graphics.FillRectangle(b, r);

            if (e.Item.Selected && e.Item.Enabled)
            {
                Rectangle hl = new Rectangle(r.X + 4, r.Y + 1, r.Width - 8, r.Height - 2);
                if (hl.Width > 0 && hl.Height > 0)
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (SolidBrush hb = new SolidBrush(Theme.Hover))
                    using (GraphicsPath path = RoundedRect(hl, 5))
                        e.Graphics.FillPath(hb, path);
                }
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            e.TextColor = e.Item.Enabled ? Theme.Text : Theme.DisabledText;
            Rectangle r = e.TextRectangle;
            // Give the label back the width that CompactContextMenu trimmed from
            // the menu, so the gutter reclaim never eats into the text itself.
            CompactContextMenu menu = e.ToolStrip as CompactContextMenu;
            int extra = menu != null ? menu.GutterTrim : 0;
            e.TextRectangle = new Rectangle(r.X, e.Item.ContentRectangle.Y, r.Width + extra, e.Item.ContentRectangle.Height);
            e.TextFormat = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            ToolStripMenuItem item = e.Item as ToolStripMenuItem;
            if (item == null || !item.Checked)
                return;

            Rectangle imgRect = e.ImageRectangle;
            float cy = e.Item.ContentRectangle.Y + e.Item.ContentRectangle.Height / 2f;
            float cx = imgRect.X + imgRect.Width / 2f;
            float sz = Math.Min(imgRect.Width, imgRect.Height) * 0.52f;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float thickness = Math.Max(2.0f, sz * 0.28f);
            using (Pen p = new Pen(Theme.Text, thickness))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                PointF a = new PointF(cx - sz * 0.48f, cy);
                PointF b = new PointF(cx - sz * 0.08f, cy + sz * 0.38f);
                PointF c2 = new PointF(cx + sz * 0.52f, cy - sz * 0.38f);
                e.Graphics.DrawLines(p, new PointF[] { a, b, c2 });
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            Rectangle r = new Rectangle(Point.Empty, e.Item.Size);
            using (SolidBrush b = new SolidBrush(Theme.Background))
                e.Graphics.FillRectangle(b, r);
            int y = r.Y + r.Height / 2;
            using (Pen p = new Pen(Theme.Separator))
                e.Graphics.DrawLine(p, r.X + 12, y, r.Right - 12, y);
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            if (d < 2)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // Applies the Windows 11 rounded-corner window style via DWM.
    internal static class WindowEffects
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        public static void RoundCorners(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return;
            try
            {
                int pref = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
            }
            catch
            {
            }
        }
    }
}
