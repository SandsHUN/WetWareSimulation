namespace WetWare.Core.Vision;

/// <summary>Single-channel floating-point image (row-major).</summary>
public sealed class GrayImage
{
    public GrayImage(int width, int height)
    {
        Width = width;
        Height = height;
        Data = new float[width * height];
    }

    public GrayImage(int width, int height, float[] data)
    {
        if (data.Length != width * height) throw new ArgumentException("Data size mismatch.", nameof(data));
        Width = width;
        Height = height;
        Data = data;
    }

    public int Width { get; }
    public int Height { get; }
    public float[] Data { get; }

    public float this[int x, int y]
    {
        get => Data[y * Width + x];
        set => Data[y * Width + x] = value;
    }

    /// <summary>Pixel with clamped (replicated) borders.</summary>
    public float GetClamped(int x, int y) =>
        Data[Math.Clamp(y, 0, Height - 1) * Width + Math.Clamp(x, 0, Width - 1)];

    public GrayImage Clone() => new(Width, Height, (float[])Data.Clone());

    public (float Min, float Max) Range()
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (float v in Data)
        {
            if (v < min) min = v;
            if (v > max) max = v;
        }
        return (min, max);
    }

    /// <summary>Resamples to a new size with bilinear interpolation.</summary>
    public GrayImage Resize(int width, int height)
    {
        var result = new GrayImage(width, height);
        for (int y = 0; y < height; y++)
        {
            double sy = (y + 0.5) * Height / height - 0.5;
            int y0 = (int)Math.Floor(sy);
            double fy = sy - y0;
            for (int x = 0; x < width; x++)
            {
                double sx = (x + 0.5) * Width / width - 0.5;
                int x0 = (int)Math.Floor(sx);
                double fx = sx - x0;
                double top = GetClamped(x0, y0) * (1 - fx) + GetClamped(x0 + 1, y0) * fx;
                double bottom = GetClamped(x0, y0 + 1) * (1 - fx) + GetClamped(x0 + 1, y0 + 1) * fx;
                result[x, y] = (float)(top * (1 - fy) + bottom * fy);
            }
        }
        return result;
    }
}
