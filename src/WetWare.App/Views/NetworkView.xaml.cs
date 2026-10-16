using System.Windows;
using System.Windows.Controls;
using ScottPlot;
using ScottPlot.Plottables;
using WetWare.App.Controls;
using WetWare.App.Localization;
using WetWare.App.ViewModels;
using WetWare.Core.Scenarios;

namespace WetWare.App.Views;

public partial class NetworkView : UserControl
{
    private const int ScopeLength = 4000;

    private readonly double[] _scope = new double[ScopeLength];
    private readonly double[] _population = new double[400];
    private readonly double[] _phaseV = new double[1500];
    private readonly double[] _phaseD = new double[1500];
    private NetworkViewModel? _vm;
    private Signal? _scopeSignal, _popSignal;
    private Scatter? _phaseTrail;
    private Dictionary<int, int> _groupColors = new();
    private int _excColor, _inhColor;

    public NetworkView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        WeightLegend.Fill = ColorMaps.LegendBrush(ColorMaps.Weight);
        HeatLegend.Fill = ColorMaps.LegendBrush(ColorMaps.Weight);
        _excColor = ColorMaps.Pack(ColorMaps.Parse("#FB7185"));
        _inhColor = ColorMaps.Pack(ColorMaps.Parse("#38BDF8"));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = DataContext as NetworkViewModel;
        if (_vm is null) return;
        foreach (var plot in new[] { PopulationPlot, ScopePlot, HistogramPlot, StdpPlot, PhasePlot }) LabPlot.Apply(plot);

        Graph.NeuronClicked += OnNeuronClicked;
        Raster.RowClicked += OnRasterRowClicked;
        _vm.FrameUpdated += OnFrame;
        _vm.NetworkReset += OnNetworkReset;
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        OnNetworkReset(null, EventArgs.Empty);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        Graph.NeuronClicked -= OnNeuronClicked;
        Raster.RowClicked -= OnRasterRowClicked;
        _vm.FrameUpdated -= OnFrame;
        _vm.NetworkReset -= OnNetworkReset;
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => BuildPlots();

    private void OnNeuronClicked(object? sender, (int Neuron, bool Right) e) => _vm?.HandleNeuronClick(e.Neuron, e.Right);

    private void OnRasterRowClicked(object? sender, int row) => _vm?.HandleNeuronClick(row, rightButton: true);

    private void OnNetworkReset(object? sender, EventArgs e)
    {
        if (_vm is null) return;
        var net = _vm.Network;
        var loc = LocalizationService.Instance;

        Graph.Network = net;
        Graph.Groups = _vm.Scenario.Groups;
        Graph.InvalidateTopology();
        Graph.HoverText = i => loc.Format("Net.Hover", i,
            net.IsInhibitory(i) ? loc["Net.Inhibitory"] : loc["Net.Excitatory"],
            net.GetVoltage(i).ToString("F1", loc.Culture), net.Incoming(i).Count, net.Outgoing(i).Count);

        _groupColors = new Dictionary<int, int>();
        foreach (var g in _vm.Scenario.Groups)
            foreach (int n in g.Neurons) _groupColors[n] = ColorMaps.Pack(ColorMaps.Parse(g.ColorHex));

        Raster.Clear();
        Raster.RowTooltip = i => loc.Format("Net.RasterHover", i, net.IsInhibitory(i) ? loc["Net.Inhibitory"] : loc["Net.Excitatory"]);

        Heatmap.RowCaption = loc["Net.Pre"];
        Heatmap.ColumnCaption = loc["Net.Post"];
        Heatmap.HoverText = (r, c, v) => double.IsNaN(v)
            ? loc.Format("Net.HeatHover.None", r, c)
            : loc.Format("Net.HeatHover", r, c, Math.Abs(v).ToString("F3", loc.Culture), v < 0 ? loc["Net.Inhibitory"] : loc["Net.Excitatory"]);
        Heatmap.Blocks = _vm.Scenario is PavlovScenario pv
            ? [new HeatmapBlock(pv.GroupA.Min(), pv.GroupA.Max(), pv.GroupB.Min(), pv.GroupB.Max(), ColorMaps.Parse("#FBBF24"), "A→B")]
            : [];
        net.FillWeightMatrix(_vm.WeightMatrix);
        Heatmap.SetMatrix(_vm.WeightMatrix);
        BuildPlots();
    }

    private void BuildPlots()
    {
        if (_vm is null) return;
        var loc = LocalizationService.Instance;
        var lif = _vm.Network.Parameters.Lif;

        PopulationPlot.Plot.Clear();
        _popSignal = PopulationPlot.Plot.Add.Signal(_population, 5, LabPlot.Accent);
        _popSignal.LineWidth = 1.5f;
        LabPlot.Labels(PopulationPlot, string.Empty, loc["Plot.PopRate"]);
        PopulationPlot.Plot.Axes.Bottom.TickLabelStyle.IsVisible = false;
        PopulationPlot.Plot.Axes.SetLimitsY(0, 60);

        ScopePlot.Plot.Clear();
        _scopeSignal = ScopePlot.Plot.Add.Signal(_scope, 0.1, LabPlot.Accent);
        _scopeSignal.LineWidth = 1.5f;
        var th = ScopePlot.Plot.Add.HorizontalLine(lif.Threshold, 1, LabPlot.Danger, LinePattern.Dashed);
        th.LegendText = loc["Plot.Threshold"];
        var rest = ScopePlot.Plot.Add.HorizontalLine(lif.RestingPotential, 1, LabPlot.Success, LinePattern.Dotted);
        rest.LegendText = loc["Plot.Rest"];
        ScopePlot.Plot.ShowLegend(Alignment.UpperLeft);
        LabPlot.Labels(ScopePlot, loc["Plot.TimeMs"], loc["Plot.VoltageMv"]);
        ScopePlot.Plot.Axes.SetLimitsY(-80, 25);

        PhasePlot.Plot.Clear();
        _phaseTrail = PhasePlot.Plot.Add.ScatterLine(_phaseV, _phaseD, LabPlot.Accent2.WithAlpha(0.8));
        _phaseTrail.LineWidth = 1.2f;
        PhasePlot.Plot.Add.HorizontalLine(0, 1, LabPlot.Muted, LinePattern.Dotted);
        var vth = PhasePlot.Plot.Add.VerticalLine(lif.Threshold, 1, LabPlot.Danger, LinePattern.Dashed);
        vth.Text = "V_th";
        var vel = PhasePlot.Plot.Add.VerticalLine(lif.RestingPotential, 1, LabPlot.Success, LinePattern.Dotted);
        vel.Text = "E_L";
        LabPlot.Labels(PhasePlot, loc["Plot.VoltageMv"], "dv/dt (mV/ms)");
        PhasePlot.Plot.Axes.SetLimits(-75, -45, -2, 4);

        UpdateHistogram();
        UpdateStdpPlot();
        OnFrame(null, new NetworkFrame { Spikes = [], StdpEvents = [], SlowUpdate = true });
    }

    private void OnFrame(object? sender, NetworkFrame frame)
    {
        if (_vm is null) return;
        var net = _vm.Network;

        foreach (var s in frame.Spikes)
        {
            int color = _groupColors.TryGetValue(s.Neuron, out int gc) ? gc : net.IsInhibitory(s.Neuron) ? _inhColor : _excColor;
            Raster.AddSpike(s.TimeMs, s.Neuron, color);
        }
        Raster.SelectedRow = net.SelectedNeuron;
        Raster.Refresh(net.TimeMs);

        if (frame.SelectedFired) Graph.NotifySelectedSpike();
        Graph.SelectedNeuron = net.SelectedNeuron;
        Graph.Render();

        // Selected neuron oscilloscope.
        var trace = net.SelectedTrace;
        int count = trace.Count;
        int pad = ScopeLength - Math.Min(count, ScopeLength);
        double rest = net.Parameters.Lif.RestingPotential;
        for (int i = 0; i < pad; i++) _scope[i] = rest;
        int skip = Math.Max(0, count - ScopeLength);
        for (int i = 0; i < ScopeLength - pad; i++) _scope[pad + i] = trace[skip + i];
        double x0 = net.TimeMs - ScopeLength * 0.1;
        if (_scopeSignal is not null) _scopeSignal.Data.XOffset = x0;
        ScopePlot.Plot.Axes.SetLimitsX(x0, net.TimeMs);
        ScopePlot.Refresh();

        // Population rate aligned with the raster window.
        var pop = net.Metrics.PopulationRate;
        int pc = pop.Count;
        int ppad = _population.Length - pc;
        for (int i = 0; i < _population.Length; i++) _population[i] = i < ppad ? 0 : pop[i - ppad];
        if (_popSignal is not null) _popSignal.Data.XOffset = net.TimeMs - _population.Length * 5;
        PopulationPlot.Plot.Axes.SetLimitsX(net.TimeMs - 2000, net.TimeMs);
        double peak = _population.Max();
        PopulationPlot.Plot.Axes.SetLimitsY(0, Math.Max(40, peak * 1.15));
        PopulationPlot.Refresh();

        if (frame.SlowUpdate)
        {
            Heatmap.Highlight = net.SelectedNeuron;
            Heatmap.SetMatrix(_vm.WeightMatrix);
            UpdateHistogram();
            UpdateStdpPlot();
            UpdatePhase();
        }
    }

    private void UpdateHistogram()
    {
        if (_vm is null) return;
        var loc = LocalizationService.Instance;
        const int bins = 20;
        var counts = _vm.Network.WeightHistogram(bins);
        var plot = HistogramPlot.Plot;
        plot.Clear();
        var bars = new List<Bar>();
        for (int b = 0; b < bins; b++)
        {
            double center = (b + 0.5) / bins * _vm.Network.Parameters.Stdp.WMax;
            bars.Add(new Bar
            {
                Position = center,
                Value = counts[b],
                Size = 1.0 / bins * 0.85,
                FillColor = Color.FromARGB((uint)ColorMaps.Weight(center)),
                LineWidth = 0,
            });
        }
        plot.Add.Bars(bars);
        LabPlot.Labels(HistogramPlot, loc["Plot.Weight"], loc["Plot.Count"]);
        plot.Axes.SetLimits(0, 1, 0, Math.Max(50, counts.Max() * 1.15));
        HistogramPlot.Refresh();
    }

    private void UpdateStdpPlot()
    {
        if (_vm is null) return;
        var loc = LocalizationService.Instance;
        var stdp = _vm.Network.Parameters.Stdp;
        var plot = StdpPlot.Plot;
        plot.Clear();

        double[] dtNeg = Enumerable.Range(0, 60).Select(i => -60 + i * 1.0).ToArray();
        double[] dtPos = Enumerable.Range(1, 60).Select(i => i * 1.0).ToArray();
        var ltd = plot.Add.ScatterLine(dtNeg, dtNeg.Select(stdp.Window).ToArray(), LabPlot.Warning);
        ltd.LineWidth = 2;
        ltd.FillY = true;
        ltd.FillYColor = LabPlot.Warning.WithAlpha(0.12);
        ltd.LegendText = "LTD";
        var ltp = plot.Add.ScatterLine(dtPos, dtPos.Select(stdp.Window).ToArray(), LabPlot.Success);
        ltp.LineWidth = 2;
        ltp.FillY = true;
        ltp.FillYColor = LabPlot.Success.WithAlpha(0.12);
        ltp.LegendText = "LTP";
        plot.Add.HorizontalLine(0, 1, LabPlot.Muted, LinePattern.Dotted);
        plot.Add.VerticalLine(0, 1, LabPlot.Muted, LinePattern.Dotted);

        var recent = _vm.RecentStdp.Where(e => Math.Abs(e.DeltaT) <= 60).ToList();
        if (recent.Count > 0)
        {
            var xs = recent.Select(e => e.DeltaT).ToArray();
            var ys = recent.Select(e => e.DeltaW).ToArray();
            var pts = plot.Add.Markers(xs, ys, MarkerShape.FilledCircle, 5, LabPlot.Accent.WithAlpha(0.55));
            pts.LegendText = loc["Plot.StdpEvents"];
            var last = recent[^1];
            plot.Add.Marker(last.DeltaT, last.DeltaW, MarkerShape.FilledCircle, 13, LabPlot.Accent);
        }
        LabPlot.Labels(StdpPlot, "Δt = t_post − t_pre (ms)", "Δw");
        double yMax = Math.Max(stdp.Window(0.5), recent.Count > 0 ? recent.Max(e => e.DeltaW) : 0) * 1.25;
        double yMin = Math.Min(stdp.Window(-0.5), recent.Count > 0 ? recent.Min(e => e.DeltaW) : 0) * 1.25;
        plot.Axes.SetLimits(-60, 60, Math.Min(-1e-3, yMin), Math.Max(1e-3, yMax));
        plot.ShowLegend(Alignment.UpperLeft);
        StdpPlot.Refresh();
    }

    private void UpdatePhase()
    {
        if (_vm is null || _phaseTrail is null) return;
        var trace = _vm.Network.SelectedTrace;
        int n = trace.Count;
        if (n < 3) return;
        int len = _phaseV.Length;
        int start = Math.Max(1, n - len);
        double lastV = trace[start - 1];
        double lastD = 0;
        for (int i = 0; i < len; i++)
        {
            int idx = start + i;
            if (idx < n)
            {
                double v = trace[idx];
                double d = (v - trace[idx - 1]) / 0.1;
                // Skip the artificial spike stroke and the reset jump so the trajectory stays readable.
                if (v > -45 || Math.Abs(d) > 20)
                {
                    _phaseV[i] = lastV;
                    _phaseD[i] = lastD;
                    continue;
                }
                lastV = v;
                lastD = d;
            }
            _phaseV[i] = lastV;
            _phaseD[i] = lastD;
        }
        PhasePlot.Refresh();
    }
}
