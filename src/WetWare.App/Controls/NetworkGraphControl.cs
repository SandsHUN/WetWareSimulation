using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WetWare.Core.Network;
using WetWare.Core.Scenarios;

namespace WetWare.App.Controls;

/// <summary>
/// Interactive small-world graph: neurons on a ring, synapses as curved chords. Firing neurons flash like a
/// calcium-imaging recording; spikes of the selected neuron travel along its axons.
/// </summary>
public sealed class NetworkGraphControl : FrameworkElement
{
    private const double PulseDurationMs = 380;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<double> _pulses = new();
    private Point[] _positions = [];
    private ImageSource? _edgeLayer;
    private int _edgeCount = -1;
    private Size _layoutSize;
    private int _hover = -1;

    private static readonly Typeface Face = new("Segoe UI");
    private static readonly Pen EdgePen = FrozenPen(Color.FromArgb(0x16, 0x93, 0xA3, 0xC2), 0.7);
    private static readonly Pen HoverPen = FrozenPen(Color.FromArgb(0xFF, 0xE8, 0xED, 0xF8), 1.5);
    private static readonly Pen SelectedPen = FrozenPen(Color.FromArgb(0xFF, 0x22, 0xD3, 0xEE), 2);
    private static readonly Pen IncomingPen = FrozenPen(Color.FromArgb(0x55, 0xA7, 0x8B, 0xFA), 0.9);
    private static readonly Brush ExcBrush = Frozen(Color.FromRgb(0xFB, 0x71, 0x85));
    private static readonly Brush InhBrush = Frozen(Color.FromRgb(0x38, 0xBD, 0xF8));
    private static readonly Brush DeadBrush = Frozen(Color.FromRgb(0x33, 0x3D, 0x55));
    private const int GlowLevels = 16;
    private static readonly Brush[] ExcGlow = GlowRamp(Color.FromRgb(0xFF, 0xB4, 0x9E));
    private static readonly Brush[] InhGlow = GlowRamp(Color.FromRgb(0x9B, 0xE7, 0xFF));
    private static readonly Brush PulseBrush = Frozen(Color.FromRgb(0xFE, 0xF0, 0x8A));

    public SpikingNetwork? Network { get; set; }

    public IReadOnlyList<NeuronGroup> Groups { get; set; } = Array.Empty<NeuronGroup>();

    public int SelectedNeuron { get; set; } = -1;

    /// <summary>Show weight-coloured axons of the selected neuron.</summary>
    public bool ShowSelectedEdges { get; set; } = true;

    public Func<int, string>? HoverText { get; set; }

    /// <summary>Raised when a neuron is clicked (neuron index, true = right button).</summary>
    public event EventHandler<(int Neuron, bool Right)>? NeuronClicked;

    /// <summary>Starts a travelling pulse along the selected neuron's axons.</summary>
    public void NotifySelectedSpike() => _pulses.Add(_clock.Elapsed.TotalMilliseconds);

    public void InvalidateTopology() => _edgeCount = -1;

    public void Render() => InvalidateVisual();

    private void EnsureLayout()
    {
        var net = Network;
        if (net is null) return;
        var size = RenderSize;
        if (_positions.Length == net.Count && size == _layoutSize && _edgeCount == net.SynapseCount) return;

        _layoutSize = size;
        _edgeCount = net.SynapseCount;
        int n = net.Count;
        _positions = new Point[n];
        var c = new Point(size.Width / 2, size.Height / 2);
        double r = Math.Max(10, Math.Min(size.Width, size.Height) / 2 - 18);
        for (int i = 0; i < n; i++)
        {
            double a = -Math.PI / 2 + 2 * Math.PI * i / n;
            _positions[i] = new Point(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a));
        }

        // The synapse layer only changes with the topology, so it is rendered once into a bitmap.
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (int s = 0; s < net.SynapseCount; s++)
            {
                var a = _positions[net.SynapsePre[s]];
                var b = _positions[net.SynapsePost[s]];
                ctx.BeginFigure(a, false, false);
                ctx.QuadraticBezierTo(ControlPoint(a, b), b, true, false);
            }
        }
        geometry.Freeze();
        var dpi = VisualTreeHelper.GetDpi(this);
        int pw = Math.Max(1, (int)(size.Width * dpi.DpiScaleX)), ph = Math.Max(1, (int)(size.Height * dpi.DpiScaleY));
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) dc.DrawGeometry(null, EdgePen, geometry);
        var bitmap = new RenderTargetBitmap(pw, ph, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        _edgeLayer = bitmap;
    }

    private Point ControlPoint(Point a, Point b)
    {
        var c = new Point(_layoutSize.Width / 2, _layoutSize.Height / 2);
        var mid = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        return new Point(mid.X + (c.X - mid.X) * 0.55, mid.Y + (c.Y - mid.Y) * 0.55);
    }

    private static Point Bezier(Point a, Point ctrl, Point b, double t)
    {
        double u = 1 - t;
        return new Point(u * u * a.X + 2 * u * t * ctrl.X + t * t * b.X, u * u * a.Y + 2 * u * t * ctrl.Y + t * t * b.Y);
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        var net = Network;
        if (net is null || RenderSize.Width < 20) return;
        EnsureLayout();
        if (_edgeLayer is not null) dc.DrawImage(_edgeLayer, new Rect(_layoutSize));

        int sel = SelectedNeuron;
        if (ShowSelectedEdges && sel >= 0 && sel < net.Count)
        {
            foreach (int s in net.Incoming(sel))
            {
                var a = _positions[net.SynapsePre[s]];
                var b = _positions[sel];
                DrawCurve(dc, a, b, IncomingPen);
            }
            foreach (int s in net.Outgoing(sel))
            {
                double w = net.SynapseWeight[s];
                var color = net.IsInhibitory(sel) ? Color.FromRgb(0x38, 0xBD, 0xF8) : ColorMaps.ToColor(ColorMaps.Weight(w));
                var pen = new Pen(new SolidColorBrush(color), 0.6 + 2.6 * w);
                DrawCurve(dc, _positions[sel], _positions[net.SynapsePost[s]], pen);
            }

            // Travelling action potentials along the selected neuron's axons.
            double now = _clock.Elapsed.TotalMilliseconds;
            _pulses.RemoveAll(p => now - p > PulseDurationMs);
            foreach (double start in _pulses)
            {
                double t = (now - start) / PulseDurationMs;
                foreach (int s in net.Outgoing(sel))
                {
                    var a = _positions[sel];
                    var b = _positions[net.SynapsePost[s]];
                    var p = Bezier(a, ControlPoint(a, b), b, t);
                    dc.DrawEllipse(PulseBrush, null, p, 2.6, 2.6);
                }
            }
        }

        var groupOf = new Dictionary<int, Color>();
        foreach (var g in Groups)
        {
            var color = ColorMaps.Parse(g.ColorHex);
            foreach (int i in g.Neurons) groupOf[i] = color;
        }

        for (int i = 0; i < net.Count; i++)
        {
            var p = _positions[i];
            bool dead = net.IsAblated(i);
            bool inh = net.IsInhibitory(i);
            double act = dead ? 0 : net.GetActivity(i);
            if (act > 0.03)
            {
                int level = Math.Clamp((int)(act * 1.15 * GlowLevels), 1, GlowLevels) - 1;
                double gr = 6 + 16 * act;
                dc.DrawEllipse(inh ? InhGlow[level] : ExcGlow[level], null, p, gr, gr);
            }
            if (groupOf.TryGetValue(i, out var gc))
                dc.DrawEllipse(null, new Pen(new SolidColorBrush(gc), 2), p, 8.5, 8.5);

            double r = i == sel ? 6.5 : 4.6;
            dc.DrawEllipse(dead ? DeadBrush : inh ? InhBrush : ExcBrush, i == sel ? SelectedPen : i == _hover ? HoverPen : null, p, r, r);
            if (dead)
            {
                var xPen = new Pen(Brushes.IndianRed, 1.4);
                dc.DrawLine(xPen, new Point(p.X - 4, p.Y - 4), new Point(p.X + 4, p.Y + 4));
                dc.DrawLine(xPen, new Point(p.X - 4, p.Y + 4), new Point(p.X + 4, p.Y - 4));
            }
        }

        if (_hover >= 0 && HoverText is not null)
        {
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var text = new FormattedText(HoverText(_hover), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Face, 11.5, Brushes.White, dpi);
            var p = _positions[_hover];
            double x = Math.Clamp(p.X + 12, 4, Math.Max(4, RenderSize.Width - text.Width - 20));
            double y = Math.Clamp(p.Y - text.Height - 12, 4, Math.Max(4, RenderSize.Height - text.Height - 12));
            var box = new Rect(x, y, text.Width + 14, text.Height + 8);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0xF0, 0x18, 0x25, 0x40)), IncomingPen, box, 7, 7);
            dc.DrawText(text, new Point(box.Left + 7, box.Top + 4));
        }
    }

    private void DrawCurve(DrawingContext dc, Point a, Point b, Pen pen)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(a, false, false);
            ctx.QuadraticBezierTo(ControlPoint(a, b), b, true, false);
        }
        dc.DrawGeometry(null, pen, g);
    }

    private int HitTestNeuron(Point p)
    {
        int best = -1;
        double bestD = 12 * 12;
        for (int i = 0; i < _positions.Length; i++)
        {
            double dx = _positions[i].X - p.X, dy = _positions[i].Y - p.Y;
            double d = dx * dx + dy * dy;
            if (d < bestD)
            {
                bestD = d;
                best = i;
            }
        }
        return best;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int hit = HitTestNeuron(e.GetPosition(this));
        if (hit != _hover)
        {
            _hover = hit;
            Cursor = hit >= 0 ? Cursors.Hand : null;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        InvalidateVisual();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        int hit = HitTestNeuron(e.GetPosition(this));
        if (hit >= 0) NeuronClicked?.Invoke(this, (hit, e.ChangedButton == MouseButton.Right));
    }

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static Pen FrozenPen(Color c, double t)
    {
        var p = new Pen(Frozen(c), t);
        p.Freeze();
        return p;
    }

    /// <summary>Pre-built glow brushes for discrete intensity levels (avoids costly opacity layers).</summary>
    private static Brush[] GlowRamp(Color c)
    {
        var ramp = new Brush[GlowLevels];
        for (int i = 0; i < GlowLevels; i++)
        {
            double k = (i + 1.0) / GlowLevels;
            var b = new RadialGradientBrush();
            b.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(0xF0 * k), c.R, c.G, c.B), 0));
            b.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(0x60 * k), c.R, c.G, c.B), 0.35));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, c.R, c.G, c.B), 1));
            b.Freeze();
            ramp[i] = b;
        }
        return ramp;
    }
}
