using WetWare.Core.Mathematics;

namespace WetWare.Core.Course.Unsupervised;

public enum HebbianRule
{
    /// <summary>τ_w dw/dt = u v – unstable, ‖w‖ grows without bound.</summary>
    Hebb,

    /// <summary>τ_w dw/dt = (u − ⟨u⟩) v – learns the covariance, still unstable.</summary>
    Covariance,

    /// <summary>τ_w dw/dt = u v − α v² w – stabilised, ‖w‖² → 1/α, w → principal eigenvector.</summary>
    Oja,
}

/// <summary>
/// A single linear neuron v = w·u learning from a two-dimensional Gaussian input cloud (lecture 7.1).
/// The cloud's principal axis is known analytically, so the angle between w and PC1 shows how a local
/// synaptic rule implements principal component analysis.
/// </summary>
public sealed class HebbianLearner
{
    private RandomSource _random = new(21);

    public HebbianLearner()
    {
        NormHistory = new RingBuffer<double>(800);
        AngleHistory = new RingBuffer<double>(800);
        Regenerate();
        Reset();
    }

    public HebbianRule Rule
    {
        get => _rule;
        set
        {
            _rule = value;
            if (Samples.Count > 0) UpdateStatistics();
        }
    }

    private HebbianRule _rule = HebbianRule.Oja;
    public double LearningRate { get; set; } = 0.01;
    public double Alpha { get; set; } = 1;

    /// <summary>Orientation of the principal axis of the cloud (degrees).</summary>
    public double CloudAngle { get; set; } = 35;

    /// <summary>Standard deviations along the principal and the minor axis.</summary>
    public double MajorSd { get; set; } = 1.4;
    public double MinorSd { get; set; } = 0.4;

    /// <summary>Offset of the cloud's mean from the origin (shows why the covariance rule subtracts it).</summary>
    public double MeanX { get; set; }
    public double MeanY { get; set; }

    /// <summary>Two separated clusters instead of one cloud (lecture 7.1.7 – where PCA is not enough).</summary>
    public bool TwoClusters { get; set; }

    public List<(double X, double Y)> Samples { get; } = new();

    public double Wx { get; private set; }
    public double Wy { get; private set; }

    public (double X, double Y)? LastInput { get; private set; }
    public double LastOutput { get; private set; }
    public int Steps { get; private set; }

    public RingBuffer<double> NormHistory { get; }
    public RingBuffer<double> AngleHistory { get; }

    public double Norm => Math.Sqrt(Wx * Wx + Wy * Wy);

    /// <summary>Empirical mean of the samples.</summary>
    public (double X, double Y) Mean { get; private set; }

    /// <summary>Principal eigenvector (unit) and eigenvalues of the matrix the rule averages over.</summary>
    public (double X, double Y, double Lambda1, double Lambda2) Principal { get; private set; }

    /// <summary>Angle (0–90°) between w and the principal eigenvector, ignoring the sign of w.</summary>
    public double AngleToPrincipal
    {
        get
        {
            double n = Norm;
            if (n < 1e-9) return 90;
            double cos = Math.Abs(Wx * Principal.X + Wy * Principal.Y) / n;
            return Math.Acos(Math.Clamp(cos, -1, 1)) * 180 / Math.PI;
        }
    }

    public void Regenerate(int seed = 21)
    {
        _random = new RandomSource(seed);
        Samples.Clear();
        double a = CloudAngle * Math.PI / 180;
        double ca = Math.Cos(a), sa = Math.Sin(a);
        for (int k = 0; k < 400; k++)
        {
            double p = MajorSd * _random.NextGaussian(), q = MinorSd * _random.NextGaussian();
            if (TwoClusters)
            {
                // Two tight blobs placed along the cloud axis.
                p = (k % 2 == 0 ? -1.3 : 1.3) + 0.3 * _random.NextGaussian();
                q = 0.3 * _random.NextGaussian();
            }
            Samples.Add((MeanX + p * ca - q * sa, MeanY + p * sa + q * ca));
        }
        UpdateStatistics();
    }

    public void Reset(int seed = 4)
    {
        var random = new RandomSource(seed);
        double angle = random.Uniform(0, 2 * Math.PI);
        Wx = 0.35 * Math.Cos(angle);
        Wy = 0.35 * Math.Sin(angle);
        Steps = 0;
        LastInput = null;
        NormHistory.Clear();
        AngleHistory.Clear();
    }

    /// <summary>Recomputes the averaged matrix: correlation Q for Hebb/Oja, covariance C for the covariance rule.</summary>
    public void UpdateStatistics()
    {
        double mx = Samples.Average(s => s.X), my = Samples.Average(s => s.Y);
        Mean = (mx, my);
        bool centre = Rule == HebbianRule.Covariance;
        double sxx = 0, sxy = 0, syy = 0;
        foreach (var (x, y) in Samples)
        {
            double dx = centre ? x - mx : x, dy = centre ? y - my : y;
            sxx += dx * dx;
            sxy += dx * dy;
            syy += dy * dy;
        }
        int n = Samples.Count;
        sxx /= n;
        sxy /= n;
        syy /= n;
        double tr = sxx + syy, det = sxx * syy - sxy * sxy;
        double disc = Math.Sqrt(Math.Max(0, tr * tr / 4 - det));
        double l1 = tr / 2 + disc, l2 = tr / 2 - disc;
        double ex = sxy, ey = l1 - sxx;
        if (Math.Abs(ex) + Math.Abs(ey) < 1e-12) (ex, ey) = sxx >= syy ? (1, 0) : (0, 1);
        double len = Math.Sqrt(ex * ex + ey * ey);
        Principal = (ex / len, ey / len, l1, l2);
    }

    public void Step(int count = 1)
    {
        for (int k = 0; k < count; k++)
        {
            var u = Samples[_random.Next(Samples.Count)];
            LastInput = u;
            double ux = u.X, uy = u.Y;
            double v = Wx * ux + Wy * uy;
            double dwx, dwy;
            switch (Rule)
            {
                case HebbianRule.Hebb:
                    dwx = ux * v;
                    dwy = uy * v;
                    break;
                case HebbianRule.Covariance:
                    // Centring the input: ⟨Δw⟩ = C w (equivalent to the lecture's u(v − ⟨v⟩) on average).
                    double cx = ux - Mean.X, cy = uy - Mean.Y;
                    double vc = Wx * cx + Wy * cy;
                    dwx = cx * vc;
                    dwy = cy * vc;
                    v = vc;
                    break;
                default:
                    dwx = ux * v - Alpha * v * v * Wx;
                    dwy = uy * v - Alpha * v * v * Wy;
                    break;
            }
            LastOutput = v;
            Wx += LearningRate * dwx;
            Wy += LearningRate * dwy;
            // Cap the unstable rules so the display stays finite.
            double n = Norm;
            if (n > 1e3)
            {
                Wx *= 1e3 / n;
                Wy *= 1e3 / n;
            }
            Steps++;
            if (Steps % 2 == 0)
            {
                NormHistory.Add(Norm);
                AngleHistory.Add(AngleToPrincipal);
            }
        }
    }
}
