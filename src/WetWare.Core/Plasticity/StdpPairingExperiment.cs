using WetWare.Core.Mathematics;

namespace WetWare.Core.Plasticity;

/// <summary>One pre/post pairing applied to the synapse.</summary>
public readonly record struct PairingResult(int Index, double DeltaT, double DeltaW, double Weight);

/// <summary>
/// The classic STDP pairing protocol from slice electrophysiology (Bi &amp; Poo, 1998):
/// a presynaptic and a postsynaptic spike are repeatedly evoked with a fixed time difference Δt
/// and the synaptic weight is tracked after every pairing.
/// </summary>
public sealed class StdpPairingExperiment
{
    private readonly RandomSource _random = new(11);

    public StdpParameters Parameters { get; } = new() { APlus = 0.02, AMinus = 0.021 };

    /// <summary>Δt = t_post − t_pre used for each pairing (ms).</summary>
    public double DeltaT { get; set; } = 10.0;

    /// <summary>When true every pairing uses a random Δt ∈ [−50, 50] ms (uncorrelated firing).</summary>
    public bool RandomTiming { get; set; }

    /// <summary>Standard deviation of biological timing jitter (ms).</summary>
    public double JitterMs { get; set; } = 1.0;

    public double InitialWeight { get; set; } = 0.5;

    public double Weight { get; private set; }

    public int PairingCount { get; private set; }

    public List<PairingResult> History { get; } = new();

    public StdpPairingExperiment() => Reset();

    public void Reset()
    {
        Weight = InitialWeight;
        PairingCount = 0;
        History.Clear();
    }

    public PairingResult Pair()
    {
        double dt = RandomTiming ? _random.Uniform(-50, 50) : DeltaT + JitterMs * _random.NextGaussian();
        if (Math.Abs(dt) < 1e-6) dt = 1e-6;
        double old = Weight;
        Weight = Math.Clamp(Weight + Parameters.Window(dt), 0, Parameters.WMax);
        PairingCount++;
        var result = new PairingResult(PairingCount, dt, Weight - old, Weight);
        History.Add(result);
        if (History.Count > 2000) History.RemoveAt(0);
        return result;
    }
}
