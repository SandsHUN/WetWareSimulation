using System.Windows.Media;

namespace WetWare.App.Controls;

/// <summary>Colour scales used by the heatmaps and image views (all colours as 0xAARRGGBB).</summary>
public static class ColorMaps
{
    private static readonly (double T, Color C)[] WeightStops =
    [
        (0.00, Color.FromRgb(0x17, 0x25, 0x54)),
        (0.25, Color.FromRgb(0x25, 0x63, 0xEB)),
        (0.50, Color.FromRgb(0x7C, 0x3A, 0xED)),
        (0.75, Color.FromRgb(0xE1, 0x1D, 0x48)),
        (1.00, Color.FromRgb(0xFB, 0xBF, 0x24)),
    ];

    private static readonly (double T, Color C)[] DivergingStops =
    [
        (0.00, Color.FromRgb(0xF4, 0x72, 0xB6)),
        (0.25, Color.FromRgb(0x86, 0x19, 0x8F)),
        (0.50, Color.FromRgb(0x0B, 0x11, 0x20)),
        (0.75, Color.FromRgb(0x0E, 0x74, 0x90)),
        (1.00, Color.FromRgb(0x5E, 0xEA, 0xD4)),
    ];

    private static readonly (double T, Color C)[] OnStops =
    [
        (0.0, Color.FromRgb(0x05, 0x0A, 0x14)),
        (0.5, Color.FromRgb(0x05, 0x96, 0x69)),
        (1.0, Color.FromRgb(0xD1, 0xFA, 0xE5)),
    ];

    private static readonly (double T, Color C)[] OffStops =
    [
        (0.0, Color.FromRgb(0x05, 0x0A, 0x14)),
        (0.5, Color.FromRgb(0xBE, 0x18, 0x5D)),
        (1.0, Color.FromRgb(0xFC, 0xE7, 0xF3)),
    ];

    public static int Weight(double t) => Sample(WeightStops, t);

    public static int Diverging(double t) => Sample(DivergingStops, t);

    public static int On(double t) => Sample(OnStops, t);

    public static int Off(double t) => Sample(OffStops, t);

    public static int Gray(double t)
    {
        byte v = (byte)(Math.Clamp(t, 0, 1) * 255);
        return Pack(255, v, v, v);
    }

    /// <summary>Hue for orientation (0–180° wraps around the colour wheel) with brightness = strength.</summary>
    public static int Orientation(double degrees, double strength)
    {
        double hue = (degrees % 180) / 180.0 * 360.0;
        return Hsv(hue, 0.85, Math.Clamp(strength, 0, 1));
    }

    public static Color OrientationColor(double degrees) => ToColor(Orientation(degrees, 1));

    public static int Hsv(double h, double s, double v)
    {
        double c = v * s;
        double x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        double m = v - c;
        (double r, double g, double b) = h switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        return Pack(255, (byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }

    public static int Pack(byte a, byte r, byte g, byte b) => (a << 24) | (r << 16) | (g << 8) | b;

    public static int Pack(Color c) => Pack(c.A, c.R, c.G, c.B);

    public static Color ToColor(int argb) =>
        Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

    public static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    /// <summary>Gradient brush that reproduces a colour map, for legends.</summary>
    public static LinearGradientBrush LegendBrush(Func<double, int> map, bool vertical = false)
    {
        var brush = new LinearGradientBrush { StartPoint = vertical ? new(0, 1) : new(0, 0), EndPoint = vertical ? new(0, 0) : new(1, 0) };
        for (int i = 0; i <= 10; i++) brush.GradientStops.Add(new GradientStop(ToColor(map(i / 10.0)), i / 10.0));
        brush.Freeze();
        return brush;
    }

    private static int Sample((double T, Color C)[] stops, double t)
    {
        if (double.IsNaN(t)) t = 0;
        t = Math.Clamp(t, 0, 1);
        for (int i = 1; i < stops.Length; i++)
        {
            if (t <= stops[i].T)
            {
                var (t0, c0) = stops[i - 1];
                var (t1, c1) = stops[i];
                double f = (t - t0) / (t1 - t0);
                return Pack(255,
                    (byte)(c0.R + (c1.R - c0.R) * f),
                    (byte)(c0.G + (c1.G - c0.G) * f),
                    (byte)(c0.B + (c1.B - c0.B) * f));
            }
        }
        return Pack(stops[^1].C);
    }
}
