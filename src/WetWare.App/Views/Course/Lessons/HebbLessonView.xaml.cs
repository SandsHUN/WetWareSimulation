using System.Windows;
using System.Windows.Media;
using ScottPlot;
using WetWare.App.Controls;
using WetWare.App.Controls.Course;
using WetWare.App.ViewModels.Course.Lessons;
using WetWare.Core.Course.Unsupervised;

namespace WetWare.App.Views.Course.Lessons;

public partial class HebbLessonView : LessonView
{
    public HebbLessonView()
    {
        InitializeComponent();
        Plane.XMin = Plane.YMin = -4;
        Plane.XMax = Plane.YMax = 4;
        Plane.XLabel = "u₁";
        Plane.YLabel = "u₂";
        Plane.Painter = Paint;
    }

    private HebbLesson Vm => (HebbLesson)DataContext;

    protected override void Setup() => StylePlots(NormPlot, AnglePlot);

    protected override void Draw(bool full)
    {
        Plane.Render();
        if (!full && Frame % 3 != 0) return;
        var learner = Vm.Learner;

        var np = NormPlot.Plot;
        np.Clear();
        var norms = learner.NormHistory.ToArray();
        if (norms.Length > 1)
        {
            var line = np.Add.Signal(norms, 2, LabPlot.Accent);
            line.LineWidth = 2;
        }
        if (learner.Rule == HebbianRule.Oja)
        {
            var target = np.Add.HorizontalLine(1 / Math.Sqrt(learner.Alpha), 1.2f, LabPlot.Success, LinePattern.Dashed);
            target.Text = "1/√α";
        }
        np.Axes.SetLimits(0, Math.Max(100, norms.Length * 2), 0, Math.Max(1.5, Math.Min(norms.DefaultIfEmpty(1).Max() * 1.15, 1100)));
        LabPlot.Labels(NormPlot, Loc["Course.Axis.Steps"], "‖w‖");
        NormPlot.Refresh();

        var ap = AnglePlot.Plot;
        ap.Clear();
        var angles = learner.AngleHistory.ToArray();
        if (angles.Length > 1)
        {
            var line = ap.Add.Signal(angles, 2, LabPlot.Warning);
            line.LineWidth = 2;
        }
        ap.Add.HorizontalLine(0, 1, LabPlot.Success, LinePattern.Dotted);
        ap.Axes.SetLimits(0, Math.Max(100, angles.Length * 2), 0, 90);
        LabPlot.Labels(AnglePlot, Loc["Course.Axis.Steps"], Loc["M7.Hebb.AngleAxis"]);
        AnglePlot.Refresh();
    }

    private void Paint(DrawingContext dc, PlaneCanvas canvas)
    {
        var learner = Vm.Learner;
        foreach (var (x, y) in learner.Samples) dc.DrawEllipse(DrawKit.Brush(DrawKit.Accent2, 0.38), null, canvas.ToScreen(x, y), 2.6, 2.6);

        // Principal axis of the averaged matrix (through the origin for Q, through the mean for C).
        var pc = learner.Principal;
        var (cx, cy) = learner.Rule == HebbianRule.Covariance ? learner.Mean : (0.0, 0.0);
        var axisPen = DrawKit.Pen(DrawKit.Success, 1.6, 0.8, dashed: true);
        dc.DrawLine(axisPen, canvas.ToScreen(cx - pc.X * 6, cy - pc.Y * 6), canvas.ToScreen(cx + pc.X * 6, cy + pc.Y * 6));
        var label = canvas.ToScreen(cx + pc.X * 2.4, cy + pc.Y * 2.4);
        DrawKit.DrawText(dc, Loc["M7.Hebb.PcLabel"], new Point(label.X + 6, label.Y - 10), 11, DrawKit.Success, canvas.Dpi);

        if (learner.Rule == HebbianRule.Oja)
        {
            double r = 1 / Math.Sqrt(learner.Alpha) * canvas.Scale;
            dc.DrawEllipse(null, DrawKit.Pen(DrawKit.Muted, 1, 0.5, dashed: true), canvas.ToScreen(0, 0), r, r);
        }

        if (learner.LastInput is { } u)
        {
            var p = canvas.ToScreen(u.X, u.Y);
            DrawKit.Glow(dc, p, 4, DrawKit.Warning, 0.9);
        }

        // Weight vector: the arrow is drawn even when ‖w‖ explodes (clipped to the canvas, length shown in the read-out).
        double n = learner.Norm;
        double shown = Math.Min(n, 3.8);
        double wx = n > 0 ? learner.Wx / n * shown : 0, wy = n > 0 ? learner.Wy / n * shown : 0;
        var origin = canvas.ToScreen(0, 0);
        var tip = canvas.ToScreen(wx, wy);
        DrawKit.Arrow(dc, origin, tip, DrawKit.Pen(DrawKit.Accent, 4), 13);
        DrawKit.Glow(dc, tip, 3, DrawKit.Accent, 0.8);
        DrawKit.DrawText(dc, "w", new Point(tip.X + 10, tip.Y - 8), 15, DrawKit.Accent, canvas.Dpi, System.Windows.HorizontalAlignment.Left, DrawKit.MathFace);
    }
}
