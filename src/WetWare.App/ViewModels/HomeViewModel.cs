using CommunityToolkit.Mvvm.Input;
using WetWare.App.Services;
using WetWare.Core.Network;

namespace WetWare.App.ViewModels;

/// <summary>Module card on the home page.</summary>
public sealed record ModuleCard(string PageKey, string Glyph, string AccentHex)
{
    public string Title => WetWare.App.Localization.LocalizationService.Instance[$"Page.{PageKey}.Title"];
    public string Description => WetWare.App.Localization.LocalizationService.Instance[$"Home.Card.{PageKey}"];
}

/// <summary>Welcome page with a live background network and entry points to every module.</summary>
public sealed partial class HomeViewModel : PageViewModel
{
    private readonly FrameClock _clock;
    private readonly NavigationService _navigation;

    public HomeViewModel(FrameClock clock, NavigationService navigation)
    {
        _clock = clock;
        _navigation = navigation;
        Network = new SpikingNetwork(new NetworkParameters { PlasticityEnabled = false, Seed = 3 });
        Network.SelectedNeuron = 0;
        Network.Advance(500);
        Network.TakeSpikes();
        Modules =
        [
            new ModuleCard("Neuron", "", "#22D3EE"),
            new ModuleCard("Network", "", "#FB7185"),
            new ModuleCard("Stdp", "", "#34D399"),
            new ModuleCard("Coding", "", "#F472B6"),
            new ModuleCard("Vision", "", "#FBBF24"),
            new ModuleCard("Recognition", "", "#A78BFA"),
            new ModuleCard("Scenarios", "", "#38BDF8"),
            new ModuleCard("Glossary", "", "#97A3C2"),
        ];
    }

    public override string Key => "Home";
    public override string Glyph => "";

    public SpikingNetwork Network { get; }

    public IReadOnlyList<ModuleCard> Modules { get; private set; }

    public event EventHandler? FrameUpdated;

    protected override void OnActivated() => _clock.Tick += OnTick;

    protected override void OnDeactivated() => _clock.Tick -= OnTick;

    protected override void OnLanguageChanged()
    {
        // Records are immutable: rebuild the list so card texts refresh.
        Modules = Modules.Select(m => m with { }).ToList();
    }

    private void OnTick(object? sender, double realMs)
    {
        Network.Advance(Math.Min(realMs * 0.4, 30));
        Network.TakeSpikes();
        Network.TakeStdpEvents();
        Network.TakeExplanations();
        FrameUpdated?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Open(string pageKey) => _navigation.Navigate(pageKey);
}
