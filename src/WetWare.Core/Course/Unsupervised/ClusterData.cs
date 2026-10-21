using WetWare.Core.Mathematics;

namespace WetWare.Core.Course.Unsupervised;

public enum ClusterLayout
{
    TwoBlobs,
    ThreeBlobs,
    FourBlobs,
    Overlapping,
    Elongated,
    Ring,
}

/// <summary>Unlabelled 2-D point clouds for the clustering lessons (7.2).</summary>
public static class ClusterData
{
    public static List<(double X, double Y)> Generate(ClusterLayout layout, int seed = 8, int count = 360)
    {
        var random = new RandomSource(seed);
        var points = new List<(double X, double Y)>(count);
        (double X, double Y, double Sx, double Sy, double Angle)[] blobs = layout switch
        {
            ClusterLayout.TwoBlobs => [(-1.5, -0.6, 0.45, 0.45, 0), (1.4, 0.9, 0.5, 0.5, 0)],
            ClusterLayout.ThreeBlobs => [(-1.6, -1.0, 0.4, 0.4, 0), (1.5, -0.8, 0.45, 0.4, 0), (0.0, 1.6, 0.45, 0.45, 0)],
            ClusterLayout.FourBlobs => [(-1.5, -1.4, 0.35, 0.35, 0), (1.5, -1.4, 0.35, 0.35, 0), (-1.5, 1.4, 0.35, 0.35, 0), (1.5, 1.4, 0.35, 0.35, 0)],
            ClusterLayout.Overlapping => [(-0.7, -0.3, 0.7, 0.6, 0), (0.8, 0.4, 0.6, 0.7, 0)],
            ClusterLayout.Elongated => [(-1.0, -1.0, 1.1, 0.25, 40), (1.2, 1.0, 1.0, 0.25, -35)],
            _ => [],
        };

        for (int k = 0; k < count; k++)
        {
            if (layout == ClusterLayout.Ring)
            {
                double a = random.Uniform(0, 2 * Math.PI), r = 2 + 0.18 * random.NextGaussian();
                points.Add((r * Math.Cos(a), r * Math.Sin(a)));
                continue;
            }
            var b = blobs[k % blobs.Length];
            double p = b.Sx * random.NextGaussian(), q = b.Sy * random.NextGaussian();
            double t = b.Angle * Math.PI / 180;
            points.Add((b.X + p * Math.Cos(t) - q * Math.Sin(t), b.Y + p * Math.Sin(t) + q * Math.Cos(t)));
        }
        return points;
    }
}
