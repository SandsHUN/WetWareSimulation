using WetWare.Core.Mathematics;

namespace WetWare.Core.Course.Bci;

/// <summary>
/// Activity-dependent stimulation with an implanted Neurochip (lecture 8.4.6–8.4.7; Jackson, Mavoori &amp; Fetz 2006).
/// Every spike of a recorded cortical neuron triggers a stimulus at a second site after a programmable delay.
/// Pre-before-post pairings strengthen the corticospinal connection with an STDP-like window
/// Δw ∝ exp(−delay/τ): short delays work best, long delays hardly at all, and randomly timed stimulation
/// (no causal order) does not strengthen the connection. The strength is read out as post-spike facilitation (PSF):
/// the bump in the spike-triggered average of the muscle's EMG.
/// </summary>
public sealed class SpikeTriggeredConditioning
{
    public const int StaLength = 60; // ms shown in the spike-triggered average
    public const double PsfLatencyMs = 9;

    private RandomSource _random = new(43);

    public SpikeTriggeredConditioning() => Reset();

    /// <summary>Spike → stimulus delay (ms).</summary>
    public double DelayMs { get; set; } = 20;

    /// <summary>Randomly timed stimulation instead of spike-triggered (the control condition).</summary>
    public bool RandomControl { get; set; }

    public double FiringRateHz { get; set; } = 12;
    public double TauMs { get; set; } = 20;
    public double PlasticityRate { get; set; } = 0.0008;
    public double MaxStrength { get; set; } = 1;

    /// <summary>Connection strength before conditioning (for the before/after comparison).</summary>
    public double InitialStrength { get; private set; }

    public double Strength { get; private set; }

    public double HoursConditioned { get; private set; }
    public long Pairings { get; private set; }

    /// <summary>Strength sampled every simulated 15 minutes.</summary>
    public List<(double Hours, double Strength)> History { get; } = new();

    /// <summary>Last measured effect for each delay tried (the summary curve of the lecture's figure).</summary>
    public SortedDictionary<double, double> EffectByDelay { get; } = new();

    public void Reset(int seed = 43)
    {
        _random = new RandomSource(seed);
        InitialStrength = 0.3;
        Strength = InitialStrength;
        HoursConditioned = 0;
        Pairings = 0;
        History.Clear();
        History.Add((0, Strength));
    }

    /// <summary>Plasticity per pairing at a given spike → stimulus delay (the conditioning window).</summary>
    public double Window(double delayMs) => delayMs < 0 ? -Math.Exp(delayMs / TauMs) : Math.Exp(-delayMs / TauMs);

    /// <summary>Simulates <paramref name="hours"/> of free behaviour with the Neurochip running.</summary>
    public void Condition(double hours)
    {
        double total = HoursConditioned + hours;
        while (HoursConditioned < total - 1e-9)
        {
            double chunk = Math.Min(0.25, total - HoursConditioned);
            long pairs = (long)(FiringRateHz * 3600 * chunk);
            double perPair;
            if (RandomControl)
            {
                // Random timing: the effective delay is spread over ±250 ms – LTP and LTD cancel on average.
                double sum = 0;
                for (int k = 0; k < 200; k++) sum += Window(_random.Uniform(-250, 250));
                perPair = sum / 200;
            }
            else
            {
                perPair = Window(DelayMs + _random.NextGaussian() * 1.5);
            }
            for (int k = 0; k < 4; k++)
            {
                double dw = PlasticityRate * perPair * (pairs / 4.0) / 1000.0;
                Strength += dw >= 0 ? dw * (MaxStrength - Strength) : dw * Strength;
            }
            Strength = Math.Clamp(Strength, 0.02, MaxStrength);
            Pairings += pairs;
            HoursConditioned += chunk;
            History.Add((HoursConditioned, Strength));
        }
        if (!RandomControl) EffectByDelay[Math.Round(DelayMs)] = Strength / InitialStrength;
    }

    /// <summary>
    /// Spike-triggered average of rectified EMG (arbitrary units) from <paramref name="sweeps"/> noisy sweeps:
    /// a baseline plus a facilitation bump whose height is the connection strength.
    /// </summary>
    public double[] SpikeTriggeredAverage(double strength, int sweeps = 300, int seed = 5)
    {
        var random = new RandomSource(seed);
        var sta = new double[StaLength];
        for (int s = 0; s < sweeps; s++)
        {
            for (int t = 0; t < StaLength; t++)
            {
                double bump = t >= PsfLatencyMs ? (t - PsfLatencyMs) / 3.0 * Math.Exp(1 - (t - PsfLatencyMs) / 3.0) : 0;
                sta[t] += 1 + strength * 1.2 * bump + 0.9 * random.NextGaussian();
            }
        }
        for (int t = 0; t < StaLength; t++) sta[t] /= sweeps;
        return sta;
    }
}
