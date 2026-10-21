using WetWare.Core.Mathematics;

namespace WetWare.Core.Course.Reinforcement;

public enum TrialKind
{
    /// <summary>Cue followed by reward (normal conditioning trial).</summary>
    Paired,

    /// <summary>Cue, but the expected reward is withheld (omission probe).</summary>
    Omission,

    /// <summary>Reward without any cue (unexpected reward).</summary>
    RewardOnly,
}

/// <summary>One simulated trial: the value prediction v(t) and the prediction error δ(t) at every time step.</summary>
public sealed record TdTrial(int Index, TrialKind Kind, double[] Value, double[] Delta, double[] Reward);

/// <summary>
/// Temporal-difference prediction of a delayed reward with a tapped delay line (Pavlovian conditioning,
/// lecture 8.2; Dayan &amp; Abbott §9.2). The cue at t_s feeds weights w(τ) so that v(t) = Σ_τ w(τ) u(t − τ);
/// δ(t) = r(t) + v(t+1) − v(t) and Δw(τ) = ε δ(t) u(t − τ). Over trials the error moves from the reward
/// back to the cue – the signature of midbrain dopamine neurons.
/// </summary>
public sealed class TdConditioning
{
    public const int Steps = 80;

    private readonly double[] _w = new double[Steps];
    private RandomSource _random = new(23);

    public TdConditioning() => Reset();

    public int CueTime { get; set; } = 15;
    public int RewardTime { get; set; } = 45;
    public double RewardSize { get; set; } = 1;

    /// <summary>Probability that a paired trial is actually rewarded (partial reinforcement).</summary>
    public double RewardProbability { get; set; } = 1;

    public double LearningRate { get; set; } = 0.3;

    /// <summary>Discount factor γ (1 = the course's undiscounted sum).</summary>
    public double Gamma { get; set; } = 1;

    public List<TdTrial> Trials { get; } = new();

    public TdTrial? Last => Trials.Count > 0 ? Trials[^1] : null;

    public IReadOnlyList<double> Weights => _w;

    public void Reset(int seed = 23)
    {
        _random = new RandomSource(seed);
        Array.Clear(_w);
        Trials.Clear();
    }

    public TdTrial RunTrial(TrialKind kind = TrialKind.Paired)
    {
        bool cue = kind != TrialKind.RewardOnly;
        bool rewarded = kind switch
        {
            TrialKind.Omission => false,
            TrialKind.RewardOnly => true,
            _ => _random.Bernoulli(RewardProbability),
        };

        var reward = new double[Steps];
        if (rewarded && RewardTime < Steps) reward[RewardTime] = RewardSize;

        // v(t) = w(t − t_s) after the cue, 0 before it (and on cue-less trials).
        var value = new double[Steps + 1];
        for (int t = 0; t < Steps; t++) value[t] = cue && t >= CueTime ? _w[t - CueTime] : 0;
        value[Steps] = 0;

        var delta = new double[Steps];
        for (int t = 0; t < Steps; t++) delta[t] = reward[t] + Gamma * value[t + 1] - value[t];

        if (cue)
        {
            for (int t = CueTime; t < Steps; t++) _w[t - CueTime] += LearningRate * delta[t];
        }

        var trial = new TdTrial(Trials.Count + 1, kind, value[..Steps], delta, reward);
        Trials.Add(trial);
        if (Trials.Count > 400) Trials.RemoveAt(0);
        return trial;
    }

    /// <summary>Prediction error at the cue and at the reward time of a trial (the two dopamine peaks).</summary>
    public (double AtCue, double AtReward) Peaks(TdTrial trial)
    {
        double cue = trial.Delta[Math.Max(0, CueTime - 1)];
        double reward = trial.Delta[Math.Clamp(RewardTime, 0, Steps - 1)];
        return (cue, reward);
    }
}
