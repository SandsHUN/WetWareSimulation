using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WetWare.Core.Course.Unsupervised;
using WetWare.Core.Mathematics;

namespace WetWare.App.ViewModels.Course.Lessons;

/// <summary>7.2 – neurons as prototypes: winner-takes-all competitive learning, K-means and the self-organizing map.</summary>
public sealed partial class CompetitiveLesson : LessonViewModel
{
    private readonly RandomSource _random = new(53);
    private double _kmeansClock;

    public CompetitiveLesson() : base("M7.Competitive", "7.2")
    {
        Learner.SetCount(3);
        RefreshTexts();
    }

    public PrototypeLearner Learner { get; } = new();

    [ObservableProperty] private string _mode = nameof(PrototypeMode.Competitive);
    [ObservableProperty] private string _layout = nameof(ClusterLayout.ThreeBlobs);
    [ObservableProperty] private double _count = 3;
    [ObservableProperty] private double _learningRate = 0.05;
    [ObservableProperty] private double _speed = 3;
    [ObservableProperty] private bool _isRunning = true;
    [ObservableProperty] private bool _showRegions = true;
    [ObservableProperty] private string _readout = "";
    [ObservableProperty] private string _explain = "";

    partial void OnModeChanged(string value)
    {
        Learner.Mode = Enum.Parse<PrototypeMode>(value);
        if (Learner.Mode == PrototypeMode.SelfOrganizingMap && Count < 8) Count = 12;
        else Learner.Reset();
        RefreshTexts();
        RaiseUpdated();
    }

    partial void OnLayoutChanged(string value)
    {
        Learner.SetData(ClusterData.Generate(Enum.Parse<ClusterLayout>(value)));
        RefreshTexts();
        RaiseUpdated();
    }

    partial void OnCountChanged(double value)
    {
        Learner.SetCount((int)Math.Round(value));
        RefreshTexts();
        RaiseUpdated();
    }

    partial void OnLearningRateChanged(double value) => Learner.LearningRate = value;

    partial void OnShowRegionsChanged(bool value) => RaiseUpdated();

    public override void Tick(double realMs)
    {
        if (!IsRunning) return;
        if (Learner.Mode == PrototypeMode.KMeans)
        {
            // Batch K-means converges in a handful of iterations: show them at 2 per second.
            _kmeansClock += realMs;
            if (_kmeansClock < 500) return;
            _kmeansClock = 0;
            Learner.Step();
        }
        else
        {
            Learner.Step((int)Speed);
        }
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

    /// <summary>Click on the canvas: drop a small new cluster of 25 points there.</summary>
    public void AddCluster(double x, double y)
    {
        var points = new List<(double X, double Y)>(Learner.Points);
        for (int k = 0; k < 25; k++) points.Add((x + 0.3 * _random.NextGaussian(), y + 0.3 * _random.NextGaussian()));
        var protos = Learner.Prototypes.ToList();
        Learner.SetData(points);
        // Keep the learned prototypes so the network has to adapt to the new data.
        for (int k = 0; k < protos.Count && k < Learner.Prototypes.Count; k++) Learner.Prototypes[k] = protos[k];
        RaiseUpdated();
    }

    protected override void OnLanguageChanged() => RefreshTexts();

    private void RefreshTexts()
    {
        Readout = Learner.Mode == PrototypeMode.KMeans
            ? Loc.Format("M7.Competitive.Readout.KMeans", Learner.Steps, F(Learner.Distortion, 3))
            : Learner.Mode == PrototypeMode.SelfOrganizingMap
                ? Loc.Format("M7.Competitive.Readout.Som", Learner.Steps, F(Learner.Distortion, 3), F(Learner.Neighbourhood, 2))
                : Loc.Format("M7.Competitive.Readout", Learner.Steps, F(Learner.Distortion, 3));
        Explain = Loc[$"M7.Competitive.Explain.{Learner.Mode}"];
    }
}
