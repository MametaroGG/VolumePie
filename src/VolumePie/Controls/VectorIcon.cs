using System;
using System.Windows;
using System.Windows.Media;
using Control = System.Windows.Controls.Control;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;

namespace VolumePie.Controls;

public enum VectorIconKind
{
    Speaker,
    Headphones,
    Microphone,
    Brightness,
    AppsGrid,
    SystemSounds,
    ChevronDown,
    ChevronUp,
    Checkmark,
    BackArrow,
    Globe
}

public class VectorIcon : Control
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind),
        typeof(VectorIconKind),
        typeof(VectorIcon),
        new FrameworkPropertyMetadata(VectorIconKind.Speaker, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size),
        typeof(double),
        typeof(VectorIcon),
        new FrameworkPropertyMetadata(16.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty IsMutedProperty = DependencyProperty.Register(
        nameof(IsMuted),
        typeof(bool),
        typeof(VectorIcon),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public VectorIconKind Kind
    {
        get => (VectorIconKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public bool IsMuted
    {
        get => (bool)GetValue(IsMutedProperty);
        set => SetValue(IsMutedProperty, value);
    }

    static VectorIcon()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(VectorIcon), new FrameworkPropertyMetadata(typeof(VectorIcon)));
        IsHitTestVisibleProperty.OverrideMetadata(typeof(VectorIcon), new FrameworkPropertyMetadata(false));
    }

    protected override Size MeasureOverride(Size constraint)
    {
        double s = Size > 0 ? Size : 16.0;
        return new Size(s, s);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double s = Size > 0 ? Size : 16.0;
        var center = new Point(RenderSize.Width / 2.0, RenderSize.Height / 2.0);
        var brush = Foreground ?? Brushes.White;

        switch (Kind)
        {
            case VectorIconKind.Speaker:
                DrawSpeaker(dc, center, s, brush, IsMuted);
                break;
            case VectorIconKind.Headphones:
                DrawHeadphones(dc, center, s, brush);
                break;
            case VectorIconKind.Microphone:
                DrawMicrophone(dc, center, s, brush, IsMuted);
                break;
            case VectorIconKind.Brightness:
                DrawBrightness(dc, center, s, brush);
                break;
            case VectorIconKind.AppsGrid:
                DrawAppsGrid(dc, center, s, brush);
                break;
            case VectorIconKind.SystemSounds:
                DrawSystemSounds(dc, center, s, brush, IsMuted);
                break;
            case VectorIconKind.ChevronDown:
                DrawChevronDown(dc, center, s, brush);
                break;
            case VectorIconKind.ChevronUp:
                DrawChevronUp(dc, center, s, brush);
                break;
            case VectorIconKind.Checkmark:
                DrawCheckmark(dc, center, s, brush);
                break;
            case VectorIconKind.BackArrow:
                DrawBackArrow(dc, center, s, brush);
                break;
            case VectorIconKind.Globe:
                DrawGlobe(dc, center, s, brush);
                break;
        }
    }

    public static void DrawSpeaker(DrawingContext dc, Point center, double size, Brush brush, bool isMuted)
    {
        double s = size / 24.0;
        var coneGeo = new StreamGeometry();
        using (var ctx = coneGeo.Open())
        {
            ctx.BeginFigure(new Point(center.X - 6.2 * s, center.Y - 2.5 * s), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(center.X - 2.6 * s, center.Y - 2.5 * s), isStroked: false, isSmoothJoin: false);
            ctx.LineTo(new Point(center.X + 1.6 * s, center.Y - 6.2 * s), isStroked: false, isSmoothJoin: false);
            ctx.LineTo(new Point(center.X + 1.6 * s, center.Y + 6.2 * s), isStroked: false, isSmoothJoin: false);
            ctx.LineTo(new Point(center.X - 2.6 * s, center.Y + 2.5 * s), isStroked: false, isSmoothJoin: false);
            ctx.LineTo(new Point(center.X - 6.2 * s, center.Y + 2.5 * s), isStroked: false, isSmoothJoin: false);
        }
        coneGeo.Freeze();
        dc.DrawGeometry(brush, null, coneGeo);

        var pen = new Pen(brush, Math.Max(1.2, 1.6 * s))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };

        if (isMuted)
        {
            var mutePen = new Pen(new SolidColorBrush(Color.FromRgb(248, 113, 113)), Math.Max(1.3, 1.8 * s))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            dc.DrawLine(mutePen, new Point(center.X + 3.2 * s, center.Y - 3.5 * s), new Point(center.X + 8.5 * s, center.Y + 3.5 * s));
            dc.DrawLine(mutePen, new Point(center.X + 8.5 * s, center.Y - 3.5 * s), new Point(center.X + 3.2 * s, center.Y + 3.5 * s));
        }
        else
        {
            var wave1 = CreateArcStrokeGeometry(new Point(center.X - 1.8 * s, center.Y), 6.4 * s, -42, 42);
            var wave2 = CreateArcStrokeGeometry(new Point(center.X - 1.8 * s, center.Y), 10.2 * s, -45, 45);
            dc.DrawGeometry(null, pen, wave1);
            dc.DrawGeometry(null, pen, wave2);
        }
    }

    public static void DrawHeadphones(DrawingContext dc, Point center, double size, Brush brush)
    {
        double s = size / 24.0;
        var pen = new Pen(brush, Math.Max(1.3, 1.8 * s))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };

        // Smooth headband arc over the top (from left ear cup to right ear cup)
        var headband = CreateArcStrokeGeometry(new Point(center.X, center.Y + 0.5 * s), 7.8 * s, 195, 345);
        dc.DrawGeometry(null, pen, headband);

        // Left ear cup pill
        var leftCup = new Rect(center.X - 8.6 * s, center.Y - 1.0 * s, 3.2 * s, 7.6 * s);
        dc.DrawRoundedRectangle(brush, null, leftCup, 1.6 * s, 1.6 * s);

        // Right ear cup pill
        var rightCup = new Rect(center.X + 5.4 * s, center.Y - 1.0 * s, 3.2 * s, 7.6 * s);
        dc.DrawRoundedRectangle(brush, null, rightCup, 1.6 * s, 1.6 * s);
    }

    public static void DrawMicrophone(DrawingContext dc, Point center, double size, Brush brush, bool isMuted)
    {
        double s = size / 24.0;
        var pillRect = new Rect(center.X - 3.4 * s, center.Y - 7.5 * s, 6.8 * s, 10.5 * s);
        dc.DrawRoundedRectangle(brush, null, pillRect, 3.4 * s, 3.4 * s);

        var pen = new Pen(brush, Math.Max(1.2, 1.6 * s))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        var cradleArc = CreateArcStrokeGeometry(new Point(center.X, center.Y - 2.0 * s), 6.2 * s, 10, 170);
        dc.DrawGeometry(null, pen, cradleArc);
        dc.DrawLine(pen, new Point(center.X, center.Y + 4.2 * s), new Point(center.X, center.Y + 8.2 * s));
        dc.DrawLine(pen, new Point(center.X - 4.5 * s, center.Y + 8.2 * s), new Point(center.X + 4.5 * s, center.Y + 8.2 * s));

        if (isMuted)
        {
            var mutePen = new Pen(new SolidColorBrush(Color.FromRgb(248, 113, 113)), Math.Max(1.3, 1.8 * s))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            dc.DrawLine(mutePen, new Point(center.X - 6.5 * s, center.Y - 6.5 * s), new Point(center.X + 6.5 * s, center.Y + 6.5 * s));
        }
    }

    public static void DrawBrightness(DrawingContext dc, Point center, double size, Brush brush)
    {
        double r = size * 0.22;
        dc.DrawEllipse(brush, null, center, r, r);
        var pen = new Pen(brush, Math.Max(1.2, size * 0.08))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        for (int a = 0; a < 360; a += 45)
        {
            double rad = a * Math.PI / 180.0;
            var p1 = new Point(center.X + Math.Cos(rad) * (size * 0.36), center.Y + Math.Sin(rad) * (size * 0.36));
            var p2 = new Point(center.X + Math.Cos(rad) * (size * 0.49), center.Y + Math.Sin(rad) * (size * 0.49));
            dc.DrawLine(pen, p1, p2);
        }
    }

    public static void DrawAppsGrid(DrawingContext dc, Point center, double size, Brush brush)
    {
        double s = size / 24.0;
        double boxSize = 7.0 * s;
        double radius = 2.0 * s;
        double gap = 3.0 * s;

        dc.DrawRoundedRectangle(brush, null, new Rect(center.X - boxSize - gap / 2.0, center.Y - boxSize - gap / 2.0, boxSize, boxSize), radius, radius);
        dc.DrawRoundedRectangle(brush, null, new Rect(center.X + gap / 2.0, center.Y - boxSize - gap / 2.0, boxSize, boxSize), radius, radius);
        dc.DrawRoundedRectangle(brush, null, new Rect(center.X - boxSize - gap / 2.0, center.Y + gap / 2.0, boxSize, boxSize), radius, radius);
        dc.DrawRoundedRectangle(brush, null, new Rect(center.X + gap / 2.0, center.Y + gap / 2.0, boxSize, boxSize), radius, radius);
    }

    public static void DrawSystemSounds(DrawingContext dc, Point center, double size, Brush brush, bool isMuted)
    {
        double s = size / 24.0;

        var bellGeo = new StreamGeometry();
        using (var ctx = bellGeo.Open())
        {
            ctx.BeginFigure(new Point(center.X - 1.2 * s, center.Y - 8.5 * s), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(center.X + 1.2 * s, center.Y - 8.5 * s), isStroked: false, isSmoothJoin: false);
            ctx.LineTo(new Point(center.X + 1.2 * s, center.Y - 6.5 * s), isStroked: false, isSmoothJoin: false);
            ctx.BezierTo(
                new Point(center.X + 6.0 * s, center.Y - 4.0 * s),
                new Point(center.X + 7.5 * s, center.Y + 2.0 * s),
                new Point(center.X + 8.5 * s, center.Y + 4.5 * s),
                isStroked: false, isSmoothJoin: true);
            ctx.LineTo(new Point(center.X - 8.5 * s, center.Y + 4.5 * s), isStroked: false, isSmoothJoin: false);
            ctx.BezierTo(
                new Point(center.X - 7.5 * s, center.Y + 2.0 * s),
                new Point(center.X - 6.0 * s, center.Y - 4.0 * s),
                new Point(center.X - 1.2 * s, center.Y - 6.5 * s),
                isStroked: false, isSmoothJoin: true);
        }
        bellGeo.Freeze();
        dc.DrawGeometry(brush, null, bellGeo);

        dc.DrawEllipse(brush, null, new Point(center.X, center.Y + 6.5 * s), 2.2 * s, 2.2 * s);

        if (isMuted)
        {
            var mutePen = new Pen(new SolidColorBrush(Color.FromRgb(248, 113, 113)), Math.Max(1.3, 1.8 * s))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            dc.DrawLine(mutePen, new Point(center.X - 7.5 * s, center.Y - 7.5 * s), new Point(center.X + 7.5 * s, center.Y + 7.5 * s));
        }
    }

    public static void DrawChevronDown(DrawingContext dc, Point center, double size, Brush brush)
    {
        double s = size / 12.0;
        var pen = new Pen(brush, Math.Max(1.2, 1.6 * s))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.DrawLine(pen, new Point(center.X - 3.8 * s, center.Y - 1.8 * s), new Point(center.X, center.Y + 2.2 * s));
        dc.DrawLine(pen, new Point(center.X, center.Y + 2.2 * s), new Point(center.X + 3.8 * s, center.Y - 1.8 * s));
    }

    public static void DrawChevronUp(DrawingContext dc, Point center, double size, Brush brush)
    {
        double s = size / 12.0;
        var pen = new Pen(brush, Math.Max(1.2, 1.6 * s))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.DrawLine(pen, new Point(center.X - 3.8 * s, center.Y + 1.8 * s), new Point(center.X, center.Y - 2.2 * s));
        dc.DrawLine(pen, new Point(center.X, center.Y - 2.2 * s), new Point(center.X + 3.8 * s, center.Y + 1.8 * s));
    }

    public static void DrawCheckmark(DrawingContext dc, Point center, double size, Brush brush)
    {
        double s = size / 14.0;
        var pen = new Pen(brush, Math.Max(1.3, 1.8 * s))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.DrawLine(pen, new Point(center.X - 4.5 * s, center.Y - 0.2 * s), new Point(center.X - 1.4 * s, center.Y + 3.2 * s));
        dc.DrawLine(pen, new Point(center.X - 1.4 * s, center.Y + 3.2 * s), new Point(center.X + 4.5 * s, center.Y - 3.6 * s));
    }

    public static void DrawBackArrow(DrawingContext dc, Point center, double size, Brush brush)
    {
        double s = size / 14.0;
        var pen = new Pen(brush, Math.Max(1.3, 1.7 * s))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.DrawLine(pen, new Point(center.X + 4.5 * s, center.Y), new Point(center.X - 4.2 * s, center.Y));
        dc.DrawLine(pen, new Point(center.X - 4.2 * s, center.Y), new Point(center.X - 0.5 * s, center.Y - 3.8 * s));
        dc.DrawLine(pen, new Point(center.X - 4.2 * s, center.Y), new Point(center.X - 0.5 * s, center.Y + 3.8 * s));
    }

    public static void DrawGlobe(DrawingContext dc, Point center, double size, Brush brush)
    {
        double s = size / 24.0;
        double r = 8.5 * s;
        var pen = new Pen(brush, Math.Max(1.2, 1.5 * s))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };

        // Outer sphere circle
        dc.DrawEllipse(null, pen, center, r, r);

        // Equator line
        dc.DrawLine(pen, new Point(center.X - r, center.Y), new Point(center.X + r, center.Y));

        // Prime meridian line
        dc.DrawLine(pen, new Point(center.X, center.Y - r), new Point(center.X, center.Y + r));

        // Longitude curved ellipse
        dc.DrawEllipse(null, pen, center, r * 0.52, r);
    }

    private static StreamGeometry CreateArcStrokeGeometry(Point center, double radius, double startDeg, double endDeg)
    {
        double startRad = startDeg * Math.PI / 180.0;
        double endRad = endDeg * Math.PI / 180.0;

        var startPt = new Point(center.X + radius * Math.Cos(startRad), center.Y + radius * Math.Sin(startRad));
        var endPt = new Point(center.X + radius * Math.Cos(endRad), center.Y + radius * Math.Sin(endRad));

        double sweep = endDeg - startDeg;
        bool isLarge = Math.Abs(sweep) >= 180.0;

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(startPt, isFilled: false, isClosed: false);
            ctx.ArcTo(endPt, new Size(radius, radius), 0, isLarge, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true);
        }
        geo.Freeze();
        return geo;
    }
}
