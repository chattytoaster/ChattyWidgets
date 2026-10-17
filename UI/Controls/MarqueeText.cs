#nullable enable
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Control = System.Windows.Controls.Control;
using Size = System.Windows.Size;

namespace ChattyWidgets.UI.Controls;

/// <summary>
/// A standalone WPF Control that displays a single line of text and automatically
/// scrolls it in a ping-pong or continuous marquee when it overflows available container width.
/// </summary>
public class MarqueeText : Control
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(MarqueeText),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnTextChanged));

    public static readonly DependencyProperty SpeedProperty =
        DependencyProperty.Register(
            nameof(Speed),
            typeof(double),
            typeof(MarqueeText),
            new FrameworkPropertyMetadata(40.0, OnMarqueePropertyChanged));

    public static readonly DependencyProperty PauseDurationProperty =
        DependencyProperty.Register(
            nameof(PauseDuration),
            typeof(double),
            typeof(MarqueeText),
            new FrameworkPropertyMetadata(1.5, OnMarqueePropertyChanged));

    public static readonly DependencyProperty PaddingEndProperty =
        DependencyProperty.Register(
            nameof(PaddingEnd),
            typeof(double),
            typeof(MarqueeText),
            new FrameworkPropertyMetadata(24.0, OnMarqueePropertyChanged));

    public static readonly DependencyProperty HoverPauseProperty =
        DependencyProperty.Register(
            nameof(HoverPause),
            typeof(bool),
            typeof(MarqueeText),
            new FrameworkPropertyMetadata(true, OnMarqueePropertyChanged));

    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.Register(
            nameof(Mode),
            typeof(MarqueeMode),
            typeof(MarqueeText),
            new FrameworkPropertyMetadata(MarqueeMode.PingPong, OnMarqueePropertyChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double Speed
    {
        get => (double)GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    public double PauseDuration
    {
        get => (double)GetValue(PauseDurationProperty);
        set => SetValue(PauseDurationProperty, value);
    }

    public double PaddingEnd
    {
        get => (double)GetValue(PaddingEndProperty);
        set => SetValue(PaddingEndProperty, value);
    }

    public bool HoverPause
    {
        get => (bool)GetValue(HoverPauseProperty);
        set => SetValue(HoverPauseProperty, value);
    }

    public MarqueeMode Mode
    {
        get => (MarqueeMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    private readonly Border _clipBorder;
    private readonly Canvas _canvas;
    private readonly TextBlock _textBlock;

    static MarqueeText()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MarqueeText), new FrameworkPropertyMetadata(typeof(MarqueeText)));
    }

    public MarqueeText()
    {
        ClipToBounds = true;
        HorizontalContentAlignment = HorizontalAlignment.Left;
        VerticalContentAlignment = VerticalAlignment.Center;

        _textBlock = new TextBlock();
        _canvas = new Canvas
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _canvas.Children.Add(_textBlock);

        _clipBorder = new Border
        {
            ClipToBounds = true,
            Background = Brushes.Transparent,
            Child = _canvas
        };

        AddVisualChild(_clipBorder);
        AddLogicalChild(_clipBorder);

        MarqueeBehavior.SetEnableMarquee(_textBlock, true);
        SyncProperties();

        Loaded += (_, _) => SyncProperties();
        SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Height > 0)
        {
            _canvas.Height = e.NewSize.Height;
        }
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index)
    {
        if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
        return _clipBorder;
    }

    protected override Size MeasureOverride(Size constraint)
    {
        _textBlock.FontFamily = FontFamily;
        _textBlock.FontSize = FontSize;
        _textBlock.FontWeight = FontWeight;
        _textBlock.FontStyle = FontStyle;
        _textBlock.FontStretch = FontStretch;
        _textBlock.Foreground = Foreground;
        _textBlock.Text = Text;

        _textBlock.Measure(new Size(double.PositiveInfinity, constraint.Height));
        double desiredWidth = Math.Min(constraint.Width, _textBlock.DesiredSize.Width);
        double desiredHeight = Math.Max(FontSize * 1.3, _textBlock.DesiredSize.Height);

        _clipBorder.Measure(new Size(double.IsInfinity(constraint.Width) ? desiredWidth : constraint.Width, desiredHeight));

        return new Size(double.IsInfinity(constraint.Width) ? desiredWidth : constraint.Width, desiredHeight);
    }

    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        _clipBorder.Arrange(new Rect(arrangeBounds));
        _canvas.Height = arrangeBounds.Height;
        return arrangeBounds;
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarqueeText mt)
        {
            mt._textBlock.Text = (string)e.NewValue;
            mt.SyncProperties();
        }
    }

    private static void OnMarqueePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarqueeText mt)
        {
            mt.SyncProperties();
        }
    }

    private void SyncProperties()
    {
        _textBlock.FontFamily = FontFamily;
        _textBlock.FontSize = FontSize;
        _textBlock.FontWeight = FontWeight;
        _textBlock.FontStyle = FontStyle;
        _textBlock.FontStretch = FontStretch;
        _textBlock.Foreground = Foreground;
        _textBlock.Text = Text;

        MarqueeBehavior.SetSpeed(_textBlock, Speed);
        MarqueeBehavior.SetPauseDuration(_textBlock, PauseDuration);
        MarqueeBehavior.SetPaddingEnd(_textBlock, PaddingEnd);
        MarqueeBehavior.SetHoverPause(_textBlock, HoverPause);
        MarqueeBehavior.SetMode(_textBlock, Mode);
    }
}
