using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WetWare.App.Localization;
using WetWare.App.Services;

namespace WetWare.App.ViewModels;

/// <summary>One glossary entry; texts come from <c>Gloss.{Id}.*</c>.</summary>
public sealed record GlossaryEntry(string Id, string Category, string? PageKey)
{
    private static LocalizationService L => LocalizationService.Instance;
    public string Term => L[$"Gloss.{Id}.Term"];
    public string Definition => L[$"Gloss.{Id}.Def"];
    public string Why => L[$"Gloss.{Id}.Why"];
    public string CategoryLabel => L[$"Gloss.Cat.{Category}"];
    public string PageTitle => PageKey is null ? string.Empty : L[$"Page.{PageKey}.Title"];
    public bool HasPage => PageKey is not null;
}

/// <summary>Searchable bilingual dictionary of the biological and computational concepts used in the lab.</summary>
public sealed partial class GlossaryViewModel : PageViewModel
{
    private readonly NavigationService _navigation;
    private readonly List<GlossaryEntry> _all;

    public GlossaryViewModel(NavigationService navigation)
    {
        _navigation = navigation;
        _all =
        [
            new("Wetware", "Concepts", "Home"),
            new("Neuromorphic", "Concepts", null),
            new("Organoid", "Concepts", null),
            new("Mea", "Concepts", null),
            new("Neuron", "Cell", "Neuron"),
            new("MembranePotential", "Cell", "Neuron"),
            new("RestingPotential", "Cell", "Neuron"),
            new("ActionPotential", "Cell", "Neuron"),
            new("Threshold", "Cell", "Neuron"),
            new("Refractory", "Cell", "Neuron"),
            new("Lif", "Math", "Neuron"),
            new("TimeConstant", "Math", "Neuron"),
            new("Rheobase", "Math", "Neuron"),
            new("PhasePlane", "Math", "Neuron"),
            new("Euler", "Math", "Neuron"),
            new("Synapse", "Network", "Network"),
            new("Psp", "Network", "Network"),
            new("Dale", "Network", "Network"),
            new("SmallWorld", "Network", "Network"),
            new("Raster", "Network", "Network"),
            new("Synchrony", "Network", "Network"),
            new("Seizure", "Network", "Scenarios"),
            new("ShortTermDepression", "Network", "Network"),
            new("Ablation", "Network", "Network"),
            new("Hebb", "Learning", "Stdp"),
            new("Stdp", "Learning", "Stdp"),
            new("Ltp", "Learning", "Stdp"),
            new("Ltd", "Learning", "Stdp"),
            new("Homeostasis", "Learning", "Scenarios"),
            new("RateCoding", "Coding", "Coding"),
            new("LatencyCoding", "Coding", "Coding"),
            new("PhaseCoding", "Coding", "Coding"),
            new("PopulationCoding", "Coding", "Coding"),
            new("ReceptiveField", "Vision", "Vision"),
            new("LateralInhibition", "Vision", "Vision"),
            new("GanglionCell", "Vision", "Vision"),
            new("V1", "Vision", "Vision"),
            new("Wta", "Vision", "Recognition"),
        ];
        _filtered = _all;
        _selected = _all[0];
    }

    public override string Key => "Glossary";
    public override string Glyph => "";

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private IReadOnlyList<GlossaryEntry> _filtered;
    [ObservableProperty] private GlossaryEntry? _selected;

    public string CountText => Loc.Format("Gloss.Count", Filtered.Count, _all.Count);

    partial void OnSearchChanged(string value) => ApplyFilter();

    protected override void OnLanguageChanged() => ApplyFilter();

    private void ApplyFilter()
    {
        string q = Search.Trim();
        var keep = Selected?.Id;
        Filtered = (string.IsNullOrEmpty(q)
                ? _all
                : _all.Where(e => e.Term.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                                  || e.Definition.Contains(q, StringComparison.CurrentCultureIgnoreCase)))
            .Select(e => e with { })
            .ToList();
        Selected = Filtered.FirstOrDefault(e => e.Id == keep) ?? Filtered.FirstOrDefault();
        OnPropertyChanged(nameof(CountText));
    }

    [RelayCommand]
    private void OpenPage(string? pageKey)
    {
        if (!string.IsNullOrEmpty(pageKey)) _navigation.Navigate(pageKey);
    }
}
