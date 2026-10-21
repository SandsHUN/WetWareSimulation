using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WetWare.Core.Course.Bci;

namespace WetWare.App.ViewModels.Course.Lessons;

/// <summary>8.4 – bidirectional BCIs: an artificial cortex → FES connection, and spike-triggered conditioning (STDP).</summary>
public sealed partial class BciLesson : LessonViewModel
{
    private double _hoursLeft;

    public BciLesson() : base("M8.Bci", "8.4")
    {
        BaselineSta = Conditioning.SpikeTriggeredAverage(Conditioning.InitialStrength);
        CurrentSta = BaselineSta;
        RefreshTexts();
    }

    public FesBridge Fes { get; } = new();
    public SpikeTriggeredConditioning Conditioning { get; } = new();

    public double[] BaselineSta { get; private set; }
    public double[] CurrentSta { get; private set; }

    [ObservableProperty] private bool _bciEnabled = true;
    [ObservableProperty] private bool _autoTarget = true;
    [ObservableProperty] private double _intent;
    [ObservableProperty] private double _gain = 0.12;
    [ObservableProperty] private double _threshold = 15;
    [ObservableProperty] private double _delay = 20;
    [ObservableProperty] private bool _randomControl;
    [ObservableProperty] private bool _isConditioning;
    [ObservableProperty] private string _fesText = "";
    [ObservableProperty] private string _conditioningText = "";

    partial void OnBciEnabledChanged(bool value) => Fes.BciEnabled = value;
    partial void OnAutoTargetChanged(bool value) => Fes.AutoTarget = value;
    partial void OnIntentChanged(double value) => Fes.Intent = value;
    partial void OnGainChanged(double value) => Fes.Gain = value;
    partial void OnThresholdChanged(double value) => Fes.ThresholdHz = value;
    partial void OnDelayChanged(double value) => Conditioning.DelayMs = value;
    partial void OnRandomControlChanged(bool value) => Conditioning.RandomControl = value;

    public override void Tick(double realMs)
    {
        Fes.Advance(realMs);
        if (IsConditioning)
        {
            // One simulated night (10 h) plays in about three seconds.
            double hours = Math.Min(_hoursLeft, realMs / 300);
            Conditioning.Condition(hours);
            _hoursLeft -= hours;
            CurrentSta = Conditioning.SpikeTriggeredAverage(Conditioning.Strength, seed: Environment.TickCount);
            if (_hoursLeft <= 1e-9) IsConditioning = false;
        }
        RefreshTexts();
        RaiseUpdated();
    }

    [RelayCommand]
    private void Night()
    {
        _hoursLeft = 10;
        IsConditioning = true;
    }

    [RelayCommand]
    private void ResetConditioning()
    {
        IsConditioning = false;
        Conditioning.Reset();
        CurrentSta = BaselineSta;
        RefreshTexts();
        RaiseUpdated();
    }

    [RelayCommand]
    private void ClearDelays()
    {
        Conditioning.EffectByDelay.Clear();
        RaiseUpdated();
    }

    protected override void OnLanguageChanged() => RefreshTexts();

    private void RefreshTexts()
    {
        var (rf, re) = Fes.DecodedRates;
        FesText = Loc.Format("M8.Bci.FesReadout", F(rf, 0), F(re, 0), F(Fes.CurrentF, 2), F(Fes.CurrentE, 2), F(Fes.WristAngle, 0), F(Fes.TrackingError, 1))
                  + "\n" + Loc[BciEnabled ? "M8.Bci.Fes.On" : "M8.Bci.Fes.Off"];
        double ratio = Conditioning.Strength / Conditioning.InitialStrength;
        ConditioningText = Loc.Format("M8.Bci.CondReadout", F(Conditioning.HoursConditioned, 1), F(Conditioning.Pairings / 1000.0, 0), F(ratio * 100, 0))
                           + "\n" + (RandomControl ? Loc["M8.Bci.Cond.Control"]
                               : Delay <= 30 ? Loc["M8.Bci.Cond.Short"]
                               : Loc["M8.Bci.Cond.Long"]);
    }
}
