using WetWare.Core.Network;

namespace WetWare.Core.Scenarios;

/// <summary>Neuron group highlighted by a scenario (e.g. "A" = conditioned stimulus).</summary>
public sealed record NeuronGroup(string Key, IReadOnlyList<int> Neurons, string ColorHex);

/// <summary>
/// Pre-configured teaching experiment. <see cref="Configure"/> prepares the parameters,
/// <see cref="Setup"/> modifies the freshly built network, <see cref="Update"/> runs before every time step.
/// Text (title, description, phases) lives in the localization tables under <c>Scenario.{Id}.*</c>.
/// </summary>
public abstract class Scenario
{
    public abstract string Id { get; }

    /// <summary>Segoe Fluent Icons glyph used on the scenario card.</summary>
    public abstract string Glyph { get; }

    public virtual IReadOnlyList<NeuronGroup> Groups => Array.Empty<NeuronGroup>();

    /// <summary>Localization key suffix of the current phase, e.g. "Training".</summary>
    public string PhaseKey { get; protected set; } = "Running";

    /// <summary>Progress of the scripted protocol (0–1), or null when open-ended.</summary>
    public double? Progress { get; protected set; }

    public virtual void Configure(NetworkParameters parameters) { }

    public virtual void Setup(SpikingNetwork network) { }

    public virtual void Update(SpikingNetwork network) { }

    /// <summary>Creates the network for this scenario.</summary>
    public SpikingNetwork CreateNetwork()
    {
        var parameters = new NetworkParameters();
        Configure(parameters);
        var network = new SpikingNetwork(parameters);
        Setup(network);
        PhaseKey = "Running";
        return network;
    }

    public void Restart(SpikingNetwork network)
    {
        var parameters = new NetworkParameters();
        Configure(parameters);
        network.Reset(parameters);
        Setup(network);
        PhaseKey = "Running";
    }

    /// <summary>Excitatory neurons in the index range [from, from + count) (skipping inhibitory cells).</summary>
    protected static int[] ExcitatoryRange(NetworkParameters p, int from, int count)
    {
        var list = new List<int>();
        for (int i = from; list.Count < count && i < p.NeuronCount; i++)
            if (!p.IsInhibitory(i)) list.Add(i);
        return list.ToArray();
    }
}
