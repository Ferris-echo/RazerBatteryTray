using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

class ModernDarkMenuRenderer2 : ToolStripProfessionalRenderer
{
    private readonly Color bgCol = Color.FromArgb(24, 27, 34);
    private readonly Color borderCol = Color.FromArgb(48, 54, 68);
    private readonly Color hoverCol = Color.FromArgb(38, 44, 58);
    private readonly Color hoverBorder = Color.FromArgb(60, 68, 86);
    private readonly Color textCol = Color.FromArgb(235, 240, 248);
    private readonly Color disabledCol = Color.FromArgb(120, 128, 144);
    private readonly Color separatorCol = Color.FromArgb(42, 48, 62);
    private readonly Color accentGreen = Color.FromArgb(0, 230, 118);

    public ModernDarkMenuRenderer2() : base(new DarkTable()) { }

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
            Rectangle r = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);
            using (GraphicsPath path = GetRoundedRectangle(r, 4))
            {
                using (SolidBrush b = new SolidBrush(hoverCol))
                    e.Graphics.FillPath(b, path);
                using (Pen p = new Pen(hoverBorder, 1f))
                    e.Graphics.DrawPath(p, path);
            }
        }
    }

    private static GraphicsPath GetRoundedRectangle(Rectangle rect, int radius)
    {
        GraphicsPath path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        Color color = textCol;
        if (!e.Item.Enabled)
            color = disabledCol;
        else if (e.Item.Tag != null && e.Item.Tag.ToString() == "Header")
            color = accentGreen;
        else if (e.Item.Selected)
            color = Color.White;

        // Perfectly vertically center the text across the full Item Height
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
        float cx = 17f; // Aligned with the left image margin column

        // Sleek Razer Chroma Green vector checkmark, exactly centered
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
        float rightX = e.Item.Width - 14f;

        Color arrowColor = e.Item.Selected ? Color.White : Color.FromArgb(145, 155, 175);
        PointF[] arrow = new PointF[] {
            new PointF(rightX - 4.5f, centerY - 4.5f),
            new PointF(rightX, centerY),
            new PointF(rightX - 4.5f, centerY + 4.5f)
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

    private class DarkTable : ProfessionalColorTable
    {
        public override Color MenuBorder { get { return Color.FromArgb(48, 54, 68); } }
        public override Color MenuItemBorder { get { return Color.Transparent; } }
        public override Color MenuItemSelected { get { return Color.FromArgb(38, 44, 58); } }
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(24, 27, 34); } }
    }
}

class P
{
    [STAThread]
    static void Main()
    {
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Renderer = new ModernDarkMenuRenderer2();
        menu.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        menu.ShowImageMargin = true;
        menu.ShowCheckMargin = false;
        menu.Padding = new Padding(3, 4, 3, 4);

        var header = new ToolStripMenuItem("● Razer DeathAdder V4 Pro (100% · 充电中)");
        header.Tag = "Header";
        header.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
        var itemOpen = new ToolStripMenuItem("打开控制面板 (O)");
        var itemRefresh = new ToolStripMenuItem("立即刷新电量 (R)");
        var itemInterval = new ToolStripMenuItem("自动刷新频率 (I)");
        itemInterval.DropDownItems.Add("30 秒");
        var itemStyle = new ToolStripMenuItem("托盘图标样式 (T)");
        itemStyle.DropDownItems.Add("现代胶囊电池 (推荐)");

        var itemAlert = new ToolStripMenuItem("低电量气泡通知 (≤20%)");
        itemAlert.Checked = true;
        var itemAuto = new ToolStripMenuItem("开机自动启动");
        itemAuto.Checked = true;

        var itemExit = new ToolStripMenuItem("退出程序 (X)");

        menu.Items.AddRange(new ToolStripItem[] {
            header, new ToolStripSeparator(),
            itemOpen, itemRefresh, itemInterval, itemStyle,
            new ToolStripSeparator(),
            itemAlert, itemAuto,
            new ToolStripSeparator(),
            itemExit
        });

        foreach (ToolStripItem it in menu.Items) {
            it.Padding = new Padding(6, 5, 14, 5);
        }

        // Show and capture
        menu.Show(100, 100);
        Application.DoEvents();

        // Render to Bitmap
        Bitmap bmp = new Bitmap(menu.Width, menu.Height);
        menu.DrawToBitmap(bmp, new Rectangle(0, 0, menu.Width, menu.Height));
        bmp.Save("rendered_menu_test.png");
        Console.WriteLine("Saved rendered_menu_test.png, size=" + menu.Size);
        menu.Close();
    }
}
