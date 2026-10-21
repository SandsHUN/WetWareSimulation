using WetWare.Core.Mathematics;

namespace WetWare.Core.Course.Synapses;

/// <summary>
/// A presynaptic spike train acting on an integrate-and-fire neuron through a conductance synapse (lecture 6.1):
/// τ_m dV/dt = −(V − E_L) − r_m g_s(t)(V − E_s) + R_m I_e, with g_s = g_max · P_s(t).
/// Whether the synapse excites or inhibits depends on where E_s lies relative to V, not on a sign.
/// </summary>
public sealed class ConductanceSynapseSimulator
{
    public const double DtMs = 0.1;
    public const int TraceLength = 6000; // 600 ms

    private readonly RandomSource _random = new(11);
    private double _nextRegularSpike;
    private double _refractoryLeft;

    public ConductanceSynapseSimulator()
    {
        Voltage = new RingBuffer<double>(TraceLength);
        Conductance = new RingBuffer<double>(TraceLength);
        Current = new RingBuffer<double>(TraceLength);
        Reset();
    }

    public SynapticFilter Filter { get; } = new();

    public double TauMembraneMs { get; set; } = 20;
    public double RestingPotential { get; set; } = -70;
    public double Threshold { get; set; } = -54;
    public double ResetPotential { get; set; } = -70;
    public double RefractoryMs { get; set; } = 2;

    /// <summary>Synaptic reversal potential E_s (0 mV ≈ AMPA, −80 mV ≈ GABA_A).</summary>
    public double ReversalPotential { get; set; }

    /// <summary>r_m·g_max: peak synaptic conductance relative to the leak conductance.</summary>
    public double Strength { get; set; } = 0.5;

    /// <summary>Constant background drive R_m·I_e (mV).</summary>
    public double BackgroundMv { get; set; } = 12;

    /// <summary>Presynaptic firing rate (Hz).</summary>
    public double PresynapticRate { get; set; } = 20;

    public bool PoissonInput { get; set; } = true;

    public double TimeMs { get; private set; }
    public double V { get; private set; }

    /// <summary>Instantaneous synaptic current I_s = g_s(V − E_s) in units of the leak (mV); negative = depolarising.</summary>
    public double SynapticCurrent { get; private set; }

    public RingBuffer<double> Voltage { get; }
    public RingBuffer<double> Conductance { get; }
    public RingBuffer<double> Current { get; }
    public List<double> PreSpikes { get; } = new();
    public List<double> PostSpikes { get; } = new();

    public void Reset()
    {
        TimeMs = 0;
        V = RestingPotential;
        _refractoryLeft = 0;
        _nextRegularSpike = 20;
        Filter.Reset();
        Voltage.Clear();
        Conductance.Clear();
        Current.Clear();
        PreSpikes.Clear();
        PostSpikes.Clear();
    }

    /// <summary>Delivers one presynaptic spike now (the "single spike" button).</summary>
    public void InjectSpike()
    {
        Filter.Spike();
        PreSpikes.Add(TimeMs);
    }

    public void Advance(double ms)
    {
        int steps = (int)Math.Round(ms / DtMs);
        for (int i = 0; i < steps; i++) Step();
        double cutoff = TimeMs - TraceLength * DtMs;
        PreSpikes.RemoveAll(t => t < cutoff);
        PostSpikes.RemoveAll(t => t < cutoff);
    }

    private void Step()
    {
        if (PresynapticRate > 0)
        {
            bool spike = PoissonInput
                ? _random.Bernoulli(PresynapticRate * DtMs / 1000.0)
                : TimeMs >= _nextRegularSpike;
            if (spike)
            {
                if (!PoissonInput) _nextRegularSpike += 1000.0 / PresynapticRate;
                InjectSpike();
            }
        }
        else
        {
            _nextRegularSpike = TimeMs + 20;
        }

        Filter.Step(DtMs);
        double g = Strength * Filter.P;
        SynapticCurrent = g * (V - ReversalPotential);

        if (_refractoryLeft > 0)
        {
            _refractoryLeft -= DtMs;
        }
        else
        {
            double dv = (-(V - RestingPotential) - SynapticCurrent + BackgroundMv) / TauMembraneMs;
            V += dv * DtMs;
            if (V >= Threshold)
            {
                V = ResetPotential;
                _refractoryLeft = RefractoryMs;
                PostSpikes.Add(TimeMs);
            }
        }

        TimeMs += DtMs;
        // Spikes are drawn as a short overshoot so the trace looks like a recording.
        Voltage.Add(PostSpikes.Count > 0 && TimeMs - PostSpikes[^1] < 0.6 ? 30 : V);
        Conductance.Add(g);
        Current.Add(-SynapticCurrent);
    }
}
