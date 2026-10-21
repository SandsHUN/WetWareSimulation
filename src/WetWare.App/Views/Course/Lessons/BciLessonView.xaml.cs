using ScottPlot;
using ScottPlot.Plottables;
using WetWare.App.Controls;
using WetWare.App.ViewModels.Course.Lessons;
using WetWare.Core.Course.Bci;

namespace WetWare.App.Views.Course.Lessons;

public partial class BciLessonView : LessonView
{
    private readonly double[] _angle = new double[1200];
    private readonly double[] _target = new double[1200];
    private Signal? _angleSignal, _targetSignal;
    private double _lastHours = -1;
    private int _lastDelays = -1;

    public BciLessonView() => InitializeComponent();

    private BciLesson Vm => (BciLesson)DataContext;

    protected override void Setup() => StylePlots(AnglePlot, StaPlot, StrengthPlot, WindowPlot);

    protected override void Draw(bool full)
    {
        var vm = Vm;
        Loop.Bridge = vm.Fes;
        Loop.CortexLabel = Loc["M8.Bci.Label.Cortex"];
        Loop.ChipLabel = "Neurochip";
        Loop.LesionLabel = Loc["M8.Bci.Label.Lesion"];
        Loop.FlexorLabel = Loc["M8.Bci.Label.Flexor"];
        Loop.ExtensorLabel = Loc["M8.Bci.Label.Extensor"];
        Loop.Render();

        if (full || _angleSignal is null)
        {
            var ap = AnglePlot.Plot;
            ap.Clear();
            _targetSignal = ap.Add.Signal(_target, 5, LabPlot.Warning.WithAlpha(0.8));
            _targetSignal.LineWidth = 1.6f;
            _targetSignal.LinePattern = LinePattern.Dashed;
            _targetSignal.LegendText = Loc["M8.Bci.Legend.Target"];
            _angleSignal = ap.Add.Signal(_angle, 5, LabPlot.Accent);
            _angleSignal.LineWidth = 2.2f;
            _angleSignal.LegendText = Loc["M8.Bci.Legend.Wrist"];
            ap.ShowLegend(Alignment.UpperLeft);
            LabPlot.Labels(AnglePlot, Loc["Plot.TimeMs"], Loc["M8.Bci.AngleAxis"]);
        }
        Fill(_angle, vm.Fes.Angle, 0);
        Fill(_target, vm.Fes.Target, 0);
        double t1 = vm.Fes.TimeMs, t0 = t1 - _angle.Length * 5;
        _angleSignal.Data.XOffset = t0;
        _targetSignal!.Data.XOffset = t0;
        AnglePlot.Plot.Axes.SetLimits(Math.Max(0, t0), Math.Max(t1, 2000), -75, 75);
        AnglePlot.Refresh();

        var cond = vm.Conditioning;
        if (!full && cond.HoursConditioned == _lastHours && cond.EffectByDelay.Count == _lastDelays && Frame % 15 != 0) return;
        _lastHours = cond.HoursConditioned;
        _lastDelays = cond.EffectByDelay.Count;

        double[] ms = Enumerable.Range(0, SpikeTriggeredConditioning.StaLength).Select(i => (double)i).ToArray();
        var sp = StaPlot.Plot;
        sp.Clear();
        var before = sp.Add.ScatterLine(ms, vm.BaselineSta, LabPlot.Muted);
        before.LineWidth = 1.6f;
        before.LegendText = Loc["M8.Bci.Legend.Before"];
        var after = sp.Add.ScatterLine(ms, vm.CurrentSta, LabPlot.Success);
        after.LineWidth = 2.2f;
        after.LegendText = Loc["M8.Bci.Legend.Now"];
        var spike = sp.Add.VerticalLine(0, 1.2f, LabPlot.Accent2, LinePattern.Dashed);
        spike.Text = Loc["M8.Bci.SpikeAt0"];
        sp.Axes.SetLimits(-2, SpikeTriggeredConditioning.StaLength, 0.6, 2.4);
        sp.ShowLegend(Alignment.UpperRight);
        LabPlot.Labels(StaPlot, Loc["M8.Bci.StaAxis"], "EMG");
        StaPlot.Refresh();

        var st = StrengthPlot.Plot;
        st.Clear();
        var hist = cond.History;
        var line = st.Add.ScatterLine(hist.Select(h => h.Hours).ToArray(), hist.Select(h => h.Strength / cond.InitialStrength * 100).ToArray(), LabPlot.Success);
        line.LineWidth = 2.2f;
        st.Add.HorizontalLine(100, 1, LabPlot.Muted, LinePattern.Dotted);
        st.Axes.SetLimits(0, Math.Max(10, cond.HoursConditioned + 0.5), 0, Math.Max(220, hist.Max(h => h.Strength) / cond.InitialStrength * 110));
        LabPlot.Labels(StrengthPlot, Loc["M8.Bci.HoursAxis"], Loc["M8.Bci.StrengthAxis"]);
        StrengthPlot.Refresh();

        var wp = WindowPlot.Plot;
        wp.Clear();
        double[] delays = Enumerable.Range(0, 101).Select(i => (double)i).ToArray();
        var model = wp.Add.ScatterLine(delays, delays.Select(cond.Window).ToArray(), LabPlot.Accent.WithAlpha(0.7));
        model.LineWidth = 2;
        model.LegendText = Loc["M8.Bci.Legend.Window"];
        if (cond.EffectByDelay.Count > 0)
        {
            // Measured effects, rescaled to the window's axis: (strength ratio − 1) relative to the best measurement.
            double best = Math.Max(1e-9, cond.EffectByDelay.Values.Max() - 1);
            var pts = wp.Add.Markers(cond.EffectByDelay.Keys.ToArray(), cond.EffectByDelay.Values.Select(v => (v - 1) / best).ToArray(),
                MarkerShape.FilledCircle, 10, LabPlot.Warning);
            pts.LegendText = Loc["M8.Bci.Legend.Measured"];
        }
        var now = wp.Add.VerticalLine(vm.Delay, 1.4f, LabPlot.Warning, LinePattern.Dashed);
        now.Text = $"{vm.Delay:0} ms";
        wp.Axes.SetLimits(0, 100, -0.1, 1.1);
        wp.ShowLegend(Alignment.UpperRight);
        LabPlot.Labels(WindowPlot, Loc["M8.Bci.DelayAxis"], Loc["M8.Bci.WindowAxis"]);
        WindowPlot.Refresh();
    }
}
