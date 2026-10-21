using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using WetWare.App.ViewModels.Course;

namespace WetWare.App.Views.Course;

/// <summary>Shared page of the three course modules: lesson stepper, theory panel, lab, takeaways and quiz.</summary>
public partial class CourseModuleView : UserControl
{
    private CourseModuleViewModel? _vm;

    public CourseModuleView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnModuleChanged;
        _vm = DataContext as CourseModuleViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnModuleChanged;
        Scroller.ScrollToTop();
    }

    private void OnModuleChanged(object? sender, PropertyChangedEventArgs e)
    {
        // A new lesson starts at the top of the page.
        if (e.PropertyName == nameof(CourseModuleViewModel.SelectedLesson)) Scroller.ScrollToTop();
    }
}
