using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using Orientation = System.Windows.Controls.Orientation;
using Cursors = System.Windows.Input.Cursors;

namespace ChattyWidgets.UI;

public static class InputDialog
{
    public static string? Show(Window? owner, string title, string prompt, string defaultValue = "")
    {
        string? result = null;

        var win = new Window
        {
            Title = title,
            Width = 420,
            Height = 210,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            Owner = owner,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize
        };

        var outerBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(22, 25, 40)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(42, 49, 70)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(22),
            Effect = new DropShadowEffect
            {
                BlurRadius = 24,
                ShadowDepth = 6,
                Opacity = 0.65,
                Color = Colors.Black
            }
        };

        var stack = new StackPanel();

        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            Margin = new Thickness(0, 0, 0, 8)
        };
        stack.Children.Add(titleText);

        var promptText = new TextBlock
        {
            Text = prompt,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            Margin = new Thickness(0, 0, 0, 10),
            TextWrapping = TextWrapping.Wrap
        };
        stack.Children.Add(promptText);

        var textBox = new TextBox
        {
            Text = defaultValue,
            Background = new SolidColorBrush(Color.FromRgb(16, 18, 28)),
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(51, 59, 82)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 16)
        };
        textBox.SelectAll();
        stack.Children.Add(textBox);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var btnCancel = new Button
        {
            Content = "Отмена",
            Width = 85,
            Height = 32,
            Margin = new Thickness(0, 0, 8, 0),
            Cursor = Cursors.Hand,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            BorderThickness = new Thickness(0)
        };
        btnCancel.Click += (s, e) => { win.DialogResult = false; win.Close(); };
        buttonPanel.Children.Add(btnCancel);

        var btnOk = new Button
        {
            Content = "Сохранить",
            Width = 100,
            Height = 32,
            Cursor = Cursors.Hand,
            Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0)
        };
        btnOk.Resources.Add(typeof(Border), new Style(typeof(Border))
        {
            Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(7)) }
        });

        void OnSubmit()
        {
            result = textBox.Text.Trim();
            win.DialogResult = true;
            win.Close();
        }

        btnOk.Click += (s, e) => OnSubmit();
        buttonPanel.Children.Add(btnOk);

        stack.Children.Add(buttonPanel);
        outerBorder.Child = stack;
        win.Content = outerBorder;

        textBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter)
            {
                OnSubmit();
            }
            else if (e.Key == Key.Escape)
            {
                win.DialogResult = false;
                win.Close();
            }
        };

        win.Loaded += (s, e) => textBox.Focus();

        bool? dialogRes = win.ShowDialog();
        return dialogRes == true ? result : null;
    }
}
