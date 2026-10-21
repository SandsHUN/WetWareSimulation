using WetWare.Core.Mathematics;

namespace WetWare.Core.Course.Reinforcement;

/// <summary>Location in the barn: A is the entrance, B/C are the two inner junctions, the leaves are food sites.</summary>
public enum BarnState
{
    A,
    B,
    C,
}

/// <summary>One move of the rat: from a state, an action (0 = left, 1 = right), its reward and the TD error it caused.</summary>
public sealed record BarnMove(BarnState From, int Action, BarnState? To, double Reward, double Delta);

/// <summary>
/// "Rat in a barn" (Dayan &amp; Abbott §9.3, lecture 8.3): a two-step decision tree with rewards
/// B-left 0, B-right 5, C-left 2, C-right 0. The critic learns state values with TD, the actor keeps action
/// preferences m(s, a) turned into a softmax policy P(a|s) ∝ exp(β m(s, a)) and nudges them with the same
/// TD error: m(s, a') += ε (δ_aa' − P(a'|s)) δ.
/// </summary>
public sealed class BarnMaze
{
    private readonly double[] _v = new double[3];
    private readonly double[,] _m = new double[3, 2];
    private RandomSource _random = new(29);

    public BarnMaze() => Reset();

    /// <summary>Reward at the leaf reached by (state, action) – the values from the lecture.</summary>
    public double[,] Rewards { get; } = { { 0, 0 }, { 0, 5 }, { 2, 0 } };

    public double CriticRate { get; set; } = 0.2;
    public double ActorRate { get; set; } = 0.2;
    public double Beta { get; set; } = 1;

    /// <summary>When false only the critic learns and the policy stays random (policy evaluation).</summary>
    public bool ActorLearns { get; set; } = true;

    public int Trials { get; private set; }

    public List<BarnMove> LastPath { get; } = new();

    public List<(double VA, double VB, double VC)> ValueHistory { get; } = new();
    public List<(double PA, double PB, double PC)> PolicyHistory { get; } = new();

    public double Value(BarnState s) => _v[(int)s];

    public double Preference(BarnState s, int action) => _m[(int)s, action];

    /// <summary>Probability of turning right in state s.</summary>
    public double ProbabilityRight(BarnState s)
    {
        if (!ActorLearns) return 0.5;
        double l = Beta * _m[(int)s, 0], r = Beta * _m[(int)s, 1];
        double max = Math.Max(l, r);
        double el = Math.Exp(l - max), er = Math.Exp(r - max);
        return er / (el + er);
    }

    /// <summary>State values under the random policy, computed exactly (what the critic should find).</summary>
    public (double A, double B, double C) RandomPolicyValues
    {
        get
        {
            double b = (Rewards[1, 0] + Rewards[1, 1]) / 2, c = (Rewards[2, 0] + Rewards[2, 1]) / 2;
            return ((b + c) / 2, b, c);
        }
    }

    public void Reset(int seed = 29)
    {
        _random = new RandomSource(seed);
        Array.Clear(_v);
        Array.Clear(_m);
        Trials = 0;
        LastPath.Clear();
        ValueHistory.Clear();
        PolicyHistory.Clear();
        Record();
    }

    /// <summary>Runs one complete trial A → (B|C) → food, learning after every move.</summary>
    public void RunTrial()
    {
        LastPath.Clear();
        var state = BarnState.A;
        while (true)
        {
            int action = _random.Bernoulli(ProbabilityRight(state)) ? 1 : 0;
            BarnState? next = state == BarnState.A ? (action == 0 ? BarnState.B : BarnState.C) : null;
            double reward = state == BarnState.A ? 0 : Rewards[(int)state, action];
            double vNext = next is { } n ? _v[(int)n] : 0;
            double delta = reward + vNext - _v[(int)state];

            _v[(int)state] += CriticRate * delta;
            if (ActorLearns)
            {
                double pRight = ProbabilityRight(state);
                double[] p = [1 - pRight, pRight];
                for (int a = 0; a < 2; a++) _m[(int)state, a] += ActorRate * ((a == action ? 1 : 0) - p[a]) * delta;
            }

            LastPath.Add(new BarnMove(state, action, next, reward, delta));
            if (next is null) break;
            state = next.Value;
        }
        Trials++;
        Record();
    }

    private void Record()
    {
        ValueHistory.Add((_v[0], _v[1], _v[2]));
        PolicyHistory.Add((ProbabilityRight(BarnState.A), ProbabilityRight(BarnState.B), ProbabilityRight(BarnState.C)));
        if (ValueHistory.Count > 1000)
        {
            ValueHistory.RemoveAt(0);
            PolicyHistory.RemoveAt(0);
        }
    }
}
