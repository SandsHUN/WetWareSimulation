using WetWare.Core.Mathematics;

namespace WetWare.Core.Course.Supervised;

/// <summary>
/// Rosenblatt's perceptron (lecture 8.1.2–8.1.4): v = Θ(wᵀu − μ) with Θ = ±1.
/// Learning rule: Δw = ε (v^d − v) u, Δμ = −ε (v^d − v). Nothing changes for correctly classified examples,
/// and for linearly separable data the rule converges after a finite number of mistakes.
/// </summary>
public sealed class Perceptron
{
    private RandomSource _random = new(9);
    private int _cursor;

    public double W1 { get; private set; }
    public double W2 { get; private set; }
    public double Mu { get; private set; }
    public double LearningRate { get; set; } = 0.1;

    public int Updates { get; private set; }
    public int Presentations { get; private set; }

    /// <summary>The example presented last and whether it caused an update.</summary>
    public (LabeledPoint Point, bool Updated)? Last { get; private set; }

    public void Reset(int seed = 9)
    {
        _random = new RandomSource(seed);
        double a = _random.Uniform(0, 2 * Math.PI);
        W1 = 0.3 * Math.Cos(a);
        W2 = 0.3 * Math.Sin(a);
        Mu = 0;
        Updates = 0;
        Presentations = 0;
        _cursor = 0;
        Last = null;
    }

    public int Classify(double x, double y) => W1 * x + W2 * y - Mu > 0 ? 1 : -1;

    public double Activation(double x, double y) => W1 * x + W2 * y - Mu;

    /// <summary>Presents the next example (cyclic order) and applies the learning rule.</summary>
    public void Step(IReadOnlyList<LabeledPoint> data)
    {
        if (data.Count == 0) return;
        var p = data[_cursor % data.Count];
        _cursor = (_cursor + 1 + _random.Next(Math.Max(1, data.Count / 3))) % data.Count;
        int v = Classify(p.X, p.Y);
        int error = p.Label - v; // 0 or ±2
        if (error != 0)
        {
            W1 += LearningRate * error * p.X;
            W2 += LearningRate * error * p.Y;
            Mu -= LearningRate * error;
            Updates++;
        }
        Presentations++;
        Last = (p, error != 0);
    }

    public double Accuracy(IReadOnlyList<LabeledPoint> data) =>
        data.Count == 0 ? 0 : data.Count(p => Classify(p.X, p.Y) == p.Label) / (double)data.Count;
}
