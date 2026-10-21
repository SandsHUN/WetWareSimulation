using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WetWare.App.Controls.Course;

/// <summary>
/// Colour-coded matrix (weight matrices, trial × time prediction errors). Values are mapped to [0, 1] by
/// <see cref="Normalize"/> and coloured by <see cref="ColorMap"/>. Hovering highlights a row and reports it.
/// </summary>
public sealed class MatrixView : FrameworkElement
{
    private double[,] _matrix = new double[1, 1];
    private PixelBuffer? _buffer;

    public Func<double, int> ColorMap { get; set; } = ColorMaps.Diverging;

    /// <summary>Maps a raw value to [0, 1] for the colour map (default: symmetric around 0 with <see cref="Range"/>).</summary>
    public Func<double, double>? Normalize { get; set; }

    public double Range { get; set; } = 1;

    public bool SquareCells { get; set; }

    public int HighlightRow { get; set; } = -1;

    /// <summary>Optional label strip at the bottom.</summary>
    public string? Caption { get; set; }

    public string? RowCaption { get; set; }

    public event EventHandler<(int Row, int Col)>? HoverChanged;

    public void SetMatrix(double[,] matrix)
    {
        _matrix = matrix;
        int rows = matrix.GetLength(0), cols = matrix.GetLength(1);
        if (_buffer is null || _buffer.Width != cols || _buffer.Height != rows) _buffer = new PixelBuffer(cols, rows);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                double v = matrix[r, c];
                double t = Normalize?.Invoke(v) ?? 0.5 + 0.5 * v / Math.Max(Range, 1e-12);
                _buffer.Pixels[r * cols + c] = double.IsNaN(v) ? ColorMaps.Pack(255, 8, 13, 26) : ColorMap(t);
            }
        }
        _buffer.Present();
        InvalidateVisual();
    }

    private Rect PlotRect
    {
        get
        {
            double left = RowCaption is null ? 2 : 18, bottom = Caption is null ? 2 : 18;
            double w = Math.Max(1, ActualWidth - left - 2), h = Math.Max(1, ActualHeight - bottom - 2);
            if (SquareCells)
            {
                int rows = _matrix.GetLength(0), cols = _matrix.GetLength(1);
                double cell = Math.Min(w / cols, h / rows);
                w = cell * cols;
                h = cell * rows;
            }
            return new Rect(left, 2, w, h);
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        var rect = PlotRect;
        if (_buffer is not null)
        {
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            dc.DrawImage(_buffer.Bitmap, rect);
        }
        dc.DrawRectangle(null, DrawKit.Pen(DrawKit.Axis, 1), rect);
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        int rowsCount = _matrix.GetLength(0);
        if (HighlightRow >= 0 && HighlightRow < rowsCount)
        {
            double ch = rect.Height / rowsCount;
            dc.DrawRectangle(null, DrawKit.Pen(DrawKit.Warning, 1.5), new Rect(rect.Left, rect.Top + HighlightRow * ch, rect.Width, ch));
        }
        if (Caption is not null)
            DrawKit.DrawText(dc, Caption, new Point(rect.Left + rect.Width / 2, rect.Bottom + 9), 10.5, DrawKit.Faint, dpi, HorizontalAlignment.Center);
        if (RowCaption is not null)
        {
            dc.PushTransform(new RotateTransform(-90, 8, rect.Top + rect.Height / 2));
            DrawKit.DrawText(dc, RowCaption, new Point(8, rect.Top + rect.Height / 2), 10.5, DrawKit.Faint, dpi, HorizontalAlignment.Center);
            dc.Pop();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var rect = PlotRect;
        var p = e.GetPosition(this);
        int rows = _matrix.GetLength(0), cols = _matrix.GetLength(1);
        var cell = rect.Contains(p)
            ? (Math.Clamp((int)((p.Y - rect.Top) / rect.Height * rows), 0, rows - 1), Math.Clamp((int)((p.X - rect.Left) / rect.Width * cols), 0, cols - 1))
            : (-1, -1);
        HoverChanged?.Invoke(this, cell);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        HoverChanged?.Invoke(this, (-1, -1));
    }
}
