using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ChattyWidgets.Core.Services;

namespace ChattyWidgets.UI;

public class TrayIconManager : IDisposable
{
    public static TrayIconManager? CurrentInstance { get; private set; }

    private readonly NotifyIcon _notifyIcon;
    private readonly Action _showMainWindowAction;
    private readonly Action<string> _openTabAction;

    public TrayIconManager(Action showMainWindowAction, Action<string> openTabAction)
    {
        CurrentInstance = this;
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
        PresetService.Instance.PresetsChanged += BuildContextMenu;
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(BuildContextMenu);
    }

    public void BuildContextMenu()
    {
        var loc = LocalizationService.Instance;
        _notifyIcon.Text = loc.T("Tray.Tooltip", "ChattyWidgets — Движок виджетов рабочего стола");
        var menu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem(loc.T("Tray.OpenHub", "Открыть Хаб ChattyWidgets"))
        {
            Font = new Font(menu.Font, System.Drawing.FontStyle.Bold)
        };
        openItem.Click += (s, e) => _showMainWindowAction();
        menu.Items.Add(openItem);

        menu.Items.Add(new ToolStripSeparator());

        // Desktop Presets submenu
        var presetsItem = new ToolStripMenuItem(loc.T("Tray.Presets", "Пресеты рабочих столов (.chwp)"));
        var settings = WidgetManager.Instance.Settings;
        if (settings.Presets == null || settings.Presets.Count == 0)
        {
            presetsItem.DropDownItems.Add(new ToolStripMenuItem(loc.T("Tray.NoPresets", "(Нет сохранённых пресетов)")) { Enabled = false });
        }
        else
        {
            foreach (var p in settings.Presets)
            {
                string pId = p.Id;
                var pItem = new ToolStripMenuItem($"{p.Icon} {p.Name}")
                {
                    Checked = (settings.ActivePresetId == p.Id)
                };
                pItem.Click += (s, e) =>
                {
                    PresetService.Instance.ApplyPreset(pId);
                    BuildContextMenu();
                };
                presetsItem.DropDownItems.Add(pItem);
            }
        }

        presetsItem.DropDownItems.Add(new ToolStripSeparator());
        var importChwpItem = new ToolStripMenuItem(loc.T("Tray.ImportPreset", "Импортировать .chwp..."));
        importChwpItem.Click += (s, e) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var ofd = new Microsoft.Win32.OpenFileDialog
                {
                    Title = loc.T("Tray.ImportDialogTitle", "Импорт пресета .chwp"),
                    Filter = loc.T("Tray.ImportFilter", "Пакет пресета ChattyWidgets (*.chwp)|*.chwp|Все файлы (*.*)|*.*")
                };
                if (ofd.ShowDialog() == true)
                {
                    if (System.Windows.Application.Current.MainWindow is MainWindow mw)
                    {
                        mw.Show();
                        mw.ImportPresetFile(ofd.FileName);
                    }
                }
            });
        };
        presetsItem.DropDownItems.Add(importChwpItem);
        menu.Items.Add(presetsItem);

        // Quick Hotkeys actions
        var hideShowItem = new ToolStripMenuItem(loc.T("Tray.ToggleWidgets", "Скрыть / Показать виджеты (Win+Alt+W)"));
        hideShowItem.Click += (s, e) => HotkeyService.Instance.ToggleWidgetsVisibility();
        menu.Items.Add(hideShowItem);

        var peekItem = new ToolStripMenuItem(loc.T("Tray.PeekMode", "Режим «Быстрый взгляд» (Win+Alt+P)"));
        peekItem.Click += (s, e) => HotkeyService.Instance.TogglePeekMode();
        menu.Items.Add(peekItem);

        menu.Items.Add(new ToolStripSeparator());

        // Quick widget toggles
        var widgetsHeader = new ToolStripMenuItem(loc.T("Tray.ActiveWidgets", "Виджеты на рабочем столе:")) { Enabled = false };
        menu.Items.Add(widgetsHeader);

        var installed = ChwPackageService.GetInstalledWidgets();
        if (installed.Count == 0)
        {
            menu.Items.Add(new ToolStripMenuItem(loc.T("Tray.NoWidgets", "Нет установленных виджетов")) { Enabled = false });
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

        var storeItem = new ToolStripMenuItem(loc.T("Tray.Store", "Магазин виджетов..."));
        storeItem.Click += (s, e) => _openTabAction("store");
        menu.Items.Add(storeItem);

        var workshopItem = new ToolStripMenuItem(loc.T("Tray.Workshop", "Мастерская виджетов (.chw)..."));
        workshopItem.Click += (s, e) => _openTabAction("workshop");
        menu.Items.Add(workshopItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem(loc.T("Tray.Exit", "Выход"));
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
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
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
