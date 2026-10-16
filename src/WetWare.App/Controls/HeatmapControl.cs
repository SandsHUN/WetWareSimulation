using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WetWare.App.Controls;

/// <summary>Rectangle highlighting a block of the matrix (e.g. the A→B projection of the Pavlov experiment).</summary>
public sealed record HeatmapBlock(int RowStart, int RowEnd, int ColStart, int ColEnd, Color Color, string Label);

/// <summary>
/// Pre × post synaptic weight matrix. NaN = no synapse, negative = inhibitory synapse, [0, 1] = excitatory weight.
/// </summary>
public sealed class HeatmapControl : FrameworkElement
{
    private const double LeftMargin = 30;
    private const double BottomMargin = 20;

    private double[,] _matrix = new double[1, 1];
    private PixelBuffer? _buffer;
    private (int Row, int Col) _hover = (-1, -1);
    private static readonly Typeface Face = new("Segoe UI");
    private static readonly Brush Faint = new SolidColorBrush(Color.FromRgb(0x5F, 0x6C, 0x8E));
    private static readonly Pen Border = new(new SolidColorBrush(Color.FromArgb(0x40, 0x5F, 0x6C, 0x8E)), 1);

    public IReadOnlyList<HeatmapBlock> Blocks { get; set; } = Array.Empty<HeatmapBlock>();

    /// <summary>Localized axis captions.</summary>
    public string RowCaption { get; set; } = "pre";
    public string ColumnCaption { get; set; } = "post";

    /// <summary>Formats the hover read-out for (row, col, value).</summary>
    public Func<int, int, double, string>? HoverText { get; set; }

    public int Highlight { get; set; } = -1;

    public void SetMatrix(double[,] matrix)
    {
        _matrix = matrix;
        Redraw();
        InvalidateVisual();
    }

    private Rect PlotRect
    {
        get
        {
            double size = Math.Max(1, Math.Min(ActualWidth - LeftMargin - 4, ActualHeight - BottomMargin - 4));
            return new Rect(LeftMargin, 2, size, size);
        }
    }

    private void Redraw()
    {
        int rows = _matrix.GetLength(0), cols = _matrix.GetLength(1);
        if (_buffer is null || _buffer.Width != cols || _buffer.Height != rows) _buffer = new PixelBuffer(cols, rows);
        int empty = ColorMaps.Pack(255, 0x08, 0x0D, 0x1A);
        int inhibitory = ColorMaps.Pack(255, 0x1E, 0x3A, 0x5F);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                double v = _matrix[r, c];
                int color = double.IsNaN(v) ? empty : v < 0 ? inhibitory : ColorMaps.Weight(v);
                if (Highlight >= 0 && (r == Highlight || c == Highlight) && double.IsNaN(v))
                    color = ColorMaps.Pack(255, 0x0E, 0x1A, 0x2E);
                _buffer.Pixels[r * cols + c] = color;
            }
        }
        _buffer.Present();
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        var rect = PlotRect;
        if (_buffer is not null)
        {
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            dc.DrawImage(_buffer.Bitmap, rect);
        }
        dc.DrawRectangle(null, Border, rect);

        int rows = _matrix.GetLength(0), cols = _matrix.GetLength(1);
        double cw = rect.Width / Math.Max(1, cols), ch = rect.Height / Math.Max(1, rows);
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        foreach (var block in Blocks)
        {
            var r = new Rect(rect.Left + block.ColStart * cw, rect.Top + block.RowStart * ch,
                (block.ColEnd - block.ColStart + 1) * cw, (block.RowEnd - block.RowStart + 1) * ch);
            dc.DrawRectangle(null, new Pen(new SolidColorBrush(block.Color), 1.5) { DashStyle = DashStyles.Dash }, r);
            var label = new FormattedText(block.Label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Face, 10,
                new SolidColorBrush(block.Color), dpi);
            dc.DrawText(label, new Point(r.Right + 3, r.Top));
        }

        if (Highlight >= 0 && Highlight < rows)
        {
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(0x90, 0x22, 0xD3, 0xEE)), 1);
            dc.DrawRectangle(null, pen, new Rect(rect.Left, rect.Top + Highlight * ch, rect.Width, ch));
            dc.DrawRectangle(null, pen, new Rect(rect.Left + Highlight * cw, rect.Top, cw, rect.Height));
        }

        var rowCaption = new FormattedText(RowCaption + " ↓", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Face, 10.5, Faint, dpi);
        dc.PushTransform(new RotateTransform(-90, 10, rect.Top + rect.Height / 2));
        dc.DrawText(rowCaption, new Point(10 - rowCaption.Width / 2, rect.Top + rect.Height / 2 - rowCaption.Height / 2));
        dc.Pop();
        var colCaption = new FormattedText(ColumnCaption + " →", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Face, 10.5, Faint, dpi);
        dc.DrawText(colCaption, new Point(rect.Left + rect.Width / 2 - colCaption.Width / 2, rect.Bottom + 3));

        if (_hover.Row >= 0 && HoverText is not null)
        {
            double v = _matrix[_hover.Row, _hover.Col];
            var text = new FormattedText(HoverText(_hover.Row, _hover.Col, v), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Face, 11.5, Brushes.White, dpi);
            double x = Math.Min(rect.Left + (_hover.Col + 1) * cw + 8, ActualWidth - text.Width - 16);
            double y = Math.Min(rect.Top + _hover.Row * ch + 8, rect.Bottom - text.Height - 8);
            var box = new Rect(x, y, text.Width + 14, text.Height + 6);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0xEE, 0x18, 0x25, 0x40)), Border, box, 6, 6);
            dc.DrawText(text, new Point(box.Left + 7, box.Top + 3));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var rect = PlotRect;
        var p = e.GetPosition(this);
        int rows = _matrix.GetLength(0), cols = _matrix.GetLength(1);
        _hover = rect.Contains(p)
            ? (Math.Clamp((int)((p.Y - rect.Top) / rect.Height * rows), 0, rows - 1), Math.Clamp((int)((p.X - rect.Left) / rect.Width * cols), 0, cols - 1))
            : (-1, -1);
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = (-1, -1);
        InvalidateVisual();
    }
}
