using ScottPlot;
using WetWare.App.Controls;
using WetWare.App.ViewModels.Course.Lessons;
using WetWare.Core.Course.RateNetworks;

namespace WetWare.App.Views.Course.Lessons;

public partial class FeedforwardLessonView : LessonView
{
    public FeedforwardLessonView()
    {
        InitializeComponent();
        Diagram.SelectedChanged += (_, i) => Vm.Selected = i;
        Matrix.HoverChanged += (_, cell) =>
        {
            if (cell.Row >= 0) Vm.Selected = cell.Row;
        };
    }

    private FeedforwardLesson Vm => (FeedforwardLesson)DataContext;

    protected override void Setup() => StylePlots(ProfilePlot);

    protected override void Draw(bool full)
    {
        var f = Vm.Filter;
        Diagram.Inputs = f.Input;
        Diagram.Outputs = f.Output;
        Diagram.Weights = f.Weights;
        Diagram.Selected = Vm.Selected;
        Diagram.Render();

        Matrix.Range = 1;
        Matrix.HighlightRow = Vm.Selected;
        Matrix.SetMatrix(f.Weights);

        var plot = ProfilePlot.Plot;
        plot.Clear();
        double[] xs = Enumerable.Range(1, FeedforwardFilter.Size).Select(i => (double)i).ToArray();
        var bars = xs.Select((x, i) => new Bar
        {
            Position = x,
            Value = f.Input[i],
            Size = 0.8,
            FillColor = LabPlot.Warning.WithAlpha(0.28),
            LineWidth = 0,
        }).ToList();
        var inputBars = plot.Add.Bars(bars);
        inputBars.LegendText = Loc["M6.Feedforward.Legend.Input"];
        var steady = plot.Add.ScatterLine(xs, f.SteadyState, LabPlot.Muted.WithAlpha(0.6));
        steady.LinePattern = LinePattern.Dotted;
        steady.LineWidth = 1.2f;
        steady.LegendText = "W·u";
        var output = plot.Add.ScatterLine(xs, f.Output, LabPlot.Accent);
        output.LineWidth = 2.4f;
        output.MarkerSize = 4;
        output.LegendText = Loc["M6.Feedforward.Legend.Output"];
        plot.Add.HorizontalLine(0, 1, LabPlot.Muted, LinePattern.Dotted);
        var sel = plot.Add.VerticalLine(Vm.Selected + 1, 1.2f, LabPlot.Warning, LinePattern.Dashed);
        sel.Text = $"i = {Vm.Selected + 1}";
        double min = Math.Min(-0.2, Math.Min(f.SteadyState.Min(), f.Output.Min())) * 1.15;
        double max = Math.Max(1.15, Math.Max(f.SteadyState.Max(), f.Input.Max()) * 1.15);
        plot.Axes.SetLimits(0, FeedforwardFilter.Size + 1, min, max);
        plot.ShowLegend(Alignment.UpperLeft);
        LabPlot.Labels(ProfilePlot, Loc["M6.Feedforward.Axis.Neuron"], Loc["M6.Feedforward.Axis.Rate"]);
        ProfilePlot.Refresh();
    }
}
