using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WetWare.Core.Course.Reinforcement;

namespace WetWare.App.ViewModels.Course.Lessons;

/// <summary>8.3 – action selection: policy evaluation with TD, softmax exploration and the actor–critic.</summary>
public sealed partial class ActorCriticLesson : LessonViewModel
{
    private const double TrialMs = 900;
    private double _animation = 1;

    public ActorCriticLesson() : base("M8.ActorCritic", "8.3") => RefreshTexts();

    public BarnMaze Maze { get; } = new();

    /// <summary>0–1 progress of the rat animation for the last trial.</summary>
    public double Animation => _animation;

    [ObservableProperty] private string _mode = "ActorCritic";
    [ObservableProperty] private double _beta = 1;
    [ObservableProperty] private double _criticRate = 0.2;
    [ObservableProperty] private double _actorRate = 0.2;
    [ObservableProperty] private double _rewardBL;
    [ObservableProperty] private double _rewardBR = 5;
    [ObservableProperty] private double _rewardCL = 2;
    [ObservableProperty] private double _rewardCR;
    [ObservableProperty] private double _speed = 1;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _readout = "";
    [ObservableProperty] private string _explain = "";

    partial void OnModeChanged(string value)
    {
        Maze.ActorLearns = value == "ActorCritic";
        Maze.Reset();
        RefreshTexts();
        RaiseUpdated();
    }

    partial void OnBetaChanged(double value)
    {
        Maze.Beta = value;
        RaiseUpdated();
    }

    partial void OnCriticRateChanged(double value) => Maze.CriticRate = value;
    partial void OnActorRateChanged(double value) => Maze.ActorRate = value;
    partial void OnRewardBLChanged(double value) => SetRewards();
    partial void OnRewardBRChanged(double value) => SetRewards();
    partial void OnRewardCLChanged(double value) => SetRewards();
    partial void OnRewardCRChanged(double value) => SetRewards();

    private void SetRewards()
    {
        Maze.Rewards[1, 0] = RewardBL;
        Maze.Rewards[1, 1] = RewardBR;
        Maze.Rewards[2, 0] = RewardCL;
        Maze.Rewards[2, 1] = RewardCR;
        RefreshTexts();
        RaiseUpdated();
    }

    public override void Tick(double realMs)
    {
        bool animating = _animation < 1;
        _animation = Math.Min(1, _animation + realMs * Speed / TrialMs);
        if (IsRunning && _animation >= 1)
        {
            // At high speed several trials run per frame; only the last one is animated.
            int trials = Speed > 4 ? (int)(Speed / 2) : 1;
            for (int k = 0; k < trials; k++) Maze.RunTrial();
            _animation = 0;
            RefreshTexts();
        }
        if (animating || IsRunning) RaiseUpdated();
    }

    [RelayCommand]
    private void ToggleRun() => IsRunning = !IsRunning;

    [RelayCommand]
    private void Trial()
    {
        Maze.RunTrial();
        _animation = 0;
        RefreshTexts();
        RaiseUpdated();
    }

    [RelayCommand]
    private void Reset()
    {
        IsRunning = false;
        Maze.Reset();
        _animation = 1;
        RefreshTexts();
        RaiseUpdated();
    }

    protected override void OnLanguageChanged() => RefreshTexts();

    private void RefreshTexts()
    {
        var target = Maze.RandomPolicyValues;
        Readout = Loc.Format("M8.ActorCritic.Readout", Maze.Trials, F(Maze.Value(BarnState.A), 2), F(Maze.Value(BarnState.B), 2),
            F(Maze.Value(BarnState.C), 2));
        Explain = Maze.ActorLearns
            ? Loc.Format("M8.ActorCritic.Explain.Actor", F(Maze.ProbabilityRight(BarnState.A) * 100, 0),
                F(Maze.ProbabilityRight(BarnState.B) * 100, 0), F((1 - Maze.ProbabilityRight(BarnState.C)) * 100, 0))
            : Loc.Format("M8.ActorCritic.Explain.Evaluation", F(target.A, 2), F(target.B, 2), F(target.C, 2));
    }
}
