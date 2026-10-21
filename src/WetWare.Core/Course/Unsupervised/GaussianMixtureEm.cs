using WetWare.Core.Mathematics;

namespace WetWare.Core.Course.Unsupervised;

/// <summary>One 2-D Gaussian component: mixing weight π, mean μ and covariance Σ = [[Sxx, Sxy], [Sxy, Syy]].</summary>
public sealed record GaussianComponent(double Pi, double Mx, double My, double Sxx, double Sxy, double Syy)
{
    public double Determinant => Sxx * Syy - Sxy * Sxy;

    public double Density(double x, double y)
    {
        double det = Math.Max(Determinant, 1e-9);
        double dx = x - Mx, dy = y - My;
        double q = (Syy * dx * dx - 2 * Sxy * dx * dy + Sxx * dy * dy) / det;
        return Math.Exp(-0.5 * q) / (2 * Math.PI * Math.Sqrt(det));
    }

    /// <summary>Semi-axes and orientation (radians) of the 1-σ ellipse.</summary>
    public (double A, double B, double Angle) Ellipse
    {
        get
        {
            double tr = Sxx + Syy, disc = Math.Sqrt(Math.Max(0, (Sxx - Syy) * (Sxx - Syy) / 4 + Sxy * Sxy));
            double l1 = tr / 2 + disc, l2 = Math.Max(1e-9, tr / 2 - disc);
            double angle = 0.5 * Math.Atan2(2 * Sxy, Sxx - Syy);
            return (Math.Sqrt(l1), Math.Sqrt(l2), angle);
        }
    }
}

/// <summary>
/// Gaussian mixture model fitted with Expectation–Maximization (lecture 7.2.6–7.2.9).
/// Generative view: a hidden cause k ~ π (prior) picks a cluster, the observation u ~ N(μ_k, Σ_k) (likelihood);
/// the E-step computes the posterior responsibilities r_nk = p(k | u_n) – soft assignments – and the M-step
/// re-estimates π, μ, Σ weighted by them. Every iteration can only increase the log-likelihood.
/// </summary>
public sealed class GaussianMixtureEm
{
    public GaussianMixtureEm()
    {
        LogLikelihoodHistory = new List<double>();
        Points = ClusterData.Generate(ClusterLayout.Overlapping, seed: 12, count: 300);
        Reset();
    }

    public List<(double X, double Y)> Points { get; private set; }
    public List<GaussianComponent> Components { get; } = new();
    public int Count { get; private set; } = 2;

    /// <summary>Responsibilities r[n, k] = p(k | u_n).</summary>
    public double[,] Responsibilities { get; private set; } = new double[0, 0];

    public List<double> LogLikelihoodHistory { get; }

    public int Iterations { get; private set; }

    /// <summary>True when the last step was an E-step (the next one will be an M-step).</summary>
    public bool AwaitingMStep { get; private set; }

    public void SetData(List<(double X, double Y)> points)
    {
        Points = points;
        Reset();
    }

    public void SetCount(int count)
    {
        Count = Math.Clamp(count, 1, 6);
        Reset();
    }

    public void Reset(int seed = 17)
    {
        var random = new RandomSource(seed);
        Components.Clear();
        for (int k = 0; k < Count; k++)
        {
            var p = Points[random.Next(Points.Count)];
            Components.Add(new GaussianComponent(1.0 / Count, p.X * 0.3, p.Y * 0.3, 1.0, 0, 1.0));
        }
        Iterations = 0;
        AwaitingMStep = false;
        LogLikelihoodHistory.Clear();
        EStep();
        AwaitingMStep = false;
        LogLikelihoodHistory.Add(LogLikelihood());
    }

    /// <summary>Performs the next half-step (E or M) so the two phases can be watched separately.</summary>
    public void HalfStep()
    {
        if (AwaitingMStep)
        {
            MStep();
            Iterations++;
            LogLikelihoodHistory.Add(LogLikelihood());
            AwaitingMStep = false;
        }
        else
        {
            EStep();
            AwaitingMStep = true;
        }
    }

    public void Iterate()
    {
        if (!AwaitingMStep) EStep();
        MStep();
        Iterations++;
        LogLikelihoodHistory.Add(LogLikelihood());
        AwaitingMStep = false;
        EStep();
    }

    public void EStep()
    {
        int n = Points.Count, k = Components.Count;
        if (Responsibilities.GetLength(0) != n || Responsibilities.GetLength(1) != k) Responsibilities = new double[n, k];
        for (int i = 0; i < n; i++)
        {
            double total = 0;
            for (int j = 0; j < k; j++)
            {
                double p = Components[j].Pi * Components[j].Density(Points[i].X, Points[i].Y);
                Responsibilities[i, j] = p;
                total += p;
            }
            for (int j = 0; j < k; j++) Responsibilities[i, j] = total > 0 ? Responsibilities[i, j] / total : 1.0 / k;
        }
    }

    public void MStep()
    {
        int n = Points.Count;
        for (int j = 0; j < Components.Count; j++)
        {
            double nk = 0, mx = 0, my = 0;
            for (int i = 0; i < n; i++)
            {
                double r = Responsibilities[i, j];
                nk += r;
                mx += r * Points[i].X;
                my += r * Points[i].Y;
            }
            if (nk < 1e-6) continue;
            mx /= nk;
            my /= nk;
            double sxx = 0, sxy = 0, syy = 0;
            for (int i = 0; i < n; i++)
            {
                double r = Responsibilities[i, j], dx = Points[i].X - mx, dy = Points[i].Y - my;
                sxx += r * dx * dx;
                sxy += r * dx * dy;
                syy += r * dy * dy;
            }
            // A small ridge keeps a component from collapsing onto a single point.
            Components[j] = new GaussianComponent(nk / n, mx, my, sxx / nk + 0.01, sxy / nk, syy / nk + 0.01);
        }
    }

    /// <summary>Mean log-likelihood per point, log p(u) = log Σ_k π_k N(u; μ_k, Σ_k).</summary>
    public double LogLikelihood()
    {
        double sum = 0;
        foreach (var (x, y) in Points)
        {
            double p = Components.Sum(c => c.Pi * c.Density(x, y));
            sum += Math.Log(Math.Max(p, 1e-300));
        }
        return sum / Points.Count;
    }

    /// <summary>Posterior p(k | u) for an arbitrary point (hover read-out).</summary>
    public double[] Posterior(double x, double y)
    {
        var p = Components.Select(c => c.Pi * c.Density(x, y)).ToArray();
        double total = p.Sum();
        return p.Select(v => total > 0 ? v / total : 1.0 / p.Length).ToArray();
    }
}
