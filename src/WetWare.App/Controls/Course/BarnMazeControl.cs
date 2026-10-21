using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WetWare.Core.Course.Reinforcement;

namespace WetWare.App.Controls.Course;

/// <summary>
/// The "rat in a barn" decision tree: entrance A, junctions B and C, four food sites. Node glow = state value V(s),
/// branch width = policy probability, the rat walks the path of the last trial and each move flashes its TD error.
/// </summary>
public sealed class BarnMazeControl : FrameworkElement
{
    public BarnMaze? Maze { get; set; }

    /// <summary>0–1 progress of the rat along the last path.</summary>
    public double Progress { get; set; } = 1;

    public string ValueLabel { get; set; } = "V";

    public void Render() => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        if (Maze is null || ActualWidth < 200 || ActualHeight < 160) return;
        double w = ActualWidth, h = ActualHeight, dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        var a = new Point(w / 2, h - 40);
        var b = new Point(w * 0.28, h * 0.5);
        var c = new Point(w * 0.72, h * 0.5);
        Point[] leaves = [new(w * 0.14, 46), new(w * 0.40, 46), new(w * 0.60, 46), new(w * 0.86, 46)];
        Point Node(BarnState s) => s switch { BarnState.A => a, BarnState.B => b, _ => c };
        Point Leaf(BarnState s, int action) => leaves[(s == BarnState.B ? 0 : 2) + action];

        double vMax = Math.Max(1, Maze.Rewards.Cast<double>().Max());

        void Branch(Point from, Point to, double probability, int action)
        {
            var color = action == 0 ? DrawKit.Accent : DrawKit.Pink;
            dc.DrawLine(DrawKit.Pen(DrawKit.Axis, 14, 0.5), from, to);
            dc.DrawLine(DrawKit.Pen(color, 2 + 10 * probability, 0.25 + 0.75 * probability), from, to);
            var mid = new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2);
            DrawKit.DrawText(dc, $"{(action == 0 ? "L" : "R")} {probability.ToString("0.00", CultureInfo.CurrentUICulture)}",
                new Point(mid.X + (action == 0 ? -14 : 14), mid.Y), 11, color, dpi, action == 0 ? HorizontalAlignment.Right : HorizontalAlignment.Left);
        }

        double pA = Maze.ProbabilityRight(BarnState.A), pB = Maze.ProbabilityRight(BarnState.B), pC = Maze.ProbabilityRight(BarnState.C);
        Branch(a, b, 1 - pA, 0);
        Branch(a, c, pA, 1);
        Branch(b, leaves[0], 1 - pB, 0);
        Branch(b, leaves[1], pB, 1);
        Branch(c, leaves[2], 1 - pC, 0);
        Branch(c, leaves[3], pC, 1);

        // Food sites: reward size as a glowing disc.
        for (int k = 0; k < 4; k++)
        {
            double r = Maze.Rewards[k < 2 ? 1 : 2, k % 2];
            double radius = 10 + 14 * r / vMax;
            if (r > 0) DrawKit.Glow(dc, leaves[k], radius * 0.55, DrawKit.Warning, 0.4 + 0.6 * r / vMax);
            else dc.DrawEllipse(DrawKit.Brush(DrawKit.Panel), DrawKit.Pen(DrawKit.Axis, 1.5), leaves[k], 10, 10);
            DrawKit.DrawText(dc, $"r = {r.ToString("0.#", CultureInfo.CurrentUICulture)}", new Point(leaves[k].X, leaves[k].Y - 30), 12,
                r > 0 ? DrawKit.Warning : DrawKit.Muted, dpi, HorizontalAlignment.Center, DrawKit.SemiBold);
        }

        // Rat path and TD error flashes.
        var path = Maze.LastPath;
        int moves = path.Count;
        double flash = Math.Clamp(1 - Progress, 0, 1);
        foreach (var state in new[] { BarnState.A, BarnState.B, BarnState.C })
        {
            var p = Node(state);
            double v = Maze.Value(state);
            var fill = DrawKit.Lerp(Color.FromRgb(0x15, 0x20, 0x36), DrawKit.Success, Math.Clamp(v / vMax, 0, 1));
            dc.DrawEllipse(DrawKit.Brush(fill), DrawKit.Pen(DrawKit.Text, 1.6, 0.7), p, 26, 26);
            DrawKit.DrawText(dc, state.ToString(), new Point(p.X, p.Y - 7), 15, DrawKit.Text, dpi, HorizontalAlignment.Center, DrawKit.SemiBold);
            DrawKit.DrawText(dc, $"{ValueLabel} = {v.ToString("0.00", CultureInfo.CurrentUICulture)}", new Point(p.X, p.Y + 10), 10.5, DrawKit.Text, dpi, HorizontalAlignment.Center);

            var move = path.FirstOrDefault(m => m.From == state);
            if (move is not null && Math.Abs(move.Delta) > 0.01 && flash > 0)
            {
                var color = move.Delta > 0 ? DrawKit.Success : DrawKit.Danger;
                dc.DrawEllipse(null, DrawKit.Pen(color, 3, flash), p, 30 + 10 * (1 - flash), 30 + 10 * (1 - flash));
                DrawKit.DrawText(dc, $"δ = {move.Delta.ToString("+0.00;−0.00", CultureInfo.CurrentUICulture)}", new Point(p.X + 34, p.Y + 22), 11.5, color, dpi);
            }
        }

        if (moves > 0)
        {
            // Two segments: A → junction, junction → food. Progress runs through both.
            var points = new List<Point> { a };
            foreach (var m in path) points.Add(m.To is { } to ? Node(to) : Leaf(m.From, m.Action));
            double t = Math.Clamp(Progress, 0, 1) * (points.Count - 1);
            int seg = Math.Min((int)t, points.Count - 2);
            double f = t - seg;
            var rat = new Point(points[seg].X + (points[seg + 1].X - points[seg].X) * f, points[seg].Y + (points[seg + 1].Y - points[seg].Y) * f);
            DrawRat(dc, rat);
        }
        else
        {
            // Waiting at the entrance, beside the node so its value stays readable.
            DrawRat(dc, new Point(a.X + 52, a.Y + 4));
        }
    }

    private static void DrawRat(DrawingContext dc, Point p)
    {
        var body = DrawKit.Brush(Color.FromRgb(0xC8, 0xCF, 0xE0));
        dc.DrawEllipse(DrawKit.Brush(Color.FromArgb(0x60, 0, 0, 0)), null, new Point(p.X + 2, p.Y + 4), 13, 8);
        dc.DrawEllipse(body, null, p, 12, 8);
        dc.DrawEllipse(body, null, new Point(p.X + 11, p.Y - 3), 6, 5);
        dc.DrawEllipse(DrawKit.Brush(DrawKit.Pink), null, new Point(p.X + 9, p.Y - 8), 2.6, 2.6);
        dc.DrawEllipse(DrawKit.Brush(Color.FromRgb(0x05, 0x0A, 0x14)), null, new Point(p.X + 14, p.Y - 4), 1.3, 1.3);
        var tail = new StreamGeometry();
        using (var ctx = tail.Open())
        {
            ctx.BeginFigure(new Point(p.X - 11, p.Y + 1), false, false);
            ctx.BezierTo(new Point(p.X - 20, p.Y + 8), new Point(p.X - 24, p.Y - 6), new Point(p.X - 30, p.Y + 2), true, true);
        }
        tail.Freeze();
        dc.DrawGeometry(null, DrawKit.Pen(DrawKit.Pink, 1.6, 0.8), tail);
    }
}
