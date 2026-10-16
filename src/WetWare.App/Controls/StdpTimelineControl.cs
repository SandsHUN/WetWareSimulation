using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace WetWare.App.Controls;

/// <summary>
/// Animated sketch of one STDP pairing: a presynaptic and a postsynaptic spike train, the Δt between them,
/// and a synaptic bouton whose size follows the weight.
/// </summary>
public sealed class StdpTimelineControl : FrameworkElement
{
    private static readonly Typeface Face = new("Segoe UI");
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0x97, 0xA3, 0xC2));
    private static readonly Pen AxonPen = new(new SolidColorBrush(Color.FromRgb(0x2C, 0x3D, 0x63)), 2);
    private static readonly Brush PreBrush = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24));
    private static readonly Brush PostBrush = new SolidColorBrush(Color.FromRgb(0xA7, 0x8B, 0xFA));

    /// <summary>t_post − t_pre of the pairing being shown (ms).</summary>
    public double DeltaT { get; set; } = 10;

    /// <summary>Animation progress 0–1 (spikes appear as the cursor passes them).</summary>
    public double Progress { get; set; } = 1;

    public double Weight { get; set; } = 0.5;

    public double LastDeltaW { get; set; }

    public string PreLabel { get; set; } = "pre";
    public string PostLabel { get; set; } = "post";

    public void Render() => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        if (w < 80 || h < 60) return;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        double boutonArea = Math.Min(130, w * 0.28);
        double left = 46, right = w - boutonArea - 16;
        double yPre = h * 0.32, yPost = h * 0.72;
        const double range = 60; // ms shown on each side of the presynaptic spike
        double X(double t) => left + (t + range) / (2 * range) * (right - left);

        dc.DrawLine(AxonPen, new Point(left, yPre), new Point(right, yPre));
        dc.DrawLine(AxonPen, new Point(left, yPost), new Point(right, yPost));
        dc.DrawText(Text(PreLabel, 12, PreBrush, dpi), new Point(4, yPre - 9));
        dc.DrawText(Text(PostLabel, 12, PostBrush, dpi), new Point(4, yPost - 9));

        double cursorT = -range + Progress * 2 * range;
        var cursorPen = new Pen(new SolidColorBrush(Color.FromArgb(0x50, 0x22, 0xD3, 0xEE)), 1);
        dc.DrawLine(cursorPen, new Point(X(cursorT), yPre - 26), new Point(X(cursorT), yPost + 26));

        bool ltp = DeltaT > 0;
        var arrowColor = ltp ? Color.FromRgb(0x34, 0xD3, 0x99) : Color.FromRgb(0xF4, 0x3F, 0x5E);
        double tPre = 0, tPost = Math.Clamp(DeltaT, -range + 2, range - 2);

        if (cursorT >= tPre) DrawSpike(dc, X(tPre), yPre, PreBrush);
        if (cursorT >= tPost) DrawSpike(dc, X(tPost), yPost, PostBrush);

        if (cursorT >= Math.Max(tPre, tPost))
        {
            var pen = new Pen(new SolidColorBrush(arrowColor), 1.6) { DashStyle = DashStyles.Dash };
            double ym = (yPre + yPost) / 2;
            dc.DrawLine(pen, new Point(X(tPre), ym), new Point(X(tPost), ym));
            var label = Text($"Δt = {DeltaT.ToString("+0.0;−0.0", CultureInfo.InvariantCulture)} ms", 12.5, new SolidColorBrush(arrowColor), dpi);
            dc.DrawText(label, new Point((X(tPre) + X(tPost)) / 2 - label.Width / 2, ym - label.Height - 2));
        }

        // Time axis ticks.
        for (int t = -60; t <= 60; t += 20)
        {
            var tick = Text(t.ToString(CultureInfo.InvariantCulture), 9.5, Muted, dpi);
            dc.DrawText(tick, new Point(X(t) - tick.Width / 2, yPost + 24));
        }

        // Synaptic bouton: radius ∝ √w so its area follows the weight.
        var center = new Point(w - boutonArea / 2 - 8, h / 2);
        double maxR = Math.Min(boutonArea / 2 - 6, h / 2 - 22);
        double r = Math.Max(4, maxR * Math.Sqrt(Math.Clamp(Weight, 0, 1)));
        var glow = new RadialGradientBrush(Color.FromArgb(0xC0, 0x22, 0xD3, 0xEE), Color.FromArgb(0x10, 0xA7, 0x8B, 0xFA));
        dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromRgb(0x2C, 0x3D, 0x63)), 1) { DashStyle = DashStyles.Dot }, center, maxR, maxR);
        dc.DrawEllipse(glow, new Pen(new SolidColorBrush(Color.FromRgb(0x22, 0xD3, 0xEE)), 1.5), center, r, r);
        var wText = Text($"w = {Weight.ToString("0.000", CultureInfo.InvariantCulture)}", 12, Brushes.White, dpi);
        dc.DrawText(wText, new Point(center.X - wText.Width / 2, center.Y + maxR + 4));
        if (Math.Abs(LastDeltaW) > 1e-9)
        {
            var dwColor = LastDeltaW > 0 ? Color.FromRgb(0x34, 0xD3, 0x99) : Color.FromRgb(0xF4, 0x3F, 0x5E);
            var dwText = Text($"Δw = {LastDeltaW.ToString("+0.0000;−0.0000", CultureInfo.InvariantCulture)}", 11.5, new SolidColorBrush(dwColor), dpi);
            dc.DrawText(dwText, new Point(center.X - dwText.Width / 2, center.Y - maxR - dwText.Height - 2));
        }
    }

    private static void DrawSpike(DrawingContext dc, double x, double y, Brush brush)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(x - 7, y), false, false);
            ctx.LineTo(new Point(x - 2, y), true, false);
            ctx.LineTo(new Point(x, y - 24), true, false);
            ctx.LineTo(new Point(x + 2.5, y + 8), true, false);
            ctx.LineTo(new Point(x + 5, y), true, false);
            ctx.LineTo(new Point(x + 9, y), true, false);
        }
        dc.DrawGeometry(null, new Pen(brush, 2.2) { LineJoin = PenLineJoin.Round }, geometry);
        dc.DrawEllipse(new RadialGradientBrush(Color.FromArgb(0x70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255)), null, new Point(x, y - 12), 10, 14);
    }

    private static FormattedText Text(string s, double size, Brush brush, double dpi) =>
        new(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Face, size, brush, dpi);
}
