using CommunityToolkit.Mvvm.Input;
using WetWare.App.Localization;
using WetWare.App.Services;
using WetWare.Core.Scenarios;

namespace WetWare.App.ViewModels;

/// <summary>Card describing one built-in experiment.</summary>
public sealed record ScenarioCard(string Id, string Glyph, string AccentHex)
{
    private static LocalizationService L => LocalizationService.Instance;
    public string Title => L[$"Scenario.{Id}.Title"];
    public string Phenomenon => L[$"Scenario.{Id}.Phenomenon"];
    public string Observe => L[$"Scenario.{Id}.Observe"];
    public string Steps => L[$"Scenario.{Id}.Steps"];
    public string Duration => L[$"Scenario.{Id}.Duration"];
}

/// <summary>Gallery of pre-configured teaching experiments that load into the network simulator.</summary>
public sealed partial class ScenariosViewModel : PageViewModel
{
    private readonly NavigationService _navigation;
    private readonly NetworkViewModel _network;

    public ScenariosViewModel(NavigationService navigation, NetworkViewModel network)
    {
        _navigation = navigation;
        _network = network;
        Cards = BuildCards();
    }

    public override string Key => "Scenarios";
    public override string Glyph => "";

    public IReadOnlyList<ScenarioCard> Cards { get; private set; }

    protected override void OnLanguageChanged() => Cards = BuildCards();

    private static IReadOnlyList<ScenarioCard> BuildCards()
    {
        string[] accents = ["#97A3C2", "#FBBF24", "#F43F5E", "#A78BFA", "#34D399"];
        return ScenarioCatalog.All.Select((f, i) =>
        {
            var s = f();
            return new ScenarioCard(s.Id, s.Glyph, accents[i % accents.Length]);
        }).ToList();
    }

    [RelayCommand]
    private void Start(string id)
    {
        _network.LoadScenario(id);
        _navigation.Navigate("Network");
    }
}
