using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WetWare.App.Controls;

/// <summary>Horizontal band of rows (e.g. a neuron group or a coding scheme) shaded behind the spikes.</summary>
public sealed record RasterBand(int StartRow, int RowCount, Color Color, string Label);

/// <summary>
/// Scrolling spike raster (rows × time) rendered into a WriteableBitmap so thousands of spikes per frame stay cheap.
/// </summary>
public sealed class RasterPlotControl : FrameworkElement
{
    private const double LeftMargin = 38;
    private const double BottomMargin = 20;
    private const double TopMargin = 4;

    private readonly List<(double T, int Row, int Color)> _spikes = new();
    private readonly List<double> _markers = new();
    private PixelBuffer? _buffer;
    private double _now;
    private int _hoverRow = -1;
    private static readonly Typeface LabelFace = new("Segoe UI");
    private static readonly Brush LabelBrush = Frozen(Color.FromRgb(0x5F, 0x6C, 0x8E));
    private static readonly Pen GridPen = FrozenPen(Color.FromArgb(0x30, 0x5F, 0x6C, 0x8E), 1);

    public static readonly DependencyProperty RowCountProperty =
        DependencyProperty.Register(nameof(RowCount), typeof(int), typeof(RasterPlotControl), new PropertyMetadata(100));

    public static readonly DependencyProperty WindowMsProperty =
        DependencyProperty.Register(nameof(WindowMs), typeof(double), typeof(RasterPlotControl), new PropertyMetadata(2000.0));

    public int RowCount { get => (int)GetValue(RowCountProperty); set => SetValue(RowCountProperty, value); }

    public double WindowMs { get => (double)GetValue(WindowMsProperty); set => SetValue(WindowMsProperty, value); }

    /// <summary>When set the time axis is fixed at [FixedStart, FixedStart + WindowMs] instead of scrolling.</summary>
    public double? FixedStart { get; set; }

    public IReadOnlyList<RasterBand> Bands { get; set; } = Array.Empty<RasterBand>();

    /// <summary>Optional continuous curve (value in [−1, 1]) drawn inside a band, e.g. the theta rhythm.</summary>
    public Func<double, double>? Overlay { get; set; }

    public RasterBand? OverlayBand { get; set; }

    public int SelectedRow { get; set; } = -1;

    public Func<int, string>? RowLabel { get; set; }

    /// <summary>Formats hover text for a row.</summary>
    public Func<int, string>? RowTooltip { get; set; }

    /// <summary>Spike pixel width.</summary>
    public int SpikeWidth { get; set; } = 2;

    public double Now => _now;

    public event EventHandler<int>? RowClicked;

    public void AddSpike(double timeMs, int row, int argb) => _spikes.Add((timeMs, row, argb));

    public void AddMarker(double timeMs) => _markers.Add(timeMs);

    public void Clear()
    {
        _spikes.Clear();
        _markers.Clear();
        _now = 0;
        Refresh(0);
    }

    /// <summary>Advances the visible window to <paramref name="nowMs"/>, drops old spikes and redraws.</summary>
    public void Refresh(double nowMs)
    {
        _now = nowMs;
        double start = FixedStart ?? nowMs - WindowMs;
        if (FixedStart is null)
        {
            _spikes.RemoveAll(s => s.T < start - 50);
            _markers.RemoveAll(m => m < start - 50);
        }
        Redraw(start);
        InvalidateVisual();
    }

    private Rect PlotRect => new(LeftMargin, TopMargin, Math.Max(1, ActualWidth - LeftMargin - 4), Math.Max(1, ActualHeight - TopMargin - BottomMargin));

    private void Redraw(double start)
    {
        var rect = PlotRect;
        var dpi = VisualTreeHelper.GetDpi(this);
        int w = (int)Math.Max(1, rect.Width * dpi.DpiScaleX);
        int h = (int)Math.Max(1, rect.Height * dpi.DpiScaleY);
        if (_buffer is null || _buffer.Width != w || _buffer.Height != h) _buffer = new PixelBuffer(w, h);

        int rows = Math.Max(1, RowCount);
        double rowH = (double)h / rows;
        _buffer.Clear(ColorMaps.Pack(255, 0x08, 0x0D, 0x1A));

        foreach (var band in Bands)
        {
            int y0 = (int)(band.StartRow * rowH);
            int y1 = (int)((band.StartRow + band.RowCount) * rowH);
            _buffer.BlendRect(0, y0, w, y1 - y0, ColorMaps.Pack(band.Color), 0.10);
        }
        if (SelectedRow >= 0 && SelectedRow < rows)
            _buffer.BlendRect(0, (int)(SelectedRow * rowH), w, Math.Max(1, (int)rowH), ColorMaps.Pack(255, 0x22, 0xD3, 0xEE), 0.18);
        if (_hoverRow >= 0 && _hoverRow < rows)
            _buffer.BlendRect(0, (int)(_hoverRow * rowH), w, Math.Max(1, (int)rowH), ColorMaps.Pack(255, 255, 255, 255), 0.06);

        double window = WindowMs;
        foreach (double m in _markers)
        {
            int x = (int)((m - start) / window * w);
            _buffer.BlendRect(x, 0, 1, h, ColorMaps.Pack(255, 0xFB, 0xBF, 0x24), 0.35);
        }

        if (Overlay is not null && OverlayBand is { } ob)
        {
            double top = ob.StartRow * rowH, height = ob.RowCount * rowH;
            int overlayColor = ColorMaps.Pack(ob.Color);
            for (int x = 0; x < w; x++)
            {
                double t = start + x * window / w;
                double v = Math.Clamp(Overlay(t), -1, 1);
                int y = (int)(top + height * (0.5 - 0.45 * v));
                _buffer.BlendRect(x, y, 1, 2, overlayColor, 0.45);
            }
        }

        int spikeH = Math.Max(1, (int)Math.Floor(rowH) - (rowH > 3 ? 1 : 0));
        int sw = Math.Max(1, (int)(SpikeWidth * dpi.DpiScaleX));
        foreach (var (t, row, color) in _spikes)
        {
            if (t < start || t > start + window || row < 0 || row >= rows) continue;
            int x = (int)((t - start) / window * w);
            int y = (int)(row * rowH);
            _buffer.FillRect(x, y, sw, spikeH, color);
        }
        _buffer.Present();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var rect = PlotRect;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (_buffer is not null) dc.DrawImage(_buffer.Bitmap, rect);
        dc.DrawRectangle(null, GridPen, rect);

        double dpiScale = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        int rows = Math.Max(1, RowCount);
        double rowH = rect.Height / rows;

        // Row labels: band names, or a few neuron indices.
        if (Bands.Count > 0)
        {
            foreach (var band in Bands)
            {
                var text = Text(band.Label, 10.5, new SolidColorBrush(band.Color), dpiScale);
                double y = rect.Top + (band.StartRow + band.RowCount / 2.0) * rowH - text.Height / 2;
                dc.PushTransform(new RotateTransform(-90, 12, y + text.Height / 2));
                dc.DrawText(text, new Point(12 - text.Width / 2, y));
                dc.Pop();
            }
        }
        else
        {
            int step = rows >= 50 ? 25 : rows >= 20 ? 10 : 5;
            for (int r = 0; r < rows; r += step)
            {
                var text = Text(RowLabel?.Invoke(r) ?? r.ToString(CultureInfo.InvariantCulture), 10, LabelBrush, dpiScale);
                dc.DrawText(text, new Point(LeftMargin - text.Width - 6, rect.Top + r * rowH + rowH / 2 - text.Height / 2));
            }
        }

        // Time axis.
        double start = FixedStart ?? _now - WindowMs;
        double tickStep = WindowMs >= 1500 ? 500 : WindowMs >= 600 ? 200 : WindowMs >= 200 ? 50 : 25;
        double firstTick = Math.Ceiling(start / tickStep) * tickStep;
        for (double t = firstTick; t <= start + WindowMs + 1e-6; t += tickStep)
        {
            double x = rect.Left + (t - start) / WindowMs * rect.Width;
            dc.DrawLine(GridPen, new Point(x, rect.Bottom), new Point(x, rect.Bottom + 4));
            string label = FixedStart is null
                ? (t / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " s"
                : t.ToString("0", CultureInfo.InvariantCulture) + " ms";
            var text = Text(label, 10, LabelBrush, dpiScale);
            dc.DrawText(text, new Point(x - text.Width / 2, rect.Bottom + 4));
        }

        if (_hoverRow >= 0 && RowTooltip is not null)
        {
            var text = Text(RowTooltip(_hoverRow), 11.5, Brushes.White, dpiScale);
            double y = rect.Top + _hoverRow * rowH - text.Height - 6;
            if (y < rect.Top) y = rect.Top + (_hoverRow + 1) * rowH + 4;
            var box = new Rect(rect.Left + 8, y, text.Width + 14, text.Height + 6);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0xEE, 0x18, 0x25, 0x40)), GridPen, box, 6, 6);
            dc.DrawText(text, new Point(box.Left + 7, box.Top + 3));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int row = RowAt(e.GetPosition(this));
        if (row != _hoverRow)
        {
            _hoverRow = row;
            Refresh(_now);
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverRow = -1;
        Refresh(_now);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        int row = RowAt(e.GetPosition(this));
        if (row >= 0) RowClicked?.Invoke(this, row);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        Refresh(_now);
    }

    private int RowAt(Point p)
    {
        var rect = PlotRect;
        if (!rect.Contains(p)) return -1;
        return Math.Clamp((int)((p.Y - rect.Top) / rect.Height * RowCount), 0, RowCount - 1);
    }

    private static FormattedText Text(string s, double size, Brush brush, double dpi) =>
        new(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, LabelFace, size, brush, dpi);

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static Pen FrozenPen(Color c, double thickness)
    {
        var p = new Pen(Frozen(c), thickness);
        p.Freeze();
        return p;
    }
}
