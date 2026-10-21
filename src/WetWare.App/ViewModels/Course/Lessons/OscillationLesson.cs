using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WetWare.Core.Course.RateNetworks;
using WetWare.Core.Mathematics;

namespace WetWare.App.ViewModels.Course.Lessons;

/// <summary>6.5 – non-symmetric E–I network: complex eigenvalues, damped oscillation and limit cycle.</summary>
public sealed partial class OscillationLesson : LessonViewModel
{
    private readonly RandomSource _random = new(19);

    public OscillationLesson() : base("M6.Oscillation", "6.5") => Push();

    public ExcitatoryInhibitoryNetwork Net { get; } = new();

    [ObservableProperty] private double _tauI = 30;
    [ObservableProperty] private double _mee = 1.25;
    [ObservableProperty] private bool _isRunning = true;
    [ObservableProperty] private string _eigenText = "";
    [ObservableProperty] private string _verdict = "";
    [ObservableProperty] private bool _stable = true;

    partial void OnTauIChanged(double value) => Push();
    partial void OnMeeChanged(double value) => Push();

    private void Push()
    {
        Net.TauI = TauI;
        Net.MEE = Mee;
        RefreshTexts();
        RaiseUpdated();
    }

    private double _restingMs;

    public override void Tick(double realMs)
    {
        if (!IsRunning) return;
        Net.Advance(realMs * 0.6);
        // A damped network soon sits still at its fixed point: kick it again so the spiral keeps being visible.
        var fp = Net.FixedPoint;
        bool resting = Net.IsStable && Math.Abs(Net.E - fp.E) + Math.Abs(Net.I - fp.I) < 0.5;
        _restingMs = resting ? _restingMs + realMs : 0;
        if (_restingMs > 1500) Kick();
        RaiseUpdated();
    }

    [RelayCommand]
    private void ToggleRun() => IsRunning = !IsRunning;

    [RelayCommand]
    private void Kick()
    {
        var fp = Net.FixedPoint;
        Net.SetState(fp.E + _random.Uniform(-15, 15), fp.I + _random.Uniform(-10, 10));
        RaiseUpdated();
    }

    [RelayCommand]
    private void Preset(string id)
    {
        Mee = 1.25;
        TauI = id switch { "Damped" => 30, "Critical" => 40, _ => 50 };
        Net.Reset();
    }

    public void SetState(double e, double i)
    {
        Net.SetState(e, i);
        RaiseUpdated();
    }

    protected override void OnLanguageChanged() => RefreshTexts();

    private void RefreshTexts()
    {
        var (l1, l2) = Net.Eigenvalues;
        string Complex(System.Numerics.Complex z) =>
            Math.Abs(z.Imaginary) < 1e-6 ? F(z.Real, 1) : $"{F(z.Real, 1)} {(z.Imaginary < 0 ? "−" : "+")} {F(Math.Abs(z.Imaginary), 1)}i";
        EigenText = $"λ₁ = {Complex(l1)}   λ₂ = {Complex(l2)}   (1/s)";
        Stable = Net.IsStable;
        double f = Net.PredictedFrequency;
        Verdict = Math.Abs(l1.Imaginary) < 1e-6
            ? Loc[Stable ? "M6.Oscillation.Verdict.Node" : "M6.Oscillation.Verdict.Saddle"]
            : Stable
                ? Loc.Format("M6.Oscillation.Verdict.Damped", F(f, 1), F(Net.CriticalTauI, 0))
                : Loc.Format("M6.Oscillation.Verdict.Cycle", F(f, 1), F(Net.CriticalTauI, 0));
    }
}
