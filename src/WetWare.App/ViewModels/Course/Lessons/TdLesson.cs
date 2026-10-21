using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WetWare.Core.Course.Reinforcement;

namespace WetWare.App.ViewModels.Course.Lessons;

/// <summary>8.2 – predicting rewards with temporal-difference learning; the TD error as the dopamine signal.</summary>
public sealed partial class TdLesson : LessonViewModel
{
    private double _clock;

    public TdLesson() : base("M8.Td", "8.2")
    {
        Td.RunTrial();
        RefreshTexts();
    }

    public TdConditioning Td { get; } = new();

    /// <summary>Per trial: TD error at the cue and at the reward (the two dopamine peaks).</summary>
    public List<(double Cue, double Reward)> PeakHistory { get; } = new();

    [ObservableProperty] private double _rewardTime = 45;
    [ObservableProperty] private double _rewardProbability = 1;
    [ObservableProperty] private double _learningRate = 0.3;
    [ObservableProperty] private double _gamma = 1;
    [ObservableProperty] private bool _isRunning = true;
    [ObservableProperty] private string _readout = "";
    [ObservableProperty] private string _explain = "";

    partial void OnRewardTimeChanged(double value) => Td.RewardTime = (int)value;
    partial void OnRewardProbabilityChanged(double value) => Td.RewardProbability = value;
    partial void OnLearningRateChanged(double value) => Td.LearningRate = value;
    partial void OnGammaChanged(double value) => Td.Gamma = value;

    public override void Tick(double realMs)
    {
        if (!IsRunning) return;
        _clock += realMs;
        if (_clock < 120) return;
        _clock = 0;
        Run(TrialKind.Paired);
        if (Td.Trials.Count >= 400) IsRunning = false;
    }

    private void Run(TrialKind kind)
    {
        var trial = Td.RunTrial(kind);
        PeakHistory.Add(Td.Peaks(trial));
        if (PeakHistory.Count > 400) PeakHistory.RemoveAt(0);
        RefreshTexts();
        RaiseUpdated();
    }

    [RelayCommand]
    private void ToggleRun() => IsRunning = !IsRunning;

    [RelayCommand]
    private void Trial() => Run(TrialKind.Paired);

    [RelayCommand]
    private void Omission() => Run(TrialKind.Omission);

    [RelayCommand]
    private void RewardOnly() => Run(TrialKind.RewardOnly);

    [RelayCommand]
    private void Reset()
    {
        Td.Reset();
        PeakHistory.Clear();
        Td.RunTrial();
        RefreshTexts();
        RaiseUpdated();
    }

    protected override void OnLanguageChanged() => RefreshTexts();

    private void RefreshTexts()
    {
        var last = Td.Last;
        if (last is null) return;
        var (cue, reward) = Td.Peaks(last);
        Readout = Loc.Format("M8.Td.Readout", last.Index, F(cue, 2), F(reward, 2), F(last.Value[Math.Min(TdConditioning.Steps - 1, Td.CueTime)], 2));
        Explain = last.Kind switch
        {
            TrialKind.Omission => Loc["M8.Td.Explain.Omission"],
            TrialKind.RewardOnly => Loc["M8.Td.Explain.RewardOnly"],
            _ => cue > 0.6 ? Loc["M8.Td.Explain.Learned"]
                : reward > 0.5 ? Loc["M8.Td.Explain.Early"]
                : Loc["M8.Td.Explain.Learning"],
        };
    }
}
