namespace WetWare.Core.Vision;

/// <summary>Convolution kernels modelling receptive fields of the early visual system.</summary>
public static class Kernels
{
    /// <summary>
    /// Difference of Gaussians: an excitatory centre minus a wider inhibitory surround —
    /// the receptive field of an ON-centre retinal ganglion cell. Normalised to zero mean
    /// so uniform illumination produces no response.
    /// </summary>
    public static float[,] DifferenceOfGaussians(double sigmaCenter, double sigmaSurround, double surroundWeight = 1.0)
    {
        int radius = (int)Math.Ceiling(sigmaSurround * 3);
        int size = 2 * radius + 1;
        var kernel = new float[size, size];
        double sumC = 0, sumS = 0;
        var c = new double[size, size];
        var s = new double[size, size];
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                double r2 = x * x + y * y;
                c[y + radius, x + radius] = Math.Exp(-r2 / (2 * sigmaCenter * sigmaCenter));
                s[y + radius, x + radius] = Math.Exp(-r2 / (2 * sigmaSurround * sigmaSurround));
                sumC += c[y + radius, x + radius];
                sumS += s[y + radius, x + radius];
            }
        }
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                kernel[y, x] = (float)(c[y, x] / sumC - surroundWeight * s[y, x] / sumS);
        return kernel;
    }

    /// <summary>
    /// Even (cosine) Gabor filter: the receptive field of a V1 simple cell preferring edges/bars
    /// at <paramref name="thetaRadians"/>. Zero mean, unit L1 norm.
    /// </summary>
    public static float[,] Gabor(double thetaRadians, double wavelength, double sigma, double aspect = 0.6, double phase = 0)
    {
        int radius = (int)Math.Ceiling(sigma * 2.5);
        int size = 2 * radius + 1;
        var raw = new double[size, size];
        double mean = 0;
        double cos = Math.Cos(thetaRadians), sin = Math.Sin(thetaRadians);
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                // x' runs across the preferred bar, y' along it.
                double xr = x * cos + y * sin;
                double yr = -x * sin + y * cos;
                double envelope = Math.Exp(-(xr * xr + aspect * aspect * yr * yr) / (2 * sigma * sigma));
                double v = envelope * Math.Cos(2 * Math.PI * xr / wavelength + phase);
                raw[y + radius, x + radius] = v;
                mean += v;
            }
        }
        mean /= size * size;
        double l1 = 0;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                raw[y, x] -= mean;
                l1 += Math.Abs(raw[y, x]);
            }
        var kernel = new float[size, size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                kernel[y, x] = (float)(raw[y, x] / l1 * 2);
        return kernel;
    }

    /// <summary>2-D convolution with replicated borders.</summary>
    public static GrayImage Convolve(GrayImage image, float[,] kernel)
    {
        int kh = kernel.GetLength(0), kw = kernel.GetLength(1);
        int ry = kh / 2, rx = kw / 2;
        int w = image.Width, h = image.Height;
        var result = new GrayImage(w, h);
        var src = image.Data;
        var dst = result.Data;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                double sum = 0;
                for (int ky = 0; ky < kh; ky++)
                {
                    int sy = Math.Clamp(y + ky - ry, 0, h - 1) * w;
                    for (int kx = 0; kx < kw; kx++)
                    {
                        int sx = Math.Clamp(x + kx - rx, 0, w - 1);
                        sum += src[sy + sx] * kernel[ky, kx];
                    }
                }
                dst[y * w + x] = (float)sum;
            }
        }
        return result;
    }
}
