using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ChattyWidgets.Core.Services;

namespace ChattyWidgets.UI;

public class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Action _showMainWindowAction;
    private readonly Action<string> _openTabAction;

    public TrayIconManager(Action showMainWindowAction, Action<string> openTabAction)
    {
        _showMainWindowAction = showMainWindowAction;
        _openTabAction = openTabAction;

        _notifyIcon = new NotifyIcon
        {
            Text = "ChattyWidgets — Движок виджетов рабочего стола",
            Icon = CreateApplicationIcon(),
            Visible = true
        };

        _notifyIcon.DoubleClick += (s, e) => _showMainWindowAction();
        BuildContextMenu();

        WidgetManager.Instance.WidgetsChanged += BuildContextMenu;
    }

    public void BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem("Открыть Хаб ChattyWidgets")
        {
            Font = new Font(menu.Font, System.Drawing.FontStyle.Bold)
        };
        openItem.Click += (s, e) => _showMainWindowAction();
        menu.Items.Add(openItem);

        menu.Items.Add(new ToolStripSeparator());

        // Quick widget toggles
        var widgetsHeader = new ToolStripMenuItem("Виджеты на рабочем столе:") { Enabled = false };
        menu.Items.Add(widgetsHeader);

        var installed = ChwPackageService.GetInstalledWidgets();
        if (installed.Count == 0)
        {
            menu.Items.Add(new ToolStripMenuItem("Нет установленных виджетов") { Enabled = false });
        }
        else
        {
            foreach (var (manifest, dir) in installed)
            {
                bool isActive = WidgetManager.Instance.IsWidgetActive(manifest.Id);
                var widgetItem = new ToolStripMenuItem(manifest.Name)
                {
                    Checked = isActive,
                    CheckOnClick = false
                };
                string id = manifest.Id;
                widgetItem.Click += (s, e) =>
                {
                    WidgetManager.Instance.ToggleWidget(id);
                    BuildContextMenu();
                };
                menu.Items.Add(widgetItem);
            }
        }

        menu.Items.Add(new ToolStripSeparator());

        var storeItem = new ToolStripMenuItem("Магазин виджетов...");
        storeItem.Click += (s, e) => _openTabAction("store");
        menu.Items.Add(storeItem);

        var workshopItem = new ToolStripMenuItem("Мастерская виджетов (.chw)...");
        workshopItem.Click += (s, e) => _openTabAction("workshop");
        menu.Items.Add(workshopItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Выход");
        exitItem.Click += (s, e) =>
        {
            _notifyIcon.Visible = false;
            System.Windows.Application.Current.Shutdown();
        };
        menu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = menu;
    }

    public static Icon CreateApplicationIcon()
    {
        string baseDirIco = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
        if (File.Exists(baseDirIco))
        {
            try { return new Icon(baseDirIco, 32, 32); } catch { }
        }

        string currDirIco = Path.Combine(Directory.GetCurrentDirectory(), "app.ico");
        if (File.Exists(currDirIco))
        {
            try { return new Icon(currDirIco, 32, 32); } catch { }
        }

        // Generate clean fallback icon
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);

            using var brush = new SolidBrush(System.Drawing.Color.FromArgb(99, 102, 241)); // Indigo accent
            g.FillRoundedRectangle(brush, new Rectangle(2, 2, 28, 28), 6);

            using var pen = new Pen(System.Drawing.Color.FromArgb(255, 255, 255), 2.5f);
            // Draw a stylish W symbol
            var points = new[]
            {
                new PointF(7, 10),
                new PointF(11, 22),
                new PointF(16, 14),
                new PointF(21, 22),
                new PointF(25, 10)
            };
            g.DrawLines(pen, points);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void ShowNotification(string title, string text)
    {
        _notifyIcon.ShowBalloonTip(3000, title, text, ToolTipIcon.Info);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}

public static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, System.Drawing.Brush brush, Rectangle bounds, int cornerRadius)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, cornerRadius * 2, cornerRadius * 2, 180, 90);
        path.AddArc(bounds.Right - (cornerRadius * 2), bounds.Y, cornerRadius * 2, cornerRadius * 2, 270, 90);
        path.AddArc(bounds.Right - (cornerRadius * 2), bounds.Bottom - (cornerRadius * 2), cornerRadius * 2, cornerRadius * 2, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - (cornerRadius * 2), cornerRadius * 2, cornerRadius * 2, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }
}
