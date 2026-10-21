using WetWare.Core.Mathematics;

namespace WetWare.Core.Course.Unsupervised;

/// <summary>
/// Sparse coding of natural image patches (Olshausen &amp; Field; lecture 7.3).
/// Generative model u = G v + noise with a sparse (Laplace) prior on v. Inference finds
/// v* = argmin ½‖u − Gv‖² + λ‖v‖₁ by iterative shrinkage, which is literally a predictive-coding loop:
/// the prediction G v is fed back, the prediction error e = u − G v is fed forward (Gᵀe) and nudges v,
/// and a soft threshold keeps most causes silent. Learning is the Hebbian-like rule ΔG = η e vᵀ.
/// </summary>
public sealed class SparseCodingModel
{
    public const int Patch = 8;
    public const int Dim = Patch * Patch;
    public const int Bases = 64;

    private readonly List<float[]> _images = new();
    private RandomSource _random = new(41);
    private double _lipschitz = 1;

    public SparseCodingModel()
    {
        for (int k = 0; k < 4; k++) _images.Add(NaturalImages.Generate(100 + k));
        ErrorHistory = new RingBuffer<double>(800);
        SparsityHistory = new RingBuffer<double>(800);
        Reset();
        CurrentPatch = Array.Empty<double>();
        NextPatch();
    }

    /// <summary>Dictionary G: column k (G[·, k]) is basis function k.</summary>
    public double[,] G { get; } = new double[Dim, Bases];

    public double Lambda { get; set; } = 0.8;
    public double LearningRate { get; set; } = 0.06;
    public int InferenceIterations { get; set; } = 60;
    public int BatchSize { get; set; } = 12;

    public int Steps { get; private set; }

    public RingBuffer<double> ErrorHistory { get; }
    public RingBuffer<double> SparsityHistory { get; }

    /// <summary>The whitened scene shown to the user (patches are drawn from these).</summary>
    public IReadOnlyList<float[]> Images => _images;

    public double[] CurrentPatch { get; private set; }
    public (int Image, int X, int Y) CurrentPatchLocation { get; private set; }

    public void Reset(int seed = 41)
    {
        _random = new RandomSource(seed);
        for (int k = 0; k < Bases; k++)
        {
            for (int i = 0; i < Dim; i++) G[i, k] = _random.NextGaussian();
        }
        NormalizeColumns();
        Steps = 0;
        ErrorHistory.Clear();
        SparsityHistory.Clear();
    }

    public double[] SamplePatch() => SamplePatch(out _);

    private double[] SamplePatch(out (int Image, int X, int Y) location)
    {
        int img = _random.Next(_images.Count);
        int x = _random.Next(NaturalImages.Size - Patch), y = _random.Next(NaturalImages.Size - Patch);
        location = (img, x, y);
        return ExtractPatch(img, x, y);
    }

    public double[] ExtractPatch(int img, int x, int y)
    {
        var patch = new double[Dim];
        var image = _images[img];
        for (int r = 0; r < Patch; r++)
            for (int c = 0; c < Patch; c++)
                patch[r * Patch + c] = image[(y + r) * NaturalImages.Size + x + c];
        double mean = patch.Average();
        for (int i = 0; i < Dim; i++) patch[i] -= mean;
        return patch;
    }

    public void SelectPatch(int image, int x, int y)
    {
        CurrentPatch = ExtractPatch(image, x, y);
        CurrentPatchLocation = (image, x, y);
    }

    /// <summary>Encodes an arbitrary (synthetic) patch, e.g. a test stimulus of the predictive-coding lesson.</summary>
    public void SetPatch(double[] patch) => CurrentPatch = patch;

    public void NextPatch()
    {
        CurrentPatch = SamplePatch(out var location);
        CurrentPatchLocation = location;
    }

    /// <summary>Prediction G v.</summary>
    public double[] Predict(double[] v)
    {
        var p = new double[Dim];
        for (int k = 0; k < Bases; k++)
        {
            double vk = v[k];
            if (vk == 0) continue;
            for (int i = 0; i < Dim; i++) p[i] += G[i, k] * vk;
        }
        return p;
    }

    /// <summary>
    /// Runs inference for <paramref name="u"/>; <paramref name="onIteration"/> receives every intermediate v,
    /// so the predictive-coding panel can replay the settling.
    /// </summary>
    public double[] Infer(double[] u, int? iterations = null, Action<int, double[]>? onIteration = null)
    {
        var v = new double[Bases];
        var grad = new double[Bases];
        double step = 1 / _lipschitz;
        int count = iterations ?? InferenceIterations;
        for (int it = 0; it < count; it++)
        {
            var prediction = Predict(v);
            // Feed-forward of the prediction error: Gᵀ(u − Gv).
            for (int k = 0; k < Bases; k++)
            {
                double s = 0;
                for (int i = 0; i < Dim; i++) s += G[i, k] * (u[i] - prediction[i]);
                grad[k] = s;
            }
            for (int k = 0; k < Bases; k++)
            {
                double z = v[k] + step * grad[k];
                double t = Lambda * step;
                v[k] = z > t ? z - t : z < -t ? z + t : 0;
            }
            onIteration?.Invoke(it, v);
        }
        return v;
    }

    /// <summary>Objective ½‖u − Gv‖² + λ‖v‖₁ split into its two parts.</summary>
    public (double Reconstruction, double Sparsity) Energy(double[] u, double[] v)
    {
        var p = Predict(v);
        double rec = 0;
        for (int i = 0; i < Dim; i++) rec += (u[i] - p[i]) * (u[i] - p[i]);
        return (0.5 * rec, Lambda * v.Sum(Math.Abs));
    }

    /// <summary>One dictionary-learning step on a mini-batch: infer v for each patch, then ΔG = η (u − Gv) vᵀ.</summary>
    public void LearnStep()
    {
        var delta = new double[Dim, Bases];
        double error = 0, active = 0, power = 0;
        for (int b = 0; b < BatchSize; b++)
        {
            var u = SamplePatch();
            var v = Infer(u);
            var p = Predict(v);
            for (int i = 0; i < Dim; i++)
            {
                double e = u[i] - p[i];
                error += e * e;
                power += u[i] * u[i];
                if (e == 0) continue;
                for (int k = 0; k < Bases; k++)
                {
                    if (v[k] != 0) delta[i, k] += e * v[k];
                }
            }
            active += v.Count(x => x != 0);
        }
        for (int i = 0; i < Dim; i++)
            for (int k = 0; k < Bases; k++)
                G[i, k] += LearningRate * delta[i, k] / BatchSize;
        NormalizeColumns();
        Steps++;
        ErrorHistory.Add(error / Math.Max(power, 1e-9));
        SparsityHistory.Add(active / BatchSize / Bases);
    }

    /// <summary>Basis function k as an 8×8 patch.</summary>
    public double[] Basis(int k)
    {
        var b = new double[Dim];
        for (int i = 0; i < Dim; i++) b[i] = G[i, k];
        return b;
    }

    /// <summary>
    /// Principal components of the same patches ("eigen-patches", the eigenfaces analogue): global, Fourier-like
    /// patterns ordered by explained variance, for comparison with the local sparse features.
    /// </summary>
    public (double[] Variance, double[][] Components) PrincipalComponents(int samples = 3000)
    {
        var random = new RandomSource(77);
        var cov = new double[Dim, Dim];
        for (int s = 0; s < samples; s++)
        {
            int img = random.Next(_images.Count);
            var u = ExtractPatch(img, random.Next(NaturalImages.Size - Patch), random.Next(NaturalImages.Size - Patch));
            for (int i = 0; i < Dim; i++)
                for (int j = i; j < Dim; j++)
                    cov[i, j] += u[i] * u[j];
        }
        for (int i = 0; i < Dim; i++)
        {
            for (int j = i; j < Dim; j++)
            {
                cov[i, j] /= samples;
                cov[j, i] = cov[i, j];
            }
        }
        var (values, vectors) = SymmetricEigen.Decompose(cov);
        var components = new double[Dim][];
        for (int k = 0; k < Dim; k++)
        {
            components[k] = new double[Dim];
            for (int i = 0; i < Dim; i++) components[k][i] = vectors[i, k];
        }
        return (values, components);
    }

    private void NormalizeColumns()
    {
        for (int k = 0; k < Bases; k++)
        {
            double n = 0;
            for (int i = 0; i < Dim; i++) n += G[i, k] * G[i, k];
            n = Math.Sqrt(Math.Max(n, 1e-12));
            for (int i = 0; i < Dim; i++) G[i, k] /= n;
        }
        _lipschitz = EstimateLipschitz();
    }

    /// <summary>Largest eigenvalue of GᵀG by power iteration – the step size bound of the shrinkage algorithm.</summary>
    private double EstimateLipschitz()
    {
        var x = Enumerable.Repeat(1.0 / Math.Sqrt(Bases), Bases).ToArray();
        double lambda = 1;
        for (int it = 0; it < 20; it++)
        {
            var y = Predict(x);
            var z = new double[Bases];
            for (int k = 0; k < Bases; k++)
            {
                double s = 0;
                for (int i = 0; i < Dim; i++) s += G[i, k] * y[i];
                z[k] = s;
            }
            lambda = Math.Sqrt(z.Sum(v => v * v));
            if (lambda < 1e-12) return 1;
            for (int k = 0; k < Bases; k++) x[k] = z[k] / lambda;
        }
        return lambda * 1.05;
    }
}
