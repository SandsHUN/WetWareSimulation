using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WetWare.App.Services;
using WetWare.Core.Mathematics;
using WetWare.Core.Recognition;

namespace WetWare.App.ViewModels;

/// <summary>State of one output neuron as shown in the output layer list.</summary>
public sealed partial class OutputNeuronItem : ObservableObject
{
    public OutputNeuronItem(int index) => Index = index;

    public int Index { get; }

    [ObservableProperty] private string _label = "?";
    [ObservableProperty] private int _spikes;
    [ObservableProperty] private double _level;
    [ObservableProperty] private bool _isWinner;
}

/// <summary>Unsupervised spiking digit recogniser: draw, train with STDP, watch receptive fields emerge, recognise.</summary>
public sealed partial class RecognitionViewModel : PageViewModel
{
    private const int StepsPerFrame = 6;

    private readonly FrameClock _clock;
    private readonly RandomSource _random = new(77);
    private double _boost;
    private int _attempt;
    private int _lastEvaluated;

    public RecognitionViewModel(FrameClock clock)
    {
        _clock = clock;
        Outputs = new ObservableCollection<OutputNeuronItem>(Enumerable.Range(0, Classifier.OutputCount).Select(i => new OutputNeuronItem(i)));
        LoadDigit("3");
        UpdateOutputs();
        RefreshTexts();
    }

    public override string Key => "Recognition";
    public override string Glyph => "";

    public SpikingClassifier Classifier { get; private set; } = new(20, 1);

    public double[] Pad { get; } = new double[DigitPatterns.PixelCount];

    public ObservableCollection<OutputNeuronItem> Outputs { get; }

    /// <summary>Accuracy (0–1) after every 100 training samples.</summary>
    public List<(int Samples, double Accuracy)> AccuracyHistory { get; } = new();

    /// <summary>Spikes of the animated presentation: (time ms, row) with rows 0–62 inputs and 63+ outputs.</summary>
    public List<(double Time, int Row)> PresentationSpikes { get; } = new();

    public event EventHandler? PadChanged;
    public event EventHandler? WeightsChanged;
    public event EventHandler? PresentationStep;

    [ObservableProperty] private bool _isTraining;
    [ObservableProperty] private bool _isRecognizing;
    [ObservableProperty] private double _trainSpeed = 6;
    [ObservableProperty] private double _trainNoise = 0.04;
    [ObservableProperty] private double _testNoise = 0.08;
    [ObservableProperty] private string _predictedText = "?";
    [ObservableProperty] private string _resultText = "";
    [ObservableProperty] private Brush _resultBrush = Brushes.Gray;
    [ObservableProperty] private string _samplesText = "0";
    [ObservableProperty] private string _accuracyText = "—";
    [ObservableProperty] private double _accuracyLevel;
    [ObservableProperty] private string _labelsText = "";
    [ObservableProperty] private int _expectedDigit = 3;

    protected override void OnActivated() => _clock.Tick += OnTick;

    protected override void OnDeactivated()
    {
        _clock.Tick -= OnTick;
        IsTraining = false;
    }

    protected override void OnLanguageChanged() => RefreshTexts();

    private void OnTick(object? sender, double realMs)
    {
        if (IsRecognizing)
        {
            AnimatePresentation();
            return;
        }
        if (!IsTraining) return;

        int samples = (int)TrainSpeed;
        for (int k = 0; k < samples; k++) Classifier.TrainStep(TrainNoise);
        if (Classifier.TrainedSamples - _lastEvaluated >= 100)
        {
            _lastEvaluated = Classifier.TrainedSamples;
            double accuracy = Classifier.Evaluate(3, TestNoise);
            AccuracyHistory.Add((Classifier.TrainedSamples, accuracy));
        }
        UpdateOutputs();
        RefreshTexts();
        WeightsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AnimatePresentation()
    {
        var c = Classifier;
        for (int s = 0; s < StepsPerFrame && c.IsPresenting; s++)
        {
            c.StepPresentation();
            double t = c.TimeMs;
            for (int i = 0; i < c.InputCount; i++)
                if (c.InputSpikes[i]) PresentationSpikes.Add((t, i));
            for (int j = 0; j < c.OutputCount; j++)
                if (c.OutputSpikes[j]) PresentationSpikes.Add((t, c.InputCount + j));
        }
        UpdateOutputs();
        PresentationStep?.Invoke(this, EventArgs.Empty);
        if (c.IsPresenting) return;

        if (c.SpikeCounts.Sum() < c.MinimumOutputSpikes && _attempt < SpikingClassifier.MaxAttempts - 1)
        {
            // Too few output spikes: repeat with a stronger stimulus (higher input firing rate).
            _attempt++;
            _boost += c.BoostStepHz;
            PresentationSpikes.Clear();
            c.BeginPresentation((double[])Pad.Clone(), learn: false, _boost);
            ResultText = Loc.Format("Rec.Boost", F(c.RateOnHz + _boost, 0));
            return;
        }

        var result = c.EndPresentation(null);
        IsRecognizing = false;
        UpdateOutputs(result.WinnerNeuron);
        if (result.Predicted < 0)
        {
            PredictedText = "?";
            ResultText = Loc["Rec.Unknown"];
            ResultBrush = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24));
            return;
        }
        double total = result.ClassScores.Sum();
        double confidence = total > 0 ? result.ClassScores[result.Predicted] / total : 0;
        PredictedText = result.Predicted.ToString(Loc.Culture);
        bool correct = result.Predicted == ExpectedDigit;
        ResultText = Loc.Format("Rec.Result", result.Predicted, F(confidence * 100, 0), result.WinnerNeuron, result.SpikeCounts.Sum());
        ResultBrush = new SolidColorBrush(correct ? Color.FromRgb(0x34, 0xD3, 0x99) : Color.FromRgb(0x22, 0xD3, 0xEE));
    }

    private void UpdateOutputs(int winner = -1)
    {
        var c = Classifier;
        int max = Math.Max(1, c.SpikeCounts.Max());
        for (int j = 0; j < c.OutputCount; j++)
        {
            var item = Outputs[j];
            item.Label = c.Labels[j] < 0 ? "–" : c.Labels[j].ToString(Loc.Culture);
            item.Spikes = c.SpikeCounts[j];
            item.Level = (double)c.SpikeCounts[j] / max;
            item.IsWinner = j == winner;
        }
    }

    private void RefreshTexts()
    {
        SamplesText = Classifier.TrainedSamples.ToString("N0", Loc.Culture);
        if (AccuracyHistory.Count > 0)
        {
            double acc = AccuracyHistory[^1].Accuracy;
            AccuracyText = $"{F(acc * 100, 0)} %";
            AccuracyLevel = acc;
        }
        else
        {
            AccuracyText = "—";
            AccuracyLevel = 0;
        }
        int assigned = Classifier.Labels.Count(l => l >= 0);
        int digits = Classifier.Labels.Where(l => l >= 0).Distinct().Count();
        LabelsText = Loc.Format("Rec.LabelsInfo", assigned, Classifier.OutputCount, digits);
        if (Classifier.TrainedSamples == 0 && !IsRecognizing) ResultText = Loc["Rec.Untrained"];
    }

    public void PaintPixel(int x, int y, bool erase)
    {
        Pad[y * DigitPatterns.Width + x] = erase ? 0 : 1;
        PadChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void LoadDigit(string digit)
    {
        int d = int.Parse(digit);
        ExpectedDigit = d;
        Array.Copy(DigitPatterns.Template(d), Pad, Pad.Length);
        PadChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ClearPad()
    {
        Array.Clear(Pad);
        PadChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void AddNoise()
    {
        for (int i = 0; i < Pad.Length; i++)
            if (_random.Bernoulli(TestNoise)) Pad[i] = 1 - Pad[i];
        PadChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Recognize()
    {
        IsTraining = false;
        _boost = 0;
        _attempt = 0;
        PresentationSpikes.Clear();
        Classifier.BeginPresentation((double[])Pad.Clone(), learn: false);
        IsRecognizing = true;
        PredictedText = "…";
        ResultText = Loc["Rec.Presenting"];
        ResultBrush = new SolidColorBrush(Color.FromRgb(0x97, 0xA3, 0xC2));
    }

    [RelayCommand]
    private void ToggleTraining()
    {
        if (IsRecognizing) return;
        IsTraining = !IsTraining;
    }

    [RelayCommand]
    private void ResetNetwork()
    {
        IsTraining = false;
        IsRecognizing = false;
        Classifier = new SpikingClassifier(20, Environment.TickCount);
        AccuracyHistory.Clear();
        _lastEvaluated = 0;
        PresentationSpikes.Clear();
        PredictedText = "?";
        UpdateOutputs();
        RefreshTexts();
        WeightsChanged?.Invoke(this, EventArgs.Empty);
        PresentationStep?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void QuickTrain()
    {
        IsTraining = false;
        for (int k = 0; k < 300; k++)
        {
            Classifier.TrainStep(TrainNoise);
            if (Classifier.TrainedSamples - _lastEvaluated >= 100)
            {
                _lastEvaluated = Classifier.TrainedSamples;
                AccuracyHistory.Add((Classifier.TrainedSamples, Classifier.Evaluate(3, TestNoise)));
            }
        }
        UpdateOutputs();
        RefreshTexts();
        WeightsChanged?.Invoke(this, EventArgs.Empty);
    }
}
