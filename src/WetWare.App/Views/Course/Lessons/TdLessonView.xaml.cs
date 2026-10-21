using ScottPlot;
using WetWare.App.Controls;
using WetWare.App.ViewModels.Course.Lessons;
using WetWare.Core.Course.Reinforcement;

namespace WetWare.App.Views.Course.Lessons;

public partial class TdLessonView : LessonView
{
    private const int HeatmapTrials = 60;

    public TdLessonView() => InitializeComponent();

    private TdLesson Vm => (TdLesson)DataContext;

    protected override void Setup() => StylePlots(ValuePlot, DopaminePlot, ShiftPlot);

    protected override void Draw(bool full)
    {
        var td = Vm.Td;
        var last = td.Last;
        if (last is null) return;
        double[] t = Enumerable.Range(0, TdConditioning.Steps).Select(i => (double)i).ToArray();
        bool cue = last.Kind != TrialKind.RewardOnly;

        var vp = ValuePlot.Plot;
        vp.Clear();
        var v = vp.Add.ScatterLine(t, last.Value, LabPlot.Accent);
        v.LineWidth = 2.4f;
        v.FillY = true;
        v.FillYColor = LabPlot.Accent.WithAlpha(0.12);
        v.LegendText = Loc["M8.Td.Legend.Value"];
        Markers(vp, cue, last.Reward.Any(r => r > 0));
        vp.Axes.SetLimits(0, TdConditioning.Steps - 1, -0.1, Math.Max(1.2, last.Value.Max() * 1.1));
        vp.ShowLegend(Alignment.UpperLeft);
        LabPlot.Labels(ValuePlot, "", "v(t)");
        ValuePlot.Refresh();

        // Model dopamine neuron: baseline 5 Hz plus 25 Hz per unit of TD error, rectified (rates cannot be negative).
        var dp = DopaminePlot.Plot;
        dp.Clear();
        var bars = last.Delta.Select((d, i) => new Bar
        {
            Position = i,
            Value = Math.Max(0, 5 + 25 * d),
            Size = 0.9,
            FillColor = d > 0.05 ? LabPlot.Warning : d < -0.05 ? LabPlot.Inhibitory : LabPlot.Muted.WithAlpha(0.5),
            LineWidth = 0,
        }).ToList();
        dp.Add.Bars(bars);
        var baseline = dp.Add.HorizontalLine(5, 1, LabPlot.Muted, LinePattern.Dotted);
        baseline.Text = Loc["M8.Td.Baseline"];
        Markers(dp, cue, last.Reward.Any(r => r > 0));
        dp.Axes.SetLimits(-0.5, TdConditioning.Steps - 0.5, 0, 34);
        LabPlot.Labels(DopaminePlot, Loc["M8.Td.TimeAxis"], Loc["M8.Td.DopamineAxis"]);
        DopaminePlot.Refresh();

        // δ for the most recent trials, newest at the bottom.
        var trials = td.Trials.Skip(Math.Max(0, td.Trials.Count - HeatmapTrials)).ToList();
        var matrix = new double[HeatmapTrials, TdConditioning.Steps];
        for (int r = 0; r < HeatmapTrials; r++)
        {
            int k = r - (HeatmapTrials - trials.Count);
            for (int c = 0; c < TdConditioning.Steps; c++) matrix[r, c] = k >= 0 ? trials[k].Delta[c] : double.NaN;
        }
        Heatmap.Range = 1;
        Heatmap.RowCaption = Loc["M8.Td.TrialAxis"];
        Heatmap.SetMatrix(matrix);

        var sp = ShiftPlot.Plot;
        sp.Clear();
        var history = Vm.PeakHistory;
        if (history.Count > 0)
        {
            double[] xs = Enumerable.Range(1, history.Count).Select(i => (double)i).ToArray();
            var atCue = sp.Add.ScatterLine(xs, history.Select(h => h.Cue).ToArray(), LabPlot.Success);
            atCue.LineWidth = 2.2f;
            atCue.LegendText = Loc["M8.Td.Legend.AtCue"];
            var atReward = sp.Add.ScatterLine(xs, history.Select(h => h.Reward).ToArray(), LabPlot.Warning);
            atReward.LineWidth = 2.2f;
            atReward.LegendText = Loc["M8.Td.Legend.AtReward"];
            sp.ShowLegend(Alignment.UpperRight);
        }
        sp.Add.HorizontalLine(0, 1, LabPlot.Muted, LinePattern.Dotted);
        sp.Axes.SetLimits(0, Math.Max(40, history.Count + 1), -1.1, 1.2);
        LabPlot.Labels(ShiftPlot, Loc["M8.Td.TrialAxis"], "δ");
        ShiftPlot.Refresh();
    }

    private void Markers(Plot plot, bool cue, bool rewarded)
    {
        var td = Vm.Td;
        if (cue)
        {
            var c = plot.Add.VerticalLine(td.CueTime, 1.5f, LabPlot.Success, LinePattern.Dashed);
            c.Text = Loc["M8.Td.Cue"];
        }
        var r = plot.Add.VerticalLine(td.RewardTime, 1.5f, rewarded ? LabPlot.Warning : LabPlot.Danger, LinePattern.Dashed);
        r.Text = rewarded ? Loc["M8.Td.Reward"] : Loc["M8.Td.NoReward"];
    }
}
