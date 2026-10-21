using WetWare.Core.Mathematics;

namespace WetWare.Core.Course.RateNetworks;

public enum RateNonlinearity
{
    /// <summary>F(x) = x – the linear network of lecture 6.3.1–6.3.6.</summary>
    Linear,

    /// <summary>F(x) = [x]⁺ – firing rates cannot be negative.</summary>
    Rectified,

    /// <summary>F(x) = r_max·tanh([x]⁺/r_max) – rectified and saturating, like a real neuron's f–I curve.</summary>
    Saturating,
}

/// <summary>One localized input bump on the ring.</summary>
public sealed record RingBump(double CenterDegrees, double Amplitude);

/// <summary>
/// Recurrent firing-rate network of N neurons with preferred directions on a ring (lecture 6.3):
/// τ dv/dt = −v + F(h + M v), with M_ij = (−J₀ + 2J₁ cos(θ_i − θ_j)) / N
/// ("excitation nearby, inhibition farther away"). M is symmetric: its cos/sin eigenvectors have eigenvalue J₁,
/// the uniform mode −J₀ and every other mode 0, so J₁ is the λ that controls amplification and memory.
/// </summary>
public sealed class RingNetwork
{
    public const int Size = 72;
    /// <summary>Sharpness κ of the input bumps, exp(κ(cos Δθ − 1)).</summary>
    public double BumpConcentration { get; set; } = 2.5;

    private readonly double[] _drive = new double[Size];
    private readonly double[] _noise = new double[Size];
    private readonly double[,] _m = new double[Size, Size];
    private double _builtJ0 = double.NaN, _builtJ1 = double.NaN;

    public RingNetwork()
    {
        for (int i = 0; i < Size; i++) Preferred[i] = -180 + 360.0 * i / Size;
        Peak = new RingBuffer<double>(600);
        ResampleNoise();
    }

    public double[] Preferred { get; } = new double[Size];
    public double[] Rates { get; } = new double[Size];
    public double[] Input { get; } = new double[Size];

    public double TauMs { get; set; } = 10;

    /// <summary>λ of the cosine modes.</summary>
    public double J1 { get; set; } = 0.9;

    /// <summary>Strength of uniform (global) inhibition.</summary>
    public double J0 { get; set; }

    public RateNonlinearity Nonlinearity { get; set; } = RateNonlinearity.Linear;
    public double MaxRate { get; set; } = 100;

    public List<RingBump> Bumps { get; } = [new(0, 10)];

    /// <summary>Constant input added to every neuron (gain-modulation experiment).</summary>
    public double Background { get; set; }

    /// <summary>Standard deviation of the frozen input noise.</summary>
    public double NoiseLevel { get; set; }

    public bool InputOn { get; set; } = true;

    public double TimeMs { get; private set; }

    /// <summary>History of the maximum rate (one sample per 5 ms).</summary>
    public RingBuffer<double> Peak { get; }

    private double _sampleClock;

    public void ResampleNoise(int seed = 3)
    {
        var random = new RandomSource(seed);
        for (int i = 0; i < Size; i++) _noise[i] = random.NextGaussian();
    }

    public void Reset()
    {
        Array.Clear(Rates);
        TimeMs = 0;
        _sampleClock = 0;
        Peak.Clear();
    }

    public double F(double x) => Nonlinearity switch
    {
        RateNonlinearity.Linear => x,
        RateNonlinearity.Rectified => Math.Max(0, x),
        _ => MaxRate * Math.Tanh(Math.Max(0, x) / MaxRate),
    };

    public double Weight(int i, int j) => (-J0 + 2 * J1 * Math.Cos((Preferred[i] - Preferred[j]) * Math.PI / 180)) / Size;

    /// <summary>The external input h currently applied (zero while the input is switched off).</summary>
    public void ComputeInput()
    {
        for (int i = 0; i < Size; i++)
        {
            double h = Background + NoiseLevel * _noise[i];
            foreach (var bump in Bumps)
            {
                double d = (Preferred[i] - bump.CenterDegrees) * Math.PI / 180;
                h += bump.Amplitude * Math.Exp(BumpConcentration * (Math.Cos(d) - 1));
            }
            Input[i] = h;
        }
    }

    public void Advance(double ms, double dtMs = 0.5)
    {
        EnsureMatrix();
        ComputeInput();
        int steps = Math.Max(1, (int)Math.Round(ms / dtMs));
        for (int s = 0; s < steps; s++)
        {
            for (int i = 0; i < Size; i++)
            {
                double recurrent = 0;
                for (int j = 0; j < Size; j++) recurrent += _m[i, j] * Rates[j];
                _drive[i] = F((InputOn ? Input[i] : 0) + recurrent);
            }
            double k = dtMs / TauMs;
            for (int i = 0; i < Size; i++)
            {
                Rates[i] += (_drive[i] - Rates[i]) * k;
                // Keep a runaway linear network finite so the plot can show the explosion without NaNs.
                if (Math.Abs(Rates[i]) > 1e6) Rates[i] = Math.Sign(Rates[i]) * 1e6;
            }
            TimeMs += dtMs;
            _sampleClock += dtMs;
            if (_sampleClock >= 5)
            {
                _sampleClock -= 5;
                Peak.Add(Rates.Max());
            }
        }
    }

    /// <summary>Decoded direction (population vector) in degrees, NaN when silent.</summary>
    public double PopulationVector
    {
        get
        {
            double x = 0, y = 0;
            for (int i = 0; i < Size; i++)
            {
                double r = Math.Max(0, Rates[i]);
                x += r * Math.Cos(Preferred[i] * Math.PI / 180);
                y += r * Math.Sin(Preferred[i] * Math.PI / 180);
            }
            return Math.Abs(x) + Math.Abs(y) < 1e-6 ? double.NaN : Math.Atan2(y, x) * 180 / Math.PI;
        }
    }

    /// <summary>Linear steady-state gain of the cosine modes, 1/(1 − λ) (∞ for λ ≥ 1).</summary>
    public double CosineGain => J1 < 1 ? 1 / (1 - J1) : double.PositiveInfinity;

    private void EnsureMatrix()
    {
        if (_builtJ0 == J0 && _builtJ1 == J1) return;
        for (int i = 0; i < Size; i++)
            for (int j = 0; j < Size; j++)
                _m[i, j] = Weight(i, j);
        _builtJ0 = J0;
        _builtJ1 = J1;
    }
}
