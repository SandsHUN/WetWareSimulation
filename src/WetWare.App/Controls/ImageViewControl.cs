using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WetWare.Core.Vision;

namespace WetWare.App.Controls;

/// <summary>
/// Pixel-exact image display (nearest-neighbour, aspect preserved) with hover read-out,
/// an optional profile line and mouse painting.
/// </summary>
public sealed class ImageViewControl : FrameworkElement
{
    private PixelBuffer? _buffer;
    private (int X, int Y) _hover = (-1, -1);
    private bool _painting;
    private static readonly Typeface Face = new("Segoe UI");
    private static readonly Pen FramePen = new(new SolidColorBrush(Color.FromArgb(0x50, 0x5F, 0x6C, 0x8E)), 1);
    private static readonly Pen ProfilePen = new(new SolidColorBrush(Color.FromArgb(0xC0, 0xFB, 0xBF, 0x24)), 1) { DashStyle = DashStyles.Dash };
    private static readonly Pen CrossPen = new(new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF)), 1);

    public int ProfileRow { get; set; } = -1;

    public bool AllowPaint { get; set; }

    public bool ShowGrid { get; set; }

    public Func<int, int, string>? HoverText { get; set; }

    /// <summary>Raised while painting: pixel coordinates and whether the right button (erase) is used.</summary>
    public event EventHandler<(int X, int Y, bool Erase)>? Painted;

    /// <summary>Raised on a simple click (used to choose the profile row).</summary>
    public event EventHandler<(int X, int Y)>? PixelClicked;

    public int ImageWidth => _buffer?.Width ?? 0;
    public int ImageHeight => _buffer?.Height ?? 0;

    public void SetPixels(int[] argb, int width, int height)
    {
        if (_buffer is null || _buffer.Width != width || _buffer.Height != height) _buffer = new PixelBuffer(width, height);
        Array.Copy(argb, _buffer.Pixels, Math.Min(argb.Length, _buffer.Pixels.Length));
        _buffer.Present();
        InvalidateVisual();
    }

    /// <summary>Maps an image through a colour map; values are normalised with [min, max].</summary>
    public void SetImage(GrayImage image, Func<double, int> map, double min = 0, double max = 1)
    {
        var pixels = new int[image.Data.Length];
        double range = Math.Max(1e-9, max - min);
        for (int i = 0; i < pixels.Length; i++) pixels[i] = map((image.Data[i] - min) / range);
        SetPixels(pixels, image.Width, image.Height);
    }

    private Rect ImageRect
    {
        get
        {
            if (_buffer is null) return Rect.Empty;
            double scale = Math.Min(ActualWidth / _buffer.Width, ActualHeight / _buffer.Height);
            double w = _buffer.Width * scale, h = _buffer.Height * scale;
            return new Rect((ActualWidth - w) / 2, (ActualHeight - h) / 2, w, h);
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        if (_buffer is null) return;
        var rect = ImageRect;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        dc.DrawImage(_buffer.Bitmap, rect);
        dc.DrawRectangle(null, FramePen, rect);

        double cw = rect.Width / _buffer.Width, ch = rect.Height / _buffer.Height;
        if (ShowGrid)
        {
            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(0x30, 0x93, 0xA3, 0xC2)), 1);
            for (int x = 1; x < _buffer.Width; x++) dc.DrawLine(gridPen, new Point(rect.Left + x * cw, rect.Top), new Point(rect.Left + x * cw, rect.Bottom));
            for (int y = 1; y < _buffer.Height; y++) dc.DrawLine(gridPen, new Point(rect.Left, rect.Top + y * ch), new Point(rect.Right, rect.Top + y * ch));
        }

        if (ProfileRow >= 0 && ProfileRow < _buffer.Height)
        {
            double y = rect.Top + (ProfileRow + 0.5) * ch;
            dc.DrawLine(ProfilePen, new Point(rect.Left, y), new Point(rect.Right, y));
        }

        if (_hover.X >= 0)
        {
            var cell = new Rect(rect.Left + _hover.X * cw, rect.Top + _hover.Y * ch, cw, ch);
            dc.DrawRectangle(null, CrossPen, cell);
            if (HoverText is not null)
            {
                double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                var text = new FormattedText(HoverText(_hover.X, _hover.Y), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Face, 11, Brushes.White, dpi);
                var box = new Rect(rect.Left + 4, rect.Bottom - text.Height - 10, text.Width + 12, text.Height + 6);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0xE0, 0x10, 0x18, 0x29)), null, box, 5, 5);
                dc.DrawText(text, new Point(box.Left + 6, box.Top + 3));
            }
        }
    }

    private (int X, int Y) PixelAt(Point p)
    {
        var rect = ImageRect;
        if (_buffer is null || !rect.Contains(p)) return (-1, -1);
        return (Math.Clamp((int)((p.X - rect.Left) / rect.Width * _buffer.Width), 0, _buffer.Width - 1),
                Math.Clamp((int)((p.Y - rect.Top) / rect.Height * _buffer.Height), 0, _buffer.Height - 1));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var px = PixelAt(e.GetPosition(this));
        if (px != _hover)
        {
            _hover = px;
            InvalidateVisual();
        }
        if (_painting && px.X >= 0)
            Painted?.Invoke(this, (px.X, px.Y, e.RightButton == MouseButtonState.Pressed));
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = (-1, -1);
        InvalidateVisual();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        var px = PixelAt(e.GetPosition(this));
        if (px.X < 0) return;
        if (AllowPaint)
        {
            _painting = true;
            CaptureMouse();
            Painted?.Invoke(this, (px.X, px.Y, e.ChangedButton == MouseButton.Right));
        }
        else
        {
            PixelClicked?.Invoke(this, px);
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        _painting = false;
        ReleaseMouseCapture();
    }
}
