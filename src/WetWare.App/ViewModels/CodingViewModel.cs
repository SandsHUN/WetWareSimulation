using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WetWare.App.Services;
using WetWare.Core.Coding;

namespace WetWare.App.ViewModels;

/// <summary>Compares rate, latency, phase and population codes for the same stimulus.</summary>
public sealed partial class CodingViewModel : PageViewModel
{
    private readonly FrameClock _clock;
    private readonly List<CodedSpike> _recent = new();
    private double _uiAccumulator;

    public CodingViewModel(FrameClock clock)
    {
        _clock = clock;
        Simulator.Stimulus = Stimulus;
        RefreshReadouts();
    }

    public override string Key => "Coding";
    public override string Glyph => "";

    public NeuralCodingSimulator Simulator { get; } = new();

    public double[] SpeedOptions { get; } = [0.1, 0.25, 0.5, 1.0];

    public event EventHandler<List<CodedSpike>>? FrameUpdated;

    [ObservableProperty] private double _stimulus = 0.6;
    [ObservableProperty] private bool _autoSweep;
    [ObservableProperty] private bool _isRunning = true;
    [ObservableProperty] private double _speed = 0.25;
    [ObservableProperty] private string _rateReadout = "";
    [ObservableProperty] private string _latencyReadout = "";
    [ObservableProperty] private string _phaseReadout = "";
    [ObservableProperty] private string _populationReadout = "";
    [ObservableProperty] private string _stimulusText = "";

    partial void OnStimulusChanged(double value)
    {
        if (!AutoSweep) Simulator.Stimulus = value;
        RefreshReadouts();
    }

    partial void OnAutoSweepChanged(bool value) => Simulator.AutoSweep = value;

    protected override void OnActivated() => _clock.Tick += OnTick;

    protected override void OnDeactivated() => _clock.Tick -= OnTick;

    protected override void OnLanguageChanged() => RefreshReadouts();

    private void OnTick(object? sender, double realMs)
    {
        List<CodedSpike> spikes = [];
        if (IsRunning)
        {
            Simulator.Advance(realMs * Speed);
            spikes = Simulator.TakeSpikes();
            _recent.AddRange(spikes);
            _recent.RemoveAll(s => s.TimeMs < Simulator.TimeMs - 1000);
            if (AutoSweep) Stimulus = Math.Round(Simulator.Stimulus, 3);
        }
        _uiAccumulator += realMs;
        if (_uiAccumulator > 150)
        {
            _uiAccumulator = 0;
            RefreshReadouts();
        }
        FrameUpdated?.Invoke(this, spikes);
    }

    [RelayCommand]
    private void ToggleRun() => IsRunning = !IsRunning;

    private void RefreshReadouts()
    {
        double s = Simulator.Stimulus;
        double now = Simulator.TimeMs;
        StimulusText = F(s, 2);

        int rateSpikes = _recent.Count(x => x.Scheme == CodingScheme.Rate && x.TimeMs >= now - 500);
        double rate = rateSpikes / (NeuralCodingSimulator.NeuronsPerCode * 0.5);
        RateReadout = Loc.Format("Coding.Readout.Rate", F(rate, 1), F(2 + s * NeuralCodingSimulator.MaxRateHz, 1));

        LatencyReadout = Loc.Format("Coding.Readout.Latency", F(Simulator.ExpectedLatency, 1));

        var phaseSpikes = _recent.Where(x => x.Scheme == CodingScheme.Phase && x.TimeMs >= now - 250).ToList();
        double cycle = 1000.0 / NeuralCodingSimulator.ThetaHz;
        double meanPhase = phaseSpikes.Count == 0 ? double.NaN
            : phaseSpikes.Average(x => (x.TimeMs % cycle) / cycle * 360.0);
        PhaseReadout = Loc.Format("Coding.Readout.Phase", double.IsNaN(meanPhase) ? "—" : F(meanPhase, 0),
            F(Simulator.ExpectedPhase * 180 / Math.PI, 0));

        double decoded = Simulator.DecodedStimulus;
        PopulationReadout = Loc.Format("Coding.Readout.Population", double.IsNaN(decoded) ? "—" : F(decoded, 2), F(s, 2));
    }
}
