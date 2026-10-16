namespace WetWare.Core.Neurons;

/// <summary>
/// Full record of one explicit-Euler integration step of a LIF neuron.
/// The "Bio-Debugger" shows these values line by line so students can follow the arithmetic.
/// </summary>
public sealed record IntegrationStep
{
    public double TimeMs { get; init; }
    public double DtMs { get; init; }
    public double VOld { get; init; }

    /// <summary>−(v − E_L)/τm (mV/ms).</summary>
    public double LeakRate { get; init; }

    public double SynapticCurrent { get; init; }
    public double ExternalCurrent { get; init; }
    public double NoiseCurrent { get; init; }
    public double TotalCurrent => SynapticCurrent + ExternalCurrent + NoiseCurrent;

    /// <summary>Number of presynaptic spikes that arrived during this step.</summary>
    public int ArrivingSpikes { get; init; }

    /// <summary>R_m·I/τm (mV/ms).</summary>
    public double InputRate { get; init; }

    public double Derivative => LeakRate + InputRate;

    /// <summary>v_old + dt·dv/dt before the threshold check.</summary>
    public double VIntegrated { get; init; }

    public double Threshold { get; init; }
    public double ResetPotential { get; init; }
    public bool Fired { get; init; }
    public bool Refractory { get; init; }
    public double VNew { get; init; }
}
