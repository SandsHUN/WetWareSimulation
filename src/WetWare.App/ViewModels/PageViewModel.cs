using CommunityToolkit.Mvvm.ComponentModel;
using WetWare.App.Localization;

namespace WetWare.App.ViewModels;

/// <summary>
/// Base class of every navigable page. Localized texts are exposed as properties that are all refreshed
/// when the language changes; pages start/stop their simulations in <see cref="OnActivated"/> / <see cref="OnDeactivated"/>.
/// </summary>
public abstract class PageViewModel : ObservableObject
{
    protected PageViewModel()
    {
        Loc.LanguageChanged += (_, _) =>
        {
            OnLanguageChanged();
            OnPropertyChanged(string.Empty);
        };
    }

    protected static LocalizationService Loc => LocalizationService.Instance;

    /// <summary>Stable page identifier, also the localization prefix: <c>Page.{Key}.*</c>.</summary>
    public abstract string Key { get; }

    /// <summary>Segoe Fluent Icons glyph for the navigation rail.</summary>
    public abstract string Glyph { get; }

    public string Title => Loc[$"Page.{Key}.Title"];

    public string Subtitle => Loc[$"Page.{Key}.Subtitle"];

    public string NavTip => Loc[$"Page.{Key}.Tip"];

    /// <summary>Step-by-step "try this" instructions shown in the guide panel.</summary>
    public IReadOnlyList<string> GuideSteps =>
        Loc[$"Page.{Key}.Guide"].Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Plain-language explanation with an everyday analogy, for visitors without a biology background.</summary>
    public string SimpleTitle => Loc[$"Page.{Key}.SimpleTitle"];

    public string SimpleText => Loc[$"Page.{Key}.Simple"];

    public bool IsActive { get; private set; }

    public void Activate()
    {
        IsActive = true;
        OnActivated();
    }

    public void Deactivate()
    {
        IsActive = false;
        OnDeactivated();
    }

    protected virtual void OnActivated() { }

    protected virtual void OnDeactivated() { }

    protected virtual void OnLanguageChanged() { }

    protected static string F(double value, int decimals = 1) =>
        value.ToString("F" + decimals, Loc.Culture);
}
