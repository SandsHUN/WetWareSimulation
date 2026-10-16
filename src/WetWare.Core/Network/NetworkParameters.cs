using WetWare.Core.Neurons;
using WetWare.Core.Plasticity;

namespace WetWare.Core.Network;

/// <summary>Configuration of the 100-neuron small-world population.</summary>
public sealed class NetworkParameters
{
    public int NeuronCount { get; set; } = 100;

    /// <summary>Every <c>InhibitoryEvery</c>-th neuron is inhibitory (5 → 20 % inhibitory, 80 % excitatory).</summary>
    public int InhibitoryEvery { get; set; } = 5;

    /// <summary>Out-degree of each neuron in the Watts–Strogatz ring (even number).</summary>
    public int Neighbours { get; set; } = 12;

    /// <summary>Probability of rewiring a ring edge to a random target (small-world shortcut).</summary>
    public double RewireProbability { get; set; } = 0.3;

    public int Seed { get; set; } = 42;

    public double DtMs { get; set; } = 0.1;

    /// <summary>Axonal + synaptic transmission delay (ms).</summary>
    public double DelayMs { get; set; } = 1.5;

    /// <summary>Decay time constant of the synaptic current (ms).</summary>
    public double TauSynapse { get; set; } = 5.0;

    /// <summary>Peak current of an excitatory synapse with w = 1 (pA).</summary>
    public double ExcitatoryCurrent { get; set; } = 300.0;

    /// <summary>Peak current of an inhibitory synapse with w = 1 (pA).</summary>
    public double InhibitoryCurrent { get; set; } = 550.0;

    /// <summary>Multiplier on all inhibitory synapses (0 = inhibition switched off).</summary>
    public double InhibitionGain { get; set; } = 1.0;

    /// <summary>Multiplier on all excitatory recurrent synapses.</summary>
    public double ExcitationGain { get; set; } = 1.0;

    /// <summary>Rate of the Poisson background input each neuron receives (Hz).</summary>
    public double NoiseRateHz { get; set; } = 500.0;

    /// <summary>Current kick per background event (pA).</summary>
    public double NoiseAmplitude { get; set; } = 60.0;

    public bool PlasticityEnabled { get; set; } = true;

    /// <summary>Fraction of synaptic resources used by one spike (Tsodyks–Markram short-term depression).</summary>
    public double DepressionUse { get; set; } = 0.5;

    /// <summary>Recovery time constant of synaptic resources (ms).</summary>
    public double DepressionRecoveryMs { get; set; } = 500.0;

    public double InitialWeightMin { get; set; } = 0.3;
    public double InitialWeightMax { get; set; } = 0.7;

    public LifParameters Lif { get; set; } = new();

    public StdpParameters Stdp { get; set; } = new();

    public bool IsInhibitory(int neuron) => neuron % InhibitoryEvery == InhibitoryEvery - 1;

    public NetworkParameters Clone()
    {
        var copy = (NetworkParameters)MemberwiseClone();
        copy.Lif = Lif.Clone();
        copy.Stdp = Stdp.Clone();
        return copy;
    }
}
