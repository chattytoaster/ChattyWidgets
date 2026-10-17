#nullable enable
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FontFamily = System.Windows.Media.FontFamily;

namespace ChattyWidgets.UI.Controls;

public enum MarqueeMode
{
    PingPong,
    Continuous
}

/// <summary>
/// Universal lightweight attached behavior that provides smooth, hardware-accelerated
/// horizontal text marquee when content overflows available container width.
/// Never alters or mutates the visual tree to guarantee 100% layout and text stability.
/// </summary>
public static class MarqueeBehavior
{
    public static readonly DependencyProperty EnableMarqueeProperty =
        DependencyProperty.RegisterAttached(
            "EnableMarquee",
            typeof(bool),
            typeof(MarqueeBehavior),
            new FrameworkPropertyMetadata(false, OnEnableMarqueeChanged));

    public static readonly DependencyProperty SpeedProperty =
        DependencyProperty.RegisterAttached(
            "Speed",
            typeof(double),
            typeof(MarqueeBehavior),
            new FrameworkPropertyMetadata(35.0, OnSettingsChanged));

    public static readonly DependencyProperty PauseDurationProperty =
        DependencyProperty.RegisterAttached(
            "PauseDuration",
            typeof(double),
            typeof(MarqueeBehavior),
            new FrameworkPropertyMetadata(1.5, OnSettingsChanged));

    public static readonly DependencyProperty PaddingEndProperty =
        DependencyProperty.RegisterAttached(
            "PaddingEnd",
            typeof(double),
            typeof(MarqueeBehavior),
            new FrameworkPropertyMetadata(20.0, OnSettingsChanged));

    public static readonly DependencyProperty HoverPauseProperty =
        DependencyProperty.RegisterAttached(
            "HoverPause",
            typeof(bool),
            typeof(MarqueeBehavior),
            new FrameworkPropertyMetadata(true));

    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.RegisterAttached(
            "Mode",
            typeof(MarqueeMode),
            typeof(MarqueeBehavior),
            new FrameworkPropertyMetadata(MarqueeMode.PingPong, OnSettingsChanged));

    private static readonly DependencyPropertyKey IsAnimatingPropertyKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "IsAnimating",
            typeof(bool),
            typeof(MarqueeBehavior),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsAnimatingProperty =
        IsAnimatingPropertyKey.DependencyProperty;

    private static readonly DependencyProperty ControllerProperty =
        DependencyProperty.RegisterAttached(
            "Controller",
            typeof(MarqueeController),
            typeof(MarqueeBehavior),
            new PropertyMetadata(null));

    public static bool GetEnableMarquee(DependencyObject obj) => (bool)obj.GetValue(EnableMarqueeProperty);
    public static void SetEnableMarquee(DependencyObject obj, bool value) => obj.SetValue(EnableMarqueeProperty, value);

    public static double GetSpeed(DependencyObject obj) => (double)obj.GetValue(SpeedProperty);
    public static void SetSpeed(DependencyObject obj, double value) => obj.SetValue(SpeedProperty, value);

    public static double GetPauseDuration(DependencyObject obj) => (double)obj.GetValue(PauseDurationProperty);
    public static void SetPauseDuration(DependencyObject obj, double value) => obj.SetValue(PauseDurationProperty, value);

    public static double GetPaddingEnd(DependencyObject obj) => (double)obj.GetValue(PaddingEndProperty);
    public static void SetPaddingEnd(DependencyObject obj, double value) => obj.SetValue(PaddingEndProperty, value);

    public static bool GetHoverPause(DependencyObject obj) => (bool)obj.GetValue(HoverPauseProperty);
    public static void SetHoverPause(DependencyObject obj, bool value) => obj.SetValue(HoverPauseProperty, value);

    public static MarqueeMode GetMode(DependencyObject obj) => (MarqueeMode)obj.GetValue(ModeProperty);
    public static void SetMode(DependencyObject obj, MarqueeMode value) => obj.SetValue(ModeProperty, value);

    public static bool GetIsAnimating(DependencyObject obj) => (bool)obj.GetValue(IsAnimatingProperty);
    private static void SetIsAnimating(DependencyObject obj, bool value) => obj.SetValue(IsAnimatingPropertyKey, value);

    private static void OnEnableMarqueeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb) return;

        bool enable = (bool)e.NewValue;
        var controller = (MarqueeController?)tb.GetValue(ControllerProperty);

        if (enable)
        {
            if (controller == null)
            {
                controller = new MarqueeController(tb);
                tb.SetValue(ControllerProperty, controller);
            }
            controller.Attach();
        }
        else
        {
            controller?.Detach();
            tb.ClearValue(ControllerProperty);
        }
    }

    private static void OnSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlock tb)
        {
            var controller = (MarqueeController?)tb.GetValue(ControllerProperty);
            controller?.UpdateAnimation();
        }
    }

    /// <summary>
    /// Safe no-op retained for API compatibility. Blanket recursive visual tree mutation
    /// is intentionally avoided to prevent UI collapse.
    /// </summary>
    public static void AutoEnableMarquee(DependencyObject root)
    {
        // Safe no-op
    }

    private class MarqueeController
    {
        private readonly TextBlock _tb;
        private Storyboard? _storyboard;
        private TranslateTransform? _transform;
        private DependencyPropertyDescriptor? _textPropDesc;
        private TextTrimming _originalTrimming = TextTrimming.CharacterEllipsis;
        private HorizontalAlignment _originalAlignment = HorizontalAlignment.Left;
        private bool _isHovered = false;

        public MarqueeController(TextBlock tb)
        {
            _tb = tb;
            _originalTrimming = tb.TextTrimming;
            _originalAlignment = tb.HorizontalAlignment;
        }

        public void Attach()
        {
            _tb.Loaded += OnLoaded;
            _tb.Unloaded += OnUnloaded;
            _tb.SizeChanged += OnSizeChanged;
            _tb.MouseEnter += OnMouseEnter;
            _tb.MouseLeave += OnMouseLeave;
            _tb.IsVisibleChanged += OnIsVisibleChanged;

            _textPropDesc = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
            _textPropDesc?.AddValueChanged(_tb, OnTextChanged);

            if (_tb.IsLoaded)
            {
                _tb.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, UpdateAnimation);
            }
        }

        public void Detach()
        {
            StopAnimation();

            _tb.Loaded -= OnLoaded;
            _tb.Unloaded -= OnUnloaded;
            _tb.SizeChanged -= OnSizeChanged;
            _tb.MouseEnter -= OnMouseEnter;
            _tb.MouseLeave -= OnMouseLeave;
            _tb.IsVisibleChanged -= OnIsVisibleChanged;

            if (_textPropDesc != null)
            {
                _textPropDesc.RemoveValueChanged(_tb, OnTextChanged);
                _textPropDesc = null;
            }

            if (_transform != null)
            {
                _transform.X = 0;
            }

            SetIsAnimating(_tb, false);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _originalTrimming = _tb.TextTrimming;
            _originalAlignment = _tb.HorizontalAlignment;
            _tb.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, UpdateAnimation);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopAnimation();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateAnimation();
        }

        private void OnTextChanged(object? sender, EventArgs e)
        {
            _tb.Dispatcher.BeginInvoke(DispatcherPriority.Render, UpdateAnimation);
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
                UpdateAnimation();
            else
                StopAnimation();
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            _isHovered = true;
            if (GetHoverPause(_tb) && _storyboard != null)
            {
                try { _storyboard.Pause(_tb); } catch { }
            }
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            _isHovered = false;
            if (GetHoverPause(_tb) && _storyboard != null)
            {
                try { _storyboard.Resume(_tb); } catch { }
            }
        }

        public void UpdateAnimation()
        {
            if (!_tb.IsVisible || string.IsNullOrEmpty(_tb.Text))
            {
                StopAnimation();
                return;
            }

            double containerWidth = 0;

            if (!double.IsNaN(_tb.MaxWidth) && _tb.MaxWidth > 0)
            {
                containerWidth = _tb.MaxWidth;
            }

            if (_tb.Parent is FrameworkElement p && p.ActualWidth > 0)
            {
                double parentAvailable = p.ActualWidth;
                if (p is Border b)
                {
                    parentAvailable -= (b.Padding.Left + b.Padding.Right + b.BorderThickness.Left + b.BorderThickness.Right);
                }
                if (containerWidth <= 0 || parentAvailable < containerWidth)
                {
                    containerWidth = parentAvailable;
                }
            }

            if (containerWidth <= 0 && _tb.ActualWidth > 0)
            {
                containerWidth = _tb.ActualWidth;
            }

            if (_tb.Margin.Left > 0 || _tb.Margin.Right > 0)
            {
                containerWidth = Math.Max(0, containerWidth - (_tb.Margin.Left + _tb.Margin.Right));
            }

            if (containerWidth <= 0) return;

            double textWidth = MeasureFormattedTextWidth(_tb);
            double paddingEnd = GetPaddingEnd(_tb);
            double overflow = textWidth - containerWidth;

            // Only animate if text actually overflows by more than 2.0px
            if (overflow <= 2.0)
            {
                StopAnimation();
                return;
            }

            StartAnimation(overflow + paddingEnd);
        }

        private static double MeasureFormattedTextWidth(TextBlock tb)
        {
            var typeface = new Typeface(
                tb.FontFamily ?? new FontFamily("Segoe UI"),
                tb.FontStyle,
                tb.FontWeight,
                tb.FontStretch);

            var dpi = VisualTreeHelper.GetDpi(tb);
            var ft = new FormattedText(
                tb.Text ?? string.Empty,
                CultureInfo.CurrentUICulture,
                tb.FlowDirection,
                typeface,
                tb.FontSize,
                Brushes.Black,
                dpi.PixelsPerDip);

            return ft.WidthIncludingTrailingWhitespace;
        }

        private void StartAnimation(double totalDistance)
        {
            StopAnimation();

            // Setup transform safely
            if (_tb.RenderTransform is TranslateTransform existingTt)
            {
                _transform = existingTt;
            }
            else if (_tb.RenderTransform is TransformGroup tg)
            {
                TranslateTransform? found = null;
                foreach (var child in tg.Children)
                {
                    if (child is TranslateTransform tt)
                    {
                        found = tt;
                        break;
                    }
                }
                if (found == null)
                {
                    found = new TranslateTransform();
                    tg.Children.Add(found);
                }
                _transform = found;
            }
            else
            {
                _transform = new TranslateTransform();
                _tb.RenderTransform = _transform;
            }

            // Ensure parent and element clip bounds so moving text is contained
            if (_tb.Parent is UIElement pe)
            {
                pe.ClipToBounds = true;
            }
            _tb.ClipToBounds = true;

            // Render all characters without clipping ellipsis during scroll
            _tb.TextTrimming = TextTrimming.None;
            _tb.HorizontalAlignment = HorizontalAlignment.Left;

            double speed = Math.Max(5.0, GetSpeed(_tb));
            double pauseSec = Math.Max(0.2, GetPauseDuration(_tb));
            double scrollDuration = totalDistance / speed;

            var mode = GetMode(_tb);
            _storyboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };

            if (mode == MarqueeMode.PingPong)
            {
                var anim = new DoubleAnimationUsingKeyFrames();
                anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseSec))));
                anim.KeyFrames.Add(new LinearDoubleKeyFrame(-totalDistance, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseSec + scrollDuration))));
                anim.KeyFrames.Add(new LinearDoubleKeyFrame(-totalDistance, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseSec + scrollDuration + pauseSec))));
                anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseSec + 2 * scrollDuration + pauseSec))));

                Storyboard.SetTarget(anim, _tb);
                Storyboard.SetTargetProperty(anim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
                _storyboard.Children.Add(anim);
            }
            else // Continuous
            {
                var anim = new DoubleAnimationUsingKeyFrames();
                anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseSec))));
                anim.KeyFrames.Add(new LinearDoubleKeyFrame(-totalDistance, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseSec + scrollDuration))));
                anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseSec + scrollDuration))));

                Storyboard.SetTarget(anim, _tb);
                Storyboard.SetTargetProperty(anim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
                _storyboard.Children.Add(anim);
            }

            SetIsAnimating(_tb, true);
            _storyboard.Begin(_tb, isControllable: true);

            if (_isHovered && GetHoverPause(_tb))
            {
                try { _storyboard.Pause(_tb); } catch { }
            }
        }

        private void StopAnimation()
        {
            if (_storyboard != null)
            {
                try
                {
                    _storyboard.Stop(_tb);
                }
                catch { }
                _storyboard = null;
            }

            if (_transform != null)
            {
                _transform.X = 0;
            }

            _tb.TextTrimming = _originalTrimming;
            _tb.HorizontalAlignment = _originalAlignment;
            SetIsAnimating(_tb, false);
        }
    }
}
