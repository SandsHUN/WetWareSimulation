using ScottPlot;
using ScottPlot.Plottables;
using WetWare.App.Controls;
using WetWare.App.ViewModels.Course.Lessons;
using WetWare.Core.Course.Synapses;

namespace WetWare.App.Views.Course.Lessons;

public partial class SynapseLessonView : LessonView
{
    private const double Dt = ConductanceSynapseSimulator.DtMs;
    private readonly double[] _v = new double[ConductanceSynapseSimulator.TraceLength];
    private readonly double[] _g = new double[ConductanceSynapseSimulator.TraceLength];
    private readonly double[] _a = new double[CoupledNeuronPair.TraceLength];
    private readonly double[] _b = new double[CoupledNeuronPair.TraceLength];
    private Signal? _vSignal, _gSignal, _aSignal, _bSignal;
    private HorizontalLine? _esLine;
    private (double Tau, bool Alpha, double Es) _kernelState = (-1, false, double.NaN);

    public SynapseLessonView() => InitializeComponent();

    private SynapseLesson Vm => (SynapseLesson)DataContext;

    protected override void Setup() => StylePlots(VoltagePlot, ConductancePlot, KernelPlot, PairPlot);

    protected override void Draw(bool full)
    {
        var vm = Vm;
        if (full || _vSignal is null) Build();

        var syn = vm.Synapse;
        Fill(_v, syn.Voltage, syn.RestingPotential);
        Fill(_g, syn.Conductance, 0);
        double x0 = syn.TimeMs - _v.Length * Dt;
        _vSignal!.Data.XOffset = x0;
        _gSignal!.Data.XOffset = x0;
        _esLine!.Y = Math.Clamp(vm.Reversal, -95, 25);
        _esLine.LineColor = vm.Reversal > syn.Threshold ? LabPlot.Excitatory : vm.Reversal < syn.RestingPotential - 3 ? LabPlot.Inhibitory : LabPlot.Accent2;
        VoltagePlot.Plot.Axes.SetLimits(x0, syn.TimeMs, -95, 32);
        ConductancePlot.Plot.Axes.SetLimits(x0, syn.TimeMs, 0, Math.Max(0.2, vm.Strength * 3));
        VoltagePlot.Refresh();
        ConductancePlot.Refresh();

        var pair = vm.Pair;
        Fill(_a, pair.TraceA, pair.RestingPotential);
        Fill(_b, pair.TraceB, pair.RestingPotential);
        for (int i = 0; i < _b.Length; i++) _b[i] -= 110; // stack the second neuron below the first
        double p0 = pair.TimeMs - _a.Length * CoupledNeuronPair.DtMs;
        _aSignal!.Data.XOffset = p0;
        _bSignal!.Data.XOffset = p0;
        PairPlot.Plot.Axes.SetLimits(p0, pair.TimeMs, -195, 30);
        PairPlot.Refresh();

        if (full || _kernelState != (vm.TauS, vm.AlphaKernel, vm.Reversal)) DrawKernel();
    }

    private void Build()
    {
        var loc = Loc;
        var vp = VoltagePlot.Plot;
        vp.Clear();
        _vSignal = vp.Add.Signal(_v, Dt, LabPlot.Accent);
        _vSignal.LineWidth = 1.6f;
        var th = vp.Add.HorizontalLine(Vm.Synapse.Threshold, 1, LabPlot.Danger, LinePattern.Dashed);
        th.LegendText = loc["Plot.Threshold"];
        var rest = vp.Add.HorizontalLine(Vm.Synapse.RestingPotential, 1, LabPlot.Success, LinePattern.Dotted);
        rest.LegendText = loc["Plot.Rest"];
        _esLine = vp.Add.HorizontalLine(0, 1.6f, LabPlot.Excitatory, LinePattern.Dashed);
        _esLine.LegendText = "E_s";
        vp.ShowLegend(Alignment.UpperLeft);
        LabPlot.Labels(VoltagePlot, "", loc["Plot.VoltageMv"]);

        var gp = ConductancePlot.Plot;
        gp.Clear();
        _gSignal = gp.Add.Signal(_g, Dt, LabPlot.Warning);
        _gSignal.LineWidth = 1.4f;
        LabPlot.Labels(ConductancePlot, loc["Plot.TimeMs"], "r_m·g_s");

        var pp = PairPlot.Plot;
        pp.Clear();
        _aSignal = pp.Add.Signal(_a, CoupledNeuronPair.DtMs, LabPlot.Accent);
        _aSignal.LineWidth = 1.4f;
        _aSignal.LegendText = loc["M6.Synapse.Pair.A"];
        _bSignal = pp.Add.Signal(_b, CoupledNeuronPair.DtMs, LabPlot.Accent2);
        _bSignal.LineWidth = 1.4f;
        _bSignal.LegendText = loc["M6.Synapse.Pair.B"];
        pp.ShowLegend(Alignment.UpperLeft);
        pp.Axes.Left.TickLabelStyle.IsVisible = false;
        LabPlot.Labels(PairPlot, loc["Plot.TimeMs"], "V");
    }

    private void DrawKernel()
    {
        var vm = Vm;
        _kernelState = (vm.TauS, vm.AlphaKernel, vm.Reversal);
        var plot = KernelPlot.Plot;
        plot.Clear();
        double tMax = Math.Max(30, vm.TauS * 6);
        double[] t = Enumerable.Range(0, 201).Select(i => i * tMax / 200).ToArray();
        var exp = plot.Add.ScatterLine(t, t.Select(x => SynapticFilter.Response(SynapseKernel.Exponential, vm.TauS, x)).ToArray(),
            vm.AlphaKernel ? LabPlot.Muted.WithAlpha(0.5) : LabPlot.Warning);
        exp.LineWidth = vm.AlphaKernel ? 1.2f : 2.4f;
        exp.LegendText = "exp(−t/τ)";
        var alpha = plot.Add.ScatterLine(t, t.Select(x => SynapticFilter.Response(SynapseKernel.Alpha, vm.TauS, x)).ToArray(),
            vm.AlphaKernel ? LabPlot.Warning : LabPlot.Muted.WithAlpha(0.5));
        alpha.LineWidth = vm.AlphaKernel ? 2.4f : 1.2f;
        alpha.LegendText = "α(t)";
        plot.ShowLegend(Alignment.UpperRight);
        LabPlot.Labels(KernelPlot, Loc["Plot.TimeMs"], "P_s");
        plot.Axes.SetLimits(0, tMax, 0, 1.08);
        KernelPlot.Refresh();
    }
}
