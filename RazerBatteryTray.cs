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
        private static readonly Color BgColor = Color.FromArgb(20, 21, 26);
        private static readonly Color HoverColor = Color.FromArgb(36, 40, 52);
        private static readonly Color BorderColor = Color.FromArgb(48, 54, 68);
        private static readonly Color TextWhite = Color.FromArgb(240, 244, 252);
        private static readonly Color TextGray = Color.FromArgb(145, 153, 168);
        private static readonly Color BrandGreen = Color.FromArgb(0, 230, 118);
        private static readonly Color SubmenuArrowColor = Color.FromArgb(170, 178, 195);
        private static readonly Color SubmenuArrowHover = Color.FromArgb(0, 230, 118);

        public ModernDarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (SolidBrush brush = new SolidBrush(BgColor))
            {
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (Pen pen = new Pen(BorderColor, 1))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.IsOnDropDown)
            {
                Rectangle rc = new Rectangle(3, 1, e.Item.Width - 6, e.Item.Height - 2);
                if (e.Item.Selected && e.Item.Enabled)
                {
                    using (GraphicsPath path = RoundedCard.GetRoundedRectangle(rc, 4))
                    {
                        using (SolidBrush brush = new SolidBrush(HoverColor))
                        {
                            e.Graphics.FillPath(brush, path);
                        }
                    }
                }
            }
            else
            {
                base.OnRenderMenuItemBackground(e);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            Color textColor;

            if (e.Item.Tag != null && e.Item.Tag.ToString() == "Header")
            {
                textColor = BrandGreen;
            }
            else if (!e.Item.Enabled)
            {
                textColor = TextGray;
            }
            else if (e.Item.Selected)
            {
                textColor = Color.White;
            }
            else
            {
                textColor = TextWhite;
            }

            Rectangle textRect = e.TextRectangle;
            textRect.X = 36;
            textRect.Width = e.Item.Width - 36 - 28;

            TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, textRect, textColor, flags);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var item = e.Item as ToolStripMenuItem;
            if (item != null && item.Checked)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                float checkCenterX = 18f;
                float checkCenterY = e.Item.Height / 2.0f;

                using (Pen pen = new Pen(BrandGreen, 2.0f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;

                    PointF[] points = new PointF[]
                    {
                        new PointF(checkCenterX - 4.5f, checkCenterY - 0.5f),
                        new PointF(checkCenterX - 1.5f, checkCenterY + 3.0f),
                        new PointF(checkCenterX + 4.5f, checkCenterY - 3.5f)
                    };
                    g.DrawLines(pen, points);
                }
            }
        }

        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            if (e.Item.Tag != null && e.Item.Tag.ToString() == "Header")
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                float dotCenterX = 18f;
                float dotCenterY = e.Item.Height / 2.0f;
                float dotRadius = 3.5f;

                using (SolidBrush dotBrush = new SolidBrush(BrandGreen))
                {
                    g.FillEllipse(dotBrush, dotCenterX - dotRadius, dotCenterY - dotRadius, dotRadius * 2, dotRadius * 2);
                }
                return;
            }

            base.OnRenderItemImage(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (Pen pen = new Pen(BorderColor, 1))
            {
                int y = e.Item.Height / 2;
                e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
            }
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color arrowColor = (e.Item != null && e.Item.Selected) ? SubmenuArrowHover : SubmenuArrowColor;

            Rectangle rect = e.ArrowRectangle;
            float cx = rect.Left + (rect.Width / 2f);
            float cy = rect.Top + (rect.Height / 2f);

            PointF[] arrowPoints = new PointF[]
            {
                new PointF(cx - 2f, cy - 4.5f),
                new PointF(cx + 2.5f, cy),
                new PointF(cx - 2f, cy + 4.5f)
            };

            using (Pen pen = new Pen(arrowColor, 1.8f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                e.Graphics.DrawLines(pen, arrowPoints);
            }
        }

        private class DarkColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return BgColor; } }
            public override Color MenuBorder { get { return BorderColor; } }
            public override Color MenuItemBorder { get { return Color.Transparent; } }
            public override Color MenuItemSelected { get { return HoverColor; } }
            public override Color SeparatorDark { get { return BorderColor; } }
            public override Color SeparatorLight { get { return Color.Transparent; } }
            public override Color ImageMarginGradientBegin { get { return BgColor; } }
            public override Color ImageMarginGradientMiddle { get { return BgColor; } }
            public override Color ImageMarginGradientEnd { get { return BgColor; } }
        }
    }

    #endregion

    #region Screen OSD Floating Notification Form

    public class DpiOsdForm : Form
    {
        private int currentDpi = 3000;
        private int currentStage = 4;
        private int totalStages = 5;
        private System.Windows.Forms.Timer displayTimer;
        private System.Windows.Forms.Timer fadeTimer;
        private float dpiScale = 1.0f;

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
                return cp;
            }
        }

        public DpiOsdForm(float scale)
        {
            this.dpiScale = scale;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.FromArgb(18, 20, 26);
            this.DoubleBuffered = true;
            this.Size = new Size((int)(240 * dpiScale), (int)(80 * dpiScale));

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
                if (this.Opacity > 0.05)
                {
                    this.Opacity -= 0.1;
                }
                else
                {
                    fadeTimer.Stop();
                    this.Hide();
                }
            };
        }

        public void ShowDpi(int dpi, int stage, int count)
        {
            this.currentDpi = dpi;
            this.currentStage = stage;
            this.totalStages = count;

            displayTimer.Stop();
            fadeTimer.Stop();
            this.Opacity = 0.96;

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int margin = (int)(24 * dpiScale);
            this.Location = new Point(wa.Right - this.Width - margin, wa.Bottom - this.Height - margin);

            if (!this.Visible)
            {
                this.Show();
            }
            this.Invalidate();
            displayTimer.Start();
        }

        private static void DrawCrosshair(Graphics g, float cx, float cy, float radius, Color color)
        {
            using (Pen pen = new Pen(color, 1.4f))
            {
                g.DrawEllipse(pen, cx - radius, cy - radius, radius * 2, radius * 2);
                g.DrawLine(pen, cx - radius - 3, cy, cx - radius + 2, cy);
                g.DrawLine(pen, cx + radius - 2, cy, cx + radius + 3, cy);
                g.DrawLine(pen, cx, cy - radius - 3, cx, cy - radius + 2);
                g.DrawLine(pen, cx, cy + radius - 2, cx, cy + radius + 3);
            }
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillEllipse(brush, cx - 1.5f, cy - 1.5f, 3f, 3f);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Rounded Card background
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedCard.GetRoundedRectangle(rect, (int)(10 * dpiScale)))
            {
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(20, 22, 30)))
                {
                    g.FillPath(bgBrush, path);
                }
                using (Pen borderPen = new Pen(Color.FromArgb(0, 230, 118), 1.5f))
                {
                    g.DrawPath(borderPen, path);
                }
            }

            // Left vertical accent bar
            using (GraphicsPath barPath = RoundedCard.GetRoundedRectangle(new Rectangle((int)(6 * dpiScale), (int)(12 * dpiScale), (int)(4 * dpiScale), Height - (int)(24 * dpiScale)), (int)(2 * dpiScale)))
            using (SolidBrush barBrush = new SolidBrush(Color.FromArgb(0, 230, 118)))
            {
                g.FillPath(barBrush, barPath);
            }

            // Vector Crosshair
            DrawCrosshair(g, 26 * dpiScale, 21 * dpiScale, 5.5f * dpiScale, Color.FromArgb(0, 230, 118));

            // Title
            using (Font titleFont = new Font("Microsoft YaHei UI", 8.8F * dpiScale, FontStyle.Regular))
            using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(160, 170, 188)))
            {
                g.DrawString("鼠标 DPI 已切换", titleFont, titleBrush, 36 * dpiScale, 13 * dpiScale);
            }

            // Big DPI Number
            using (Font dpiFont = new Font("Microsoft YaHei UI", 18F * dpiScale, FontStyle.Bold))
            using (SolidBrush dpiBrush = new SolidBrush(Color.FromArgb(0, 230, 118)))
            {
                g.DrawString(currentDpi + " DPI", dpiFont, dpiBrush, 20 * dpiScale, 34 * dpiScale);
            }

            // Stage pill on right
            string stageText = string.Format("第 {0} / {1} 档", currentStage, totalStages);
            using (Font stageFont = new Font("Microsoft YaHei UI", 8.8F * dpiScale, FontStyle.Bold))
            {
                SizeF stSize = g.MeasureString(stageText, stageFont);
                int pillW = (int)stSize.Width + (int)(14 * dpiScale);
                int pillH = (int)(24 * dpiScale);
                int pillX = Width - pillW - (int)(14 * dpiScale);
                int pillY = (int)(38 * dpiScale);

                Rectangle pillRect = new Rectangle(pillX, pillY, pillW, pillH);
                using (GraphicsPath pillPath = RoundedCard.GetRoundedRectangle(pillRect, (int)(6 * dpiScale)))
                {
                    using (SolidBrush pillBg = new SolidBrush(Color.FromArgb(30, 44, 38)))
                    {
                        g.FillPath(pillBg, pillPath);
                    }
                    using (Pen pillBorder = new Pen(Color.FromArgb(0, 180, 90), 1f))
                    {
                        g.DrawPath(pillBorder, pillPath);
                    }
                }

                using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(0, 230, 118)))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(stageText, stageFont, textBrush, pillRect, sf);
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
        private ToolStripMenuItem dpiOsdMenuItem;
        private ToolStripMenuItem styleCapsuleItem;
        private ToolStripMenuItem styleNumItem;
        private ToolStripMenuItem int30sMenuItem;
        private ToolStripMenuItem int1mMenuItem;
        private ToolStripMenuItem int5mMenuItem;
        private ToolStripMenuItem dpiMenu;
        private ToolStripMenuItem rateMenu;
        private System.Windows.Forms.Timer updateTimer;

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
        private StatusPill pillDpi;
        private StatusPill pillRate;
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
        private SubtleDivider divSettings;
        private ModernCheckBox chkAutoStart;
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

            InitializeFormUI();
            InitializeTray();
            LoadConfig();

            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Interval = userSelectedInterval;
            updateTimer.Tick += (s, e) => RefreshBatteryStatus(false);
            updateTimer.Start();

            RefreshBatteryStatus(false);
            StartDpiMonitor();
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

            // Status Pill 1: Charging / Battery State
            pillStatus = new StatusPill();
            pillStatus.Location = new Point((int)(145 * dpiScale), (int)(56 * dpiScale));
            pillStatus.Size = new Size((int)(84 * dpiScale), (int)(28 * dpiScale));
            pillStatus.Font = new Font("Microsoft YaHei UI", 8.8F, FontStyle.Bold, GraphicsUnit.Point);
            pillStatus.SetStatus("电池供电", Color.FromArgb(0, 230, 118));
            cardBattery.Controls.Add(pillStatus);

            // Status Pill 2: DPI Pill
            pillDpi = new StatusPill();
            pillDpi.Location = new Point((int)(236 * dpiScale), (int)(56 * dpiScale));
            pillDpi.Size = new Size((int)(105 * dpiScale), (int)(28 * dpiScale));
            pillDpi.Font = new Font("Microsoft YaHei UI", 8.8F, FontStyle.Bold, GraphicsUnit.Point);
            pillDpi.SetStatus("3000 DPI", Color.FromArgb(0, 200, 255));
            cardBattery.Controls.Add(pillDpi);

            // Status Pill 3: Polling Rate Pill
            pillRate = new StatusPill();
            pillRate.Location = new Point((int)(348 * dpiScale), (int)(56 * dpiScale));
            pillRate.Size = new Size((int)(78 * dpiScale), (int)(28 * dpiScale));
            pillRate.Font = new Font("Microsoft YaHei UI", 8.8F, FontStyle.Bold, GraphicsUnit.Point);
            pillRate.SetStatus("4000 Hz", Color.FromArgb(255, 214, 0));
            cardBattery.Controls.Add(pillRate);

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
            int card3H = (int)(208 * dpiScale);

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

            // Row 3: Modern subtle divider
            divSettings = new SubtleDivider();
            divSettings.Location = new Point(cPad, (int)(114 * dpiScale));
            divSettings.Size = new Size(secW, (int)(8 * dpiScale));
            cardSettings.Controls.Add(divSettings);

            // Row 4: Checkboxes
            int chkY1 = (int)(126 * dpiScale);
            int chkY2 = (int)(154 * dpiScale);
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

            chkLowAlert = new ModernCheckBox();
            chkLowAlert.Text = "低电量提醒 (≤20%)";
            chkLowAlert.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            chkLowAlert.Location = new Point(cPad + chkW + chkGap, chkY1);
            chkLowAlert.Size = new Size(chkW, chkH);
            chkLowAlert.CheckedChanged += (s, e) => {
                if (isUpdatingUI) return;
                SetLowBatteryAlert(chkLowAlert.Checked);
            };
            cardSettings.Controls.Add(chkLowAlert);

            chkDpiOsd = new ModernCheckBox();
            chkDpiOsd.Text = "DPI 按键切换屏幕提示 (OSD 浮窗)";
            chkDpiOsd.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            chkDpiOsd.Location = new Point(cPad, chkY2);
            chkDpiOsd.Size = new Size(secW, chkH);
            chkDpiOsd.CheckedChanged += (s, e) => {
                if (isUpdatingUI) return;
                SetDpiOsdEnabled(chkDpiOsd.Checked);
            };
            cardSettings.Controls.Add(chkDpiOsd);

            // Row 5: Hint inside Card 3
            lblSettingsTip = new Label();
            lblSettingsTip.Text = "注：切换线缆/接收器或按键调 DPI 时将自动即时同步，无需等待计时周期";
            lblSettingsTip.Font = new Font("Microsoft YaHei UI", 8.2F, FontStyle.Regular, GraphicsUnit.Point);
            lblSettingsTip.ForeColor = Color.FromArgb(120, 128, 142);
            lblSettingsTip.Location = new Point(cPad, (int)(182 * dpiScale));
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

            // Update DPI Pill
            if (pillDpi != null)
            {
                pillDpi.SetStatus(newDpi + " DPI", Color.FromArgb(0, 200, 255));
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
                bool ok = RazerDeviceHelper.SetRazerDpi(dpi);
                if (ok)
                {
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
                            OnDpiChanged(dpi, 1, 5);
                        }));
                    }
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
                        if (pillRate != null) pillRate.SetStatus(hz + " Hz", Color.FromArgb(255, 214, 0));

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
                if (chkDpiOsd != null) chkDpiOsd.Checked = dpiOsdEnabled;
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
                        key.SetValue("DpiOsdAlert", dpiOsdEnabled ? 1 : 0);
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
            string chgPart = lastInfo.IsCharging ? "充电中" : "正常供电";

            string menuStatus = string.Format("{0} ({1}% · {2}{3}{4})", lastInfo.DeviceName, lastInfo.BatteryPercent, chgPart, dpiPart, ratePart);
            statusMenuItem.Text = menuStatus;

            string timeStr = lastInfo.LastUpdated.ToString("HH:mm:ss");
            string chgStr = lastInfo.IsCharging ? "正在充电" : "电池供电";
            string tipText = string.Format("雷蛇电量管家\n{0} · {1}%\n状态: {2}{3}{4}\n最后同步: {5}",
                lastInfo.DeviceName, lastInfo.BatteryPercent, chgStr, dpiPart, ratePart, timeStr);

            if (tipText.Length > 63)
            {
                tipText = string.Format("{0}: {1}%\n{2}{3}{4}", lastInfo.DeviceName, lastInfo.BatteryPercent, chgStr, dpiPart, ratePart);
                if (tipText.Length > 63)
                {
                    tipText = string.Format("电量: {0}% ({1})", lastInfo.BatteryPercent, chgStr);
                }
            }
            trayIcon.Text = tipText;
        }

        private void UpdateUI(MouseBatteryInfo info, bool showTipIfManual)
        {
            if (info == null || !info.IsConnected)
            {
                if (updateTimer != null) updateTimer.Interval = 3000;

                lblDeviceName.Text = "未检测到雷蛇鼠标";
                lblConnDot.Text = "○ 未连接";
                lblConnDot.ForeColor = Color.FromArgb(140, 145, 155);

                lblBatteryBig.Text = "--%";
                lblBatteryBig.ForeColor = Color.FromArgb(140, 145, 155);

                pillStatus.SetStatus("未连接", Color.FromArgb(140, 145, 155));
                pillDpi.SetStatus("-- DPI", Color.FromArgb(140, 145, 155));
                pillRate.SetStatus("-- Hz", Color.FromArgb(140, 145, 155));

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

            if (info.Dpi > 0)
            {
                pillDpi.SetStatus(info.Dpi + " DPI", Color.FromArgb(0, 200, 255));
            }
            if (info.PollingRate > 0)
            {
                pillRate.SetStatus(info.PollingRate + " Hz", Color.FromArgb(255, 214, 0));
            }

            barBattery.Value = info.BatteryPercent;
            barBattery.ProgressColor = accentColor;

            string timeStr = info.LastUpdated.ToString("HH:mm:ss");
            lblUpdateTime.Text = "最后同步: " + timeStr + " · 自动侦测硬件插拔";

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
                string perfTip = (info.Dpi > 0 && info.PollingRate > 0) ? string.Format("\n性能: {0} DPI · {1} Hz", info.Dpi, info.PollingRate) : "";
                trayIcon.ShowBalloonTip(1500, info.DeviceName, string.Format("电量: {0}% ({1}){2}\n更新时间: {3}", info.BatteryPercent, chgStr, perfTip, timeStr), ToolTipIcon.Info);
            }
        }

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);
        private const int SM_CXSMICON = 49;

        private int GetTrayIconSize()
        {
            try
            {
                int sz = GetSystemMetrics(SM_CXSMICON);
                if (sz >= 16) return sz;
            }
            catch { }
            return 32;
        }

        private void UpdateTrayIcon(int batteryPercent, bool isCharging, bool isConnected)
        {
            try
            {
                int iconSize = GetTrayIconSize();
                if (iconSize < 32) iconSize = 32;

                using (Bitmap bmp = new Bitmap(iconSize, iconSize))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    g.Clear(Color.Transparent);

                    if (!isConnected)
                    {
                        DrawDisconnectedTrayIcon(g, iconSize);
                    }
                    else if (trayStyle == 1)
                    {
                        DrawNumberBadgeTrayIcon(g, iconSize, batteryPercent, isCharging);
                    }
                    else
                    {
                        DrawModernCapsuleTrayIcon(g, iconSize, batteryPercent, isCharging);
                    }

                    IntPtr hIcon = bmp.GetHicon();
                    try
                    {
                        Icon newIcon = Icon.FromHandle(hIcon);
                        trayIcon.Icon = newIcon;
                    }
                    finally
                    {
                        DestroyIcon(hIcon);
                    }
                }
            }
            catch { }
        }

        private void DrawDisconnectedTrayIcon(Graphics g, int sz)
        {
            int bodyW = sz - 8;
            int bodyH = (int)(sz * 0.52f);
            int bodyX = 2;
            int bodyY = (sz - bodyH) / 2;

            using (GraphicsPath p = RoundedCard.GetRoundedRectangle(new Rectangle(bodyX, bodyY, bodyW, bodyH), 4))
            {
                using (Pen pen = new Pen(Color.FromArgb(130, 138, 150), 1.8f))
                {
                    pen.DashStyle = DashStyle.Dash;
                    g.DrawPath(pen, p);
                }
            }

            int capW = 3;
            int capH = (int)(bodyH * 0.44f);
            int capX = bodyX + bodyW + 1;
            int capY = bodyY + (bodyH - capH) / 2;
            using (GraphicsPath cp = RoundedCard.GetRoundedRectangle(new Rectangle(capX, capY, capW, capH), 1))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(130, 138, 150)))
            {
                g.FillPath(b, cp);
            }

            using (Font f = new Font("Arial", sz * 0.38f, FontStyle.Bold))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(160, 170, 185)))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString("?", f, b, new RectangleF(bodyX, bodyY - 1, bodyW, bodyH), sf);
            }
        }

        private void DrawModernCapsuleTrayIcon(Graphics g, int sz, int percent, bool isCharging)
        {
            Color levelColor;
            if (isCharging) levelColor = Color.FromArgb(0, 230, 118);
            else if (percent > 40) levelColor = Color.FromArgb(0, 230, 118);
            else if (percent > 20) levelColor = Color.FromArgb(255, 214, 0);
            else levelColor = Color.FromArgb(255, 45, 85);

            int bodyW = sz - 8;
            int bodyH = (int)(sz * 0.52f);
            int bodyX = 2;
            int bodyY = (sz - bodyH) / 2;

            using (GraphicsPath p = RoundedCard.GetRoundedRectangle(new Rectangle(bodyX, bodyY, bodyW, bodyH), 4))
            {
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(210, 14, 16, 22)))
                {
                    g.FillPath(bg, p);
                }
                using (Pen pen = new Pen(Color.White, 2.0f))
                {
                    g.DrawPath(pen, p);
                }
            }

            int capW = 3;
            int capH = (int)(bodyH * 0.44f);
            int capX = bodyX + bodyW + 1;
            int capY = bodyY + (bodyH - capH) / 2;
            using (GraphicsPath cp = RoundedCard.GetRoundedRectangle(new Rectangle(capX, capY, capW, capH), 1))
            using (SolidBrush b = new SolidBrush(Color.White))
            {
                g.FillPath(b, cp);
            }

            int innerPad = 2;
            int innerW = bodyW - (innerPad * 2) - 2;
            int innerH = bodyH - (innerPad * 2) - 2;
            int fillW = (int)(innerW * (percent / 100.0f));
            if (percent > 0 && fillW < 2) fillW = 2;

            if (fillW > 0)
            {
                Rectangle fillRect = new Rectangle(bodyX + innerPad + 1, bodyY + innerPad + 1, fillW, innerH);
                using (GraphicsPath fp = RoundedCard.GetRoundedRectangle(fillRect, 2))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(140, levelColor.R, levelColor.G, levelColor.B)))
                {
                    g.FillPath(b, fp);
                }
            }

            if (isCharging)
            {
                float cx = bodyX + (bodyW / 2.0f);
                float cy = bodyY + (bodyH / 2.0f);
                float bh = bodyH * 0.85f;
                float bw = bh * 0.50f;

                PointF[] bolt = new PointF[]
                {
                    new PointF(cx + (bw * 0.1f), cy - (bh * 0.5f)),
                    new PointF(cx - (bw * 0.5f), cy + (bh * 0.05f)),
                    new PointF(cx - (bw * 0.05f), cy + (bh * 0.05f)),
                    new PointF(cx - (bw * 0.15f), cy + (bh * 0.5f)),
                    new PointF(cx + (bw * 0.5f), cy - (bh * 0.05f)),
                    new PointF(cx + (bw * 0.05f), cy - (bh * 0.05f))
                };

                using (GraphicsPath bp = new GraphicsPath())
                {
                    bp.AddPolygon(bolt);
                    using (Pen glowPen = new Pen(Color.FromArgb(200, 0, 0, 0), 2.2f))
                    {
                        glowPen.LineJoin = LineJoin.Round;
                        g.DrawPath(glowPen, bp);
                    }
                    using (SolidBrush boltBrush = new SolidBrush(Color.FromArgb(0, 255, 136)))
                    {
                        g.FillPath(boltBrush, bp);
                    }
                }
            }
            else
            {
                string text = percent.ToString();
                float fontSize = (sz >= 32) ? (bodyH * 0.65f) : (bodyH * 0.72f);
                using (Font f = new Font("Arial", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    RectangleF textRect = new RectangleF(bodyX, bodyY, bodyW, bodyH);
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;

                    using (GraphicsPath textPath = new GraphicsPath())
                    {
                        textPath.AddString(text, f.FontFamily, (int)f.Style, f.Size, textRect, sf);
                        using (Pen outline = new Pen(Color.FromArgb(220, 0, 0, 0), 2.4f))
                        {
                            outline.LineJoin = LineJoin.Round;
                            g.DrawPath(outline, textPath);
                        }
                        using (SolidBrush fill = new SolidBrush(Color.White))
                        {
                            g.FillPath(fill, textPath);
                        }
                    }
                }
            }
        }

        private void DrawNumberBadgeTrayIcon(Graphics g, int sz, int percent, bool isCharging)
        {
            Color accentColor;
            if (isCharging) accentColor = Color.FromArgb(0, 230, 118);
            else if (percent > 40) accentColor = Color.FromArgb(0, 230, 118);
            else if (percent > 20) accentColor = Color.FromArgb(255, 214, 0);
            else accentColor = Color.FromArgb(255, 45, 85);

            float cx = sz / 2.0f;
            float cy = sz / 2.0f;

            string text = percent.ToString();
            float fontSize;
            if (percent == 100)
            {
                fontSize = sz * 0.44f;
            }
            else if (percent >= 10)
            {
                fontSize = sz * 0.54f;
            }
            else
            {
                fontSize = sz * 0.62f;
            }

            using (Font f = new Font("Arial", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;

                RectangleF textBounds = new RectangleF(0, 0, sz, sz);

                using (GraphicsPath textPath = new GraphicsPath())
                {
                    textPath.AddString(text, f.FontFamily, (int)f.Style, f.Size, textBounds, sf);

                    using (Pen outline = new Pen(Color.FromArgb(240, 5, 8, 12), sz * 0.14f))
                    {
                        outline.LineJoin = LineJoin.Round;
                        g.DrawPath(outline, textPath);
                    }

                    using (SolidBrush brush = new SolidBrush(accentColor))
                    {
                        g.FillPath(brush, textPath);
                    }
                }
            }

            if (isCharging)
            {
                float boltW = sz * 0.28f;
                float boltH = sz * 0.38f;
                float bx = sz - boltW - 1;
                float by = 1;

                PointF[] bolt = new PointF[]
                {
                    new PointF(bx + boltW * 0.65f, by),
                    new PointF(bx, by + boltH * 0.55f),
                    new PointF(bx + boltW * 0.45f, by + boltH * 0.55f),
                    new PointF(bx + boltW * 0.35f, by + boltH),
                    new PointF(bx + boltW, by + boltH * 0.45f),
                    new PointF(bx + boltW * 0.55f, by + boltH * 0.45f)
                };

                using (GraphicsPath bp = new GraphicsPath())
                {
                    bp.AddPolygon(bolt);
                    using (Pen glow = new Pen(Color.FromArgb(240, 0, 0, 0), 2.0f))
                    {
                        glow.LineJoin = LineJoin.Round;
                        g.DrawPath(glow, bp);
                    }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(0, 255, 136)))
                    {
                        g.FillPath(b, bp);
                    }
                }
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

                                                            if (status == 0x02 || (cmdClass == 0x07 && cmdId == 0x80))
                                                            {
                                                                int pct = (int)Math.Round((rawBatt / 255.0) * 100);
                                                                pct = Math.Max(0, Math.Min(100, pct));

                                                                // 2. Query Charging
                                                                bool isCharging = false;
                                                                byte[] chgReq = CreateRazerReport(tid, 0x07, 0x84, 0x02, caps.FeatureReportByteLength, prepended);
                                                                if (HidD_SetFeature(handle, chgReq, chgReq.Length))
                                                                {
                                                                    Thread.Sleep(15);
                                                                    byte[] chgResp = new byte[caps.FeatureReportByteLength];
                                                                    if (prepended) chgResp[0] = 0x00;
                                                                    if (HidD_GetFeature(handle, chgResp, chgResp.Length))
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

                                                                return new MouseBatteryInfo
                                                                {
                                                                    IsConnected = true,
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

                return new MouseBatteryInfo { IsConnected = false };
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
