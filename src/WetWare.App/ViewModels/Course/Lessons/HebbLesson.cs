using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WetWare.Core.Course.Unsupervised;

namespace WetWare.App.ViewModels.Course.Lessons;

/// <summary>7.1 – Hebb, covariance and Oja rules on a 2-D input cloud; the weight vector finds the principal component.</summary>
public sealed partial class HebbLesson : LessonViewModel
{
    public HebbLesson() : base("M7.Hebb", "7.1")
    {
        Regenerate();
    }

    public HebbianLearner Learner { get; } = new();

    [ObservableProperty] private string _rule = nameof(HebbianRule.Oja);
    [ObservableProperty] private double _learningRate = 0.01;
    [ObservableProperty] private double _alpha = 1;
    [ObservableProperty] private double _cloudAngle = 35;
    [ObservableProperty] private double _elongation = 3.5;
    [ObservableProperty] private double _offset;
    [ObservableProperty] private bool _twoClusters;
    [ObservableProperty] private double _speed = 4;
    [ObservableProperty] private bool _isRunning = true;
    [ObservableProperty] private string _readout = "";
    [ObservableProperty] private string _explain = "";

    partial void OnRuleChanged(string value)
    {
        Learner.Rule = Enum.Parse<HebbianRule>(value);
        Learner.Reset();
        RefreshTexts();
        RaiseUpdated();
    }

    partial void OnLearningRateChanged(double value) => Learner.LearningRate = value;

    partial void OnAlphaChanged(double value)
    {
        Learner.Alpha = value;
        RefreshTexts();
    }

    partial void OnCloudAngleChanged(double value) => Regenerate();
    partial void OnElongationChanged(double value) => Regenerate();
    partial void OnOffsetChanged(double value) => Regenerate();
    partial void OnTwoClustersChanged(bool value) => Regenerate();

    private void Regenerate()
    {
        Learner.CloudAngle = CloudAngle;
        Learner.MajorSd = 0.4 * Elongation;
        Learner.MinorSd = 0.4;
        // The offset is applied perpendicular to the cloud axis, so the mean and the principal axis disagree.
        double a = (CloudAngle + 90) * Math.PI / 180;
        Learner.MeanX = Offset * Math.Cos(a);
        Learner.MeanY = Offset * Math.Sin(a);
        Learner.TwoClusters = TwoClusters;
        Learner.Regenerate();
        RefreshTexts();
        RaiseUpdated();
    }

    public override void Tick(double realMs)
    {
        if (!IsRunning) return;
        Learner.Step((int)Math.Max(1, Speed));
        RefreshTexts();
        RaiseUpdated();
    }

    [RelayCommand]
    private void ToggleRun() => IsRunning = !IsRunning;

    [RelayCommand]
    private void StepOnce()
    {
        Learner.Step();
        RefreshTexts();
        RaiseUpdated();
    }

    [RelayCommand]
    private void Reset()
    {
        Learner.Reset(Environment.TickCount);
        RefreshTexts();
        RaiseUpdated();
    }

    [RelayCommand]
    private void Preset(string id)
    {
        switch (id)
        {
            case "Hebb":
                (Rule, Offset, TwoClusters) = ("Hebb", 0, false);
                break;
            case "Oja":
                (Rule, Offset, TwoClusters) = ("Oja", 0, false);
                break;
            case "Offset":
                (Rule, Offset, TwoClusters) = ("Oja", 1.6, false);
                break;
            case "Covariance":
                (Rule, Offset, TwoClusters) = ("Covariance", 1.6, false);
                break;
            case "Clusters":
                (Rule, Offset, TwoClusters) = ("Oja", 0, true);
                break;
        }
        Learner.Reset();
        IsRunning = true;
    }

    protected override void OnLanguageChanged() => RefreshTexts();

    private void RefreshTexts()
    {
        var pc = Learner.Principal;
        Readout = Loc.Format("M7.Hebb.Readout", Learner.Steps, F(Learner.Norm, 3), F(Learner.AngleToPrincipal, 1), F(pc.Lambda1, 2), F(pc.Lambda2, 2));
        Explain = Learner.Rule switch
        {
            HebbianRule.Hebb => Loc.Format("M7.Hebb.Explain.Hebb", F(Learner.Norm, 2)),
            HebbianRule.Covariance => Loc.Format("M7.Hebb.Explain.Covariance", F(Learner.Norm, 2)),
            _ => TwoClusters
                ? Loc["M7.Hebb.Explain.Clusters"]
                : Loc.Format("M7.Hebb.Explain.Oja", F(1 / Math.Sqrt(Learner.Alpha), 2), F(Learner.AngleToPrincipal, 1)),
        };
    }
}
