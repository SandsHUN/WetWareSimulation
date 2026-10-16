namespace WetWare.Core.Neurons;

/// <summary>
/// Parameters of the Leaky Integrate-and-Fire membrane:
/// τm·dv/dt = −(v − E_L) + R_m·I.
/// Units: ms, mV, MΩ and pA (so R_m·I / 1000 is in mV).
/// </summary>
public sealed class LifParameters
{
    /// <summary>Membrane time constant τm = R_m·C_m (ms).</summary>
    public double TauM { get; set; } = 20.0;

    /// <summary>Resting (leak reversal) potential E_L (mV).</summary>
    public double RestingPotential { get; set; } = -70.0;

    /// <summary>Firing threshold V_th (mV).</summary>
    public double Threshold { get; set; } = -50.0;

    /// <summary>Reset potential after a spike V_reset (mV).</summary>
    public double ResetPotential { get; set; } = -65.0;

    /// <summary>Membrane resistance R_m (MΩ).</summary>
    public double Resistance { get; set; } = 100.0;

    /// <summary>Absolute refractory period τref (ms).</summary>
    public double RefractoryPeriod { get; set; } = 2.0;

    /// <summary>Voltage drive produced by an input current: R_m·I (mV).</summary>
    public double Drive(double currentPa) => Resistance * currentPa / 1000.0;

    /// <summary>Leak term −(v − E_L)/τm (mV/ms).</summary>
    public double LeakRate(double v) => -(v - RestingPotential) / TauM;

    /// <summary>Input term R_m·I/τm (mV/ms).</summary>
    public double InputRate(double currentPa) => Drive(currentPa) / TauM;

    /// <summary>Right-hand side of the LIF equation, dv/dt (mV/ms).</summary>
    public double Derivative(double v, double currentPa) => LeakRate(v) + InputRate(currentPa);

    /// <summary>Stable fixed point of the sub-threshold dynamics, v* = E_L + R_m·I (mV).</summary>
    public double FixedPoint(double currentPa) => RestingPotential + Drive(currentPa);

    /// <summary>Minimum constant current that eventually makes the neuron fire (pA).</summary>
    public double Rheobase => (Threshold - RestingPotential) * 1000.0 / Resistance;

    /// <summary>Membrane capacitance C_m = τm / R_m (pF).</summary>
    public double Capacitance => TauM / Resistance * 1000.0;

    /// <summary>
    /// Analytical steady-state firing rate for a constant current (Hz):
    /// f = 1 / (τref + τm·ln((RI − (V_reset − E_L)) / (RI − (V_th − E_L)))).
    /// </summary>
    public double FiringRate(double currentPa)
    {
        double drive = Drive(currentPa);
        double toThreshold = Threshold - RestingPotential;
        if (drive <= toThreshold) return 0.0;

        double fromReset = ResetPotential - RestingPotential;
        double ratio = (drive - fromReset) / (drive - toThreshold);
        if (ratio <= 1.0) return 1000.0 / Math.Max(RefractoryPeriod, 1e-6);

        double periodMs = RefractoryPeriod + TauM * Math.Log(ratio);
        return 1000.0 / periodMs;
    }

    public LifParameters Clone() => (LifParameters)MemberwiseClone();
}
