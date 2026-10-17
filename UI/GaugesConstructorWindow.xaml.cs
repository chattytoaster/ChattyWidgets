using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ChattyWidgets.Core.Models;
using ChattyWidgets.Core.Services;
using Button = System.Windows.Controls.Button;
using RadioButton = System.Windows.Controls.RadioButton;
using CheckBox = System.Windows.Controls.CheckBox;
using Orientation = System.Windows.Controls.Orientation;
using Cursors = System.Windows.Input.Cursors;

namespace ChattyWidgets.UI;

public partial class GaugesConstructorWindow : Window
{
    private readonly string _instanceId;
    private readonly Action? _onSaved;
    private SystemGaugesConfig _config;

    public GaugesConstructorWindow(string instanceId, Action? onSaved = null)
    {
        InitializeComponent();
        _instanceId = instanceId;
        _onSaved = onSaved;
        _config = SystemGaugesConfig.Load(instanceId);

        if (_config.CircleColumns == 1)
        {
            RbCols1.IsChecked = true;
        }
        else
        {
            RbCols2.IsChecked = true;
        }

        ApplyLocalization();
        RenderItems();
    }

    private void ApplyLocalization()
    {
        var loc = LocalizationService.Instance;
        Title = loc.T("GaugesConstructor.Title", "Конструктор формата датчиков");
        if (TxtHeaderTitle != null) TxtHeaderTitle.Text = loc.T("GaugesConstructor.Header", "Конструктор формата виджета");
        if (TxtDesc1 != null) TxtDesc1.Text = loc.T("GaugesConstructor.Desc1", "Настройте элементы, их порядок и вид отображения:");
        if (TxtDesc2 != null) TxtDesc2.Text = loc.T("GaugesConstructor.Desc2", "Вы можете перемещать метрики кнопками ▲ и ▼, включать GPU и выбирать между полоской и круговой диаграммой.");
        if (TxtCirclesLabel != null) TxtCirclesLabel.Text = loc.T("GaugesConstructor.CirclesLabel", "Круговые диаграммы:");
        if (RbCols2 != null) RbCols2.Content = loc.T("GaugesConstructor.Cols2", "2 в ряд (Сетка)");
        if (RbCols1 != null) RbCols1.Content = loc.T("GaugesConstructor.Cols1", "1 в ряд (Список)");
        if (BtnResetDefaults != null) BtnResetDefaults.Content = loc.T("GaugesConstructor.Reset", "↺ По умолчанию");
        if (BtnCancel != null) BtnCancel.Content = loc.T("GaugesConstructor.Cancel", "Отмена");
        if (BtnApply != null) BtnApply.Content = loc.T("GaugesConstructor.Apply", "Применить");
    }

    private void OnCols2Checked(object sender, RoutedEventArgs e)
    {
        if (_config != null) _config.CircleColumns = 2;
    }

    private void OnCols1Checked(object sender, RoutedEventArgs e)
    {
        if (_config != null) _config.CircleColumns = 1;
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnResetDefaultsClick(object sender, RoutedEventArgs e)
    {
        _config = SystemGaugesConfig.CreateDefault();
        RbCols2.IsChecked = true;
        RenderItems();
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        SystemGaugesConfig.Save(_instanceId, _config);
        _onSaved?.Invoke();
        Close();
    }

    private void RenderItems()
    {
        ItemsPanel.Children.Clear();

        for (int i = 0; i < _config.Items.Count; i++)
        {
            int index = i;
            var item = _config.Items[i];

            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var dock = new DockPanel();

            // Left side: Reorder buttons (▲ / ▼)
            var reorderStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 8, 0) };
            DockPanel.SetDock(reorderStack, Dock.Left);

            var btnUp = new Button
            {
                Content = "▲",
                FontSize = 9,
                Width = 22,
                Height = 22,
                Margin = new Thickness(0, 0, 3, 0),
                Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                IsEnabled = index > 0
            };
            btnUp.Click += (s, e) =>
            {
                if (index > 0)
                {
                    var temp = _config.Items[index];
                    _config.Items[index] = _config.Items[index - 1];
                    _config.Items[index - 1] = temp;
                    RenderItems();
                }
            };

            var btnDown = new Button
            {
                Content = "▼",
                FontSize = 9,
                Width = 22,
                Height = 22,
                Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                IsEnabled = index < _config.Items.Count - 1
            };
            btnDown.Click += (s, e) =>
            {
                if (index < _config.Items.Count - 1)
                {
                    var temp = _config.Items[index];
                    _config.Items[index] = _config.Items[index + 1];
                    _config.Items[index + 1] = temp;
                    RenderItems();
                }
            };

            reorderStack.Children.Add(btnUp);
            reorderStack.Children.Add(btnDown);
            dock.Children.Add(reorderStack);

            // Right side: Format options (Полоска / Круг)
            var formatStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(formatStack, Dock.Right);

            var loc = LocalizationService.Instance;
            var rbBar = new RadioButton
            {
                Content = loc.T("SystemGauges.FormatBar", "▬ Полоска"),
                GroupName = $"Format_{item.Id}",
                IsChecked = item.DisplayType == GaugeDisplayType.Bar,
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                FontSize = 11,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            rbBar.Checked += (s, e) => item.DisplayType = GaugeDisplayType.Bar;

            var rbCircle = new RadioButton
            {
                Content = loc.T("SystemGauges.FormatCircle", "◯ Круг"),
                GroupName = $"Format_{item.Id}",
                IsChecked = item.DisplayType == GaugeDisplayType.Circle,
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                FontSize = 11,
                Cursor = Cursors.Hand
            };
            rbCircle.Checked += (s, e) => item.DisplayType = GaugeDisplayType.Circle;

            formatStack.Children.Add(rbBar);
            formatStack.Children.Add(rbCircle);
            dock.Children.Add(formatStack);

            // Center: Checkbox with Icon and Title
            var cb = new CheckBox
            {
                IsChecked = item.IsEnabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            cb.Checked += (s, e) => item.IsEnabled = true;
            cb.Unchecked += (s, e) => item.IsEnabled = false;

            var titleBlock = new TextBlock
            {
                Text = GetMetricDisplayLabel(item),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                VerticalAlignment = VerticalAlignment.Center
            };

            var centerStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            centerStack.Children.Add(cb);
            centerStack.Children.Add(titleBlock);
            dock.Children.Add(centerStack);

            card.Child = dock;
            ItemsPanel.Children.Add(card);
        }
    }

    private static string GetMetricDisplayLabel(MetricGaugeItem item)
    {
        var loc = LocalizationService.Instance;
        return item.Id.ToLowerInvariant() switch
        {
            "cpu" => "⚡ " + loc.T("SystemGauges.Cpu", "Процессор (CPU)"),
            "ram" => "🧠 " + loc.T("SystemGauges.Ram", "Память (RAM)"),
            "disk" => "💾 " + loc.T("SystemGauges.Disk", "Накопитель (Диск C:)"),
            "gpu" => "🎮 " + loc.T("SystemGauges.Gpu", "Видеокарта (GPU)"),
            "net" => "🌐 " + loc.T("SystemGauges.Net", "Сеть (Скорость)"),
            _ => item.Title
        };
    }
}
