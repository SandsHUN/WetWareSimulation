using System.Windows;
using System.Windows.Controls;
using ScottPlot;
using WetWare.App.Controls;
using WetWare.App.Localization;
using WetWare.App.ViewModels;

namespace WetWare.App.Views;

public partial class StdpLabView : UserControl
{
    private StdpLabViewModel? _vm;
    private int _lastHistoryCount = -1;

    public StdpLabView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = DataContext as StdpLabViewModel;
        if (_vm is null) return;
        LabPlot.Apply(WindowPlot);
        LabPlot.Apply(WeightPlot);
        _vm.Updated += OnUpdated;
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        _lastHistoryCount = -1;
        OnUpdated(null, EventArgs.Empty);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        _vm.Updated -= OnUpdated;
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        _lastHistoryCount = -1;
        OnUpdated(null, EventArgs.Empty);
    }

    private void OnUpdated(object? sender, EventArgs e)
    {
        if (_vm is null) return;
        var loc = LocalizationService.Instance;
        var pairing = _vm.AnimatedPairing;
        Timeline.PreLabel = loc["Stdp.Pre"];
        Timeline.PostLabel = loc["Stdp.Post"];
        Timeline.DeltaT = pairing?.DeltaT ?? _vm.DeltaT;
        Timeline.Progress = pairing is null ? 1 : _vm.AnimationProgress;
        Timeline.Weight = _vm.DisplayedWeight;
        Timeline.LastDeltaW = _vm.LastPairing?.DeltaW ?? 0;
        Timeline.Render();

        // Plots only change when a pairing was committed or parameters moved.
        int count = _vm.Experiment.History.Count;
        if (sender is not null && count == _lastHistoryCount && _vm.IsRunning) return;
        _lastHistoryCount = count;
        DrawWindow(loc);
        DrawWeights(loc);
    }

    private void DrawWindow(LocalizationService loc)
    {
        var p = _vm!.Experiment.Parameters;
        var plot = WindowPlot.Plot;
        plot.Clear();
        double[] neg = Enumerable.Range(0, 101).Select(i => -60 + i * 0.6).Where(x => x < 0).ToArray();
        double[] pos = Enumerable.Range(0, 101).Select(i => i * 0.6).Where(x => x > 0).ToArray();
        var ltd = plot.Add.ScatterLine(neg, neg.Select(p.Window).ToArray(), LabPlot.Warning);
        ltd.LineWidth = 2.5f;
        ltd.FillY = true;
        ltd.FillYColor = LabPlot.Warning.WithAlpha(0.15);
        ltd.LegendText = loc["Stdp.Ltd"];
        var ltp = plot.Add.ScatterLine(pos, pos.Select(p.Window).ToArray(), LabPlot.Success);
        ltp.LineWidth = 2.5f;
        ltp.FillY = true;
        ltp.FillYColor = LabPlot.Success.WithAlpha(0.15);
        ltp.LegendText = loc["Stdp.Ltp"];
        plot.Add.HorizontalLine(0, 1, LabPlot.Muted, LinePattern.Dotted);
        plot.Add.VerticalLine(0, 1, LabPlot.Muted, LinePattern.Dotted);
        var target = plot.Add.VerticalLine(_vm.DeltaT, 1.5f, LabPlot.Accent, LinePattern.Dashed);
        target.Text = "Δt";

        var history = _vm.Experiment.History;
        if (history.Count > 0)
        {
            var recent = history.Skip(Math.Max(0, history.Count - 200)).ToList();
            var pts = plot.Add.Markers(recent.Select(h => h.DeltaT).ToArray(), recent.Select(h => h.DeltaW).ToArray(),
                MarkerShape.FilledCircle, 6, LabPlot.Accent.WithAlpha(0.5));
            pts.LegendText = loc["Stdp.Points"];
            var last = recent[^1];
            plot.Add.Marker(last.DeltaT, last.DeltaW, MarkerShape.FilledCircle, 14, LabPlot.Accent);
        }
        LabPlot.Labels(WindowPlot, "Δt = t_post − t_pre (ms)", "Δw");
        double max = Math.Max(p.APlus, p.AMinus) * 1.2 + 1e-4;
        plot.Axes.SetLimits(-60, 60, -max, max);
        plot.ShowLegend(Alignment.UpperLeft);
        WindowPlot.Refresh();
    }

    private void DrawWeights(LocalizationService loc)
    {
        var history = _vm!.Experiment.History;
        var plot = WeightPlot.Plot;
        plot.Clear();
        var xs = new List<double> { 0 };
        var ys = new List<double> { _vm.Experiment.InitialWeight };
        foreach (var h in history)
        {
            xs.Add(h.Index);
            ys.Add(h.Weight);
        }
        var line = plot.Add.ScatterLine(xs.ToArray(), ys.ToArray(), LabPlot.Accent);
        line.LineWidth = 2;
        line.FillY = true;
        line.FillYColor = LabPlot.Accent.WithAlpha(0.1);
        plot.Add.HorizontalLine(_vm.Experiment.InitialWeight, 1, LabPlot.Muted, LinePattern.Dotted);
        LabPlot.Labels(WeightPlot, loc["Stdp.PairingAxis"], "w");
        plot.Axes.SetLimits(0, Math.Max(20, xs[^1] + 2), 0, 1.05);
        WeightPlot.Refresh();
    }
}
