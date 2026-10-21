using CommunityToolkit.Mvvm.Input;
using WetWare.App.Services;

namespace WetWare.App.ViewModels.Course;

/// <summary>Landing page of the course companion: how to study, and the three modules with their lessons.</summary>
public sealed partial class CourseHubViewModel : PageViewModel
{
    private readonly NavigationService _navigation;

    public CourseHubViewModel(NavigationService navigation, Module6ViewModel m6, Module7ViewModel m7, Module8ViewModel m8)
    {
        _navigation = navigation;
        Modules = [m6, m7, m8];
    }

    public override string Key => "Course";
    public override string Glyph => "";
    public override string NavGroup => "Course";

    public IReadOnlyList<CourseModuleViewModel> Modules { get; }

    public int LessonCount => Modules.Sum(m => m.Lessons.Count);

    /// <summary>Opens a module (parameter: module key) or one of its lessons (parameter: lesson view model).</summary>
    [RelayCommand]
    private void Open(object target)
    {
        switch (target)
        {
            case CourseModuleViewModel module:
                _navigation.Navigate(module.Key);
                break;
            case LessonViewModel lesson:
                var owner = Modules.First(m => m.Lessons.Contains(lesson));
                _navigation.Navigate($"{owner.Key}/{lesson.Index}");
                break;
            case string key:
                _navigation.Navigate(key);
                break;
        }
    }
}
