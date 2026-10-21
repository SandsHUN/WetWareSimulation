using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WetWare.App.Controls.Course;

/// <summary>
/// Input layer u (top) → weights W → output layer v (bottom). Node brightness follows activity; the connections
/// of the selected output neuron are drawn red (positive) / blue (negative) with width ∝ |w|. Hover picks the neuron.
/// </summary>
public sealed class LayerDiagram : FrameworkElement
{
    public double[] Inputs { get; set; } = Array.Empty<double>();
    public double[] Outputs { get; set; } = Array.Empty<double>();
    public double[,] Weights { get; set; } = new double[0, 0];
    public int Selected { get; set; } = -1;
    public string InputLabel { get; set; } = "u";
    public string OutputLabel { get; set; } = "v";

    public event EventHandler<int>? SelectedChanged;

    public void Render() => InvalidateVisual();

    private double X(int i, int n) => 34 + (ActualWidth - 50) * (i + 0.5) / n;

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        int n = Inputs.Length;
        if (n == 0 || ActualWidth < 60) return;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double yIn = 20, yOut = ActualHeight - 20;
        double r = Math.Max(2.2, Math.Min(5, (ActualWidth - 50) / n * 0.32));
        double maxIn = Math.Max(1e-9, Inputs.Max(Math.Abs)), maxOut = Math.Max(1e-9, Outputs.Max(Math.Abs));

        // Faint "all connections" fabric.
        var fabric = DrawKit.Pen(DrawKit.Axis, 0.6, 0.35);
        for (int i = 0; i < n; i += 2)
            for (int j = 0; j < n; j++)
                if (Weights[i, j] != 0 && i != Selected)
                    dc.DrawLine(fabric, new Point(X(j, n), yIn + r), new Point(X(i, n), yOut - r));

        if (Selected >= 0 && Selected < n)
        {
            double wMax = 1e-9;
            for (int j = 0; j < n; j++) wMax = Math.Max(wMax, Math.Abs(Weights[Selected, j]));
            for (int j = 0; j < n; j++)
            {
                double w = Weights[Selected, j];
                if (w == 0) continue;
                var pen = DrawKit.Pen(w > 0 ? DrawKit.Excitatory : DrawKit.Inhibitory, 1 + 3 * Math.Abs(w) / wMax, 0.9);
                dc.DrawLine(pen, new Point(X(j, n), yIn + r), new Point(X(Selected, n), yOut - r));
            }
        }

        for (int i = 0; i < n; i++)
        {
            double a = Math.Clamp(Inputs[i] / maxIn, 0, 1);
            dc.DrawEllipse(DrawKit.Brush(DrawKit.Lerp(DrawKit.Grid, DrawKit.Warning, a)), null, new Point(X(i, n), yIn), r, r);
            double v = Outputs[i] / maxOut;
            var c = v >= 0 ? DrawKit.Lerp(DrawKit.Grid, DrawKit.Accent, v) : DrawKit.Lerp(DrawKit.Grid, DrawKit.Pink, -v);
            if (i == Selected) DrawKit.Glow(dc, new Point(X(i, n), yOut), r + 1, c);
            else dc.DrawEllipse(DrawKit.Brush(c), null, new Point(X(i, n), yOut), r, r);
        }
        DrawKit.DrawText(dc, InputLabel, new Point(4, yIn), 13, DrawKit.Warning, dpi, HorizontalAlignment.Left, DrawKit.MathFace);
        DrawKit.DrawText(dc, OutputLabel, new Point(4, yOut), 13, DrawKit.Accent, dpi, HorizontalAlignment.Left, DrawKit.MathFace);
        DrawKit.DrawText(dc, "W", new Point(8, (yIn + yOut) / 2), 14, DrawKit.Muted, dpi, HorizontalAlignment.Left, DrawKit.MathFace);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int n = Inputs.Length;
        if (n == 0) return;
        double x = e.GetPosition(this).X;
        int i = Math.Clamp((int)((x - 34) / (ActualWidth - 50) * n), 0, n - 1);
        if (i != Selected) SelectedChanged?.Invoke(this, i);
    }
}
