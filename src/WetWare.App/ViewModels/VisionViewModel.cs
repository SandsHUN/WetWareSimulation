using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WetWare.App.Services;
using WetWare.Core.Mathematics;
using WetWare.Core.Vision;

namespace WetWare.App.ViewModels;

/// <summary>Retina → V1 model: centre–surround ganglion cells, orientation-selective cortex and spike trains.</summary>
public sealed partial class VisionViewModel : PageViewModel
{
    public const int Size = StimulusLibrary.Size;
    private const double SlowMotion = 0.05;

    private readonly FrameClock _clock;
    private readonly RandomSource _random = new(21);
    private bool _dirty = true;
    private bool _isCustom;

    public VisionViewModel(FrameClock clock)
    {
        _clock = clock;
        Stimuli = Enum.GetValues<VisualStimulus>().Select(s => new LocalizedOption<VisualStimulus>(s, $"Vision.Stim.{s}")).ToList();
        _selectedStimulus = Stimuli[0];
        Input = StimulusLibrary.Create(VisualStimulus.HermannGrid);
        Result = VisualPathway.Process(Input, Parameters);
        OnGlow = new float[Size * Size];
        OffGlow = new float[Size * Size];
    }

    public override string Key => "Vision";
    public override string Glyph => "";

    public IReadOnlyList<LocalizedOption<VisualStimulus>> Stimuli { get; }

    public VisualPathwayParameters Parameters { get; } = new();

    public GrayImage Input { get; private set; }

    public VisualPathwayResult Result { get; private set; }

    /// <summary>Decaying spike "afterglow" of ON and OFF ganglion cells, for the spike view.</summary>
    public float[] OnGlow { get; }
    public float[] OffGlow { get; }

    public event EventHandler? ImagesUpdated;
    public event EventHandler? SpikesUpdated;

    [ObservableProperty] private LocalizedOption<VisualStimulus> _selectedStimulus;
    [ObservableProperty] private double _angle = 30;
    [ObservableProperty] private double _centerSigma = 1.0;
    [ObservableProperty] private double _surroundSigma = 3.0;
    [ObservableProperty] private double _surroundWeight = 1.0;
    [ObservableProperty] private double _wavelength = 8;
    [ObservableProperty] private bool _paintMode;
    [ObservableProperty] private bool _paintWhite = true;
    [ObservableProperty] private double _brushSize = 3;
    [ObservableProperty] private bool _spikesRunning = true;
    [ObservableProperty] private int _profileRow = Size / 2;
    [ObservableProperty] private string _explanation = "";
    [ObservableProperty] private string _spikeRateText = "";

    public bool AngleRelevant => !_isCustom && SelectedStimulus.Value is VisualStimulus.Bars or VisualStimulus.Grating;

    partial void OnSelectedStimulusChanged(LocalizedOption<VisualStimulus> value)
    {
        _isCustom = false;
        Input = StimulusLibrary.Create(value.Value, Angle);
        OnPropertyChanged(nameof(AngleRelevant));
        MarkDirty();
    }

    partial void OnAngleChanged(double value)
    {
        if (AngleRelevant) Input = StimulusLibrary.Create(SelectedStimulus.Value, value);
        MarkDirty();
    }

    partial void OnCenterSigmaChanged(double value)
    {
        Parameters.CenterSigma = value;
        if (SurroundSigma <= value) SurroundSigma = value + 0.5;
        MarkDirty();
    }

    partial void OnSurroundSigmaChanged(double value)
    {
        Parameters.SurroundSigma = Math.Max(value, CenterSigma + 0.25);
        MarkDirty();
    }

    partial void OnSurroundWeightChanged(double value)
    {
        Parameters.SurroundWeight = value;
        MarkDirty();
    }

    partial void OnWavelengthChanged(double value)
    {
        Parameters.GaborWavelength = value;
        MarkDirty();
    }

    partial void OnProfileRowChanged(int value) => ImagesUpdated?.Invoke(this, EventArgs.Empty);

    private void MarkDirty() => _dirty = true;

    protected override void OnActivated()
    {
        _clock.Tick += OnTick;
        _dirty = true;
    }

    protected override void OnDeactivated() => _clock.Tick -= OnTick;

    protected override void OnLanguageChanged() => UpdateExplanation();

    private void OnTick(object? sender, double realMs)
    {
        if (_dirty)
        {
            _dirty = false;
            Result = VisualPathway.Process(Input, Parameters);
            UpdateExplanation();
            ImagesUpdated?.Invoke(this, EventArgs.Empty);
        }

        if (!SpikesRunning) return;
        double decay = Math.Exp(-realMs / 70.0);
        for (int i = 0; i < OnGlow.Length; i++)
        {
            OnGlow[i] *= (float)decay;
            OffGlow[i] *= (float)decay;
        }
        // Simulate in slow motion (1/20 speed) so individual spikes stay visible.
        var (on, off) = VisualPathway.SampleSpikes(Result, Parameters, realMs * SlowMotion, _random);
        foreach (int i in on) OnGlow[i] = 1;
        foreach (int i in off) OffGlow[i] = 1;
        double seconds = realMs * SlowMotion / 1000.0;
        double perCell = seconds > 0 ? (on.Count + off.Count) / (2.0 * Size * Size) / seconds : 0;
        SpikeRateText = Loc.Format("Vision.SpikeRate", F(perCell, 1), on.Count + off.Count);
        SpikesUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateExplanation()
    {
        string key = _isCustom ? "Vision.Explain.Custom" : $"Vision.Explain.{SelectedStimulus.Value}";
        Explanation = Loc.Has(key) ? Loc[key] : Loc["Vision.Explain.Custom"];
    }

    /// <summary>Paints into the input image (custom stimulus).</summary>
    public void Paint(int x, int y, bool erase)
    {
        if (!_isCustom)
        {
            _isCustom = true;
            OnPropertyChanged(nameof(AngleRelevant));
        }
        float value = (PaintWhite ^ erase) ? 0.95f : 0.05f;
        int r = (int)BrushSize;
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (dx * dx + dy * dy > r * r) continue;
                int px = x + dx, py = y + dy;
                if (px >= 0 && py >= 0 && px < Input.Width && py < Input.Height) Input[px, py] = value;
            }
        MarkDirty();
    }

    [RelayCommand]
    private void ClearCanvas()
    {
        _isCustom = true;
        Input = new GrayImage(Size, Size);
        Array.Fill(Input.Data, 0.05f);
        PaintMode = true;
        OnPropertyChanged(nameof(AngleRelevant));
        MarkDirty();
    }

    [RelayCommand]
    private void LoadImage()
    {
        var dialog = new OpenFileDialog
        {
            Filter = Loc["Vision.ImageFilter"] + "|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff",
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(dialog.FileName);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            var gray = new FormatConvertedBitmap(bitmap, PixelFormats.Gray8, null, 0);
            int w = gray.PixelWidth, h = gray.PixelHeight;
            var bytes = new byte[w * h];
            gray.CopyPixels(bytes, w, 0);
            var full = new GrayImage(w, h, bytes.Select(b => b / 255f).ToArray());
            // Crop to a centred square, then downsample to the retina resolution.
            int side = Math.Min(w, h);
            var square = new GrayImage(side, side);
            int ox = (w - side) / 2, oy = (h - side) / 2;
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                    square[x, y] = full[ox + x, oy + y];
            Input = square.Resize(Size, Size);
            _isCustom = true;
            OnPropertyChanged(nameof(AngleRelevant));
            MarkDirty();
        }
        catch (Exception)
        {
            Explanation = Loc["Vision.LoadFailed"];
        }
    }

    [RelayCommand]
    private void ApplyPreset(string id)
    {
        switch (id)
        {
            case "Strong":
                SurroundWeight = 1.0;
                CenterSigma = 1.0;
                SurroundSigma = 3.0;
                break;
            case "None":
                SurroundWeight = 0.0;
                break;
            case "Wide":
                CenterSigma = 2.0;
                SurroundSigma = 6.0;
                SurroundWeight = 1.0;
                break;
        }
    }
}
