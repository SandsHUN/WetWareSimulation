using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WetWare.Core.Course.Unsupervised;
using WetWare.Core.Mathematics;

namespace WetWare.App.ViewModels.Course.Lessons;

/// <summary>One recorded inference iteration: the causes, the prediction, the error and the two energy terms.</summary>
public sealed record InferenceFrame(double[] V, double[] Prediction, double[] Error, double Reconstruction, double Sparsity);

/// <summary>7.5 – predictive coding: inference as a loop of top-down predictions and bottom-up prediction errors.</summary>
public sealed partial class PredictiveCodingLesson : LessonViewModel
{
    public const int Iterations = 60;

    private readonly RandomSource _random = new(71);
    private double _playClock;

    public PredictiveCodingLesson(SparseCodingModel model) : base("M7.Predictive", "7.5")
    {
        Model = model;
        Present();
    }

    public SparseCodingModel Model { get; }

    public List<InferenceFrame> Frames { get; } = new();

    public double[] Input { get; private set; } = new double[SparseCodingModel.Dim];

    /// <summary>Seconds since activation (drives the pulses of the hierarchy diagram).</summary>
    public double Phase { get; private set; }

    [ObservableProperty] private string _stimulus = "Natural";
    [ObservableProperty] private double _iteration;
    [ObservableProperty] private bool _isPlaying = true;
    [ObservableProperty] private double _edgeAngle = 30;
    [ObservableProperty] private string _readout = "";
    [ObservableProperty] private string _explain = "";
    [ObservableProperty] private bool _needsTraining;

    public InferenceFrame Current => Frames[Math.Clamp((int)Iteration, 0, Frames.Count - 1)];

    partial void OnStimulusChanged(string value) => Present();

    partial void OnEdgeAngleChanged(double value)
    {
        if (Stimulus == "Edge") Present();
    }

    partial void OnIterationChanged(double value)
    {
        RefreshTexts();
        RaiseUpdated();
    }

    public override void Tick(double realMs)
    {
        Phase += realMs / 1000;
        if (IsPlaying)
        {
            _playClock += realMs;
            if (_playClock > 70)
            {
                _playClock = 0;
                if (Iteration < Frames.Count - 1) Iteration++;
                else IsPlaying = false;
            }
        }
        RaiseUpdated();
    }

    protected override void OnActivated()
    {
        // The dictionary may have been trained in lesson 7.4 meanwhile.
        NeedsTraining = Model.Steps < 150;
        Present();
    }

    [RelayCommand]
    private void Replay()
    {
        Iteration = 0;
        IsPlaying = true;
    }

    [RelayCommand]
    private void NewStimulus() => Present();

    [RelayCommand]
    private void QuickTrain()
    {
        for (int s = 0; s < 300; s++) Model.LearnStep();
        NeedsTraining = Model.Steps < 150;
        Present();
    }

    private void Present()
    {
        Input = Stimulus switch
        {
            "Edge" => Edge(EdgeAngle),
            "Noise" => Enumerable.Range(0, SparseCodingModel.Dim).Select(_ => 1.4 * _random.NextGaussian()).ToArray(),
            _ => NextNatural(),
        };
        Model.SetPatch(Input);
        Frames.Clear();
        double[] Copy(double[] a) => (double[])a.Clone();
        Model.Infer(Input, Iterations, (_, v) =>
        {
            var p = Model.Predict(v);
            var e = Input.Zip(p, (a, b) => a - b).ToArray();
            var (rec, sparse) = Model.Energy(Input, v);
            Frames.Add(new InferenceFrame(Copy(v), p, e, rec, sparse));
        });
        // Iteration 0 = before any update: nothing predicted yet, the whole input is "error".
        Frames.Insert(0, new InferenceFrame(new double[SparseCodingModel.Bases], new double[SparseCodingModel.Dim], Copy(Input),
            0.5 * Input.Sum(x => x * x), 0));
        _playClock = 0;
        Iteration = 0;
        IsPlaying = true;
        RefreshTexts();
        RaiseUpdated();
    }

    private double[] NextNatural()
    {
        Model.NextPatch();
        return Model.CurrentPatch;
    }

    /// <summary>A soft oriented edge through the patch centre.</summary>
    private static double[] Edge(double degrees)
    {
        double a = degrees * Math.PI / 180;
        var patch = new double[SparseCodingModel.Dim];
        for (int r = 0; r < SparseCodingModel.Patch; r++)
            for (int c = 0; c < SparseCodingModel.Patch; c++)
            {
                double x = c - 3.5, y = r - 3.5;
                patch[r * SparseCodingModel.Patch + c] = 1.6 * Math.Tanh(1.5 * (x * Math.Cos(a) + y * Math.Sin(a)));
            }
        double mean = patch.Average();
        return patch.Select(v => v - mean).ToArray();
    }

    protected override void OnLanguageChanged() => RefreshTexts();

    private void RefreshTexts()
    {
        if (Frames.Count == 0) return;
        var f = Current;
        double power = Input.Sum(x => x * x);
        double explained = 100 * (1 - 2 * f.Reconstruction / Math.Max(power, 1e-9));
        Readout = Loc.Format("M7.Predictive.Readout", (int)Iteration, F(f.Reconstruction, 2), F(f.Sparsity, 2),
            F(f.Reconstruction + f.Sparsity, 2), f.V.Count(v => v != 0), F(Math.Max(0, explained), 0));
        double finalExplained = 100 * (1 - 2 * Frames[^1].Reconstruction / Math.Max(power, 1e-9));
        Explain = Stimulus == "Noise" || finalExplained < 55
            ? Loc.Format("M7.Predictive.Explain.Surprise", F(Math.Max(0, finalExplained), 0))
            : Loc.Format("M7.Predictive.Explain.Expected", F(finalExplained, 0));
    }
}
