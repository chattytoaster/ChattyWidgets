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
    private bool _isLight = false;

    public SystemGaugesView(string instanceId)
    {
        InitializeComponent();
        _instanceId = instanceId;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void ApplyTheme(bool isLight)
    {
        _isLight = isLight;
        if (Content is Border rootBorder)
        {
            if (isLight)
            {
                rootBorder.Background = new SolidColorBrush(Color.FromArgb(242, 248, 250, 252));
                rootBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0));
                rootBorder.BorderThickness = new Thickness(1);
            }
            else
            {
                rootBorder.Background = new SolidColorBrush(Color.FromArgb(208, 18, 22, 34));
                rootBorder.BorderBrush = null;
                rootBorder.BorderThickness = new Thickness(0);
            }
        }
        if (TxtHeaderTitle != null)
        {
            TxtHeaderTitle.Foreground = _isLight ? new SolidColorBrush(Color.FromRgb(71, 85, 105)) : new SolidColorBrush(Color.FromRgb(148, 163, 184));
        }
        BuildDynamicUI();
        UpdateView(SystemMonitorService.Instance.CurrentMetrics);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ReloadConfig();
        SystemMonitorService.Instance.MetricsUpdated += OnMetricsUpdated;
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        UpdateHeaderAndLocalization();
        UpdateView(SystemMonitorService.Instance.CurrentMetrics);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        SystemMonitorService.Instance.MetricsUpdated -= OnMetricsUpdated;
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        Dispatcher.Invoke(() =>
        {
            UpdateHeaderAndLocalization();
            BuildDynamicUI();
            UpdateView(SystemMonitorService.Instance.CurrentMetrics);
        });
    }

    private void UpdateHeaderAndLocalization()
    {
        if (TxtHeaderTitle != null)
        {
            TxtHeaderTitle.Text = LocalizationService.Instance.T("Gauges.HeaderTitle", "СИСТЕМА");
        }
    }

    private string GetLocalizedTitle(MetricGaugeItem item)
    {
        var loc = LocalizationService.Instance;
        return item.Id.ToLowerInvariant() switch
        {
            "cpu" => loc.T("Gauges.CpuTitle", "Процессор (CPU)"),
            "ram" => loc.T("Gauges.RamTitle", "Память (RAM)"),
            "disk" => loc.T("Gauges.DiskTitle", "Диск (C:)"),
            "gpu" => loc.T("Gauges.GpuTitle", "Графика (GPU)"),
            "net" => loc.T("Gauges.NetTitle", "Сеть (Network)"),
            _ => item.Title
        };
    }

    private void OnMetricsUpdated(SystemMetrics metrics)
    {
        Dispatcher.Invoke(() => UpdateView(metrics));
    }

    public void ReloadConfig()
    {
        _config = SystemGaugesConfig.Load(_instanceId);
        UpdateHeaderAndLocalization();
        if (TxtHeaderTitle != null)
        {
            TxtHeaderTitle.Foreground = _isLight ? new SolidColorBrush(Color.FromRgb(71, 85, 105)) : new SolidColorBrush(Color.FromRgb(148, 163, 184));
        }
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
                Text = LocalizationService.Instance.T("Gauges.AllDisabled", "Все датчики отключены.\nОткройте Конструктор для включения."),
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 16, 0, 16)
            };
            ItemsHost.Children.Add(emptyNotice);
            return;
        }

        int circleCols = _config.CircleColumns > 0 ? _config.CircleColumns : 2;

        if (circleCols == 2)
        {
            int i = 0;
            while (i < enabledItems.Count)
            {
                var item = enabledItems[i];
                if (item.DisplayType == GaugeDisplayType.Bar)
                {
                    AddBarItem(item);
                    i++;
                }
                else
                {
                    // Circle item: pair with next Circle item if available
                    if (i + 1 < enabledItems.Count && enabledItems[i + 1].DisplayType == GaugeDisplayType.Circle)
                    {
                        var item2 = enabledItems[i + 1];
                        AddTwoCircleItemsInRow(item, item2);
                        i += 2;
                    }
                    else
                    {
                        AddSingleCircleItemInRow(item);
                        i++;
                    }
                }
            }
        }
        else
        {
            // 1 column list (traditional)
            foreach (var item in enabledItems)
            {
                if (item.DisplayType == GaugeDisplayType.Bar)
                {
                    AddBarItem(item);
                }
                else
                {
                    AddSingleCircleItemInRow(item);
                }
            }
        }
    }

    private void AddTwoCircleItemsInRow(MetricGaugeItem item1, MetricGaugeItem item2)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var elem1 = CreateCompactCircleElement(item1);
        Grid.SetColumn(elem1, 0);
        grid.Children.Add(elem1);

        var elem2 = CreateCompactCircleElement(item2);
        Grid.SetColumn(elem2, 2);
        grid.Children.Add(elem2);

        ItemsHost.Children.Add(grid);
    }

    private UIElement CreateCompactCircleElement(MetricGaugeItem item)
    {
        var border = new Border
        {
            Background = _isLight ? new SolidColorBrush(Color.FromArgb(24, 0, 0, 0)) : new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(6, 8, 6, 8)
        };

        var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

        var txtTitle = new TextBlock
        {
            Text = GetLocalizedTitle(item),
            FontWeight = FontWeights.SemiBold,
            FontSize = 11,
            Foreground = _isLight ? new SolidColorBrush(Color.FromRgb(15, 23, 42)) : new SolidColorBrush(Color.FromRgb(241, 245, 249)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 115
        };

        var gauge = new CircularGauge
        {
            Width = 48,
            Height = 48,
            StrokeThickness = 4.0,
            ProgressBrush = GetBrush(item.ColorHex),
            TrackBrush = _isLight ? new SolidColorBrush(Color.FromArgb(28, 0, 0, 0)) : new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            Foreground = _isLight ? new SolidColorBrush(Color.FromRgb(15, 23, 42)) : Brushes.White,
            Margin = new Thickness(0, 5, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var txtDetail = new TextBlock
        {
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = GetBrush(item.ColorHex),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 115
        };

        sp.Children.Add(txtTitle);
        sp.Children.Add(gauge);
        sp.Children.Add(txtDetail);
        border.Child = sp;

        switch (item.Id.ToLowerInvariant())
        {
            case "cpu":
                _updaters.Add(m =>
                {
                    gauge.Value = m.CpuPercent;
                    txtDetail.Text = $"{m.CpuPercent:0}%";
                });
                break;
            case "ram":
                _updaters.Add(m =>
                {
                    gauge.Value = m.MemoryPercent;
                    string unit = LocalizationService.Instance.IsRussian ? "ГБ" : "GB";
                    txtDetail.Text = $"{m.UsedMemoryGb:0.0} / {m.TotalMemoryGb:0} {unit}";
                });
                break;
            case "disk":
                _updaters.Add(m =>
                {
                    gauge.Value = m.DiskPercent;
                    txtDetail.Text = $"{m.DiskPercent:0}%";
                });
                break;
            case "gpu":
                _updaters.Add(m =>
                {
                    gauge.Value = m.GpuPercent;
                    string tempStr = m.GpuTemperature > 0 ? $" • {m.GpuTemperature:0}°C" : "";
                    txtDetail.Text = $"{m.GpuPercent:0}%{tempStr}";
                });
                break;
            case "net":
                _updaters.Add(m =>
                {
                    double totalKb = m.NetworkDownloadKbps + m.NetworkUploadKbps;
                    double valNorm = Math.Min(100.0, (totalKb / 5000.0) * 100.0);
                    gauge.Value = valNorm;
                    txtDetail.Text = $"↓{FormatSpeed(m.NetworkDownloadKbps)} ↑{FormatSpeed(m.NetworkUploadKbps)}";
                });
                break;
        }

        return border;
    }

    private void AddSingleCircleItemInRow(MetricGaugeItem item)
    {
        switch (item.Id.ToLowerInvariant())
        {
            case "cpu":
                ItemsHost.Children.Add(CreateCircleElement(item, (m, gauge, title, detail) =>
                {
                    gauge.Value = m.CpuPercent;
                    string prefix = LocalizationService.Instance.IsRussian ? "Загрузка: " : "Load: ";
                    detail.Text = $"{prefix}{m.CpuPercent:0}%";
                }));
                break;
            case "ram":
                ItemsHost.Children.Add(CreateCircleElement(item, (m, gauge, title, detail) =>
                {
                    gauge.Value = m.MemoryPercent;
                    string unit = LocalizationService.Instance.IsRussian ? "ГБ" : "GB";
                    detail.Text = $"{m.UsedMemoryGb:0.0} / {m.TotalMemoryGb:0} {unit} ({m.MemoryPercent:0}%)";
                }));
                break;
            case "disk":
                ItemsHost.Children.Add(CreateCircleElement(item, (m, gauge, title, detail) =>
                {
                    gauge.Value = m.DiskPercent;
                    string prefix = LocalizationService.Instance.IsRussian ? "Занято: " : "Used: ";
                    detail.Text = $"{prefix}{m.DiskPercent:0}%";
                }));
                break;
            case "gpu":
                ItemsHost.Children.Add(CreateCircleElement(item, (m, gauge, title, detail) =>
                {
                    gauge.Value = m.GpuPercent;
                    string tempStr = m.GpuTemperature > 0 ? $" • {m.GpuTemperature:0}°C" : "";
                    detail.Text = $"{m.GpuName} ({m.GpuPercent:0}%{tempStr})";
                }));
                break;
            case "net":
                AddNetCircleItem(item);
                break;
        }
    }

    private void AddBarItem(MetricGaugeItem item)
    {
        switch (item.Id.ToLowerInvariant())
        {
            case "cpu":
                ItemsHost.Children.Add(CreateBarElement(item, (m, pb, val) =>
                {
                    pb.Value = m.CpuPercent;
                    val.Text = $"{m.CpuPercent:0}%";
                }));
                break;
            case "ram":
                ItemsHost.Children.Add(CreateBarElement(item, (m, pb, val) =>
                {
                    pb.Value = m.MemoryPercent;
                    string unit = LocalizationService.Instance.IsRussian ? "ГБ" : "GB";
                    val.Text = $"{m.UsedMemoryGb:0.0} / {m.TotalMemoryGb:0} {unit}";
                }));
                break;
            case "disk":
                ItemsHost.Children.Add(CreateBarElement(item, (m, pb, val) =>
                {
                    pb.Value = m.DiskPercent;
                    val.Text = $"{m.DiskPercent:0}%";
                }));
                break;
            case "gpu":
                ItemsHost.Children.Add(CreateBarElement(item, (m, pb, val) =>
                {
                    pb.Value = m.GpuPercent;
                    string tempStr = m.GpuTemperature > 0 ? $" {m.GpuTemperature:0}°C" : "";
                    val.Text = $"{m.GpuPercent:0}%{tempStr}";
                }));
                break;
            case "net":
                AddNetBarItem(item);
                break;
        }
    }

    private void AddNetCircleItem(MetricGaugeItem item)
    {
        var border = new Border
        {
            Margin = new Thickness(0, 3, 0, 4),
            Background = _isLight ? new SolidColorBrush(Color.FromArgb(24, 0, 0, 0)) : new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
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
            TrackBrush = _isLight ? new SolidColorBrush(Color.FromArgb(28, 0, 0, 0)) : new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            Foreground = _isLight ? new SolidColorBrush(Color.FromRgb(15, 23, 42)) : Brushes.White,
            Margin = new Thickness(0, 0, 10, 0)
        };
        DockPanel.SetDock(gauge, Dock.Left);
        dock.Children.Add(gauge);

        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var txtTitle = new TextBlock
        {
            Text = GetLocalizedTitle(item),
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Foreground = _isLight ? new SolidColorBrush(Color.FromRgb(15, 23, 42)) : new SolidColorBrush(Color.FromRgb(241, 245, 249))
        };
        var txtRates = new TextBlock
        {
            FontSize = 11,
            Foreground = _isLight ? new SolidColorBrush(Color.FromRgb(71, 85, 105)) : new SolidColorBrush(Color.FromRgb(148, 163, 184)),
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

    private void AddNetBarItem(MetricGaugeItem item)
    {
        var border = new Border
        {
            Margin = new Thickness(0, 6, 0, 2),
            Background = _isLight ? new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)) : new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
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
            Foreground = _isLight ? new SolidColorBrush(Color.FromRgb(15, 23, 42)) : new SolidColorBrush(Color.FromRgb(226, 232, 240))
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
            Foreground = _isLight ? new SolidColorBrush(Color.FromRgb(15, 23, 42)) : new SolidColorBrush(Color.FromRgb(226, 232, 240))
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

    private static string FormatSpeed(double kbps)
    {
        bool isRu = LocalizationService.Instance.IsRussian;
        string unitMb = isRu ? "МБ/с" : "MB/s";
        string unitKb = isRu ? "КБ/с" : "KB/s";
        if (kbps >= 1024.0)
        {
            return $"{(kbps / 1024.0):0.0} {unitMb}";
        }
        return $"{kbps:0} {unitKb}";
    }

    private UIElement CreateBarElement(MetricGaugeItem item, Action<SystemMetrics, ProgressBar, TextBlock> updateAction)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 4) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var dock = new DockPanel();
        var txtTitle = new TextBlock
        {
            Text = GetLocalizedTitle(item),
            FontSize = 11.5,
            Foreground = _isLight ? new SolidColorBrush(Color.FromRgb(15, 23, 42)) : new SolidColorBrush(Color.FromRgb(203, 213, 225))
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
            Background = _isLight ? new SolidColorBrush(Color.FromArgb(25, 0, 0, 0)) : GetBrush(item.ColorHex, 40),
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
            Background = _isLight ? new SolidColorBrush(Color.FromArgb(24, 0, 0, 0)) : new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
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
            TrackBrush = _isLight ? new SolidColorBrush(Color.FromArgb(28, 0, 0, 0)) : new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            Foreground = _isLight ? new SolidColorBrush(Color.FromRgb(15, 23, 42)) : Brushes.White,
            Margin = new Thickness(0, 0, 10, 0)
        };
        DockPanel.SetDock(gauge, Dock.Left);
        dock.Children.Add(gauge);

        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var txtTitle = new TextBlock
        {
            Text = GetLocalizedTitle(item),
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Foreground = _isLight ? new SolidColorBrush(Color.FromRgb(15, 23, 42)) : new SolidColorBrush(Color.FromRgb(241, 245, 249))
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
