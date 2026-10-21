using System.Numerics;
using WetWare.Core.Mathematics;

namespace WetWare.Core.Course.RateNetworks;

/// <summary>
/// Two interacting populations, excitatory v_E and inhibitory v_I (Dayan &amp; Abbott §7.5, lecture 6.3.11):
/// τ_E dv_E/dt = −v_E + [M_EE v_E + M_EI v_I − γ_E]⁺,  τ_I dv_I/dt = −v_I + [M_IE v_E + M_II v_I − γ_I]⁺.
/// The connection matrix is not symmetric, so the eigenvalues of the Jacobian at the fixed point can be complex:
/// the real part decides damping vs growth, the imaginary part the oscillation frequency. With the default
/// parameters a Hopf bifurcation occurs at τ_I = 40 ms – below it the activity spirals into the fixed point
/// (damped oscillation), above it settles onto a limit cycle.
/// </summary>
public sealed class ExcitatoryInhibitoryNetwork
{
    public const double SampleMs = 1;

    private double _sampleClock;

    public ExcitatoryInhibitoryNetwork()
    {
        Trajectory = new RingBuffer<(double E, double I)>(1600);
        TraceE = new RingBuffer<double>(1000);
        TraceI = new RingBuffer<double>(1000);
        Reset();
    }

    public double MEE { get; set; } = 1.25;
    public double MEI { get; set; } = -1;
    public double MIE { get; set; } = 1;
    public double MII { get; set; }
    public double GammaE { get; set; } = -10;
    public double GammaI { get; set; } = 10;
    public double TauE { get; set; } = 10;
    public double TauI { get; set; } = 30;

    public double E { get; private set; }
    public double I { get; private set; }
    public double TimeMs { get; private set; }

    public RingBuffer<(double E, double I)> Trajectory { get; }
    public RingBuffer<double> TraceE { get; }
    public RingBuffer<double> TraceI { get; }

    public void Reset() => SetState(FixedPoint.E + 12, FixedPoint.I - 6);

    /// <summary>Kicks the network to a new state (click in the phase plane).</summary>
    public void SetState(double e, double i)
    {
        E = Math.Max(0, e);
        I = Math.Max(0, i);
        TimeMs = 0;
        Trajectory.Clear();
        TraceE.Clear();
        TraceI.Clear();
        Trajectory.Add((E, I));
    }

    public void Advance(double ms, double dtMs = 0.1)
    {
        int steps = Math.Max(1, (int)Math.Round(ms / dtMs));
        for (int s = 0; s < steps; s++)
        {
            double de = (-E + Math.Max(0, MEE * E + MEI * I - GammaE)) / TauE;
            double di = (-I + Math.Max(0, MIE * E + MII * I - GammaI)) / TauI;
            E = Math.Clamp(E + de * dtMs, 0, 1e4);
            I = Math.Clamp(I + di * dtMs, 0, 1e4);
            TimeMs += dtMs;
            _sampleClock += dtMs;
            if (_sampleClock >= SampleMs)
            {
                _sampleClock -= SampleMs;
                Trajectory.Add((E, I));
                TraceE.Add(E);
                TraceI.Add(I);
            }
        }
    }

    /// <summary>Fixed point assuming both populations are in their linear (above-threshold) regime.</summary>
    public (double E, double I) FixedPoint
    {
        get
        {
            // (M − 1) v = γ  →  solve the 2×2 system.
            double a = MEE - 1, b = MEI, c = MIE, d = MII - 1;
            double det = a * d - b * c;
            if (Math.Abs(det) < 1e-12) return (0, 0);
            double e = (GammaE * d - b * GammaI) / det;
            double i = (a * GammaI - c * GammaE) / det;
            return (e, i);
        }
    }

    /// <summary>Eigenvalues (1/s) of the Jacobian at the fixed point.</summary>
    public (Complex L1, Complex L2) Eigenvalues
    {
        get
        {
            double a = (MEE - 1) / TauE, b = MEI / TauE, c = MIE / TauI, d = (MII - 1) / TauI;
            double trace = a + d, det = a * d - b * c;
            var root = Complex.Sqrt(trace * trace / 4 - det);
            // Convert from 1/ms to 1/s so the numbers are readable.
            return ((trace / 2 + root) * 1000, (trace / 2 - root) * 1000);
        }
    }

    /// <summary>Oscillation frequency predicted by the imaginary part (Hz), 0 for real eigenvalues.</summary>
    public double PredictedFrequency => Math.Abs(Eigenvalues.L1.Imaginary) / (2 * Math.PI);

    /// <summary>τ_I at which the real part crosses zero (Hopf bifurcation).</summary>
    public double CriticalTauI => (MEE - 1) / TauE is > 0 and var a ? (1 - MII) / a : double.PositiveInfinity;

    public bool IsStable => Eigenvalues.L1.Real < 0;

    /// <summary>dv_E/dt = 0 curve: v_I as a function of v_E (linear branch).</summary>
    public double ENullcline(double e) => ((1 - MEE) * e + GammaE) / MEI;

    /// <summary>dv_I/dt = 0 curve: v_I as a function of v_E.</summary>
    public double INullcline(double e) => MII == 1 ? 0 : Math.Max(0, (MIE * e - GammaI) / (1 - MII));
}
