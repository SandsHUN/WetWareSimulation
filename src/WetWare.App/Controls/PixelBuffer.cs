using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WetWare.App.Controls;

/// <summary>CPU pixel buffer backed by a <see cref="WriteableBitmap"/> for fast per-frame drawing.</summary>
public sealed class PixelBuffer
{
    public PixelBuffer(int width, int height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        Pixels = new int[Width * Height];
        Bitmap = new WriteableBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32, null);
    }

    public int Width { get; }
    public int Height { get; }
    public int[] Pixels { get; }
    public WriteableBitmap Bitmap { get; }

    public void Clear(int argb) => Array.Fill(Pixels, argb);

    public void SetPixel(int x, int y, int argb)
    {
        if ((uint)x < (uint)Width && (uint)y < (uint)Height) Pixels[y * Width + x] = argb;
    }

    public void FillRect(int x, int y, int w, int h, int argb)
    {
        int x0 = Math.Max(0, x), y0 = Math.Max(0, y);
        int x1 = Math.Min(Width, x + w), y1 = Math.Min(Height, y + h);
        for (int yy = y0; yy < y1; yy++)
        {
            int row = yy * Width;
            for (int xx = x0; xx < x1; xx++) Pixels[row + xx] = argb;
        }
    }

    /// <summary>Additively blends a colour (premultiplied by <paramref name="alpha"/>), used for glows.</summary>
    public void BlendRect(int x, int y, int w, int h, int argb, double alpha)
    {
        int x0 = Math.Max(0, x), y0 = Math.Max(0, y);
        int x1 = Math.Min(Width, x + w), y1 = Math.Min(Height, y + h);
        int r = (argb >> 16) & 0xFF, g = (argb >> 8) & 0xFF, b = argb & 0xFF;
        for (int yy = y0; yy < y1; yy++)
        {
            int row = yy * Width;
            for (int xx = x0; xx < x1; xx++)
            {
                int p = Pixels[row + xx];
                int pr = (p >> 16) & 0xFF, pg = (p >> 8) & 0xFF, pb = p & 0xFF;
                pr = (int)(pr + (r - pr) * alpha);
                pg = (int)(pg + (g - pg) * alpha);
                pb = (int)(pb + (b - pb) * alpha);
                Pixels[row + xx] = (255 << 24) | (pr << 16) | (pg << 8) | pb;
            }
        }
    }

    public void Present() =>
        Bitmap.WritePixels(new Int32Rect(0, 0, Width, Height), Pixels, Width * 4, 0);
}
