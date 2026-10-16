using WetWare.Core.Mathematics;

namespace WetWare.Core.Neurons;

/// <summary>Shapes of the electrode current injected into a single neuron.</summary>
public enum StimulusShape
{
    Constant,
    Pulses,
    Step,
    Ramp,
    Sine,
    Noisy,
}

/// <summary>Time-dependent current generator for the single-neuron laboratory.</summary>
public sealed class Stimulus
{
    private double _noiseState;

    public StimulusShape Shape { get; set; } = StimulusShape.Constant;

    /// <summary>Peak (or constant) amplitude in pA.</summary>
    public double Amplitude { get; set; } = 250.0;

    /// <summary>Frequency of the pulse train / sine wave (Hz).</summary>
    public double FrequencyHz { get; set; } = 10.0;

    /// <summary>Width of one pulse (ms).</summary>
    public double PulseWidthMs { get; set; } = 20.0;

    /// <summary>Standard deviation of the additive membrane noise (pA).</summary>
    public double NoiseSigma { get; set; }

    /// <summary>Period of the step / ramp cycle (ms).</summary>
    public double CycleMs { get; set; } = 400.0;

    /// <summary>Returns the deterministic part of the current at time <paramref name="tMs"/>.</summary>
    public double Deterministic(double tMs)
    {
        switch (Shape)
        {
            case StimulusShape.Constant:
                return Amplitude;
            case StimulusShape.Pulses:
            {
                double period = 1000.0 / Math.Max(FrequencyHz, 0.1);
                return tMs % period < PulseWidthMs ? Amplitude : 0.0;
            }
            case StimulusShape.Step:
                return tMs % CycleMs < CycleMs / 2 ? 0.0 : Amplitude;
            case StimulusShape.Ramp:
                return Amplitude * (tMs % CycleMs) / CycleMs;
            case StimulusShape.Sine:
                return Amplitude * 0.5 * (1.0 + Math.Sin(2.0 * Math.PI * FrequencyHz * tMs / 1000.0));
            case StimulusShape.Noisy:
                return Amplitude * 0.8;
            default:
                return 0.0;
        }
    }

    /// <summary>Current including the Ornstein–Uhlenbeck noise component (pA).</summary>
    public double Sample(double tMs, double dtMs, RandomSource random)
    {
        double sigma = NoiseSigma + (Shape == StimulusShape.Noisy ? Amplitude * 0.5 : 0.0);
        if (sigma > 0)
        {
            // OU process with 5 ms correlation time keeps the noise visible but smooth.
            const double tau = 5.0;
            _noiseState += -_noiseState * dtMs / tau + sigma * Math.Sqrt(2.0 * dtMs / tau) * random.NextGaussian();
        }
        else
        {
            _noiseState = 0;
        }
        return Deterministic(tMs) + _noiseState;
    }

    public void Reset() => _noiseState = 0;
}
