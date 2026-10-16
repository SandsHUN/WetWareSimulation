using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WetWare.App.Services;
using WetWare.Core.Neurons;

namespace WetWare.App.ViewModels;

/// <summary>Single LIF neuron laboratory: electrode, oscilloscope, live equation, phase plane, f–I curve, Bio-Debugger.</summary>
public sealed partial class NeuronLabViewModel : PageViewModel
{
    private readonly FrameClock _clock;
    private double _uiAccumulator;

    public NeuronLabViewModel(FrameClock clock)
    {
        _clock = clock;
        Shapes =
        [
            new(StimulusShape.Constant, "Stim.Constant"),
            new(StimulusShape.Pulses, "Stim.Pulses"),
            new(StimulusShape.Step, "Stim.Step"),
            new(StimulusShape.Ramp, "Stim.Ramp"),
            new(StimulusShape.Sine, "Stim.Sine"),
            new(StimulusShape.Noisy, "Stim.Noisy"),
        ];
        Presets =
        [
            new("Silent", "Preset.Silent"),
            new("Tonic", "Preset.Tonic"),
            new("Fast", "Preset.Fast"),
            new("Pulses", "Preset.Pulses"),
            new("Noise", "Preset.Noise"),
            new("Leaky", "Preset.Leaky"),
        ];
        _selectedShape = Shapes[0];
        ApplyAll();
        RefreshDerived();
    }

    public override string Key => "Neuron";
    public override string Glyph => "";

    public SingleNeuronSimulator Simulator { get; } = new();

    public IReadOnlyList<LocalizedOption<StimulusShape>> Shapes { get; }
    public IReadOnlyList<LocalizedOption<string>> Presets { get; }
    public double[] SpeedOptions { get; } = [0.02, 0.05, 0.1, 0.25, 0.5, 1.0];

    /// <summary>Raised every frame after the simulation advanced; the view redraws its plots.</summary>
    public event EventHandler? FrameUpdated;

    /// <summary>Raised when parameters changed so static curves (phase plane, f–I) must be rebuilt.</summary>
    public event EventHandler? ParametersChanged;

    [ObservableProperty] private LocalizedOption<StimulusShape> _selectedShape;
    [ObservableProperty] private double _amplitude = 260;
    [ObservableProperty] private double _frequency = 10;
    [ObservableProperty] private double _pulseWidth = 25;
    [ObservableProperty] private double _noiseSigma;
    [ObservableProperty] private double _tauM = 20;
    [ObservableProperty] private double _restingPotential = -70;
    [ObservableProperty] private double _threshold = -50;
    [ObservableProperty] private double _resetPotential = -65;
    [ObservableProperty] private double _resistance = 100;
    [ObservableProperty] private double _refractoryPeriod = 2;
    [ObservableProperty] private double _speed = 0.1;
    [ObservableProperty] private bool _isRunning = true;

    [ObservableProperty] private string _vText = "";
    [ObservableProperty] private string _dvdtText = "";
    [ObservableProperty] private string _currentText = "";
    [ObservableProperty] private Brush _vBrush = Brushes.LightGreen;
    [ObservableProperty] private string _narrative = "";
    [ObservableProperty] private string _phaseNarrative = "";
    [ObservableProperty] private string _measuredRateText = "";
    [ObservableProperty] private string _theoryRateText = "";
    [ObservableProperty] private string _rheobaseText = "";
    [ObservableProperty] private string _capacitanceText = "";
    [ObservableProperty] private string _fixedPointText = "";
    [ObservableProperty] private string _timeText = "";
    [ObservableProperty] private string _tauText = "";
    [ObservableProperty] private string _restText = "";
    [ObservableProperty] private string _resistanceText = "";
    [ObservableProperty] private double _rateLevel;
    [ObservableProperty] private IReadOnlyList<DebugLine> _debugLines = [];

    public bool ShowFrequency => SelectedShape.Value is StimulusShape.Pulses or StimulusShape.Sine;
    public bool ShowPulseWidth => SelectedShape.Value == StimulusShape.Pulses;

    partial void OnSelectedShapeChanged(LocalizedOption<StimulusShape> value)
    {
        OnPropertyChanged(nameof(ShowFrequency));
        OnPropertyChanged(nameof(ShowPulseWidth));
        ApplyAll();
    }

    partial void OnAmplitudeChanged(double value) => ApplyAll();
    partial void OnFrequencyChanged(double value) => ApplyAll();
    partial void OnPulseWidthChanged(double value) => ApplyAll();
    partial void OnNoiseSigmaChanged(double value) => ApplyAll();
    partial void OnTauMChanged(double value) => ApplyAll();
    partial void OnRestingPotentialChanged(double value) => ApplyAll();
    partial void OnThresholdChanged(double value) => ApplyAll();
    partial void OnResetPotentialChanged(double value) => ApplyAll();
    partial void OnResistanceChanged(double value) => ApplyAll();
    partial void OnRefractoryPeriodChanged(double value) => ApplyAll();

    private void ApplyAll()
    {
        var p = Simulator.Parameters;
        p.TauM = TauM;
        p.RestingPotential = RestingPotential;
        p.Threshold = Math.Max(Threshold, RestingPotential + 1);
        p.ResetPotential = Math.Min(ResetPotential, p.Threshold - 0.5);
        p.Resistance = Resistance;
        p.RefractoryPeriod = RefractoryPeriod;
        var s = Simulator.Stimulus;
        s.Shape = SelectedShape.Value;
        s.Amplitude = Amplitude;
        s.FrequencyHz = Frequency;
        s.PulseWidthMs = PulseWidth;
        s.NoiseSigma = NoiseSigma;
        RefreshDerived();
        ParametersChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnActivated() => _clock.Tick += OnTick;

    protected override void OnDeactivated() => _clock.Tick -= OnTick;

    protected override void OnLanguageChanged()
    {
        RefreshDerived();
        ParametersChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnTick(object? sender, double realMs)
    {
        if (IsRunning) Simulator.Advance(realMs * Speed);
        _uiAccumulator += realMs;
        if (_uiAccumulator >= 60 || !IsRunning)
        {
            _uiAccumulator = 0;
            RefreshDerived();
        }
        FrameUpdated?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ToggleRun() => IsRunning = !IsRunning;

    [RelayCommand]
    private void StepOnce()
    {
        IsRunning = false;
        Simulator.Step();
        RefreshDerived();
        FrameUpdated?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void StepMany()
    {
        IsRunning = false;
        for (int i = 0; i < 10; i++) Simulator.Step();
        RefreshDerived();
        FrameUpdated?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Reset()
    {
        Simulator.Reset();
        RefreshDerived();
        FrameUpdated?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void RestoreDefaults()
    {
        TauM = 20;
        RestingPotential = -70;
        Threshold = -50;
        ResetPotential = -65;
        Resistance = 100;
        RefractoryPeriod = 2;
        NoiseSigma = 0;
    }

    [RelayCommand]
    private void ApplyPreset(string id)
    {
        RestoreDefaults();
        switch (id)
        {
            case "Silent":
                SelectedShape = Shapes[0];
                Amplitude = 150;
                break;
            case "Tonic":
                SelectedShape = Shapes[0];
                Amplitude = 260;
                break;
            case "Fast":
                SelectedShape = Shapes[0];
                Amplitude = 480;
                break;
            case "Pulses":
                SelectedShape = Shapes[1];
                Amplitude = 400;
                Frequency = 8;
                PulseWidth = 30;
                break;
            case "Noise":
                SelectedShape = Shapes[0];
                Amplitude = 170;
                NoiseSigma = 90;
                break;
            case "Leaky":
                SelectedShape = Shapes[1];
                Amplitude = 280;
                Frequency = 20;
                PulseWidth = 12;
                TauM = 8;
                break;
        }
        Simulator.Reset();
        IsRunning = true;
    }

    private void RefreshDerived()
    {
        var p = Simulator.Parameters;
        double v = Simulator.V;
        double current = Simulator.LastCurrent;
        double baseCurrent = Simulator.Stimulus.Deterministic(Simulator.TimeMs);
        VText = $"{F(v, 2)} mV";
        DvdtText = $"{F(p.Derivative(v, current), 3)} mV/ms";
        CurrentText = $"{F(current, 0)} pA";
        double level = Math.Clamp((v - p.RestingPotential) / (p.Threshold - p.RestingPotential), 0, 1);
        VBrush = new SolidColorBrush(Simulator.IsRefractory ? Color.FromRgb(0xF4, 0x3F, 0x5E) : VoltageColor(level));

        double rheobase = p.Rheobase;
        double theory = p.FiringRate(baseCurrent);
        double fixedPoint = p.FixedPoint(baseCurrent);
        RheobaseText = $"{F(rheobase, 0)} pA";
        CapacitanceText = $"{F(p.Capacitance, 0)} pF";
        FixedPointText = $"{F(fixedPoint, 1)} mV";
        MeasuredRateText = $"{F(Simulator.MeasuredRate, 1)} Hz";
        TheoryRateText = SelectedShape.Value == StimulusShape.Constant ? $"{F(theory, 1)} Hz" : "—";
        RateLevel = Math.Clamp(Simulator.MeasuredRate / 150.0, 0, 1);
        TimeText = $"t = {F(Simulator.TimeMs, 1)} ms";
        TauText = $"{F(p.TauM, 1)} ms";
        RestText = $"{F(p.RestingPotential, 1)} mV";
        ResistanceText = $"{F(p.Resistance, 0)} MΩ";

        if (Simulator.IsRefractory)
            Narrative = Loc.Format("Neuron.Narr.Refractory", F(p.RefractoryPeriod, 1));
        else if (baseCurrent < rheobase)
            Narrative = Loc.Format("Neuron.Narr.Below", F(baseCurrent, 0), F(rheobase, 0), F(fixedPoint, 1), F(p.Threshold, 1));
        else
            Narrative = Loc.Format("Neuron.Narr.Above", F(baseCurrent, 0), F(rheobase, 0), F(fixedPoint, 1), F(p.Threshold, 1), F(theory, 1));
        if (NoiseSigma > 0 || SelectedShape.Value == StimulusShape.Noisy)
            Narrative += " " + Loc["Neuron.Narr.Noise"];

        PhaseNarrative = fixedPoint < p.Threshold
            ? Loc.Format("Neuron.Phase.Stable", F(fixedPoint, 1), F(p.TauM, 0))
            : Loc.Format("Neuron.Phase.Cycle", F(fixedPoint, 1), F(p.Threshold, 1), F(p.ResetPotential, 1));

        DebugLines = BioDebugger.Build(Simulator.LastStep, p, networkMode: false);
    }

    /// <summary>Green at rest → yellow → orange-red near threshold.</summary>
    public static Color VoltageColor(double level)
    {
        level = Math.Clamp(level, 0, 1);
        if (level < 0.5)
        {
            double t = level / 0.5;
            return Color.FromRgb((byte)(0x34 + (0xFB - 0x34) * t), (byte)(0xD3 + (0xBF - 0xD3) * t), (byte)(0x99 + (0x24 - 0x99) * t));
        }
        double u = (level - 0.5) / 0.5;
        return Color.FromRgb((byte)(0xFB + (0xF9 - 0xFB) * u), (byte)(0xBF + (0x73 - 0xBF) * u), (byte)(0x24 + (0x16 - 0x24) * u));
    }
}
