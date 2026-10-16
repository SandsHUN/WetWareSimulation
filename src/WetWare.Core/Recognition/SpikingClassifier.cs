using WetWare.Core.Mathematics;

namespace WetWare.Core.Recognition;

/// <summary>Outcome of presenting one pattern to the classifier.</summary>
public sealed record ClassificationResult(int Predicted, int[] SpikeCounts, double[] ClassScores, int WinnerNeuron, int? TrueLabel)
{
    public bool IsCorrect => TrueLabel is int label && label == Predicted;
}

/// <summary>
/// Unsupervised spiking pattern recogniser in the spirit of Diehl &amp; Cook (2015):
/// Poisson-encoded pixels → excitatory LIF output layer with STDP, adaptive thresholds (homeostasis)
/// and winner-take-all lateral inhibition. Output neurons learn prototype "receptive fields";
/// labels are assigned afterwards from which digit each neuron responds to most.
/// </summary>
public sealed class SpikingClassifier
{
    public const double DtMs = 0.5;

    private readonly RandomSource _random;
    private double[] _pixels = new double[DigitPatterns.PixelCount];
    private bool _learning;
    private double _rateBoost;
    private readonly double[] _preTrace;
    private readonly double[] _refractory;
    private readonly double[,] _responses;

    public SpikingClassifier(int outputCount = 20, int seed = 1)
    {
        InputCount = DigitPatterns.PixelCount;
        OutputCount = outputCount;
        _random = new RandomSource(seed);
        Weights = new double[outputCount, InputCount];
        Theta = new double[outputCount];
        Voltage = new double[outputCount];
        SpikeCounts = new int[outputCount];
        Labels = Enumerable.Repeat(-1, outputCount).ToArray();
        InputSpikes = new bool[InputCount];
        OutputSpikes = new bool[outputCount];
        _preTrace = new double[InputCount];
        _refractory = new double[outputCount];
        _responses = new double[outputCount, DigitPatterns.ClassCount];
        Reset();
    }

    public int InputCount { get; }
    public int OutputCount { get; }

    /// <summary>Synaptic weights [output, input] in [0, 1].</summary>
    public double[,] Weights { get; }

    /// <summary>Adaptive threshold increment of each output neuron (homeostasis).</summary>
    public double[] Theta { get; }

    public double[] Voltage { get; }

    public int[] SpikeCounts { get; }

    /// <summary>Digit assigned to each output neuron (−1 = not yet assigned).</summary>
    public int[] Labels { get; }

    public bool[] InputSpikes { get; }

    public bool[] OutputSpikes { get; }

    public double PresentationMs { get; set; } = 150.0;
    public double RateOnHz { get; set; } = 60.0;
    public double RateOffHz { get; set; } = 1.0;
    public double TauMs { get; set; } = 20.0;
    public double BaseThreshold { get; set; } = 7.0;
    public double ThetaPlus { get; set; } = 0.12;
    public double ThetaDecay { get; set; } = 0.998;
    public double Inhibition { get; set; } = 8.0;
    public double LearningRate { get; set; } = 0.04;
    public double TraceTarget { get; set; } = 0.35;
    public double WeightSum { get; set; } = 10.0;

    /// <summary>A presentation with fewer output spikes is repeated with a stronger (higher-rate) stimulus.</summary>
    public int MinimumOutputSpikes { get; set; } = 6;

    /// <summary>Input-rate increment applied on each repeated presentation (Hz).</summary>
    public double BoostStepHz { get; set; } = 30.0;

    public const int MaxAttempts = 5;

    public double TimeMs { get; private set; }

    public bool IsPresenting { get; private set; }

    public int TrainedSamples { get; private set; }

    public void Reset()
    {
        for (int j = 0; j < OutputCount; j++)
        {
            for (int i = 0; i < InputCount; i++) Weights[j, i] = _random.Uniform(0.05, 0.35);
            Theta[j] = 0;
            Labels[j] = -1;
            for (int c = 0; c < DigitPatterns.ClassCount; c++) _responses[j, c] = 0;
        }
        NormalizeAll();
        TrainedSamples = 0;
        IsPresenting = false;
    }

    public double ThresholdOf(int neuron) => BaseThreshold + Theta[neuron];

    public void BeginPresentation(double[] pixels, bool learn, double rateBoost = 0)
    {
        _pixels = pixels;
        _learning = learn;
        _rateBoost = rateBoost;
        TimeMs = 0;
        IsPresenting = true;
        Array.Clear(Voltage);
        Array.Clear(SpikeCounts);
        Array.Clear(_preTrace);
        Array.Clear(_refractory);
        Array.Clear(InputSpikes);
        Array.Clear(OutputSpikes);
    }

    /// <summary>Advances the presentation by one step; returns false once it has finished.</summary>
    public bool StepPresentation()
    {
        if (!IsPresenting) return false;
        double decay = Math.Exp(-DtMs / TauMs);
        double traceDecay = Math.Exp(-DtMs / 20.0);

        for (int i = 0; i < InputCount; i++)
        {
            double rate = (_pixels[i] > 0.5 ? RateOnHz + _rateBoost : RateOffHz);
            bool spike = _random.NextDouble() < rate * DtMs / 1000.0;
            InputSpikes[i] = spike;
            _preTrace[i] *= traceDecay;
            if (spike) _preTrace[i] = 1.0;
        }

        for (int j = 0; j < OutputCount; j++)
        {
            OutputSpikes[j] = false;
            Voltage[j] *= decay;
            if (_refractory[j] > 0)
            {
                _refractory[j] -= DtMs;
                continue;
            }
            double input = 0;
            for (int i = 0; i < InputCount; i++)
                if (InputSpikes[i]) input += Weights[j, i];
            Voltage[j] += input;
        }

        // Winner-take-all: at most the strongest suprathreshold neuron fires in a step.
        int winner = -1;
        double best = 0;
        for (int j = 0; j < OutputCount; j++)
        {
            double margin = Voltage[j] - ThresholdOf(j);
            if (margin >= 0 && (winner < 0 || margin > best))
            {
                winner = j;
                best = margin;
            }
        }

        if (winner >= 0)
        {
            OutputSpikes[winner] = true;
            SpikeCounts[winner]++;
            Voltage[winner] = 0;
            _refractory[winner] = 5.0;
            for (int k = 0; k < OutputCount; k++)
                if (k != winner) Voltage[k] = Math.Max(-Inhibition, Voltage[k] - Inhibition);

            if (_learning)
            {
                Theta[winner] += ThetaPlus;
                for (int i = 0; i < InputCount; i++)
                {
                    double w = Weights[winner, i] + LearningRate * (_preTrace[i] - TraceTarget);
                    Weights[winner, i] = Math.Clamp(w, 0, 1);
                }
            }
        }

        TimeMs += DtMs;
        if (TimeMs >= PresentationMs) IsPresenting = false;
        return IsPresenting;
    }

    /// <summary>Finishes a presentation: updates labels (when learning) and classifies.</summary>
    public ClassificationResult EndPresentation(int? trueLabel)
    {
        IsPresenting = false;
        if (_learning)
        {
            for (int j = 0; j < OutputCount; j++)
            {
                Theta[j] *= ThetaDecay;
                NormalizeRow(j);
            }
            if (trueLabel is int label)
            {
                for (int j = 0; j < OutputCount; j++)
                {
                    for (int c = 0; c < DigitPatterns.ClassCount; c++) _responses[j, c] *= 0.995;
                    _responses[j, label] += SpikeCounts[j];
                }
                AssignLabels();
            }
            TrainedSamples++;
        }
        return Classify(trueLabel);
    }

    /// <summary>Runs a complete presentation. During learning, silent trials are repeated with stronger input.</summary>
    public ClassificationResult Present(double[] pixels, bool learn, int? trueLabel = null)
    {
        double boost = 0;
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            BeginPresentation(pixels, learn, boost);
            while (StepPresentation()) { }
            if (SpikeCounts.Sum() >= MinimumOutputSpikes || attempt == MaxAttempts - 1) break;
            boost += BoostStepHz;
        }
        return EndPresentation(trueLabel);
    }

    /// <summary>Trains on one noisy sample of a random digit.</summary>
    public ClassificationResult TrainStep(double noise)
    {
        int digit = _random.Next(DigitPatterns.ClassCount);
        return Present(DigitPatterns.Noisy(digit, noise, _random), learn: true, trueLabel: digit);
    }

    /// <summary>Accuracy on <paramref name="samplesPerClass"/> noisy samples of each digit, without learning.</summary>
    public double Evaluate(int samplesPerClass, double noise)
    {
        int correct = 0, total = 0;
        for (int d = 0; d < DigitPatterns.ClassCount; d++)
        {
            for (int k = 0; k < samplesPerClass; k++)
            {
                var result = Present(DigitPatterns.Noisy(d, noise, _random), learn: false, trueLabel: d);
                if (result.IsCorrect) correct++;
                total++;
            }
        }
        return total == 0 ? 0 : (double)correct / total;
    }

    private ClassificationResult Classify(int? trueLabel)
    {
        int classes = DigitPatterns.ClassCount;
        var scores = new double[classes];
        var members = new int[classes];
        for (int j = 0; j < OutputCount; j++)
        {
            if (Labels[j] < 0) continue;
            scores[Labels[j]] += SpikeCounts[j];
            members[Labels[j]]++;
        }
        for (int c = 0; c < classes; c++) if (members[c] > 0) scores[c] /= members[c];

        int predicted = -1;
        double best = 0;
        for (int c = 0; c < classes; c++)
        {
            if (scores[c] > best)
            {
                best = scores[c];
                predicted = c;
            }
        }

        int winner = -1, max = 0;
        for (int j = 0; j < OutputCount; j++)
            if (SpikeCounts[j] > max) { max = SpikeCounts[j]; winner = j; }

        return new ClassificationResult(predicted, (int[])SpikeCounts.Clone(), scores, winner, trueLabel);
    }

    private void AssignLabels()
    {
        for (int j = 0; j < OutputCount; j++)
        {
            int best = -1;
            double bestValue = 0;
            for (int c = 0; c < DigitPatterns.ClassCount; c++)
            {
                if (_responses[j, c] > bestValue)
                {
                    bestValue = _responses[j, c];
                    best = c;
                }
            }
            Labels[j] = best;
        }
    }

    private void NormalizeAll()
    {
        for (int j = 0; j < OutputCount; j++) NormalizeRow(j);
    }

    private void NormalizeRow(int j)
    {
        double sum = 0;
        for (int i = 0; i < InputCount; i++) sum += Weights[j, i];
        if (sum <= 1e-9) return;
        double factor = WeightSum / sum;
        for (int i = 0; i < InputCount; i++) Weights[j, i] = Math.Min(1.0, Weights[j, i] * factor);
    }
}
