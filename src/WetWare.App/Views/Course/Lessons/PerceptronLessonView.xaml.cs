using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ScottPlot;
using WetWare.App.Controls;
using WetWare.App.Controls.Course;
using WetWare.App.ViewModels.Course.Lessons;
using WetWare.Core.Course.Supervised;
using Color = System.Windows.Media.Color;

namespace WetWare.App.Views.Course.Lessons;

public partial class PerceptronLessonView : LessonView
{
    private static readonly Color Positive = DrawKit.Accent;
    private static readonly Color Negative = DrawKit.Pink;

    public PerceptronLessonView()
    {
        InitializeComponent();
        Plane.XMin = Plane.YMin = -1.3;
        Plane.XMax = Plane.YMax = 1.3;
        Plane.GridStep = 0.5;
        Plane.Painter = Paint;
        Plane.WorldClick += (_, p) => Vm.AddPoint(p.X, p.Y, p.Button == MouseButton.Right ? -1 : 1);
    }

    private PerceptronLesson Vm => (PerceptronLesson)DataContext;

    protected override void Setup() => StylePlots(CurvePlot, SigmoidPlot);

    protected override void Draw(bool full)
    {
        Plane.Render();
        var vm = Vm;
        var plot = CurvePlot.Plot;
        plot.Clear();
        if (vm.UsesMlp)
        {
            var loss = vm.Mlp.LossHistory.ToArray();
            if (loss.Length > 1)
            {
                var line = plot.Add.Signal(loss, 1, LabPlot.Warning);
                line.LineWidth = 2;
            }
            plot.Axes.SetLimits(0, Math.Max(50, loss.Length), 0, Math.Max(0.05, loss.DefaultIfEmpty(0.15).Max() * 1.1));
            LabPlot.Labels(CurvePlot, Loc["M8.Perceptron.EpochAxis"], "E = ½(d − v)²");
        }
        else
        {
            var acc = vm.AccuracyHistory.Select(a => a * 100).ToArray();
            if (acc.Length > 1)
            {
                var line = plot.Add.Signal(acc, 1, LabPlot.Success);
                line.LineWidth = 2;
            }
            plot.Add.HorizontalLine(100, 1, LabPlot.Muted, LinePattern.Dotted);
            plot.Axes.SetLimits(0, Math.Max(50, acc.Length), 0, 105);
            LabPlot.Labels(CurvePlot, Loc["M8.Perceptron.PresentationAxis"], Loc["M8.Perceptron.AccuracyAxis"]);
        }
        CurvePlot.Refresh();

        if (vm.UsesMlp && (full || Frame % 10 == 0))
        {
            var sp = SigmoidPlot.Plot;
            sp.Clear();
            double[] xs = Enumerable.Range(0, 121).Select(i => -6 + i * 0.1).ToArray();
            var g = sp.Add.ScatterLine(xs, xs.Select(x => MultilayerPerceptron.Sigmoid(vm.Beta * x)).ToArray(), LabPlot.Accent2);
            g.LineWidth = 2.4f;
            var step = sp.Add.ScatterLine(xs, xs.Select(x => x > 0 ? 1.0 : 0.0).ToArray(), LabPlot.Muted.WithAlpha(0.5));
            step.LinePattern = LinePattern.Dotted;
            sp.Axes.SetLimits(-6, 6, -0.05, 1.05);
            LabPlot.Labels(SigmoidPlot, "a", "g(a)");
            SigmoidPlot.Refresh();
        }
    }

    private void Paint(DrawingContext dc, PlaneCanvas canvas)
    {
        var vm = Vm;
        // Decision regions: perceptron = two flat colours, MLP = graded output probability.
        const int cells = 52;
        double step = (canvas.XMax - canvas.XMin) / cells, px = step * canvas.Scale + 0.6;
        for (int a = 0; a < cells; a++)
        {
            for (int b = 0; b < cells; b++)
            {
                double x = canvas.XMin + (a + 0.5) * step, y = canvas.YMin + (b + 0.5) * step;
                double p = vm.UsesMlp ? vm.Mlp.Output(x, y) : vm.Perceptron.Classify(x, y) > 0 ? 1 : 0;
                var color = DrawKit.Lerp(Negative, Positive, p);
                double alpha = vm.UsesMlp ? 0.06 + 0.16 * Math.Abs(p - 0.5) * 2 : 0.13;
                var c = canvas.ToScreen(x, y);
                dc.DrawRectangle(DrawKit.Brush(color, alpha), null, new Rect(c.X - px / 2, c.Y - px / 2, px, px));
            }
        }

        if (vm.UsesMlp)
        {
            // Each hidden unit contributes one line (its own half-plane).
            for (int j = 0; j < vm.Mlp.Hidden; j++)
            {
                var (wx, wy, bias) = vm.Mlp.HiddenLine(j);
                DrawLine(dc, canvas, wx, wy, -bias, DrawKit.Pen(DrawKit.Text, 1, 0.22, dashed: true));
            }
        }
        else
        {
            var per = vm.Perceptron;
            DrawLine(dc, canvas, per.W1, per.W2, per.Mu, DrawKit.Pen(DrawKit.Warning, 2.6));
            // Weight vector, perpendicular to the decision line.
            double n = Math.Sqrt(per.W1 * per.W1 + per.W2 * per.W2);
            if (n > 1e-9)
            {
                double ox = per.W1 * per.Mu / (n * n), oy = per.W2 * per.Mu / (n * n);
                var from = canvas.ToScreen(ox, oy);
                var to = canvas.ToScreen(ox + per.W1 / n * 0.35, oy + per.W2 / n * 0.35);
                DrawKit.Arrow(dc, from, to, DrawKit.Pen(DrawKit.Warning, 2.2), 9);
                DrawKit.DrawText(dc, "w", new Point(to.X + 8, to.Y - 8), 14, DrawKit.Warning, canvas.Dpi, System.Windows.HorizontalAlignment.Left, DrawKit.MathFace);
            }
        }

        foreach (var p in vm.Data)
        {
            var s = canvas.ToScreen(p.X, p.Y);
            int predicted = vm.UsesMlp ? (vm.Mlp.Output(p.X, p.Y) > 0.5 ? 1 : -1) : vm.Perceptron.Classify(p.X, p.Y);
            var color = p.Label > 0 ? Positive : Negative;
            if (p.Label > 0) dc.DrawEllipse(DrawKit.Brush(color), DrawKit.Pen(Color.FromRgb(0x06, 0x10, 0x1F), 1.2), s, 4.6, 4.6);
            else dc.DrawRectangle(DrawKit.Brush(color), DrawKit.Pen(Color.FromRgb(0x06, 0x10, 0x1F), 1.2), new Rect(s.X - 4.2, s.Y - 4.2, 8.4, 8.4));
            if (predicted != p.Label) dc.DrawEllipse(null, DrawKit.Pen(DrawKit.Danger, 1.8), s, 8.5, 8.5);
        }

        if (!vm.UsesMlp && vm.Perceptron.Last is { } last)
        {
            var s = canvas.ToScreen(last.Point.X, last.Point.Y);
            dc.DrawEllipse(null, DrawKit.Pen(last.Updated ? DrawKit.Warning : DrawKit.Success, 2.4), s, 12, 12);
        }
    }

    /// <summary>Draws the line w₁x + w₂y = c across the canvas.</summary>
    private static void DrawLine(DrawingContext dc, PlaneCanvas canvas, double w1, double w2, double c, System.Windows.Media.Pen pen)
    {
        if (Math.Abs(w1) < 1e-9 && Math.Abs(w2) < 1e-9) return;
        Point a, b;
        if (Math.Abs(w2) > Math.Abs(w1))
        {
            a = canvas.ToScreen(canvas.XMin, (c - w1 * canvas.XMin) / w2);
            b = canvas.ToScreen(canvas.XMax, (c - w1 * canvas.XMax) / w2);
        }
        else
        {
            a = canvas.ToScreen((c - w2 * canvas.YMin) / w1, canvas.YMin);
            b = canvas.ToScreen((c - w2 * canvas.YMax) / w1, canvas.YMax);
        }
        dc.DrawLine(pen, a, b);
    }
}
