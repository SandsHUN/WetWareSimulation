using WetWare.Core.Mathematics;

namespace WetWare.Core.Coding;

/// <summary>The neural coding schemes compared on the coding page.</summary>
public enum CodingScheme
{
    Rate,
    Latency,
    Phase,
    Population,
}

public readonly record struct CodedSpike(double TimeMs, CodingScheme Scheme, int Neuron);

/// <summary>
/// Generates spike trains that encode one scalar stimulus s ∈ [0, 1] in four different ways:
/// rate (Poisson frequency), latency (time-to-first-spike after each onset), phase (spike phase relative to
/// an 8 Hz theta rhythm) and population (Gaussian tuning curves, decoded with the population vector).
/// </summary>
public sealed class NeuralCodingSimulator
{
    public const int NeuronsPerCode = 8;
    public const int PopulationSize = 12;
    public const double ThetaHz = 8.0;
    public const double OnsetPeriodMs = 125.0;
    public const double MaxRateHz = 90.0;
    public const double TuningWidth = 0.12;

    private readonly RandomSource _random = new(5);
    private readonly double[] _phaseFired = new double[NeuronsPerCode];
    private readonly double[] _latencyTarget = new double[NeuronsPerCode];
    private readonly bool[] _latencyFired = new bool[NeuronsPerCode];
    private readonly Queue<(double Time, int Neuron)> _populationWindow = new();
    private double _lastOnset = double.NegativeInfinity;

    public double Stimulus { get; set; } = 0.5;

    /// <summary>When enabled the stimulus slowly sweeps between 0 and 1.</summary>
    public bool AutoSweep { get; set; }

    public double TimeMs { get; private set; }

    public double DtMs { get; set; } = 0.5;

    public List<CodedSpike> PendingSpikes { get; } = new();

    /// <summary>Preferred stimulus of each population neuron (evenly spaced over [0, 1]).</summary>
    public static double PreferredStimulus(int neuron) => neuron / (double)(PopulationSize - 1);

    public static double TuningCurve(int neuron, double stimulus)
    {
        double d = stimulus - PreferredStimulus(neuron);
        return MaxRateHz * Math.Exp(-d * d / (2 * TuningWidth * TuningWidth));
    }

    /// <summary>Theta-cycle phase in [0, 2π).</summary>
    public double ThetaPhase => 2 * Math.PI * ((TimeMs * ThetaHz / 1000.0) % 1.0);

    /// <summary>Stimulus value decoded from population spikes of the last 250 ms.</summary>
    public double DecodedStimulus { get; private set; } = double.NaN;

    /// <summary>Latency of the first spike after an onset for the current stimulus (ms).</summary>
    public double ExpectedLatency => 5 + (1 - Stimulus) * 70;

    /// <summary>Preferred firing phase for the current stimulus (radians); strong input fires early in the cycle.</summary>
    public double ExpectedPhase => (1 - Stimulus) * 1.7 * Math.PI + 0.15 * Math.PI;

    public void Reset()
    {
        TimeMs = 0;
        PendingSpikes.Clear();
        _populationWindow.Clear();
        _lastOnset = double.NegativeInfinity;
        Array.Fill(_phaseFired, double.NegativeInfinity);
        DecodedStimulus = double.NaN;
    }

    public void Advance(double durationMs)
    {
        int steps = (int)Math.Round(durationMs / DtMs);
        for (int i = 0; i < steps; i++) Step();
    }

    private void Step()
    {
        double dt = DtMs;
        if (AutoSweep) Stimulus = 0.5 + 0.48 * Math.Sin(2 * Math.PI * TimeMs / 6000.0);
        double s = Math.Clamp(Stimulus, 0, 1);

        // 1) Rate code: Poisson spikes whose frequency grows with the stimulus.
        double rate = 2 + s * MaxRateHz;
        for (int n = 0; n < NeuronsPerCode; n++)
            if (_random.Bernoulli(rate * dt / 1000.0)) Emit(CodingScheme.Rate, n);

        // 2) Latency code: one spike per stimulus onset, earlier for stronger stimuli.
        if (TimeMs - _lastOnset >= OnsetPeriodMs)
        {
            _lastOnset = TimeMs;
            for (int n = 0; n < NeuronsPerCode; n++)
            {
                _latencyFired[n] = false;
                _latencyTarget[n] = ExpectedLatency + 1.5 * _random.NextGaussian();
            }
        }
        for (int n = 0; n < NeuronsPerCode; n++)
        {
            if (!_latencyFired[n] && TimeMs - _lastOnset >= _latencyTarget[n])
            {
                _latencyFired[n] = true;
                Emit(CodingScheme.Latency, n);
            }
        }

        // 3) Phase code: one spike per theta cycle at a stimulus-dependent phase.
        double cycleMs = 1000.0 / ThetaHz;
        double phase = ThetaPhase;
        for (int n = 0; n < NeuronsPerCode; n++)
        {
            double target = ExpectedPhase + 0.08 * n - 0.28;
            double diff = phase - target;
            if (diff >= 0 && diff < 2 * Math.PI * dt / cycleMs && TimeMs - _phaseFired[n] > cycleMs * 0.5)
            {
                _phaseFired[n] = TimeMs;
                Emit(CodingScheme.Phase, n);
            }
        }

        // 4) Population code: Gaussian tuning curves.
        for (int n = 0; n < PopulationSize; n++)
        {
            if (_random.Bernoulli(TuningCurve(n, s) * dt / 1000.0))
            {
                Emit(CodingScheme.Population, n);
                _populationWindow.Enqueue((TimeMs, n));
            }
        }
        while (_populationWindow.Count > 0 && _populationWindow.Peek().Time < TimeMs - 250) _populationWindow.Dequeue();
        if (_populationWindow.Count > 0)
            DecodedStimulus = _populationWindow.Average(e => PreferredStimulus(e.Neuron));

        TimeMs += dt;
    }

    private void Emit(CodingScheme scheme, int neuron)
    {
        if (PendingSpikes.Count < 20000) PendingSpikes.Add(new CodedSpike(TimeMs, scheme, neuron));
    }

    public List<CodedSpike> TakeSpikes()
    {
        var copy = new List<CodedSpike>(PendingSpikes);
        PendingSpikes.Clear();
        return copy;
    }
}
