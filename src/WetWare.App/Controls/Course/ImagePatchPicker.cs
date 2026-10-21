using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WetWare.App.Controls.Course;

/// <summary>A grey-scale scene with the currently encoded patch framed; clicking picks a new patch location.</summary>
public sealed class ImagePatchPicker : FrameworkElement
{
    private PixelBuffer? _buffer;
    private float[]? _source;
    private int _size;

    public int PatchX { get; set; }
    public int PatchY { get; set; }
    public int PatchSize { get; set; } = 8;

    /// <summary>Raised with the top-left pixel of the clicked patch.</summary>
    public event EventHandler<(int X, int Y)>? PatchPicked;

    public void SetImage(float[] pixels, int size)
    {
        if (ReferenceEquals(pixels, _source) && _buffer is not null) return;
        _source = pixels;
        _size = size;
        _buffer = new PixelBuffer(size, size);
        double sd = Math.Sqrt(pixels.Average(v => (double)v * v));
        for (int i = 0; i < pixels.Length; i++)
        {
            double t = 0.5 + 0.18 * pixels[i] / Math.Max(sd, 1e-6);
            _buffer.Pixels[i] = ColorMaps.Gray(t);
        }
        _buffer.Present();
        InvalidateVisual();
    }

    public void Render() => InvalidateVisual();

    private Rect ImageRect
    {
        get
        {
            double s = Math.Min(ActualWidth, ActualHeight);
            return new Rect((ActualWidth - s) / 2, (ActualHeight - s) / 2, s, s);
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        if (_buffer is null) return;
        var rect = ImageRect;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        dc.DrawImage(_buffer.Bitmap, rect);
        double scale = rect.Width / _size;
        var frame = new Rect(rect.Left + PatchX * scale, rect.Top + PatchY * scale, PatchSize * scale, PatchSize * scale);
        frame.Inflate(1.5, 1.5);
        dc.DrawRectangle(null, DrawKit.Pen(DrawKit.Warning, 2.5), frame);
        var halo = frame;
        halo.Inflate(3, 3);
        dc.DrawRectangle(null, DrawKit.Pen(DrawKit.Warning, 3, 0.3), halo);
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (_buffer is null) return;
        var rect = ImageRect;
        var p = e.GetPosition(this);
        if (!rect.Contains(p)) return;
        double scale = rect.Width / _size;
        int x = Math.Clamp((int)((p.X - rect.Left) / scale) - PatchSize / 2, 0, _size - PatchSize);
        int y = Math.Clamp((int)((p.Y - rect.Top) / scale) - PatchSize / 2, 0, _size - PatchSize);
        PatchPicked?.Invoke(this, (x, y));
    }
}
