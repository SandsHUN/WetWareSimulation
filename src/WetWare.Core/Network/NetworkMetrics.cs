using WetWare.Core.Mathematics;

namespace WetWare.Core.Network;

/// <summary>
/// Running population statistics sampled once per simulated millisecond:
/// mean firing rate (1 s window), Golomb–Rinzel synchrony index χ and the population rate trace.
/// </summary>
public sealed class NetworkMetrics
{
    private const double SyncTauMs = 400.0;
    private const int PopulationBinMs = 5;

    private readonly RingBuffer<int> _spikesPerMs = new(1000);
    private readonly double[] _meanV;
    private readonly double[] _meanV2;
    private double _meanBar;
    private double _meanBar2;
    private int _currentMsSpikes;
    private int _binSpikes;
    private int _binMs;
    private int _windowTotal;
    private long _samples;

    public NetworkMetrics(SpikingNetwork network)
    {
        _meanV = new double[network.Count];
        _meanV2 = new double[network.Count];
        PopulationRate = new RingBuffer<double>(400);
    }

    /// <summary>Population firing rate in 5 ms bins (Hz per neuron); 400 bins = last 2 s.</summary>
    public RingBuffer<double> PopulationRate { get; }

    /// <summary>Average firing rate of living neurons over the last second (Hz).</summary>
    public double MeanRateHz { get; private set; }

    /// <summary>Synchrony index χ ∈ [0, 1]: ≈0.1 = asynchronous, 1 = perfectly synchronous firing.</summary>
    public double SynchronyIndex { get; private set; }

    internal void RegisterSpike() => _currentMsSpikes++;

    internal void SampleMillisecond(double[] activity, bool[] ablated)
    {
        // Firing rate over a sliding one-second window.
        if (_spikesPerMs.Count == _spikesPerMs.Capacity) _windowTotal -= _spikesPerMs[0];
        _spikesPerMs.Add(_currentMsSpikes);
        _windowTotal += _currentMsSpikes;

        int alive = 0;
        for (int i = 0; i < ablated.Length; i++) if (!ablated[i]) alive++;
        double windowSeconds = _spikesPerMs.Count / 1000.0;
        MeanRateHz = alive == 0 || windowSeconds <= 0 ? 0 : _windowTotal / (alive * windowSeconds);

        _binSpikes += _currentMsSpikes;
        _binMs++;
        if (_binMs >= PopulationBinMs)
        {
            PopulationRate.Add(alive == 0 ? 0 : _binSpikes / (alive * PopulationBinMs / 1000.0));
            _binSpikes = 0;
            _binMs = 0;
        }
        _currentMsSpikes = 0;

        // Golomb–Rinzel χ² = Var(population mean) / mean(Var(individual)), computed on spike trains
        // low-pass filtered with τ = 8 ms and estimated with exponential moving averages.
        // Independent firing gives χ ≈ 1/√N (≈ 0.1); perfectly synchronous firing gives χ = 1.
        double alpha = _samples < 50 ? 1.0 / (_samples + 1) : 1.0 / SyncTauMs;
        _samples++;
        if (alive == 0) return;

        double bar = 0;
        double meanVar = 0;
        for (int i = 0; i < activity.Length; i++)
        {
            if (ablated[i]) continue;
            double x = activity[i];
            bar += x;
            _meanV[i] += alpha * (x - _meanV[i]);
            _meanV2[i] += alpha * (x * x - _meanV2[i]);
            meanVar += Math.Max(0, _meanV2[i] - _meanV[i] * _meanV[i]);
        }
        bar /= alive;
        meanVar /= alive;
        _meanBar += alpha * (bar - _meanBar);
        _meanBar2 += alpha * (bar * bar - _meanBar2);
        double varBar = Math.Max(0, _meanBar2 - _meanBar * _meanBar);
        SynchronyIndex = meanVar <= 1e-12 ? 0 : Math.Clamp(Math.Sqrt(varBar / meanVar), 0, 1);
    }
}
