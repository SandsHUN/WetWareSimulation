namespace WetWare.Core.Course.Synapses;

/// <summary>Time course of the open-channel fraction P_s after one presynaptic spike.</summary>
public enum SynapseKernel
{
    /// <summary>Instant rise, K(t) = exp(−t/τ_s) – e.g. AMPA or GABA_A.</summary>
    Exponential,

    /// <summary>Smooth rise and fall, α(t) = (t/τ)·exp(1 − t/τ), peak 1 at t = τ – slower synapses such as NMDA.</summary>
    Alpha,
}

/// <summary>
/// Linear filter that turns a presynaptic spike train into the open fraction P_s(t).
/// The alpha kernel is realised as two cascaded first-order filters, so its impulse response is exactly α(t).
/// </summary>
public sealed class SynapticFilter
{
    private double _z;

    public SynapseKernel Kernel { get; set; } = SynapseKernel.Exponential;

    /// <summary>Decay (exponential) or peak time (alpha) in ms.</summary>
    public double TauMs { get; set; } = 5;

    /// <summary>Current open fraction (sum of the kernels of all past spikes).</summary>
    public double P { get; private set; }

    public void Reset()
    {
        P = 0;
        _z = 0;
    }

    public void Spike()
    {
        if (Kernel == SynapseKernel.Exponential) P += 1;
        else _z += Math.E / TauMs;
    }

    public void Step(double dtMs)
    {
        double decay = Math.Exp(-dtMs / TauMs);
        if (Kernel == SynapseKernel.Exponential)
        {
            P *= decay;
            return;
        }

        // dz/dt = −z/τ, dP/dt = −P/τ + z (exact update of the cascade over one step).
        P = (P + _z * dtMs) * decay;
        _z *= decay;
    }

    /// <summary>Analytical single-spike response, for drawing the kernel shape.</summary>
    public static double Response(SynapseKernel kernel, double tauMs, double tMs)
    {
        if (tMs < 0) return 0;
        return kernel == SynapseKernel.Exponential
            ? Math.Exp(-tMs / tauMs)
            : tMs / tauMs * Math.Exp(1 - tMs / tauMs);
    }
}
