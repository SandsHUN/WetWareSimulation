using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ScottPlot;
using WetWare.App.Controls;
using WetWare.App.Localization;
using WetWare.App.ViewModels;
using WetWare.Core.Vision;

namespace WetWare.App.Views;

public partial class VisionView : UserControl
{
    private VisionViewModel? _vm;
    private ImageViewControl[] _stageViews = [];
    private int[] _spikePixels = [];

    public VisionView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = DataContext as VisionViewModel;
        if (_vm is null) return;
        LabPlot.Apply(ProfilePlot);
        _stageViews = [InputView, OnView, OffView, ContrastView, OrientationView, SpikeView];
        foreach (var view in _stageViews)
        {
            view.PixelClicked += OnPixelClicked;
            view.HoverText = HoverText;
        }
        InputView.Painted += OnPainted;
        _vm.ImagesUpdated += OnImagesUpdated;
        _vm.SpikesUpdated += OnSpikesUpdated;
        _vm.PropertyChanged += OnVmPropertyChanged;
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        BuildOrientationLegend();
        InputView.AllowPaint = _vm.PaintMode;
        OnImagesUpdated(null, EventArgs.Empty);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        foreach (var view in _stageViews) view.PixelClicked -= OnPixelClicked;
        InputView.Painted -= OnPainted;
        _vm.ImagesUpdated -= OnImagesUpdated;
        _vm.SpikesUpdated -= OnSpikesUpdated;
        _vm.PropertyChanged -= OnVmPropertyChanged;
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => OnImagesUpdated(null, EventArgs.Empty);

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VisionViewModel.PaintMode)) InputView.AllowPaint = _vm!.PaintMode;
    }

    private void OnPainted(object? sender, (int X, int Y, bool Erase) e) => _vm?.Paint(e.X, e.Y, e.Erase);

    private void OnPixelClicked(object? sender, (int X, int Y) e)
    {
        if (_vm is not null) _vm.ProfileRow = e.Y;
    }

    private string HoverText(int x, int y)
    {
        if (_vm is null) return string.Empty;
        var r = _vm.Result;
        var loc = LocalizationService.Instance;
        int i = y * r.Input.Width + x;
        string orientation = VisualPathwayResult.OrientationAngles[r.DominantOrientation[i]].ToString("0", loc.Culture);
        return loc.Format("Vision.Hover", x, y, r.Input.Data[i].ToString("F2", loc.Culture),
            r.CenterSurround.Data[i].ToString("+0.000;−0.000", loc.Culture), orientation);
    }

    private void BuildOrientationLegend()
    {
        OrientationLegend.Children.Clear();
        foreach (double angle in VisualPathwayResult.OrientationAngles)
        {
            var chip = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(6, 0, 6, 0) };
            var bar = new Rectangle
            {
                Width = 16,
                Height = 4,
                RadiusX = 2,
                RadiusY = 2,
                Fill = new SolidColorBrush(ColorMaps.OrientationColor(angle)),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(-angle),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
            };
            chip.Children.Add(bar);
            chip.Children.Add(new TextBlock
            {
                Text = $"{angle:0}°",
                Margin = new Thickness(5, 0, 0, 0),
                Style = (Style)FindResource("Caption"),
            });
            OrientationLegend.Children.Add(chip);
        }
    }

    private void OnImagesUpdated(object? sender, EventArgs e)
    {
        if (_vm is null) return;
        var r = _vm.Result;
        int row = _vm.ProfileRow;
        foreach (var view in _stageViews) view.ProfileRow = row;

        InputView.SetImage(r.Input, ColorMaps.Gray);
        float onMax = Math.Max(1e-3f, r.OnCells.Range().Max);
        float offMax = Math.Max(1e-3f, r.OffCells.Range().Max);
        float shared = Math.Max(onMax, offMax);
        OnView.SetImage(r.OnCells, ColorMaps.On, 0, shared);
        OffView.SetImage(r.OffCells, ColorMaps.Off, 0, shared);
        ContrastView.SetImage(r.CenterSurround, ColorMaps.Diverging, -shared, shared);

        // Orientation map: hue = preferred orientation, brightness = selectivity.
        int n = r.Input.Data.Length;
        float strengthMax = Math.Max(1e-3f, r.EdgeStrength.Range().Max);
        var pixels = new int[n];
        for (int i = 0; i < n; i++)
        {
            double angle = VisualPathwayResult.OrientationAngles[r.DominantOrientation[i]];
            pixels[i] = ColorMaps.Orientation(angle, Math.Pow(r.EdgeStrength.Data[i] / strengthMax, 0.7));
        }
        OrientationView.SetPixels(pixels, r.Input.Width, r.Input.Height);

        DrawKernels();
        DrawProfile();
    }

    private void DrawKernels()
    {
        var p = _vm!.Parameters;
        ShowKernel(DogKernel, Kernels.DifferenceOfGaussians(p.CenterSigma, p.SurroundSigma, p.SurroundWeight));
        var gabors = new[] { Gabor0, Gabor45, Gabor90, Gabor135 };
        for (int k = 0; k < 4; k++)
        {
            double theta = (VisualPathwayResult.OrientationAngles[k] + 90) * Math.PI / 180.0;
            ShowKernel(gabors[k], Kernels.Gabor(theta, p.GaborWavelength, p.GaborWavelength * 0.45));
        }
    }

    private static void ShowKernel(ImageViewControl view, float[,] kernel)
    {
        int h = kernel.GetLength(0), w = kernel.GetLength(1);
        var img = new GrayImage(w, h);
        float max = 1e-6f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                img[x, y] = kernel[y, x];
                max = Math.Max(max, Math.Abs(kernel[y, x]));
            }
        view.SetImage(img, ColorMaps.Diverging, -max, max);
    }

    private void DrawProfile()
    {
        var r = _vm!.Result;
        var loc = LocalizationService.Instance;
        int row = Math.Clamp(_vm.ProfileRow, 0, r.Input.Height - 1);
        int w = r.Input.Width;
        double[] xs = Enumerable.Range(0, w).Select(x => (double)x).ToArray();
        double[] input = xs.Select(x => (double)r.Input[(int)x, row]).ToArray();
        double[] response = xs.Select(x => (double)r.CenterSurround[(int)x, row]).ToArray();
        double scale = Math.Max(1e-3, response.Max(Math.Abs));

        var plot = ProfilePlot.Plot;
        plot.Clear();
        var a = plot.Add.ScatterLine(xs, input, LabPlot.Muted);
        a.LineWidth = 2;
        a.LegendText = loc["Vision.Profile.Input"];
        var b = plot.Add.ScatterLine(xs, response.Select(v => 0.5 + 0.5 * v / scale).ToArray(), LabPlot.Accent);
        b.LineWidth = 2;
        b.LegendText = loc["Vision.Profile.Response"];
        plot.Add.HorizontalLine(0.5, 1, LabPlot.Accent.WithAlpha(0.3), LinePattern.Dotted);
        LabPlot.Labels(ProfilePlot, loc.Format("Vision.Profile.Axis", row), loc["Vision.Profile.Value"]);
        plot.Axes.SetLimits(0, w - 1, -0.05, 1.05);
        plot.ShowLegend(Alignment.LowerRight);
        ProfilePlot.Refresh();
    }

    private void OnSpikesUpdated(object? sender, EventArgs e)
    {
        if (_vm is null) return;
        var input = _vm.Result.Input.Data;
        int n = input.Length;
        if (_spikePixels.Length != n) _spikePixels = new int[n];
        var on = _vm.OnGlow;
        var off = _vm.OffGlow;
        for (int i = 0; i < n; i++)
        {
            double baseV = 0.10 * input[i];
            double r = baseV + 0.95 * off[i] + 0.2 * on[i];
            double g = baseV + 0.85 * on[i] + 0.1 * off[i];
            double b = baseV + 0.55 * on[i] + 0.6 * off[i];
            _spikePixels[i] = ColorMaps.Pack(255, (byte)(Math.Min(1, r) * 255), (byte)(Math.Min(1, g) * 255), (byte)(Math.Min(1, b) * 255));
        }
        SpikeView.SetPixels(_spikePixels, _vm.Result.Input.Width, _vm.Result.Input.Height);
    }
}
