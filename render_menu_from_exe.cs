using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

class P
{
    [STAThread]
    static void Main()
    {
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        menu.ShowImageMargin = true;
        menu.ShowCheckMargin = false;
        menu.Padding = new Padding(3, 4, 3, 4);

        var asm = System.Reflection.Assembly.LoadFrom("RazerBatteryTray.exe");
        var rendererType = asm.GetType("RazerBatteryTray.ModernDarkMenuRenderer");
        menu.Renderer = (ToolStripRenderer)Activator.CreateInstance(rendererType);

        var header = new ToolStripMenuItem("Razer DeathAdder V4 Pro (100% · 充电中)");
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
            it.Padding = new Padding(6, 4, 12, 4);
        }

        menu.Show(100, 100);
        Application.DoEvents();

        Bitmap bmp = new Bitmap(menu.Width, menu.Height);
        menu.DrawToBitmap(bmp, new Rectangle(0, 0, menu.Width, menu.Height));
        bmp.Save("final_context_menu_v6.png");
        Console.WriteLine("Saved final_context_menu_v6.png");
        menu.Close();
    }
}
