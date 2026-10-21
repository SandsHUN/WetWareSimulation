using ScottPlot;
using WetWare.App.Controls;
using WetWare.App.ViewModels.Course.Lessons;
using WetWare.Core.Course.Unsupervised;

namespace WetWare.App.Views.Course.Lessons;

public partial class SparseCodingLessonView : LessonView
{
    public SparseCodingLessonView()
    {
        InitializeComponent();
        Scene.PatchPicked += (_, p) => Vm.PickPatch(p.X, p.Y);
    }

    private SparseCodingLesson Vm => (SparseCodingLesson)DataContext;

    protected override void Setup() => StylePlots(CodePlot, HistoryPlot);

    protected override void Draw(bool full)
    {
        var vm = Vm;
        var model = vm.Model;
        var (img, px, py) = model.CurrentPatchLocation;
        Scene.SetImage(model.Images[img], NaturalImages.Size);
        Scene.PatchX = px;
        Scene.PatchY = py;
        Scene.Render();

        Dictionary.Activity = vm.Code;
        Dictionary.SetPatches(Enumerable.Range(0, SparseCodingModel.Bases).Select(model.Basis).ToList());
        if (vm.ShowPca && (full || PcaGrid.Tag is null))
        {
            PcaGrid.Tag = true;
            PcaGrid.SetPatches(vm.Pca.Components.Take(64).ToList());
        }

        double scale = Math.Max(1e-6, model.CurrentPatch.Max(Math.Abs));
        Triplet.SharedScale = scale;
        Triplet.SetPatches([model.CurrentPatch, vm.Prediction, vm.Residual]);

        var cp = CodePlot.Plot;
        cp.Clear();
        var bars = vm.Code.Select((v, k) => new Bar
        {
            Position = k + 1,
            Value = v,
            Size = 0.8,
            FillColor = v > 0 ? LabPlot.Accent : LabPlot.Excitatory,
            LineWidth = 0,
        }).ToList();
        cp.Add.Bars(bars);
        cp.Add.HorizontalLine(0, 1, LabPlot.Muted, LinePattern.Dotted);
        double m = Math.Max(0.5, vm.Code.Max(Math.Abs) * 1.15);
        cp.Axes.SetLimits(0, SparseCodingModel.Bases + 1, -m, m);
        LabPlot.Labels(CodePlot, Loc["M7.Sparse.CauseAxis"], "v_k");
        CodePlot.Refresh();

        if (!full && Frame % 4 != 0) return;
        var hp = HistoryPlot.Plot;
        hp.Clear();
        var err = model.ErrorHistory.ToArray();
        var spars = model.SparsityHistory.ToArray();
        if (err.Length > 1)
        {
            var e = hp.Add.Signal(err, 1, LabPlot.Warning);
            e.LineWidth = 1.6f;
            e.LegendText = Loc["M7.Sparse.Legend.Error"];
            var s = hp.Add.Signal(spars, 1, LabPlot.Accent);
            s.LineWidth = 1.6f;
            s.LegendText = Loc["M7.Sparse.Legend.Active"];
            hp.ShowLegend(Alignment.UpperRight);
        }
        hp.Axes.SetLimits(0, Math.Max(100, err.Length), 0, Math.Max(0.5, err.DefaultIfEmpty(0).Max() * 1.05));
        LabPlot.Labels(HistoryPlot, Loc["Course.Axis.Updates"], "");
        HistoryPlot.Refresh();
    }
}
