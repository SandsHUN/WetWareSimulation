using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WetWare.App.Services;
using WetWare.Core.Plasticity;

namespace WetWare.App.ViewModels;

/// <summary>STDP pairing laboratory (Bi &amp; Poo protocol) with the interactive learning window.</summary>
public sealed partial class StdpLabViewModel : PageViewModel
{
    private const double AnimationMs = 650;

    private readonly FrameClock _clock;
    private double _animationTime = AnimationMs;
    private double _animationDuration = AnimationMs;
    private PairingResult? _pending;

    public StdpLabViewModel(FrameClock clock)
    {
        _clock = clock;
        PushParameters();
        RefreshTexts();
    }

    public override string Key => "Stdp";
    public override string Glyph => "";

    public StdpPairingExperiment Experiment { get; } = new();

    public event EventHandler? Updated;

    /// <summary>0–1 progress of the pairing animation.</summary>
    public double AnimationProgress => Math.Clamp(_animationTime / _animationDuration, 0, 1);

    public PairingResult? LastPairing { get; private set; }

    /// <summary>The pairing currently being animated (or the last one).</summary>
    public PairingResult? AnimatedPairing => _pending ?? LastPairing;

    /// <summary>Weight shown by the bouton: it only changes once the animation reaches the postsynaptic spike.</summary>
    public double DisplayedWeight => LastPairing?.Weight ?? Experiment.InitialWeight;

    [ObservableProperty] private double _aPlus = 0.02;
    [ObservableProperty] private double _aMinus = 0.021;
    [ObservableProperty] private double _tauPlus = 20;
    [ObservableProperty] private double _tauMinus = 20;
    [ObservableProperty] private double _deltaT = 10;
    [ObservableProperty] private double _jitter = 1;
    [ObservableProperty] private bool _randomTiming;
    [ObservableProperty] private double _pairingsPerSecond = 2;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _explanation = "";
    [ObservableProperty] private string _balanceText = "";
    [ObservableProperty] private string _balanceVerdict = "";
    [ObservableProperty] private string _weightText = "";
    [ObservableProperty] private string _countText = "";
    [ObservableProperty] private string _windowValueText = "";

    partial void OnAPlusChanged(double value) => PushParameters();
    partial void OnAMinusChanged(double value) => PushParameters();
    partial void OnTauPlusChanged(double value) => PushParameters();
    partial void OnTauMinusChanged(double value) => PushParameters();
    partial void OnDeltaTChanged(double value) => PushParameters();
    partial void OnJitterChanged(double value) => PushParameters();
    partial void OnRandomTimingChanged(bool value) => PushParameters();

    private void PushParameters()
    {
        var p = Experiment.Parameters;
        p.APlus = APlus;
        p.AMinus = AMinus;
        p.TauPlus = TauPlus;
        p.TauMinus = TauMinus;
        Experiment.DeltaT = DeltaT;
        Experiment.JitterMs = Jitter;
        Experiment.RandomTiming = RandomTiming;
        RefreshTexts();
        Updated?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnActivated() => _clock.Tick += OnTick;

    protected override void OnDeactivated() => _clock.Tick -= OnTick;

    protected override void OnLanguageChanged() => RefreshTexts();

    private void OnTick(object? sender, double realMs)
    {
        bool animating = _animationTime < _animationDuration;
        _animationTime += realMs;

        if (_pending is { } pending && AnimationProgress >= 0.75)
        {
            // Commit the weight change once both spikes have been drawn.
            LastPairing = pending;
            _pending = null;
            RefreshTexts();
        }

        if (IsRunning && _animationTime >= 1000.0 / PairingsPerSecond) StartPairing();

        if (animating || IsRunning) Updated?.Invoke(this, EventArgs.Empty);
    }

    private void StartPairing()
    {
        if (_pending is { } previous) LastPairing = previous;
        _pending = Experiment.Pair();
        _animationDuration = Math.Min(AnimationMs, 1000.0 / PairingsPerSecond * 0.85);
        _animationTime = 0;
    }

    [RelayCommand]
    private void PairOnce()
    {
        StartPairing();
        Updated?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ToggleRun() => IsRunning = !IsRunning;

    [RelayCommand]
    private void Reset()
    {
        IsRunning = false;
        Experiment.Reset();
        LastPairing = null;
        _pending = null;
        _animationTime = _animationDuration;
        RefreshTexts();
        Updated?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ApplyPreset(string id)
    {
        switch (id)
        {
            case "Causal":
                RandomTiming = false;
                DeltaT = 8;
                break;
            case "AntiCausal":
                RandomTiming = false;
                DeltaT = -8;
                break;
            case "Distant":
                RandomTiming = false;
                DeltaT = 45;
                break;
            case "Random":
                RandomTiming = true;
                break;
        }
        Reset();
        IsRunning = true;
    }

    private void RefreshTexts()
    {
        var p = Experiment.Parameters;
        double balance = p.NetBalance;
        BalanceText = $"A+τ+ − A−τ− = {F(balance, 3)}";
        BalanceVerdict = balance < 0 ? Loc["Stdp.Balance.Negative"] : balance > 0 ? Loc["Stdp.Balance.Positive"] : Loc["Stdp.Balance.Zero"];
        WeightText = F(Experiment.Weight, 3);
        CountText = Experiment.PairingCount.ToString(Loc.Culture);
        WindowValueText = $"Δw(Δt = {F(DeltaT, 0)} ms) = {F(p.Window(DeltaT), 4)}";

        if (LastPairing is { } last)
        {
            Explanation = last.DeltaT > 0
                ? Loc.Format("Stdp.Explain.Ltp", F(last.DeltaT, 1), F(last.DeltaW, 4), F(last.Weight, 3))
                : Loc.Format("Stdp.Explain.Ltd", F(-last.DeltaT, 1), F(-last.DeltaW, 4), F(last.Weight, 3));
        }
        else
        {
            Explanation = Loc["Stdp.Explain.Start"];
        }
    }
}
