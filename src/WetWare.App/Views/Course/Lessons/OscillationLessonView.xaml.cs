using System.Windows;
using System.Windows.Media;
using ScottPlot;
using ScottPlot.Plottables;
using WetWare.App.Controls;
using WetWare.App.Controls.Course;
using WetWare.App.ViewModels.Course.Lessons;
using WetWare.Core.Course.RateNetworks;

namespace WetWare.App.Views.Course.Lessons;

public partial class OscillationLessonView : LessonView
{
    private readonly double[] _e = new double[1000];
    private readonly double[] _i = new double[1000];
    private Signal? _eSignal, _iSignal;
    private (double TauI, double Mee) _spectrumState = (-1, -1);

    public OscillationLessonView()
    {
        InitializeComponent();
        Plane.XLabel = "v_E (Hz)";
        Plane.YLabel = "v_I (Hz)";
        Plane.EqualAspect = false;
        Plane.GridStep = 10;
        Plane.Painter = PaintPhasePlane;
        Plane.WorldClick += (_, p) => Vm.SetState(p.X, p.Y);
    }

    private OscillationLesson Vm => (OscillationLesson)DataContext;

    protected override void Setup() => StylePlots(EigenPlot, TracePlot);

    protected override void Draw(bool full)
    {
        var net = Vm.Net;
        double eMax = 60, iMax = 45;
        for (int k = 0; k < net.Trajectory.Count; k++)
        {
            eMax = Math.Max(eMax, net.Trajectory[k].E * 1.1);
            iMax = Math.Max(iMax, net.Trajectory[k].I * 1.1);
        }
        Plane.XMin = 0;
        Plane.XMax = Math.Min(eMax, 200);
        Plane.YMin = 0;
        Plane.YMax = Math.Min(iMax, 200);
        Plane.GridStep = Plane.XMax > 100 ? 20 : 10;
        Plane.Render();

        if (full || _eSignal is null)
        {
            var tp = TracePlot.Plot;
            tp.Clear();
            _eSignal = tp.Add.Signal(_e, ExcitatoryInhibitoryNetwork.SampleMs, LabPlot.Excitatory);
            _eSignal.LineWidth = 2;
            _eSignal.LegendText = "v_E";
            _iSignal = tp.Add.Signal(_i, ExcitatoryInhibitoryNetwork.SampleMs, LabPlot.Inhibitory);
            _iSignal.LineWidth = 2;
            _iSignal.LegendText = "v_I";
            tp.ShowLegend(Alignment.UpperLeft);
            LabPlot.Labels(TracePlot, Loc["Plot.TimeMs"], Loc["M6.Axis.Rate"]);
        }
        var fp = net.FixedPoint;
        Fill(_e, net.TraceE, net.TraceE.Count > 0 ? net.TraceE[0] : fp.E);
        Fill(_i, net.TraceI, net.TraceI.Count > 0 ? net.TraceI[0] : fp.I);
        double t1 = net.TraceE.Count * ExcitatoryInhibitoryNetwork.SampleMs, t0 = t1 - _e.Length * ExcitatoryInhibitoryNetwork.SampleMs;
        _eSignal!.Data.XOffset = t0;
        _iSignal!.Data.XOffset = t0;
        TracePlot.Plot.Axes.SetLimits(t0, t1, 0, Math.Max(40, Math.Max(_e.Max(), _i.Max()) * 1.1));
        TracePlot.Refresh();

        if (full || _spectrumState != (Vm.TauI, Vm.Mee)) DrawSpectrum();
    }

    private void PaintPhasePlane(DrawingContext dc, PlaneCanvas canvas)
    {
        var net = Vm.Net;
        // Vector field: normalised arrows showing where the state flows.
        int nx = 15, ny = 11;
        var arrowPen = DrawKit.Pen(DrawKit.Muted, 1.1, 0.4);
        for (int a = 0; a < nx; a++)
        {
            for (int b = 0; b < ny; b++)
            {
                double e = canvas.XMin + (a + 0.5) * (canvas.XMax - canvas.XMin) / nx;
                double i = canvas.YMin + (b + 0.5) * (canvas.YMax - canvas.YMin) / ny;
                double de = (-e + Math.Max(0, net.MEE * e + net.MEI * i - net.GammaE)) / net.TauE;
                double di = (-i + Math.Max(0, net.MIE * e + net.MII * i - net.GammaI)) / net.TauI;
                var p = canvas.ToScreen(e, i);
                var v = new Vector(de, -di);
                if (v.Length < 1e-9) continue;
                v.Normalize();
                DrawKit.Arrow(dc, p - v * 7, p + v * 7, arrowPen, 5);
            }
        }

        // Nullclines.
        var eLine = new List<System.Windows.Point>();
        var iLine = new List<System.Windows.Point>();
        for (int k = 0; k <= 100; k++)
        {
            double e = canvas.XMin + k * (canvas.XMax - canvas.XMin) / 100;
            eLine.Add(canvas.ToScreen(e, net.ENullcline(e)));
            iLine.Add(canvas.ToScreen(e, net.INullcline(e)));
        }
        dc.DrawGeometry(null, DrawKit.Pen(DrawKit.Success, 2.2, 0.9), DrawKit.Polyline(eLine));
        dc.DrawGeometry(null, DrawKit.Pen(DrawKit.Accent2, 2.2, 0.9), DrawKit.Polyline(iLine));

        // Trajectory with a fading tail.
        int n = net.Trajectory.Count;
        for (int k = 1; k < n; k++)
        {
            double age = (double)k / n;
            var a0 = canvas.ToScreen(net.Trajectory[k - 1].E, net.Trajectory[k - 1].I);
            var a1 = canvas.ToScreen(net.Trajectory[k].E, net.Trajectory[k].I);
            dc.DrawLine(DrawKit.Pen(DrawKit.Warning, 1 + 1.6 * age, 0.15 + 0.85 * age), a0, a1);
        }

        var fp = net.FixedPoint;
        var fps = canvas.ToScreen(fp.E, fp.I);
        if (net.IsStable) dc.DrawEllipse(DrawKit.Brush(DrawKit.Text), null, fps, 5, 5);
        else dc.DrawEllipse(DrawKit.Brush(DrawKit.Panel), DrawKit.Pen(DrawKit.Text, 2), fps, 6, 6);
        DrawKit.DrawText(dc, Loc["M6.Oscillation.FixedPoint"], new System.Windows.Point(fps.X + 10, fps.Y + 12), 10.5, DrawKit.Muted, canvas.Dpi);
        DrawKit.Glow(dc, canvas.ToScreen(net.E, net.I), 5, DrawKit.Warning);
    }

    private void DrawSpectrum()
    {
        _spectrumState = (Vm.TauI, Vm.Mee);
        var net = Vm.Net;
        var plot = EigenPlot.Plot;
        plot.Clear();

        // Locus of the eigenvalues as τ_I sweeps 10–80 ms.
        var probe = new ExcitatoryInhibitoryNetwork { MEE = net.MEE };
        var re1 = new List<double>();
        var im1 = new List<double>();
        var im2 = new List<double>();
        for (double tau = 10; tau <= 80; tau += 1)
        {
            probe.TauI = tau;
            var (l1, l2) = probe.Eigenvalues;
            re1.Add(l1.Real);
            im1.Add(l1.Imaginary);
            im2.Add(l2.Imaginary);
        }
        var locus1 = plot.Add.ScatterLine(re1.ToArray(), im1.ToArray(), LabPlot.Muted.WithAlpha(0.5));
        locus1.LineWidth = 1.2f;
        var locus2 = plot.Add.ScatterLine(re1.ToArray(), im2.ToArray(), LabPlot.Muted.WithAlpha(0.5));
        locus2.LineWidth = 1.2f;

        var border = plot.Add.VerticalLine(0, 1.5f, LabPlot.Danger, LinePattern.Dashed);
        border.Text = "Re = 0";
        plot.Add.HorizontalLine(0, 1, LabPlot.Muted, LinePattern.Dotted);
        var (a, b) = net.Eigenvalues;
        var color = net.IsStable ? LabPlot.Success : LabPlot.Warning;
        plot.Add.Marker(a.Real, a.Imaginary, MarkerShape.FilledCircle, 12, color);
        plot.Add.Marker(b.Real, b.Imaginary, MarkerShape.FilledCircle, 12, color);
        double reSpan = Math.Max(40, Math.Max(Math.Abs(a.Real), Math.Abs(b.Real)) * 1.3);
        double imSpan = Math.Max(60, Math.Max(im1.Max(), Math.Abs(a.Imaginary)) * 1.2);
        plot.Axes.SetLimits(-reSpan, reSpan * 0.6, -imSpan, imSpan);
        LabPlot.Labels(EigenPlot, "Re λ (1/s)", "Im λ (1/s)");
        EigenPlot.Refresh();
    }
}
