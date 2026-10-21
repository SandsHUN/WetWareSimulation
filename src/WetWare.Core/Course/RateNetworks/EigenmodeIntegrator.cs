using WetWare.Core.Mathematics;

namespace WetWare.Core.Course.RateNetworks;

/// <summary>
/// One eigenmode of a linear recurrent network (lecture 6.3.3): τ dc/dt = −(1 − λ)c + h·e.
/// λ &lt; 1 leaks back to zero with time constant τ/(1 − λ); λ = 1 integrates its input and holds it
/// (the oculomotor neural integrator); λ &gt; 1 runs away.
/// </summary>
public sealed class EigenmodeIntegrator
{
    public const double SampleMs = 2;

    private double _pulseLeft;
    private double _pulseValue;
    private double _sampleClock;

    public EigenmodeIntegrator()
    {
        Trace = new RingBuffer<double>(1500);
        InputTrace = new RingBuffer<double>(1500);
    }

    public double Lambda { get; set; } = 0.9;
    public double TauMs { get; set; } = 20;

    /// <summary>Sustained input h·e (in addition to pulses).</summary>
    public double Sustained { get; set; }

    public double C { get; private set; }

    public double TimeMs { get; private set; }

    public RingBuffer<double> Trace { get; }
    public RingBuffer<double> InputTrace { get; }

    /// <summary>Effective time constant τ/(1 − λ); infinite for a perfect integrator.</summary>
    public double EffectiveTau => Lambda < 1 ? TauMs / (1 - Lambda) : double.PositiveInfinity;

    public double Gain => Lambda < 1 ? 1 / (1 - Lambda) : double.PositiveInfinity;

    public void Reset()
    {
        C = 0;
        TimeMs = 0;
        _pulseLeft = 0;
        Trace.Clear();
        InputTrace.Clear();
    }

    /// <summary>A brief burst of input (a "saccade command"): area = <paramref name="value"/> × 50 ms.</summary>
    public void Pulse(double value)
    {
        _pulseValue = value;
        _pulseLeft = 50;
    }

    public void Advance(double ms, double dtMs = 0.5)
    {
        int steps = Math.Max(1, (int)Math.Round(ms / dtMs));
        for (int s = 0; s < steps; s++)
        {
            double h = Sustained + (_pulseLeft > 0 ? _pulseValue : 0);
            _pulseLeft -= dtMs;
            C += (-(1 - Lambda) * C + h) / TauMs * dtMs;
            C = Math.Clamp(C, -1e4, 1e4);
            TimeMs += dtMs;
            _sampleClock += dtMs;
            if (_sampleClock >= SampleMs)
            {
                _sampleClock -= SampleMs;
                Trace.Add(C);
                InputTrace.Add(h);
            }
        }
    }

    /// <summary>Closed-form response to a constant input switched on at t = 0 from c(0) = 0.</summary>
    public static double StepResponse(double lambda, double tauMs, double h, double tMs)
    {
        double a = 1 - lambda;
        if (Math.Abs(a) < 1e-9) return h * tMs / tauMs;
        return h / a * (1 - Math.Exp(-tMs * a / tauMs));
    }
}
