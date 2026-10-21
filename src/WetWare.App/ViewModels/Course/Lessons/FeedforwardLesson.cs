using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WetWare.Core.Course.RateNetworks;

namespace WetWare.App.ViewModels.Course.Lessons;

/// <summary>6.2 – firing-rate feedforward network v = W u as a linear filter (edge detection).</summary>
public sealed partial class FeedforwardLesson : LessonViewModel
{
    public FeedforwardLesson() : base("M6.Feedforward", "6.2")
    {
        Filter.Restart();
        RefreshTexts();
    }

    public FeedforwardFilter Filter { get; } = new();

    [ObservableProperty] private string _profile = nameof(InputProfile.Bar);
    [ObservableProperty] private string _kernel = nameof(FilterKernel.Derivative);
    [ObservableProperty] private int _selected = FeedforwardFilter.Size / 2 - 7;
    [ObservableProperty] private string _taps = "";
    [ObservableProperty] private string _explain = "";
    [ObservableProperty] private string _computation = "";

    partial void OnSelectedChanged(int value)
    {
        RefreshTexts();
        RaiseUpdated();
    }

    [RelayCommand]
    private void SetProfile(string id)
    {
        Profile = id;
        Filter.SetProfile(Enum.Parse<InputProfile>(id));
        Filter.Restart();
        RefreshTexts();
        RaiseUpdated();
    }

    partial void OnKernelChanged(string value)
    {
        Filter.SetKernel(Enum.Parse<FilterKernel>(value));
        Filter.Restart();
        RefreshTexts();
        RaiseUpdated();
    }

    [RelayCommand]
    private void Restart()
    {
        Filter.Restart();
        RaiseUpdated();
    }

    public override void Tick(double realMs)
    {
        // Only animate while the outputs are still relaxing towards W u.
        double gap = 0;
        for (int i = 0; i < FeedforwardFilter.Size; i++) gap = Math.Max(gap, Math.Abs(Filter.SteadyState[i] - Filter.Output[i]));
        if (gap < 1e-4) return;
        Filter.Advance(realMs * 0.25);
        RefreshTexts();
        RaiseUpdated();
    }

    protected override void OnLanguageChanged() => RefreshTexts();

    private void RefreshTexts()
    {
        var taps = FeedforwardFilter.KernelTaps(Filter.Kernel);
        Taps = "[" + string.Join(", ", taps.Select(t => F(t, t % 1 == 0 ? 0 : 2))) + "]";
        Explain = Loc[$"M6.Feedforward.Explain.{Filter.Kernel}"];

        int i = Math.Clamp(Selected, 0, FeedforwardFilter.Size - 1);
        var terms = new List<string>();
        for (int j = 0; j < FeedforwardFilter.Size; j++)
        {
            double w = Filter.Weights[i, j];
            if (w != 0) terms.Add($"{F(w, 2)}·{F(Filter.Input[j], 2)}");
        }
        Computation = Loc.Format("M6.Feedforward.Computation", i + 1, string.Join(" + ", terms), F(Filter.SteadyState[i], 3), F(Filter.Output[i], 3));
    }
}
