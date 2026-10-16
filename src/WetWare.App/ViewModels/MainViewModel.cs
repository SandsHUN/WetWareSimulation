using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WetWare.App.Localization;
using WetWare.App.Services;

namespace WetWare.App.ViewModels;

/// <summary>Shell: navigation rail, language switch and the guide panel.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly SettingsService _settings;

    [ObservableProperty]
    private PageViewModel _currentPage;

    [ObservableProperty]
    private bool _showGuide;

    [ObservableProperty]
    private bool _sidebarExpanded;

    [ObservableProperty]
    private bool _showSimple;

    public MainViewModel(IEnumerable<PageViewModel> pages, SettingsService settings, NavigationService navigation)
    {
        _settings = settings;
        Pages = pages.ToList();
        _showGuide = settings.Current.ShowGuides;
        _showSimple = settings.Current.ShowSimple;
        _sidebarExpanded = settings.Current.SidebarExpanded;
        _currentPage = Pages[0];
        _currentPage.Activate();
        navigation.NavigateRequested += (_, key) => Navigate(key);
        LocalizationService.Instance.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsHungarian));
            OnPropertyChanged(nameof(IsEnglish));
        };
    }

    public IReadOnlyList<PageViewModel> Pages { get; }

    public bool IsHungarian
    {
        get => LocalizationService.Instance.IsHungarian;
        set { if (value) SetLanguage(LocalizationService.Hungarian); }
    }

    public bool IsEnglish
    {
        get => !LocalizationService.Instance.IsHungarian;
        set { if (value) SetLanguage(LocalizationService.English); }
    }

    partial void OnCurrentPageChanging(PageViewModel value) => CurrentPage?.Deactivate();

    partial void OnCurrentPageChanged(PageViewModel value) => value.Activate();

    partial void OnShowGuideChanged(bool value)
    {
        _settings.Current.ShowGuides = value;
        _settings.Save();
    }

    partial void OnShowSimpleChanged(bool value)
    {
        _settings.Current.ShowSimple = value;
        _settings.Save();
    }

    partial void OnSidebarExpandedChanged(bool value)
    {
        _settings.Current.SidebarExpanded = value;
        _settings.Save();
    }

    [RelayCommand]
    private void Navigate(string key)
    {
        var page = Pages.FirstOrDefault(p => p.Key == key);
        if (page is not null) CurrentPage = page;
    }

    [RelayCommand]
    private void ToggleSidebar() => SidebarExpanded = !SidebarExpanded;

    private void SetLanguage(string language)
    {
        LocalizationService.Instance.SetLanguage(language);
        _settings.Current.Language = language;
        _settings.Save();
    }
}
