using ScottPlot;
using ScottPlot.Plottables;
using WetWare.App.Controls;
using WetWare.App.ViewModels.Course.Lessons;

namespace WetWare.App.Views.Course.Lessons;

public partial class NonlinearRecurrentLessonView : LessonView
{
    private static readonly Color[] SnapshotColors = [LabPlot.Success, LabPlot.Accent2, LabPlot.Excitatory, LabPlot.Inhibitory, LabPlot.Muted];
    private readonly double[] _peak = new double[600];
    private Signal? _peakSignal;

    public NonlinearRecurrentLessonView() => InitializeComponent();

    private NonlinearRecurrentLesson Vm => (NonlinearRecurrentLesson)DataContext;

    protected override void Setup() => StylePlots(PeakPlot, ProfilePlot);

    protected override void Draw(bool full)
    {
        var ring = Vm.Ring;
        RingView.Angles = ring.Preferred;
        RingView.Rates = ring.Rates;
        RingView.Input = ring.Input;
        RingView.InputOn = ring.InputOn;
        RingView.Scale = Math.Max(10, ring.Rates.Max() * 1.05);
        RingView.PopulationVector = ring.PopulationVector;
        RingView.Render();

        if (full || _peakSignal is null)
        {
            var pp = PeakPlot.Plot;
            pp.Clear();
            _peakSignal = pp.Add.Signal(_peak, 5, LabPlot.Warning);
            _peakSignal.LineWidth = 2;
            LabPlot.Labels(PeakPlot, Loc["Plot.TimeMs"], Loc["M6.Nonlinear.PeakAxis"]);
        }
        Fill(_peak, ring.Peak, 0);
        double t1 = ring.TimeMs, t0 = t1 - _peak.Length * 5;
        _peakSignal.Data.XOffset = t0;
        PeakPlot.Plot.Axes.SetLimits(Math.Max(0, t0), Math.Max(t1, 500), 0, Math.Max(10, _peak.Max() * 1.15));
        PeakPlot.Refresh();

        if (!full && Frame % 2 == 1) return;
        var plot = ProfilePlot.Plot;
        plot.Clear();
        double[] xs = ring.Preferred;
        double top = 10;
        for (int k = 0; k < Vm.Snapshots.Count; k++)
        {
            var (label, rates) = Vm.Snapshots[k];
            var snap = plot.Add.ScatterLine(xs, rates, SnapshotColors[k % SnapshotColors.Length].WithAlpha(0.75));
            snap.LineWidth = 1.6f;
            snap.LinePattern = LinePattern.Dashed;
            snap.LegendText = label;
            top = Math.Max(top, rates.Max());
        }
        var input = plot.Add.ScatterLine(xs, ring.Input, LabPlot.Warning.WithAlpha(ring.InputOn ? 0.9 : 0.3));
        input.LineWidth = 1.2f;
        input.MarkerSize = 3;
        input.LegendText = Loc["M6.Linear.Legend.RingInput"];
        var output = plot.Add.ScatterLine(xs, ring.Rates, LabPlot.Accent);
        output.LineWidth = 2.6f;
        output.FillY = true;
        output.FillYColor = LabPlot.Accent.WithAlpha(0.12);
        output.LegendText = Loc["M6.Linear.Legend.RingOutput"];
        plot.Add.HorizontalLine(0, 1, LabPlot.Muted, LinePattern.Dotted);
        top = Math.Max(top, Math.Max(ring.Rates.Max(), ring.Input.Max())) * 1.15;
        plot.Axes.SetLimits(-180, 180, Math.Min(-2, ring.Input.Min() * 1.15), top);
        plot.ShowLegend(Alignment.UpperLeft);
        LabPlot.Labels(ProfilePlot, Loc["M6.Axis.Preferred"], Loc["M6.Axis.Rate"]);
        ProfilePlot.Refresh();
    }
}
