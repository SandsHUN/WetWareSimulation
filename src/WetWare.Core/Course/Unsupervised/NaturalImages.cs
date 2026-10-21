using WetWare.Core.Mathematics;

namespace WetWare.Core.Course.Unsupervised;

/// <summary>
/// Synthetic "natural" scenes for the sparse-coding lesson: a dead-leaves model (randomly placed, oriented,
/// occluding shapes with power-law sizes) reproduces the key statistics of natural images – sharp edges at
/// all orientations and an approximately 1/f spectrum. A centre–surround filter then whitens the image the
/// way the retina does, so learning concentrates on edges rather than on slow brightness gradients.
/// </summary>
public static class NaturalImages
{
    public const int Size = 128;

    public static float[] Generate(int seed, bool whiten = true)
    {
        var random = new RandomSource(seed);
        var image = new float[Size * Size];
        Array.Fill(image, 0.5f);

        for (int s = 0; s < 220; s++)
        {
            // Power-law radii: many small leaves, a few big ones.
            double radius = 4 * Math.Pow(1 - random.NextDouble() * 0.97, -0.8);
            radius = Math.Min(radius, 50);
            double cx = random.Uniform(-10, Size + 10), cy = random.Uniform(-10, Size + 10);
            double angle = random.Uniform(0, Math.PI);
            double aspect = random.Uniform(0.25, 1);
            bool rectangle = random.Bernoulli(0.5);
            float value = (float)random.NextDouble();
            double ca = Math.Cos(angle), sa = Math.Sin(angle);
            int x0 = Math.Max(0, (int)(cx - radius)), x1 = Math.Min(Size - 1, (int)(cx + radius));
            int y0 = Math.Max(0, (int)(cy - radius)), y1 = Math.Min(Size - 1, (int)(cy + radius));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    double dx = x - cx, dy = y - cy;
                    double p = (dx * ca + dy * sa) / radius, q = (-dx * sa + dy * ca) / (radius * aspect);
                    bool inside = rectangle ? Math.Abs(p) <= 1 && Math.Abs(q) <= 1 : p * p + q * q <= 1;
                    if (inside) image[y * Size + x] = value;
                }
            }
        }

        image = Blur(image, 0.8);
        if (!whiten) return image;

        // Centre–surround whitening: image − blurred image (a difference-of-Gaussians high-pass).
        var surround = Blur(image, 2.5);
        for (int i = 0; i < image.Length; i++) image[i] -= surround[i];
        double sd = Math.Sqrt(image.Average(v => (double)v * v));
        for (int i = 0; i < image.Length; i++) image[i] = (float)(image[i] / Math.Max(sd, 1e-6));
        return image;
    }

    private static float[] Blur(float[] src, double sigma)
    {
        int radius = (int)Math.Ceiling(sigma * 3);
        var kernel = new double[2 * radius + 1];
        double sum = 0;
        for (int i = -radius; i <= radius; i++) sum += kernel[i + radius] = Math.Exp(-i * i / (2 * sigma * sigma));
        for (int i = 0; i < kernel.Length; i++) kernel[i] /= sum;

        var tmp = new float[src.Length];
        var dst = new float[src.Length];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                double acc = 0;
                for (int k = -radius; k <= radius; k++) acc += kernel[k + radius] * src[y * Size + Math.Clamp(x + k, 0, Size - 1)];
                tmp[y * Size + x] = (float)acc;
            }
        }
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                double acc = 0;
                for (int k = -radius; k <= radius; k++) acc += kernel[k + radius] * tmp[Math.Clamp(y + k, 0, Size - 1) * Size + x];
                dst[y * Size + x] = (float)acc;
            }
        }
        return dst;
    }
}
