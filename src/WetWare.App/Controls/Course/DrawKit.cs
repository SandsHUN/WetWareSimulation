using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace WetWare.App.Controls.Course;

/// <summary>Shared pens, brushes and primitives for the course's custom-drawn visuals (glows, arrows, labels).</summary>
public static class DrawKit
{
    public static readonly Typeface Face = new("Segoe UI");
    public static readonly Typeface SemiBold = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    public static readonly Typeface MathFace = new(new FontFamily("Cambria Math"), FontStyles.Italic, FontWeights.Normal, FontStretches.Normal);

    public static readonly Color Text = Color.FromRgb(0xE8, 0xED, 0xF8);
    public static readonly Color Muted = Color.FromRgb(0x97, 0xA3, 0xC2);
    public static readonly Color Faint = Color.FromRgb(0x5F, 0x6C, 0x8E);
    public static readonly Color Grid = Color.FromRgb(0x1B, 0x27, 0x42);
    public static readonly Color Axis = Color.FromRgb(0x2C, 0x3D, 0x63);
    public static readonly Color Panel = Color.FromRgb(0x0A, 0x10, 0x20);
    public static readonly Color Accent = Color.FromRgb(0x22, 0xD3, 0xEE);
    public static readonly Color Accent2 = Color.FromRgb(0xA7, 0x8B, 0xFA);
    public static readonly Color Pink = Color.FromRgb(0xF4, 0x72, 0xB6);
    public static readonly Color Excitatory = Color.FromRgb(0xFB, 0x71, 0x85);
    public static readonly Color Inhibitory = Color.FromRgb(0x38, 0xBD, 0xF8);
    public static readonly Color Success = Color.FromRgb(0x34, 0xD3, 0x99);
    public static readonly Color Warning = Color.FromRgb(0xFB, 0xBF, 0x24);
    public static readonly Color Danger = Color.FromRgb(0xF4, 0x3F, 0x5E);

    /// <summary>Distinct colours for clusters / prototypes / mixture components.</summary>
    public static readonly Color[] Palette =
    [
        Accent, Pink, Warning, Success, Accent2, Excitatory,
        Color.FromRgb(0x60, 0xA5, 0xFA), Color.FromRgb(0xF9, 0x73, 0x16), Color.FromRgb(0x2D, 0xD4, 0xBF),
        Color.FromRgb(0xE8, 0x79, 0xF9), Color.FromRgb(0xA3, 0xE6, 0x35), Color.FromRgb(0xFD, 0xA4, 0xAF),
    ];

    private static readonly Dictionary<(Color, byte), SolidColorBrush> BrushCache = new();

    public static SolidColorBrush Brush(Color c, double alpha = 1)
    {
        byte a = (byte)System.Math.Clamp(alpha * 255, 0, 255);
        if (BrushCache.TryGetValue((c, a), out var brush)) return brush;
        brush = new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B));
        brush.Freeze();
        BrushCache[(c, a)] = brush;
        return brush;
    }

    public static Pen Pen(Color c, double thickness, double alpha = 1, bool dashed = false)
    {
        var pen = new Pen(Brush(c, alpha), thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        if (dashed) pen.DashStyle = new DashStyle([4, 3], 0);
        pen.Freeze();
        return pen;
    }

    public static Color Lerp(Color a, Color b, double t)
    {
        t = System.Math.Clamp(t, 0, 1);
        return Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
    }

    public static FormattedText Label(string text, double size, Color color, double dpi, Typeface? face = null) =>
        new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face ?? Face, size, Brush(color), dpi);

    public static void DrawText(DrawingContext dc, string text, Point at, double size, Color color, double dpi,
        HorizontalAlignment align = HorizontalAlignment.Left, Typeface? face = null)
    {
        var ft = Label(text, size, color, dpi, face);
        double x = align switch
        {
            HorizontalAlignment.Center => at.X - ft.Width / 2,
            HorizontalAlignment.Right => at.X - ft.Width,
            _ => at.X,
        };
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }

    /// <summary>Filled dot with a soft halo – the lab's "glowing" marker.</summary>
    public static void Glow(DrawingContext dc, Point p, double radius, Color c, double intensity = 1)
    {
        var halo = new RadialGradientBrush(Color.FromArgb((byte)(110 * System.Math.Clamp(intensity, 0, 1)), c.R, c.G, c.B), Color.FromArgb(0, c.R, c.G, c.B));
        halo.Freeze();
        dc.DrawEllipse(halo, null, p, radius * 2.6, radius * 2.6);
        dc.DrawEllipse(Brush(c), null, p, radius, radius);
    }

    public static void Arrow(DrawingContext dc, Point from, Point to, Pen pen, double head = 9)
    {
        dc.DrawLine(pen, from, to);
        var d = to - from;
        if (d.Length < 1e-6) return;
        d.Normalize();
        var n = new Vector(-d.Y, d.X);
        var a = to - d * head + n * head * 0.5;
        var b = to - d * head - n * head * 0.5;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(to, true, true);
            ctx.LineTo(a, false, false);
            ctx.LineTo(b, false, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(pen.Brush, null, geometry);
    }

    public static StreamGeometry Polyline(IReadOnlyList<Point> points, bool closed = false)
    {
        var geometry = new StreamGeometry();
        if (points.Count == 0) return geometry;
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], closed, closed);
            for (int i = 1; i < points.Count; i++) ctx.LineTo(points[i], true, true);
        }
        geometry.Freeze();
        return geometry;
    }
}
