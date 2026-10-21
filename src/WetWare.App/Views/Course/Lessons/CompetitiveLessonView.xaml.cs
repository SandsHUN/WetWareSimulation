using System.Windows;
using System.Windows.Media;
using WetWare.App.Controls;
using WetWare.App.Controls.Course;
using WetWare.App.ViewModels.Course.Lessons;
using WetWare.Core.Course.Unsupervised;

namespace WetWare.App.Views.Course.Lessons;

public partial class CompetitiveLessonView : LessonView
{
    public CompetitiveLessonView()
    {
        InitializeComponent();
        Plane.XMin = Plane.YMin = -3.2;
        Plane.XMax = Plane.YMax = 3.2;
        Plane.Painter = Paint;
        Plane.WorldClick += (_, p) => Vm.AddCluster(p.X, p.Y);
    }

    private CompetitiveLesson Vm => (CompetitiveLesson)DataContext;

    protected override void Setup() => StylePlots(DistortionPlot);

    protected override void Draw(bool full)
    {
        Plane.Render();
        if (!full && Frame % 3 != 0) return;
        var plot = DistortionPlot.Plot;
        plot.Clear();
        var history = Vm.Learner.DistortionHistory.ToArray();
        if (history.Length > 1)
        {
            var line = plot.Add.Signal(history, 1, LabPlot.Warning);
            line.LineWidth = 2;
        }
        plot.Axes.SetLimits(0, Math.Max(50, history.Length), 0, Math.Max(0.5, history.DefaultIfEmpty(1).Max() * 1.1));
        LabPlot.Labels(DistortionPlot, Loc["Course.Axis.Updates"], Loc["M7.Competitive.DistortionAxis"]);
        DistortionPlot.Refresh();
    }

    private void Paint(DrawingContext dc, PlaneCanvas canvas)
    {
        var learner = Vm.Learner;
        var palette = DrawKit.Palette;

        if (Vm.ShowRegions && learner.Prototypes.Count > 0)
        {
            // Voronoi cells: the input region each neuron wins.
            const int cells = 48;
            double step = (canvas.XMax - canvas.XMin) / cells;
            double px = step * canvas.Scale + 0.6;
            for (int a = 0; a < cells; a++)
            {
                for (int b = 0; b < cells; b++)
                {
                    double x = canvas.XMin + (a + 0.5) * step, y = canvas.YMin + (b + 0.5) * step;
                    int k = learner.Nearest(x, y);
                    var p = canvas.ToScreen(x, y);
                    dc.DrawRectangle(DrawKit.Brush(palette[k % palette.Length], 0.09), null, new Rect(p.X - px / 2, p.Y - px / 2, px, px));
                }
            }
        }

        for (int n = 0; n < learner.Points.Count; n++)
        {
            var (x, y) = learner.Points[n];
            int k = n < learner.Assignment.Length ? learner.Assignment[n] : 0;
            dc.DrawEllipse(DrawKit.Brush(palette[k % palette.Length], 0.6), null, canvas.ToScreen(x, y), 2.6, 2.6);
        }

        if (learner.Mode == PrototypeMode.SelfOrganizingMap)
        {
            var chain = learner.Prototypes.Select(p => canvas.ToScreen(p.X, p.Y)).ToList();
            dc.DrawGeometry(null, DrawKit.Pen(DrawKit.Text, 2, 0.8), DrawKit.Polyline(chain));
        }

        for (int k = 0; k < learner.Prototypes.Count; k++)
        {
            var trail = learner.Trails[k].Select(p => canvas.ToScreen(p.X, p.Y)).ToList();
            if (trail.Count > 1) dc.DrawGeometry(null, DrawKit.Pen(palette[k % palette.Length], 1.4, 0.45), DrawKit.Polyline(trail));
        }

        if (learner.LastInput is { } u && learner.LastWinner >= 0)
        {
            var w = learner.Prototypes[learner.LastWinner];
            var pu = canvas.ToScreen(u.X, u.Y);
            dc.DrawLine(DrawKit.Pen(DrawKit.Text, 1.2, 0.7, dashed: true), pu, canvas.ToScreen(w.X, w.Y));
            DrawKit.Glow(dc, pu, 3.5, DrawKit.Text, 0.8);
        }

        for (int k = 0; k < learner.Prototypes.Count; k++)
        {
            var p = canvas.ToScreen(learner.Prototypes[k].X, learner.Prototypes[k].Y);
            var color = palette[k % palette.Length];
            DrawKit.Glow(dc, p, k == learner.LastWinner ? 9 : 7, color);
            dc.DrawEllipse(null, DrawKit.Pen(Color.FromRgb(0x06, 0x10, 0x1F), 2), p, 7, 7);
            DrawKit.DrawText(dc, $"w{k + 1}", new Point(p.X + 10, p.Y - 10), 11, DrawKit.Text, canvas.Dpi, System.Windows.HorizontalAlignment.Left, DrawKit.MathFace);
        }
    }
}
