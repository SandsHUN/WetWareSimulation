using WetWare.Core.Mathematics;

namespace WetWare.Core.Course.Supervised;

/// <summary>
/// Two-layer network of sigmoid units trained by backpropagation (lecture 8.1.4–8.1.5):
/// hidden h_j = g(β(W_j·u + b_j)), output v = g(β(Σ_j a_j h_j + c)), with g(x) = 1/(1 + e^(−x)).
/// The squared error E = ½ (d − v)² is minimised by gradient descent; the chain rule passes the output error
/// back to every hidden weight. Each hidden unit draws one line; their combination bends the decision border,
/// which is what solves XOR.
/// </summary>
public sealed class MultilayerPerceptron
{
    private double[,] _w = new double[0, 2];
    private double[] _b = Array.Empty<double>();
    private double[] _a = Array.Empty<double>();
    private double _c;
    private RandomSource _random = new(13);

    public MultilayerPerceptron() => Reset();

    public int Hidden { get; private set; } = 4;
    public double Beta { get; set; } = 1.5;
    public double LearningRate { get; set; } = 0.5;
    public int Epochs { get; private set; }

    public List<double> LossHistory { get; } = new();

    public void Reset(int hidden = -1, int seed = 13)
    {
        if (hidden > 0) Hidden = hidden;
        _random = new RandomSource(seed);
        _w = new double[Hidden, 2];
        _b = new double[Hidden];
        _a = new double[Hidden];
        for (int j = 0; j < Hidden; j++)
        {
            _w[j, 0] = _random.NextGaussian() * 1.5;
            _w[j, 1] = _random.NextGaussian() * 1.5;
            _b[j] = _random.NextGaussian() * 0.5;
            _a[j] = _random.NextGaussian() * 0.8;
        }
        _c = 0;
        Epochs = 0;
        LossHistory.Clear();
    }

    public static double Sigmoid(double x) => 1 / (1 + Math.Exp(-x));

    /// <summary>Output probability of class +1.</summary>
    public double Output(double x, double y)
    {
        double s = _c;
        for (int j = 0; j < Hidden; j++) s += _a[j] * Sigmoid(Beta * (_w[j, 0] * x + _w[j, 1] * y + _b[j]));
        return Sigmoid(Beta * s);
    }

    /// <summary>Hidden-unit line j: w_x x + w_y y + b = 0 (for drawing the hidden "half-planes").</summary>
    public (double Wx, double Wy, double B) HiddenLine(int j) => (_w[j, 0], _w[j, 1], _b[j]);

    /// <summary>One epoch of online backpropagation over the shuffled data; returns the mean loss.</summary>
    public double TrainEpoch(IReadOnlyList<LabeledPoint> data)
    {
        if (data.Count == 0) return 0;
        var order = Enumerable.Range(0, data.Count).OrderBy(_ => _random.NextDouble()).ToArray();
        var h = new double[Hidden];
        double loss = 0;
        foreach (int n in order)
        {
            var p = data[n];
            double d = p.Label > 0 ? 1 : 0;
            double s = _c;
            for (int j = 0; j < Hidden; j++)
            {
                h[j] = Sigmoid(Beta * (_w[j, 0] * p.X + _w[j, 1] * p.Y + _b[j]));
                s += _a[j] * h[j];
            }
            double v = Sigmoid(Beta * s);
            loss += 0.5 * (d - v) * (d - v);

            // Backward pass: δ_out = (v − d) g'(·), δ_j = δ_out a_j g'(·) (chain rule).
            double deltaOut = (v - d) * Beta * v * (1 - v);
            for (int j = 0; j < Hidden; j++)
            {
                double deltaHidden = deltaOut * _a[j] * Beta * h[j] * (1 - h[j]);
                _a[j] -= LearningRate * deltaOut * h[j];
                _w[j, 0] -= LearningRate * deltaHidden * p.X;
                _w[j, 1] -= LearningRate * deltaHidden * p.Y;
                _b[j] -= LearningRate * deltaHidden;
            }
            _c -= LearningRate * deltaOut;
        }
        Epochs++;
        loss /= data.Count;
        LossHistory.Add(loss);
        if (LossHistory.Count > 2000) LossHistory.RemoveAt(0);
        return loss;
    }

    public double Accuracy(IReadOnlyList<LabeledPoint> data) =>
        data.Count == 0 ? 0 : data.Count(p => (Output(p.X, p.Y) > 0.5 ? 1 : -1) == p.Label) / (double)data.Count;
}
