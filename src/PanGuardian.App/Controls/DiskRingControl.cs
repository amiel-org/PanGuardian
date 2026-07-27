using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace PanGuardian.App.Controls;

public sealed class DiskRingControl : FrameworkElement
{
    public static readonly DependencyProperty UsedPercentProperty =
        DependencyProperty.Register(
            nameof(UsedPercent),
            typeof(double),
            typeof(DiskRingControl),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PrimaryTextProperty =
        DependencyProperty.Register(
            nameof(PrimaryText),
            typeof(string),
            typeof(DiskRingControl),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SecondaryTextProperty =
        DependencyProperty.Register(
            nameof(SecondaryText),
            typeof(string),
            typeof(DiskRingControl),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public double UsedPercent
    {
        get => (double)GetValue(UsedPercentProperty);
        set => SetValue(UsedPercentProperty, value);
    }

    public string PrimaryText
    {
        get => (string)GetValue(PrimaryTextProperty);
        set => SetValue(PrimaryTextProperty, value);
    }

    public string SecondaryText
    {
        get => (string)GetValue(SecondaryTextProperty);
        set => SetValue(SecondaryTextProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var size = Math.Min(width, height);
        var center = new Point(width / 2, height / 2);
        var radius = size / 2 - 18;
        var used = Math.Clamp(UsedPercent, 0, 100);

        var trackPen = new Pen(new SolidColorBrush(Color.FromRgb(231, 237, 245)), 15)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.DrawEllipse(null, trackPen, center, radius, radius);

        var usedColor = used >= 92
            ? Color.FromRgb(209, 52, 56)
            : Color.FromRgb(0, 120, 212);

        DrawArc(dc, center, radius, -90, 360 * used / 100, usedColor, 15, 1);
        DrawArc(dc, center, radius - 26, -90, 360, Color.FromArgb(70, 0, 120, 212), 1.1, 0.55);
        DrawArc(dc, center, radius + 18, -74, 108, Color.FromArgb(80, 0, 166, 166), 2, 0.45);

        for (var i = 0; i < 48; i++)
        {
            var angle = -90 + i * 7.5;
            var inner = radius - 9;
            var outer = radius + 4;
            var pen = i / 48d * 100 <= used
                ? new Pen(new SolidColorBrush(Color.FromArgb(105, usedColor.R, usedColor.G, usedColor.B)), 1)
                : new Pen(new SolidColorBrush(Color.FromArgb(90, 203, 213, 225)), 1);
            dc.DrawLine(pen, PointOnCircle(center, inner, angle), PointOnCircle(center, outer, angle));
        }

        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(247, 251, 255)), null, center, radius - 51, radius - 51);
        dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromRgb(220, 234, 247)), 1), center, radius - 52, radius - 52);

        var primary = new FormattedText(
            PrimaryText,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            28,
            new SolidColorBrush(Color.FromRgb(22, 32, 51)),
            dpi);
        dc.DrawText(primary, new Point(center.X - primary.Width / 2, center.Y - 28));

        var secondary = new FormattedText(
            SecondaryText,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            13,
            new SolidColorBrush(Color.FromRgb(82, 96, 113)),
            dpi);
        dc.DrawText(secondary, new Point(center.X - secondary.Width / 2, center.Y + 12));
    }

    private static void DrawArc(DrawingContext dc, Point center, double radius, double startAngle, double sweepAngle, Color color, double thickness, double opacity)
    {
        if (sweepAngle <= 0)
        {
            return;
        }

        var endAngle = startAngle + Math.Min(sweepAngle, 359.9);
        var start = PointOnCircle(center, radius, startAngle);
        var end = PointOnCircle(center, radius, endAngle);
        var geometry = new StreamGeometry();

        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.ArcTo(end, new Size(radius, radius), 0, sweepAngle > 180, SweepDirection.Clockwise, true, false);
        }

        geometry.Freeze();
        var brush = new SolidColorBrush(color) { Opacity = opacity };
        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.DrawGeometry(null, pen, geometry);
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180;
        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }
}
