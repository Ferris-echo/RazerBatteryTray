using System;
using System.Drawing;
using System.Windows.Forms;

class Test : ToolStripProfessionalRenderer
{
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        Console.WriteLine("ItemCheck: ItemHeight=" + e.Item.Height + ", ImageRect=" + e.ImageRectangle);
    }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        Console.WriteLine(e.Item.Text + ": ItemHeight=" + e.Item.Height + ", TextRect=" + e.TextRectangle + ", FontHeight=" + e.TextFont.Height);
    }
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        Console.WriteLine("Arrow: ArrowRect=" + e.ArrowRectangle + ", ItemHeight=" + e.Item.Height);
    }
}

class P
{
    [STAThread]
    static void Main()
    {
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Renderer = new Test();
        menu.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        menu.ShowImageMargin = true;
        menu.ShowCheckMargin = false;
        menu.Padding = new Padding(3, 4, 3, 4);

        var mi = new ToolStripMenuItem("测试项");
        mi.Checked = true;
        mi.Padding = new Padding(6, 4, 12, 4);
        var sub = new ToolStripMenuItem("子项");
        sub.DropDownItems.Add("1");
        sub.Padding = new Padding(6, 4, 12, 4);

        menu.Items.Add(mi);
        menu.Items.Add(sub);
        menu.Show(10, 10);
        Application.DoEvents();
        menu.Close();
    }
}
