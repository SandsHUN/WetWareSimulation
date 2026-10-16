using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScottPlot;
using WetWare.App.Controls;
using WetWare.App.Localization;
using WetWare.App.ViewModels;
using WetWare.Core.Recognition;

namespace WetWare.App.Views;

public partial class RecognitionView : UserControl
{
    private RecognitionViewModel? _vm;
    private readonly List<(ImageViewControl View, TextBlock Label, Border Frame)> _fields = new();
    private readonly int _inputColor = ColorMaps.Pack(ColorMaps.Parse("#FBBF24"));
    private readonly int _outputColor = ColorMaps.Pack(ColorMaps.Parse("#A78BFA"));
    private int _drawnSpikes;

    public RecognitionView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = DataContext as RecognitionViewModel;
        if (_vm is null) return;
        LabPlot.Apply(AccuracyPlot);
        BuildFields();
        ConfigureRaster();
        PadView.Painted += OnPainted;
        _vm.PadChanged += OnPadChanged;
        _vm.WeightsChanged += OnWeightsChanged;
        _vm.PresentationStep += OnPresentationStep;
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        OnPadChanged(null, EventArgs.Empty);
        OnWeightsChanged(null, EventArgs.Empty);
        OnPresentationStep(null, EventArgs.Empty);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        PadView.Painted -= OnPainted;
        _vm.PadChanged -= OnPadChanged;
        _vm.WeightsChanged -= OnWeightsChanged;
        _vm.PresentationStep -= OnPresentationStep;
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        ConfigureRaster();
        DrawAccuracy();
        OnPresentationStep(null, EventArgs.Empty);
    }

    private void ConfigureRaster()
    {
        var loc = LocalizationService.Instance;
        Raster.FixedStart = 0;
        Raster.Bands =
        [
            new RasterBand(0, DigitPatterns.PixelCount, ColorMaps.Parse("#FBBF24"), loc["Rec.Band.Input"]),
            new RasterBand(DigitPatterns.PixelCount, 20, ColorMaps.Parse("#A78BFA"), loc["Rec.Band.Output"]),
        ];
        Raster.RowTooltip = row => row < DigitPatterns.PixelCount
            ? loc.Format("Rec.Hover.Input", row % DigitPatterns.Width, row / DigitPatterns.Width)
            : loc.Format("Rec.Hover.Output", row - DigitPatterns.PixelCount, _vm?.Classifier.Labels[row - DigitPatterns.PixelCount] is int l && l >= 0 ? l.ToString() : "–");
    }

    private void BuildFields()
    {
        FieldsGrid.Children.Clear();
        _fields.Clear();
        for (int j = 0; j < 20; j++)
        {
            var view = new ImageViewControl { Height = 62 };
            var label = new TextBlock
            {
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Foreground = (Brush)FindResource("TextBrush"),
            };
            var panel = new StackPanel();
            panel.Children.Add(view);
            panel.Children.Add(label);
            var frame = new Border
            {
                Margin = new Thickness(3),
                Padding = new Thickness(4),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = (Brush)FindResource("BorderBrush"),
                Background = (Brush)FindResource("Bg1Brush"),
                Child = panel,
                ToolTip = LocalizationService.Instance.Format("Rec.Field.Tip", j),
            };
            FieldsGrid.Children.Add(frame);
            _fields.Add((view, label, frame));
        }
    }

    private void OnPainted(object? sender, (int X, int Y, bool Erase) e) => _vm?.PaintPixel(e.X, e.Y, e.Erase);

    private void OnPadChanged(object? sender, EventArgs e)
    {
        if (_vm is null) return;
        var pixels = _vm.Pad.Select(v => v > 0.5 ? ColorMaps.Pack(255, 0x22, 0xD3, 0xEE) : ColorMaps.Pack(255, 0x0B, 0x11, 0x20)).ToArray();
        PadView.SetPixels(pixels, DigitPatterns.Width, DigitPatterns.Height);
    }

    private void OnWeightsChanged(object? sender, EventArgs e)
    {
        if (_vm is null) return;
        var c = _vm.Classifier;
        for (int j = 0; j < _fields.Count && j < c.OutputCount; j++)
        {
            double max = 1e-6;
            for (int i = 0; i < c.InputCount; i++) max = Math.Max(max, c.Weights[j, i]);
            var pixels = new int[c.InputCount];
            for (int i = 0; i < c.InputCount; i++) pixels[i] = ColorMaps.Weight(c.Weights[j, i] / max);
            _fields[j].View.SetPixels(pixels, DigitPatterns.Width, DigitPatterns.Height);
            _fields[j].Label.Text = c.Labels[j] < 0 ? "–" : $"→ {c.Labels[j]}";
        }
        DrawAccuracy();
    }

    private void OnPresentationStep(object? sender, EventArgs e)
    {
        if (_vm is null) return;
        var spikes = _vm.PresentationSpikes;
        if (spikes.Count < _drawnSpikes)
        {
            Raster.Clear();
            _drawnSpikes = 0;
        }
        for (int k = _drawnSpikes; k < spikes.Count; k++)
        {
            var (t, row) = spikes[k];
            Raster.AddSpike(t, row, row < DigitPatterns.PixelCount ? _inputColor : _outputColor);
        }
        _drawnSpikes = spikes.Count;
        Raster.Refresh(_vm.Classifier.TimeMs);

        int winner = _vm.Outputs.FirstOrDefault(o => o.IsWinner)?.Index ?? -1;
        for (int j = 0; j < _fields.Count; j++)
        {
            _fields[j].Frame.BorderBrush = (Brush)FindResource(j == winner ? "AccentBrush" : "BorderBrush");
            _fields[j].Frame.BorderThickness = new Thickness(j == winner ? 2 : 1);
        }
    }

    private void DrawAccuracy()
    {
        if (_vm is null) return;
        var loc = LocalizationService.Instance;
        var plot = AccuracyPlot.Plot;
        plot.Clear();
        var history = _vm.AccuracyHistory;
        if (history.Count > 0)
        {
            var xs = history.Select(h => (double)h.Samples).Prepend(0).ToArray();
            var ys = history.Select(h => h.Accuracy * 100).Prepend(10).ToArray();
            var line = plot.Add.ScatterLine(xs, ys, LabPlot.Success);
            line.LineWidth = 2;
            line.MarkerSize = 5;
        }
        var chance = plot.Add.HorizontalLine(10, 1, LabPlot.Muted, LinePattern.Dotted);
        chance.LegendText = loc["Rec.Chance"];
        plot.ShowLegend(Alignment.LowerRight);
        LabPlot.Labels(AccuracyPlot, loc["Rec.Samples"], loc["Rec.AccuracyAxis"]);
        double maxX = history.Count > 0 ? history[^1].Samples : 500;
        plot.Axes.SetLimits(0, Math.Max(500, maxX), 0, 100);
        AccuracyPlot.Refresh();
    }
}
