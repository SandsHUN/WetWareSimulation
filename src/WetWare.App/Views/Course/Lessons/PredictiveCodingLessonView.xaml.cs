using ScottPlot;
using WetWare.App.Controls;
using WetWare.App.ViewModels.Course.Lessons;
using WetWare.Core.Course.Unsupervised;

namespace WetWare.App.Views.Course.Lessons;

public partial class PredictiveCodingLessonView : LessonView
{
    private int _lastIteration = -1;
    private object? _lastFrames;

    public PredictiveCodingLessonView() => InitializeComponent();

    private PredictiveCodingLesson Vm => (PredictiveCodingLesson)DataContext;

    protected override void Setup() => StylePlots(CausePlot, EnergyPlot);

    protected override void Draw(bool full)
    {
        var vm = Vm;
        if (vm.Frames.Count == 0) return;
        var f = vm.Current;
        double inputNorm = Math.Sqrt(vm.Input.Sum(x => x * x));
        Hierarchy.Levels = [Loc["M7.Predictive.Level.Input"], Loc["M7.Predictive.Level.V1"], Loc["M7.Predictive.Level.V2"]];
        Hierarchy.PredictionLabel = Loc["M7.Predictive.Col.Prediction"];
        Hierarchy.ErrorLabel = Loc["M7.Predictive.Col.Error"];
        Hierarchy.Error = Math.Sqrt(f.Error.Sum(x => x * x)) / Math.Max(inputNorm, 1e-9);
        Hierarchy.Prediction = Math.Sqrt(f.Prediction.Sum(x => x * x)) / Math.Max(inputNorm, 1e-9);
        Hierarchy.Phase = vm.Phase;
        Hierarchy.Render();

        int it = (int)vm.Iteration;
        if (!full && it == _lastIteration && ReferenceEquals(_lastFrames, vm.Frames[0])) return;
        _lastIteration = it;
        _lastFrames = vm.Frames[0];

        Triplet.SharedScale = Math.Max(1e-6, vm.Input.Max(Math.Abs));
        Triplet.SetPatches([vm.Input, f.Prediction, f.Error]);

        var cp = CausePlot.Plot;
        cp.Clear();
        cp.Add.Bars(f.V.Select((v, k) => new Bar
        {
            Position = k + 1,
            Value = v,
            Size = 0.8,
            FillColor = v > 0 ? LabPlot.Accent : LabPlot.Excitatory,
            LineWidth = 0,
        }).ToList());
        double m = Math.Max(0.5, vm.Frames[^1].V.Max(Math.Abs) * 1.2);
        cp.Axes.SetLimits(0, SparseCodingModel.Bases + 1, -m, m);
        LabPlot.Labels(CausePlot, Loc["M7.Sparse.CauseAxis"], "v_k");
        CausePlot.Refresh();

        var ep = EnergyPlot.Plot;
        ep.Clear();
        double[] xs = Enumerable.Range(0, vm.Frames.Count).Select(i => (double)i).ToArray();
        var rec = ep.Add.ScatterLine(xs, vm.Frames.Select(x => x.Reconstruction).ToArray(), LabPlot.Excitatory);
        rec.LineWidth = 2;
        rec.LegendText = Loc["M7.Predictive.Legend.Error"];
        var sp = ep.Add.ScatterLine(xs, vm.Frames.Select(x => x.Sparsity).ToArray(), LabPlot.Accent);
        sp.LineWidth = 2;
        sp.LegendText = Loc["M7.Predictive.Legend.Sparsity"];
        var total = ep.Add.ScatterLine(xs, vm.Frames.Select(x => x.Reconstruction + x.Sparsity).ToArray(), LabPlot.Warning);
        total.LineWidth = 2.6f;
        total.LegendText = Loc["M7.Predictive.Legend.Total"];
        var marker = ep.Add.VerticalLine(it, 1.5f, LabPlot.Muted, LinePattern.Dashed);
        marker.Text = $"t = {it}";
        double top = vm.Frames.Max(x => x.Reconstruction + x.Sparsity) * 1.08;
        ep.Axes.SetLimits(0, vm.Frames.Count - 1, 0, Math.Max(top, 0.1));
        ep.ShowLegend(Alignment.UpperRight);
        LabPlot.Labels(EnergyPlot, Loc["M7.Predictive.IterationAxis"], Loc["M7.Predictive.EnergyAxis"]);
        EnergyPlot.Refresh();
    }
}
