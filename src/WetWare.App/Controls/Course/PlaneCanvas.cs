using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WetWare.App.Controls.Course;

/// <summary>
/// A square-ish 2-D "world" with grid and axes; the owning view paints its content through <see cref="Painter"/>
/// in world coordinates (via <see cref="ToScreen(double, double)"/>). Clicks are reported in world coordinates.
/// Used for the data clouds of module 7, the perceptron plane and the E–I phase plane.
/// </summary>
public sealed class PlaneCanvas : FrameworkElement
{
    private Point? _hover;

    public double XMin { get; set; } = -3;
    public double XMax { get; set; } = 3;
    public double YMin { get; set; } = -3;
    public double YMax { get; set; } = 3;

    /// <summary>Keep one world unit equally long on both axes.</summary>
    public bool EqualAspect { get; set; } = true;

    public string XLabel { get; set; } = "x₁";
    public string YLabel { get; set; } = "x₂";

    public bool ShowAxes { get; set; } = true;

    public double GridStep { get; set; } = 1;

    public Action<DrawingContext, PlaneCanvas>? Painter { get; set; }

    /// <summary>Optional text drawn next to the cursor, from the world position under it.</summary>
    public Func<double, double, string?>? HoverText { get; set; }

    public event EventHandler<(double X, double Y, MouseButton Button)>? WorldClick;

    public double Dpi => VisualTreeHelper.GetDpi(this).PixelsPerDip;

    /// <summary>Screen rectangle of the plotting area.</summary>
    public Rect Area { get; private set; }

    public void Render() => InvalidateVisual();

    public Point ToScreen(double x, double y) =>
        new(Area.Left + (x - XMin) / (XMax - XMin) * Area.Width, Area.Bottom - (y - YMin) / (YMax - YMin) * Area.Height);

    public (double X, double Y) ToWorld(Point p) =>
        (XMin + (p.X - Area.Left) / Area.Width * (XMax - XMin), YMin + (Area.Bottom - p.Y) / Area.Height * (YMax - YMin));

    /// <summary>Screen length of one world unit along x.</summary>
    public double Scale => Area.Width / (XMax - XMin);

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        double w = ActualWidth, h = ActualHeight;
        if (w < 40 || h < 40) return;

        const double left = 30, bottom = 22, top = 6, right = 8;
        double aw = w - left - right, ah = h - top - bottom;
        if (EqualAspect)
        {
            double ratio = (XMax - XMin) / (YMax - YMin);
            if (aw / ah > ratio) aw = ah * ratio;
            else ah = aw / ratio;
        }
        Area = new Rect(left + (w - left - right - aw) / 2, top, aw, ah);

        dc.DrawRoundedRectangle(DrawKit.Brush(DrawKit.Panel), null, Area, 6, 6);
        double dpi = Dpi;
        var gridPen = DrawKit.Pen(DrawKit.Grid, 1);
        for (double gx = Math.Ceiling(XMin / GridStep) * GridStep; gx <= XMax + 1e-9; gx += GridStep)
        {
            var p = ToScreen(gx, YMin);
            dc.DrawLine(gridPen, new Point(p.X, Area.Top), new Point(p.X, Area.Bottom));
            DrawKit.DrawText(dc, gx.ToString("0.##", CultureInfo.CurrentUICulture), new Point(p.X, Area.Bottom + 9), 9.5, DrawKit.Faint, dpi, HorizontalAlignment.Center);
        }
        for (double gy = Math.Ceiling(YMin / GridStep) * GridStep; gy <= YMax + 1e-9; gy += GridStep)
        {
            var p = ToScreen(XMin, gy);
            dc.DrawLine(gridPen, new Point(Area.Left, p.Y), new Point(Area.Right, p.Y));
            DrawKit.DrawText(dc, gy.ToString("0.##", CultureInfo.CurrentUICulture), new Point(Area.Left - 4, p.Y), 9.5, DrawKit.Faint, dpi, HorizontalAlignment.Right);
        }
        if (ShowAxes)
        {
            var axisPen = DrawKit.Pen(DrawKit.Axis, 1.4);
            if (XMin < 0 && XMax > 0) dc.DrawLine(axisPen, ToScreen(0, YMin), ToScreen(0, YMax));
            if (YMin < 0 && YMax > 0) dc.DrawLine(axisPen, ToScreen(XMin, 0), ToScreen(XMax, 0));
        }
        DrawKit.DrawText(dc, XLabel, new Point(Area.Right - 4, Area.Bottom - 10), 11, DrawKit.Muted, dpi, HorizontalAlignment.Right, DrawKit.MathFace);
        DrawKit.DrawText(dc, YLabel, new Point(Area.Left + 6, Area.Top + 10), 11, DrawKit.Muted, dpi, HorizontalAlignment.Left, DrawKit.MathFace);

        dc.PushClip(new RectangleGeometry(Area, 6, 6));
        Painter?.Invoke(dc, this);
        dc.Pop();

        if (_hover is { } hp && HoverText is not null && Area.Contains(hp))
        {
            var (x, y) = ToWorld(hp);
            string? text = HoverText(x, y);
            if (!string.IsNullOrEmpty(text))
            {
                var ft = DrawKit.Label(text, 11.5, DrawKit.Text, dpi);
                double bx = Math.Min(hp.X + 14, Area.Right - ft.Width - 14), by = Math.Min(hp.Y + 12, Area.Bottom - ft.Height - 8);
                var box = new Rect(bx, by, ft.Width + 14, ft.Height + 8);
                dc.DrawRoundedRectangle(DrawKit.Brush(Color.FromRgb(0x18, 0x25, 0x40), 0.94), DrawKit.Pen(DrawKit.Axis, 1), box, 6, 6);
                dc.DrawText(ft, new Point(box.Left + 7, box.Top + 4));
            }
        }
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        var p = e.GetPosition(this);
        if (!Area.Contains(p)) return;
        var (x, y) = ToWorld(p);
        WorldClick?.Invoke(this, (x, y, e.ChangedButton));
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (HoverText is null) return;
        _hover = e.GetPosition(this);
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = null;
        InvalidateVisual();
    }
}
