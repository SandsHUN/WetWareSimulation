using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ScottPlot;
using WetWare.App.Controls;
using WetWare.App.Controls.Course;
using WetWare.App.ViewModels.Course.Lessons;
using Color = System.Windows.Media.Color;

namespace WetWare.App.Views.Course.Lessons;

public partial class MixtureLessonView : LessonView
{
    public MixtureLessonView()
    {
        InitializeComponent();
        Plane.XMin = Plane.YMin = -3.2;
        Plane.XMax = Plane.YMax = 3.2;
        Plane.Painter = Paint;
        Plane.MouseMove += OnPlaneMouseMove;
    }

    private MixtureLesson Vm => (MixtureLesson)DataContext;

    protected override void Setup() => StylePlots(BayesPlot, LikelihoodPlot);

    private void OnPlaneMouseMove(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(Plane);
        if (!Plane.Area.Contains(p)) return;
        var (x, y) = Plane.ToWorld(p);
        Vm.SetProbe(x, y);
    }

    protected override void Draw(bool full)
    {
        Plane.Render();
        DrawBayes();
        DrawLikelihood();
    }

    private static Color Mix(double[] weights)
    {
        double r = 0, g = 0, b = 0;
        for (int k = 0; k < weights.Length; k++)
        {
            var c = DrawKit.Palette[k % DrawKit.Palette.Length];
            r += weights[k] * c.R;
            g += weights[k] * c.G;
            b += weights[k] * c.B;
        }
        return Color.FromRgb((byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(b, 0, 255));
    }

    private void Paint(DrawingContext dc, PlaneCanvas canvas)
    {
        var model = Vm.Model;
        int k = model.Components.Count;
        var resp = model.Responsibilities;
        var w = new double[k];
        for (int n = 0; n < model.Points.Count; n++)
        {
            // Soft assignment: the colour is the responsibility-weighted blend of the component colours.
            for (int j = 0; j < k; j++) w[j] = n < resp.GetLength(0) ? resp[n, j] : 1.0 / k;
            dc.DrawEllipse(DrawKit.Brush(Mix(w), 0.75), null, canvas.ToScreen(model.Points[n].X, model.Points[n].Y), 2.8, 2.8);
        }

        foreach (var (x, y, c) in Vm.Dreams)
            dc.DrawEllipse(null, DrawKit.Pen(DrawKit.Palette[c % DrawKit.Palette.Length], 1.3, 0.9), canvas.ToScreen(x, y), 3.5, 3.5);

        for (int j = 0; j < k; j++)
        {
            var comp = model.Components[j];
            var color = DrawKit.Palette[j % DrawKit.Palette.Length];
            var (a, b, angle) = comp.Ellipse;
            foreach (double s in new[] { 1.0, 2.0 })
            {
                var pts = new List<System.Windows.Point>();
                for (int t = 0; t <= 64; t++)
                {
                    double phi = t * 2 * Math.PI / 64;
                    double ex = s * a * Math.Cos(phi), ey = s * b * Math.Sin(phi);
                    pts.Add(canvas.ToScreen(comp.Mx + ex * Math.Cos(angle) - ey * Math.Sin(angle), comp.My + ex * Math.Sin(angle) + ey * Math.Cos(angle)));
                }
                dc.DrawGeometry(DrawKit.Brush(color, s == 1 ? 0.08 : 0.03), DrawKit.Pen(color, s == 1 ? 2 : 1.2, s == 1 ? 0.95 : 0.5, dashed: s == 2), DrawKit.Polyline(pts, closed: true));
            }
            var m = canvas.ToScreen(comp.Mx, comp.My);
            DrawKit.Glow(dc, m, 6, color);
            DrawKit.DrawText(dc, $"π{j + 1} = {comp.Pi.ToString("0.00", Loc.Culture)}", new System.Windows.Point(m.X + 10, m.Y - 12), 11.5, DrawKit.Text, canvas.Dpi);
        }

        if (Vm.Probe is { } probe)
        {
            var p = canvas.ToScreen(probe.X, probe.Y);
            var pen = DrawKit.Pen(DrawKit.Text, 1.2, 0.8);
            dc.DrawLine(pen, new System.Windows.Point(p.X - 9, p.Y), new System.Windows.Point(p.X + 9, p.Y));
            dc.DrawLine(pen, new System.Windows.Point(p.X, p.Y - 9), new System.Windows.Point(p.X, p.Y + 9));
            dc.DrawEllipse(DrawKit.Brush(Mix(model.Posterior(probe.X, probe.Y))), DrawKit.Pen(DrawKit.Text, 1.5), p, 5, 5);
        }
    }

    private void DrawBayes()
    {
        var model = Vm.Model;
        var plot = BayesPlot.Plot;
        plot.Clear();
        var probe = Vm.Probe ?? (0, 0);
        int k = model.Components.Count;
        var likelihood = model.Components.Select(c => c.Density(probe.X, probe.Y)).ToArray();
        double lMax = Math.Max(1e-12, likelihood.Max());
        var posterior = model.Posterior(probe.X, probe.Y);
        var bars = new List<Bar>();
        var ticks = new List<Tick>();
        for (int j = 0; j < k; j++)
        {
            var wc = DrawKit.Palette[j % DrawKit.Palette.Length];
            var c = new ScottPlot.Color(wc.R, wc.G, wc.B);
            double x0 = j * 4;
            bars.Add(new Bar { Position = x0, Value = model.Components[j].Pi, FillColor = c.WithAlpha(0.35), LineWidth = 0, Size = 0.85 });
            bars.Add(new Bar { Position = x0 + 1, Value = likelihood[j] / lMax, FillColor = c.WithAlpha(0.6), LineWidth = 0, Size = 0.85 });
            bars.Add(new Bar { Position = x0 + 2, Value = posterior[j], FillColor = c, LineWidth = 0, Size = 0.85 });
            ticks.Add(Tick.Major(x0, Loc["M7.Em.Prior"]));
            ticks.Add(Tick.Major(x0 + 1, Loc["M7.Em.LikelihoodShort"]));
            ticks.Add(Tick.Major(x0 + 2, Loc["M7.Em.Posterior"]));
        }
        plot.Add.Bars(bars);
        plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks.ToArray());
        plot.Axes.SetLimits(-0.8, k * 4 - 1.2, 0, 1.08);
        LabPlot.Labels(BayesPlot, "", Loc["M7.Em.BayesAxis"]);
        BayesPlot.Refresh();
    }

    private void DrawLikelihood()
    {
        var history = Vm.Model.LogLikelihoodHistory;
        var plot = LikelihoodPlot.Plot;
        plot.Clear();
        if (history.Count > 0)
        {
            double[] xs = Enumerable.Range(0, history.Count).Select(i => (double)i).ToArray();
            var line = plot.Add.ScatterLine(xs, history.ToArray(), LabPlot.Success);
            line.LineWidth = 2.2f;
            line.MarkerSize = 6;
            double min = history.Min(), max = history.Max();
            plot.Axes.SetLimits(0, Math.Max(5, history.Count), min - 0.05 - (max - min) * 0.1, max + 0.05 + (max - min) * 0.1);
        }
        LabPlot.Labels(LikelihoodPlot, Loc["M7.Em.IterationAxis"], "⟨log p(u)⟩");
        LikelihoodPlot.Refresh();
    }
}
