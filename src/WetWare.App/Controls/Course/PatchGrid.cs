using System.Windows;
using System.Windows.Media;

namespace WetWare.App.Controls.Course;

/// <summary>
/// Grid of small square image patches (dictionary basis functions, principal components, an image patch and
/// its reconstruction). Each patch is contrast-normalised on its own, mid-grey = 0. Optional per-patch
/// activity highlights the causes that are active in the current sparse code.
/// </summary>
public sealed class PatchGrid : FrameworkElement
{
    private IReadOnlyList<double[]> _patches = Array.Empty<double[]>();
    private PixelBuffer? _buffer;
    private int _columns = 1, _side = 1;

    public int Columns { get; set; } = 8;

    /// <summary>Gap between patches in source pixels.</summary>
    public int Gap { get; set; } = 1;

    /// <summary>Use one common contrast scale instead of per-patch normalisation (to compare magnitudes).</summary>
    public double? SharedScale { get; set; }

    /// <summary>Activity per patch (e.g. |v_k|); active patches get a coloured frame, inactive ones are dimmed.</summary>
    public double[]? Activity { get; set; }

    public Color Highlight { get; set; } = DrawKit.Accent;

    public void SetPatches(IReadOnlyList<double[]> patches)
    {
        _patches = patches;
        if (patches.Count == 0) return;
        _side = (int)Math.Round(Math.Sqrt(patches[0].Length));
        _columns = Math.Min(Columns, patches.Count);
        int rows = (patches.Count + _columns - 1) / _columns;
        int width = _columns * (_side + Gap) + Gap, height = rows * (_side + Gap) + Gap;
        if (_buffer is null || _buffer.Width != width || _buffer.Height != height) _buffer = new PixelBuffer(width, height);
        _buffer.Clear(ColorMaps.Pack(255, 0x0A, 0x10, 0x20));
        for (int k = 0; k < patches.Count; k++)
        {
            var patch = patches[k];
            double scale = SharedScale ?? Math.Max(1e-9, patch.Max(Math.Abs));
            bool dim = Activity is not null && k < Activity.Length && Activity[k] == 0;
            int ox = Gap + k % _columns * (_side + Gap), oy = Gap + k / _columns * (_side + Gap);
            for (int r = 0; r < _side; r++)
            {
                for (int c = 0; c < _side; c++)
                {
                    double t = 0.5 + 0.5 * Math.Clamp(patch[r * _side + c] / scale, -1, 1);
                    if (dim) t = 0.5 + (t - 0.5) * 0.35;
                    byte g = (byte)(t * 255);
                    _buffer.Pixels[(oy + r) * _buffer.Width + ox + c] = ColorMaps.Pack(255, g, g, g);
                }
            }
        }
        _buffer.Present();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        if (_buffer is null) return;
        double scale = Math.Min(ActualWidth / _buffer.Width, ActualHeight / _buffer.Height);
        var rect = new Rect((ActualWidth - _buffer.Width * scale) / 2, (ActualHeight - _buffer.Height * scale) / 2,
            _buffer.Width * scale, _buffer.Height * scale);
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        dc.DrawImage(_buffer.Bitmap, rect);

        if (Activity is null) return;
        double max = Activity.Length == 0 ? 1 : Math.Max(1e-9, Activity.Max(Math.Abs));
        for (int k = 0; k < Math.Min(Activity.Length, _patches.Count); k++)
        {
            if (Activity[k] == 0) continue;
            double ox = rect.Left + (Gap + k % _columns * (_side + Gap)) * scale, oy = rect.Top + (Gap + k / _columns * (_side + Gap)) * scale;
            var cell = new Rect(ox - 1, oy - 1, _side * scale + 2, _side * scale + 2);
            dc.DrawRectangle(null, DrawKit.Pen(Highlight, 1 + 2 * Math.Abs(Activity[k]) / max, 0.4 + 0.6 * Math.Abs(Activity[k]) / max), cell);
        }
    }
}
