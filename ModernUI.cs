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
        public Color Check;
    }

    // Reads the current Windows light/dark setting.
    internal static class Win11Theme
    {
        public static MenuTheme Resolve()
        {
            MenuTheme t = new MenuTheme();
            if (IsLightTheme())
            {
                t.Background = Color.FromArgb(250, 250, 250);
                t.Text = Color.FromArgb(24, 24, 24);
                t.DisabledText = Color.FromArgb(130, 130, 130);
                t.Hover = Color.FromArgb(238, 238, 238);
                t.Separator = Color.FromArgb(220, 220, 220);
                t.Border = Color.FromArgb(210, 210, 210);
                t.Check = t.Text;
            }
            else
            {
                t.Background = Color.FromArgb(31, 31, 31);
                t.Text = Color.FromArgb(248, 248, 248);
                t.DisabledText = Color.FromArgb(130, 130, 130);
                t.Hover = Color.FromArgb(52, 52, 52);
                t.Separator = Color.FromArgb(70, 70, 70);
                t.Border = Color.FromArgb(53, 53, 53);
                t.Check = t.Text;
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

    // A context menu that can trim ToolStripDropDownMenu's unused submenu-arrow
    // gutter while still enforcing a compact modern minimum width.
    internal sealed class CompactContextMenu : ContextMenuStrip
    {
        public int GutterTrim;
        public int MinimumMenuWidth;

        public override Size GetPreferredSize(Size proposedSize)
        {
            Size s = base.GetPreferredSize(proposedSize);
            if (GutterTrim > 0)
                s.Width = Math.Max(16, s.Width - GutterTrim);
            if (MinimumMenuWidth > 0)
                s.Width = Math.Max(s.Width, MinimumMenuWidth);
            return s;
        }
    }

    // Flat, Windows 11-styled renderer for the tray context menu.
    internal sealed class Win11MenuRenderer : ToolStripRenderer
    {
        private const int MenuCornerRadius = 11;
        private const int HoverCornerRadius = 5;
        private const int ItemTextLeft = 28;
        private const int ItemTextRight = 4;
        private const int CheckLeft = 12;
        private const int SeparatorInset = 12;

        public MenuTheme Theme;

        public Win11MenuRenderer()
        {
            Theme = Win11Theme.Resolve();
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
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
            using (GraphicsPath path = RoundedRect(r, Scale(e.Graphics, MenuCornerRadius)))
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
                int xPad = Scale(e.Graphics, 4);
                int yPad = Scale(e.Graphics, 2);
                Rectangle hl = new Rectangle(
                    r.X + xPad,
                    r.Y + yPad,
                    r.Width - xPad * 2,
                    r.Height - yPad * 2);
                if (hl.Width > 0 && hl.Height > 0)
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (SolidBrush hb = new SolidBrush(Theme.Hover))
                    using (GraphicsPath path = RoundedRect(hl, Scale(e.Graphics, HoverCornerRadius)))
                        e.Graphics.FillPath(hb, path);
                }
            }

            ToolStripMenuItem item = e.Item as ToolStripMenuItem;
            if (item != null && item.Checked)
                DrawCheck(e.Graphics, e.Item);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            e.TextColor = e.Item.Enabled ? Theme.Text : Theme.DisabledText;
            int left = Scale(e.Graphics, ItemTextLeft);
            int right = Scale(e.Graphics, ItemTextRight);
            e.TextRectangle = new Rectangle(
                left,
                0,
                Math.Max(1, e.Item.Width - left - right),
                e.Item.Height);
            e.TextFormat = TextFormatFlags.Left
                | TextFormatFlags.VerticalCenter
                | TextFormatFlags.EndEllipsis
                | TextFormatFlags.NoPrefix;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            // Checks are drawn from OnRenderMenuItemBackground so their position
            // is independent of ToolStrip's built-in check/image margins.
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            Rectangle r = new Rectangle(Point.Empty, e.Item.Size);
            using (SolidBrush b = new SolidBrush(Theme.Background))
                e.Graphics.FillRectangle(b, r);
            int y = r.Y + r.Height / 2;
            int inset = Scale(e.Graphics, SeparatorInset);
            using (Pen p = new Pen(Theme.Separator))
                e.Graphics.DrawLine(p, r.X + inset, y, r.Right - inset, y);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            Rectangle r = e.ArrowRectangle;
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            float size = Scale(e.Graphics, 4);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen p = new Pen(e.Item.Enabled ? Theme.Text : Theme.DisabledText, Scale(e.Graphics, 1.6f)))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                e.Graphics.DrawLines(p, new PointF[]
                {
                    new PointF(cx - size * 0.45f, cy - size),
                    new PointF(cx + size * 0.45f, cy),
                    new PointF(cx - size * 0.45f, cy + size)
                });
            }
        }

        private void DrawCheck(Graphics g, ToolStripItem item)
        {
            float scale = g.DpiX / 96f;
            float x = CheckLeft * scale;
            float cy = item.Height / 2f;
            float width = 10.5f * scale;
            float height = 7.5f * scale;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen p = new Pen(item.Enabled ? Theme.Check : Theme.DisabledText, Math.Max(1.6f, 1.8f * scale)))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                g.DrawLines(p, new PointF[]
                {
                    new PointF(x, cy - height * 0.05f),
                    new PointF(x + width * 0.35f, cy + height * 0.38f),
                    new PointF(x + width, cy - height * 0.48f)
                });
            }
        }

        private static int Scale(Graphics g, int value)
        {
            return (int)Math.Round(value * g.DpiX / 96f);
        }

        private static float Scale(Graphics g, float value)
        {
            return value * g.DpiX / 96f;
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
