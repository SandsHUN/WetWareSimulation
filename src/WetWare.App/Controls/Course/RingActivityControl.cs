using System.Windows;
using System.Windows.Media;

namespace WetWare.App.Controls.Course;

/// <summary>
/// Polar view of a ring network: every neuron sits at its preferred direction; outer bars show its firing rate,
/// the inner dotted ring the external input, the arrow the population vector (decoded direction).
/// </summary>
public sealed class RingActivityControl : FrameworkElement
{
    public double[] Angles { get; set; } = Array.Empty<double>();
    public double[] Rates { get; set; } = Array.Empty<double>();
    public double[] Input { get; set; } = Array.Empty<double>();
    public double Scale { get; set; } = 50;
    public double PopulationVector { get; set; } = double.NaN;
    public bool InputOn { get; set; } = true;

    public void Render() => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size < 80 || Angles.Length == 0) return;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        double r0 = size * 0.24, rMax = size * 0.46;
        Point P(double deg, double r) => new(c.X + r * Math.Cos(deg * Math.PI / 180), c.Y - r * Math.Sin(deg * Math.PI / 180));

        var core = new RadialGradientBrush(Color.FromArgb(0x30, 0x22, 0xD3, 0xEE), Color.FromArgb(0x00, 0x22, 0xD3, 0xEE));
        core.Freeze();
        dc.DrawEllipse(core, null, c, rMax, rMax);
        dc.DrawEllipse(null, DrawKit.Pen(DrawKit.Axis, 1, 0.7, dashed: true), c, rMax, rMax);
        dc.DrawEllipse(null, DrawKit.Pen(DrawKit.Axis, 1.2), c, r0, r0);

        foreach (int deg in new[] { 0, 90, 180, 270 })
        {
            dc.DrawLine(DrawKit.Pen(DrawKit.Grid, 1), P(deg, r0 * 0.3), P(deg, rMax));
            string label = deg switch { 0 => "0°", 90 => "90°", 180 => "±180°", _ => "−90°" };
            DrawKit.DrawText(dc, label, P(deg, rMax + 12), 10, DrawKit.Faint, dpi, HorizontalAlignment.Center);
        }

        int n = Angles.Length;
        double inMax = Math.Max(1e-6, Input.Length == 0 ? 1 : Input.Max(Math.Abs));
        double barWidth = Math.Max(2, 2 * Math.PI * r0 / n * 0.7);
        for (int i = 0; i < n; i++)
        {
            double rate = Math.Max(0, Rates[i]);
            double t = Math.Clamp(rate / Scale, 0, 1);
            var color = DrawKit.Lerp(DrawKit.Accent2, DrawKit.Warning, t);
            if (t > 0.01)
            {
                var pen = DrawKit.Pen(color, barWidth, 0.35 + 0.65 * t);
                dc.DrawLine(pen, P(Angles[i], r0 + 3), P(Angles[i], r0 + 3 + t * (rMax - r0 - 4)));
            }
            if (Input.Length == n)
            {
                double a = Input[i] / inMax;
                var dotColor = a >= 0 ? DrawKit.Warning : DrawKit.Inhibitory;
                double opacity = InputOn ? 0.15 + 0.85 * Math.Abs(a) : 0.12;
                dc.DrawEllipse(DrawKit.Brush(dotColor, opacity), null, P(Angles[i], r0 - 9), 1.2 + 2 * Math.Abs(a), 1.2 + 2 * Math.Abs(a));
            }
        }

        if (!double.IsNaN(PopulationVector) && Rates.Length > 0 && Rates.Max() > 0.5)
        {
            var tip = P(PopulationVector, r0 * 0.85);
            DrawKit.Arrow(dc, c, tip, DrawKit.Pen(DrawKit.Text, 2.4), 10);
            DrawKit.Glow(dc, tip, 3, DrawKit.Text, 0.7);
        }
        dc.DrawEllipse(DrawKit.Brush(DrawKit.Text), null, c, 3, 3);
    }
}
