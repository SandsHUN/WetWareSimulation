using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WetWare.Core.Course.Synapses;

namespace WetWare.App.ViewModels.Course.Lessons;

/// <summary>6.1 – the synapse as a time-varying conductance with a reversal potential; two coupled neurons.</summary>
public sealed partial class SynapseLesson : LessonViewModel
{
    public SynapseLesson() : base("M6.Synapse", "6.1")
    {
        Push();
        PushPair();
    }

    public ConductanceSynapseSimulator Synapse { get; } = new();
    public CoupledNeuronPair Pair { get; } = new();

    [ObservableProperty] private double _reversal;
    [ObservableProperty] private double _strength = 0.5;
    [ObservableProperty] private double _tauS = 5;
    [ObservableProperty] private bool _alphaKernel;
    [ObservableProperty] private double _preRate = 20;
    [ObservableProperty] private bool _poisson = true;
    [ObservableProperty] private double _background = 12;
    [ObservableProperty] private bool _isRunning = true;
    [ObservableProperty] private bool _pairExcitatory = true;
    [ObservableProperty] private double _pairStrength = 0.1;
    [ObservableProperty] private double _pairTau = 10;
    [ObservableProperty] private string _readout = "";
    [ObservableProperty] private string _explain = "";
    [ObservableProperty] private string _pairText = "";

    partial void OnReversalChanged(double value) => Push();
    partial void OnStrengthChanged(double value) => Push();
    partial void OnTauSChanged(double value) => Push();
    partial void OnAlphaKernelChanged(bool value) => Push();
    partial void OnPreRateChanged(double value) => Push();
    partial void OnPoissonChanged(bool value) => Push();
    partial void OnBackgroundChanged(double value) => Push();
    partial void OnPairExcitatoryChanged(bool value) => PushPair(reset: true);
    partial void OnPairStrengthChanged(double value) => PushPair();
    partial void OnPairTauChanged(double value) => PushPair();

    private void Push()
    {
        Synapse.ReversalPotential = Reversal;
        Synapse.Strength = Strength;
        Synapse.Filter.TauMs = TauS;
        Synapse.Filter.Kernel = AlphaKernel ? SynapseKernel.Alpha : SynapseKernel.Exponential;
        Synapse.PresynapticRate = PreRate;
        Synapse.PoissonInput = Poisson;
        Synapse.BackgroundMv = Background;
        RefreshTexts();
        RaiseUpdated();
    }

    private void PushPair(bool reset = false)
    {
        Pair.Excitatory = PairExcitatory;
        Pair.Strength = PairStrength;
        Pair.SynapseTauMs = PairTau;
        if (reset) Pair.Reset();
        RefreshTexts();
    }

    public override void Tick(double realMs)
    {
        if (!IsRunning) return;
        // Half real-time speed: the 600 ms window scrolls slowly enough to follow single PSPs.
        Synapse.Advance(Math.Round(realMs * 0.5, 1));
        Pair.Advance(Math.Round(realMs * 0.5, 1));
        RefreshTexts();
        RaiseUpdated();
    }

    [RelayCommand]
    private void ToggleRun() => IsRunning = !IsRunning;

    [RelayCommand]
    private void Inject()
    {
        Synapse.InjectSpike();
        RaiseUpdated();
    }

    [RelayCommand]
    private void Reset()
    {
        Synapse.Reset();
        Pair.Reset();
        RaiseUpdated();
    }

    [RelayCommand]
    private void Preset(string id)
    {
        switch (id)
        {
            case "AMPA":
                (Reversal, TauS, AlphaKernel, Strength) = (0, 3, false, 0.5);
                break;
            case "GABA":
                (Reversal, TauS, AlphaKernel, Strength) = (-80, 8, false, 0.5);
                break;
            case "NMDA":
                (Reversal, TauS, AlphaKernel, Strength) = (0, 40, true, 0.15);
                break;
            case "Shunting":
                (Reversal, TauS, AlphaKernel, Strength) = (-68, 8, false, 1.2);
                break;
        }
        Synapse.Reset();
    }

    protected override void OnLanguageChanged() => RefreshTexts();

    private void RefreshTexts()
    {
        double drive = Synapse.SynapticCurrent;
        double rate = Synapse.PostSpikes.Count(t => t > Synapse.TimeMs - 1000) / Math.Min(1.0, Math.Max(0.1, Synapse.TimeMs / 1000));
        Readout = Loc.Format("M6.Synapse.Readout", F(Synapse.V, 1), F(Strength * Synapse.Filter.P, 3), F(-drive, 2), F(rate, 0));

        double margin = Reversal - Synapse.Threshold;
        Explain = Reversal > Synapse.Threshold
            ? Loc.Format("M6.Synapse.Explain.Exc", F(Reversal, 0), F(margin, 0))
            : Reversal < Synapse.RestingPotential - 3
                ? Loc.Format("M6.Synapse.Explain.Inh", F(Reversal, 0))
                : Loc.Format("M6.Synapse.Explain.Shunt", F(Reversal, 0));

        double phase = Pair.RelativePhase;
        string pattern = double.IsNaN(phase) ? Loc["M6.Synapse.Pair.Wait"]
            : phase < 0.12 ? Loc["M6.Synapse.Pair.Sync"]
            : phase > 0.35 ? Loc["M6.Synapse.Pair.Anti"]
            : Loc["M6.Synapse.Pair.Between"];
        PairText = double.IsNaN(phase) ? pattern : Loc.Format("M6.Synapse.Pair.Readout", F(phase, 2), pattern);
    }
}
