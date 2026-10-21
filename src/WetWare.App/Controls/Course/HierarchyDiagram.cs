using System.Windows;
using System.Windows.Media;

namespace WetWare.App.Controls.Course;

/// <summary>
/// Hierarchical predictive coding (Rao &amp; Ballard): each level sends a prediction down (feedback) and receives the
/// prediction error from below (feed-forward). Arrow widths follow the current signal sizes and small pulses travel
/// along the pathways.
/// </summary>
public sealed class HierarchyDiagram : FrameworkElement
{
    public string[] Levels { get; set; } = ["Retina / LGN", "V1", "V2"];
    public string PredictionLabel { get; set; } = "prediction";
    public string ErrorLabel { get; set; } = "error";

    /// <summary>Relative size (0–1) of the prediction error flowing up from the lowest level.</summary>
    public double Error { get; set; } = 0.5;

    /// <summary>Relative size (0–1) of the prediction flowing down.</summary>
    public double Prediction { get; set; } = 0.5;

    /// <summary>Animation phase in seconds.</summary>
    public double Phase { get; set; }

    public void Render() => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        double w = ActualWidth, h = ActualHeight;
        if (w < 120 || h < 120) return;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        int n = Levels.Length;
        double boxW = Math.Min(190, w * 0.42), boxH = 40;
        double cx = w / 2;
        double Y(int level) => h - 30 - level * (h - 60) / (n - 1);

        for (int level = 0; level < n - 1; level++)
        {
            double y0 = Y(level) - boxH / 2, y1 = Y(level + 1) + boxH / 2;
            // Higher levels see smaller residual errors: the lowest link carries the full error.
            double err = Math.Clamp(Error * (level == 0 ? 1 : 0.45), 0, 1);
            double pred = Math.Clamp(Prediction * (level == 0 ? 1 : 0.7), 0, 1);
            double xUp = cx + boxW * 0.22, xDown = cx - boxW * 0.22;

            var up = DrawKit.Pen(DrawKit.Excitatory, 1.5 + 9 * err, 0.35 + 0.6 * err);
            DrawKit.Arrow(dc, new Point(xUp, y0), new Point(xUp, y1 + 4), up, 10 + 6 * err);
            var down = DrawKit.Pen(DrawKit.Accent, 1.5 + 9 * pred, 0.35 + 0.6 * pred);
            DrawKit.Arrow(dc, new Point(xDown, y1), new Point(xDown, y0 - 4), down, 10 + 6 * pred);

            // Travelling pulses.
            for (int k = 0; k < 3; k++)
            {
                double t = (Phase * 0.9 + k / 3.0) % 1;
                DrawKit.Glow(dc, new Point(xUp, y0 + (y1 - y0) * t), 2 + 2 * err, DrawKit.Excitatory, err);
                DrawKit.Glow(dc, new Point(xDown, y1 + (y0 - y1) * t), 2 + 2 * pred, DrawKit.Accent, pred);
            }

            double ym = (y0 + y1) / 2;
            DrawKit.DrawText(dc, ErrorLabel, new Point(xUp + 16, ym), 11.5, DrawKit.Excitatory, dpi);
            DrawKit.DrawText(dc, PredictionLabel, new Point(xDown - 16, ym), 11.5, DrawKit.Accent, dpi, HorizontalAlignment.Right);
        }

        for (int level = 0; level < n; level++)
        {
            var rect = new Rect(cx - boxW / 2, Y(level) - boxH / 2, boxW, boxH);
            var fill = new LinearGradientBrush(Color.FromRgb(0x1B, 0x29, 0x44), Color.FromRgb(0x15, 0x20, 0x36), 90);
            fill.Freeze();
            dc.DrawRoundedRectangle(fill, DrawKit.Pen(level == 0 ? DrawKit.Warning : DrawKit.Accent2, 1.4, 0.8), rect, 10, 10);
            DrawKit.DrawText(dc, Levels[level], new Point(cx, Y(level)), 13, DrawKit.Text, dpi, HorizontalAlignment.Center, DrawKit.SemiBold);
        }
    }
}
