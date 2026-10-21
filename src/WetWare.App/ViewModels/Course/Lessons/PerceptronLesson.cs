using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WetWare.Core.Course.Supervised;

namespace WetWare.App.ViewModels.Course.Lessons;

/// <summary>8.1 – neurons as classifiers: the perceptron rule, linear separability, XOR and a backprop-trained MLP.</summary>
public sealed partial class PerceptronLesson : LessonViewModel
{
    private double _clock;

    public PerceptronLesson() : base("M8.Perceptron", "8.1")
    {
        Data = PlaneDatasets.Generate(PlaneDataset.Separable);
        Perceptron.Reset();
        Mlp.Reset(4);
        RefreshTexts();
    }

    public List<LabeledPoint> Data { get; private set; }
    public Perceptron Perceptron { get; } = new();
    public MultilayerPerceptron Mlp { get; } = new();

    /// <summary>Perceptron accuracy after every presentation (for the learning curve).</summary>
    public List<double> AccuracyHistory { get; } = new();

    public bool UsesMlp => Model == "Mlp";

    [ObservableProperty] private string _dataset = nameof(PlaneDataset.Separable);
    [ObservableProperty] private string _model = "Perceptron";
    [ObservableProperty] private double _hidden = 4;
    [ObservableProperty] private double _beta = 1.5;
    [ObservableProperty] private double _learningRate = 0.1;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _readout = "";
    [ObservableProperty] private string _explain = "";

    partial void OnDatasetChanged(string value)
    {
        Data = PlaneDatasets.Generate(Enum.Parse<PlaneDataset>(value));
        ResetModels();
    }

    partial void OnModelChanged(string value)
    {
        OnPropertyChanged(nameof(UsesMlp));
        LearningRate = value == "Mlp" ? 0.5 : 0.1;
        ResetModels();
    }

    partial void OnHiddenChanged(double value) => ResetModels();

    partial void OnBetaChanged(double value)
    {
        Mlp.Beta = value;
        RaiseUpdated();
    }

    partial void OnLearningRateChanged(double value)
    {
        Perceptron.LearningRate = value;
        Mlp.LearningRate = value;
    }

    private void ResetModels()
    {
        Perceptron.Reset(Environment.TickCount);
        Mlp.Reset((int)Hidden, Environment.TickCount);
        Mlp.Beta = Beta;
        AccuracyHistory.Clear();
        RefreshTexts();
        RaiseUpdated();
    }

    public override void Tick(double realMs)
    {
        if (!IsRunning) return;
        _clock += realMs;
        if (UsesMlp)
        {
            for (int e = 0; e < 3; e++) Mlp.TrainEpoch(Data);
        }
        else
        {
            // One presentation every 120 ms so each correction of the line can be followed.
            if (_clock < 120) return;
            _clock = 0;
            StepPerceptron();
        }
        RefreshTexts();
        RaiseUpdated();
    }

    private void StepPerceptron()
    {
        Perceptron.Step(Data);
        AccuracyHistory.Add(Perceptron.Accuracy(Data));
        if (AccuracyHistory.Count > 600) AccuracyHistory.RemoveAt(0);
    }

    [RelayCommand]
    private void ToggleRun() => IsRunning = !IsRunning;

    [RelayCommand]
    private void StepOnce()
    {
        if (UsesMlp) Mlp.TrainEpoch(Data);
        else StepPerceptron();
        RefreshTexts();
        RaiseUpdated();
    }

    [RelayCommand]
    private void Reset()
    {
        IsRunning = false;
        ResetModels();
    }

    [RelayCommand]
    private void ClearData()
    {
        Data = new List<LabeledPoint>();
        ResetModels();
    }

    /// <summary>Left click adds a +1 example, right click a −1 example.</summary>
    public void AddPoint(double x, double y, int label)
    {
        Data = new List<LabeledPoint>(Data) { new(x, y, label) };
        RefreshTexts();
        RaiseUpdated();
    }

    protected override void OnLanguageChanged() => RefreshTexts();

    private void RefreshTexts()
    {
        double accuracy = UsesMlp ? Mlp.Accuracy(Data) : Perceptron.Accuracy(Data);
        Readout = UsesMlp
            ? Loc.Format("M8.Perceptron.Readout.Mlp", Mlp.Epochs, F(Mlp.LossHistory.LastOrDefault(), 4), F(accuracy * 100, 0))
            : Loc.Format("M8.Perceptron.Readout", Perceptron.Presentations, Perceptron.Updates, F(accuracy * 100, 0),
                F(Perceptron.W1, 2), F(Perceptron.W2, 2), F(Perceptron.Mu, 2));
        bool hard = Dataset is "Xor" or "Circle" or "Moons";
        Explain = UsesMlp
            ? Loc[hard ? "M8.Perceptron.Explain.MlpHard" : "M8.Perceptron.Explain.Mlp"]
            : Loc[hard ? "M8.Perceptron.Explain.Hard" : accuracy >= 0.999 ? "M8.Perceptron.Explain.Done" : "M8.Perceptron.Explain.Learning"];
    }
}
