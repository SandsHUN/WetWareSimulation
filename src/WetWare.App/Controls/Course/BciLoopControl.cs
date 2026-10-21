using System.Windows;
using System.Windows.Media;
using WetWare.Core.Course.Bci;

namespace WetWare.App.Controls.Course;

/// <summary>
/// The artificial recurrent connection: two recorded cortical units (flexor / extensor tuned) → Neurochip rate
/// decoding → functional electrical stimulation of the forearm muscles → wrist movement. The natural spinal pathway
/// is drawn lesioned; when the BCI is off nothing reaches the muscles.
/// </summary>
public sealed class BciLoopControl : FrameworkElement
{
    public FesBridge? Bridge { get; set; }
    public string CortexLabel { get; set; } = "Motor cortex";
    public string ChipLabel { get; set; } = "Neurochip";
    public string LesionLabel { get; set; } = "lesion";
    public string FlexorLabel { get; set; } = "flexor";
    public string ExtensorLabel { get; set; } = "extensor";

    public void Render() => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        var fes = Bridge;
        double w = ActualWidth, h = ActualHeight;
        if (fes is null || w < 300 || h < 180) return;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double now = fes.TimeMs;

        // ---- Cortex with two units ----
        var cortex = new Point(w * 0.13, h * 0.42);
        var brush = new RadialGradientBrush(Color.FromRgb(0x2A, 0x23, 0x55), Color.FromRgb(0x15, 0x1A, 0x33));
        brush.Freeze();
        dc.DrawEllipse(brush, DrawKit.Pen(DrawKit.Accent2, 1.5, 0.6), cortex, w * 0.1, h * 0.27);
        DrawKit.DrawText(dc, CortexLabel, new Point(cortex.X, cortex.Y - h * 0.27 - 12), 11.5, DrawKit.Accent2, dpi, HorizontalAlignment.Center, DrawKit.SemiBold);
        var unitF = new Point(cortex.X - w * 0.035, cortex.Y - h * 0.07);
        var unitE = new Point(cortex.X + w * 0.03, cortex.Y + h * 0.08);
        DrawUnit(dc, unitF, Recent(fes.SpikesF, now), DrawKit.Accent, "F", dpi);
        DrawUnit(dc, unitE, Recent(fes.SpikesE, now), DrawKit.Pink, "E", dpi);

        // ---- Lesioned natural pathway (cortex → spinal cord → arm) ----
        var armShoulder = new Point(w * 0.66, h * 0.5);
        var natural = new StreamGeometry();
        using (var ctx = natural.Open())
        {
            ctx.BeginFigure(new Point(cortex.X, cortex.Y + h * 0.27), false, false);
            ctx.BezierTo(new Point(cortex.X + 10, h * 0.98), new Point(armShoulder.X - 80, h * 0.98), new Point(armShoulder.X - 10, armShoulder.Y + 30), true, true);
        }
        natural.Freeze();
        dc.DrawGeometry(null, DrawKit.Pen(DrawKit.Faint, 2, 0.6, dashed: true), natural);
        var lesion = new Point(w * 0.36, h * 0.93);
        var red = DrawKit.Pen(DrawKit.Danger, 3);
        dc.DrawLine(red, new Point(lesion.X - 9, lesion.Y - 9), new Point(lesion.X + 9, lesion.Y + 9));
        dc.DrawLine(red, new Point(lesion.X - 9, lesion.Y + 9), new Point(lesion.X + 9, lesion.Y - 9));
        DrawKit.DrawText(dc, LesionLabel, new Point(lesion.X + 14, lesion.Y), 10.5, DrawKit.Danger, dpi);

        // ---- Neurochip ----
        var chip = new Rect(w * 0.31, h * 0.2, w * 0.2, h * 0.44);
        var chipFill = new LinearGradientBrush(Color.FromRgb(0x1B, 0x29, 0x44), Color.FromRgb(0x10, 0x18, 0x29), 90);
        chipFill.Freeze();
        dc.DrawRoundedRectangle(chipFill, DrawKit.Pen(fes.BciEnabled ? DrawKit.Accent : DrawKit.Faint, 1.6), chip, 10, 10);
        DrawKit.DrawText(dc, ChipLabel, new Point(chip.Left + chip.Width / 2, chip.Top - 12), 11.5, fes.BciEnabled ? DrawKit.Accent : DrawKit.Faint, dpi, HorizontalAlignment.Center, DrawKit.SemiBold);
        var (rf, re) = fes.DecodedRates;
        double maxRate = 70;
        double barH = chip.Height - 34, barW = chip.Width * 0.22;
        double thY = chip.Bottom - 12 - barH * Math.Clamp(fes.ThresholdHz / maxRate, 0, 1);
        void Bar(double x, double rate, Color color, string label)
        {
            double hh = barH * Math.Clamp(rate / maxRate, 0, 1);
            dc.DrawRoundedRectangle(DrawKit.Brush(DrawKit.Grid), null, new Rect(x, chip.Bottom - 12 - barH, barW, barH), 4, 4);
            dc.DrawRoundedRectangle(DrawKit.Brush(color, 0.85), null, new Rect(x, chip.Bottom - 12 - hh, barW, hh), 4, 4);
            DrawKit.DrawText(dc, label, new Point(x + barW / 2, chip.Top + 12), 10.5, color, dpi, HorizontalAlignment.Center);
        }
        Bar(chip.Left + chip.Width * 0.18, rf, DrawKit.Accent, "F");
        Bar(chip.Left + chip.Width * 0.6, re, DrawKit.Pink, "E");
        dc.DrawLine(DrawKit.Pen(DrawKit.Warning, 1.4, 0.9, dashed: true), new Point(chip.Left + 6, thY), new Point(chip.Right - 6, thY));

        // Recording wires cortex → chip.
        Wire(dc, unitF, new Point(chip.Left, chip.Top + chip.Height * 0.35), DrawKit.Accent, Math.Clamp(rf / maxRate, 0, 1), now);
        Wire(dc, unitE, new Point(chip.Left, chip.Top + chip.Height * 0.65), DrawKit.Pink, Math.Clamp(re / maxRate, 0, 1), now);

        // ---- Arm ----
        double forearm = w * 0.22;
        var elbow = armShoulder;
        var wrist = new Point(elbow.X + forearm, elbow.Y);
        dc.DrawLine(DrawKit.Pen(Color.FromRgb(0x3A, 0x4A, 0x70), 22), new Point(elbow.X - 40, elbow.Y + 26), elbow);
        dc.DrawLine(DrawKit.Pen(Color.FromRgb(0x46, 0x58, 0x84), 24), elbow, wrist);

        // Muscles: flexor on top, extensor below; brightness = stimulation current.
        double fl = Math.Clamp(fes.CurrentF / 4, 0, 1), ex = Math.Clamp(fes.CurrentE / 4, 0, 1);
        var flexor = new Point((elbow.X + wrist.X) / 2, elbow.Y - 9);
        var extensor = new Point((elbow.X + wrist.X) / 2, elbow.Y + 9);
        dc.DrawEllipse(DrawKit.Brush(DrawKit.Lerp(Color.FromRgb(0x5B, 0x3A, 0x55), DrawKit.Accent, fl)), null, flexor, forearm * 0.38, 6);
        dc.DrawEllipse(DrawKit.Brush(DrawKit.Lerp(Color.FromRgb(0x5B, 0x3A, 0x55), DrawKit.Pink, ex)), null, extensor, forearm * 0.38, 6);
        DrawKit.DrawText(dc, FlexorLabel, new Point(flexor.X, flexor.Y - 24), 10.5, DrawKit.Accent, dpi, HorizontalAlignment.Center);
        DrawKit.DrawText(dc, ExtensorLabel, new Point(extensor.X, extensor.Y + 24), 10.5, DrawKit.Pink, dpi, HorizontalAlignment.Center);

        // Stimulation wires chip → muscles.
        Wire(dc, new Point(chip.Right, chip.Top + chip.Height * 0.35), new Point(flexor.X - forearm * 0.3, flexor.Y - 4), DrawKit.Accent, fes.BciEnabled ? fl : 0, now);
        Wire(dc, new Point(chip.Right, chip.Top + chip.Height * 0.65), new Point(extensor.X - forearm * 0.3, extensor.Y + 4), DrawKit.Pink, fes.BciEnabled ? ex : 0, now);

        // Target (ghost) and actual hand. Flexion = upward rotation.
        DrawHand(dc, wrist, fes.TargetAngle, w * 0.09, DrawKit.Pen(DrawKit.Warning, 2, 0.7, dashed: true), null);
        DrawHand(dc, wrist, fes.WristAngle, w * 0.09, DrawKit.Pen(DrawKit.Text, 1.5), DrawKit.Brush(Color.FromRgb(0xC8, 0xCF, 0xE0)));
        dc.DrawEllipse(DrawKit.Brush(Color.FromRgb(0x46, 0x58, 0x84)), null, wrist, 8, 8);
    }

    private static int Recent(List<double> spikes, double now)
    {
        int count = 0;
        for (int i = spikes.Count - 1; i >= 0 && now - spikes[i] < 40; i--) count++;
        return count;
    }

    private static void DrawUnit(DrawingContext dc, Point p, int recent, Color color, string label, double dpi)
    {
        if (recent > 0) DrawKit.Glow(dc, p, 9, color, 1);
        else dc.DrawEllipse(DrawKit.Brush(color, 0.35), DrawKit.Pen(color, 1.4), p, 9, 9);
        DrawKit.DrawText(dc, label, new Point(p.X, p.Y), 10, Color.FromRgb(0x06, 0x10, 0x1F), dpi, HorizontalAlignment.Center, DrawKit.SemiBold);
    }

    private static void Wire(DrawingContext dc, Point a, Point b, Color color, double level, double now)
    {
        dc.DrawLine(DrawKit.Pen(color, 1.5, 0.25 + 0.5 * level), a, b);
        if (level <= 0.02) return;
        for (int k = 0; k < 3; k++)
        {
            double t = (now / 500.0 + k / 3.0) % 1;
            DrawKit.Glow(dc, new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t), 2 + 2 * level, color, level);
        }
    }

    private static void DrawHand(DrawingContext dc, Point wrist, double angleDeg, double length, Pen pen, Brush? fill)
    {
        double a = -angleDeg * Math.PI / 180; // screen y grows downwards
        var dir = new Vector(Math.Cos(a), Math.Sin(a));
        var n = new Vector(-dir.Y, dir.X);
        var tip = wrist + dir * length;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(wrist + n * 10, true, true);
            ctx.LineTo(tip + n * 8, true, true);
            ctx.QuadraticBezierTo(tip + dir * 12, tip - n * 8, true, true);
            ctx.LineTo(wrist - n * 10, true, true);
        }
        geometry.Freeze();
        dc.DrawGeometry(fill, pen, geometry);
    }
}
