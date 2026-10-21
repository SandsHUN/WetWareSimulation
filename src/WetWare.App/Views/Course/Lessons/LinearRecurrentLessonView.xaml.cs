using ScottPlot;
using ScottPlot.Plottables;
using WetWare.App.Controls;
using WetWare.App.ViewModels.Course.Lessons;
using WetWare.Core.Course.RateNetworks;

namespace WetWare.App.Views.Course.Lessons;

public partial class LinearRecurrentLessonView : LessonView
{
    private readonly double[] _c = new double[1500];
    private readonly double[] _h = new double[1500];
    private Signal? _cSignal, _hSignal;

    public LinearRecurrentLessonView() => InitializeComponent();

    private LinearRecurrentLesson Vm => (LinearRecurrentLesson)DataContext;

    protected override void Setup() => StylePlots(ModePlot, RingPlot);

    protected override void Draw(bool full)
    {
        var vm = Vm;
        if (full || _cSignal is null) BuildModePlot();

        var mode = vm.Mode;
        Fill(_c, mode.Trace, 0);
        Fill(_h, mode.InputTrace, 0);
        for (int i = 0; i < _h.Length; i++) _h[i] *= 10; // input drawn ×10 so pulses are visible next to c
        double t1 = mode.TimeMs, t0 = t1 - _c.Length * EigenmodeIntegrator.SampleMs;
        _cSignal!.Data.XOffset = t0;
        _hSignal!.Data.XOffset = t0;
        double span = Math.Max(4, Math.Max(_c.Max(Math.Abs), 1) * 1.2);
        ModePlot.Plot.Axes.SetLimits(t0, t1, -Math.Min(span, 60), Math.Min(span, 60));
        ModePlot.Refresh();

        Eye.Gaze = mode.C / 8;
        Eye.Render();

        if (Frame % 2 == 0 || full) DrawRing();
    }

    private void BuildModePlot()
    {
        var plot = ModePlot.Plot;
        plot.Clear();
        _hSignal = plot.Add.Signal(_h, EigenmodeIntegrator.SampleMs, LabPlot.Warning.WithAlpha(0.7));
        _hSignal.LineWidth = 1.2f;
        _hSignal.LegendText = Loc["M6.Linear.Legend.Input"];
        _cSignal = plot.Add.Signal(_c, EigenmodeIntegrator.SampleMs, LabPlot.Accent);
        _cSignal.LineWidth = 2.2f;
        _cSignal.LegendText = Loc["M6.Linear.Legend.Mode"];
        plot.Add.HorizontalLine(0, 1, LabPlot.Muted, LinePattern.Dotted);
        plot.ShowLegend(Alignment.UpperLeft);
        LabPlot.Labels(ModePlot, Loc["Plot.TimeMs"], "c(t)");
    }

    private void DrawRing()
    {
        var ring = Vm.Ring;
        var plot = RingPlot.Plot;
        plot.Clear();
        double[] xs = ring.Preferred;
        var input = plot.Add.ScatterLine(xs, ring.Input, LabPlot.Warning.WithAlpha(0.85));
        input.LineWidth = 1.2f;
        input.MarkerSize = 3.5f;
        input.LegendText = Loc["M6.Linear.Legend.RingInput"];
        double[] shown = ring.Rates.Select(r => Math.Clamp(r, -200, 200)).ToArray();
        var output = plot.Add.ScatterLine(xs, shown, LabPlot.Accent);
        output.LineWidth = 2.6f;
        output.FillY = true;
        output.FillYColor = LabPlot.Accent.WithAlpha(0.12);
        output.LegendText = Loc["M6.Linear.Legend.RingOutput"];
        plot.Add.HorizontalLine(0, 1, LabPlot.Muted, LinePattern.Dotted);
        double max = Math.Max(10, Math.Max(shown.Max(), ring.Input.Max()) * 1.15);
        double min = Math.Min(-4, Math.Min(shown.Min(), ring.Input.Min()) * 1.15);
        plot.Axes.SetLimits(-180, 180, min, Math.Min(max, 210));
        plot.ShowLegend(Alignment.UpperLeft);
        LabPlot.Labels(RingPlot, Loc["M6.Axis.Preferred"], Loc["M6.Axis.Rate"]);
        RingPlot.Refresh();
    }
}
