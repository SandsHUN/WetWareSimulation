using System.Windows;
using System.Windows.Controls;
using ScottPlot;
using WetWare.App.Controls;
using WetWare.App.Localization;
using WetWare.App.ViewModels;
using WetWare.Core.Coding;

namespace WetWare.App.Views;

public partial class CodingView : UserControl
{
    private const int RateRow = 0, LatencyRow = 8, PhaseRow = 16, PopulationRow = 24;

    private CodingViewModel? _vm;
    private readonly Dictionary<CodingScheme, int> _colors = new()
    {
        [CodingScheme.Rate] = ColorMaps.Pack(ColorMaps.Parse("#34D399")),
        [CodingScheme.Latency] = ColorMaps.Pack(ColorMaps.Parse("#FBBF24")),
        [CodingScheme.Phase] = ColorMaps.Pack(ColorMaps.Parse("#F472B6")),
        [CodingScheme.Population] = ColorMaps.Pack(ColorMaps.Parse("#22D3EE")),
    };
    private double _lastOnset = double.NegativeInfinity;
    private int _frame;

    public CodingView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = DataContext as CodingViewModel;
        if (_vm is null) return;
        LabPlot.Apply(TuningPlot);
        ConfigureRaster();
        _vm.FrameUpdated += OnFrame;
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        DrawTuning();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        _vm.FrameUpdated -= OnFrame;
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        ConfigureRaster();
        DrawTuning();
    }

    private void ConfigureRaster()
    {
        var loc = LocalizationService.Instance;
        var phaseBand = new RasterBand(PhaseRow, 8, ColorMaps.Parse("#F472B6"), loc["Coding.Phase.Short"]);
        Raster.Bands =
        [
            new RasterBand(RateRow, 8, ColorMaps.Parse("#34D399"), loc["Coding.Rate.Short"]),
            new RasterBand(LatencyRow, 8, ColorMaps.Parse("#FBBF24"), loc["Coding.Latency.Short"]),
            phaseBand,
            new RasterBand(PopulationRow, 12, ColorMaps.Parse("#22D3EE"), loc["Coding.Population.Short"]),
        ];
        Raster.OverlayBand = phaseBand;
        Raster.Overlay = t => Math.Cos(2 * Math.PI * NeuralCodingSimulator.ThetaHz * t / 1000.0);
    }

    private void OnFrame(object? sender, List<CodedSpike> spikes)
    {
        if (_vm is null) return;
        foreach (var s in spikes)
        {
            int row = s.Scheme switch
            {
                CodingScheme.Rate => RateRow + s.Neuron,
                CodingScheme.Latency => LatencyRow + s.Neuron,
                CodingScheme.Phase => PhaseRow + s.Neuron,
                _ => PopulationRow + s.Neuron,
            };
            Raster.AddSpike(s.TimeMs, row, _colors[s.Scheme]);
        }

        // Stimulus onsets of the latency code are drawn as vertical markers.
        double now = _vm.Simulator.TimeMs;
        double onset = Math.Floor(now / NeuralCodingSimulator.OnsetPeriodMs) * NeuralCodingSimulator.OnsetPeriodMs;
        if (onset > _lastOnset)
        {
            _lastOnset = onset;
            Raster.AddMarker(onset);
        }
        Raster.Refresh(now);

        if (++_frame % 6 == 0) DrawTuning();
    }

    private void DrawTuning()
    {
        if (_vm is null) return;
        var loc = LocalizationService.Instance;
        var plot = TuningPlot.Plot;
        plot.Clear();
        double[] xs = Enumerable.Range(0, 101).Select(i => i / 100.0).ToArray();
        for (int n = 0; n < NeuralCodingSimulator.PopulationSize; n++)
        {
            var curve = plot.Add.ScatterLine(xs, xs.Select(x => NeuralCodingSimulator.TuningCurve(n, x)).ToArray(), LabPlot.Accent.WithAlpha(0.35));
            curve.LineWidth = 1.2f;
        }
        double s = _vm.Simulator.Stimulus;
        var bars = new List<Bar>();
        for (int n = 0; n < NeuralCodingSimulator.PopulationSize; n++)
        {
            bars.Add(new Bar
            {
                Position = NeuralCodingSimulator.PreferredStimulus(n),
                Value = NeuralCodingSimulator.TuningCurve(n, s),
                Size = 0.05,
                FillColor = LabPlot.Accent.WithAlpha(0.8),
                LineWidth = 0,
            });
        }
        plot.Add.Bars(bars);
        var line = plot.Add.VerticalLine(s, 2, LabPlot.Warning, LinePattern.Solid);
        line.Text = "s";
        double decoded = _vm.Simulator.DecodedStimulus;
        if (!double.IsNaN(decoded))
        {
            var d = plot.Add.VerticalLine(decoded, 1.5f, LabPlot.Excitatory, LinePattern.Dashed);
            d.Text = "ŝ";
        }
        LabPlot.Labels(TuningPlot, loc["Coding.Axis.Stimulus"], loc["Plot.RateHz"]);
        plot.Axes.SetLimits(-0.05, 1.05, 0, NeuralCodingSimulator.MaxRateHz * 1.1);
        TuningPlot.Refresh();
    }
}
