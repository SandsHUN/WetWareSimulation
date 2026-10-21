using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WetWare.Core.Course.RateNetworks;

namespace WetWare.App.ViewModels.Course.Lessons;

/// <summary>6.3 – linear recurrent networks: eigenmodes, amplification by 1/(1 − λ), integration and memory at λ = 1.</summary>
public sealed partial class LinearRecurrentLesson : LessonViewModel
{
    public LinearRecurrentLesson() : base("M6.Linear", "6.3")
    {
        Ring.Nonlinearity = RateNonlinearity.Linear;
        Ring.NoiseLevel = 2.5;
        Ring.Bumps[0] = new RingBump(0, 4);
        Ring.BumpConcentration = 1.2;
        Push();
        // Start with one burst so the decay of the mode is visible immediately.
        Mode.Pulse(1);
    }

    public EigenmodeIntegrator Mode { get; } = new();
    public RingNetwork Ring { get; } = new();

    [ObservableProperty] private double _lambda = 0.9;
    [ObservableProperty] private double _sustained;
    [ObservableProperty] private double _noise = 2.5;
    [ObservableProperty] private bool _isRunning = true;
    [ObservableProperty] private string _gainText = "";
    [ObservableProperty] private string _explain = "";
    [ObservableProperty] private string _ringText = "";

    partial void OnLambdaChanged(double value) => Push();
    partial void OnSustainedChanged(double value) => Push();

    partial void OnNoiseChanged(double value)
    {
        Ring.NoiseLevel = value;
        Ring.Reset();
        Push();
    }

    private void Push()
    {
        Mode.Lambda = Lambda;
        Mode.Sustained = Sustained;
        Ring.J1 = Lambda;
        RefreshTexts();
        RaiseUpdated();
    }

    public override void Tick(double realMs)
    {
        if (!IsRunning) return;
        Mode.Advance(realMs);
        Ring.Advance(realMs * 0.5);
        RefreshTexts();
        RaiseUpdated();
    }

    [RelayCommand]
    private void ToggleRun() => IsRunning = !IsRunning;

    [RelayCommand]
    private void Pulse(string sign) => Mode.Pulse(sign == "+" ? 1 : -1);

    [RelayCommand]
    private void Reset()
    {
        Mode.Reset();
        Ring.Reset();
        RaiseUpdated();
    }

    [RelayCommand]
    private void NewNoise()
    {
        Ring.ResampleNoise(Environment.TickCount);
        Ring.Reset();
    }

    [RelayCommand]
    private void Preset(string id)
    {
        Lambda = id switch
        {
            "Leaky" => 0.5,
            "Amplifier" => 0.9,
            "Integrator" => 1.0,
            "Unstable" => 1.03,
            _ => 0,
        };
        Sustained = 0;
        Reset();
    }

    protected override void OnLanguageChanged() => RefreshTexts();

    private void RefreshTexts()
    {
        GainText = Lambda < 1
            ? Loc.Format("M6.Linear.Gain", F(Mode.Gain, 2), F(Mode.EffectiveTau, 0))
            : Lambda == 1 ? Loc["M6.Linear.Gain.Integrator"] : Loc["M6.Linear.Gain.Unstable"];

        Explain = Lambda < 0.001 ? Loc["M6.Linear.Explain.None"]
            : Lambda < 0 ? Loc["M6.Linear.Explain.Negative"]
            : Lambda < 0.98 ? Loc.Format("M6.Linear.Explain.Stable", F(Lambda, 2), F(Mode.EffectiveTau, 0))
            : Lambda <= 1.0 ? Loc["M6.Linear.Explain.Integrator"]
            : Loc["M6.Linear.Explain.Unstable"];

        double outMax = Ring.Rates.Max(), inMax = Ring.Input.Max();
        RingText = Lambda >= 1
            ? Loc["M6.Linear.Ring.Unstable"]
            : Loc.Format("M6.Linear.Ring.Readout", F(inMax, 1), F(outMax, 1), F(Ring.CosineGain, 1));
    }
}
