using ScottPlot;
using ScottPlot.WPF;

namespace WetWare.App.Controls;

/// <summary>Applies the lab's dark scientific look to ScottPlot charts.</summary>
public static class LabPlot
{
    public static readonly Color Background = Color.FromHex("#101829");
    public static readonly Color DataBackground = Color.FromHex("#0A1020");
    public static readonly Color AxisColor = Color.FromHex("#6C7899");
    public static readonly Color GridColor = Color.FromHex("#1B2742");
    public static readonly Color Accent = Color.FromHex("#22D3EE");
    public static readonly Color Accent2 = Color.FromHex("#A78BFA");
    public static readonly Color Excitatory = Color.FromHex("#FB7185");
    public static readonly Color Inhibitory = Color.FromHex("#38BDF8");
    public static readonly Color Success = Color.FromHex("#34D399");
    public static readonly Color Warning = Color.FromHex("#FBBF24");
    public static readonly Color Danger = Color.FromHex("#F43F5E");
    public static readonly Color Muted = Color.FromHex("#97A3C2");

    public static void Apply(WpfPlot control)
    {
        var plot = control.Plot;
        plot.FigureBackground.Color = Background;
        plot.DataBackground.Color = DataBackground;
        plot.Axes.Color(AxisColor);
        plot.Grid.MajorLineColor = GridColor;
        plot.Axes.Frame(false);
        plot.Font.Set("Segoe UI");
        foreach (var axis in plot.Axes.GetAxes())
        {
            axis.Label.FontSize = 12;
            axis.Label.Bold = false;
            axis.TickLabelStyle.FontSize = 11;
        }
        plot.Legend.BackgroundColor = Color.FromHex("#152036");
        plot.Legend.FontColor = Color.FromHex("#E8EDF8");
        plot.Legend.OutlineColor = Color.FromHex("#2C3D63");
        control.UserInputProcessor.Disable();
    }

    public static void Labels(WpfPlot control, string x, string y)
    {
        control.Plot.Axes.Bottom.Label.Text = x;
        control.Plot.Axes.Left.Label.Text = y;
    }
}
