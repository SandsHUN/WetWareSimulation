using WetWare.Core.Mathematics;

namespace WetWare.Core.Course.Unsupervised;

public enum PrototypeMode
{
    /// <summary>Online winner-takes-all: only the winner moves, Δw_i* = ε(u − w_i*).</summary>
    Competitive,

    /// <summary>Batch K-means: assign every point to its nearest centroid, then move each centroid to its cluster mean.</summary>
    KMeans,

    /// <summary>Kohonen self-organizing map: a 1-D chain of neurons, the winner's neighbours learn too.</summary>
    SelfOrganizingMap,
}

/// <summary>
/// Output neurons whose weight vectors are prototypes in input space (lecture 7.2.1–7.2.5).
/// For equal-norm weights the most active neuron (largest w·u) is the nearest prototype, so the
/// winner-takes-all step is a neural nearest-neighbour decision.
/// </summary>
public sealed class PrototypeLearner
{
    private RandomSource _random = new(31);

    public PrototypeLearner()
    {
        DistortionHistory = new RingBuffer<double>(600);
        Points = ClusterData.Generate(ClusterLayout.ThreeBlobs);
        Reset();
    }

    public PrototypeMode Mode { get; set; } = PrototypeMode.Competitive;
    public int Count { get; private set; } = 3;
    public double LearningRate { get; set; } = 0.05;

    /// <summary>Initial SOM neighbourhood width (in neuron index units); it shrinks during learning.</summary>
    public double NeighbourhoodStart { get; set; } = 4;

    public List<(double X, double Y)> Points { get; private set; }
    public List<(double X, double Y)> Prototypes { get; } = new();

    /// <summary>Recent trajectory of every prototype (for the "comet tails").</summary>
    public List<List<(double X, double Y)>> Trails { get; } = new();

    public int[] Assignment { get; private set; } = Array.Empty<int>();

    public (double X, double Y)? LastInput { get; private set; }
    public int LastWinner { get; private set; } = -1;
    public int Steps { get; private set; }
    public RingBuffer<double> DistortionHistory { get; }

    /// <summary>Current SOM neighbourhood width.</summary>
    public double Neighbourhood => Math.Max(0.4, NeighbourhoodStart * Math.Exp(-Steps / 900.0));

    public void SetData(List<(double X, double Y)> points)
    {
        Points = points;
        Reset();
    }

    public void SetCount(int count)
    {
        Count = Math.Clamp(count, 1, 24);
        Reset();
    }

    public void Reset(int seed = 31)
    {
        _random = new RandomSource(seed);
        Prototypes.Clear();
        Trails.Clear();
        for (int k = 0; k < Count; k++)
        {
            // Start clumped near the centre so the migration towards the data is visible.
            Prototypes.Add((_random.Uniform(-0.6, 0.6), _random.Uniform(-0.6, 0.6)));
            Trails.Add(new List<(double, double)> { Prototypes[k] });
        }
        Steps = 0;
        LastInput = null;
        LastWinner = -1;
        DistortionHistory.Clear();
        Assign();
    }

    public int Nearest(double x, double y)
    {
        int best = 0;
        double bestD = double.MaxValue;
        for (int k = 0; k < Prototypes.Count; k++)
        {
            double dx = x - Prototypes[k].X, dy = y - Prototypes[k].Y, d = dx * dx + dy * dy;
            if (d < bestD)
            {
                bestD = d;
                best = k;
            }
        }
        return best;
    }

    /// <summary>One online sample (competitive / SOM) or one full batch iteration (K-means).</summary>
    public void Step(int samples = 1)
    {
        if (Mode == PrototypeMode.KMeans)
        {
            KMeansIteration();
            return;
        }

        for (int s = 0; s < samples; s++)
        {
            var u = Points[_random.Next(Points.Count)];
            int winner = Nearest(u.X, u.Y);
            LastInput = u;
            LastWinner = winner;
            double sigma = Neighbourhood;
            for (int k = 0; k < Prototypes.Count; k++)
            {
                double h = Mode == PrototypeMode.SelfOrganizingMap
                    ? Math.Exp(-(k - winner) * (k - winner) / (2 * sigma * sigma))
                    : k == winner ? 1 : 0;
                if (h < 1e-4) continue;
                var w = Prototypes[k];
                Prototypes[k] = (w.X + LearningRate * h * (u.X - w.X), w.Y + LearningRate * h * (u.Y - w.Y));
            }
            Steps++;
            if (Steps % 4 == 0) RecordTrail();
        }
        Assign();
    }

    private void KMeansIteration()
    {
        Assign();
        var sums = new (double X, double Y, int N)[Prototypes.Count];
        for (int n = 0; n < Points.Count; n++)
        {
            int k = Assignment[n];
            sums[k] = (sums[k].X + Points[n].X, sums[k].Y + Points[n].Y, sums[k].N + 1);
        }
        for (int k = 0; k < Prototypes.Count; k++)
        {
            if (sums[k].N > 0) Prototypes[k] = (sums[k].X / sums[k].N, sums[k].Y / sums[k].N);
        }
        Steps++;
        LastInput = null;
        LastWinner = -1;
        RecordTrail();
        Assign();
    }

    private void RecordTrail()
    {
        for (int k = 0; k < Prototypes.Count; k++)
        {
            var trail = Trails[k];
            trail.Add(Prototypes[k]);
            if (trail.Count > 120) trail.RemoveAt(0);
        }
    }

    private void Assign()
    {
        if (Assignment.Length != Points.Count) Assignment = new int[Points.Count];
        double distortion = 0;
        for (int n = 0; n < Points.Count; n++)
        {
            int k = Nearest(Points[n].X, Points[n].Y);
            Assignment[n] = k;
            double dx = Points[n].X - Prototypes[k].X, dy = Points[n].Y - Prototypes[k].Y;
            distortion += dx * dx + dy * dy;
        }
        Distortion = distortion / Math.Max(1, Points.Count);
        DistortionHistory.Add(Distortion);
    }

    /// <summary>Mean squared distance of the points to their prototype (the K-means objective).</summary>
    public double Distortion { get; private set; }
}
