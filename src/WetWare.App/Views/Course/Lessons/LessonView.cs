using System.Windows;
using System.Windows.Controls;
using ScottPlot.WPF;
using WetWare.App.Controls;
using WetWare.App.Localization;
using WetWare.App.ViewModels.Course;
using WetWare.Core.Mathematics;

namespace WetWare.App.Views.Course.Lessons;

/// <summary>
/// Base of every lesson lab view: hooks the lesson's <see cref="LessonViewModel.Updated"/> event and the language switch
/// while the view is loaded, and calls <see cref="Draw"/> to repaint the plots and custom visuals.
/// </summary>
public abstract class LessonView : UserControl
{
    private LessonViewModel? _lesson;

    protected LessonView()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected static LocalizationService Loc => LocalizationService.Instance;

    /// <summary>Number of Updated events received so far (used to throttle expensive redraws).</summary>
    protected long Frame { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _lesson = DataContext as LessonViewModel;
        if (_lesson is null) return;
        Setup();
        _lesson.Updated += OnUpdated;
        Loc.LanguageChanged += OnLanguageChanged;
        Draw(true);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_lesson is null) return;
        _lesson.Updated -= OnUpdated;
        Loc.LanguageChanged -= OnLanguageChanged;
        _lesson = null;
    }

    private void OnUpdated(object? sender, EventArgs e)
    {
        Frame++;
        Draw(false);
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Draw(true);

    /// <summary>One-time styling of plots and controls.</summary>
    protected virtual void Setup() { }

    /// <param name="full">True after loading or a language change: redraw everything, including static parts.</param>
    protected abstract void Draw(bool full);

    protected static void StylePlots(params WpfPlot[] plots)
    {
        foreach (var plot in plots) LabPlot.Apply(plot);
    }

    /// <summary>Copies the newest samples of a trace into a fixed-length array (left-padded with <paramref name="pad"/>).</summary>
    protected static void Fill(double[] target, RingBuffer<double> source, double pad)
    {
        int count = source.Count, n = target.Length;
        int padCount = n - Math.Min(count, n), skip = Math.Max(0, count - n);
        for (int i = 0; i < padCount; i++) target[i] = pad;
        for (int i = 0; i < n - padCount; i++) target[padCount + i] = source[skip + i];
    }
}
