using WetWare.Core.Mathematics;

namespace WetWare.Core.Neurons;

/// <summary>
/// One LIF neuron driven by an electrode. Records voltage/current traces for the oscilloscope,
/// a phase-plane trail and the latest integration step for the Bio-Debugger.
/// </summary>
public sealed class SingleNeuronSimulator
{
    public const double DefaultDt = 0.1;

    private readonly RandomSource _random = new(7);
    private double _refractoryLeft;

    public SingleNeuronSimulator(int traceCapacity = 4000)
    {
        VoltageTrace = new RingBuffer<double>(traceCapacity);
        CurrentTrace = new RingBuffer<double>(traceCapacity);
        PhaseTrail = new RingBuffer<(double V, double DvDt)>(600);
        Reset();
    }

    public LifParameters Parameters { get; } = new();

    public Stimulus Stimulus { get; } = new();

    public double DtMs { get; set; } = DefaultDt;

    public double TimeMs { get; private set; }

    public double V { get; private set; }

    public double LastCurrent { get; private set; }

    public bool IsRefractory => _refractoryLeft > 0;

    public RingBuffer<double> VoltageTrace { get; }

    public RingBuffer<double> CurrentTrace { get; }

    public RingBuffer<(double V, double DvDt)> PhaseTrail { get; }

    /// <summary>Spike times (ms) of the last few seconds, used for the instantaneous rate.</summary>
    public List<double> SpikeTimes { get; } = new();

    public IntegrationStep? LastStep { get; private set; }

    public int TotalSpikes { get; private set; }

    public void Reset()
    {
        TimeMs = 0;
        V = Parameters.RestingPotential;
        _refractoryLeft = 0;
        TotalSpikes = 0;
        LastStep = null;
        SpikeTimes.Clear();
        VoltageTrace.Clear();
        CurrentTrace.Clear();
        PhaseTrail.Clear();
        Stimulus.Reset();
    }

    /// <summary>Advances the neuron by one time step and returns the detailed step record.</summary>
    public IntegrationStep Step()
    {
        var p = Parameters;
        double dt = DtMs;
        double current = Stimulus.Sample(TimeMs, dt, _random);
        double deterministic = Stimulus.Deterministic(TimeMs);
        double vOld = V;
        double leak = p.LeakRate(vOld);
        double input = p.InputRate(current);
        bool refractory = _refractoryLeft > 0;
        bool fired = false;
        double integrated;

        if (refractory)
        {
            _refractoryLeft -= dt;
            integrated = p.ResetPotential;
            V = p.ResetPotential;
        }
        else
        {
            integrated = vOld + dt * (leak + input);
            V = integrated;
            if (V >= p.Threshold)
            {
                fired = true;
                V = p.ResetPotential;
                _refractoryLeft = p.RefractoryPeriod;
                TotalSpikes++;
                SpikeTimes.Add(TimeMs + dt);
            }
        }

        TimeMs += dt;
        LastCurrent = current;

        // Draw the spike as a tall stroke so it looks like a real action potential on the scope.
        VoltageTrace.Add(fired ? 20.0 : V);
        CurrentTrace.Add(current);
        PhaseTrail.Add((V, p.Derivative(V, current)));

        while (SpikeTimes.Count > 0 && SpikeTimes[0] < TimeMs - 3000) SpikeTimes.RemoveAt(0);

        LastStep = new IntegrationStep
        {
            TimeMs = TimeMs,
            DtMs = dt,
            VOld = vOld,
            LeakRate = leak,
            ExternalCurrent = deterministic,
            NoiseCurrent = current - deterministic,
            InputRate = input,
            VIntegrated = integrated,
            Threshold = p.Threshold,
            ResetPotential = p.ResetPotential,
            Fired = fired,
            Refractory = refractory,
            VNew = V,
        };
        return LastStep;
    }

    /// <summary>Runs as many steps as fit into <paramref name="durationMs"/>.</summary>
    public void Advance(double durationMs)
    {
        int steps = (int)Math.Round(durationMs / DtMs);
        for (int i = 0; i < steps; i++) Step();
    }

    /// <summary>Firing rate measured over the last second (Hz).</summary>
    public double MeasuredRate
    {
        get
        {
            double window = Math.Min(1000.0, TimeMs);
            if (window <= 0) return 0;
            int count = SpikeTimes.Count(t => t >= TimeMs - window);
            return count * 1000.0 / window;
        }
    }
}
