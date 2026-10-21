using System.Windows;
using System.Windows.Media;

namespace WetWare.App.Controls.Course;

/// <summary>
/// A stylised eye whose gaze follows the stored value of the eigenmode – the oculomotor neural integrator
/// analogy of lecture 6.3.6: brief bursts move the eye, persistent activity holds it in place.
/// </summary>
public sealed class EyeControl : FrameworkElement
{
    /// <summary>Gaze position, −1 (left) … +1 (right).</summary>
    public double Gaze { get; set; }

    public void Render() => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        double w = ActualWidth, h = ActualHeight;
        if (w < 40 || h < 30) return;
        var c = new Point(w / 2, h / 2);
        double rx = Math.Min(w * 0.45, h * 0.9), ry = rx * 0.48;

        // Almond-shaped eye outline.
        var eye = new StreamGeometry();
        using (var ctx = eye.Open())
        {
            ctx.BeginFigure(new Point(c.X - rx, c.Y), true, true);
            ctx.QuadraticBezierTo(new Point(c.X, c.Y - ry * 2), new Point(c.X + rx, c.Y), true, true);
            ctx.QuadraticBezierTo(new Point(c.X, c.Y + ry * 2), new Point(c.X - rx, c.Y), true, true);
        }
        eye.Freeze();
        var sclera = new RadialGradientBrush(Color.FromRgb(0xE8, 0xED, 0xF8), Color.FromRgb(0x9A, 0xA6, 0xC4));
        sclera.Freeze();
        dc.PushClip(eye);
        dc.DrawGeometry(sclera, null, eye);
        double g = Math.Clamp(Gaze, -1.3, 1.3);
        var iris = new Point(c.X + g * rx * 0.55, c.Y);
        double ir = ry * 0.85;
        var irisBrush = new RadialGradientBrush(DrawKit.Accent, Color.FromRgb(0x1E, 0x3A, 0x8A)) { GradientOrigin = new Point(0.4, 0.35) };
        irisBrush.Freeze();
        dc.DrawEllipse(irisBrush, null, iris, ir, ir);
        dc.DrawEllipse(DrawKit.Brush(Color.FromRgb(0x05, 0x0A, 0x14)), null, iris, ir * 0.45, ir * 0.45);
        dc.DrawEllipse(DrawKit.Brush(Colors.White, 0.8), null, new Point(iris.X - ir * 0.3, iris.Y - ir * 0.3), ir * 0.14, ir * 0.14);
        dc.Pop();
        dc.DrawGeometry(null, DrawKit.Pen(DrawKit.Axis, 2.5), eye);
    }
}
