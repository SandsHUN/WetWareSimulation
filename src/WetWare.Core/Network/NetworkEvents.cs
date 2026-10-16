namespace WetWare.Core.Network;

/// <summary>A single action potential.</summary>
public readonly record struct SpikeEvent(double TimeMs, int Neuron);

/// <summary>
/// A noteworthy STDP update: the synapse <see cref="Pre"/>→<see cref="Post"/> changed by
/// <see cref="DeltaW"/> because the two cells fired <see cref="DeltaT"/> ms apart (t_post − t_pre).
/// </summary>
public readonly record struct StdpEvent(double TimeMs, int Pre, int Post, double DeltaT, double DeltaW, double NewWeight);

/// <summary>Why the selected neuron fired – used by the "What is happening now?" narrator.</summary>
public readonly record struct FiringExplanation(
    double TimeMs,
    int Neuron,
    double SynapticCurrent,
    double ExternalCurrent,
    double NoiseCurrent,
    int RecentInputs);
