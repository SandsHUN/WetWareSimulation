using CommunityToolkit.Mvvm.ComponentModel;
using WetWare.App.Localization;

namespace WetWare.App.ViewModels;

/// <summary>Combo-box entry whose label (and tooltip) follow the current language.</summary>
public sealed class LocalizedOption<T> : ObservableObject
{
    public LocalizedOption(T value, string key)
    {
        Value = value;
        Key = key;
        LocalizationService.Instance.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Label));
            OnPropertyChanged(nameof(Tip));
        };
    }

    public T Value { get; }

    public string Key { get; }

    public string Label => LocalizationService.Instance[Key];

    public string Tip => LocalizationService.Instance.Has(Key + ".Tip") ? LocalizationService.Instance[Key + ".Tip"] : Label;

    public override string ToString() => Label;
}
