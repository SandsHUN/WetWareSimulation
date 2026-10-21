using WetWare.Core.Mathematics;

namespace WetWare.Core.Course.Bci;

/// <summary>
/// Artificial recurrent connection cortex → computer → muscle (lecture 8.4.4; Moritz, Perlmutter &amp; Fetz 2008).
/// Two cortical units, one tuned to flexion and one to extension, fire Poisson spikes whose rate follows the
/// movement intention. The spinal pathway is "lesioned": the muscles only move when the BCI converts each unit's
/// smoothed rate into functional electrical stimulation (FES) of its muscle. Wrist torque = flexor − extensor force.
/// </summary>
public sealed class FesBridge
{
    public const double DtMs = 1;

    private readonly RandomSource _random = new(37);
    private double _rateF, _rateE;
    private double _forceF, _forceE;
    private double _sampleClock;

    public FesBridge()
    {
        Angle = new RingBuffer<double>(1200);
        Target = new RingBuffer<double>(1200);
        StimF = new RingBuffer<double>(1200);
        StimE = new RingBuffer<double>(1200);
    }

    /// <summary>Movement intention −1 (extend) … +1 (flex).</summary>
    public double Intent { get; set; }

    /// <summary>When true the intention follows a slow sinusoidal target the "subject" tries to track.</summary>
    public bool AutoTarget { get; set; } = true;

    public bool BciEnabled { get; set; } = true;

    /// <summary>Stimulation gain (mA per Hz above threshold).</summary>
    public double Gain { get; set; } = 0.12;

    /// <summary>Firing-rate threshold below which no stimulation is delivered (Hz).</summary>
    public double ThresholdHz { get; set; } = 15;

    public double BaseRateHz { get; set; } = 10;
    public double TuningHz { get; set; } = 50;

    public double TimeMs { get; private set; }
    public double WristAngle { get; private set; }
    public double TargetAngle { get; private set; }

    public double CurrentF { get; private set; }
    public double CurrentE { get; private set; }

    public List<double> SpikesF { get; } = new();
    public List<double> SpikesE { get; } = new();

    public RingBuffer<double> Angle { get; }
    public RingBuffer<double> Target { get; }
    public RingBuffer<double> StimF { get; }
    public RingBuffer<double> StimE { get; }

    /// <summary>Smoothed (decoded) rates of the two units in Hz.</summary>
    public (double Flexor, double Extensor) DecodedRates => (_rateF, _rateE);

    public void Reset()
    {
        TimeMs = 0;
        WristAngle = 0;
        _rateF = _rateE = 0;
        _forceF = _forceE = 0;
        SpikesF.Clear();
        SpikesE.Clear();
        Angle.Clear();
        Target.Clear();
        StimF.Clear();
        StimE.Clear();
    }

    public void Advance(double ms)
    {
        int steps = (int)Math.Round(ms / DtMs);
        for (int s = 0; s < steps; s++) Step();
        SpikesF.RemoveAll(t => t < TimeMs - 3000);
        SpikesE.RemoveAll(t => t < TimeMs - 3000);
    }

    private void Step()
    {
        if (AutoTarget)
        {
            TargetAngle = 40 * Math.Sin(2 * Math.PI * TimeMs / 4000.0);
            // The subject's intention: mostly the target itself plus a gentle correction of the remaining error.
            Intent = Math.Clamp(TargetAngle / 45.0 + (TargetAngle - WristAngle) / 90.0, -1, 1);
        }
        else
        {
            TargetAngle = Intent * 40;
        }

        double rateF = BaseRateHz + TuningHz * Math.Max(0, Intent);
        double rateE = BaseRateHz + TuningHz * Math.Max(0, -Intent);
        bool spikeF = _random.Bernoulli(rateF * DtMs / 1000), spikeE = _random.Bernoulli(rateE * DtMs / 1000);
        if (spikeF) SpikesF.Add(TimeMs);
        if (spikeE) SpikesE.Add(TimeMs);

        // Rate decoding: exponential filter (τ = 100 ms) of the spike trains, in Hz.
        double k = DtMs / 100.0;
        _rateF += (-_rateF * k) + (spikeF ? 1000.0 / 100.0 : 0);
        _rateE += (-_rateE * k) + (spikeE ? 1000.0 / 100.0 : 0);

        CurrentF = BciEnabled ? Gain * Math.Max(0, _rateF - ThresholdHz) : 0;
        CurrentE = BciEnabled ? Gain * Math.Max(0, _rateE - ThresholdHz) : 0;

        // Muscle force follows stimulation with a 60 ms twitch time constant.
        _forceF += (CurrentF - _forceF) * DtMs / 60.0;
        _forceE += (CurrentE - _forceE) * DtMs / 60.0;

        // Wrist: heavily damped – the angle relaxes (τ = 150 ms) to the equilibrium set by the net muscle force.
        double equilibrium = Math.Clamp(8.5 * (_forceF - _forceE), -70, 70);
        WristAngle += (equilibrium - WristAngle) * DtMs / 150.0;

        TimeMs += DtMs;
        _sampleClock += DtMs;
        if (_sampleClock >= 5)
        {
            _sampleClock -= 5;
            Angle.Add(WristAngle);
            Target.Add(TargetAngle);
            StimF.Add(CurrentF);
            StimE.Add(CurrentE);
        }
    }

    /// <summary>Root-mean-square tracking error over the recorded window (degrees).</summary>
    public double TrackingError
    {
        get
        {
            if (Angle.Count == 0) return 0;
            double sum = 0;
            for (int i = 0; i < Angle.Count; i++) sum += (Angle[i] - Target[i]) * (Angle[i] - Target[i]);
            return Math.Sqrt(sum / Angle.Count);
        }
    }
}
