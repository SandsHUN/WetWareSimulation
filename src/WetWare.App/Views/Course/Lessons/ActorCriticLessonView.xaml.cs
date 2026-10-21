using ScottPlot;
using WetWare.App.Controls;
using WetWare.App.ViewModels.Course.Lessons;

namespace WetWare.App.Views.Course.Lessons;

public partial class ActorCriticLessonView : LessonView
{
    private int _lastTrials = -1;

    public ActorCriticLessonView() => InitializeComponent();

    private ActorCriticLesson Vm => (ActorCriticLesson)DataContext;

    protected override void Setup() => StylePlots(ValuePlot, PolicyPlot);

    protected override void Draw(bool full)
    {
        var maze = Vm.Maze;
        Barn.Maze = maze;
        Barn.Progress = Vm.Animation;
        Barn.ValueLabel = "V";
        Barn.Render();

        if (!full && maze.Trials == _lastTrials) return;
        _lastTrials = maze.Trials;
        double[] xs = Enumerable.Range(0, maze.ValueHistory.Count).Select(i => (double)i).ToArray();

        var vp = ValuePlot.Plot;
        vp.Clear();
        (string Name, Func<(double VA, double VB, double VC), double> Get, Color Color)[] values =
        [
            ("V(A)", h => h.VA, LabPlot.Success),
            ("V(B)", h => h.VB, LabPlot.Accent),
            ("V(C)", h => h.VC, LabPlot.Excitatory),
        ];
        foreach (var (name, get, color) in values)
        {
            var line = vp.Add.ScatterLine(xs, maze.ValueHistory.Select(get).ToArray(), color);
            line.LineWidth = 2;
            line.LegendText = name;
        }
        if (!maze.ActorLearns)
        {
            var target = maze.RandomPolicyValues;
            foreach (var (v, color) in new[] { (target.A, LabPlot.Success), (target.B, LabPlot.Accent), (target.C, LabPlot.Excitatory) })
                vp.Add.HorizontalLine(v, 1, color.WithAlpha(0.6), LinePattern.Dotted);
        }
        double vMax = Math.Max(1, maze.Rewards.Cast<double>().Max()) * 1.1;
        vp.Axes.SetLimits(0, Math.Max(30, xs.Length), -0.2, vMax);
        vp.ShowLegend(Alignment.LowerRight);
        LabPlot.Labels(ValuePlot, Loc["M8.Td.TrialAxis"], "V(s)");
        ValuePlot.Refresh();

        var pp = PolicyPlot.Plot;
        pp.Clear();
        (string Name, Func<(double PA, double PB, double PC), double> Get, Color Color)[] policies =
        [
            ("P(R|A)", h => h.PA, LabPlot.Success),
            ("P(R|B)", h => h.PB, LabPlot.Accent),
            ("P(R|C)", h => h.PC, LabPlot.Excitatory),
        ];
        foreach (var (name, get, color) in policies)
        {
            var line = pp.Add.ScatterLine(xs, maze.PolicyHistory.Select(get).ToArray(), color);
            line.LineWidth = 2;
            line.LegendText = name;
        }
        pp.Add.HorizontalLine(0.5, 1, LabPlot.Muted, LinePattern.Dotted);
        pp.Axes.SetLimits(0, Math.Max(30, xs.Length), -0.03, 1.03);
        pp.ShowLegend(Alignment.LowerRight);
        LabPlot.Labels(PolicyPlot, Loc["M8.Td.TrialAxis"], Loc["M8.ActorCritic.PolicyAxis"]);
        PolicyPlot.Refresh();
    }
}
