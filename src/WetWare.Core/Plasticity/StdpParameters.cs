namespace WetWare.Core.Plasticity;

/// <summary>
/// Pair-based additive STDP window:
/// Δw = A₊·exp(−Δt/τ₊) for Δt &gt; 0 (LTP), Δw = −A₋·exp(Δt/τ₋) for Δt &lt; 0 (LTD), with Δt = t_post − t_pre.
/// </summary>
public sealed class StdpParameters
{
    public double APlus { get; set; } = 0.010;
    public double AMinus { get; set; } = 0.0105;
    public double TauPlus { get; set; } = 20.0;
    public double TauMinus { get; set; } = 20.0;
    public double WMax { get; set; } = 1.0;

    /// <summary>Global learning-rate multiplier exposed on the control panel.</summary>
    public double LearningRate { get; set; } = 1.0;

    /// <summary>Weight change for a single pre/post pair separated by <paramref name="deltaTMs"/>.</summary>
    public double Window(double deltaTMs)
    {
        if (deltaTMs > 0) return LearningRate * APlus * Math.Exp(-deltaTMs / TauPlus);
        if (deltaTMs < 0) return -LearningRate * AMinus * Math.Exp(deltaTMs / TauMinus);
        return 0.0;
    }

    /// <summary>
    /// Integral of the window (A₊τ₊ − A₋τ₋). Negative means uncorrelated firing depresses
    /// synapses on average, which keeps the network stable.
    /// </summary>
    public double NetBalance => APlus * TauPlus - AMinus * TauMinus;

    public StdpParameters Clone() => (StdpParameters)MemberwiseClone();
}
