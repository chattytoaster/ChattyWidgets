using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ChattyWidgets.Core.Models;
using ChattyWidgets.Core.Services;
using ChattyWidgets.UI.Controls;
using UserControl = System.Windows.Controls.UserControl;
using ProgressBar = System.Windows.Controls.ProgressBar;
using Orientation = System.Windows.Controls.Orientation;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace ChattyWidgets.UI.Views;

public partial class SystemGaugesView : UserControl
{
    private readonly string _instanceId;
    private SystemGaugesConfig _config = new();
    private readonly List<Action<SystemMetrics>> _updaters = new();

    public SystemGaugesView(string instanceId)
    {
        InitializeComponent();
        _instanceId = instanceId;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ReloadConfig();
        SystemMonitorService.Instance.MetricsUpdated += OnMetricsUpdated;
        UpdateView(SystemMonitorService.Instance.CurrentMetrics);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        SystemMonitorService.Instance.MetricsUpdated -= OnMetricsUpdated;
    }

    private void OnMetricsUpdated(SystemMetrics metrics)
    {
        Dispatcher.Invoke(() => UpdateView(metrics));
    }

    public void ReloadConfig()
    {
        _config = SystemGaugesConfig.Load(_instanceId);
        BuildDynamicUI();
        UpdateView(SystemMonitorService.Instance.CurrentMetrics);
    }

    private void BuildDynamicUI()
    {
        ItemsHost.Children.Clear();
        _updaters.Clear();

        var enabledItems = _config.Items.Where(i => i.IsEnabled).ToList();
        if (enabledItems.Count == 0)
        {
            var emptyNotice = new TextBlock
            {
                Text = "Все датчики отключены.\nОткройте Конструктор для включения.",
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 16, 0, 16)
            };
            ItemsHost.Children.Add(emptyNotice);
            return;
        }

        foreach (var item in enabledItems)
        {
            switch (item.Id.ToLowerInvariant())
            {
                case "cpu":
                    AddCpuItem(item);
                    break;
                case "ram":
                    AddRamItem(item);
                    break;
                case "disk":
                    AddDiskItem(item);
                    break;
                case "gpu":
                    AddGpuItem(item);
                    break;
                case "net":
                    AddNetItem(item);
                    break;
            }
        }
    }

    private void AddCpuItem(MetricGaugeItem item)
    {
        if (item.DisplayType == GaugeDisplayType.Circle)
        {
            ItemsHost.Children.Add(CreateCircleElement(item, (m, gauge, title, detail) =>
            {
                gauge.Value = m.CpuPercent;
                detail.Text = $"Загрузка: {m.CpuPercent:0}%";
            }));
        }
        else
        {
            ItemsHost.Children.Add(CreateBarElement(item, (m, pb, val) =>
            {
                pb.Value = m.CpuPercent;
                val.Text = $"{m.CpuPercent:0}%";
            }));
        }
    }

    private void AddRamItem(MetricGaugeItem item)
    {
        if (item.DisplayType == GaugeDisplayType.Circle)
        {
            ItemsHost.Children.Add(CreateCircleElement(item, (m, gauge, title, detail) =>
            {
                gauge.Value = m.MemoryPercent;
                detail.Text = $"{m.UsedMemoryGb:0.0} / {m.TotalMemoryGb:0} ГБ ({m.MemoryPercent:0}%)";
            }));
        }
        else
        {
            ItemsHost.Children.Add(CreateBarElement(item, (m, pb, val) =>
            {
                pb.Value = m.MemoryPercent;
                val.Text = $"{m.UsedMemoryGb:0.0} / {m.TotalMemoryGb:0} ГБ";
            }));
        }
    }

    private void AddDiskItem(MetricGaugeItem item)
    {
        if (item.DisplayType == GaugeDisplayType.Circle)
        {
            ItemsHost.Children.Add(CreateCircleElement(item, (m, gauge, title, detail) =>
            {
                gauge.Value = m.DiskPercent;
                detail.Text = $"Занято: {m.DiskPercent:0}%";
            }));
        }
        else
        {
            ItemsHost.Children.Add(CreateBarElement(item, (m, pb, val) =>
            {
                pb.Value = m.DiskPercent;
                val.Text = $"{m.DiskPercent:0}%";
            }));
        }
    }

    private void AddGpuItem(MetricGaugeItem item)
    {
        if (item.DisplayType == GaugeDisplayType.Circle)
        {
            ItemsHost.Children.Add(CreateCircleElement(item, (m, gauge, title, detail) =>
            {
                gauge.Value = m.GpuPercent;
                string tempStr = m.GpuTemperature > 0 ? $" • {m.GpuTemperature:0}°C" : "";
                detail.Text = $"{m.GpuName} ({m.GpuPercent:0}%{tempStr})";
            }));
        }
        else
        {
            ItemsHost.Children.Add(CreateBarElement(item, (m, pb, val) =>
            {
                pb.Value = m.GpuPercent;
                string tempStr = m.GpuTemperature > 0 ? $" {m.GpuTemperature:0}°C" : "";
                val.Text = $"{m.GpuPercent:0}%{tempStr}";
            }));
        }
    }

    private void AddNetItem(MetricGaugeItem item)
    {
        if (item.DisplayType == GaugeDisplayType.Circle)
        {
            var border = new Border
            {
                Margin = new Thickness(0, 3, 0, 4),
                Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 6, 8, 6)
            };

            var dock = new DockPanel();
            var gauge = new CircularGauge
            {
                Width = 46,
                Height = 46,
                StrokeThickness = 4.0,
                ProgressBrush = GetBrush(item.ColorHex),
                TrackBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Margin = new Thickness(0, 0, 10, 0)
            };
            DockPanel.SetDock(gauge, Dock.Left);
            dock.Children.Add(gauge);

            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var txtTitle = new TextBlock
            {
                Text = item.Title,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249))
            };
            var txtRates = new TextBlock
            {
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            sp.Children.Add(txtTitle);
            sp.Children.Add(txtRates);
            dock.Children.Add(sp);

            border.Child = dock;
            ItemsHost.Children.Add(border);

            _updaters.Add(m =>
            {
                double totalKb = m.NetworkDownloadKbps + m.NetworkUploadKbps;
                double valNorm = Math.Min(100.0, (totalKb / 5000.0) * 100.0);
                gauge.Value = valNorm;
                txtRates.Text = $"↓ {FormatSpeed(m.NetworkDownloadKbps)}  ↑ {FormatSpeed(m.NetworkUploadKbps)}";
            });
        }
        else
        {
            var border = new Border
            {
                Margin = new Thickness(0, 6, 0, 2),
                Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 5, 8, 5)
            };

            var dock = new DockPanel();
            var leftStack = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(leftStack, Dock.Left);

            var arrowDown = new TextBlock
            {
                Text = "↓ ",
                Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                FontWeight = FontWeights.Bold,
                FontSize = 11
            };
            var txtDown = new TextBlock
            {
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240))
            };
            leftStack.Children.Add(arrowDown);
            leftStack.Children.Add(txtDown);

            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(rightStack, Dock.Right);

            var arrowUp = new TextBlock
            {
                Text = "↑ ",
                Foreground = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                FontWeight = FontWeights.Bold,
                FontSize = 11
            };
            var txtUp = new TextBlock
            {
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240))
            };
            rightStack.Children.Add(arrowUp);
            rightStack.Children.Add(txtUp);

            dock.Children.Add(leftStack);
            dock.Children.Add(rightStack);
            border.Child = dock;
            ItemsHost.Children.Add(border);

            _updaters.Add(m =>
            {
                txtDown.Text = FormatSpeed(m.NetworkDownloadKbps);
                txtUp.Text = FormatSpeed(m.NetworkUploadKbps);
            });
        }
    }

    private static string FormatSpeed(double kbps)
    {
        if (kbps >= 1024.0)
        {
            return $"{(kbps / 1024.0):0.0} МБ/с";
        }
        return $"{kbps:0} КБ/с";
    }

    private UIElement CreateBarElement(MetricGaugeItem item, Action<SystemMetrics, ProgressBar, TextBlock> updateAction)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 4) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var dock = new DockPanel();
        var txtTitle = new TextBlock
        {
            Text = item.Title,
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225))
        };
        var txtVal = new TextBlock
        {
            FontSize = 11.5,
            FontWeight = FontWeights.Bold,
            Foreground = GetBrush(item.ColorHex),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        dock.Children.Add(txtVal);
        DockPanel.SetDock(txtVal, Dock.Right);
        dock.Children.Add(txtTitle);
        Grid.SetRow(dock, 0);

        var pb = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 6,
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = GetBrush(item.ColorHex),
            Background = GetBrush(item.ColorHex, 40),
            BorderThickness = new Thickness(0)
        };
        Grid.SetRow(pb, 1);

        grid.Children.Add(dock);
        grid.Children.Add(pb);

        _updaters.Add(m => updateAction(m, pb, txtVal));
        return grid;
    }

    private UIElement CreateCircleElement(MetricGaugeItem item, Action<SystemMetrics, CircularGauge, TextBlock, TextBlock> updateAction)
    {
        var border = new Border
        {
            Margin = new Thickness(0, 3, 0, 4),
            Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 6, 8, 6)
        };

        var dock = new DockPanel();
        var gauge = new CircularGauge
        {
            Width = 46,
            Height = 46,
            StrokeThickness = 4.0,
            ProgressBrush = GetBrush(item.ColorHex),
            TrackBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            Margin = new Thickness(0, 0, 10, 0)
        };
        DockPanel.SetDock(gauge, Dock.Left);
        dock.Children.Add(gauge);

        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var txtTitle = new TextBlock
        {
            Text = item.Title,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249))
        };
        var txtDetail = new TextBlock
        {
            FontSize = 11,
            Foreground = GetBrush(item.ColorHex),
            Margin = new Thickness(0, 2, 0, 0)
        };
        sp.Children.Add(txtTitle);
        sp.Children.Add(txtDetail);
        dock.Children.Add(sp);

        border.Child = dock;
        _updaters.Add(m => updateAction(m, gauge, txtTitle, txtDetail));
        return border;
    }

    private static Brush GetBrush(string hex, byte alpha = 255)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        }
        catch
        {
            return new SolidColorBrush(Color.FromArgb(alpha, 56, 189, 248));
        }
    }

    private void UpdateView(SystemMetrics metrics)
    {
        foreach (var updater in _updaters)
        {
            try { updater(metrics); } catch { }
        }
    }
}
