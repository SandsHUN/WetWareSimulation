using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WetWare.Core.Course.RateNetworks;

namespace WetWare.App.ViewModels.Course.Lessons;

/// <summary>6.4 – nonlinear recurrent ring: selective amplification, winner-takes-all, gain modulation, working memory.</summary>
public sealed partial class NonlinearRecurrentLesson : LessonViewModel
{
    private double _cueLeft;

    public NonlinearRecurrentLesson() : base("M6.Nonlinear", "6.4") => Preset("Amplify");

    public RingNetwork Ring { get; } = new();

    /// <summary>Frozen output profiles for comparison (gain modulation).</summary>
    public List<(string Label, double[] Rates)> Snapshots { get; } = new();

    [ObservableProperty] private string _nonlinearity = nameof(RateNonlinearity.Rectified);
    [ObservableProperty] private double _j1 = 1.9;
    [ObservableProperty] private double _j0 = 0.5;
    [ObservableProperty] private double _background;
    [ObservableProperty] private double _noise = 3;
    [ObservableProperty] private double _cue;
    [ObservableProperty] private double _amplitude = 4;
    [ObservableProperty] private bool _secondBump;
    [ObservableProperty] private bool _inputOn = true;
    [ObservableProperty] private bool _isRunning = true;
    [ObservableProperty] private string _scenario = "Amplify";
    [ObservableProperty] private string _explain = "";
    [ObservableProperty] private string _readout = "";

    partial void OnNonlinearityChanged(string value) => Push();
    partial void OnJ1Changed(double value) => Push();
    partial void OnJ0Changed(double value) => Push();
    partial void OnBackgroundChanged(double value) => Push();
    partial void OnNoiseChanged(double value) => Push();
    partial void OnCueChanged(double value) => Push();
    partial void OnAmplitudeChanged(double value) => Push();
    partial void OnSecondBumpChanged(bool value) => Push();
    partial void OnInputOnChanged(bool value) => Push();

    private bool _loading;

    private void Push()
    {
        if (_loading) return;
        Configure(Ring, Background);
        Ring.InputOn = InputOn;
        RefreshTexts();
        RaiseUpdated();
    }

    private void Configure(RingNetwork ring, double background)
    {
        ring.Nonlinearity = Enum.Parse<RateNonlinearity>(Nonlinearity);
        ring.J1 = J1;
        ring.J0 = J0;
        ring.Background = background;
        ring.NoiseLevel = Noise;
        ring.BumpConcentration = Scenario == "Gain" ? 1 : 2.5;
        ring.Bumps.Clear();
        ring.Bumps.Add(new RingBump(Cue, Amplitude));
        if (SecondBump) ring.Bumps.Add(new RingBump(Cue + 180, Amplitude * 0.9));
    }

    public override void Tick(double realMs)
    {
        if (!IsRunning) return;
        if (_cueLeft > 0)
        {
            _cueLeft -= realMs * 0.5;
            if (_cueLeft <= 0) InputOn = false;
        }
        Ring.Advance(realMs * 0.5);
        RefreshTexts();
        RaiseUpdated();
    }

    [RelayCommand]
    private void ToggleRun() => IsRunning = !IsRunning;

    [RelayCommand]
    private void Reset()
    {
        Ring.Reset();
        RaiseUpdated();
    }

    /// <summary>Shows the cue for 250 ms (simulated), then removes it – does the bump survive?</summary>
    [RelayCommand]
    private void FlashCue()
    {
        InputOn = true;
        _cueLeft = 250;
    }

    [RelayCommand]
    private void Freeze()
    {
        if (Snapshots.Count >= 5) Snapshots.RemoveAt(0);
        Snapshots.Add(($"b = {F(Background, 1)}", (double[])Ring.Rates.Clone()));
        RaiseUpdated();
    }

    [RelayCommand]
    private void ClearSnapshots()
    {
        Snapshots.Clear();
        RaiseUpdated();
    }

    [RelayCommand]
    private void Preset(string id)
    {
        _loading = true;
        Scenario = id;
        Snapshots.Clear();
        _cueLeft = 0;
        InputOn = true;
        switch (id)
        {
            case "Amplify":
                (Nonlinearity, J1, J0, Background, Noise, Cue, Amplitude, SecondBump) = ("Rectified", 1.9, 0.5, 0, 3, 0, 4, false);
                break;
            case "Wta":
                (Nonlinearity, J1, J0, Background, Noise, Cue, Amplitude, SecondBump) = ("Saturating", 2.5, 4, 0, 0.5, -90, 10, true);
                break;
            case "Gain":
                (Nonlinearity, J1, J0, Background, Noise, Cue, Amplitude, SecondBump) = ("Rectified", 1.9, 2, 0, 0, 0, 4, false);
                break;
            case "Memory":
                (Nonlinearity, J1, J0, Background, Noise, Cue, Amplitude, SecondBump) = ("Saturating", 3, 1, 0, 0, 45, 10, false);
                break;
        }
        _loading = false;
        Configure(Ring, Background);
        Ring.InputOn = true;
        Ring.Reset();

        if (id == "Gain")
        {
            // Steady-state profiles for three background levels: the tuning shape stays, the amplitude scales.
            foreach (double b in new[] { 0.0, 3.0, 6.0 })
            {
                var probe = new RingNetwork();
                Configure(probe, b);
                probe.Advance(600);
                Snapshots.Add(($"b = {F(b, 0)}", (double[])probe.Rates.Clone()));
            }
        }
        if (id == "Memory") FlashCue();
        RefreshTexts();
        RaiseUpdated();
    }

    protected override void OnLanguageChanged() => RefreshTexts();

    private void RefreshTexts()
    {
        Explain = Loc[$"M6.Nonlinear.Explain.{Scenario}"];
        double max = Ring.Rates.Max();
        int active = Ring.Rates.Count(r => r > 0.5);
        double pv = Ring.PopulationVector;
        Readout = Loc.Format("M6.Nonlinear.Readout", F(max, 1), active, RingNetwork.Size,
            double.IsNaN(pv) ? "—" : F(pv, 0) + "°", InputOn ? Loc["Course.On"] : Loc["Course.Off"]);
    }
}
