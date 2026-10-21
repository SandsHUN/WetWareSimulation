using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WetWare.Core.Course.Unsupervised;
using WetWare.Core.Mathematics;

namespace WetWare.App.ViewModels.Course.Lessons;

/// <summary>7.3 – generative models: prior, likelihood, posterior; Gaussian mixtures fitted by EM (soft assignment).</summary>
public sealed partial class MixtureLesson : LessonViewModel
{
    private RandomSource _random = new(61);
    private double _clock;

    public MixtureLesson() : base("M7.Em", "7.3") => RefreshTexts();

    public GaussianMixtureEm Model { get; } = new();

    /// <summary>"Dreamed" samples drawn from the current model.</summary>
    public List<(double X, double Y, int K)> Dreams { get; } = new();

    /// <summary>Point under the mouse for the Bayes panel.</summary>
    public (double X, double Y)? Probe { get; private set; }

    [ObservableProperty] private string _layout = nameof(ClusterLayout.Overlapping);
    [ObservableProperty] private double _count = 2;
    [ObservableProperty] private bool _isRunning = true;
    [ObservableProperty] private string _phase = "";
    [ObservableProperty] private string _readout = "";
    [ObservableProperty] private string _probeText = "";

    partial void OnLayoutChanged(string value)
    {
        Model.SetData(ClusterData.Generate(Enum.Parse<ClusterLayout>(value), seed: 12, count: 300));
        Dreams.Clear();
        RefreshTexts();
        RaiseUpdated();
    }

    partial void OnCountChanged(double value)
    {
        Model.SetCount((int)Math.Round(value));
        Dreams.Clear();
        RefreshTexts();
        RaiseUpdated();
    }

    public override void Tick(double realMs)
    {
        if (!IsRunning) return;
        _clock += realMs;
        if (_clock < 350) return;
        _clock = 0;
        Model.HalfStep();
        if (Model.Iterations >= 60) IsRunning = false;
        RefreshTexts();
        RaiseUpdated();
    }

    [RelayCommand]
    private void ToggleRun() => IsRunning = !IsRunning;

    [RelayCommand]
    private void HalfStep()
    {
        Model.HalfStep();
        RefreshTexts();
        RaiseUpdated();
    }

    [RelayCommand]
    private void Iterate()
    {
        Model.Iterate();
        RefreshTexts();
        RaiseUpdated();
    }

    [RelayCommand]
    private void Reset()
    {
        Model.Reset(Environment.TickCount);
        Dreams.Clear();
        IsRunning = false;
        RefreshTexts();
        RaiseUpdated();
    }

    /// <summary>Samples 150 points from the model: k ~ π (prior), then u ~ N(μ_k, Σ_k) (likelihood).</summary>
    [RelayCommand]
    private void Dream()
    {
        Dreams.Clear();
        _random = new RandomSource(Environment.TickCount);
        for (int s = 0; s < 150; s++)
        {
            double r = _random.NextDouble(), acc = 0;
            int k = 0;
            for (; k < Model.Components.Count - 1; k++)
            {
                acc += Model.Components[k].Pi;
                if (r < acc) break;
            }
            var c = Model.Components[k];
            // Cholesky factor of Σ.
            double l11 = Math.Sqrt(c.Sxx), l21 = c.Sxy / l11, l22 = Math.Sqrt(Math.Max(1e-9, c.Syy - l21 * l21));
            double z1 = _random.NextGaussian(), z2 = _random.NextGaussian();
            Dreams.Add((c.Mx + l11 * z1, c.My + l21 * z1 + l22 * z2, k));
        }
        RaiseUpdated();
    }

    [RelayCommand]
    private void ClearDreams()
    {
        Dreams.Clear();
        RaiseUpdated();
    }

    public void SetProbe(double x, double y)
    {
        Probe = (x, y);
        RefreshTexts();
        RaiseUpdated();
    }

    protected override void OnLanguageChanged() => RefreshTexts();

    private void RefreshTexts()
    {
        Phase = Model.AwaitingMStep ? Loc["M7.Em.Phase.AfterE"] : Loc["M7.Em.Phase.AfterM"];
        Readout = Loc.Format("M7.Em.Readout", Model.Iterations, F(Model.LogLikelihoodHistory.LastOrDefault(), 3));
        if (Probe is { } p)
        {
            var post = Model.Posterior(p.X, p.Y);
            ProbeText = Loc.Format("M7.Em.Probe", F(p.X, 2), F(p.Y, 2)) + "  " +
                        string.Join("  ", post.Select((v, k) => $"p({k + 1}|u) = {F(v, 2)}"));
        }
        else
        {
            ProbeText = Loc["M7.Em.Probe.Hint"];
        }
    }
}
