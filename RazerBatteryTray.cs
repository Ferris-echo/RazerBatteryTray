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

        [DllImport("kernel32.dll")]
        static extern ulong GetTickCount64();

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                SetProcessDPIAware();
            }
            catch { }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => {
                MessageBox.Show("程序发生异常: " + e.Exception.Message + "\n\n" + e.Exception.StackTrace, "雷蛇电量管家 - 错误提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                Exception ex = e.ExceptionObject as Exception;
                string msg = ex != null ? (ex.Message + "\n\n" + ex.StackTrace) : "未知系统错误";
                MessageBox.Show("未处理的致命异常: " + msg, "雷蛇电量管家 - 致命错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            bool isAutoStart = false;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] != null && args[i].IndexOf("autostart", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isAutoStart = true;
                        break;
                    }
                }
            }

            // Fallback heuristic: If launched without --autostart, but system was booted within 6 minutes (360,000 ms)
            // and this app is registered in HKCU Run key, check if AutoStartShowUI is NOT 1.
            // This ensures that even if Windows Run key was outdated or stripped parameters, it will stay silent on boot.
            if (!isAutoStart)
            {
                try
                {
                    ulong uptimeMs = GetTickCount64();
                    if (uptimeMs < 360000) // Within 6 minutes of system boot
                    {
                        using (var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                        {
                            if (runKey != null && runKey.GetValue("RazerBatteryTray") != null)
                            {
                                bool showUI = false;
                                using (var cfgKey = Registry.CurrentUser.OpenSubKey(@"Software\RazerBatteryTray", false))
                                {
                                    if (cfgKey != null)
                                    {
                                        var val = cfgKey.GetValue("AutoStartShowUI");
                                        if (val != null && (int)val == 1)
                                        {
                                            showUI = true;
                                        }
                                    }
                                }

                                if (!showUI)
                                {
                                    isAutoStart = true;
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(isAutoStart));
        }
    }

    public class MouseBatteryInfo
    {
        public bool IsConnected { get; set; }
        public bool IsSleeping { get; set; }
        public bool IsDonglePresent { get; set; }
        public string DeviceName { get; set; }
        public int BatteryPercent { get; set; }
        public bool IsCharging { get; set; }
        public DateTime LastUpdated { get; set; }

        public int Dpi { get; set; }
        public int DpiStage { get; set; }
        public int DpiStageCount { get; set; }
        public int[] DpiStages { get; set; }
        public int PollingRate { get; set; }
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
            PressedColor = Color.FromArgb(0, 160, 65);
            BorderColor = Color.Transparent;
            CornerRadius = 8;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); isHovered = false; isPressed = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { isPressed = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); isPressed = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            base.OnPaint(pevent);
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Color fill = NormalColor;
            if (isPressed) fill = PressedColor;
            else if (isHovered) fill = HoverColor;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedCard.GetRoundedRectangle(rect, CornerRadius))
            {
                using (SolidBrush brush = new SolidBrush(fill))
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
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); isHovered = false; Invalidate(); }
        protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            base.OnPaint(pevent);
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int boxSize = 16;
            int boxY = (Height - boxSize) / 2;
            Rectangle boxRect = new Rectangle(1, boxY, boxSize, boxSize);

            Color boxBg = isChecked ? (isHovered ? Color.FromArgb(0, 230, 118) : Color.FromArgb(0, 200, 83)) : (isHovered ? Color.FromArgb(38, 42, 54) : Color.FromArgb(28, 30, 38));
            Color boxBorder = isChecked ? Color.FromArgb(0, 230, 118) : (isHovered ? Color.FromArgb(90, 98, 120) : Color.FromArgb(58, 63, 78));

            using (GraphicsPath path = RoundedCard.GetRoundedRectangle(boxRect, 4))
            {
                using (SolidBrush brush = new SolidBrush(boxBg))
                {
                    g.FillPath(brush, path);
                }
                using (Pen pen = new Pen(boxBorder, 1f))
                {
                    g.DrawPath(pen, path);
                }
            }

            if (isChecked)
            {
                using (Pen checkPen = new Pen(Color.FromArgb(10, 24, 15), 2.0f))
                {
                    checkPen.StartCap = LineCap.Round;
                    checkPen.EndCap = LineCap.Round;
                    PointF[] checkPoints = new PointF[]
                    {
                        new PointF(boxRect.Left + 3.5f, boxRect.Top + 8.5f),
                        new PointF(boxRect.Left + 6.5f, boxRect.Top + 11.5f),
                        new PointF(boxRect.Left + 12.5f, boxRect.Top + 4.5f)
                    };
                    g.DrawLines(checkPen, checkPoints);
                }
            }

            int textX = boxRect.Right + 8;
            Rectangle textRect = new Rectangle(textX, 0, Width - textX, Height);
            Color textCol = isHovered ? Color.White : Color.FromArgb(220, 226, 238);
            TextRenderer.DrawText(g, Text, Font, textRect, textCol,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    public class ModernSegmentButton : Control
    {
        private bool isSelected = false;
        private bool isHovered = false;

        public bool Selected
        {
            get { return isSelected; }
            set { if (isSelected != value) { isSelected = value; Invalidate(); } }
        }

        public ModernSegmentButton()
        {
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); isHovered = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            base.OnPaint(pevent);
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Color bg;
            Color border;
            Color fg;

            if (isSelected)
            {
                bg = isHovered ? Color.FromArgb(0, 230, 118) : Color.FromArgb(0, 200, 83);
                border = Color.FromArgb(0, 230, 118);
                fg = Color.FromArgb(10, 24, 15);
            }
            else
            {
                bg = isHovered ? Color.FromArgb(38, 42, 54) : Color.FromArgb(28, 30, 38);
                border = isHovered ? Color.FromArgb(80, 88, 108) : Color.FromArgb(48, 52, 65);
                fg = isHovered ? Color.White : Color.FromArgb(180, 188, 205);
            }

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedCard.GetRoundedRectangle(rect, 6))
            {
                using (SolidBrush brush = new SolidBrush(bg))
                {
                    g.FillPath(brush, path);
                }
                using (Pen pen = new Pen(border, 1f))
                {
                    g.DrawPath(pen, path);
                }
            }

            Font useFont = isSelected ? new Font(Font, FontStyle.Bold) : Font;
            TextRenderer.DrawText(g, Text, useFont, ClientRectangle, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    public class SubtleDivider : Control
    {
        public SubtleDivider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            int y = Height / 2;
            using (Pen pen = new Pen(Color.FromArgb(38, 42, 54), 1f))
            {
                g.DrawLine(pen, 0, y, Width, y);
            }
        }
    }

    public class ModernProgressBar : Control
    {
        private int value = 0;
        public int Value
        {
            get { return value; }
            set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
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
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int h = Height;
            int radius = h / 2;
            Rectangle rect = new Rectangle(0, 0, Width, h);

            using (GraphicsPath trackPath = RoundedCard.GetRoundedRectangle(rect, radius))
            {
                using (SolidBrush brush = new SolidBrush(TrackColor))
                {
                    g.FillPath(brush, trackPath);
                }
            }

            if (value > 0)
            {
                int fillWidth = (int)((Width * (value / 100.0f)));
                if (fillWidth < radius * 2) fillWidth = radius * 2;
                if (fillWidth > Width) fillWidth = Width;

                Rectangle fillRect = new Rectangle(0, 0, fillWidth, h);
                using (GraphicsPath fillPath = RoundedCard.GetRoundedRectangle(fillRect, radius))
                {
                    using (SolidBrush brush = new SolidBrush(ProgressColor))
                    {
                        g.FillPath(brush, fillPath);
                    }
                }
            }
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

    #endregion

    #region Context Menu Custom Renderer

    public class ModernDarkMenuRenderer : ToolStripProfessionalRenderer
    {
        private static readonly Color bgCol = Color.FromArgb(24, 27, 34);
        private static readonly Color hoverCol = Color.FromArgb(38, 44, 58);
        private static readonly Color hoverBorder = Color.FromArgb(58, 68, 90);
        private static readonly Color textCol = Color.FromArgb(235, 240, 250);
        private static readonly Color disabledCol = Color.FromArgb(100, 110, 128);
        private static readonly Color separatorCol = Color.FromArgb(42, 48, 62);
        private static readonly Color accentGreen = Color.FromArgb(0, 230, 118);

        public ModernDarkMenuRenderer() : base(new DarkColorTable()) { }

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

            // Text is drawn starting at e.TextRectangle.X (aligning Header 'R' with other text below)
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

    #region Screen OSD Floating Notification Form

    public class DpiOsdForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_HIDEWINDOW = 0x0080;
        private const uint SWP_NOOWNERZORDER = 0x0200;
        private const uint SWP_NOSENDCHANGING = 0x0400;

        private int currentDpi = 3000;
        private int currentStage = 4;
        private int totalStages = 5;
        private int osdStyle = 0; // 0 = Centered Capsule, 1 = Stepped Gauge, 2 = Compact Top-Right
        private System.Windows.Forms.Timer displayTimer;
        private System.Windows.Forms.Timer fadeTimer;
        private float dpiScale = 1.0f;

        public int OsdStyle
        {
            get { return osdStyle; }
            set { osdStyle = value; }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE: never steal focus from full-screen games
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW: hide from Alt+Tab
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST: render above foreground windows
                cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT: mouse clicks pass directly through to games
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_MOUSEACTIVATE = 0x0021;
            const int MA_NOACTIVATE = 3;
            if (m.Msg == WM_MOUSEACTIVATE)
            {
                m.Result = (IntPtr)MA_NOACTIVATE;
                return;
            }
            base.WndProc(ref m);
        }

        public DpiOsdForm(float scale)
        {
            this.dpiScale = scale;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            // Note: Do NOT set this.TopMost = true; WinForms TopMost setter calls SetWindowPos without SWP_NOACTIVATE!
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.FromArgb(17, 19, 25);
            this.DoubleBuffered = true;
            this.Size = new Size((int)(160 * dpiScale), (int)(58 * dpiScale));

            // Pre-create native HWND so it never incurs creation latency during gaming
            IntPtr forceHandle = this.Handle;

            displayTimer = new System.Windows.Forms.Timer();
            displayTimer.Interval = 2000;
            displayTimer.Tick += (s, e) =>
            {
                displayTimer.Stop();
                fadeTimer.Start();
            };

            fadeTimer = new System.Windows.Forms.Timer();
            fadeTimer.Interval = 20;
            fadeTimer.Tick += (s, e) =>
            {
                if (this.Opacity > 0.08)
                {
                    this.Opacity -= 0.12;
                }
                else
                {
                    fadeTimer.Stop();
                    HideOsd();
                }
            };
        }

        public void HideOsd()
        {
            try
            {
                if (this.IsHandleCreated)
                {
                    SetWindowPos(this.Handle, IntPtr.Zero, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_HIDEWINDOW | SWP_NOSENDCHANGING);
                }
            }
            catch { }
        }

        public void ShowDpi(int dpi, int stage, int count)
        {
            this.currentDpi = dpi;
            this.currentStage = stage;
            this.totalStages = count > 0 ? count : 5;

            displayTimer.Stop();
            fadeTimer.Stop();
            this.Opacity = 1.0;

            int targetW;
            int targetH;
            if (osdStyle == 0)
            {
                targetW = 160;
                targetH = 58;
            }
            else if (osdStyle == 1)
            {
                targetW = (dpi >= 10000) ? 192 : 184;
                targetH = 62;
            }
            else
            {
                targetW = (dpi >= 10000) ? 178 : 170;
                targetH = 62;
            }

            int scaledW = (int)(targetW * dpiScale);
            int scaledH = (int)(targetH * dpiScale);

            Screen targetScreen = Screen.FromPoint(Cursor.Position);
            if (targetScreen == null) targetScreen = Screen.PrimaryScreen;
            Rectangle wa = targetScreen.WorkingArea;
            int margin = (int)(24 * dpiScale);

            int targetX = wa.Right - scaledW - margin;
            int targetY = wa.Bottom - scaledH - margin;

            // Pure Win32 display without activation or Z-order change that could minimize full-screen games
            SetWindowPos(this.Handle, HWND_TOPMOST, targetX, targetY, scaledW, scaledH,
                SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOSENDCHANGING);

            this.Invalidate();
            displayTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // 1. Common Card Background & Subtle Dark Edge
            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (GraphicsPath path = RoundedCard.GetRoundRectF(rect, 8f * dpiScale))
            {
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(17, 19, 25)))
                {
                    g.FillPath(bgBrush, path);
                }
                using (Pen borderPen = new Pen(Color.FromArgb(42, 47, 60), 1.0f))
                {
                    g.DrawPath(borderPen, path);
                }
            }

            using (FontFamily ffYaHei = new FontFamily("Microsoft YaHei UI"))
            using (FontFamily ffSegoe = new FontFamily("Segoe UI"))
            {
                StringFormat sf = StringFormat.GenericTypographic;

                if (osdStyle == 0)
                {
                    // ================= STYLE 0: 居中电竞胶囊 (Ultra-Compact, Symmetrical HUD) =================
                    // 1. Top Title (Centered Dot + "鼠标 DPI")
                    float topY = 7f * dpiScale;
                    float titleSize = 10f * dpiScale;
                    using (GraphicsPath titlePath = new GraphicsPath())
                    {
                        titlePath.AddString("鼠标 DPI", ffYaHei, (int)FontStyle.Regular, titleSize, PointF.Empty, sf);
                        RectangleF titleBounds = titlePath.GetBounds();

                        float dotD = 4f * dpiScale;
                        float dotGap = 4.5f * dpiScale;
                        float topContentW = dotD + dotGap + titleBounds.Width;
                        float topStartX = (Width - topContentW) / 2f;

                        float dotY = topY + (titleBounds.Height - dotD) / 2f;
                        using (SolidBrush dotBrush = new SolidBrush(Color.FromArgb(0, 230, 118)))
                            g.FillEllipse(dotBrush, topStartX, dotY, dotD, dotD);

                        using (Matrix mTitle = new Matrix())
                        {
                            mTitle.Translate(topStartX + dotD + dotGap - titleBounds.Left, topY - titleBounds.Top);
                            titlePath.Transform(mTitle);
                        }
                        using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(145, 155, 175)))
                            g.FillPath(titleBrush, titlePath);

                        // 3. Bottom Capsules (Row 3)
                        int segCount = totalStages > 0 ? totalStages : 5;
                        float segW = 19f * dpiScale;
                        float segH = 3.5f * dpiScale;
                        float segGap = 4f * dpiScale;
                        float totalSegW = segCount * segW + (segCount - 1) * segGap;
                        float segStartX = (Width - totalSegW) / 2f;
                        float bottomMargin = 8f * dpiScale;
                        float segY = Height - bottomMargin - segH;

                        for (int i = 1; i <= segCount; i++)
                        {
                            RectangleF segRect = new RectangleF(segStartX + (i - 1) * (segW + segGap), segY, segW, segH);
                            using (GraphicsPath sp = RoundedCard.GetRoundRectF(segRect, 1.75f * dpiScale))
                            {
                                Color c = (i == currentStage) ? Color.FromArgb(0, 230, 118) : Color.FromArgb(36, 40, 52);
                                using (SolidBrush b = new SolidBrush(c))
                                    g.FillPath(b, sp);
                            }
                        }

                        // 2. Middle Value ("3000" + "DPI") - Perfectly Centered & Exact Baseline Aligned
                        float numFontSize = 20f * dpiScale;
                        float unitFontSize = 10f * dpiScale;

                        using (GraphicsPath numPath = new GraphicsPath())
                        using (GraphicsPath unitPath = new GraphicsPath())
                        {
                            string numStr = currentDpi.ToString();
                            numPath.AddString(numStr, ffSegoe, (int)FontStyle.Bold, numFontSize, PointF.Empty, sf);
                            RectangleF numBounds = numPath.GetBounds();

                            unitPath.AddString("DPI", ffSegoe, (int)FontStyle.Bold, unitFontSize, PointF.Empty, sf);
                            RectangleF unitBounds = unitPath.GetBounds();

                            float valGap = 4f * dpiScale;
                            float totalValW = numBounds.Width + valGap + unitBounds.Width;
                            float valStartX = (Width - totalValW) / 2f;

                            float midZoneTop = topY + titleBounds.Height;
                            float midZoneBottom = segY;
                            float midZoneH = midZoneBottom - midZoneTop;
                            float targetNumTop = midZoneTop + (midZoneH - numBounds.Height) / 2f;
                            float targetBaseline = targetNumTop + numBounds.Height;

                            using (Matrix mNum = new Matrix())
                            {
                                mNum.Translate(valStartX - numBounds.Left, targetNumTop - numBounds.Top);
                                numPath.Transform(mNum);
                            }
                            using (SolidBrush whiteBrush = new SolidBrush(Color.FromArgb(248, 250, 255)))
                                g.FillPath(whiteBrush, numPath);

                            float targetUnitTop = targetBaseline - unitBounds.Height;
                            using (Matrix mUnit = new Matrix())
                            {
                                mUnit.Translate(valStartX + numBounds.Width + valGap - unitBounds.Left, targetUnitTop - unitBounds.Top);
                                unitPath.Transform(mUnit);
                            }
                            using (SolidBrush greenBrush = new SolidBrush(Color.FromArgb(0, 230, 118)))
                                g.FillPath(greenBrush, unitPath);
                        }
                    }
                }
                else if (osdStyle == 1)
                {
                    // ================= STYLE 1: 右侧阶梯能量计 (Faithful & Optical Left Aligned) =================
                    float padLeft = 14f * dpiScale;
                    float padRight = 14f * dpiScale;

                    float titleFontSize = 9.5f * dpiScale;
                    using (GraphicsPath titlePath = new GraphicsPath())
                    {
                        titlePath.AddString("鼠标 DPI", ffYaHei, (int)FontStyle.Regular, titleFontSize, PointF.Empty, sf);
                        RectangleF titleBounds = titlePath.GetBounds();

                        float numFontSize = (currentDpi >= 10000 ? 19.5f : 21.5f) * dpiScale;
                        float unitFontSize = 10f * dpiScale;

                        using (GraphicsPath numPath = new GraphicsPath())
                        using (GraphicsPath unitPath = new GraphicsPath())
                        {
                            string numStr = currentDpi.ToString();
                            numPath.AddString(numStr, ffSegoe, (int)FontStyle.Bold, numFontSize, PointF.Empty, sf);
                            RectangleF numBounds = numPath.GetBounds();

                            unitPath.AddString("DPI", ffSegoe, (int)FontStyle.Bold, unitFontSize, PointF.Empty, sf);
                            RectangleF unitBounds = unitPath.GetBounds();

                            float rowGap = 3.5f * dpiScale;
                            float totalLeftBlockH = titleBounds.Height + rowGap + numBounds.Height;
                            float startY = (Height - totalLeftBlockH) / 2f;

                            // 1. Top Left: Dot + "鼠标 DPI"
                            float topY = startY;
                            float dotD = 4.5f * dpiScale;
                            float dotGap = 5f * dpiScale;
                            float dotX = padLeft;
                            float dotY = topY + (titleBounds.Height - dotD) / 2f;

                            using (SolidBrush dotBrush = new SolidBrush(Color.FromArgb(0, 230, 118)))
                                g.FillEllipse(dotBrush, dotX, dotY, dotD, dotD);

                            float titleX = dotX + dotD + dotGap;
                            using (Matrix mTitle = new Matrix())
                            {
                                mTitle.Translate(titleX - titleBounds.Left, topY - titleBounds.Top);
                                titlePath.Transform(mTitle);
                            }
                            using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(145, 155, 175)))
                                g.FillPath(titleBrush, titlePath);

                            // 2. Large Value: Optical left anchor with dotX
                            float numY = topY + titleBounds.Height + rowGap;
                            float baselineY = numY + numBounds.Height;

                            using (Matrix mNum = new Matrix())
                            {
                                mNum.Translate(padLeft - numBounds.Left, numY - numBounds.Top);
                                numPath.Transform(mNum);
                            }
                            using (SolidBrush whiteBrush = new SolidBrush(Color.FromArgb(248, 250, 255)))
                                g.FillPath(whiteBrush, numPath);

                            // Unit: Exact baseline alignment
                            float valGap = 4f * dpiScale;
                            float unitX = padLeft + numBounds.Width + valGap;
                            float unitY = baselineY - unitBounds.Height;

                            using (Matrix mUnit = new Matrix())
                            {
                                mUnit.Translate(unitX - unitBounds.Left, unitY - unitBounds.Top);
                                unitPath.Transform(mUnit);
                            }
                            using (SolidBrush greenBrush = new SolidBrush(Color.FromArgb(0, 230, 118)))
                                g.FillPath(greenBrush, unitPath);

                            // 3. Right Stepped Energy Bars
                            int barCount = totalStages > 0 ? totalStages : 5;
                            float barW = 4.5f * dpiScale;
                            float barGap = 4f * dpiScale;
                            float totalBarsW = barCount * barW + (barCount - 1) * barGap;
                            float barStartX = Width - padRight - totalBarsW;
                            float barBaseY = baselineY + (0.5f * dpiScale);

                            float minH = 8f * dpiScale;
                            float maxH = 25f * dpiScale;
                            float stepH = (maxH - minH) / (barCount - 1);

                            for (int i = 1; i <= barCount; i++)
                            {
                                float barH = minH + (i - 1) * stepH;
                                float barY = barBaseY - barH;
                                RectangleF barRect = new RectangleF(barStartX + (i - 1) * (barW + barGap), barY, barW, barH);
                                using (GraphicsPath bp = RoundedCard.GetRoundRectF(barRect, 1.75f * dpiScale))
                                {
                                    Color c;
                                    if (i == currentStage)
                                        c = Color.FromArgb(0, 230, 118); // Active stage: Razer green
                                    else if (i < currentStage)
                                        c = Color.FromArgb(0, 135, 68);  // Lower stages: deep green
                                    else
                                        c = Color.FromArgb(38, 43, 56);  // Higher stages: dark grey track

                                    using (SolidBrush b = new SolidBrush(c))
                                        g.FillPath(b, bp);
                                }
                            }
                        }
                    }
                }
                else
                {
                    // ================= STYLE 2: 顶置微型指示段 (Faithful & Balanced Compact) =================
                    float padLeft = 14f * dpiScale;
                    float padRight = 14f * dpiScale;

                    float titleFontSize = 9.5f * dpiScale;
                    using (GraphicsPath titlePath = new GraphicsPath())
                    {
                        titlePath.AddString("鼠标 DPI", ffYaHei, (int)FontStyle.Regular, titleFontSize, PointF.Empty, sf);
                        RectangleF titleBounds = titlePath.GetBounds();

                        float numFontSize = (currentDpi >= 10000 ? 19.5f : 21.5f) * dpiScale;
                        float unitFontSize = 10f * dpiScale;

                        using (GraphicsPath numPath = new GraphicsPath())
                        using (GraphicsPath unitPath = new GraphicsPath())
                        {
                            string numStr = currentDpi.ToString();
                            numPath.AddString(numStr, ffSegoe, (int)FontStyle.Bold, numFontSize, PointF.Empty, sf);
                            RectangleF numBounds = numPath.GetBounds();

                            unitPath.AddString("DPI", ffSegoe, (int)FontStyle.Bold, unitFontSize, PointF.Empty, sf);
                            RectangleF unitBounds = unitPath.GetBounds();

                            float rowGap = 3.5f * dpiScale;
                            float totalLeftBlockH = titleBounds.Height + rowGap + numBounds.Height;
                            float startY = (Height - totalLeftBlockH) / 2f;
                            float topY = startY;

                            // 1. Top Left: Accent Bar + "鼠标 DPI"
                            float barW = 3f * dpiScale;
                            float barGap = 5.5f * dpiScale;
                            float barH = titleBounds.Height - 1f * dpiScale;
                            RectangleF pillRect = new RectangleF(padLeft, topY + 0.5f * dpiScale, barW, barH);
                            using (GraphicsPath vp = RoundedCard.GetRoundRectF(pillRect, 1.25f * dpiScale))
                            using (SolidBrush vb = new SolidBrush(Color.FromArgb(0, 230, 118)))
                                g.FillPath(vb, vp);

                            float titleX = padLeft + barW + barGap;
                            using (Matrix mTitle = new Matrix())
                            {
                                mTitle.Translate(titleX - titleBounds.Left, topY - titleBounds.Top);
                                titlePath.Transform(mTitle);
                            }
                            using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(145, 155, 175)))
                                g.FillPath(titleBrush, titlePath);

                            // 2. Top Right: 5 Mini Capsules
                            int segCount = totalStages > 0 ? totalStages : 5;
                            float segW = 11f * dpiScale;
                            float segH = 4.2f * dpiScale;
                            float segGap = 3.5f * dpiScale;
                            float totalSegW = segCount * segW + (segCount - 1) * segGap;
                            float segStartX = Width - padRight - totalSegW;
                            float segY = topY + (titleBounds.Height - segH) / 2f;

                            for (int i = 1; i <= segCount; i++)
                            {
                                RectangleF segRect = new RectangleF(segStartX + (i - 1) * (segW + segGap), segY, segW, segH);
                                using (GraphicsPath sp = RoundedCard.GetRoundRectF(segRect, 1.75f * dpiScale))
                                {
                                    Color c = (i == currentStage) ? Color.FromArgb(0, 230, 118) : Color.FromArgb(38, 43, 56);
                                    using (SolidBrush b = new SolidBrush(c))
                                        g.FillPath(b, sp);
                                }
                            }

                            // 3. Bottom Row: Number + Unit
                            float numY = topY + titleBounds.Height + rowGap;
                            float baselineY = numY + numBounds.Height;

                            using (Matrix mNum = new Matrix())
                            {
                                mNum.Translate(padLeft - numBounds.Left, numY - numBounds.Top);
                                numPath.Transform(mNum);
                            }
                            using (SolidBrush whiteBrush = new SolidBrush(Color.FromArgb(248, 250, 255)))
                                g.FillPath(whiteBrush, numPath);

                            float valGap = 4f * dpiScale;
                            float unitX = padLeft + numBounds.Width + valGap;
                            float unitY = baselineY - unitBounds.Height;

                            using (Matrix mUnit = new Matrix())
                            {
                                mUnit.Translate(unitX - unitBounds.Left, unitY - unitBounds.Top);
                                unitPath.Transform(mUnit);
                            }
                            using (SolidBrush greenBrush = new SolidBrush(Color.FromArgb(0, 230, 118)))
                                g.FillPath(greenBrush, unitPath);
                        }
                    }
                }
            }
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

        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        const ushort HID_USAGE_PAGE_GENERIC = 0x01;
        const ushort HID_USAGE_GENERIC_MOUSE = 0x02;
        const uint RIDEV_INPUTSINK = 0x00000100;
        const int WM_INPUT = 0x00FF;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterRawInputDevices([MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

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
        private ToolStripMenuItem autoStartShowUIMenuItem;
        private ToolStripMenuItem lowBatteryAlertMenuItem;
        private ToolStripMenuItem dpiOsdMenuItem;
        private ToolStripMenuItem styleCapsuleItem;
        private ToolStripMenuItem styleNumItem;
        private ToolStripMenuItem osdStyleMenu;
        private ToolStripMenuItem osdStyleCapsuleItem;
        private ToolStripMenuItem osdStyleGaugeItem;
        private ToolStripMenuItem osdStyleCompactItem;
        private ToolStripMenuItem int30sMenuItem;
        private ToolStripMenuItem int1mMenuItem;
        private ToolStripMenuItem int5mMenuItem;
        private ToolStripMenuItem dpiMenu;
        private ToolStripMenuItem rateMenu;
        private System.Windows.Forms.Timer updateTimer;

        // Mouse Sleeping & Instant Wakeup
        private volatile bool isMouseSleeping = false;
        private System.Windows.Forms.Timer wakeBurstTimer;
        private int wakeRetryCount = 0;
        private System.Windows.Forms.Timer bootPollTimer;
        private int bootPollCount = 0;

        // AutoStart Window Visibility Control
        private bool isAutoStartLaunch = false;
        private bool autoStartShowMainWindow = false;
        private bool allowVisibleCore = false;

        // Background DPI Listener Thread
        private Thread dpiMonitorThread;
        private volatile bool isDpiMonitorRunning = false;
        private int lastMonitoredDpi = -1;
        private int lastMonitoredStage = -1;
        private DpiOsdForm osdForm;

        // Visual controls - Card 1: Status
        private RoundedCard cardBattery;
        private Label lblDeviceName;
        private Label lblConnDot;
        private Label lblBatteryBig;
        private StatusPill pillStatus;
        private ModernProgressBar barBattery;
        private Label lblUpdateTime;

        // Visual controls - Card 2: Performance & Tuning
        private RoundedCard cardPerformance;
        private Label lblPerfTitle;
        private Label lblDpiTitle;
        private ModernSegmentButton[] btnDpiStages;
        private int[] dpiStageValues = new int[] { 400, 800, 1600, 3000, 6400 };
        private Label lblRateTitle;
        private ModernSegmentButton[] btnRates;
        private int[] pollingRateValues = new int[] { 1000, 2000, 4000, 8000 };

        // Visual controls - Card 3: Settings & Preferences
        private RoundedCard cardSettings;
        private Label lblSettingsTitle;
        private Label lblStyleTitle;
        private ModernSegmentButton btnStyleCapsule;
        private ModernSegmentButton btnStyleNum;
        private Label lblIntervalTitle;
        private ModernSegmentButton btnInt30s;
        private ModernSegmentButton btnInt1m;
        private ModernSegmentButton btnInt5m;
        private Label lblOsdStyleTitle;
        private ModernSegmentButton btnOsdStyleCapsule;
        private ModernSegmentButton btnOsdStyleGauge;
        private ModernSegmentButton btnOsdStyleCompact;
        private SubtleDivider divSettings;
        private ModernCheckBox chkAutoStart;
        private ModernCheckBox chkAutoStartShowUI;
        private ModernCheckBox chkLowAlert;
        private ModernCheckBox chkDpiOsd;
        private Label lblSettingsTip;

        // Visual controls - Bottom Actions
        private ModernButton btnRefresh;
        private ModernButton btnHideToTray;

        private const string AppName = "RazerBatteryTray";
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ConfigRegistryKey = @"Software\RazerBatteryTray";

        private bool lowBatteryAlertEnabled = true;
        private bool dpiOsdEnabled = true;
        private bool lastLowAlertFired = false;
        private bool isUpdatingUI = false;
        private int trayStyle = 0; // 0 = Capsule, 1 = Number
        private int osdStyle = 0; // 0 = Centered Capsule, 1 = Stepped Gauge, 2 = Compact Top-Right
        private float dpiScale = 1.0f;
        private MouseBatteryInfo lastInfo = null;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool DestroyIcon(IntPtr handle);

        public MainForm(bool isAutoStart = false)
        {
            this.isAutoStartLaunch = isAutoStart;

            using (Graphics g = this.CreateGraphics())
            {
                dpiScale = g.DpiX / 96.0f;
                if (dpiScale < 1.0f) dpiScale = 1.0f;
            }

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

            osdForm = new DpiOsdForm(dpiScale);

            RazerDeviceHelper.LoadHardwareCache();
            InitializeFormUI();
            InitializeTray();
            LoadConfig();

            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Interval = userSelectedInterval;
            updateTimer.Tick += (s, e) => RefreshBatteryStatus(false);
            updateTimer.Start();

            RefreshBatteryStatus(false);
            StartBootPolling();
            StartDpiMonitor();
        }

        protected override void SetVisibleCore(bool value)
        {
            if (isAutoStartLaunch && !autoStartShowMainWindow && !allowVisibleCore)
            {
                value = false;
                if (!this.IsHandleCreated) CreateHandle();
            }
            base.SetVisibleCore(value);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyModernWin11Theme();
            RegisterUsbNotification();
            RegisterMouseRawInput();
        }

        private void RegisterMouseRawInput()
        {
            try
            {
                RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];
                rid[0].usUsagePage = HID_USAGE_PAGE_GENERIC;
                rid[0].usUsage = HID_USAGE_GENERIC_MOUSE;
                rid[0].dwFlags = RIDEV_INPUTSINK;
                rid[0].hwndTarget = this.Handle;
                RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
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
            else if (m.Msg == WM_INPUT)
            {
                OnMouseRawInputActivity();
            }
            base.WndProc(ref m);
        }

        private void OnMouseRawInputActivity()
        {
            if (isMouseSleeping || lastInfo == null || !lastInfo.IsConnected || lastInfo.BatteryPercent <= 0)
            {
                TriggerWakeBurst();
            }
        }

        private void TriggerWakeBurst()
        {
            if (wakeBurstTimer == null)
            {
                wakeBurstTimer = new System.Windows.Forms.Timer();
                wakeBurstTimer.Tick += WakeBurstTimer_Tick;
            }

            if (!wakeBurstTimer.Enabled)
            {
                wakeRetryCount = 0;
                wakeBurstTimer.Interval = 80;
                wakeBurstTimer.Start();
            }
        }

        private void WakeBurstTimer_Tick(object sender, EventArgs e)
        {
            wakeRetryCount++;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var info = RazerDeviceHelper.QueryRazerDeviceInfo();
                try
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        if (info != null)
                        {
                            lastInfo = info;
                            UpdateUI(info, false);

                            if (info.IsConnected && !info.IsSleeping && info.BatteryPercent > 0)
                            {
                                if (wakeBurstTimer != null) wakeBurstTimer.Stop();
                                wakeRetryCount = 0;
                                return;
                            }
                        }

                        if (wakeRetryCount >= 6)
                        {
                            if (wakeBurstTimer != null) wakeBurstTimer.Stop();
                            wakeRetryCount = 0;
                        }
                        else if (wakeBurstTimer != null && wakeBurstTimer.Enabled)
                        {
                            wakeBurstTimer.Interval = 100 + wakeRetryCount * 150;
                        }
                    }));
                }
                catch { }
            });
        }

        private void StartBootPolling()
        {
            bootPollCount = 0;
            if (bootPollTimer == null)
            {
                bootPollTimer = new System.Windows.Forms.Timer();
                bootPollTimer.Interval = 1200;
                bootPollTimer.Tick += (s, e) =>
                {
                    bootPollCount++;
                    if (lastInfo == null || lastInfo.IsSleeping || !lastInfo.IsConnected || lastInfo.BatteryPercent <= 0)
                    {
                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            var info = RazerDeviceHelper.QueryRazerDeviceInfo();
                            try
                            {
                                this.BeginInvoke(new Action(() =>
                                {
                                    lastInfo = info;
                                    UpdateUI(info, false);
                                }));
                            }
                            catch { }
                        });
                    }
                    if (bootPollCount >= 8 || (lastInfo != null && lastInfo.IsConnected && !lastInfo.IsSleeping && lastInfo.BatteryPercent > 0))
                    {
                        bootPollTimer.Stop();
                    }
                };
            }
            bootPollTimer.Start();
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


        private void OnDeviceHardwareChange()
        {
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
                int trueVal = 1;
                DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref trueVal, sizeof(int));
                DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref trueVal, sizeof(int));

                int roundVal = 2;
                DwmSetWindowAttribute(this.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref roundVal, sizeof(int));

                int captionBg = 0x00171312;
                DwmSetWindowAttribute(this.Handle, DWMWA_CAPTION_COLOR, ref captionBg, sizeof(int));

                int captionText = 0x00FFFFFF;
                DwmSetWindowAttribute(this.Handle, DWMWA_TEXT_COLOR, ref captionText, sizeof(int));

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
            this.BackColor = Color.FromArgb(18, 19, 23);
            this.ForeColor = Color.White;
            this.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.ShowInTaskbar = true;

            int padX = (int)(18 * dpiScale);
            int baseW = (int)(470 * dpiScale);
            int cardW = baseW - (padX * 2);

            // ================= CARD 1: BATTERY & DEVICE STATUS =================
            int card1Y = (int)(14 * dpiScale);
            int card1H = (int)(176 * dpiScale);

            cardBattery = new RoundedCard();
            cardBattery.Location = new Point(padX, card1Y);
            cardBattery.Size = new Size(cardW, card1H);
            cardBattery.CornerRadius = (int)(10 * dpiScale);
            cardBattery.BackColor = Color.FromArgb(25, 27, 34);
            cardBattery.BorderColor = Color.FromArgb(42, 46, 58);
            this.Controls.Add(cardBattery);

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

            lblBatteryBig = new Label();
            lblBatteryBig.Text = "--%";
            lblBatteryBig.Font = new Font("Microsoft YaHei UI", 34F, FontStyle.Bold, GraphicsUnit.Point);
            lblBatteryBig.ForeColor = Color.FromArgb(0, 230, 118);
            lblBatteryBig.Location = new Point((int)(16 * dpiScale), (int)(46 * dpiScale));
            lblBatteryBig.AutoSize = true;
            cardBattery.Controls.Add(lblBatteryBig);

            // Status Pill: Power / Charging State (sole status badge in Card 1)
            pillStatus = new StatusPill();
            pillStatus.Location = new Point((int)(180 * dpiScale), (int)(60 * dpiScale));
            pillStatus.Size = new Size((int)(110 * dpiScale), (int)(32 * dpiScale));
            pillStatus.Font = new Font("Microsoft YaHei UI", 9.2F, FontStyle.Bold, GraphicsUnit.Point);
            pillStatus.SetStatus("⚡ 充电中", Color.FromArgb(0, 230, 118));
            cardBattery.Controls.Add(pillStatus);

            barBattery = new ModernProgressBar();
            barBattery.Location = new Point((int)(18 * dpiScale), (int)(118 * dpiScale));
            barBattery.Size = new Size(cardW - (int)(36 * dpiScale), (int)(10 * dpiScale));
            barBattery.Value = 0;
            barBattery.TrackColor = Color.FromArgb(38, 42, 53);
            barBattery.ProgressColor = Color.FromArgb(0, 230, 118);
            cardBattery.Controls.Add(barBattery);

            lblUpdateTime = new Label();
            lblUpdateTime.Text = "最后同步: --:--:-- · 自动侦测硬件插拔";
            lblUpdateTime.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
            lblUpdateTime.ForeColor = Color.FromArgb(135, 142, 156);
            lblUpdateTime.Location = new Point((int)(18 * dpiScale), (int)(142 * dpiScale));
            lblUpdateTime.AutoSize = true;
            cardBattery.Controls.Add(lblUpdateTime);

            // ================= CARD 2: PERFORMANCE & TUNING =================
            int card2Y = card1Y + card1H + (int)(12 * dpiScale);
            int card2H = (int)(122 * dpiScale);

            cardPerformance = new RoundedCard();
            cardPerformance.Location = new Point(padX, card2Y);
            cardPerformance.Size = new Size(cardW, card2H);
            cardPerformance.CornerRadius = (int)(10 * dpiScale);
            cardPerformance.BackColor = Color.FromArgb(25, 27, 34);
            cardPerformance.BorderColor = Color.FromArgb(42, 46, 58);
            this.Controls.Add(cardPerformance);

            int cPad = (int)(18 * dpiScale);
            int secW = cardW - (cPad * 2);

            lblPerfTitle = new Label();
            lblPerfTitle.Text = "鼠标性能与档位调节";
            lblPerfTitle.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            lblPerfTitle.ForeColor = Color.FromArgb(240, 245, 255);
            lblPerfTitle.Location = new Point(cPad, (int)(14 * dpiScale));
            lblPerfTitle.AutoSize = true;
            cardPerformance.Controls.Add(lblPerfTitle);

            // Row 1: DPI 档位
            int r1Y = (int)(40 * dpiScale);
            int lblW = (int)(75 * dpiScale);
            int segH = (int)(28 * dpiScale);

            lblDpiTitle = new Label();
            lblDpiTitle.Text = "DPI 档位";
            lblDpiTitle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            lblDpiTitle.ForeColor = Color.FromArgb(160, 168, 185);
            lblDpiTitle.Location = new Point(cPad, r1Y + (int)(4 * dpiScale));
            lblDpiTitle.Size = new Size(lblW, (int)(22 * dpiScale));
            cardPerformance.Controls.Add(lblDpiTitle);

            int dpiStartX = cPad + lblW;
            int dpiGap = (int)(6 * dpiScale);
            int dpiSegW = (secW - lblW - (dpiGap * 4)) / 5;

            btnDpiStages = new ModernSegmentButton[5];
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                int dpiVal = dpiStageValues[i];
                var btn = new ModernSegmentButton();
                btn.Text = dpiVal.ToString();
                btn.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
                btn.Location = new Point(dpiStartX + i * (dpiSegW + dpiGap), r1Y);
                btn.Size = new Size(dpiSegW, segH);
                btn.Click += (s, e) => SetDpiFromUI(dpiStageValues[index]);
                cardPerformance.Controls.Add(btn);
                btnDpiStages[i] = btn;
            }

            // Row 2: 回报率
            int r2Y = (int)(76 * dpiScale);

            lblRateTitle = new Label();
            lblRateTitle.Text = "回报率";
            lblRateTitle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            lblRateTitle.ForeColor = Color.FromArgb(160, 168, 185);
            lblRateTitle.Location = new Point(cPad, r2Y + (int)(4 * dpiScale));
            lblRateTitle.Size = new Size(lblW, (int)(22 * dpiScale));
            cardPerformance.Controls.Add(lblRateTitle);

            int hzStartX = cPad + lblW;
            int hzGap = (int)(6 * dpiScale);
            int hzSegW = (secW - lblW - (hzGap * 3)) / 4;

            btnRates = new ModernSegmentButton[4];
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                int hzVal = pollingRateValues[i];
                var btn = new ModernSegmentButton();
                btn.Text = hzVal + " Hz";
                btn.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
                btn.Location = new Point(hzStartX + i * (hzSegW + hzGap), r2Y);
                btn.Size = new Size(hzSegW, segH);
                btn.Click += (s, e) => SetPollingRateFromUI(pollingRateValues[index]);
                cardPerformance.Controls.Add(btn);
                btnRates[i] = btn;
            }

            // ================= CARD 3: CONFIGURATION & PREFERENCES =================
            int card3Y = card2Y + card2H + (int)(12 * dpiScale);
            int card3H = (int)(244 * dpiScale);

            cardSettings = new RoundedCard();
            cardSettings.Location = new Point(padX, card3Y);
            cardSettings.Size = new Size(cardW, card3H);
            cardSettings.CornerRadius = (int)(10 * dpiScale);
            cardSettings.BackColor = Color.FromArgb(25, 27, 34);
            cardSettings.BorderColor = Color.FromArgb(42, 46, 58);
            this.Controls.Add(cardSettings);

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

            lblStyleTitle = new Label();
            lblStyleTitle.Text = "托盘图标样式";
            lblStyleTitle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            lblStyleTitle.ForeColor = Color.FromArgb(160, 168, 185);
            lblStyleTitle.Location = new Point(cPad, row1Y + (int)(4 * dpiScale));
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
            int row2Y = (int)(76 * dpiScale);

            lblIntervalTitle = new Label();
            lblIntervalTitle.Text = "自动刷新频率";
            lblIntervalTitle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            lblIntervalTitle.ForeColor = Color.FromArgb(160, 168, 185);
            lblIntervalTitle.Location = new Point(cPad, row2Y + (int)(4 * dpiScale));
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

            // Row 3: DPI 浮窗样式 (居中胶囊 / 阶梯能量 / 顶置微标)
            int row3Y = (int)(112 * dpiScale);

            lblOsdStyleTitle = new Label();
            lblOsdStyleTitle.Text = "DPI 浮窗样式";
            lblOsdStyleTitle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            lblOsdStyleTitle.ForeColor = Color.FromArgb(160, 168, 185);
            lblOsdStyleTitle.Location = new Point(cPad, row3Y + (int)(4 * dpiScale));
            lblOsdStyleTitle.Size = new Size(lblTitleW, (int)(22 * dpiScale));
            cardSettings.Controls.Add(lblOsdStyleTitle);

            btnOsdStyleCapsule = new ModernSegmentButton();
            btnOsdStyleCapsule.Text = "居中胶囊";
            btnOsdStyleCapsule.Font = new Font("Microsoft YaHei UI", 8.8F, FontStyle.Regular, GraphicsUnit.Point);
            btnOsdStyleCapsule.Location = new Point(seg1X, row3Y);
            btnOsdStyleCapsule.Size = new Size(seg2W, segH);
            btnOsdStyleCapsule.Click += (s, e) => SetOsdStyle(0, true);
            cardSettings.Controls.Add(btnOsdStyleCapsule);

            btnOsdStyleGauge = new ModernSegmentButton();
            btnOsdStyleGauge.Text = "阶梯能量";
            btnOsdStyleGauge.Font = new Font("Microsoft YaHei UI", 8.8F, FontStyle.Regular, GraphicsUnit.Point);
            btnOsdStyleGauge.Location = new Point(seg1X + seg2W + seg2Gap, row3Y);
            btnOsdStyleGauge.Size = new Size(seg2W, segH);
            btnOsdStyleGauge.Click += (s, e) => SetOsdStyle(1, true);
            cardSettings.Controls.Add(btnOsdStyleGauge);

            btnOsdStyleCompact = new ModernSegmentButton();
            btnOsdStyleCompact.Text = "顶置微标";
            btnOsdStyleCompact.Font = new Font("Microsoft YaHei UI", 8.8F, FontStyle.Regular, GraphicsUnit.Point);
            btnOsdStyleCompact.Location = new Point(seg1X + (seg2W + seg2Gap) * 2, row3Y);
            btnOsdStyleCompact.Size = new Size(seg2W, segH);
            btnOsdStyleCompact.Click += (s, e) => SetOsdStyle(2, true);
            cardSettings.Controls.Add(btnOsdStyleCompact);

            // Row 4: Modern subtle divider
            divSettings = new SubtleDivider();
            divSettings.Location = new Point(cPad, (int)(150 * dpiScale));
            divSettings.Size = new Size(secW, (int)(8 * dpiScale));
            cardSettings.Controls.Add(divSettings);

            // Row 5: Checkboxes in 2x2 grid
            int chkY1 = (int)(162 * dpiScale);
            int chkY2 = (int)(190 * dpiScale);
            int chkGap = (int)(14 * dpiScale);
            int chkW = (secW - chkGap) / 2;
            int chkH = (int)(24 * dpiScale);

            chkAutoStart = new ModernCheckBox();
            chkAutoStart.Text = "开机自动启动";
            chkAutoStart.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            chkAutoStart.Location = new Point(cPad, chkY1);
            chkAutoStart.Size = new Size(chkW, chkH);
            chkAutoStart.CheckedChanged += (s, e) => {
                if (isUpdatingUI) return;
                SetAutoStart(chkAutoStart.Checked);
            };
            cardSettings.Controls.Add(chkAutoStart);

            chkAutoStartShowUI = new ModernCheckBox();
            chkAutoStartShowUI.Text = "开机弹出主窗口";
            chkAutoStartShowUI.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            chkAutoStartShowUI.Location = new Point(cPad + chkW + chkGap, chkY1);
            chkAutoStartShowUI.Size = new Size(chkW, chkH);
            chkAutoStartShowUI.CheckedChanged += (s, e) => {
                if (isUpdatingUI) return;
                SetAutoStartShowUI(chkAutoStartShowUI.Checked, true);
            };
            cardSettings.Controls.Add(chkAutoStartShowUI);

            chkLowAlert = new ModernCheckBox();
            chkLowAlert.Text = "低电量提醒 (≤20%)";
            chkLowAlert.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            chkLowAlert.Location = new Point(cPad, chkY2);
            chkLowAlert.Size = new Size(chkW, chkH);
            chkLowAlert.CheckedChanged += (s, e) => {
                if (isUpdatingUI) return;
                SetLowBatteryAlert(chkLowAlert.Checked);
            };
            cardSettings.Controls.Add(chkLowAlert);

            chkDpiOsd = new ModernCheckBox();
            chkDpiOsd.Text = "DPI 切换屏幕提示 (OSD)";
            chkDpiOsd.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            chkDpiOsd.Location = new Point(cPad + chkW + chkGap, chkY2);
            chkDpiOsd.Size = new Size(chkW, chkH);
            chkDpiOsd.CheckedChanged += (s, e) => {
                if (isUpdatingUI) return;
                SetDpiOsdEnabled(chkDpiOsd.Checked);
            };
            cardSettings.Controls.Add(chkDpiOsd);

            // Row 6: Hint inside Card 3
            lblSettingsTip = new Label();
            lblSettingsTip.Text = "注：切换线缆/接收器或按键调 DPI 时将自动即时同步，无需等待计时周期";
            lblSettingsTip.Font = new Font("Microsoft YaHei UI", 8.2F, FontStyle.Regular, GraphicsUnit.Point);
            lblSettingsTip.ForeColor = Color.FromArgb(120, 128, 142);
            lblSettingsTip.Location = new Point(cPad, (int)(218 * dpiScale));
            lblSettingsTip.AutoSize = true;
            cardSettings.Controls.Add(lblSettingsTip);

            // ================= BOTTOM ROW: ACTIONS =================
            int card4Y = card3Y + card3H + (int)(14 * dpiScale);
            int btnH = (int)(38 * dpiScale);
            int btnGap = (int)(14 * dpiScale);
            int btnW = (cardW - btnGap) / 2;

            btnRefresh = new ModernButton();
            btnRefresh.Text = "立即刷新";
            btnRefresh.Location = new Point(padX, card4Y);
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
            btnHideToTray.Location = new Point(padX + btnW + btnGap, card4Y);
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

            int clientH = card4Y + btnH + (int)(16 * dpiScale);
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

            // Submenu: DPI 调节
            dpiMenu = new ToolStripMenuItem("调节 DPI 档位 (&D)");
            dpiMenu.DropDown.Renderer = contextMenu.Renderer;
            for (int i = 0; i < dpiStageValues.Length; i++)
            {
                int val = dpiStageValues[i];
                var sub = new ToolStripMenuItem(val + " DPI", null, (s, e) => SetDpiFromUI(val));
                sub.Tag = val;
                dpiMenu.DropDownItems.Add(sub);
            }
            contextMenu.Items.Add(dpiMenu);

            // Submenu: 回报率调节
            rateMenu = new ToolStripMenuItem("调节回报率 (&P)");
            rateMenu.DropDown.Renderer = contextMenu.Renderer;
            for (int i = 0; i < pollingRateValues.Length; i++)
            {
                int val = pollingRateValues[i];
                var sub = new ToolStripMenuItem(val + " Hz", null, (s, e) => SetPollingRateFromUI(val));
                sub.Tag = val;
                rateMenu.DropDownItems.Add(sub);
            }
            contextMenu.Items.Add(rateMenu);

            var intervalMenu = new ToolStripMenuItem("自动刷新频率 (&I)");
            intervalMenu.DropDown.Renderer = contextMenu.Renderer;
            int30sMenuItem = new ToolStripMenuItem("30 秒", null, (s, e) => SetInterval(30000, true));
            int1mMenuItem = new ToolStripMenuItem("1 分钟", null, (s, e) => SetInterval(60000, true));
            int5mMenuItem = new ToolStripMenuItem("5 分钟", null, (s, e) => SetInterval(300000, true));
            intervalMenu.DropDownItems.AddRange(new ToolStripItem[] { int30sMenuItem, int1mMenuItem, int5mMenuItem });
            contextMenu.Items.Add(intervalMenu);

            var styleMenu = new ToolStripMenuItem("托盘图标样式 (&T)");
            styleMenu.DropDown.Renderer = contextMenu.Renderer;
            styleCapsuleItem = new ToolStripMenuItem("现代胶囊电池", null, (s, e) => SetTrayStyle(0, true));
            styleNumItem = new ToolStripMenuItem("醒目数字能量表", null, (s, e) => SetTrayStyle(1, true));
            styleCapsuleItem.Checked = (trayStyle == 0);
            styleNumItem.Checked = (trayStyle == 1);
            styleMenu.DropDownItems.AddRange(new ToolStripItem[] { styleCapsuleItem, styleNumItem });
            contextMenu.Items.Add(styleMenu);

            osdStyleMenu = new ToolStripMenuItem("DPI 浮窗样式 (&O)");
            osdStyleMenu.DropDown.Renderer = contextMenu.Renderer;
            osdStyleCapsuleItem = new ToolStripMenuItem("居中电竞胶囊", null, (s, e) => SetOsdStyle(0, true));
            osdStyleGaugeItem = new ToolStripMenuItem("右侧阶梯能量计", null, (s, e) => SetOsdStyle(1, true));
            osdStyleCompactItem = new ToolStripMenuItem("顶置微型指示段", null, (s, e) => SetOsdStyle(2, true));
            osdStyleCapsuleItem.Checked = (osdStyle == 0);
            osdStyleGaugeItem.Checked = (osdStyle == 1);
            osdStyleCompactItem.Checked = (osdStyle == 2);
            osdStyleMenu.DropDownItems.AddRange(new ToolStripItem[] { osdStyleCapsuleItem, osdStyleGaugeItem, osdStyleCompactItem });
            contextMenu.Items.Add(osdStyleMenu);

            contextMenu.Items.Add(new ToolStripSeparator());

            dpiOsdMenuItem = new ToolStripMenuItem("DPI 切换屏幕提示 (OSD)", null, (s, e) => {
                SetDpiOsdEnabled(!dpiOsdEnabled, true);
            });
            contextMenu.Items.Add(dpiOsdMenuItem);

            lowBatteryAlertMenuItem = new ToolStripMenuItem("低电量气泡通知 (≤20%)", null, (s, e) => {
                SetLowBatteryAlert(!lowBatteryAlertEnabled, true);
            });
            contextMenu.Items.Add(lowBatteryAlertMenuItem);

            autoStartMenuItem = new ToolStripMenuItem("开机自动启动", null, (s, e) => {
                SetAutoStart(!IsAutoStartEnabled(), true);
            });
            contextMenu.Items.Add(autoStartMenuItem);

            autoStartShowUIMenuItem = new ToolStripMenuItem("开机弹出主窗口", null, (s, e) => {
                SetAutoStartShowUI(!autoStartShowMainWindow, true);
            });
            contextMenu.Items.Add(autoStartShowUIMenuItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("退出程序 (&X)", null, (s, e) => ExitApp());
            contextMenu.Items.Add(exitItem);

            foreach (ToolStripItem item in contextMenu.Items)
            {
                item.Padding = new Padding(6, 4, 12, 4);
            }

            trayIcon = new NotifyIcon();
            trayIcon.ContextMenuStrip = contextMenu;
            trayIcon.Text = "雷蛇鼠标检测中...";
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
            allowVisibleCore = true;
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
                StopDpiMonitor();
                base.OnFormClosing(e);
            }
        }

        private void StartDpiMonitor()
        {
            if (dpiMonitorThread != null && dpiMonitorThread.IsAlive) return;
            isDpiMonitorRunning = true;
            dpiMonitorThread = new Thread(DpiMonitorWorker);
            dpiMonitorThread.IsBackground = true;
            dpiMonitorThread.Name = "RazerDpiMonitorWorker";
            dpiMonitorThread.Start();
        }

        private void StopDpiMonitor()
        {
            isDpiMonitorRunning = false;
        }

        private void DpiMonitorWorker()
        {
            while (isDpiMonitorRunning)
            {
                try
                {
                    int dpi, stage, count;
                    if (RazerDeviceHelper.FastQueryDpi(out dpi, out stage, out count))
                    {
                        if (isMouseSleeping || lastInfo == null || !lastInfo.IsConnected || (lastInfo != null && lastInfo.BatteryPercent <= 0))
                        {
                            isMouseSleeping = false;
                            try
                            {
                                this.BeginInvoke(new Action(() => RefreshBatteryStatus(false)));
                            }
                            catch { }
                        }

                        if (dpi > 0)
                        {
                            if (lastMonitoredDpi != -1 && (dpi != lastMonitoredDpi || stage != lastMonitoredStage))
                            {
                                int newDpi = dpi;
                                int newStage = stage;
                                int newCount = count;
                                try
                                {
                                    this.BeginInvoke(new Action(() => {
                                        OnDpiChanged(newDpi, newStage, newCount);
                                    }));
                                }
                                catch { }
                            }
                            lastMonitoredDpi = dpi;
                            lastMonitoredStage = stage;
                        }
                    }
                }
                catch { }
                Thread.Sleep(200);
            }
        }

        private void OnDpiChanged(int newDpi, int newStage, int stageCount)
        {
            if (dpiOsdEnabled && osdForm != null)
            {
                osdForm.ShowDpi(newDpi, newStage, stageCount);
            }

            if (lastInfo != null)
            {
                lastInfo.Dpi = newDpi;
                lastInfo.DpiStage = newStage;
                lastInfo.DpiStageCount = stageCount;
            }

            // Highlight corresponding segment button
            if (btnDpiStages != null)
            {
                for (int i = 0; i < btnDpiStages.Length; i++)
                {
                    if (i < dpiStageValues.Length)
                    {
                        btnDpiStages[i].Selected = (dpiStageValues[i] == newDpi);
                    }
                }
            }

            // Update Tray DPI submenu checks
            if (dpiMenu != null)
            {
                foreach (ToolStripItem item in dpiMenu.DropDownItems)
                {
                    var mi = item as ToolStripMenuItem;
                    if (mi != null && mi.Tag != null)
                    {
                        mi.Checked = ((int)mi.Tag == newDpi);
                    }
                }
            }

            // Update status text
            UpdateStatusDisplay();
        }

        public void SetDpiFromUI(int dpi)
        {
            new Thread(() => {
                int targetStage = -1;
                for (int i = 0; i < dpiStageValues.Length; i++)
                {
                    if (dpiStageValues[i] == dpi)
                    {
                        targetStage = i + 1;
                        break;
                    }
                }

                if (targetStage > 0)
                {
                    RazerDeviceHelper.SetRazerDpiStage(targetStage);
                }
                RazerDeviceHelper.SetRazerDpi(dpi);

                int queryDpi, stage, count;
                if (RazerDeviceHelper.FastQueryDpi(out queryDpi, out stage, out count))
                {
                    this.BeginInvoke(new Action(() => {
                        OnDpiChanged(queryDpi, stage, count);
                    }));
                }
                else
                {
                    this.BeginInvoke(new Action(() => {
                        OnDpiChanged(dpi, targetStage > 0 ? targetStage : 1, dpiStageValues.Length);
                    }));
                }
            }) { IsBackground = true }.Start();
        }

        public void SetPollingRateFromUI(int hz)
        {
            new Thread(() => {
                bool ok = RazerDeviceHelper.SetRazerPollingRate(hz);
                if (ok)
                {
                    this.BeginInvoke(new Action(() => {
                        if (lastInfo != null) lastInfo.PollingRate = hz;

                        if (btnRates != null)
                        {
                            for (int i = 0; i < btnRates.Length; i++)
                            {
                                if (i < pollingRateValues.Length)
                                {
                                    btnRates[i].Selected = (pollingRateValues[i] == hz);
                                }
                            }
                        }

                        if (rateMenu != null)
                        {
                            foreach (ToolStripItem item in rateMenu.DropDownItems)
                            {
                                var mi = item as ToolStripMenuItem;
                                if (mi != null && mi.Tag != null)
                                {
                                    mi.Checked = ((int)mi.Tag == hz);
                                }
                            }
                        }

                        UpdateStatusDisplay();
                        if (trayIcon != null)
                        {
                            trayIcon.ShowBalloonTip(1200, "回报率已切换", "鼠标回报率已设置为: " + hz + " Hz", ToolTipIcon.Info);
                        }
                    }));
                }
            }) { IsBackground = true }.Start();
        }

        private void SetDpiOsdEnabled(bool enabled, bool showNotification = false)
        {
            dpiOsdEnabled = enabled;

            isUpdatingUI = true;
            try
            {
                if (chkDpiOsd != null && chkDpiOsd.Checked != enabled) chkDpiOsd.Checked = enabled;
                if (dpiOsdMenuItem != null) dpiOsdMenuItem.Checked = enabled;
            }
            finally
            {
                isUpdatingUI = false;
            }

            SaveConfig();

            if (showNotification && trayIcon != null)
            {
                if (enabled)
                    trayIcon.ShowBalloonTip(1500, "DPI 屏幕提示已开启", "按键切换 DPI 时将在屏幕右下角弹出浮窗提示。", ToolTipIcon.Info);
                else
                    trayIcon.ShowBalloonTip(1500, "DPI 屏幕提示已关闭", "已关闭按键切换 DPI 的屏幕浮窗提示。", ToolTipIcon.None);
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

        private void SetOsdStyle(int style, bool showNotification = false)
        {
            osdStyle = style;
            if (osdStyle < 0 || osdStyle > 2) osdStyle = 0;
            SaveConfig();

            if (osdForm != null)
            {
                osdForm.OsdStyle = osdStyle;
            }

            isUpdatingUI = true;
            try
            {
                if (osdStyleCapsuleItem != null) osdStyleCapsuleItem.Checked = (osdStyle == 0);
                if (osdStyleGaugeItem != null) osdStyleGaugeItem.Checked = (osdStyle == 1);
                if (osdStyleCompactItem != null) osdStyleCompactItem.Checked = (osdStyle == 2);

                if (btnOsdStyleCapsule != null) btnOsdStyleCapsule.Selected = (osdStyle == 0);
                if (btnOsdStyleGauge != null) btnOsdStyleGauge.Selected = (osdStyle == 1);
                if (btnOsdStyleCompact != null) btnOsdStyleCompact.Selected = (osdStyle == 2);
            }
            finally
            {
                isUpdatingUI = false;
            }

            // Trigger instant live preview so the user immediately sees their chosen style!
            if (dpiOsdEnabled && osdForm != null)
            {
                int previewDpi = (lastInfo != null && lastInfo.Dpi > 0) ? lastInfo.Dpi : 3000;
                int previewStage = (lastInfo != null && lastInfo.DpiStage > 0) ? lastInfo.DpiStage : 4;
                int previewCount = (lastInfo != null && lastInfo.DpiStageCount > 0) ? lastInfo.DpiStageCount : 5;
                osdForm.ShowDpi(previewDpi, previewStage, previewCount);
            }

            if (showNotification && trayIcon != null)
            {
                string name = (osdStyle == 0) ? "居中电竞胶囊" : ((osdStyle == 1) ? "右侧阶梯能量计" : "顶置微型指示段");
                trayIcon.ShowBalloonTip(1500, "DPI 浮窗样式已切换", "当前浮窗样式: " + name, ToolTipIcon.Info);
            }
        }

        private void UpdateMenuStatusTexts()
        {
            if (lowBatteryAlertMenuItem != null)
            {
                lowBatteryAlertMenuItem.Text = "低电量气泡通知 (≤20%)";
                lowBatteryAlertMenuItem.Checked = lowBatteryAlertEnabled;
            }
            bool autoStart = IsAutoStartEnabled();
            if (autoStartMenuItem != null)
            {
                autoStartMenuItem.Text = "开机自动启动";
                autoStartMenuItem.Checked = autoStart;
            }
            if (autoStartShowUIMenuItem != null)
            {
                autoStartShowUIMenuItem.Text = "开机弹出主窗口";
                autoStartShowUIMenuItem.Checked = autoStartShowMainWindow;
                autoStartShowUIMenuItem.Enabled = autoStart;
            }
            if (chkAutoStartShowUI != null)
            {
                chkAutoStartShowUI.Checked = autoStartShowMainWindow;
                chkAutoStartShowUI.Enabled = autoStart;
            }
            if (dpiOsdMenuItem != null)
            {
                dpiOsdMenuItem.Text = "DPI 切换屏幕提示 (OSD)";
                dpiOsdMenuItem.Checked = dpiOsdEnabled;
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
                        if (val != null) lowBatteryAlertEnabled = (int)val == 1;

                        var dVal = key.GetValue("DpiOsdAlert");
                        if (dVal != null) dpiOsdEnabled = (int)dVal == 1;

                        var autoShowVal = key.GetValue("AutoStartShowUI");
                        if (autoShowVal != null) autoStartShowMainWindow = (int)autoShowVal == 1;

                        var sVal = key.GetValue("TrayIconStyle");
                        if (sVal != null)
                        {
                            trayStyle = (int)sVal;
                            if (trayStyle != 0 && trayStyle != 1) trayStyle = 0;
                        }

                        var oVal = key.GetValue("OsdStyle");
                        if (oVal != null)
                        {
                            osdStyle = (int)oVal;
                            if (osdStyle < 0 || osdStyle > 2) osdStyle = 0;
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
            if (autoStart)
            {
                SyncAutoStartRegistry();
            }

            isUpdatingUI = true;
            try
            {
                UpdateMenuStatusTexts();
                if (chkLowAlert != null) chkLowAlert.Checked = lowBatteryAlertEnabled;
                if (chkDpiOsd != null) chkDpiOsd.Checked = dpiOsdEnabled;
                if (chkAutoStart != null) chkAutoStart.Checked = autoStart;
                if (chkAutoStartShowUI != null)
                {
                    chkAutoStartShowUI.Checked = autoStartShowMainWindow;
                    chkAutoStartShowUI.Enabled = autoStart;
                }
                if (autoStartShowUIMenuItem != null)
                {
                    autoStartShowUIMenuItem.Checked = autoStartShowMainWindow;
                    autoStartShowUIMenuItem.Enabled = autoStart;
                }

                if (styleCapsuleItem != null) styleCapsuleItem.Checked = (trayStyle == 0);
                if (styleNumItem != null) styleNumItem.Checked = (trayStyle == 1);
                if (btnStyleCapsule != null) btnStyleCapsule.Selected = (trayStyle == 0);
                if (btnStyleNum != null) btnStyleNum.Selected = (trayStyle == 1);

                if (osdForm != null) osdForm.OsdStyle = osdStyle;
                if (osdStyleCapsuleItem != null) osdStyleCapsuleItem.Checked = (osdStyle == 0);
                if (osdStyleGaugeItem != null) osdStyleGaugeItem.Checked = (osdStyle == 1);
                if (osdStyleCompactItem != null) osdStyleCompactItem.Checked = (osdStyle == 2);
                if (btnOsdStyleCapsule != null) btnOsdStyleCapsule.Selected = (osdStyle == 0);
                if (btnOsdStyleGauge != null) btnOsdStyleGauge.Selected = (osdStyle == 1);
                if (btnOsdStyleCompact != null) btnOsdStyleCompact.Selected = (osdStyle == 2);

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
                        key.SetValue("DpiOsdAlert", dpiOsdEnabled ? 1 : 0);
                        key.SetValue("AutoStartShowUI", autoStartShowMainWindow ? 1 : 0);
                        key.SetValue("TrayIconStyle", trayStyle);
                        key.SetValue("OsdStyle", osdStyle);
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

        private void SyncAutoStartRegistry()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true))
                {
                    if (key != null)
                    {
                        var val = key.GetValue(AppName) as string;
                        if (!string.IsNullOrEmpty(val))
                        {
                            string exePath = Application.ExecutablePath;
                            string expected = "\"" + exePath + "\" --autostart";
                            if (!string.Equals(val.Trim(), expected, StringComparison.OrdinalIgnoreCase))
                            {
                                key.SetValue(AppName, expected);
                            }
                        }
                    }
                }
            }
            catch { }
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
                            key.SetValue(AppName, "\"" + exePath + "\" --autostart");
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
                if (chkAutoStartShowUI != null) chkAutoStartShowUI.Enabled = enabled;
                if (autoStartShowUIMenuItem != null) autoStartShowUIMenuItem.Enabled = enabled;
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

        private void SetAutoStartShowUI(bool showUI, bool showNotification = false)
        {
            autoStartShowMainWindow = showUI;
            SaveConfig();

            isUpdatingUI = true;
            try
            {
                if (chkAutoStartShowUI != null && chkAutoStartShowUI.Checked != showUI) chkAutoStartShowUI.Checked = showUI;
                if (autoStartShowUIMenuItem != null) autoStartShowUIMenuItem.Checked = showUI;
            }
            finally
            {
                isUpdatingUI = false;
            }

            if (showNotification && trayIcon != null)
            {
                if (showUI)
                    trayIcon.ShowBalloonTip(1500, "开机行为已更新", "开机启动时将自动显示主窗口面板。", ToolTipIcon.Info);
                else
                    trayIcon.ShowBalloonTip(1500, "开机行为已更新", "开机启动时将保持静默，仅常驻系统托盘。", ToolTipIcon.Info);
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
                var info = RazerDeviceHelper.QueryRazerDeviceInfo();
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

        private void UpdateStatusDisplay()
        {
            if (lastInfo == null || !lastInfo.IsConnected) return;

            string dpiPart = lastInfo.Dpi > 0 ? (" · " + lastInfo.Dpi + " DPI") : "";
            string ratePart = lastInfo.PollingRate > 0 ? (" · " + lastInfo.PollingRate + "Hz") : "";
            string chgPart = lastInfo.IsSleeping ? "休眠待机" : (lastInfo.IsCharging ? "充电中" : "电池供电");
            string menuStatus = string.Format("{0} ({1}% · {2})", lastInfo.DeviceName, lastInfo.BatteryPercent, chgPart);
            statusMenuItem.Text = menuStatus;

            string timeStr = lastInfo.LastUpdated.ToString("HH:mm:ss");
            string chgStr = lastInfo.IsSleeping ? "休眠待机 (移动唤醒)" : (lastInfo.IsCharging ? "正在充电" : "电池供电");
            string tipText = string.Format("雷蛇电量管家\n{0} · {1}%\n状态: {2}{3}{4}\n最后同步: {5}",
                lastInfo.DeviceName, lastInfo.BatteryPercent, chgStr, dpiPart, ratePart, timeStr);

            if (tipText.Length > 63)
            {
                tipText = string.Format("{0}: {1}%\n{2}{3}", lastInfo.DeviceName, lastInfo.BatteryPercent, chgStr, dpiPart);
                if (tipText.Length > 63)
                {
                    tipText = string.Format("电量: {0}% ({1})", lastInfo.BatteryPercent, chgPart);
                }
            }
            trayIcon.Text = tipText;
        }

        private void UpdateUI(MouseBatteryInfo info, bool showTipIfManual)
        {
            if (info == null || !info.IsConnected)
            {
                isMouseSleeping = false;
                if (updateTimer != null) updateTimer.Interval = 3000;

                lblDeviceName.Text = "未检测到雷蛇鼠标";
                lblConnDot.Text = "○ 未连接";
                lblConnDot.ForeColor = Color.FromArgb(140, 145, 155);

                lblBatteryBig.Text = "--%";
                lblBatteryBig.ForeColor = Color.FromArgb(140, 145, 155);

                pillStatus.SetStatus("未连接", Color.FromArgb(140, 145, 155));
                pillStatus.Location = new Point(lblBatteryBig.Right + (int)(16 * dpiScale), (int)(60 * dpiScale));

                barBattery.Value = 0;
                lblUpdateTime.Text = "最后同步: " + DateTime.Now.ToString("HH:mm:ss") + " · 未检测到设备";

                statusMenuItem.Text = "未检测到雷蛇鼠标 (未连接)";
                trayIcon.Text = "未检测到雷蛇鼠标 (未连接)";
                UpdateTrayIcon(-1, false, false);
                lastLowAlertFired = false;
                if (showTipIfManual)
                {
                    trayIcon.ShowBalloonTip(2000, "雷蛇鼠标未连接", "未能找到已连接或唤醒的雷蛇鼠标，请移动鼠标唤醒后重试。", ToolTipIcon.Warning);
                }
                return;
            }

            // Absolute safety guard: connected mouse should NEVER show 0% (indicates sleep / no telemetry)
            if (info.BatteryPercent <= 0)
            {
                if (RazerDeviceHelper.CachedBatteryPercent > 0)
                {
                    info.BatteryPercent = RazerDeviceHelper.CachedBatteryPercent;
                }
                else
                {
                    info.BatteryPercent = 50;
                }
                info.IsSleeping = true;
            }

            if (info.IsSleeping)
            {
                isMouseSleeping = true;
                if (updateTimer != null) updateTimer.Interval = userSelectedInterval;

                Color sleepColor = Color.FromArgb(255, 183, 77); // Warm amber
                lblDeviceName.Text = info.DeviceName;
                lblConnDot.Text = "◐ 休眠待机";
                lblConnDot.ForeColor = sleepColor;

                lblBatteryBig.Text = info.BatteryPercent + "%";
                lblBatteryBig.ForeColor = Color.FromArgb(220, 225, 235);

                pillStatus.SetStatus("休眠待机", sleepColor);
                pillStatus.Location = new Point(lblBatteryBig.Right + (int)(16 * dpiScale), (int)(60 * dpiScale));

                barBattery.Value = info.BatteryPercent;
                barBattery.ProgressColor = sleepColor;

                string timeStr = info.LastUpdated.ToString("HH:mm:ss");
                lblUpdateTime.Text = "最后同步: " + timeStr + " · 移动鼠标即刻唤醒";

                // Update Performance Card with cached stages
                if (info.DpiStages != null && info.DpiStages.Length > 0)
                {
                    dpiStageValues = info.DpiStages;
                    for (int i = 0; i < btnDpiStages.Length; i++)
                    {
                        if (i < dpiStageValues.Length)
                        {
                            btnDpiStages[i].Text = dpiStageValues[i].ToString();
                            btnDpiStages[i].Selected = (dpiStageValues[i] == info.Dpi);
                            btnDpiStages[i].Visible = true;
                        }
                        else
                        {
                            btnDpiStages[i].Visible = false;
                        }
                    }
                }

                if (info.PollingRate > 0)
                {
                    for (int i = 0; i < btnRates.Length; i++)
                    {
                        if (i < pollingRateValues.Length)
                        {
                            btnRates[i].Selected = (pollingRateValues[i] == info.PollingRate);
                        }
                    }
                }

                UpdateStatusDisplay();
                UpdateTrayIcon(info.BatteryPercent, false, true, true);

                if (showTipIfManual)
                {
                    trayIcon.ShowBalloonTip(1500, info.DeviceName, string.Format("电量: {0}% (休眠待机)\n移动鼠标将即刻恢复工作", info.BatteryPercent), ToolTipIcon.Info);
                }
                return;
            }

            // Normal awake state
            isMouseSleeping = false;
            if (updateTimer != null) updateTimer.Interval = userSelectedInterval;

            Color accentColor;
            if (info.BatteryPercent > 40 || info.IsCharging)
                accentColor = Color.FromArgb(0, 230, 118);
            else if (info.BatteryPercent > 20)
                accentColor = Color.FromArgb(255, 214, 0);
            else
                accentColor = Color.FromArgb(255, 45, 85);

            string chgStr = info.IsCharging ? "正在充电" : "电池供电";
            lblDeviceName.Text = info.DeviceName;
            lblConnDot.Text = "● 已连接";
            lblConnDot.ForeColor = Color.FromArgb(0, 230, 118);

            lblBatteryBig.Text = info.BatteryPercent + "%";
            lblBatteryBig.ForeColor = accentColor;

            pillStatus.SetStatus(chgStr, accentColor);
            pillStatus.Location = new Point(lblBatteryBig.Right + (int)(16 * dpiScale), (int)(60 * dpiScale));

            barBattery.Value = info.BatteryPercent;
            barBattery.ProgressColor = accentColor;

            string normalTimeStr = info.LastUpdated.ToString("HH:mm:ss");
            lblUpdateTime.Text = "最后同步: " + normalTimeStr + " · 自动侦测硬件插拔";

            // Update Performance Card dynamic stages
            if (info.DpiStages != null && info.DpiStages.Length > 0)
            {
                dpiStageValues = info.DpiStages;
                for (int i = 0; i < btnDpiStages.Length; i++)
                {
                    if (i < dpiStageValues.Length)
                    {
                        btnDpiStages[i].Text = dpiStageValues[i].ToString();
                        btnDpiStages[i].Selected = (dpiStageValues[i] == info.Dpi);
                        btnDpiStages[i].Visible = true;
                    }
                    else
                    {
                        btnDpiStages[i].Visible = false;
                    }
                }

                // Update tray menu items
                if (dpiMenu != null)
                {
                    dpiMenu.DropDownItems.Clear();
                    for (int i = 0; i < dpiStageValues.Length; i++)
                    {
                        int val = dpiStageValues[i];
                        var sub = new ToolStripMenuItem(val + " DPI", null, (s, e) => SetDpiFromUI(val));
                        sub.Tag = val;
                        sub.Checked = (val == info.Dpi);
                        dpiMenu.DropDownItems.Add(sub);
                    }
                }
            }

            if (info.PollingRate > 0)
            {
                for (int i = 0; i < btnRates.Length; i++)
                {
                    if (i < pollingRateValues.Length)
                    {
                        btnRates[i].Selected = (pollingRateValues[i] == info.PollingRate);
                    }
                }

                if (rateMenu != null)
                {
                    foreach (ToolStripItem item in rateMenu.DropDownItems)
                    {
                        var mi = item as ToolStripMenuItem;
                        if (mi != null && mi.Tag != null)
                        {
                            mi.Checked = ((int)mi.Tag == info.PollingRate);
                        }
                    }
                }
            }

            UpdateStatusDisplay();
            UpdateTrayIcon(info.BatteryPercent, info.IsCharging, true, false);

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
                string perfTip = (info.Dpi > 0 && info.PollingRate > 0) ? string.Format("\n性能: {0} DPI · {1} Hz", info.Dpi, info.PollingRate) : "";
                trayIcon.ShowBalloonTip(1500, info.DeviceName, string.Format("电量: {0}% ({1}){2}\n更新时间: {3}", info.BatteryPercent, chgStr, perfTip, normalTimeStr), ToolTipIcon.Info);
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

        private void UpdateTrayIcon(int percent, bool isCharging, bool isConnected, bool isSleeping = false)
        {
            int iconSize = GetTrayIconSize();
            using (Bitmap bmp = DrawTrayBitmap(percent, isCharging, isConnected, trayStyle, iconSize, isSleeping))
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

        private static Bitmap DrawTrayBitmap(int percent, bool isCharging, bool isConnected, int style, int iconSize, bool isSleeping = false)
        {
            if (iconSize >= 24)
            {
                return DrawTrayBitmap24(percent, isCharging, isConnected, style, isSleeping);
            }
            else
            {
                return DrawTrayBitmap16(percent, isCharging, isConnected, style, isSleeping);
            }
        }

        private static Bitmap DrawTrayBitmap24(int percent, bool isCharging, bool isConnected, int style, bool isSleeping = false)
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

                Color accentColor = isSleeping ? Color.FromArgb(255, 183, 77) :
                                    ((percent > 40 || isCharging) ? Color.FromArgb(0, 230, 118) :
                                    ((percent > 20) ? Color.FromArgb(255, 214, 0) : Color.FromArgb(255, 50, 65)));

                if (style == 1) // 醒目数字能量表 (Centered digits)
                {
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(20, 23, 30)))
                        g.FillRectangle(bg, 0, 0, 24, 24);
                    using (Pen border = new Pen(Color.FromArgb(48, 56, 74), 1f))
                        g.DrawRectangle(border, 0, 0, 23, 23);

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

                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(18, 22, 30)))
                        g.FillRectangle(bg, 2, 4, 18, 16);

                    int fillW = Math.Max(1, (int)(18 * (percent / 100.0)));
                    using (SolidBrush fb = new SolidBrush(accentColor))
                        g.FillRectangle(fb, 2, 4, fillW, 16);

                    if (isCharging)
                        DrawBolt24(bmp, 10, 11, Color.White);
                    else
                        DrawDigits24(bmp, percent, 10, 8, Color.White, true);

                    using (Pen p = new Pen(whiteCol, 1f))
                    {
                        g.DrawLine(p, 2, 3, 19, 3);
                        g.DrawLine(p, 2, 20, 19, 20);
                        g.DrawLine(p, 1, 4, 1, 19);
                        g.DrawLine(p, 20, 4, 20, 19);
                    }
                    bmp.SetPixel(1, 3, whiteCol);
                    bmp.SetPixel(1, 20, whiteCol);
                    bmp.SetPixel(20, 3, whiteCol);
                    bmp.SetPixel(20, 20, whiteCol);

                    using (SolidBrush cap = new SolidBrush(whiteCol))
                        g.FillRectangle(cap, 21, 8, 2, 8);
                }
            }
            return bmp;
        }

        private static Bitmap DrawTrayBitmap16(int percent, bool isCharging, bool isConnected, int style, bool isSleeping = false)
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

                Color accentColor = isSleeping ? Color.FromArgb(255, 183, 77) :
                                    ((percent > 40 || isCharging) ? Color.FromArgb(0, 230, 118) :
                                    ((percent > 20) ? Color.FromArgb(255, 214, 0) : Color.FromArgb(255, 50, 65)));

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

                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(18, 22, 30)))
                        g.FillRectangle(bg, 1, 3, 12, 10);

                    int fillW = Math.Max(1, (int)(12 * (percent / 100.0)));
                    using (SolidBrush fb = new SolidBrush(accentColor))
                        g.FillRectangle(fb, 1, 3, fillW, 10);

                    if (isCharging)
                        DrawBolt16(bmp, 6, 5, Color.White);
                    else
                        DrawDigits16(bmp, percent, 7, 5, Color.White, true);

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
            StopDpiMonitor();
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            if (osdForm != null && !osdForm.IsDisposed)
            {
                osdForm.Dispose();
            }
            Application.Exit();
        }
    }

    public static class RazerDeviceHelper
    {
        private static readonly object hidLock = new object();

        // Hardware Cache for Standby / Sleep Retention
        public static int CachedBatteryPercent = 0;
        public static string CachedDeviceName = "";
        public static int CachedDpi = 0;
        public static int CachedDpiStage = 0;
        public static int CachedDpiStageCount = 0;
        public static int[] CachedDpiStages = null;
        public static int CachedPollingRate = 0;
        public static DateTime CachedLastUpdated = DateTime.MinValue;

        private const string CacheRegistryKey = @"Software\RazerBatteryTray\HardwareCache";

        public static void LoadHardwareCache()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(CacheRegistryKey))
                {
                    if (key != null)
                    {
                        var batt = key.GetValue("BatteryPercent");
                        if (batt != null)
                        {
                            int b = (int)batt;
                            if (b > 0) CachedBatteryPercent = b;
                        }

                        var dev = key.GetValue("DeviceName");
                        if (dev != null) CachedDeviceName = (string)dev;

                        var dpi = key.GetValue("Dpi");
                        if (dpi != null) CachedDpi = (int)dpi;

                        var st = key.GetValue("DpiStage");
                        if (st != null) CachedDpiStage = (int)st;

                        var stCount = key.GetValue("DpiStageCount");
                        if (stCount != null) CachedDpiStageCount = (int)stCount;

                        var stStr = key.GetValue("DpiStages") as string;
                        if (!string.IsNullOrEmpty(stStr))
                        {
                            string[] parts = stStr.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            CachedDpiStages = new int[parts.Length];
                            for (int i = 0; i < parts.Length; i++)
                            {
                                int p;
                                if (int.TryParse(parts[i], out p)) CachedDpiStages[i] = p;
                            }
                        }

                        var poll = key.GetValue("PollingRate");
                        if (poll != null) CachedPollingRate = (int)poll;

                        var updatedStr = key.GetValue("LastUpdated") as string;
                        if (!string.IsNullOrEmpty(updatedStr))
                        {
                            DateTime dt;
                            if (DateTime.TryParse(updatedStr, out dt)) CachedLastUpdated = dt;
                        }
                    }
                }
            }
            catch { }
        }

        public static void SaveHardwareCache()
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(CacheRegistryKey))
                {
                    if (key != null)
                    {
                        if (CachedBatteryPercent > 0) key.SetValue("BatteryPercent", CachedBatteryPercent);
                        if (!string.IsNullOrEmpty(CachedDeviceName)) key.SetValue("DeviceName", CachedDeviceName);
                        if (CachedDpi > 0) key.SetValue("Dpi", CachedDpi);
                        if (CachedDpiStage > 0) key.SetValue("DpiStage", CachedDpiStage);
                        if (CachedDpiStageCount > 0) key.SetValue("DpiStageCount", CachedDpiStageCount);
                        if (CachedDpiStages != null && CachedDpiStages.Length > 0)
                        {
                            string stStr = string.Join(",", Array.ConvertAll(CachedDpiStages, s => s.ToString()));
                            key.SetValue("DpiStages", stStr);
                        }
                        if (CachedPollingRate > 0) key.SetValue("PollingRate", CachedPollingRate);
                        if (CachedLastUpdated != DateTime.MinValue) key.SetValue("LastUpdated", CachedLastUpdated.ToString("o"));
                    }
                }
            }
            catch { }
        }

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

        static byte[] CreateRazerReport(byte transactionId, byte commandClass, byte commandId, byte dataSize, int totalLength, bool prependedZero, byte[] args = null)
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

            if (args != null)
            {
                for (int i = 0; i < args.Length && i < 80; i++)
                {
                    report[offset + 8 + i] = args[i];
                }
            }

            report[offset + 88] = CalculateCrc(report, offset);
            report[offset + 89] = 0x00;

            return report;
        }

        public static MouseBatteryInfo QueryRazerDeviceInfo()
        {
            lock (hidLock)
            {
                bool foundRazerDongle = false;
                string foundDeviceName = null;

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
                                                foundRazerDongle = true;
                                                StringBuilder prod = new StringBuilder(256);
                                                string prodName = "Razer Mouse";
                                                if (HidD_GetProductString(handle, prod, prod.Capacity) && prod.Length > 0)
                                                {
                                                    prodName = prod.ToString();
                                                    foundDeviceName = prodName;
                                                }

                                                byte[] transIds = new byte[] { 0x1F, 0x3F, 0xFF };
                                                bool prepended = (caps.FeatureReportByteLength == 91);
                                                int offset = prepended ? 1 : 0;

                                                foreach (byte tid in transIds)
                                                {
                                                    // 1. Query Battery
                                                    byte[] req = CreateRazerReport(tid, 0x07, 0x80, 0x02, caps.FeatureReportByteLength, prepended);
                                                    if (HidD_SetFeature(handle, req, req.Length))
                                                    {
                                                        Thread.Sleep(15);
                                                        byte[] resp = new byte[caps.FeatureReportByteLength];
                                                        if (prepended) resp[0] = 0x00;

                                                        if (HidD_GetFeature(handle, resp, resp.Length))
                                                        {
                                                            byte status = resp[offset + 0];
                                                            byte cmdClass = resp[offset + 6];
                                                            byte cmdId = resp[offset + 7];
                                                            byte rawBatt = resp[offset + 9];

                                                            if (status == 0x02 && cmdClass == 0x07 && cmdId == 0x80 && rawBatt > 0)
                                                            {
                                                                int pct = (int)Math.Round((rawBatt / 255.0) * 100);
                                                                pct = Math.Max(1, Math.Min(100, pct));

                                                                // 2. Query Charging
                                                                bool isCharging = false;
                                                                byte[] chgReq = CreateRazerReport(tid, 0x07, 0x84, 0x02, caps.FeatureReportByteLength, prepended);
                                                                if (HidD_SetFeature(handle, chgReq, chgReq.Length))
                                                                {
                                                                    Thread.Sleep(15);
                                                                    byte[] chgResp = new byte[caps.FeatureReportByteLength];
                                                                    if (prepended) chgResp[0] = 0x00;
                                                                    if (HidD_GetFeature(handle, chgResp, chgResp.Length) && chgResp[offset + 0] == 0x02)
                                                                    {
                                                                        isCharging = (chgResp[offset + 9] == 1);
                                                                    }
                                                                }

                                                                // 3. Query DPI Stages & Active DPI
                                                                int liveDpi = 0;
                                                                int activeStage = 0;
                                                                int stageCount = 0;
                                                                int[] stageList = null;

                                                                byte[] stagesReq = CreateRazerReport(tid, 0x04, 0x86, 0x26, caps.FeatureReportByteLength, prepended, new byte[] { 0x01 });
                                                                if (HidD_SetFeature(handle, stagesReq, stagesReq.Length))
                                                                {
                                                                    Thread.Sleep(15);
                                                                    byte[] stResp = new byte[caps.FeatureReportByteLength];
                                                                    if (prepended) stResp[0] = 0x00;
                                                                    if (HidD_GetFeature(handle, stResp, stResp.Length))
                                                                    {
                                                                        if (stResp[offset + 0] == 0x02)
                                                                        {
                                                                            activeStage = stResp[offset + 9];
                                                                            stageCount = stResp[offset + 10];
                                                                            if (stageCount > 0 && stageCount <= 5)
                                                                            {
                                                                                stageList = new int[stageCount];
                                                                                for (int s = 0; s < stageCount; s++)
                                                                                {
                                                                                    int stOffset = offset + 11 + (s * 7);
                                                                                    int stNum = stResp[stOffset];
                                                                                    int stX = (stResp[stOffset + 1] << 8) | stResp[stOffset + 2];
                                                                                    stageList[s] = stX;
                                                                                    if (stNum == activeStage)
                                                                                    {
                                                                                        liveDpi = stX;
                                                                                    }
                                                                                }
                                                                            }
                                                                        }
                                                                    }
                                                                }

                                                                if (liveDpi <= 0)
                                                                {
                                                                    byte[] dpiReq = CreateRazerReport(tid, 0x04, 0x85, 0x07, caps.FeatureReportByteLength, prepended, new byte[] { 0x00 });
                                                                    if (HidD_SetFeature(handle, dpiReq, dpiReq.Length))
                                                                    {
                                                                        Thread.Sleep(15);
                                                                        byte[] dpiResp = new byte[caps.FeatureReportByteLength];
                                                                        if (prepended) dpiResp[0] = 0x00;
                                                                        if (HidD_GetFeature(handle, dpiResp, dpiResp.Length) && dpiResp[offset + 0] == 0x02)
                                                                        {
                                                                            liveDpi = (dpiResp[offset + 9] << 8) | dpiResp[offset + 10];
                                                                        }
                                                                    }
                                                                }

                                                                // 4. Query Polling Rate
                                                                int pollingRate = 0;
                                                                byte[] pollReq = CreateRazerReport(tid, 0x00, 0xC0, 0x01, caps.FeatureReportByteLength, prepended);
                                                                if (HidD_SetFeature(handle, pollReq, pollReq.Length))
                                                                {
                                                                    Thread.Sleep(15);
                                                                    byte[] pollResp = new byte[caps.FeatureReportByteLength];
                                                                    if (prepended) pollResp[0] = 0x00;
                                                                    if (HidD_GetFeature(handle, pollResp, pollResp.Length) && pollResp[offset + 0] == 0x02)
                                                                    {
                                                                        byte rawPoll = pollResp[offset + 9];
                                                                        switch (rawPoll)
                                                                        {
                                                                            case 0x01: pollingRate = 8000; break;
                                                                            case 0x02: pollingRate = 4000; break;
                                                                            case 0x04: pollingRate = 2000; break;
                                                                            case 0x08: pollingRate = 1000; break;
                                                                            case 0x10: pollingRate = 500; break;
                                                                            case 0x40: pollingRate = 125; break;
                                                                        }
                                                                    }
                                                                }

                                                                if (pct > 0) CachedBatteryPercent = pct;
                                                                if (!string.IsNullOrEmpty(prodName)) CachedDeviceName = prodName;
                                                                if (liveDpi > 0) CachedDpi = liveDpi;
                                                                if (activeStage > 0) CachedDpiStage = activeStage;
                                                                if (stageCount > 0) CachedDpiStageCount = stageCount;
                                                                if (stageList != null && stageList.Length > 0) CachedDpiStages = stageList;
                                                                if (pollingRate > 0) CachedPollingRate = pollingRate;
                                                                CachedLastUpdated = DateTime.Now;
                                                                SaveHardwareCache();

                                                                return new MouseBatteryInfo
                                                                {
                                                                    IsConnected = true,
                                                                    IsSleeping = false,
                                                                    IsDonglePresent = true,
                                                                    DeviceName = prodName,
                                                                    BatteryPercent = pct,
                                                                    IsCharging = isCharging,
                                                                    LastUpdated = DateTime.Now,
                                                                    Dpi = liveDpi,
                                                                    DpiStage = activeStage,
                                                                    DpiStageCount = stageCount,
                                                                    DpiStages = stageList,
                                                                    PollingRate = pollingRate
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

                if (foundRazerDongle)
                {
                    string dName = !string.IsNullOrEmpty(foundDeviceName) ? foundDeviceName :
                                   (!string.IsNullOrEmpty(CachedDeviceName) ? CachedDeviceName : "雷蛇无线设备 (待机)");
                    return new MouseBatteryInfo
                    {
                        IsConnected = true,
                        IsSleeping = true,
                        IsDonglePresent = true,
                        DeviceName = dName,
                        BatteryPercent = CachedBatteryPercent > 0 ? CachedBatteryPercent : 50,
                        IsCharging = false,
                        LastUpdated = CachedLastUpdated != DateTime.MinValue ? CachedLastUpdated : DateTime.Now,
                        Dpi = CachedDpi > 0 ? CachedDpi : 800,
                        DpiStage = CachedDpiStage > 0 ? CachedDpiStage : 1,
                        DpiStageCount = CachedDpiStageCount > 0 ? CachedDpiStageCount : 5,
                        DpiStages = CachedDpiStages,
                        PollingRate = CachedPollingRate > 0 ? CachedPollingRate : 1000
                    };
                }

                return new MouseBatteryInfo { IsConnected = false, IsSleeping = false, IsDonglePresent = false };
            }
        }

        public static bool FastQueryDpi(out int curDpi, out int activeStage, out int stageCount)
        {
            curDpi = 0;
            activeStage = 0;
            stageCount = 0;

            lock (hidLock)
            {
                Guid hidGuid;
                HidD_GetHidGuid(out hidGuid);

                IntPtr devInfo = SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
                if (devInfo == IntPtr.Zero || devInfo == new IntPtr(-1)) return false;

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
                                                bool prepended = (caps.FeatureReportByteLength == 91);
                                                int offset = prepended ? 1 : 0;

                                                byte[] stagesReq = CreateRazerReport(0x1F, 0x04, 0x86, 0x26, caps.FeatureReportByteLength, prepended, new byte[] { 0x01 });
                                                if (HidD_SetFeature(handle, stagesReq, stagesReq.Length))
                                                {
                                                    Thread.Sleep(10);
                                                    byte[] resp = new byte[caps.FeatureReportByteLength];
                                                    if (prepended) resp[0] = 0x00;
                                                    if (HidD_GetFeature(handle, resp, resp.Length) && resp[offset + 0] == 0x02)
                                                    {
                                                        activeStage = resp[offset + 9];
                                                        stageCount = resp[offset + 10];
                                                        for (int s = 0; s < stageCount && s < 5; s++)
                                                        {
                                                            int stOffset = offset + 11 + (s * 7);
                                                            int stNum = resp[stOffset];
                                                            if (stNum == activeStage)
                                                            {
                                                                curDpi = (resp[stOffset + 1] << 8) | resp[stOffset + 2];
                                                                break;
                                                            }
                                                        }
                                                        Marshal.FreeHGlobal(detailBuffer);
                                                        return true;
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
            }
            return false;
        }

        public static bool SetRazerDpiStage(int targetStage)
        {
            lock (hidLock)
            {
                Guid hidGuid;
                HidD_GetHidGuid(out hidGuid);

                IntPtr devInfo = SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
                if (devInfo == IntPtr.Zero || devInfo == new IntPtr(-1)) return false;

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
                                                bool prepended = (caps.FeatureReportByteLength == 91);
                                                int offset = prepended ? 1 : 0;

                                                // 1. Read current 38-byte stages table
                                                byte[] stagesReq = CreateRazerReport(0x1F, 0x04, 0x86, 0x26, caps.FeatureReportByteLength, prepended, new byte[] { 0x01 });
                                                if (HidD_SetFeature(handle, stagesReq, stagesReq.Length))
                                                {
                                                    Thread.Sleep(15);
                                                    byte[] resp = new byte[caps.FeatureReportByteLength];
                                                    if (prepended) resp[0] = 0x00;
                                                    if (HidD_GetFeature(handle, resp, resp.Length) && resp[offset + 0] == 0x02)
                                                    {
                                                        byte[] fullPayload = new byte[0x26];
                                                        Array.Copy(resp, offset + 8, fullPayload, 0, 0x26);
                                                        fullPayload[1] = (byte)targetStage;

                                                        // 2. Set active stage via Cmd 0x06
                                                        byte[] setReq = CreateRazerReport(0x1F, 0x04, 0x06, 0x26, caps.FeatureReportByteLength, prepended, fullPayload);
                                                        if (HidD_SetFeature(handle, setReq, setReq.Length))
                                                        {
                                                            Thread.Sleep(20);
                                                            byte[] setResp = new byte[caps.FeatureReportByteLength];
                                                            if (prepended) setResp[0] = 0x00;
                                                            if (HidD_GetFeature(handle, setResp, setResp.Length))
                                                            {
                                                                if (setResp[offset + 0] == 0x02)
                                                                {
                                                                    Marshal.FreeHGlobal(detailBuffer);
                                                                    return true;
                                                                }
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
            }
            return false;
        }

        public static bool SetRazerDpi(int dpi)
        {
            lock (hidLock)
            {
                Guid hidGuid;
                HidD_GetHidGuid(out hidGuid);

                IntPtr devInfo = SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
                if (devInfo == IntPtr.Zero || devInfo == new IntPtr(-1)) return false;

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
                                                bool prepended = (caps.FeatureReportByteLength == 91);
                                                int offset = prepended ? 1 : 0;

                                                byte[] args = new byte[7];
                                                args[0] = 0x01; // VARSTORE
                                                args[1] = (byte)((dpi >> 8) & 0xFF);
                                                args[2] = (byte)(dpi & 0xFF);
                                                args[3] = (byte)((dpi >> 8) & 0xFF);
                                                args[4] = (byte)(dpi & 0xFF);
                                                args[5] = 0x00;
                                                args[6] = 0x00;

                                                byte[] req = CreateRazerReport(0x1F, 0x04, 0x05, 0x07, caps.FeatureReportByteLength, prepended, args);
                                                if (HidD_SetFeature(handle, req, req.Length))
                                                {
                                                    Thread.Sleep(15);
                                                    byte[] resp = new byte[caps.FeatureReportByteLength];
                                                    if (prepended) resp[0] = 0x00;
                                                    if (HidD_GetFeature(handle, resp, resp.Length))
                                                    {
                                                        if (resp[offset + 0] == 0x02)
                                                        {
                                                            Marshal.FreeHGlobal(detailBuffer);
                                                            return true;
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
            }
            return false;
        }

        public static bool SetRazerPollingRate(int hz)
        {
            lock (hidLock)
            {
                Guid hidGuid;
                HidD_GetHidGuid(out hidGuid);

                IntPtr devInfo = SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
                if (devInfo == IntPtr.Zero || devInfo == new IntPtr(-1)) return false;

                SP_DEVICE_INTERFACE_DATA ifData = new SP_DEVICE_INTERFACE_DATA();
                ifData.cbSize = Marshal.SizeOf(ifData);

                byte rateByte = 0x02; // default 4000
                switch (hz)
                {
                    case 8000: rateByte = 0x01; break;
                    case 4000: rateByte = 0x02; break;
                    case 2000: rateByte = 0x04; break;
                    case 1000: rateByte = 0x08; break;
                    case 500:  rateByte = 0x10; break;
                    case 125:  rateByte = 0x40; break;
                }

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
                                                bool prepended = (caps.FeatureReportByteLength == 91);
                                                int offset = prepended ? 1 : 0;

                                                byte[] args = new byte[] { 0x01, rateByte };
                                                byte[] req = CreateRazerReport(0x1F, 0x00, 0x40, 0x02, caps.FeatureReportByteLength, prepended, args);
                                                if (HidD_SetFeature(handle, req, req.Length))
                                                {
                                                    Thread.Sleep(15);
                                                    byte[] resp = new byte[caps.FeatureReportByteLength];
                                                    if (prepended) resp[0] = 0x00;
                                                    if (HidD_GetFeature(handle, resp, resp.Length))
                                                    {
                                                        if (resp[offset + 0] == 0x02)
                                                        {
                                                            Marshal.FreeHGlobal(detailBuffer);
                                                            return true;
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
            }
            return false;
        }
    }
}
