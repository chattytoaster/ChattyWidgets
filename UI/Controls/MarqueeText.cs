#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Size = System.Windows.Size;
using FontFamily = System.Windows.Media.FontFamily;

namespace ChattyWidgets.UI.Controls;

/// <summary>
/// Hardware-accelerated Marquee Text control that directly uses DrawingContext with FormattedText.
/// Guarantees that 100% of overflowing text is rendered and smoothly scrolled,
/// strictly clipped to the available layout bounds without empty spaces or layout disruption.
/// </summary>
public class MarqueeText : FrameworkElement
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(MarqueeText),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnTextChanged));

    public static readonly DependencyProperty FontSizeProperty =
        DependencyProperty.Register(
            nameof(FontSize),
            typeof(double),
            typeof(MarqueeText),
            new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnFormattedChanged));

    public static readonly DependencyProperty FontWeightProperty =
        DependencyProperty.Register(
            nameof(FontWeight),
            typeof(FontWeight),
            typeof(MarqueeText),
            new FrameworkPropertyMetadata(FontWeights.Normal, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnFormattedChanged));

    public static readonly DependencyProperty FontFamilyProperty =
        DependencyProperty.Register(
            nameof(FontFamily),
            typeof(FontFamily),
            typeof(MarqueeText),
            new FrameworkPropertyMetadata(new FontFamily("Segoe UI"), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnFormattedChanged));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Brush),
            typeof(MarqueeText),
            new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SpeedProperty =
        DependencyProperty.Register(
            nameof(Speed),
            typeof(double),
            typeof(MarqueeText),
            new FrameworkPropertyMetadata(35.0, OnAnimationParamChanged));

    public static readonly DependencyProperty PauseDurationProperty =
        DependencyProperty.Register(
            nameof(PauseDuration),
            typeof(double),
            typeof(MarqueeText),
            new FrameworkPropertyMetadata(1.5, OnAnimationParamChanged));

    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.Register(
            nameof(Mode),
            typeof(MarqueeMode),
            typeof(MarqueeText),
            new FrameworkPropertyMetadata(MarqueeMode.PingPong, OnAnimationParamChanged));

    public static readonly DependencyProperty HoverPauseProperty =
        DependencyProperty.Register(
            nameof(HoverPause),
            typeof(bool),
            typeof(MarqueeText),
            new FrameworkPropertyMetadata(true));

    public static readonly DependencyProperty ScrollOffsetProperty =
        DependencyProperty.Register(
            nameof(ScrollOffset),
            typeof(double),
            typeof(MarqueeText),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextTrimmingProperty =
        DependencyProperty.Register(
            nameof(TextTrimming),
            typeof(TextTrimming),
            typeof(MarqueeText),
            new FrameworkPropertyMetadata(TextTrimming.CharacterEllipsis));

    public static readonly DependencyProperty TextAlignmentProperty =
        DependencyProperty.Register(
            nameof(TextAlignment),
            typeof(TextAlignment),
            typeof(MarqueeText),
            new FrameworkPropertyMetadata(TextAlignment.Left, FrameworkPropertyMetadataOptions.AffectsRender));

    public TextAlignment TextAlignment
    {
        get => (TextAlignment)GetValue(TextAlignmentProperty);
        set => SetValue(TextAlignmentProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontWeight FontWeight
    {
        get => (FontWeight)GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public FontFamily FontFamily
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
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

    public MarqueeMode Mode
    {
        get => (MarqueeMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public bool HoverPause
    {
        get => (bool)GetValue(HoverPauseProperty);
        set => SetValue(HoverPauseProperty, value);
    }

    public double ScrollOffset
    {
        get => (double)GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    public TextTrimming TextTrimming
    {
        get => (TextTrimming)GetValue(TextTrimmingProperty);
        set => SetValue(TextTrimmingProperty, value);
    }

    private FormattedText? _formattedText;
    private Storyboard? _storyboard;
    private bool _isHovered = false;

    public MarqueeText()
    {
        ClipToBounds = true;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarqueeText mt)
        {
            mt._formattedText = null;
            mt.UpdateAnimation();
        }
    }

    private static void OnFormattedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarqueeText mt)
        {
            mt._formattedText = null;
            mt.UpdateAnimation();
        }
    }

    private static void OnAnimationParamChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarqueeText mt)
        {
            mt.UpdateAnimation();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateAnimation();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopAnimation();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateAnimation();
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        _isHovered = true;
        if (HoverPause && _storyboard != null)
        {
            try { _storyboard.Pause(this); } catch { }
        }
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        _isHovered = false;
        if (HoverPause && _storyboard != null)
        {
            try { _storyboard.Resume(this); } catch { }
        }
    }

    private FormattedText? GetOrCreateFormattedText()
    {
        if (string.IsNullOrEmpty(Text)) return null;
        if (_formattedText != null) return _formattedText;

        var typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeight, FontStretches.Normal);
        var dpi = VisualTreeHelper.GetDpi(this);

        _formattedText = new FormattedText(
            Text,
            CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            Math.Max(8.0, FontSize),
            Brushes.White,
            dpi.PixelsPerDip);

        return _formattedText;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var ft = GetOrCreateFormattedText();
        if (ft == null) return new Size(0, 0);

        double w = ft.WidthIncludingTrailingWhitespace;
        double h = ft.Height;

        if (!double.IsPositiveInfinity(availableSize.Width))
        {
            w = Math.Min(w, availableSize.Width);
        }

        return new Size(w, h);
    }

    public void UpdateAnimation()
    {
        if (!IsLoaded || ActualWidth <= 0 || string.IsNullOrEmpty(Text))
        {
            StopAnimation();
            return;
        }

        var ft = GetOrCreateFormattedText();
        if (ft == null)
        {
            StopAnimation();
            return;
        }

        double textWidth = ft.WidthIncludingTrailingWhitespace;
        double availableWidth = ActualWidth;
        double overflow = textWidth - availableWidth;

        // If text comfortably fits, no scrolling needed
        if (overflow <= 2.0)
        {
            StopAnimation();
            ScrollOffset = 0;
            return;
        }

        StartAnimation(overflow, textWidth);
    }

    private void StartAnimation(double overflow, double textWidth)
    {
        StopAnimation();

        double totalDistance = overflow + 24.0;
        double speed = Math.Max(10.0, Speed);
        double pauseSec = Math.Max(0.2, PauseDuration);
        double scrollDuration = totalDistance / speed;

        _storyboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };

        if (Mode == MarqueeMode.PingPong)
        {
            var anim = new DoubleAnimationUsingKeyFrames();
            anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseSec))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(totalDistance, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseSec + scrollDuration))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(totalDistance, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseSec + scrollDuration + pauseSec))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseSec + 2 * scrollDuration + pauseSec))));

            Storyboard.SetTarget(anim, this);
            Storyboard.SetTargetProperty(anim, new PropertyPath(ScrollOffsetProperty));
            _storyboard.Children.Add(anim);
        }
        else // Continuous
        {
            double loopDistance = textWidth + 36.0;
            double loopDuration = loopDistance / speed;

            var anim = new DoubleAnimationUsingKeyFrames();
            anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseSec))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(loopDistance, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseSec + loopDuration))));
            anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseSec + loopDuration))));

            Storyboard.SetTarget(anim, this);
            Storyboard.SetTargetProperty(anim, new PropertyPath(ScrollOffsetProperty));
            _storyboard.Children.Add(anim);
        }

        _storyboard.Begin(this, isControllable: true);

        if (_isHovered && HoverPause)
        {
            try { _storyboard.Pause(this); } catch { }
        }
    }

    private void StopAnimation()
    {
        if (_storyboard != null)
        {
            try { _storyboard.Stop(this); } catch { }
            _storyboard = null;
        }
        ScrollOffset = 0;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(Text) || ActualWidth <= 0 || ActualHeight <= 0) return;

        var ft = GetOrCreateFormattedText();
        if (ft == null) return;

        double availableWidth = ActualWidth;
        double textWidth = ft.WidthIncludingTrailingWhitespace;

        // Strictly clip all rendering to this control's arranged bounding box
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, availableWidth, ActualHeight)));

        double y = Math.Max(0, (ActualHeight - ft.Height) / 2);

        if (textWidth <= availableWidth)
        {
            double x = 0;
            if (TextAlignment == TextAlignment.Center)
                x = Math.Max(0, (availableWidth - textWidth) / 2.0);
            else if (TextAlignment == TextAlignment.Right)
                x = Math.Max(0, availableWidth - textWidth);

            // Fits entirely: render normally with Foreground
            ft.SetForegroundBrush(Foreground ?? Brushes.White);
            dc.DrawText(ft, new Point(x, y));
        }
        else
        {
            ft.SetForegroundBrush(Foreground ?? Brushes.White);
            dc.DrawText(ft, new Point(-ScrollOffset, y));

            if (Mode == MarqueeMode.Continuous)
            {
                double secondX = -ScrollOffset + textWidth + 36.0;
                if (secondX < availableWidth)
                {
                    dc.DrawText(ft, new Point(secondX, y));
                }
            }
        }

        dc.Pop();
    }
}
