using WetWare.Core.Mathematics;

namespace WetWare.Core.Course.Supervised;

/// <summary>A training example in the plane with its desired output v^d = ±1.</summary>
public readonly record struct LabeledPoint(double X, double Y, int Label);

public enum PlaneDataset
{
    Separable,
    And,
    Or,
    Xor,
    Circle,
    Moons,
}

/// <summary>Two-class toy problems for the supervised-learning lesson (8.1).</summary>
public static class PlaneDatasets
{
    public static List<LabeledPoint> Generate(PlaneDataset dataset, int seed = 5)
    {
        var random = new RandomSource(seed);
        var points = new List<LabeledPoint>();
        switch (dataset)
        {
            case PlaneDataset.Separable:
                for (int k = 0; k < 60; k++)
                {
                    int label = k % 2 == 0 ? 1 : -1;
                    double cx = label * 0.45, cy = label * 0.25;
                    points.Add(new(cx + 0.28 * random.NextGaussian(), cy + 0.28 * random.NextGaussian(), label));
                }
                break;
            case PlaneDataset.And:
            case PlaneDataset.Or:
            case PlaneDataset.Xor:
                foreach (var (x, y) in new[] { (0, 0), (0, 1), (1, 0), (1, 1) })
                {
                    bool a = x == 1, b = y == 1;
                    bool truth = dataset switch
                    {
                        PlaneDataset.And => a && b,
                        PlaneDataset.Or => a || b,
                        _ => a ^ b,
                    };
                    // Corners at ±0.7 with a little scatter so the plot is not just four dots.
                    for (int r = 0; r < 12; r++)
                    {
                        points.Add(new((x * 2 - 1) * 0.7 + 0.1 * random.NextGaussian(),
                            (y * 2 - 1) * 0.7 + 0.1 * random.NextGaussian(), truth ? 1 : -1));
                    }
                }
                break;
            case PlaneDataset.Circle:
                for (int k = 0; k < 90; k++)
                {
                    double a = random.Uniform(0, 2 * Math.PI);
                    bool inner = k % 2 == 0;
                    double r = inner ? random.Uniform(0, 0.45) : random.Uniform(0.7, 1.0);
                    points.Add(new(r * Math.Cos(a), r * Math.Sin(a), inner ? 1 : -1));
                }
                break;
            default:
                for (int k = 0; k < 90; k++)
                {
                    bool upper = k % 2 == 0;
                    double t = random.Uniform(0, Math.PI);
                    double x = upper ? Math.Cos(t) * 0.7 - 0.25 : 0.25 - Math.Cos(t) * 0.7;
                    double y = upper ? Math.Sin(t) * 0.7 - 0.15 : 0.15 - Math.Sin(t) * 0.7;
                    points.Add(new(x + 0.07 * random.NextGaussian(), y + 0.07 * random.NextGaussian(), upper ? 1 : -1));
                }
                break;
        }
        return points;
    }
}
