using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RazerBatteryTray
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            try
            {
                SetProcessDPIAware();
            }
            catch { }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => {
                MessageBox.Show("程序发生异常: " + e.Exception.Message + "\n\n" + e.Exception.StackTrace, "雷蛇电量助手 - 错误提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                Exception ex = e.ExceptionObject as Exception;
                string msg = ex != null ? (ex.Message + "\n\n" + ex.StackTrace) : "未知系统错误";
                MessageBox.Show("未处理的致命异常: " + msg, "雷蛇电量助手 - 致命错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            // Ensure single instance by terminating old/stale instances
            try
            {
                Process current = Process.GetCurrentProcess();
                foreach (Process p in Process.GetProcessesByName("RazerBatteryTray"))
                {
                    if (p.Id != current.Id)
                    {
                        try { p.Kill(); p.WaitForExit(500); } catch { }
                    }
                }
            }
            catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class MouseBatteryInfo
    {
        public bool IsConnected { get; set; }
        public string DeviceName { get; set; }
        public int BatteryPercent { get; set; }
        public bool IsCharging { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    #region Modern UI Custom Controls

    public class RoundedCard : Panel
    {
        public Color BorderColor { get; set; }
        public int CornerRadius { get; set; }

        public RoundedCard()
        {
            BorderColor = Color.FromArgb(42, 46, 58);
            CornerRadius = 10;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.BackColor = Color.FromArgb(25, 27, 34);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            base.OnPaint(pevent);
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = GetRoundedRectangle(rect, CornerRadius))
            {
                using (SolidBrush brush = new SolidBrush(BackColor))
                {
                    g.FillPath(brush, path);
                }

                if (BorderColor != Color.Transparent)
                {
                    using (Pen pen = new Pen(BorderColor, 1f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }
        }

        public static GraphicsPath GetRoundedRectangle(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d > bounds.Width) d = bounds.Width;
            if (d > bounds.Height) d = bounds.Height;
            if (d <= 0) d = 1;

            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static GraphicsPath GetRoundRectF(RectangleF bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class ModernButton : Control
    {
        private bool isHovered = false;
        private bool isPressed = false;

        public Color NormalColor { get; set; }
        public Color HoverColor { get; set; }
        public Color PressedColor { get; set; }
        public Color BorderColor { get; set; }
        public int CornerRadius { get; set; }

        public ModernButton()
        {
            NormalColor = Color.FromArgb(0, 200, 83);
            HoverColor = Color.FromArgb(0, 230, 118);
            PressedColor = Color.FromArgb(0, 150, 60);
            BorderColor = Color.Transparent;
            CornerRadius = 8;

            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = Color.Transparent;
            this.Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { isHovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            if (mevent.Button == MouseButtons.Left)
            {
                isPressed = true;
                Invalidate();
            }
            base.OnMouseDown(mevent);
        }
        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            isPressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Color bg = isPressed ? PressedColor : (isHovered ? HoverColor : NormalColor);

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedCard.GetRoundedRectangle(rect, CornerRadius))
            {
                using (SolidBrush brush = new SolidBrush(bg))
                {
                    g.FillPath(brush, path);
                }

                if (BorderColor != Color.Transparent)
                {
                    using (Pen pen = new Pen(BorderColor, 1f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            TextRenderer.DrawText(g, Text, Font, ClientRectangle, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    public class ModernCheckBox : Control
    {
        private bool isChecked = false;
        private bool isHovered = false;

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (isChecked != value)
                {
                    isChecked = value;
                    Invalidate();
                    if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
                }
            }
        }

        public ModernCheckBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = Color.Transparent;
            this.Cursor = Cursors.Hand;
            this.ForeColor = Color.FromArgb(215, 222, 235);
        }

        protected override void OnMouseEnter(EventArgs e) { isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { isHovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int boxSize = (int)Math.Max(16, 16 * (g.DpiX / 96.0f));
            int boxY = (Height - boxSize) / 2;
            Rectangle boxRect = new Rectangle(0, boxY, boxSize, boxSize);

            using (GraphicsPath path = RoundedCard.GetRoundedRectangle(boxRect, 4))
            {
                if (isChecked)
                {
                    Color checkBg = isHovered ? Color.FromArgb(0, 245, 125) : Color.FromArgb(0, 200, 83);
                    using (SolidBrush b = new SolidBrush(checkBg))
                    {
                        g.FillPath(b, path);
                    }
                    using (Pen checkPen = new Pen(Color.FromArgb(10, 20, 15), Math.Max(1.8f, 1.8f * (g.DpiX / 96.0f))))
                    {
                        checkPen.StartCap = LineCap.Round;
                        checkPen.EndCap = LineCap.Round;
                        float x1 = boxRect.X + boxSize * 0.25f;
                        float y1 = boxRect.Y + boxSize * 0.50f;
                        float x2 = boxRect.X + boxSize * 0.45f;
                        float y2 = boxRect.Y + boxSize * 0.72f;
                        float x3 = boxRect.X + boxSize * 0.78f;
                        float y3 = boxRect.Y + boxSize * 0.28f;
                        g.DrawLines(checkPen, new PointF[] { new PointF(x1, y1), new PointF(x2, y2), new PointF(x3, y3) });
                    }
                }
                else
                {
                    Color boxBg = isHovered ? Color.FromArgb(38, 42, 54) : Color.FromArgb(28, 31, 40);
                    Color borderC = isHovered ? Color.FromArgb(80, 88, 110) : Color.FromArgb(55, 60, 75);
                    using (SolidBrush b = new SolidBrush(boxBg))
                    {
                        g.FillPath(b, path);
                    }
                    using (Pen p = new Pen(borderC, 1.2f))
                    {
                        g.DrawPath(p, path);
                    }
                }
            }

            int textX = boxSize + (int)(8 * (g.DpiX / 96.0f));
            Rectangle textRect = new Rectangle(textX, 0, Width - textX, Height);
            Color textColor = isHovered ? Color.White : ForeColor;
            TextRenderer.DrawText(g, Text, Font, textRect, textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    public class ModernSegmentButton : Control
    {
        private bool isSelected = false;
        private bool isHovered = false;

        public event EventHandler SelectedChanged;

        public bool Selected
        {
            get { return isSelected; }
            set
            {
                if (isSelected != value)
                {
                    isSelected = value;
                    Invalidate();
                    if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty);
                }
            }
        }

        public int CornerRadius { get; set; }

        public ModernSegmentButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = Color.Transparent;
            this.Cursor = Cursors.Hand;
            CornerRadius = 6;
        }

        protected override void OnMouseEnter(EventArgs e) { isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { isHovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            float dpi = g.DpiX / 96.0f;
            if (dpi < 1.0f) dpi = 1.0f;

            Color bg = isSelected ? Color.FromArgb(18, 48, 30) : (isHovered ? Color.FromArgb(36, 40, 52) : Color.FromArgb(26, 29, 38));
            Color border = isSelected ? Color.FromArgb(0, 230, 118) : (isHovered ? Color.FromArgb(68, 76, 96) : Color.FromArgb(44, 49, 64));
            Color textC = isSelected ? Color.FromArgb(0, 230, 118) : (isHovered ? Color.White : Color.FromArgb(170, 178, 195));

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            int rad = (int)(CornerRadius * dpi);
            using (GraphicsPath path = RoundedCard.GetRoundedRectangle(r, rad))
            {
                using (SolidBrush b = new SolidBrush(bg))
                    g.FillPath(b, path);
                using (Pen p = new Pen(border, isSelected ? Math.Max(1.4f, 1.4f * dpi) : 1f))
                    g.DrawPath(p, path);
            }

            Font drawFont = isSelected ? new Font(Font.FontFamily, Font.Size, FontStyle.Bold) : Font;
            TextRenderer.DrawText(g, Text, drawFont, ClientRectangle, textC,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    public class SubtleDivider : Control
    {
        public SubtleDivider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (Pen p = new Pen(Color.FromArgb(38, 43, 56), 1f))
            {
                e.Graphics.DrawLine(p, 0, Height / 2, Width, Height / 2);
            }
        }
    }

    public class ModernProgressBar : Control
    {
        private int val = 0;
        public int Value
        {
            get { return val; }
            set {
                int clamped = Math.Max(0, Math.Min(100, value));
                if (this.val != clamped)
                {
                    this.val = clamped;
                    Invalidate();
                }
            }
        }

        public Color TrackColor { get; set; }
        public Color ProgressColor { get; set; }

        public ModernProgressBar()
        {
            TrackColor = Color.FromArgb(38, 42, 53);
            ProgressColor = Color.FromArgb(0, 230, 118);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int h = Height - 1;
            Rectangle trackRect = new Rectangle(0, 0, Width - 1, h);
            using (GraphicsPath trackPath = GetPillPath(trackRect))
            using (SolidBrush trackBrush = new SolidBrush(TrackColor))
            {
                g.FillPath(trackBrush, trackPath);
            }

            if (val > 0)
            {
                int fillWidth = (int)Math.Max(h, (Width * (val / 100.0)));
                fillWidth = Math.Min(Width - 1, fillWidth);
                Rectangle fillRect = new Rectangle(0, 0, fillWidth, h);
                using (GraphicsPath fillPath = GetPillPath(fillRect))
                using (SolidBrush fillBrush = new SolidBrush(ProgressColor))
                {
                    g.FillPath(fillBrush, fillPath);
                }
            }
        }

        private GraphicsPath GetPillPath(Rectangle bounds)
        {
            GraphicsPath path = new GraphicsPath();
            int d = bounds.Height;
            if (d <= 0 || bounds.Width <= d)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(bounds.X, bounds.Y, d, d, 90, 180);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 180);
            path.CloseFigure();
            return path;
        }
    }

    public class StatusPill : Control
    {
        private string statusText = "检测中...";
        private Color statusColor = Color.FromArgb(0, 230, 118);

        public void SetStatus(string text, Color color)
        {
            this.statusText = text;
            this.statusColor = color;
            Invalidate();
        }

        public StatusPill()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedCard.GetRoundedRectangle(rect, Height / 2))
            {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(35, statusColor.R, statusColor.G, statusColor.B)))
                {
                    g.FillPath(brush, path);
                }
                using (Pen pen = new Pen(Color.FromArgb(100, statusColor.R, statusColor.G, statusColor.B), 1.2f))
                {
                    g.DrawPath(pen, path);
                }
            }

            TextRenderer.DrawText(g, statusText, Font, ClientRectangle, statusColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    public class ModernDarkMenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly Color bgCol = Color.FromArgb(24, 27, 34);
        private readonly Color borderCol = Color.FromArgb(48, 54, 68);
        private readonly Color hoverCol = Color.FromArgb(38, 44, 58);
        private readonly Color hoverBorder = Color.FromArgb(60, 68, 86);
        private readonly Color textCol = Color.FromArgb(235, 240, 248);
        private readonly Color disabledCol = Color.FromArgb(120, 128, 144);
        private readonly Color separatorCol = Color.FromArgb(42, 48, 62);
        private readonly Color accentGreen = Color.FromArgb(0, 230, 118);

        public ModernDarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(bgCol))
            {
                e.Graphics.FillRectangle(b, e.AffectedBounds);
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (Pen p = new Pen(borderCol, 1f))
            {
                Rectangle r = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
                e.Graphics.DrawRectangle(p, r);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected && e.Item.Enabled)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
                using (GraphicsPath path = RoundedCard.GetRoundedRectangle(r, 4))
                {
                    using (SolidBrush b = new SolidBrush(hoverCol))
                    {
                        e.Graphics.FillPath(b, path);
                    }
                    using (Pen p = new Pen(hoverBorder, 1f))
                    {
                        e.Graphics.DrawPath(p, path);
                    }
                }
            }
            else if (e.Item.OwnerItem != null && e.Item.Pressed)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
                using (GraphicsPath path = RoundedCard.GetRoundedRectangle(r, 4))
                using (SolidBrush b = new SolidBrush(hoverCol))
                {
                    e.Graphics.FillPath(b, path);
                }
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            Color color = textCol;
            if (!e.Item.Enabled)
            {
                color = disabledCol;
            }
            else if (e.Item.Tag != null && e.Item.Tag.ToString() == "Header")
            {
                color = accentGreen;

                // Draw status dot in left image margin column (exactly aligned with checkmarks)
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                float dotY = e.Item.Height / 2f;
                float dotX = 17f;
                float radius = 3.5f;
                using (SolidBrush dotBrush = new SolidBrush(accentGreen))
                {
                    e.Graphics.FillEllipse(dotBrush, dotX - radius, dotY - radius, radius * 2f, radius * 2f);
                }
            }
            else if (e.Item.Selected)
            {
                color = Color.White;
            }

            // Text is drawn starting at e.TextRectangle.X (aligning Header 'R' with '打' below)
            Rectangle textRect = new Rectangle(e.TextRectangle.X, 0, e.TextRectangle.Width, e.Item.Height);
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, textRect, color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (Pen p = new Pen(separatorCol, 1f))
            {
                e.Graphics.DrawLine(p, 8, y, e.Item.Width - 8, y);
            }
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float centerY = e.Item.Height / 2f;
            float cx = 17f;

            PointF[] pts = new PointF[] {
                new PointF(cx - 5.0f, centerY - 0.5f),
                new PointF(cx - 1.5f, centerY + 3.5f),
                new PointF(cx + 5.5f, centerY - 4.5f)
            };
            using (Pen p = new Pen(accentGreen, 2.2f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                e.Graphics.DrawLines(p, pts);
            }
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float centerY = e.Item.Height / 2f;
            float cx = e.ArrowRectangle.X + e.ArrowRectangle.Width / 2f;

            Color arrowColor = e.Item.Selected ? Color.White : Color.FromArgb(145, 155, 175);
            PointF[] arrow = new PointF[] {
                new PointF(cx - 2.5f, centerY - 4.5f),
                new PointF(cx + 2.0f, centerY),
                new PointF(cx - 2.5f, centerY + 4.5f)
            };
            using (Pen p = new Pen(arrowColor, 1.8f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                e.Graphics.DrawLines(p, arrow);
            }
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

        private class DarkColorTable : ProfessionalColorTable
        {
            public override Color MenuBorder { get { return Color.FromArgb(48, 54, 68); } }
            public override Color MenuItemBorder { get { return Color.Transparent; } }
            public override Color MenuItemSelected { get { return Color.FromArgb(38, 44, 58); } }
            public override Color ToolStripDropDownBackground { get { return Color.FromArgb(24, 27, 34); } }
            public override Color ImageMarginGradientBegin { get { return Color.FromArgb(24, 27, 34); } }
            public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(24, 27, 34); } }
            public override Color ImageMarginGradientEnd { get { return Color.FromArgb(24, 27, 34); } }
        }
    }

    #endregion

    public class MainForm : Form
    {
        // Windows 11 DWM Immersive Dark Mode & Styling API
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        const int DWMWA_BORDER_COLOR = 34;
        const int DWMWA_CAPTION_COLOR = 35;
        const int DWMWA_TEXT_COLOR = 36;

        // Hardware Change Notifications (USB Plug/Unplug, Wireless/Wired switch)
        private const int WM_DEVICECHANGE = 0x0219;
        private const int DBT_DEVICEARRIVAL = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        private const int DBT_DEVNODES_CHANGED = 0x0007;

        [StructLayout(LayoutKind.Sequential)]
        struct DEV_BROADCAST_DEVICEINTERFACE
        {
            public int dbcc_size;
            public int dbcc_devicetype;
            public int dbcc_reserved;
            public Guid dbcc_classguid;
            public short dbcc_name;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr RegisterDeviceNotification(IntPtr hRecipient, IntPtr NotificationFilter, uint Flags);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnregisterDeviceNotification(IntPtr Handle);

        const uint DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;
        const int DBT_DEVTYP_DEVICEINTERFACE = 5;

        private IntPtr hDevNotify = IntPtr.Zero;
        private System.Windows.Forms.Timer deviceChangeTimer1;
        private System.Windows.Forms.Timer deviceChangeTimer2;
        private int userSelectedInterval = 60000;

        private NotifyIcon trayIcon;
        private ContextMenuStrip contextMenu;
        private ToolStripMenuItem statusMenuItem;
        private ToolStripMenuItem autoStartMenuItem;
        private ToolStripMenuItem lowBatteryAlertMenuItem;
        private ToolStripMenuItem styleCapsuleItem;
        private ToolStripMenuItem styleNumItem;
        private ToolStripMenuItem int30sMenuItem;
        private ToolStripMenuItem int1mMenuItem;
        private ToolStripMenuItem int5mMenuItem;
        private System.Windows.Forms.Timer updateTimer;

        // Visual controls - Card 1: Status
        private RoundedCard cardBattery;
        private Label lblDeviceName;
        private Label lblConnDot;
        private Label lblBatteryBig;
        private StatusPill pillStatus;
        private ModernProgressBar barBattery;
        private Label lblUpdateTime;

        // Visual controls - Card 2: Settings & Preferences
        private RoundedCard cardSettings;
        private Label lblSettingsTitle;
        private Label lblStyleTitle;
        private ModernSegmentButton btnStyleCapsule;
        private ModernSegmentButton btnStyleNum;
        private Label lblIntervalTitle;
        private ModernSegmentButton btnInt30s;
        private ModernSegmentButton btnInt1m;
        private ModernSegmentButton btnInt5m;
        private SubtleDivider divSettings;
        private ModernCheckBox chkAutoStart;
        private ModernCheckBox chkLowAlert;
        private Label lblSettingsTip;

        // Visual controls - Card 3: Actions
        private ModernButton btnRefresh;
        private ModernButton btnHideToTray;

        private const string AppName = "RazerBatteryTray";
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ConfigRegistryKey = @"Software\RazerBatteryTray";

        private bool lowBatteryAlertEnabled = true;
        private bool lastLowAlertFired = false;
        private bool isUpdatingUI = false;
        private int trayStyle = 0; // 0 = Capsule, 1 = Ring, 2 = Number
        private float dpiScale = 1.0f;
        private MouseBatteryInfo lastInfo = null;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool DestroyIcon(IntPtr handle);

        public MainForm()
        {
            using (Graphics g = this.CreateGraphics())
            {
                dpiScale = g.DpiX / 96.0f;
                if (dpiScale < 1.0f) dpiScale = 1.0f;
            }

            // Set high-resolution application icon for window and taskbar
            try
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(icoPath))
                {
                    this.Icon = new Icon(icoPath);
                }
                else
                {
                    this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
            }
            catch { }

            InitializeFormUI();
            InitializeTray();
            LoadConfig();

            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Interval = userSelectedInterval;
            updateTimer.Tick += (s, e) => RefreshBatteryStatus(false);
            updateTimer.Start();

            RefreshBatteryStatus(false);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyModernWin11Theme();
            RegisterUsbNotification();
        }

        private void RegisterUsbNotification()
        {
            try
            {
                Guid hidGuid;
                RazerDeviceHelper.HidD_GetHidGuid(out hidGuid);

                DEV_BROADCAST_DEVICEINTERFACE dbi = new DEV_BROADCAST_DEVICEINTERFACE();
                dbi.dbcc_size = Marshal.SizeOf(dbi);
                dbi.dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE;
                dbi.dbcc_reserved = 0;
                dbi.dbcc_classguid = hidGuid;

                IntPtr buffer = Marshal.AllocHGlobal(dbi.dbcc_size);
                try
                {
                    Marshal.StructureToPtr(dbi, buffer, true);
                    hDevNotify = RegisterDeviceNotification(this.Handle, buffer, DEVICE_NOTIFY_WINDOW_HANDLE);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch { }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_DEVICECHANGE)
            {
                int wp = m.WParam.ToInt32();
                if (wp == DBT_DEVICEARRIVAL || wp == DBT_DEVICEREMOVECOMPLETE || wp == DBT_DEVNODES_CHANGED)
                {
                    OnDeviceHardwareChange();
                }
            }
            base.WndProc(ref m);
        }

        private void OnDeviceHardwareChange()
        {
            // Two-stage proactive refresh: Stage 1 at 250ms, Stage 2 at 1000ms (after firmware handshake)
            if (deviceChangeTimer1 == null)
            {
                deviceChangeTimer1 = new System.Windows.Forms.Timer();
                deviceChangeTimer1.Tick += (s, e) =>
                {
                    deviceChangeTimer1.Stop();
                    RefreshBatteryStatus(false);
                };
            }
            deviceChangeTimer1.Stop();
            deviceChangeTimer1.Interval = 250;
            deviceChangeTimer1.Start();

            if (deviceChangeTimer2 == null)
            {
                deviceChangeTimer2 = new System.Windows.Forms.Timer();
                deviceChangeTimer2.Tick += (s, e) =>
                {
                    deviceChangeTimer2.Stop();
                    RefreshBatteryStatus(false);
                };
            }
            deviceChangeTimer2.Stop();
            deviceChangeTimer2.Interval = 1000;
            deviceChangeTimer2.Start();
        }

        private void ApplyModernWin11Theme()
        {
            try
            {
                // Dark mode title bar
                int trueVal = 1;
                DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref trueVal, sizeof(int));
                DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref trueVal, sizeof(int));

                // Win11 rounded window corners (2 = DWMWCP_ROUND)
                int roundVal = 2;
                DwmSetWindowAttribute(this.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref roundVal, sizeof(int));

                // Match caption color to window background #121317 (0x00171312 BGR)
                int captionBg = 0x00171312;
                DwmSetWindowAttribute(this.Handle, DWMWA_CAPTION_COLOR, ref captionBg, sizeof(int));

                // Caption text color: White
                int captionText = 0x00FFFFFF;
                DwmSetWindowAttribute(this.Handle, DWMWA_TEXT_COLOR, ref captionText, sizeof(int));

                // Subtle border color #2A2E3A (0x003A2E2A BGR)
                int borderCol = 0x003A2E2A;
                DwmSetWindowAttribute(this.Handle, DWMWA_BORDER_COLOR, ref borderCol, sizeof(int));
            }
            catch { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                this.TopMost = true;
                this.BringToFront();
                this.Activate();
                this.TopMost = false;
            }
            catch { }
        }

        private void InitializeFormUI()
        {
            this.Text = "雷蛇电量管家";
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(18, 19, 23); // Deep space obsidian #121317
            this.ForeColor = Color.White;
            this.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.ShowInTaskbar = true;

            // Dimensions calculated with high-DPI scaling
            int padX = (int)(18 * dpiScale);
            int baseW = (int)(460 * dpiScale);
            int cardW = baseW - (padX * 2);

            // ================= CARD 1: BATTERY STATUS =================
            int card1Y = (int)(14 * dpiScale);
            int card1H = (int)(176 * dpiScale);

            cardBattery = new RoundedCard();
            cardBattery.Location = new Point(padX, card1Y);
            cardBattery.Size = new Size(cardW, card1H);
            cardBattery.CornerRadius = (int)(10 * dpiScale);
            cardBattery.BackColor = Color.FromArgb(25, 27, 34);
            cardBattery.BorderColor = Color.FromArgb(42, 46, 58);
            this.Controls.Add(cardBattery);

            // Top Row: Device Name + Connection status
            lblDeviceName = new Label();
            lblDeviceName.Text = "正在检测雷蛇设备...";
            lblDeviceName.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            lblDeviceName.ForeColor = Color.White;
            lblDeviceName.Location = new Point((int)(18 * dpiScale), (int)(16 * dpiScale));
            lblDeviceName.AutoSize = true;
            cardBattery.Controls.Add(lblDeviceName);

            lblConnDot = new Label();
            lblConnDot.Text = "● 已连接";
            lblConnDot.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            lblConnDot.ForeColor = Color.FromArgb(0, 230, 118);
            lblConnDot.Location = new Point(cardW - (int)(105 * dpiScale), (int)(18 * dpiScale));
            lblConnDot.Size = new Size((int)(90 * dpiScale), (int)(22 * dpiScale));
            lblConnDot.TextAlign = ContentAlignment.MiddleRight;
            cardBattery.Controls.Add(lblConnDot);

            // Middle Row: Big Percentage + Status Pill
            lblBatteryBig = new Label();
            lblBatteryBig.Text = "--%";
            lblBatteryBig.Font = new Font("Microsoft YaHei UI", 34F, FontStyle.Bold, GraphicsUnit.Point);
            lblBatteryBig.ForeColor = Color.FromArgb(0, 230, 118);
            lblBatteryBig.Location = new Point((int)(16 * dpiScale), (int)(46 * dpiScale));
            lblBatteryBig.AutoSize = true;
            cardBattery.Controls.Add(lblBatteryBig);

            pillStatus = new StatusPill();
            pillStatus.Location = new Point((int)(180 * dpiScale), (int)(64 * dpiScale));
            pillStatus.Size = new Size((int)(115 * dpiScale), (int)(32 * dpiScale));
            pillStatus.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point);
            pillStatus.SetStatus("⚡ 充电中", Color.FromArgb(0, 230, 118));
            cardBattery.Controls.Add(pillStatus);

            // Battery Progress Bar
            barBattery = new ModernProgressBar();
            barBattery.Location = new Point((int)(18 * dpiScale), (int)(118 * dpiScale));
            barBattery.Size = new Size(cardW - (int)(36 * dpiScale), (int)(10 * dpiScale));
            barBattery.Value = 0;
            barBattery.TrackColor = Color.FromArgb(38, 42, 53);
            barBattery.ProgressColor = Color.FromArgb(0, 230, 118);
            cardBattery.Controls.Add(barBattery);

            // Sync Time Footer in Card 1
            lblUpdateTime = new Label();
            lblUpdateTime.Text = "最后同步: --:--:-- · 自动侦测硬件插拔";
            lblUpdateTime.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
            lblUpdateTime.ForeColor = Color.FromArgb(135, 142, 156);
            lblUpdateTime.Location = new Point((int)(18 * dpiScale), (int)(142 * dpiScale));
            lblUpdateTime.AutoSize = true;
            cardBattery.Controls.Add(lblUpdateTime);

            // ================= CARD 2: CONFIGURATION & PREFERENCES =================
            int card2Y = card1Y + card1H + (int)(12 * dpiScale);
            int card2H = (int)(194 * dpiScale);

            cardSettings = new RoundedCard();
            cardSettings.Location = new Point(padX, card2Y);
            cardSettings.Size = new Size(cardW, card2H);
            cardSettings.CornerRadius = (int)(10 * dpiScale);
            cardSettings.BackColor = Color.FromArgb(25, 27, 34);
            cardSettings.BorderColor = Color.FromArgb(42, 46, 58);
            this.Controls.Add(cardSettings);

            int cPad = (int)(18 * dpiScale);
            int secW = cardW - (cPad * 2);

            // Section Header
            lblSettingsTitle = new Label();
            lblSettingsTitle.Text = "功能设置与偏好";
            lblSettingsTitle.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            lblSettingsTitle.ForeColor = Color.FromArgb(240, 245, 255);
            lblSettingsTitle.Location = new Point(cPad, (int)(14 * dpiScale));
            lblSettingsTitle.AutoSize = true;
            cardSettings.Controls.Add(lblSettingsTitle);

            // Row 1: 托盘图标样式 (Capsule / Badge)
            int row1Y = (int)(40 * dpiScale);
            int lblTitleW = (int)(95 * dpiScale);
            int segH = (int)(30 * dpiScale);

            lblStyleTitle = new Label();
            lblStyleTitle.Text = "托盘图标样式";
            lblStyleTitle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            lblStyleTitle.ForeColor = Color.FromArgb(160, 168, 185);
            lblStyleTitle.Location = new Point(cPad, row1Y + (int)(5 * dpiScale));
            lblStyleTitle.Size = new Size(lblTitleW, (int)(22 * dpiScale));
            cardSettings.Controls.Add(lblStyleTitle);

            int seg1W = (secW - lblTitleW - (int)(8 * dpiScale)) / 2;
            int seg1X = cPad + lblTitleW;

            btnStyleCapsule = new ModernSegmentButton();
            btnStyleCapsule.Text = "现代胶囊电池";
            btnStyleCapsule.Font = new Font("Microsoft YaHei UI", 8.8F, FontStyle.Regular, GraphicsUnit.Point);
            btnStyleCapsule.Location = new Point(seg1X, row1Y);
            btnStyleCapsule.Size = new Size(seg1W, segH);
            btnStyleCapsule.Click += (s, e) => SetTrayStyle(0, false);
            cardSettings.Controls.Add(btnStyleCapsule);

            btnStyleNum = new ModernSegmentButton();
            btnStyleNum.Text = "醒目数字能量表";
            btnStyleNum.Font = new Font("Microsoft YaHei UI", 8.8F, FontStyle.Regular, GraphicsUnit.Point);
            btnStyleNum.Location = new Point(seg1X + seg1W + (int)(8 * dpiScale), row1Y);
            btnStyleNum.Size = new Size(seg1W, segH);
            btnStyleNum.Click += (s, e) => SetTrayStyle(1, false);
            cardSettings.Controls.Add(btnStyleNum);

            // Row 2: 自动刷新频率 (30s / 1m / 5m)
            int row2Y = (int)(78 * dpiScale);

            lblIntervalTitle = new Label();
            lblIntervalTitle.Text = "自动刷新频率";
            lblIntervalTitle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            lblIntervalTitle.ForeColor = Color.FromArgb(160, 168, 185);
            lblIntervalTitle.Location = new Point(cPad, row2Y + (int)(5 * dpiScale));
            lblIntervalTitle.Size = new Size(lblTitleW, (int)(22 * dpiScale));
            cardSettings.Controls.Add(lblIntervalTitle);

            int seg2Gap = (int)(6 * dpiScale);
            int seg2W = (secW - lblTitleW - (seg2Gap * 2)) / 3;

            btnInt30s = new ModernSegmentButton();
            btnInt30s.Text = "30 秒";
            btnInt30s.Font = new Font("Microsoft YaHei UI", 8.8F, FontStyle.Regular, GraphicsUnit.Point);
            btnInt30s.Location = new Point(seg1X, row2Y);
            btnInt30s.Size = new Size(seg2W, segH);
            btnInt30s.Click += (s, e) => SetInterval(30000, false);
            cardSettings.Controls.Add(btnInt30s);

            btnInt1m = new ModernSegmentButton();
            btnInt1m.Text = "1 分钟";
            btnInt1m.Font = new Font("Microsoft YaHei UI", 8.8F, FontStyle.Regular, GraphicsUnit.Point);
            btnInt1m.Location = new Point(seg1X + seg2W + seg2Gap, row2Y);
            btnInt1m.Size = new Size(seg2W, segH);
            btnInt1m.Click += (s, e) => SetInterval(60000, false);
            cardSettings.Controls.Add(btnInt1m);

            btnInt5m = new ModernSegmentButton();
            btnInt5m.Text = "5 分钟";
            btnInt5m.Font = new Font("Microsoft YaHei UI", 8.8F, FontStyle.Regular, GraphicsUnit.Point);
            btnInt5m.Location = new Point(seg1X + (seg2W + seg2Gap) * 2, row2Y);
            btnInt5m.Size = new Size(seg2W, segH);
            btnInt5m.Click += (s, e) => SetInterval(300000, false);
            cardSettings.Controls.Add(btnInt5m);

            // Row 3: Modern subtle divider
            divSettings = new SubtleDivider();
            divSettings.Location = new Point(cPad, (int)(118 * dpiScale));
            divSettings.Size = new Size(secW, (int)(8 * dpiScale));
            cardSettings.Controls.Add(divSettings);

            // Row 4: Checkboxes
            int chkY = (int)(132 * dpiScale);
            int chkGap = (int)(14 * dpiScale);
            int chkW = (secW - chkGap) / 2;
            int chkH = (int)(26 * dpiScale);

            chkAutoStart = new ModernCheckBox();
            chkAutoStart.Text = "开机自动启动";
            chkAutoStart.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            chkAutoStart.Location = new Point(cPad, chkY);
            chkAutoStart.Size = new Size(chkW, chkH);
            chkAutoStart.CheckedChanged += (s, e) => {
                if (isUpdatingUI) return;
                SetAutoStart(chkAutoStart.Checked);
            };
            cardSettings.Controls.Add(chkAutoStart);

            chkLowAlert = new ModernCheckBox();
            chkLowAlert.Text = "低电量提醒 (≤20%)";
            chkLowAlert.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            chkLowAlert.Location = new Point(cPad + chkW + chkGap, chkY);
            chkLowAlert.Size = new Size(chkW, chkH);
            chkLowAlert.CheckedChanged += (s, e) => {
                if (isUpdatingUI) return;
                SetLowBatteryAlert(chkLowAlert.Checked);
            };
            cardSettings.Controls.Add(chkLowAlert);

            // Row 5: Hint inside Card 2
            lblSettingsTip = new Label();
            lblSettingsTip.Text = "注：切换线缆/接收器时会自动即时同步，无需等待计时周期";
            lblSettingsTip.Font = new Font("Microsoft YaHei UI", 8.2F, FontStyle.Regular, GraphicsUnit.Point);
            lblSettingsTip.ForeColor = Color.FromArgb(120, 128, 142);
            lblSettingsTip.Location = new Point(cPad, (int)(166 * dpiScale));
            lblSettingsTip.AutoSize = true;
            cardSettings.Controls.Add(lblSettingsTip);

            // ================= CARD 3 / BOTTOM ROW: ACTIONS =================
            int card3Y = card2Y + card2H + (int)(12 * dpiScale);
            int btnH = (int)(38 * dpiScale);
            int btnGap = (int)(14 * dpiScale);
            int btnW = (cardW - btnGap) / 2;

            btnRefresh = new ModernButton();
            btnRefresh.Text = "立即刷新";
            btnRefresh.Location = new Point(padX, card3Y);
            btnRefresh.Size = new Size(btnW, btnH);
            btnRefresh.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point);
            btnRefresh.NormalColor = Color.FromArgb(0, 200, 83);
            btnRefresh.HoverColor = Color.FromArgb(0, 230, 118);
            btnRefresh.PressedColor = Color.FromArgb(0, 160, 65);
            btnRefresh.ForeColor = Color.FromArgb(10, 24, 15);
            btnRefresh.CornerRadius = (int)(8 * dpiScale);
            btnRefresh.Click += (s, e) => RefreshBatteryStatus(true);
            this.Controls.Add(btnRefresh);

            btnHideToTray = new ModernButton();
            btnHideToTray.Text = "最小化到托盘";
            btnHideToTray.Location = new Point(padX + btnW + btnGap, card3Y);
            btnHideToTray.Size = new Size(btnW, btnH);
            btnHideToTray.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point);
            btnHideToTray.NormalColor = Color.FromArgb(36, 39, 49);
            btnHideToTray.HoverColor = Color.FromArgb(48, 52, 65);
            btnHideToTray.PressedColor = Color.FromArgb(28, 30, 38);
            btnHideToTray.BorderColor = Color.FromArgb(58, 63, 78);
            btnHideToTray.ForeColor = Color.White;
            btnHideToTray.CornerRadius = (int)(8 * dpiScale);
            btnHideToTray.Click += (s, e) => {
                this.Hide();
                trayIcon.ShowBalloonTip(2000, "已最小化到托盘", "电量管家正在系统托盘运行，随时点击右下角托盘图标唤出。", ToolTipIcon.Info);
            };
            this.Controls.Add(btnHideToTray);

            int clientH = card3Y + btnH + (int)(16 * dpiScale);
            this.ClientSize = new Size(baseW, clientH);
        }

        private void InitializeTray()
        {
            contextMenu = new ContextMenuStrip();
            contextMenu.Renderer = new ModernDarkMenuRenderer();
            contextMenu.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            contextMenu.ShowImageMargin = true;
            contextMenu.ShowCheckMargin = false;
            contextMenu.Padding = new Padding(3, 4, 3, 4);

            statusMenuItem = new ToolStripMenuItem("正在检测设备...");
            statusMenuItem.Tag = "Header";
            statusMenuItem.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
            statusMenuItem.Click += (s, e) => ShowWindow();
            contextMenu.Items.Add(statusMenuItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var showItem = new ToolStripMenuItem("打开控制面板 (&O)", null, (s, e) => ShowWindow());
            contextMenu.Items.Add(showItem);

            var refreshItem = new ToolStripMenuItem("立即刷新电量 (&R)", null, (s, e) => RefreshBatteryStatus(true));
            contextMenu.Items.Add(refreshItem);

            var intervalMenu = new ToolStripMenuItem("自动刷新频率 (&I)");
            intervalMenu.DropDown.Renderer = contextMenu.Renderer;
            int30sMenuItem = new ToolStripMenuItem("30 秒", null, (s, e) => SetInterval(30000, true));
            int1mMenuItem = new ToolStripMenuItem("1 分钟", null, (s, e) => SetInterval(60000, true));
            int5mMenuItem = new ToolStripMenuItem("5 分钟", null, (s, e) => SetInterval(300000, true));
            intervalMenu.DropDownItems.AddRange(new ToolStripItem[] { int30sMenuItem, int1mMenuItem, int5mMenuItem });
            contextMenu.Items.Add(intervalMenu);

            // Tray Icon Style Selector Submenu (2 styles: Capsule & Badge)
            var styleMenu = new ToolStripMenuItem("托盘图标样式 (&T)");
            styleMenu.DropDown.Renderer = contextMenu.Renderer;
            styleCapsuleItem = new ToolStripMenuItem("现代胶囊电池", null, (s, e) => SetTrayStyle(0, true));
            styleNumItem = new ToolStripMenuItem("醒目数字能量表", null, (s, e) => SetTrayStyle(1, true));
            styleCapsuleItem.Checked = (trayStyle == 0);
            styleNumItem.Checked = (trayStyle == 1);
            styleMenu.DropDownItems.AddRange(new ToolStripItem[] { styleCapsuleItem, styleNumItem });
            contextMenu.Items.Add(styleMenu);

            contextMenu.Items.Add(new ToolStripSeparator());

            lowBatteryAlertMenuItem = new ToolStripMenuItem("低电量气泡通知 (≤20%)", null, (s, e) => {
                SetLowBatteryAlert(!lowBatteryAlertEnabled, true);
            });
            contextMenu.Items.Add(lowBatteryAlertMenuItem);

            autoStartMenuItem = new ToolStripMenuItem("开机自动启动", null, (s, e) => {
                SetAutoStart(!IsAutoStartEnabled(), true);
            });
            contextMenu.Items.Add(autoStartMenuItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("退出程序 (&X)", null, (s, e) => ExitApp());
            contextMenu.Items.Add(exitItem);

            foreach (ToolStripItem item in contextMenu.Items)
            {
                item.Padding = new Padding(6, 4, 12, 4);
            }

            trayIcon = new NotifyIcon();
            trayIcon.ContextMenuStrip = contextMenu;
            trayIcon.Text = "雷蛇鼠标电量检测中...";
            UpdateTrayIcon(-1, false, false);
            trayIcon.Visible = true;

            trayIcon.Click += (s, e) => {
                var me = e as MouseEventArgs;
                if (me != null && me.Button == MouseButtons.Left)
                {
                    ToggleWindow();
                }
            };
            trayIcon.DoubleClick += (s, e) => ShowWindow();
        }

        private void ShowWindow()
        {
            RefreshBatteryStatus(false);
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.TopMost = true;
            this.BringToFront();
            this.Activate();
            this.TopMost = false;
        }

        private void ToggleWindow()
        {
            if (this.Visible && this.WindowState != FormWindowState.Minimized)
            {
                this.Hide();
            }
            else
            {
                ShowWindow();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                trayIcon.ShowBalloonTip(1500, "雷蛇电量已最小化", "程序在系统托盘持续运行，随时双击托盘图标可重新打开面板。", ToolTipIcon.Info);
            }
            else
            {
                base.OnFormClosing(e);
            }
        }

        private void SetInterval(int ms, bool showNotification = false)
        {
            userSelectedInterval = ms;
            if (updateTimer != null) updateTimer.Interval = ms;
            SaveConfig();

            isUpdatingUI = true;
            try
            {
                if (int30sMenuItem != null) int30sMenuItem.Checked = (ms == 30000);
                if (int1mMenuItem != null) int1mMenuItem.Checked = (ms == 60000);
                if (int5mMenuItem != null) int5mMenuItem.Checked = (ms == 300000);

                if (btnInt30s != null) btnInt30s.Selected = (ms == 30000);
                if (btnInt1m != null) btnInt1m.Selected = (ms == 60000);
                if (btnInt5m != null) btnInt5m.Selected = (ms == 300000);
            }
            finally
            {
                isUpdatingUI = false;
            }

            if (showNotification && trayIcon != null)
            {
                string sec = (ms >= 60000) ? ((ms / 60000) + " 分钟") : ((ms / 1000) + " 秒");
                trayIcon.ShowBalloonTip(1500, "刷新频率已设置", "已设置为: " + sec, ToolTipIcon.Info);
            }
        }

        private void SetTrayStyle(int style, bool showNotification = false)
        {
            trayStyle = style;
            SaveConfig();

            isUpdatingUI = true;
            try
            {
                if (styleCapsuleItem != null) styleCapsuleItem.Checked = (trayStyle == 0);
                if (styleNumItem != null) styleNumItem.Checked = (trayStyle == 1);

                if (btnStyleCapsule != null) btnStyleCapsule.Selected = (trayStyle == 0);
                if (btnStyleNum != null) btnStyleNum.Selected = (trayStyle == 1);
            }
            finally
            {
                isUpdatingUI = false;
            }

            if (lastInfo != null)
            {
                UpdateTrayIcon(lastInfo.BatteryPercent, lastInfo.IsCharging, lastInfo.IsConnected);
            }
            else
            {
                UpdateTrayIcon(-1, false, false);
            }

            if (showNotification && trayIcon != null)
            {
                string name = (trayStyle == 0) ? "现代胶囊电池" : "醒目数字能量表";
                trayIcon.ShowBalloonTip(1500, "托盘样式已切换", "当前显示样式: " + name, ToolTipIcon.Info);
            }
        }

        private void UpdateMenuStatusTexts()
        {
            if (lowBatteryAlertMenuItem != null)
            {
                lowBatteryAlertMenuItem.Text = "低电量气泡通知 (≤20%)";
                lowBatteryAlertMenuItem.Checked = lowBatteryAlertEnabled;
            }
            if (autoStartMenuItem != null)
            {
                bool autoStart = IsAutoStartEnabled();
                autoStartMenuItem.Text = "开机自动启动";
                autoStartMenuItem.Checked = autoStart;
            }
        }

        private void LoadConfig()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(ConfigRegistryKey))
                {
                    if (key != null)
                    {
                        var val = key.GetValue("LowBatteryAlert");
                        if (val != null)
                        {
                            lowBatteryAlertEnabled = (int)val == 1;
                        }

                        var sVal = key.GetValue("TrayIconStyle");
                        if (sVal != null)
                        {
                            trayStyle = (int)sVal;
                            if (trayStyle != 0 && trayStyle != 1) trayStyle = 0;
                        }

                        var rVal = key.GetValue("RefreshInterval");
                        if (rVal != null)
                        {
                            userSelectedInterval = (int)rVal;
                            if (userSelectedInterval != 30000 && userSelectedInterval != 60000 && userSelectedInterval != 300000)
                            {
                                userSelectedInterval = 60000;
                            }
                        }
                    }
                }
            }
            catch { }

            bool autoStart = IsAutoStartEnabled();

            isUpdatingUI = true;
            try
            {
                UpdateMenuStatusTexts();
                if (chkLowAlert != null) chkLowAlert.Checked = lowBatteryAlertEnabled;
                if (chkAutoStart != null) chkAutoStart.Checked = autoStart;

                if (styleCapsuleItem != null) styleCapsuleItem.Checked = (trayStyle == 0);
                if (styleNumItem != null) styleNumItem.Checked = (trayStyle == 1);
                if (btnStyleCapsule != null) btnStyleCapsule.Selected = (trayStyle == 0);
                if (btnStyleNum != null) btnStyleNum.Selected = (trayStyle == 1);

                if (int30sMenuItem != null) int30sMenuItem.Checked = (userSelectedInterval == 30000);
                if (int1mMenuItem != null) int1mMenuItem.Checked = (userSelectedInterval == 60000);
                if (int5mMenuItem != null) int5mMenuItem.Checked = (userSelectedInterval == 300000);
                if (btnInt30s != null) btnInt30s.Selected = (userSelectedInterval == 30000);
                if (btnInt1m != null) btnInt1m.Selected = (userSelectedInterval == 60000);
                if (btnInt5m != null) btnInt5m.Selected = (userSelectedInterval == 300000);
            }
            finally
            {
                isUpdatingUI = false;
            }
        }

        private void SaveConfig()
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(ConfigRegistryKey))
                {
                    if (key != null)
                    {
                        key.SetValue("LowBatteryAlert", lowBatteryAlertEnabled ? 1 : 0);
                        key.SetValue("TrayIconStyle", trayStyle);
                        key.SetValue("RefreshInterval", userSelectedInterval);
                    }
                }
            }
            catch { }
        }

        private bool IsAutoStartEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false))
                {
                    if (key != null)
                    {
                        var val = key.GetValue(AppName);
                        return val != null;
                    }
                }
            }
            catch { }
            return false;
        }

        private void SetAutoStart(bool enabled, bool showNotification = false)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true))
                {
                    if (key != null)
                    {
                        if (enabled)
                        {
                            string exePath = Application.ExecutablePath;
                            key.SetValue(AppName, "\"" + exePath + "\"");
                        }
                        else
                        {
                            key.DeleteValue(AppName, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("设置开机启动失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            isUpdatingUI = true;
            try
            {
                UpdateMenuStatusTexts();
                if (chkAutoStart != null && chkAutoStart.Checked != enabled) chkAutoStart.Checked = enabled;
            }
            finally
            {
                isUpdatingUI = false;
            }

            if (showNotification)
            {
                if (enabled)
                {
                    trayIcon.ShowBalloonTip(2000, "开机自启已开启", "雷蛇电量托盘助手已设置为开机自动启动并在托盘静默运行。", ToolTipIcon.Info);
                }
                else
                {
                    trayIcon.ShowBalloonTip(2000, "开机自启已关闭", "已取消开机自动启动。", ToolTipIcon.None);
                }
            }
        }

        private void SetLowBatteryAlert(bool enabled, bool showNotification = false)
        {
            lowBatteryAlertEnabled = enabled;

            isUpdatingUI = true;
            try
            {
                UpdateMenuStatusTexts();
                if (chkLowAlert != null && chkLowAlert.Checked != enabled) chkLowAlert.Checked = enabled;
            }
            finally
            {
                isUpdatingUI = false;
            }

            SaveConfig();

            if (showNotification)
            {
                if (enabled)
                {
                    trayIcon.ShowBalloonTip(2500, "低电量提醒已开启", "当鼠标电量 ≤ 20% 且未充电时，系统托盘将弹出气泡提醒您及时充电。", ToolTipIcon.Info);
                }
                else
                {
                    trayIcon.ShowBalloonTip(2000, "低电量提醒已关闭", "已关闭低电量气泡通知功能。", ToolTipIcon.None);
                }
            }
        }

        private void RefreshBatteryStatus(bool showTipIfManual)
        {
            try
            {
                var info = RazerDeviceHelper.QueryRazerBattery();
                lastInfo = info;
                UpdateUI(info, showTipIfManual);
            }
            catch (Exception ex)
            {
                statusMenuItem.Text = "读取失败: " + ex.Message;
                trayIcon.Text = "雷蛇鼠标检测异常";
                UpdateTrayIcon(-1, false, false);
            }
        }

        private void UpdateUI(MouseBatteryInfo info, bool showTipIfManual)
        {
            if (info == null || !info.IsConnected)
            {
                // In sleep or disconnected mode, speed up update cycle to 3s to detect wake/reconnect immediately
                if (updateTimer != null) updateTimer.Interval = 3000;

                lblDeviceName.Text = "未检测到雷蛇鼠标";
                lblConnDot.Text = "○ 未连接";
                lblConnDot.ForeColor = Color.FromArgb(140, 145, 155);

                lblBatteryBig.Text = "--%";
                lblBatteryBig.ForeColor = Color.FromArgb(140, 145, 155);

                pillStatus.SetStatus("休眠或未连接", Color.FromArgb(140, 145, 155));
                pillStatus.Location = new Point(lblBatteryBig.Right + (int)(16 * dpiScale), lblBatteryBig.Top + (lblBatteryBig.Height - pillStatus.Height) / 2);
                barBattery.Value = 0;
                lblUpdateTime.Text = "最后同步: " + DateTime.Now.ToString("HH:mm:ss") + " · 未检测到设备";

                statusMenuItem.Text = "未检测到雷蛇鼠标 (未连接/休眠)";
                trayIcon.Text = "未检测到雷蛇鼠标 (休眠或未连接)";
                UpdateTrayIcon(-1, false, false);
                lastLowAlertFired = false;
                if (showTipIfManual)
                {
                    trayIcon.ShowBalloonTip(2000, "雷蛇鼠标未连接", "未能找到已连接或唤醒的雷蛇鼠标，请移动鼠标唤醒后重试。", ToolTipIcon.Warning);
                }
                return;
            }

            // In normal connected mode, restore the user-selected interval (e.g. 60s)
            if (updateTimer != null) updateTimer.Interval = userSelectedInterval;

            Color accentColor;
            if (info.BatteryPercent > 40 || info.IsCharging)
                accentColor = Color.FromArgb(0, 230, 118); // Emerald Green
            else if (info.BatteryPercent > 20)
                accentColor = Color.FromArgb(255, 214, 0); // Yellow
            else
                accentColor = Color.FromArgb(255, 45, 85); // Red

            string chgStr = info.IsCharging ? "⚡ 正在充电" : "🔋 电池供电";
            lblDeviceName.Text = info.DeviceName;
            lblConnDot.Text = "● 已连接";
            lblConnDot.ForeColor = Color.FromArgb(0, 230, 118);

            lblBatteryBig.Text = info.BatteryPercent + "%";
            lblBatteryBig.ForeColor = accentColor;

            pillStatus.SetStatus(chgStr, accentColor);
            pillStatus.Location = new Point(lblBatteryBig.Right + (int)(16 * dpiScale), lblBatteryBig.Top + (lblBatteryBig.Height - pillStatus.Height) / 2);

            barBattery.Value = info.BatteryPercent;
            barBattery.ProgressColor = accentColor;

            string timeStr = info.LastUpdated.ToString("HH:mm:ss");
            lblUpdateTime.Text = "最后同步: " + timeStr + " · 自动侦测硬件插拔";

            string menuStatus = string.Format("{0} ({1}% · {2})", info.DeviceName, info.BatteryPercent, info.IsCharging ? "充电中" : "正常供电");
            statusMenuItem.Text = menuStatus;

            string tipText = string.Format("雷蛇电量管家\n设备: {0}\n电量: {1}% ({2})\n同步: {3}", info.DeviceName, info.BatteryPercent, chgStr, timeStr);
            if (tipText.Length > 63)
            {
                tipText = string.Format("{0}: {1}% ({2})", info.DeviceName, info.BatteryPercent, chgStr);
                if (tipText.Length > 63)
                {
                tipText = string.Format("电量: {0}% ({1})", info.BatteryPercent, chgStr);
                }
            }
            trayIcon.Text = tipText;

            UpdateTrayIcon(info.BatteryPercent, info.IsCharging, true);

            if (lowBatteryAlertEnabled && !info.IsCharging)
            {
                if (info.BatteryPercent <= 20)
                {
                    if (!lastLowAlertFired)
                    {
                        trayIcon.ShowBalloonTip(4000, "雷蛇鼠标电量不足", string.Format("当前电量仅剩 {0}%，请及时连接充电器！", info.BatteryPercent), ToolTipIcon.Warning);
                        lastLowAlertFired = true;
                    }
                }
                else
                {
                    lastLowAlertFired = false;
                }
            }
            else
            {
                lastLowAlertFired = false;
            }

            if (showTipIfManual)
            {
                trayIcon.ShowBalloonTip(1500, info.DeviceName, string.Format("电量: {0}% ({1})\n更新时间: {2}", info.BatteryPercent, chgStr, timeStr), ToolTipIcon.Info);
            }
        }

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);
        private const int SM_CXSMICON = 49;

        private int GetTrayIconSize()
        {
            try
            {
                int size = GetSystemMetrics(SM_CXSMICON);
                if (size >= 24) return 24;
                if (size >= 20) return 20;
                return 16;
            }
            catch
            {
                return 16;
            }
        }

        private void UpdateTrayIcon(int percent, bool isCharging, bool isConnected)
        {
            int iconSize = GetTrayIconSize();
            using (Bitmap bmp = DrawTrayBitmap(percent, isCharging, isConnected, trayStyle, iconSize))
            {
                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    using (Icon tempIcon = Icon.FromHandle(hIcon))
                    {
                        trayIcon.Icon = (Icon)tempIcon.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }

        private static readonly byte[][] Digits3 = new byte[][] {
            new byte[] { 0x7, 0x5, 0x5, 0x5, 0x7 }, // 0
            new byte[] { 0x1, 0x3, 0x1, 0x1, 0x1 }, // 1 (width 2, or 1 in 100)
            new byte[] { 0x7, 0x1, 0x7, 0x4, 0x7 }, // 2
            new byte[] { 0x7, 0x1, 0x7, 0x1, 0x7 }, // 3
            new byte[] { 0x5, 0x5, 0x7, 0x1, 0x1 }, // 4
            new byte[] { 0x7, 0x4, 0x7, 0x1, 0x7 }, // 5
            new byte[] { 0x7, 0x4, 0x7, 0x5, 0x7 }, // 6
            new byte[] { 0x7, 0x1, 0x1, 0x1, 0x1 }, // 7
            new byte[] { 0x7, 0x5, 0x7, 0x5, 0x7 }, // 8
            new byte[] { 0x7, 0x5, 0x7, 0x1, 0x7 }, // 9
        };

        private static readonly byte[][] Digits4x7 = new byte[][] {
            new byte[] { 0x6, 0x9, 0x9, 0x9, 0x9, 0x9, 0x6 }, // 0
            new byte[] { 0x2, 0x6, 0x2, 0x2, 0x2, 0x2, 0x7 }, // 1 (width 3)
            new byte[] { 0x6, 0x9, 0x1, 0x2, 0x4, 0x8, 0xF }, // 2
            new byte[] { 0xE, 0x1, 0x1, 0x6, 0x1, 0x1, 0xE }, // 3
            new byte[] { 0x9, 0x9, 0x9, 0xF, 0x1, 0x1, 0x1 }, // 4
            new byte[] { 0xF, 0x8, 0xE, 0x1, 0x1, 0x9, 0x6 }, // 5
            new byte[] { 0x6, 0x8, 0xE, 0x9, 0x9, 0x9, 0x6 }, // 6
            new byte[] { 0xF, 0x1, 0x2, 0x2, 0x4, 0x4, 0x4 }, // 7
            new byte[] { 0x6, 0x9, 0x9, 0x6, 0x9, 0x9, 0x6 }, // 8
            new byte[] { 0x6, 0x9, 0x9, 0x7, 0x1, 0x1, 0x6 }, // 9
        };

        private static Bitmap DrawTrayBitmap(int percent, bool isCharging, bool isConnected, int style, int iconSize)
        {
            if (iconSize >= 24)
            {
                return DrawTrayBitmap24(percent, isCharging, isConnected, style);
            }
            else
            {
                return DrawTrayBitmap16(percent, isCharging, isConnected, style);
            }
        }

        private static Bitmap DrawTrayBitmap24(int percent, bool isCharging, bool isConnected, int style)
        {
            Bitmap bmp = new Bitmap(24, 24);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                if (!isConnected)
                {
                    Color whiteCol = Color.White;
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(18, 22, 30)))
                    {
                        g.FillRectangle(bg, 2, 4, 18, 16);
                    }
                    using (Pen p = new Pen(whiteCol, 1f))
                    {
                        g.DrawLine(p, 2, 3, 19, 3);
                        g.DrawLine(p, 2, 20, 19, 20);
                        g.DrawLine(p, 1, 4, 1, 19);
                        g.DrawLine(p, 20, 4, 20, 19);
                        bmp.SetPixel(1, 3, whiteCol);
                        bmp.SetPixel(1, 20, whiteCol);
                        bmp.SetPixel(20, 3, whiteCol);
                        bmp.SetPixel(20, 20, whiteCol);
                    }
                    using (SolidBrush cap = new SolidBrush(whiteCol))
                    {
                        g.FillRectangle(cap, 21, 8, 2, 8);
                    }
                    DrawQuestionMark24(bmp, 10, 8, Color.FromArgb(160, 165, 180));
                    return bmp;
                }

                Color accentColor = (percent > 40 || isCharging) ? Color.FromArgb(0, 230, 118) :
                                    ((percent > 20) ? Color.FromArgb(255, 214, 0) : Color.FromArgb(255, 50, 65));

                if (style == 1) // 醒目数字能量表 (Centered digits)
                {
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(20, 23, 30)))
                        g.FillRectangle(bg, 0, 0, 24, 24);
                    using (Pen border = new Pen(Color.FromArgb(48, 56, 74), 1f))
                        g.DrawRectangle(border, 0, 0, 23, 23);

                    // Area above bar: Y=1..16, 7px font -> startY = 5, center X = 11
                    if (isCharging)
                        DrawBolt24(bmp, 11, 10, Color.White);
                    else
                        DrawDigits24(bmp, percent, 11, 5, Color.White, false);

                    int barW = Math.Max(2, (int)(18 * (percent / 100.0)));
                    using (SolidBrush barB = new SolidBrush(accentColor))
                        g.FillRectangle(barB, 3, 17, barW, 4);
                }
                else // 现代胶囊电池 (Default, sealed closed white corners)
                {
                    Color whiteCol = Color.White;

                    // 1. Fill cavity background
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(18, 22, 30)))
                        g.FillRectangle(bg, 2, 4, 18, 16);

                    // 2. Fill battery level
                    int fillW = Math.Max(1, (int)(18 * (percent / 100.0)));
                    using (SolidBrush fb = new SolidBrush(accentColor))
                        g.FillRectangle(fb, 2, 4, fillW, 16);

                    // 3. Draw digits with smart contrast
                    if (isCharging)
                        DrawBolt24(bmp, 10, 11, Color.White);
                    else
                        DrawDigits24(bmp, percent, 10, 8, Color.White, true);

                    // 4. DRAW 100% SEALED SOLID WHITE SHELL ON TOP
                    using (Pen p = new Pen(whiteCol, 1f))
                    {
                        g.DrawLine(p, 2, 3, 19, 3);
                        g.DrawLine(p, 2, 20, 19, 20);
                        g.DrawLine(p, 1, 4, 1, 19);
                        g.DrawLine(p, 20, 4, 20, 19);
                    }
                    // Explicitly seal all 4 corner pixels with solid white
                    bmp.SetPixel(1, 3, whiteCol);
                    bmp.SetPixel(1, 20, whiteCol);
                    bmp.SetPixel(20, 3, whiteCol);
                    bmp.SetPixel(20, 20, whiteCol);

                    // Terminal cap
                    using (SolidBrush cap = new SolidBrush(whiteCol))
                        g.FillRectangle(cap, 21, 8, 2, 8);
                }
            }
            return bmp;
        }

        private static Bitmap DrawTrayBitmap16(int percent, bool isCharging, bool isConnected, int style)
        {
            Bitmap bmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                if (!isConnected)
                {
                    Color whiteCol = Color.White;
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(18, 22, 30)))
                        g.FillRectangle(bg, 1, 3, 12, 10);
                    using (Pen p = new Pen(whiteCol, 1f))
                    {
                        g.DrawLine(p, 1, 2, 12, 2);
                        g.DrawLine(p, 1, 13, 12, 13);
                        g.DrawLine(p, 0, 3, 0, 12);
                        g.DrawLine(p, 13, 3, 13, 12);
                    }
                    bmp.SetPixel(0, 2, whiteCol);
                    bmp.SetPixel(0, 13, whiteCol);
                    bmp.SetPixel(13, 2, whiteCol);
                    bmp.SetPixel(13, 13, whiteCol);
                    using (SolidBrush cap = new SolidBrush(whiteCol))
                        g.FillRectangle(cap, 14, 5, 2, 6);
                    DrawQuestionMark16(bmp, 6, 5, Color.FromArgb(160, 165, 180));
                    return bmp;
                }

                Color accentColor = (percent > 40 || isCharging) ? Color.FromArgb(0, 230, 118) :
                                    ((percent > 20) ? Color.FromArgb(255, 214, 0) : Color.FromArgb(255, 50, 65));

                if (style == 1) // 醒目数字能量表 (Centered digits)
                {
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(20, 23, 30)))
                        g.FillRectangle(bg, 0, 0, 16, 16);
                    using (Pen border = new Pen(Color.FromArgb(48, 56, 74), 1f))
                        g.DrawRectangle(border, 0, 0, 15, 15);

                    if (isCharging)
                    {
                        bmp.SetPixel(14, 1, Color.FromArgb(0, 255, 136));
                        bmp.SetPixel(13, 2, Color.FromArgb(0, 255, 136));
                        bmp.SetPixel(14, 2, Color.FromArgb(0, 255, 136));
                        bmp.SetPixel(13, 3, Color.FromArgb(0, 255, 136));
                        DrawBolt16(bmp, 6, 4, Color.White);
                    }
                    else
                    {
                        DrawDigits16(bmp, percent, 7, 3, Color.White, false);
                    }

                    int barW = Math.Max(2, (int)(12 * (percent / 100.0)));
                    using (SolidBrush barB = new SolidBrush(accentColor))
                        g.FillRectangle(barB, 2, 12, barW, 3);
                }
                else // 现代胶囊电池 (Default, sealed closed white corners)
                {
                    Color whiteCol = Color.White;

                    // 1. Fill cavity background
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(18, 22, 30)))
                        g.FillRectangle(bg, 1, 3, 12, 10);

                    // 2. Fill battery level
                    int fillW = Math.Max(1, (int)(12 * (percent / 100.0)));
                    using (SolidBrush fb = new SolidBrush(accentColor))
                        g.FillRectangle(fb, 1, 3, fillW, 10);

                    // 3. Draw digits with smart contrast
                    if (isCharging)
                        DrawBolt16(bmp, 6, 5, Color.White);
                    else
                        DrawDigits16(bmp, percent, 7, 5, Color.White, true);

                    // 4. DRAW 100% SEALED SOLID WHITE SHELL ON TOP
                    using (Pen p = new Pen(whiteCol, 1f))
                    {
                        g.DrawLine(p, 1, 2, 12, 2);
                        g.DrawLine(p, 1, 13, 12, 13);
                        g.DrawLine(p, 0, 3, 0, 12);
                        g.DrawLine(p, 13, 3, 13, 12);
                    }
                    // Explicitly seal all 4 corner pixels with solid white
                    bmp.SetPixel(0, 2, whiteCol);
                    bmp.SetPixel(0, 13, whiteCol);
                    bmp.SetPixel(13, 2, whiteCol);
                    bmp.SetPixel(13, 13, whiteCol);

                    // Terminal cap
                    using (SolidBrush cap = new SolidBrush(whiteCol))
                        g.FillRectangle(cap, 14, 5, 2, 6);
                }
            }
            return bmp;
        }

        private static void DrawDigits24(Bitmap bmp, int value, int centerX, int startY, Color color, bool smartContrast = false)
        {
            string s = value.ToString();
            int totalW = 0;
            int[] widths = new int[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                int d = s[i] - '0';
                int w = (d == 1) ? 3 : 4;
                widths[i] = w;
                totalW += w;
            }
            totalW += s.Length - 1;

            int curX = centerX - (totalW / 2);
            for (int i = 0; i < s.Length; i++)
            {
                int d = s[i] - '0';
                int w = widths[i];
                byte[] rows = Digits4x7[d];
                for (int r = 0; r < 7; r++)
                {
                    byte row = rows[r];
                    for (int c = 0; c < w; c++)
                    {
                        int bit = (w == 3) ? (2 - c) : (3 - c);
                        if ((row & (1 << bit)) != 0)
                        {
                            int px = curX + c;
                            int py = startY + r;
                            if (px >= 0 && px < bmp.Width && py >= 0 && py < bmp.Height)
                            {
                                Color drawCol = color;
                                if (smartContrast)
                                {
                                    Color bg = bmp.GetPixel(px, py);
                                    int lum = (int)(bg.R * 0.299 + bg.G * 0.587 + bg.B * 0.114);
                                    drawCol = (lum > 110) ? Color.FromArgb(10, 24, 15) : Color.White;
                                }
                                bmp.SetPixel(px, py, drawCol);
                            }
                        }
                    }
                }
                curX += w + 1;
            }
        }

        private static void DrawBolt24(Bitmap bmp, int cx, int cy, Color color)
        {
            Point[] pts = new Point[] {
                new Point(cx + 1, cy - 5), new Point(cx - 3, cy), new Point(cx, cy),
                new Point(cx - 2, cy + 5), new Point(cx + 3, cy - 1), new Point(cx, cy - 1)
            };
            using (Graphics g = Graphics.FromImage(bmp))
            using (SolidBrush b = new SolidBrush(color))
            {
                g.FillPolygon(b, pts);
            }
        }

        private static void DrawQuestionMark24(Bitmap bmp, int cx, int cy, Color color)
        {
            int[,] qPts = new int[,] { {0,0}, {1,0}, {2,0}, {3,0}, {3,1}, {3,2}, {2,3}, {1,4}, {1,6} };
            for (int i = 0; i < qPts.GetLength(0); i++)
            {
                int px = cx + qPts[i, 0];
                int py = cy + qPts[i, 1];
                if (px >= 0 && px < bmp.Width && py >= 0 && py < bmp.Height)
                    bmp.SetPixel(px, py, color);
            }
        }

        private static void DrawDigits16(Bitmap bmp, int value, int centerX, int startY, Color color, bool smartContrast = false)
        {
            string s = value.ToString();
            int totalW = 0;
            for (int i = 0; i < s.Length; i++)
            {
                int d = s[i] - '0';
                int w = (d == 1 && s.Length == 3) ? 1 : (d == 1 ? 2 : 3);
                totalW += w;
                if (i < s.Length - 1) totalW += 1;
            }

            int curX = centerX - (totalW / 2);
            for (int i = 0; i < s.Length; i++)
            {
                int d = s[i] - '0';
                byte[] rows = Digits3[d];
                int w = (d == 1 && s.Length == 3) ? 1 : (d == 1 ? 2 : 3);
                for (int r = 0; r < 5; r++)
                {
                    byte row = rows[r];
                    for (int c = 0; c < w; c++)
                    {
                        int bit = (w == 1) ? 0 : ((w == 2) ? (1 - c) : (2 - c));
                        if ((row & (1 << bit)) != 0)
                        {
                            int px = curX + c;
                            int py = startY + r;
                            if (px >= 0 && px < bmp.Width && py >= 0 && py < bmp.Height)
                            {
                                Color drawCol = color;
                                if (smartContrast)
                                {
                                    Color bg = bmp.GetPixel(px, py);
                                    int lum = (int)(bg.R * 0.299 + bg.G * 0.587 + bg.B * 0.114);
                                    drawCol = (lum > 110) ? Color.FromArgb(10, 24, 15) : Color.White;
                                }
                                bmp.SetPixel(px, py, drawCol);
                            }
                        }
                    }
                }
                curX += w + 1;
            }
        }

        private static void DrawBolt16(Bitmap bmp, int startX, int startY, Color color)
        {
            int[,] bolt = new int[,] {
                {0, 2}, {1, 1}, {2, 0},
                {1, 2}, {2, 2}, {3, 2},
                {0, 3}, {1, 3}, {2, 3},
                {1, 4}, {2, 5}
            };
            for (int i = 0; i < bolt.GetLength(0); i++)
            {
                int px = startX + bolt[i, 0];
                int py = startY + bolt[i, 1];
                if (px >= 0 && px < bmp.Width && py >= 0 && py < bmp.Height)
                    bmp.SetPixel(px, py, color);
            }
        }

        private static void DrawQuestionMark16(Bitmap bmp, int cx, int cy, Color color)
        {
            int[,] qPts = new int[,] { {0,0}, {1,0}, {2,0}, {2,1}, {1,2}, {1,4} };
            for (int i = 0; i < qPts.GetLength(0); i++)
            {
                int px = cx + qPts[i, 0];
                int py = cy + qPts[i, 1];
                if (px >= 0 && px < bmp.Width && py >= 0 && py < bmp.Height)
                    bmp.SetPixel(px, py, color);
            }
        }
        private void ExitApp()
        {
            if (hDevNotify != IntPtr.Zero)
            {
                try { UnregisterDeviceNotification(hDevNotify); } catch { }
                hDevNotify = IntPtr.Zero;
            }
            if (deviceChangeTimer1 != null) { deviceChangeTimer1.Stop(); deviceChangeTimer1.Dispose(); }
            if (deviceChangeTimer2 != null) { deviceChangeTimer2.Stop(); deviceChangeTimer2.Dispose(); }
            if (updateTimer != null)
            {
                updateTimer.Stop();
                updateTimer.Dispose();
            }
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            Application.Exit();
        }
    }

    public static class RazerDeviceHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HIDD_ATTRIBUTES
        {
            public int Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [DllImport("hid.dll", SetLastError = true)]
        public static extern void HidD_GetHidGuid(out Guid HidGuid);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, string Enumerator, IntPtr hwndParent, uint Flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiEnumDeviceInterfaces(IntPtr DeviceInfoSet, IntPtr DeviceInfoData, ref Guid InterfaceClassGuid, uint MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr DeviceInfoSet, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData, IntPtr DeviceInterfaceDetailData, uint DeviceInterfaceDetailDataSize, out uint RequiredSize, IntPtr DeviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_GetAttributes(IntPtr HidDeviceObject, ref HIDD_ATTRIBUTES Attributes);

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_GetPreparsedData(IntPtr HidDeviceObject, out IntPtr PreparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_FreePreparsedData(IntPtr PreparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        static extern int HidP_GetCaps(IntPtr PreparsedData, out HIDP_CAPS Capabilities);

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_SetFeature(IntPtr HidDeviceObject, byte[] lpReportBuffer, int ReportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_GetFeature(IntPtr HidDeviceObject, byte[] lpReportBuffer, int ReportBufferLength);

        [DllImport("hid.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool HidD_GetProductString(IntPtr HidDeviceObject, StringBuilder Buffer, int BufferLength);

        const uint DIGCF_PRESENT = 0x02;
        const uint DIGCF_DEVICEINTERFACE = 0x10;
        const uint FILE_SHARE_READ = 0x00000001;
        const uint FILE_SHARE_WRITE = 0x00000002;
        const uint OPEN_EXISTING = 3;

        static byte CalculateCrc(byte[] report, int startOffset)
        {
            byte crc = 0;
            for (int i = startOffset + 2; i < startOffset + 88; i++)
            {
                crc ^= report[i];
            }
            return crc;
        }

        static byte[] CreateRazerReport(byte transactionId, byte commandClass, byte commandId, byte dataSize, int totalLength, bool prependedZero)
        {
            byte[] report = new byte[totalLength];
            int offset = prependedZero ? 1 : 0;

            report[offset + 0] = 0x00;
            report[offset + 1] = transactionId;
            report[offset + 2] = 0x00;
            report[offset + 3] = 0x00;
            report[offset + 4] = 0x00;
            report[offset + 5] = dataSize;
            report[offset + 6] = commandClass;
            report[offset + 7] = commandId;

            report[offset + 88] = CalculateCrc(report, offset);
            report[offset + 89] = 0x00;

            return report;
        }

        public static MouseBatteryInfo QueryRazerBattery()
        {
            Guid hidGuid;
            HidD_GetHidGuid(out hidGuid);

            IntPtr devInfo = SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (devInfo == IntPtr.Zero || devInfo == new IntPtr(-1))
            {
                return new MouseBatteryInfo { IsConnected = false };
            }

            SP_DEVICE_INTERFACE_DATA ifData = new SP_DEVICE_INTERFACE_DATA();
            ifData.cbSize = Marshal.SizeOf(ifData);

            uint memberIdx = 0;
            try
            {
                while (SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref hidGuid, memberIdx++, ref ifData))
                {
                    uint reqSize;
                    SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, IntPtr.Zero, 0, out reqSize, IntPtr.Zero);

                    IntPtr detailBuffer = Marshal.AllocHGlobal((int)reqSize);
                    Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 5);

                    if (SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, detailBuffer, reqSize, out reqSize, IntPtr.Zero))
                    {
                        IntPtr pDevicePath = new IntPtr(detailBuffer.ToInt64() + 4);
                        string devicePath = Marshal.PtrToStringAuto(pDevicePath);

                        if (devicePath != null && devicePath.ToLower().Contains("vid_1532"))
                        {
                            IntPtr handle = CreateFile(devicePath, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                            if (handle != IntPtr.Zero && handle.ToInt64() != -1)
                            {
                                try
                                {
                                    IntPtr preparsed;
                                    if (HidD_GetPreparsedData(handle, out preparsed))
                                    {
                                        HIDP_CAPS caps;
                                        HidP_GetCaps(preparsed, out caps);
                                        HidD_FreePreparsedData(preparsed);

                                        if (caps.FeatureReportByteLength >= 90)
                                        {
                                            StringBuilder prod = new StringBuilder(256);
                                            string prodName = "Razer Mouse";
                                            if (HidD_GetProductString(handle, prod, prod.Capacity) && prod.Length > 0)
                                            {
                                                prodName = prod.ToString();
                                            }

                                            byte[] transIds = new byte[] { 0x1F, 0x3F, 0xFF };
                                            bool prepended = (caps.FeatureReportByteLength == 91);

                                            foreach (byte tid in transIds)
                                            {
                                                byte[] req = CreateRazerReport(tid, 0x07, 0x80, 0x02, caps.FeatureReportByteLength, prepended);
                                                if (HidD_SetFeature(handle, req, req.Length))
                                                {
                                                    Thread.Sleep(20);
                                                    byte[] resp = new byte[caps.FeatureReportByteLength];
                                                    if (prepended) resp[0] = 0x00;

                                                    if (HidD_GetFeature(handle, resp, resp.Length))
                                                    {
                                                        int offset = prepended ? 1 : 0;
                                                        byte status = resp[offset + 0];
                                                        byte cmdClass = resp[offset + 6];
                                                        byte cmdId = resp[offset + 7];
                                                        byte rawBatt = resp[offset + 9];

                                                        if (status == 0x02 || (cmdClass == 0x07 && cmdId == 0x80))
                                                        {
                                                            int pct = (int)Math.Round((rawBatt / 255.0) * 100);
                                                            pct = Math.Max(0, Math.Min(100, pct));

                                                            bool isCharging = false;
                                                            byte[] chgReq = CreateRazerReport(tid, 0x07, 0x84, 0x02, caps.FeatureReportByteLength, prepended);
                                                            if (HidD_SetFeature(handle, chgReq, chgReq.Length))
                                                            {
                                                                Thread.Sleep(20);
                                                                byte[] chgResp = new byte[caps.FeatureReportByteLength];
                                                                if (prepended) chgResp[0] = 0x00;
                                                                if (HidD_GetFeature(handle, chgResp, chgResp.Length))
                                                                {
                                                                    byte chgVal = chgResp[offset + 9];
                                                                    isCharging = (chgVal == 1);
                                                                }
                                                            }

                                                            return new MouseBatteryInfo
                                                            {
                                                                IsConnected = true,
                                                                DeviceName = prodName,
                                                                BatteryPercent = pct,
                                                                IsCharging = isCharging,
                                                                LastUpdated = DateTime.Now
                                                            };
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                finally
                                {
                                    CloseHandle(handle);
                                }
                            }
                        }
                    }

                    Marshal.FreeHGlobal(detailBuffer);
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(devInfo);
            }

            return new MouseBatteryInfo { IsConnected = false };
        }
    }
}
