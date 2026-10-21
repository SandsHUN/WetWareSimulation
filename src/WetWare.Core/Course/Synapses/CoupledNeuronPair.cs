using WetWare.Core.Mathematics;

namespace WetWare.Core.Course.Synapses;

/// <summary>
/// Two integrate-and-fire neurons driving each other through alpha synapses (Dayan &amp; Abbott fig. 5.20, lecture 6.1.5).
/// With excitatory synapses (E_s = 0 mV) the pair settles into alternating, anti-phase firing; with inhibitory
/// synapses (E_s = −80 mV) it synchronises.
/// </summary>
public sealed class CoupledNeuronPair
{
    public const double DtMs = 0.1;
    public const int TraceLength = 5000; // 500 ms

    private readonly double[] _v = new double[2];
    private readonly SynapticFilter[] _synapses = [new(), new()]; // _synapses[i]: synapse onto neuron i

    public CoupledNeuronPair()
    {
        TraceA = new RingBuffer<double>(TraceLength);
        TraceB = new RingBuffer<double>(TraceLength);
        Reset();
    }

    public double TauMembraneMs { get; set; } = 20;
    public double RestingPotential { get; set; } = -70;
    public double Threshold { get; set; } = -54;
    public double ResetPotential { get; set; } = -80;
    public double DriveMv { get; set; } = 25;

    /// <summary>r_m·g_max of both synapses.</summary>
    public double Strength { get; set; } = 0.1;

    public double SynapseTauMs { get; set; } = 10;

    public double ReversalPotential { get; set; }

    public bool Excitatory
    {
        get => ReversalPotential > Threshold;
        set => ReversalPotential = value ? 0 : -80;
    }

    public double TimeMs { get; private set; }

    public RingBuffer<double> TraceA { get; }
    public RingBuffer<double> TraceB { get; }
    public List<double> SpikesA { get; } = new();
    public List<double> SpikesB { get; } = new();

    public void Reset()
    {
        TimeMs = 0;
        // Start out of phase so the coupling, not the initial condition, decides the final pattern.
        _v[0] = -70;
        _v[1] = -58;
        foreach (var s in _synapses)
        {
            s.Kernel = SynapseKernel.Alpha;
            s.Reset();
        }
        TraceA.Clear();
        TraceB.Clear();
        SpikesA.Clear();
        SpikesB.Clear();
    }

    public void Advance(double ms)
    {
        int steps = (int)Math.Round(ms / DtMs);
        for (int i = 0; i < steps; i++) Step();
        double cutoff = TimeMs - 4000;
        SpikesA.RemoveAll(t => t < cutoff);
        SpikesB.RemoveAll(t => t < cutoff);
    }

    private void Step()
    {
        Span<bool> fired = stackalloc bool[2];
        for (int i = 0; i < 2; i++)
        {
            var syn = _synapses[i];
            syn.TauMs = SynapseTauMs;
            syn.Step(DtMs);
            double synaptic = Strength * syn.P * (_v[i] - ReversalPotential);
            _v[i] += (-(_v[i] - RestingPotential) - synaptic + DriveMv) / TauMembraneMs * DtMs;
            if (_v[i] >= Threshold)
            {
                _v[i] = ResetPotential;
                fired[i] = true;
            }
        }

        TimeMs += DtMs;
        if (fired[0])
        {
            _synapses[1].Spike();
            SpikesA.Add(TimeMs);
        }
        if (fired[1])
        {
            _synapses[0].Spike();
            SpikesB.Add(TimeMs);
        }
        TraceA.Add(fired[0] ? 20 : _v[0]);
        TraceB.Add(fired[1] ? 20 : _v[1]);
    }

    /// <summary>
    /// Relative phase of B's spikes within A's cycle over the last second: 0 = synchronous, 0.5 = anti-phase.
    /// NaN until both neurons have fired enough.
    /// </summary>
    public double RelativePhase
    {
        get
        {
            var a = SpikesA.Where(t => t > TimeMs - 1000).ToList();
            if (a.Count < 3 || SpikesB.Count == 0) return double.NaN;
            var phases = new List<double>();
            for (int k = 1; k < a.Count; k++)
            {
                double start = a[k - 1], period = a[k] - start;
                foreach (double tb in SpikesB)
                {
                    if (tb >= start && tb < a[k]) phases.Add((tb - start) / period);
                }
            }
            if (phases.Count == 0) return double.NaN;
            // Circular mean, folded to [0, 0.5] (0.9 of a cycle is as close to synchrony as 0.1).
            double sx = phases.Sum(p => Math.Cos(2 * Math.PI * p)), sy = phases.Sum(p => Math.Sin(2 * Math.PI * p));
            double mean = Math.Atan2(sy, sx) / (2 * Math.PI);
            if (mean < 0) mean += 1;
            return mean > 0.5 ? 1 - mean : mean;
        }
    }
}
