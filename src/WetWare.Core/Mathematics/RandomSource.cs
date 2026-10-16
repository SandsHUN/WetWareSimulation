namespace WetWare.Core.Mathematics;

/// <summary>
/// Seedable random source with the distributions the biological models need
/// (uniform, Gaussian, Poisson). Wraps <see cref="Random"/> so every simulation is reproducible.
/// </summary>
public sealed class RandomSource
{
    private readonly Random _random;
    private double? _spareGaussian;

    public RandomSource(int seed) => _random = new Random(seed);

    public double NextDouble() => _random.NextDouble();

    public int Next(int maxExclusive) => _random.Next(maxExclusive);

    public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

    public double Uniform(double min, double max) => min + (max - min) * _random.NextDouble();

    public bool Bernoulli(double probability) => _random.NextDouble() < probability;

    /// <summary>Standard normal sample (Box–Muller, caches the second value).</summary>
    public double NextGaussian()
    {
        if (_spareGaussian is double spare)
        {
            _spareGaussian = null;
            return spare;
        }

        double u1 = 1.0 - _random.NextDouble();
        double u2 = _random.NextDouble();
        double radius = Math.Sqrt(-2.0 * Math.Log(u1));
        double angle = 2.0 * Math.PI * u2;
        _spareGaussian = radius * Math.Sin(angle);
        return radius * Math.Cos(angle);
    }

    /// <summary>Poisson-distributed count (Knuth's method, intended for small expectations).</summary>
    public int Poisson(double lambda)
    {
        if (lambda <= 0) return 0;
        if (lambda > 30) return Math.Max(0, (int)Math.Round(lambda + Math.Sqrt(lambda) * NextGaussian()));

        double limit = Math.Exp(-lambda);
        double product = _random.NextDouble();
        int count = 0;
        while (product > limit)
        {
            count++;
            product *= _random.NextDouble();
        }
        return count;
    }
}
