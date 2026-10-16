using WetWare.Core.Mathematics;
using WetWare.Core.Neurons;

namespace WetWare.Core.Network;

/// <summary>
/// Population of LIF neurons connected by delayed, exponentially decaying current synapses with
/// trace-based additive STDP and Poisson background noise. Integrated with explicit Euler at dt = 0.1 ms.
/// </summary>
public sealed class SpikingNetwork
{
    private const int MaxPendingSpikes = 20000;
    private const int MaxPendingStdpEvents = 400;

    private RandomSource _random = null!;

    // Neuron state
    private double[] _v = [];
    private double[] _refractory = [];
    private double[] _iSyn = [];
    private double[] _iNoise = [];
    private double[] _iScenario = [];
    private double[] _traceSpike = [];
    private double[] _recentInput = [];
    private double[] _preTrace = [];
    private double[] _postTrace = [];
    private double[] _lastSpike = [];
    private double[] _resources = [];
    private double[] _syncTrace = [];
    private bool[] _ablated = [];
    private bool[] _inhibitory = [];

    // Delay line: ring of per-neuron incoming current.
    private double[,] _delayLine = new double[1, 1];
    private int[,] _delayCount = new int[1, 1];
    private int _delaySteps;
    private long _stepIndex;

    // Synapses (structure of arrays)
    private readonly List<int> _pre = new();
    private readonly List<int> _post = new();
    private readonly List<double> _weight = new();
    private readonly List<bool> _plastic = new();
    private int[][] _outgoing = [];
    private int[][] _incoming = [];

    private readonly List<SpikeEvent> _pendingSpikes = new();
    private readonly List<StdpEvent> _pendingStdp = new();
    private readonly List<FiringExplanation> _pendingExplanations = new();

    // Selected-neuron instrumentation
    private int _selected;
    private double _selVOld, _selLeak, _selInput, _selIntegrated, _selVNew, _selSyn, _selExt, _selNoise;
    private int _selArrivals;
    private bool _selFired, _selRefractory;

    private double _decaySyn, _decayPlus, _decayMinus, _decayRecent, _decaySync;
    private double _metricsAccumulator;

    public SpikingNetwork(NetworkParameters? parameters = null)
    {
        Parameters = parameters ?? new NetworkParameters();
        SelectedTrace = new RingBuffer<double>(4000);
        Rebuild();
    }

    public NetworkParameters Parameters { get; private set; }

    public int Count => _v.Length;

    public double TimeMs { get; private set; }

    public long TotalSpikes { get; private set; }

    public NetworkMetrics Metrics { get; private set; } = null!;

    /// <summary>Voltage trace of the selected neuron (one sample per step, spikes drawn as +20 mV peaks).</summary>
    public RingBuffer<double> SelectedTrace { get; }

    public IReadOnlyList<int> SynapsePre => _pre;
    public IReadOnlyList<int> SynapsePost => _post;
    public IReadOnlyList<double> SynapseWeight => _weight;
    public IReadOnlyList<bool> SynapsePlastic => _plastic;
    public int SynapseCount => _pre.Count;

    /// <summary>Neurons receiving the electrode current from the control panel.</summary>
    public HashSet<int> ElectrodeTargets { get; } = new();

    /// <summary>Electrode current (pA) applied to every neuron in <see cref="ElectrodeTargets"/>.</summary>
    public double ElectrodeCurrent { get; set; }

    public int SelectedNeuron
    {
        get => _selected;
        set
        {
            int clamped = Math.Clamp(value, 0, Count - 1);
            if (clamped == _selected) return;
            _selected = clamped;
            SelectedTrace.Clear();
        }
    }

    public double GetVoltage(int neuron) => _v[neuron];

    public double GetSynapticCurrent(int neuron) => _iSyn[neuron];

    public double GetExternalCurrent(int neuron) => ExternalCurrentOf(neuron);

    public double GetNoiseCurrent(int neuron) => _iNoise[neuron];

    public double GetLastSpike(int neuron) => _lastSpike[neuron];

    /// <summary>Recent-activity glow (decays with ~30 ms), drives the "calcium imaging" flashes.</summary>
    public double GetActivity(int neuron) => _traceSpike[neuron];

    public bool IsInhibitory(int neuron) => _inhibitory[neuron];

    /// <summary>Available synaptic resources (0–1) of a neuron's outgoing excitatory synapses.</summary>
    public double GetResources(int neuron) => _resources[neuron];

    public bool IsAblated(int neuron) => _ablated[neuron];

    public IReadOnlyList<int> Outgoing(int neuron) => _outgoing[neuron];

    public IReadOnlyList<int> Incoming(int neuron) => _incoming[neuron];

    /// <summary>Rebuilds the network from scratch with new parameters (new topology from the seed).</summary>
    public void Reset(NetworkParameters parameters)
    {
        Parameters = parameters;
        Rebuild();
    }

    /// <summary>Restores initial state and weights while keeping the current parameters.</summary>
    public void Rebuild()
    {
        var p = Parameters;
        int n = p.NeuronCount;
        _random = new RandomSource(p.Seed);

        _v = new double[n];
        _refractory = new double[n];
        _iSyn = new double[n];
        _iNoise = new double[n];
        _iScenario = new double[n];
        _traceSpike = new double[n];
        _recentInput = new double[n];
        _preTrace = new double[n];
        _postTrace = new double[n];
        _lastSpike = new double[n];
        _resources = new double[n];
        _syncTrace = new double[n];
        _ablated = new bool[n];
        _inhibitory = new bool[n];

        for (int i = 0; i < n; i++)
        {
            _inhibitory[i] = p.IsInhibitory(i);
            _v[i] = p.Lif.RestingPotential + _random.Uniform(0, p.Lif.Threshold - p.Lif.RestingPotential) * 0.8;
            _lastSpike[i] = double.NegativeInfinity;
            _resources[i] = 1.0;
        }

        _delaySteps = Math.Max(1, (int)Math.Round(p.DelayMs / p.DtMs));
        _delayLine = new double[_delaySteps + 1, n];
        _delayCount = new int[_delaySteps + 1, n];
        _stepIndex = 0;
        TimeMs = 0;
        TotalSpikes = 0;

        _decaySyn = Math.Exp(-p.DtMs / p.TauSynapse);
        _decayRecent = Math.Exp(-p.DtMs / 30.0);
        _decaySync = Math.Exp(-p.DtMs / 8.0);
        UpdateStdpDecay();

        BuildSmallWorld();
        Metrics = new NetworkMetrics(this);
        _metricsAccumulator = 0;
        _pendingSpikes.Clear();
        _pendingStdp.Clear();
        _pendingExplanations.Clear();
        SelectedTrace.Clear();
        ElectrodeTargets.Clear();
        ElectrodeCurrent = 0;
        _selected = Math.Clamp(_selected, 0, n - 1);
    }

    private void UpdateStdpDecay()
    {
        _decayPlus = Math.Exp(-Parameters.DtMs / Parameters.Stdp.TauPlus);
        _decayMinus = Math.Exp(-Parameters.DtMs / Parameters.Stdp.TauMinus);
    }

    /// <summary>Directed Watts–Strogatz graph: ring lattice with random shortcut rewiring.</summary>
    private void BuildSmallWorld()
    {
        var p = Parameters;
        int n = p.NeuronCount;
        _pre.Clear();
        _post.Clear();
        _weight.Clear();
        _plastic.Clear();

        var existing = new HashSet<(int, int)>();
        int half = Math.Max(1, p.Neighbours / 2);
        for (int i = 0; i < n; i++)
        {
            for (int d = 1; d <= half; d++)
            {
                foreach (int sign in new[] { 1, -1 })
                {
                    int target = ((i + sign * d) % n + n) % n;
                    if (_random.Bernoulli(p.RewireProbability))
                    {
                        for (int attempt = 0; attempt < 20; attempt++)
                        {
                            int candidate = _random.Next(n);
                            if (candidate != i && !existing.Contains((i, candidate)))
                            {
                                target = candidate;
                                break;
                            }
                        }
                    }
                    if (target == i || !existing.Add((i, target))) continue;

                    bool inhibitory = _inhibitory[i];
                    double w = inhibitory ? 1.0 : _random.Uniform(p.InitialWeightMin, p.InitialWeightMax);
                    _pre.Add(i);
                    _post.Add(target);
                    _weight.Add(w);
                    _plastic.Add(!inhibitory);
                }
            }
        }
        RebuildIndex();
    }

    private void RebuildIndex()
    {
        int n = Count;
        var outLists = Enumerable.Range(0, n).Select(_ => new List<int>()).ToArray();
        var inLists = Enumerable.Range(0, n).Select(_ => new List<int>()).ToArray();
        for (int s = 0; s < _pre.Count; s++)
        {
            outLists[_pre[s]].Add(s);
            inLists[_post[s]].Add(s);
        }
        _outgoing = outLists.Select(l => l.ToArray()).ToArray();
        _incoming = inLists.Select(l => l.ToArray()).ToArray();
    }

    /// <summary>Adds (or overwrites) a synapse. Intended for scenario setup.</summary>
    public void SetSynapse(int pre, int post, double weight, bool plastic = true)
    {
        for (int s = 0; s < _pre.Count; s++)
        {
            if (_pre[s] == pre && _post[s] == post)
            {
                _weight[s] = weight;
                _plastic[s] = plastic && !_inhibitory[pre];
                return;
            }
        }
        _pre.Add(pre);
        _post.Add(post);
        _weight.Add(weight);
        _plastic.Add(plastic && !_inhibitory[pre]);
        RebuildIndex();
    }

    /// <summary>Removes every synapse from <paramref name="pre"/> to <paramref name="post"/> sets (scenario setup).</summary>
    public void RemoveSynapses(Func<int, int, bool> predicate)
    {
        for (int s = _pre.Count - 1; s >= 0; s--)
        {
            if (!predicate(_pre[s], _post[s])) continue;
            _pre.RemoveAt(s);
            _post.RemoveAt(s);
            _weight.RemoveAt(s);
            _plastic.RemoveAt(s);
        }
        RebuildIndex();
    }

    public double GetWeight(int pre, int post)
    {
        foreach (int s in _outgoing[pre])
            if (_post[s] == post) return _weight[s];
        return double.NaN;
    }

    /// <summary>Scenario-controlled external current for one neuron (pA).</summary>
    public void SetScenarioCurrent(int neuron, double currentPa) => _iScenario[neuron] = currentPa;

    public void ClearScenarioCurrents() => Array.Clear(_iScenario);

    /// <summary>"Laser ablation": permanently silences a neuron (until reset).</summary>
    public void SetAblated(int neuron, bool ablated)
    {
        _ablated[neuron] = ablated;
        if (ablated)
        {
            _v[neuron] = Parameters.Lif.RestingPotential;
            _traceSpike[neuron] = 0;
        }
    }

    public int AblatedCount => _ablated.Count(a => a);

    /// <summary>Applies a changed STDP time constant without rebuilding.</summary>
    public void RefreshDerivedConstants()
    {
        _decaySyn = Math.Exp(-Parameters.DtMs / Parameters.TauSynapse);
        UpdateStdpDecay();
    }

    private double ExternalCurrentOf(int i) =>
        _iScenario[i] + (ElectrodeTargets.Contains(i) ? ElectrodeCurrent : 0.0);

    /// <summary>Advances the whole population by one time step.</summary>
    public void Step()
    {
        var p = Parameters;
        var lif = p.Lif;
        var stdp = p.Stdp;
        double dt = p.DtMs;
        int n = Count;
        int ringSize = _delaySteps + 1;
        int slot = (int)(_stepIndex % ringSize);
        double noiseLambda = p.NoiseRateHz * dt / 1000.0;
        double tauM = lif.TauM;
        double el = lif.RestingPotential;
        double rOver1000 = lif.Resistance / 1000.0;

        for (int i = 0; i < n; i++)
        {
            double arrived = _delayLine[slot, i];
            int arrivals = _delayCount[slot, i];
            _delayLine[slot, i] = 0;
            _delayCount[slot, i] = 0;
            _iSyn[i] = _iSyn[i] * _decaySyn + arrived;
            if (arrived > 0) _recentInput[i] += arrivals;
            _recentInput[i] *= _decayRecent;

            int events = noiseLambda > 0 ? _random.Poisson(noiseLambda) : 0;
            _iNoise[i] = _iNoise[i] * _decaySyn + events * p.NoiseAmplitude;

            _traceSpike[i] *= _decayRecent;
            _syncTrace[i] *= _decaySync;
            _resources[i] += (1.0 - _resources[i]) * dt / p.DepressionRecoveryMs;
            _preTrace[i] *= _decayPlus;
            _postTrace[i] *= _decayMinus;

            bool isSelected = i == _selected;
            double vOld = _v[i];
            double ext = ExternalCurrentOf(i);
            double total = _iSyn[i] + _iNoise[i] + ext;
            double leak = -(vOld - el) / tauM;
            double input = rOver1000 * total / tauM;
            bool fired = false;
            bool refractory = false;
            double integrated;

            if (_ablated[i])
            {
                _v[i] = el;
                integrated = el;
            }
            else if (_refractory[i] > 0)
            {
                refractory = true;
                _refractory[i] -= dt;
                _v[i] = lif.ResetPotential;
                integrated = lif.ResetPotential;
            }
            else
            {
                integrated = vOld + dt * (leak + input);
                _v[i] = integrated;
                if (integrated >= lif.Threshold)
                {
                    fired = true;
                    Fire(i, slot, ringSize, stdp, isSelected, ext);
                }
            }

            if (isSelected)
            {
                _selVOld = vOld;
                _selLeak = leak;
                _selInput = input;
                _selIntegrated = integrated;
                _selVNew = _v[i];
                _selSyn = _iSyn[i];
                _selExt = ext;
                _selNoise = _iNoise[i];
                _selArrivals = arrivals;
                _selFired = fired;
                _selRefractory = refractory;
                SelectedTrace.Add(fired ? 20.0 : _v[i]);
            }
        }

        _stepIndex++;
        TimeMs += dt;

        _metricsAccumulator += dt;
        if (_metricsAccumulator >= 1.0)
        {
            _metricsAccumulator -= 1.0;
            Metrics.SampleMillisecond(_syncTrace, _ablated);
        }
    }

    private void Fire(int i, int slot, int ringSize, Plasticity.StdpParameters stdp, bool isSelected, double ext)
    {
        var p = Parameters;
        _v[i] = p.Lif.ResetPotential;
        _refractory[i] = p.Lif.RefractoryPeriod;
        _lastSpike[i] = TimeMs + p.DtMs;
        _traceSpike[i] = 1.0;
        _syncTrace[i] += 1.0;
        TotalSpikes++;
        Metrics?.RegisterSpike();

        if (_pendingSpikes.Count < MaxPendingSpikes) _pendingSpikes.Add(new SpikeEvent(TimeMs + p.DtMs, i));

        if (isSelected && _pendingExplanations.Count < 50)
        {
            _pendingExplanations.Add(new FiringExplanation(TimeMs, i, _iSyn[i], ext, _iNoise[i], (int)Math.Round(_recentInput[i])));
        }

        // Propagate through delayed synapses.
        int arrivalSlot = (slot + _delaySteps) % ringSize;
        double excScale = p.ExcitatoryCurrent * p.ExcitationGain;
        double inhScale = -p.InhibitoryCurrent * p.InhibitionGain;
        double scale = _inhibitory[i] ? inhScale : excScale;
        if (!_inhibitory[i])
        {
            // Short-term depression: each spike releases a fraction of the remaining vesicle pool.
            double released = p.DepressionUse * _resources[i];
            _resources[i] -= released;
            scale *= released / p.DepressionUse;
        }
        foreach (int s in _outgoing[i])
        {
            _delayLine[arrivalSlot, _post[s]] += _weight[s] * scale;
            _delayCount[arrivalSlot, _post[s]]++;
        }

        if (!p.PlasticityEnabled) return;

        double lr = stdp.LearningRate;
        double wMax = stdp.WMax;
        double now = TimeMs + p.DtMs;

        // Pre-synaptic spike: depress synapses whose post cell fired recently (Δt < 0 → LTD).
        StdpEvent? strongest = null;
        foreach (int s in _outgoing[i])
        {
            if (!_plastic[s]) continue;
            int post = _post[s];
            double trace = _postTrace[post];
            if (trace < 1e-4) continue;
            double old = _weight[s];
            double w = Math.Max(0.0, old - lr * stdp.AMinus * trace);
            _weight[s] = w;
            double dw = w - old;
            if (strongest is null || Math.Abs(dw) > Math.Abs(strongest.Value.DeltaW))
                strongest = new StdpEvent(now, i, post, _lastSpike[post] - now, dw, w);
        }
        _preTrace[i] += 1.0;

        // Post-synaptic spike: potentiate synapses whose pre cell fired recently (Δt > 0 → LTP).
        foreach (int s in _incoming[i])
        {
            if (!_plastic[s]) continue;
            int pre = _pre[s];
            double trace = _preTrace[pre];
            if (trace < 1e-4) continue;
            double old = _weight[s];
            double w = Math.Min(wMax, old + lr * stdp.APlus * trace);
            _weight[s] = w;
            double dw = w - old;
            if (strongest is null || Math.Abs(dw) > Math.Abs(strongest.Value.DeltaW))
                strongest = new StdpEvent(now, pre, i, now - _lastSpike[pre], dw, w);
        }
        _postTrace[i] += 1.0;

        if (strongest is { } ev && Math.Abs(ev.DeltaW) > 1e-5 && !double.IsInfinity(ev.DeltaT) && _pendingStdp.Count < MaxPendingStdpEvents)
        {
            _pendingStdp.Add(ev);
        }
    }

    /// <summary>Advances the simulation by <paramref name="durationMs"/> (rounded to whole steps).</summary>
    public void Advance(double durationMs, Action<SpikingNetwork>? perStep = null)
    {
        int steps = (int)Math.Round(durationMs / Parameters.DtMs);
        for (int k = 0; k < steps; k++)
        {
            perStep?.Invoke(this);
            Step();
        }
    }

    /// <summary>Returns and clears the spikes produced since the previous call (telemetry "PUB" channel).</summary>
    public List<SpikeEvent> TakeSpikes()
    {
        var copy = new List<SpikeEvent>(_pendingSpikes);
        _pendingSpikes.Clear();
        return copy;
    }

    public List<StdpEvent> TakeStdpEvents()
    {
        var copy = new List<StdpEvent>(_pendingStdp);
        _pendingStdp.Clear();
        return copy;
    }

    public List<FiringExplanation> TakeExplanations()
    {
        var copy = new List<FiringExplanation>(_pendingExplanations);
        _pendingExplanations.Clear();
        return copy;
    }

    /// <summary>The last integration step of the selected neuron (for the Bio-Debugger).</summary>
    public IntegrationStep LastSelectedStep => new()
    {
        TimeMs = TimeMs,
        DtMs = Parameters.DtMs,
        VOld = _selVOld,
        LeakRate = _selLeak,
        SynapticCurrent = _selSyn,
        ExternalCurrent = _selExt,
        NoiseCurrent = _selNoise,
        ArrivingSpikes = _selArrivals,
        InputRate = _selInput,
        VIntegrated = _selIntegrated,
        Threshold = Parameters.Lif.Threshold,
        ResetPotential = Parameters.Lif.ResetPotential,
        Fired = _selFired,
        Refractory = _selRefractory,
        VNew = _selVNew,
    };

    /// <summary>Dense pre×post weight matrix; NaN where no synapse exists. Inhibitory weights are negative.</summary>
    public void FillWeightMatrix(double[,] matrix)
    {
        int n = Count;
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                matrix[i, j] = double.NaN;
        for (int s = 0; s < _pre.Count; s++)
        {
            double w = _weight[s];
            matrix[_pre[s], _post[s]] = _inhibitory[_pre[s]] ? -w : w;
        }
    }

    /// <summary>Histogram of plastic (excitatory) weights between 0 and w_max.</summary>
    public int[] WeightHistogram(int bins)
    {
        var counts = new int[bins];
        double wMax = Parameters.Stdp.WMax;
        for (int s = 0; s < _pre.Count; s++)
        {
            if (!_plastic[s]) continue;
            int b = (int)(_weight[s] / wMax * bins);
            counts[Math.Clamp(b, 0, bins - 1)]++;
        }
        return counts;
    }

    /// <summary>Fraction of plastic synapses stronger than half of w_max.</summary>
    public double StrongSynapseFraction()
    {
        int plastic = 0, strong = 0;
        double half = Parameters.Stdp.WMax * 0.5;
        for (int s = 0; s < _pre.Count; s++)
        {
            if (!_plastic[s]) continue;
            plastic++;
            if (_weight[s] > half) strong++;
        }
        return plastic == 0 ? 0 : (double)strong / plastic;
    }

    public double MeanPlasticWeight()
    {
        double sum = 0;
        int count = 0;
        for (int s = 0; s < _pre.Count; s++)
        {
            if (!_plastic[s]) continue;
            sum += _weight[s];
            count++;
        }
        return count == 0 ? 0 : sum / count;
    }

    /// <summary>Mean weight of all synapses from group <paramref name="from"/> to group <paramref name="to"/>.</summary>
    public double MeanWeightBetween(IReadOnlyCollection<int> from, IReadOnlyCollection<int> to)
    {
        var toSet = to as ISet<int> ?? new HashSet<int>(to);
        double sum = 0;
        int count = 0;
        foreach (int pre in from)
        {
            foreach (int s in _outgoing[pre])
            {
                if (!toSet.Contains(_post[s])) continue;
                sum += _weight[s];
                count++;
            }
        }
        return count == 0 ? 0 : sum / count;
    }
}
