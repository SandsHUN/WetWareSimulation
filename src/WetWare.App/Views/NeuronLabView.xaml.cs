using System.Windows;
using System.Windows.Controls;
using ScottPlot;
using ScottPlot.Plottables;
using WetWare.App.Controls;
using WetWare.App.Localization;
using WetWare.App.ViewModels;

namespace WetWare.App.Views;

public partial class NeuronLabView : UserControl
{
    private const int TraceLength = 4000; // 400 ms at dt = 0.1 ms

    private readonly double[] _voltage = new double[TraceLength];
    private readonly double[] _current = new double[TraceLength];
    private NeuronLabViewModel? _vm;
    private Signal? _vSignal, _iSignal;
    private HorizontalLine? _thresholdLine, _restLine;
    private Scatter? _trail;
    private Marker? _stateMarker, _fiMarker;
    private readonly double[] _trailX = new double[600], _trailY = new double[600];
    private int _frame;

    public NeuronLabView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = DataContext as NeuronLabViewModel;
        if (_vm is null) return;
        foreach (var plot in new[] { ScopePlot, CurrentPlot, PhasePlot, FiPlot }) LabPlot.Apply(plot);
        BuildScope();
        BuildStaticPlots();
        _vm.FrameUpdated += OnFrame;
        _vm.ParametersChanged += OnParametersChanged;
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        _vm.FrameUpdated -= OnFrame;
        _vm.ParametersChanged -= OnParametersChanged;
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        BuildScope();
        BuildStaticPlots();
    }

    private void OnParametersChanged(object? sender, EventArgs e) => BuildStaticPlots();

    private void BuildScope()
    {
        var loc = LocalizationService.Instance;
        var p = _vm!.Simulator.Parameters;

        ScopePlot.Plot.Clear();
        _vSignal = ScopePlot.Plot.Add.Signal(_voltage, 0.1, LabPlot.Accent);
        _vSignal.LineWidth = 1.8f;
        _thresholdLine = ScopePlot.Plot.Add.HorizontalLine(p.Threshold, 1, LabPlot.Danger, LinePattern.Dashed);
        _thresholdLine.LegendText = loc["Plot.Threshold"];
        _restLine = ScopePlot.Plot.Add.HorizontalLine(p.RestingPotential, 1, LabPlot.Success, LinePattern.Dotted);
        _restLine.LegendText = loc["Plot.Rest"];
        ScopePlot.Plot.ShowLegend(Alignment.UpperLeft);
        LabPlot.Labels(ScopePlot, loc["Plot.TimeMs"], loc["Plot.VoltageMv"]);
        ScopePlot.Plot.Axes.SetLimitsY(-90, 30);

        CurrentPlot.Plot.Clear();
        _iSignal = CurrentPlot.Plot.Add.Signal(_current, 0.1, LabPlot.Warning);
        _iSignal.LineWidth = 1.4f;
        var rheo = CurrentPlot.Plot.Add.HorizontalLine(p.Rheobase, 1, LabPlot.Muted, LinePattern.Dashed);
        rheo.LegendText = loc["Plot.Rheobase"];
        CurrentPlot.Plot.ShowLegend(Alignment.UpperLeft);
        LabPlot.Labels(CurrentPlot, string.Empty, loc["Plot.CurrentPa"]);
        OnFrame(null, EventArgs.Empty);
    }

    private void BuildStaticPlots()
    {
        if (_vm is null) return;
        var loc = LocalizationService.Instance;
        var p = _vm.Simulator.Parameters;
        double current = _vm.Simulator.Stimulus.Deterministic(_vm.Simulator.TimeMs);

        if (_thresholdLine is not null) _thresholdLine.Y = p.Threshold;
        if (_restLine is not null) _restLine.Y = p.RestingPotential;

        // Phase plane: dv/dt as a function of v for the present input current.
        var plot = PhasePlot.Plot;
        plot.Clear();
        double vMin = Math.Min(p.ResetPotential, p.RestingPotential) - 15, vMax = p.Threshold + 10;
        double[] vs = Enumerable.Range(0, 120).Select(i => vMin + (vMax - vMin) * i / 119.0).ToArray();
        double[] rhs = vs.Select(v => p.Derivative(v, current)).ToArray();
        var line = plot.Add.ScatterLine(vs, rhs, LabPlot.Accent2);
        line.LineWidth = 2;
        line.LegendText = loc["Plot.Nullcline"];
        plot.Add.HorizontalLine(0, 1, LabPlot.Muted, LinePattern.Dotted);
        var th = plot.Add.VerticalLine(p.Threshold, 1, LabPlot.Danger, LinePattern.Dashed);
        th.Text = "V_th";
        var reset = plot.Add.VerticalLine(p.ResetPotential, 1, LabPlot.Warning, LinePattern.Dotted);
        reset.Text = "V_reset";
        double fixedPoint = p.FixedPoint(current);
        bool stable = fixedPoint < p.Threshold;
        if (fixedPoint > vMin && fixedPoint < vMax)
        {
            var fp = plot.Add.Marker(fixedPoint, 0, stable ? MarkerShape.FilledCircle : MarkerShape.OpenCircle, 13, LabPlot.Success);
            fp.LegendText = stable ? loc["Plot.FixedStable"] : loc["Plot.FixedVirtual"];
        }
        _trail = plot.Add.ScatterLine(_trailX, _trailY, LabPlot.Warning.WithAlpha(0.55));
        _trail.LineWidth = 1.2f;
        _stateMarker = plot.Add.Marker(p.RestingPotential, 0, MarkerShape.FilledCircle, 10, LabPlot.Warning);
        _stateMarker.LegendText = loc["Plot.State"];
        LabPlot.Labels(PhasePlot, loc["Plot.VoltageMv"], "dv/dt (mV/ms)");
        double rMin = rhs.Min(), rMax = rhs.Max();
        plot.Axes.SetLimits(vMin, vMax, Math.Min(-0.5, rMin) * 1.1, Math.Max(0.5, rMax) * 1.1);
        plot.ShowLegend(Alignment.UpperRight);
        PhasePlot.Refresh();

        // f–I curve (analytical) with the current operating point.
        var fi = FiPlot.Plot;
        fi.Clear();
        double[] currents = Enumerable.Range(0, 121).Select(i => i * 5.0).ToArray();
        double[] rates = currents.Select(i => p.FiringRate(i)).ToArray();
        var curve = fi.Add.ScatterLine(currents, rates, LabPlot.Accent);
        curve.LineWidth = 2.2f;
        curve.LegendText = loc["Plot.Analytic"];
        var rheo = fi.Add.VerticalLine(p.Rheobase, 1, LabPlot.Warning, LinePattern.Dashed);
        rheo.Text = loc["Plot.Rheobase"];
        _fiMarker = fi.Add.Marker(current, p.FiringRate(current), MarkerShape.FilledCircle, 12, LabPlot.Excitatory);
        _fiMarker.LegendText = loc["Plot.Operating"];
        LabPlot.Labels(FiPlot, loc["Plot.CurrentPa"], loc["Plot.RateHz"]);
        fi.Axes.SetLimits(0, 600, 0, Math.Max(20, rates.Max() * 1.1));
        fi.ShowLegend(Alignment.UpperLeft);
        FiPlot.Refresh();
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        if (_vm is null || _vSignal is null || _iSignal is null) return;
        var sim = _vm.Simulator;
        int count = sim.VoltageTrace.Count;
        int pad = TraceLength - Math.Min(count, TraceLength);
        for (int i = 0; i < pad; i++)
        {
            _voltage[i] = sim.Parameters.RestingPotential;
            _current[i] = 0;
        }
        int skip = Math.Max(0, count - TraceLength);
        for (int i = 0; i < TraceLength - pad; i++)
        {
            _voltage[pad + i] = sim.VoltageTrace[skip + i];
            _current[pad + i] = sim.CurrentTrace[skip + i];
        }
        double x0 = sim.TimeMs - TraceLength * 0.1;
        _vSignal.Data.XOffset = x0;
        _iSignal.Data.XOffset = x0;
        ScopePlot.Plot.Axes.SetLimitsX(x0, sim.TimeMs);
        CurrentPlot.Plot.Axes.SetLimitsX(x0, sim.TimeMs);
        double iMax = Math.Max(_vm.Amplitude * 1.6 + _vm.NoiseSigma * 3, 50);
        CurrentPlot.Plot.Axes.SetLimitsY(Math.Min(-20, -_vm.NoiseSigma * 3), iMax);
        ScopePlot.Refresh();
        CurrentPlot.Refresh();

        // Phase-plane trail and markers update at a lower rate.
        if (++_frame % 2 != 0 && sender is not null) return;
        int n = sim.PhaseTrail.Count;
        if (n > 0 && _trail is not null && _stateMarker is not null)
        {
            // Fixed-length arrays (ScottPlot keeps the reference); pad with the oldest sample.
            int len = _trailX.Length;
            int offset = len - Math.Min(n, len);
            int first = Math.Max(0, n - len);
            for (int i = 0; i < len; i++)
            {
                var (v, d) = sim.PhaseTrail[i < offset ? first : first + i - offset];
                _trailX[i] = v;
                _trailY[i] = d;
            }
            _stateMarker.X = _trailX[len - 1];
            _stateMarker.Y = _trailY[len - 1];
            PhasePlot.Refresh();
        }
        if (_fiMarker is not null)
        {
            double current = sim.Stimulus.Deterministic(sim.TimeMs);
            _fiMarker.X = current;
            _fiMarker.Y = sim.Parameters.FiringRate(current);
            FiPlot.Refresh();
        }
    }
}
