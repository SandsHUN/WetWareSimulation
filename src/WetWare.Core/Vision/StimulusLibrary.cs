using WetWare.Core.Mathematics;

namespace WetWare.Core.Vision;

/// <summary>Built-in visual stimuli, including classic illusions explained by lateral inhibition.</summary>
public enum VisualStimulus
{
    HermannGrid,
    MachBands,
    Bars,
    Grating,
    Checkerboard,
    Disc,
    Face,
    Mondrian,
    Gradient,
    NoisySquare,
}

public static class StimulusLibrary
{
    public const int Size = 96;

    public static GrayImage Create(VisualStimulus stimulus, double angleDegrees = 30, int size = Size)
    {
        var img = new GrayImage(size, size);
        switch (stimulus)
        {
            case VisualStimulus.HermannGrid:
                Fill(img, 0.95f);
                int cell = size / 6, street = Math.Max(3, size / 24);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        bool inStreetX = (x % cell) < street;
                        bool inStreetY = (y % cell) < street;
                        img[x, y] = inStreetX || inStreetY ? 0.95f : 0.05f;
                    }
                break;

            case VisualStimulus.MachBands:
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        int band = x * 6 / size;
                        img[x, y] = 0.12f + band * 0.15f;
                    }
                break;

            case VisualStimulus.Bars:
            {
                Fill(img, 0.1f);
                double a = angleDegrees * Math.PI / 180;
                double cos = Math.Cos(a), sin = Math.Sin(a);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        double u = (x - size / 2.0) * cos + (y - size / 2.0) * sin;
                        double v = -(x - size / 2.0) * sin + (y - size / 2.0) * cos;
                        if (Math.Abs(v) < size * 0.06 && Math.Abs(u) < size * 0.38) img[x, y] = 0.95f;
                        if (Math.Abs(u) < size * 0.05 && v > size * 0.15 && v < size * 0.42) img[x, y] = 0.95f;
                    }
                break;
            }

            case VisualStimulus.Grating:
            {
                double a = angleDegrees * Math.PI / 180;
                double k = 2 * Math.PI / (size / 8.0);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        double u = x * Math.Cos(a) + y * Math.Sin(a);
                        double r = Math.Sqrt(Math.Pow(x - size / 2.0, 2) + Math.Pow(y - size / 2.0, 2));
                        double window = Math.Exp(-r * r / (2 * Math.Pow(size * 0.3, 2)));
                        img[x, y] = (float)(0.5 + 0.45 * window * Math.Sin(k * u));
                    }
                break;
            }

            case VisualStimulus.Checkerboard:
            {
                int c = size / 8;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        img[x, y] = ((x / c) + (y / c)) % 2 == 0 ? 0.9f : 0.1f;
                break;
            }

            case VisualStimulus.Disc:
                Fill(img, 0.15f);
                FillCircle(img, size / 2.0, size / 2.0, size * 0.28, 0.9f);
                FillCircle(img, size / 2.0, size / 2.0, size * 0.1, 0.15f);
                break;

            case VisualStimulus.Face:
                Fill(img, 0.12f);
                FillCircle(img, size / 2.0, size / 2.0, size * 0.4, 0.85f);
                FillCircle(img, size * 0.36, size * 0.4, size * 0.06, 0.1f);
                FillCircle(img, size * 0.64, size * 0.4, size * 0.06, 0.1f);
                for (int x = (int)(size * 0.3); x < size * 0.7; x++)
                {
                    double t = (x - size / 2.0) / (size * 0.2);
                    int y = (int)(size * 0.62 + size * 0.08 * (1 - t * t));
                    for (int d = -2; d <= 2; d++)
                        if (y + d >= 0 && y + d < size) img[x, y + d] = 0.1f;
                }
                break;

            case VisualStimulus.Mondrian:
            {
                var rnd = new RandomSource(3);
                Fill(img, 0.5f);
                for (int r = 0; r < 14; r++)
                {
                    int x0 = rnd.Next(size), y0 = rnd.Next(size);
                    int w = rnd.Next(size / 8, size / 2), h = rnd.Next(size / 8, size / 2);
                    float v = (float)rnd.Uniform(0.05, 0.95);
                    for (int y = y0; y < Math.Min(size, y0 + h); y++)
                        for (int x = x0; x < Math.Min(size, x0 + w); x++)
                            img[x, y] = v;
                }
                break;
            }

            case VisualStimulus.Gradient:
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        img[x, y] = (float)x / (size - 1);
                // Two identical grey squares on different backgrounds (simultaneous contrast).
                for (int y = size * 3 / 8; y < size * 5 / 8; y++)
                {
                    for (int x = size / 8; x < size / 4 + size / 16; x++) img[x, y] = 0.5f;
                    for (int x = size * 3 / 4 - size / 16; x < size * 7 / 8; x++) img[x, y] = 0.5f;
                }
                break;

            case VisualStimulus.NoisySquare:
            {
                var rnd = new RandomSource(9);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        bool inside = x > size / 4 && x < size * 3 / 4 && y > size / 4 && y < size * 3 / 4;
                        img[x, y] = (float)Math.Clamp((inside ? 0.7 : 0.3) + 0.18 * rnd.NextGaussian(), 0, 1);
                    }
                break;
            }
        }
        return img;
    }

    private static void Fill(GrayImage img, float value) => Array.Fill(img.Data, value);

    private static void FillCircle(GrayImage img, double cx, double cy, double r, float value)
    {
        for (int y = 0; y < img.Height; y++)
            for (int x = 0; x < img.Width; x++)
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) img[x, y] = value;
    }
}
