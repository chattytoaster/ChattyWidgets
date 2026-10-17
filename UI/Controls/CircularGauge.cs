using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Size = System.Windows.Size;
using Pen = System.Windows.Media.Pen;
using FontFamily = System.Windows.Media.FontFamily;

namespace ChattyWidgets.UI.Controls;

public class CircularGauge : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(CircularGauge),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(CircularGauge),
            new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(CircularGauge),
            new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ProgressBrushProperty =
        DependencyProperty.Register(nameof(ProgressBrush), typeof(Brush), typeof(CircularGauge),
            new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(56, 189, 248)), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty =
        DependencyProperty.Register(nameof(TrackBrush), typeof(Brush), typeof(CircularGauge),
            new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(CircularGauge),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(CircularGauge),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(CircularGauge),
            new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public Brush ProgressBrush
    {
        get => (Brush)GetValue(ProgressBrushProperty);
        set => SetValue(ProgressBrushProperty, value);
    }

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Label
    {
        get => (string?)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double s = 50;
        if (!double.IsInfinity(availableSize.Width) && availableSize.Width > 0)
            s = Math.Min(s, availableSize.Width);
        if (!double.IsInfinity(availableSize.Height) && availableSize.Height > 0)
            s = Math.Min(s, availableSize.Height);
        return new Size(s, s);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth;
        double h = ActualHeight;
        if (w < 4 || h < 4) return;

        double size = Math.Min(w, h);
        double stroke = Math.Max(2, StrokeThickness);
        double radius = (size - stroke) / 2.0;
        Point center = new Point(w / 2.0, h / 2.0);

        // 1. Draw background track circle
        var trackPen = new Pen(TrackBrush, stroke)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        trackPen.Freeze();
        dc.DrawEllipse(null, trackPen, center, radius, radius);

        // 2. Draw progress arc
        double max = Maximum > 0 ? Maximum : 100.0;
        double ratio = Math.Clamp(Value / max, 0.0, 1.0);
        double sweepAngle = ratio * 360.0;

        if (sweepAngle > 0.5)
        {
            var progressPen = new Pen(ProgressBrush, stroke)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            progressPen.Freeze();

            if (sweepAngle >= 359.5)
            {
                dc.DrawEllipse(null, progressPen, center, radius, radius);
            }
            else
            {
                double startAngleRad = -Math.PI / 2.0;
                double endAngleRad = startAngleRad + (sweepAngle * Math.PI / 180.0);

                Point startPoint = new Point(center.X + radius * Math.Cos(startAngleRad), center.Y + radius * Math.Sin(startAngleRad));
                Point endPoint = new Point(center.X + radius * Math.Cos(endAngleRad), center.Y + radius * Math.Sin(endAngleRad));

                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    ctx.BeginFigure(startPoint, false, false);
                    ctx.ArcTo(endPoint, new Size(radius, radius), 0, sweepAngle > 180.0, SweepDirection.Clockwise, true, false);
                }
                geometry.Freeze();
                dc.DrawGeometry(null, progressPen, geometry);
            }
        }

        // 3. Draw center text
        string displayText = !string.IsNullOrEmpty(Text) ? Text : $"{Math.Round(Value)}%";
        double fontSize = Math.Max(10, size * 0.26);

        var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var formattedText = new FormattedText(
            displayText,
            CultureInfo.InvariantCulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Foreground ?? Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        Point textPos = new Point(center.X - formattedText.Width / 2.0, center.Y - formattedText.Height / 2.0);
        dc.DrawText(formattedText, textPos);
    }
}
