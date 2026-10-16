using WetWare.Core.Network;

namespace WetWare.Core.Scenarios;

/// <summary>Spontaneous activity of the balanced network, no scripted protocol.</summary>
public sealed class FreeScenario : Scenario
{
    public override string Id => "Free";
    public override string Glyph => "";
}

/// <summary>
/// Classical (Pavlovian) conditioning through Hebbian STDP: group A is repeatedly followed by group B,
/// so A→B synapses potentiate until A alone triggers B.
/// </summary>
public sealed class PavlovScenario : Scenario
{
    public const double BaselineEndMs = 3000;
    public const double TrainingEndMs = 21000;
    private const double PeriodMs = 400;
    private const double PulseMs = 6;
    private const double PairingLagMs = 8;
    private const double PulseCurrent = 900;

    private int[] _a = [];
    private int[] _b = [];
    private NeuronGroup[] _groups = [];

    public override string Id => "Pavlov";
    public override string Glyph => "";
    public override IReadOnlyList<NeuronGroup> Groups => _groups;

    public IReadOnlyList<int> GroupA => _a;
    public IReadOnlyList<int> GroupB => _b;

    public override void Configure(NetworkParameters p)
    {
        p.NoiseRateHz = 300;
        p.Stdp.LearningRate = 6.0;
        p.Stdp.AMinus = 0.0105;
    }

    public override void Setup(SpikingNetwork network)
    {
        var p = network.Parameters;
        _a = ExcitatoryRange(p, 0, 8);
        _b = ExcitatoryRange(p, 50, 8);
        _groups =
        [
            new NeuronGroup("A", _a, "#FBBF24"),
            new NeuronGroup("B", _b, "#A78BFA"),
        ];

        // Weak, plastic all-to-all projection A → B: the association that has to be learned.
        foreach (int pre in _a)
            foreach (int post in _b)
                network.SetSynapse(pre, post, 0.05);
        // No shortcut back B → A, so the learned direction is unambiguous.
        network.RemoveSynapses((pre, post) => _b.Contains(pre) && _a.Contains(post));
    }

    public override void Update(SpikingNetwork network)
    {
        double t = network.TimeMs;
        double phase = t % PeriodMs;
        bool aOn = phase < PulseMs;
        bool training = t >= BaselineEndMs && t < TrainingEndMs;
        bool bOn = training && phase >= PairingLagMs && phase < PairingLagMs + PulseMs;

        foreach (int i in _a) network.SetScenarioCurrent(i, aOn ? PulseCurrent : 0);
        foreach (int i in _b) network.SetScenarioCurrent(i, bOn ? PulseCurrent : 0);

        if (t < BaselineEndMs)
        {
            PhaseKey = "Baseline";
            Progress = t / TrainingEndMs;
        }
        else if (training)
        {
            PhaseKey = "Training";
            Progress = t / TrainingEndMs;
        }
        else
        {
            PhaseKey = "Test";
            Progress = 1.0;
        }
    }
}

/// <summary>Loss of inhibition turns asynchronous activity into synchronous, seizure-like bursting.</summary>
public sealed class SeizureScenario : Scenario
{
    public const double SeizureStartMs = 4000;
    public const double SeizureEndMs = 12000;

    public override string Id => "Seizure";
    public override string Glyph => "";

    public override void Configure(NetworkParameters p)
    {
        p.PlasticityEnabled = false;
    }

    public override void Update(SpikingNetwork network)
    {
        double t = network.TimeMs;
        var p = network.Parameters;
        if (t < SeizureStartMs)
        {
            PhaseKey = "Normal";
            p.InhibitionGain = 1.0;
            p.ExcitationGain = 1.0;
        }
        else if (t < SeizureEndMs)
        {
            PhaseKey = "Seizure";
            p.InhibitionGain = 0.0;
            p.ExcitationGain = 4.0;
        }
        else
        {
            PhaseKey = "Recovery";
            p.InhibitionGain = 1.0;
            p.ExcitationGain = 1.0;
        }
        Progress = Math.Min(1.0, t / (SeizureEndMs + 4000));
    }
}

/// <summary>
/// Too weak depression (A₋ ≪ A₊) drives every synapse to w_max; re-balancing the rule restores a spread distribution.
/// </summary>
public sealed class HomeostasisScenario : Scenario
{
    public const double ImbalanceEndMs = 20000;

    public override string Id => "Homeostasis";
    public override string Glyph => "";

    public override void Configure(NetworkParameters p)
    {
        p.Stdp.LearningRate = 4.0;
        p.Stdp.AMinus = p.Stdp.APlus * 0.4;
        p.ExcitatoryCurrent = 200;
    }

    public override void Update(SpikingNetwork network)
    {
        double t = network.TimeMs;
        var stdp = network.Parameters.Stdp;
        if (t < ImbalanceEndMs)
        {
            PhaseKey = "Imbalance";
            stdp.AMinus = stdp.APlus * 0.4;
        }
        else
        {
            PhaseKey = "Rebalanced";
            stdp.AMinus = stdp.APlus * 1.35;
        }
        Progress = Math.Min(1.0, t / (ImbalanceEndMs * 2));
    }
}

/// <summary>
/// Rate coding (constant currents of increasing strength) versus phase coding
/// (the same bias on top of an 8 Hz theta rhythm, so stronger input fires earlier in the cycle).
/// </summary>
public sealed class CodingScenario : Scenario
{
    public const double ThetaHz = 8.0;

    private int[] _rate = [];
    private int[] _phase = [];
    private NeuronGroup[] _groups = [];

    public override string Id => "Coding";
    public override string Glyph => "";
    public override IReadOnlyList<NeuronGroup> Groups => _groups;

    public override void Configure(NetworkParameters p)
    {
        p.PlasticityEnabled = false;
        p.NoiseRateHz = 150;
        p.ExcitationGain = 0.15;
    }

    public override void Setup(SpikingNetwork network)
    {
        var p = network.Parameters;
        _rate = ExcitatoryRange(p, 0, 10);
        _phase = ExcitatoryRange(p, 50, 10);
        _groups =
        [
            new NeuronGroup("Rate", _rate, "#34D399"),
            new NeuronGroup("Phase", _phase, "#F472B6"),
        ];
    }

    public override void Update(SpikingNetwork network)
    {
        double t = network.TimeMs;
        for (int k = 0; k < _rate.Length; k++)
            network.SetScenarioCurrent(_rate[k], 160 + k * 35);

        double theta = Math.Sin(2 * Math.PI * ThetaHz * t / 1000.0);
        for (int k = 0; k < _phase.Length; k++)
            network.SetScenarioCurrent(_phase[k], 60 + k * 12 + 160 * theta);
        PhaseKey = "Running";
        Progress = null;
    }
}

public static class ScenarioCatalog
{
    public static IReadOnlyList<Func<Scenario>> All { get; } =
    [
        () => new FreeScenario(),
        () => new PavlovScenario(),
        () => new SeizureScenario(),
        () => new HomeostasisScenario(),
        () => new CodingScenario(),
    ];

    public static Scenario Create(string id) =>
        All.Select(f => f()).FirstOrDefault(s => s.Id == id) ?? new FreeScenario();
}
