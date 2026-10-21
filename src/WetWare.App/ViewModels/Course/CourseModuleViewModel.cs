using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WetWare.App.Services;

namespace WetWare.App.ViewModels.Course;

/// <summary>
/// A module of the computational neuroscience course: an ordered set of lessons, each with theory and a lab.
/// The shell's "In plain words" and "Guide" panels follow the selected lesson.
/// </summary>
public abstract partial class CourseModuleViewModel : PageViewModel
{
    private readonly FrameClock _clock;

    protected CourseModuleViewModel(FrameClock clock, string accentFrom, string accentTo, IReadOnlyList<LessonViewModel> lessons)
    {
        _clock = clock;
        Lessons = lessons;
        for (int i = 0; i < lessons.Count; i++) lessons[i].Index = i;
        var from = (Color)ColorConverter.ConvertFromString(accentFrom);
        var to = (Color)ColorConverter.ConvertFromString(accentTo);
        AccentColor = from;
        AccentBrush = new SolidColorBrush(from);
        AccentBrush.Freeze();
        AccentGradient = new LinearGradientBrush(from, to, 0);
        AccentGradient.Freeze();
        AccentSoft = new SolidColorBrush(Color.FromArgb(0x1C, from.R, from.G, from.B));
        AccentSoft.Freeze();
        HeroGradient = new LinearGradientBrush(
            [
                new GradientStop(Color.FromArgb(0x38, from.R, from.G, from.B), 0),
                new GradientStop(Color.FromArgb(0x10, to.R, to.G, to.B), 0.6),
                new GradientStop(Color.FromArgb(0x26, to.R, to.G, to.B), 1),
            ],
            0);
        HeroGradient.Freeze();
        foreach (var lesson in lessons) lesson.AccentBrush = AccentBrush;
        _selectedLesson = lessons[0];
        _selectedLesson.IsSelected = true;
    }

    public override string NavGroup => "Course";

    /// <summary>Module number as text ("6").</summary>
    public abstract string ModuleNumber { get; }

    public override string? NavBadge => ModuleNumber;

    /// <summary>The number is in the badge, so the rail shows a short name only.</summary>
    public override string NavTitle => Loc[$"Page.{Key}.Nav"];

    public string ModuleLabel => Loc.Format("Course.ModuleLabel", ModuleNumber);

    public IReadOnlyList<LessonViewModel> Lessons { get; }

    public Color AccentColor { get; }
    public SolidColorBrush AccentBrush { get; }
    public SolidColorBrush AccentSoft { get; }
    public LinearGradientBrush AccentGradient { get; }
    public LinearGradientBrush HeroGradient { get; }

    [ObservableProperty]
    private LessonViewModel _selectedLesson;

    public string ProgressText => Loc.Format("Course.LessonOf", SelectedLesson.Index + 1, Lessons.Count);

    public bool HasPrevious => SelectedLesson.Index > 0;
    public bool HasNext => SelectedLesson.Index < Lessons.Count - 1;
    public string PreviousTitle => HasPrevious ? Lessons[SelectedLesson.Index - 1].Title : string.Empty;
    public string NextTitle => HasNext ? Lessons[SelectedLesson.Index + 1].Title : string.Empty;

    public override IReadOnlyList<string> GuideSteps => SelectedLesson.GuideSteps;
    public override string SimpleTitle => SelectedLesson.SimpleTitle;
    public override string SimpleText => SelectedLesson.SimpleText;

    public void SelectLesson(int index)
    {
        if (index >= 0 && index < Lessons.Count) SelectedLesson = Lessons[index];
    }

    partial void OnSelectedLessonChanging(LessonViewModel value)
    {
        SelectedLesson.IsSelected = false;
        if (IsActive) SelectedLesson.Deactivate();
    }

    partial void OnSelectedLessonChanged(LessonViewModel value)
    {
        value.IsSelected = true;
        if (IsActive) value.Activate();
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(HasPrevious));
        OnPropertyChanged(nameof(HasNext));
        OnPropertyChanged(nameof(PreviousTitle));
        OnPropertyChanged(nameof(NextTitle));
        OnPropertyChanged(nameof(GuideSteps));
        OnPropertyChanged(nameof(SimpleTitle));
        OnPropertyChanged(nameof(SimpleText));
    }

    [RelayCommand]
    private void Select(LessonViewModel lesson) => SelectedLesson = lesson;

    [RelayCommand]
    private void Previous() => SelectLesson(SelectedLesson.Index - 1);

    [RelayCommand]
    private void Next() => SelectLesson(SelectedLesson.Index + 1);

    protected override void OnActivated()
    {
        SelectedLesson.Activate();
        _clock.Tick += OnTick;
    }

    protected override void OnDeactivated()
    {
        _clock.Tick -= OnTick;
        SelectedLesson.Deactivate();
    }

    private void OnTick(object? sender, double realMs) => SelectedLesson.Tick(realMs);
}
