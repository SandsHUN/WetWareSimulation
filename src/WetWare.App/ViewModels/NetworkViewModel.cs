using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WetWare.App.Services;
using WetWare.Core.Network;
using WetWare.Core.Scenarios;

namespace WetWare.App.ViewModels;

public enum NetworkTool
{
    Select,
    Stimulate,
    Ablate,
}

public enum NarrativeKind
{
    Info,
    Spike,
    Ltp,
    Ltd,
    Phase,
    Alert,
}

public sealed record NarrativeEntry(string Time, string Text, NarrativeKind Kind);

/// <summary>Telemetry snapshot handed to the view once per frame (the in-process "PUB" message).</summary>
public sealed class NetworkFrame
{
    public required List<SpikeEvent> Spikes { get; init; }
    public required List<StdpEvent> StdpEvents { get; init; }
    public bool SelectedFired { get; init; }
    public bool SlowUpdate { get; init; }
}

/// <summary>The 100-neuron network dashboard: monitors, topology, plasticity, math lab, controls and metrics.</summary>
public sealed partial class NetworkViewModel : PageViewModel
{
    private const double SyncAlertLevel = 0.42;
    private readonly FrameClock _clock;
    private readonly Stopwatch _wall = Stopwatch.StartNew();
    private double _slowAccumulator;
    private double _lastFireNarrative = -10000;
    private double _lastStdpNarrative = -10000;
    private string _lastPhase = "";
    private bool _syncAlerted;
    private bool _syncing;

    public NetworkViewModel(FrameClock clock)
    {
        _clock = clock;
        ScenarioOptions = ScenarioCatalog.All.Select(f => f())
            .Select(s => new LocalizedOption<string>(s.Id, $"Scenario.{s.Id}.Title")).ToList();
        _selectedScenario = ScenarioOptions[0];
        Scenario = new FreeScenario();
        Network = Scenario.CreateNetwork();
        Network.SelectedNeuron = 7;
        WeightMatrix = new double[Network.Count, Network.Count];
        PullParameters();
        RefreshMetrics();
        RefreshSelected();
    }

    public override string Key => "Network";
    public override string Glyph => "";

    public SpikingNetwork Network { get; private set; }

    public Scenario Scenario { get; private set; }

    public double[,] WeightMatrix { get; }

    public IReadOnlyList<LocalizedOption<string>> ScenarioOptions { get; }

    public double[] SpeedOptions { get; } = [0.05, 0.1, 0.25, 0.5, 1.0, 2.0, 4.0];

    public ObservableCollection<NarrativeEntry> Narratives { get; } = new();

    /// <summary>Recent STDP updates for the live learning-window chart.</summary>
    public List<StdpEvent> RecentStdp { get; } = new();

    public event EventHandler<NetworkFrame>? FrameUpdated;

    /// <summary>Raised when a new network was built (scenario change / reset).</summary>
    public event EventHandler? NetworkReset;

    [ObservableProperty] private LocalizedOption<string> _selectedScenario;
    [ObservableProperty] private bool _isRunning = true;
    [ObservableProperty] private double _speed = 0.5;
    [ObservableProperty] private NetworkTool _tool = NetworkTool.Select;

    [ObservableProperty] private double _electrodeCurrent = 300;
    [ObservableProperty] private bool _electrodeOn;
    [ObservableProperty] private double _learningRate = 1;
    [ObservableProperty] private bool _plasticityEnabled = true;
    [ObservableProperty] private double _inhibitionGain = 1;
    [ObservableProperty] private double _excitationGain = 1;
    [ObservableProperty] private double _noiseRate = 500;
    [ObservableProperty] private double _depressionRatio = 1.05;

    [ObservableProperty] private string _timeText = "";
    [ObservableProperty] private string _phaseText = "";
    [ObservableProperty] private double _phaseProgress;
    [ObservableProperty] private bool _hasProgress;
    [ObservableProperty] private string _syncText = "";
    [ObservableProperty] private string _rateText = "";
    [ObservableProperty] private string _strongText = "";
    [ObservableProperty] private string _spikesText = "";
    [ObservableProperty] private string _meanWeightText = "";
    [ObservableProperty] private string _ablatedText = "";
    [ObservableProperty] private double _syncLevel;
    [ObservableProperty] private double _rateLevel;
    [ObservableProperty] private double _strongLevel;
    [ObservableProperty] private double _meanWeightLevel;
    [ObservableProperty] private Brush _syncBrush = Brushes.Cyan;

    [ObservableProperty] private string _selectedTitle = "";
    [ObservableProperty] private string _selectedInfo = "";
    [ObservableProperty] private string _vText = "";
    [ObservableProperty] private Brush _vBrush = Brushes.LightGreen;
    [ObservableProperty] private string _synText = "";
    [ObservableProperty] private string _extText = "";
    [ObservableProperty] private string _noiseText = "";
    [ObservableProperty] private string _dvdtText = "";
    [ObservableProperty] private string _latestStdpText = "";
    [ObservableProperty] private IReadOnlyList<DebugLine> _debugLines = [];

    public bool IsSelectTool { get => Tool == NetworkTool.Select; set { if (value) Tool = NetworkTool.Select; } }
    public bool IsStimulateTool { get => Tool == NetworkTool.Stimulate; set { if (value) Tool = NetworkTool.Stimulate; } }
    public bool IsAblateTool { get => Tool == NetworkTool.Ablate; set { if (value) Tool = NetworkTool.Ablate; } }

    public string ScenarioDescription => Loc[$"Scenario.{Scenario.Id}.Observe"];
    public string ScenarioPhenomenon => Loc[$"Scenario.{Scenario.Id}.Phenomenon"];
    public bool ScenarioControlsGains => Scenario is SeizureScenario;
    public bool ScenarioControlsRule => Scenario is HomeostasisScenario;
    public int StimulatedCount => Network.ElectrodeTargets.Count;

    partial void OnToolChanged(NetworkTool value)
    {
        OnPropertyChanged(nameof(IsSelectTool));
        OnPropertyChanged(nameof(IsStimulateTool));
        OnPropertyChanged(nameof(IsAblateTool));
    }

    partial void OnSelectedScenarioChanged(LocalizedOption<string> value) => LoadScenario(value.Value);

    partial void OnElectrodeCurrentChanged(double value) => PushParameters();
    partial void OnElectrodeOnChanged(bool value)
    {
        PushParameters();
        if (value && Network.ElectrodeTargets.Count == 0) Network.ElectrodeTargets.Add(Network.SelectedNeuron);
        OnPropertyChanged(nameof(StimulatedCount));
    }
    partial void OnLearningRateChanged(double value) => PushParameters();
    partial void OnPlasticityEnabledChanged(bool value) => PushParameters();
    partial void OnInhibitionGainChanged(double value) => PushParameters();
    partial void OnExcitationGainChanged(double value) => PushParameters();
    partial void OnNoiseRateChanged(double value) => PushParameters();
    partial void OnDepressionRatioChanged(double value) => PushParameters();

    /// <summary>Loads a teaching scenario (also used by the scenario gallery).</summary>
    public void LoadScenario(string id)
    {
        var option = ScenarioOptions.FirstOrDefault(o => o.Value == id);
        if (option is not null && option != SelectedScenario)
        {
            SelectedScenario = option; // re-enters through OnSelectedScenarioChanged
            return;
        }
        Scenario = ScenarioCatalog.Create(id);
        int selected = Network.SelectedNeuron;
        Network = Scenario.CreateNetwork();
        Network.SelectedNeuron = Scenario.Groups.Count > 0 ? Scenario.Groups[^1].Neurons[0] : selected;
        _lastPhase = "";
        _syncAlerted = false;
        RecentStdp.Clear();
        Narratives.Clear();
        AddNarrative(Loc.Format("Net.Narr.Loaded", Loc[$"Scenario.{id}.Title"]), NarrativeKind.Phase);
        PullParameters();
        ElectrodeOn = false;
        IsRunning = true;
        OnPropertyChanged(nameof(ScenarioDescription));
        OnPropertyChanged(nameof(ScenarioPhenomenon));
        OnPropertyChanged(nameof(ScenarioControlsGains));
        OnPropertyChanged(nameof(ScenarioControlsRule));
        OnPropertyChanged(nameof(StimulatedCount));
        NetworkReset?.Invoke(this, EventArgs.Empty);
        RefreshMetrics();
        RefreshSelected();
    }

    private void PushParameters()
    {
        if (_syncing || Network is null) return;
        var p = Network.Parameters;
        Network.ElectrodeCurrent = ElectrodeOn ? ElectrodeCurrent : 0;
        p.Stdp.LearningRate = LearningRate;
        p.PlasticityEnabled = PlasticityEnabled;
        p.InhibitionGain = InhibitionGain;
        p.ExcitationGain = ExcitationGain;
        p.NoiseRateHz = NoiseRate;
        p.Stdp.AMinus = p.Stdp.APlus * DepressionRatio;
    }

    /// <summary>Reads parameters back (scenarios may change them while running).</summary>
    private void PullParameters()
    {
        _syncing = true;
        var p = Network.Parameters;
        LearningRate = p.Stdp.LearningRate;
        PlasticityEnabled = p.PlasticityEnabled;
        InhibitionGain = p.InhibitionGain;
        ExcitationGain = p.ExcitationGain;
        NoiseRate = p.NoiseRateHz;
        DepressionRatio = Math.Round(p.Stdp.AMinus / p.Stdp.APlus, 2);
        _syncing = false;
    }

    protected override void OnActivated() => _clock.Tick += OnTick;

    protected override void OnDeactivated() => _clock.Tick -= OnTick;

    protected override void OnLanguageChanged()
    {
        RefreshMetrics();
        RefreshSelected();
    }

    private void OnTick(object? sender, double realMs)
    {
        bool selectedFired = false;
        if (IsRunning)
        {
            double simMs = Math.Min(realMs * Speed, 60);
            selectedFired = AdvanceNetwork(simMs);
        }
        Publish(realMs, selectedFired);
    }

    private bool AdvanceNetwork(double simMs)
    {
        long before = Network.TotalSpikes;
        int sel = Network.SelectedNeuron;
        double lastSpikeBefore = Network.GetLastSpike(sel);
        Network.Advance(simMs, n => Scenario.Update(n));
        return Network.GetLastSpike(sel) > lastSpikeBefore && Network.TotalSpikes > before;
    }

    private void Publish(double realMs, bool selectedFired)
    {
        var spikes = Network.TakeSpikes();
        var stdp = Network.TakeStdpEvents();
        foreach (var ev in stdp)
        {
            RecentStdp.Add(ev);
        }
        if (RecentStdp.Count > 160) RecentStdp.RemoveRange(0, RecentStdp.Count - 160);

        HandleNarratives(stdp);

        _slowAccumulator += realMs;
        bool slow = _slowAccumulator >= 120 || !IsRunning;
        if (slow)
        {
            _slowAccumulator = 0;
            Network.FillWeightMatrix(WeightMatrix);
            if (Scenario is SeizureScenario or HomeostasisScenario) PullParameters();
            RefreshMetrics();
            RefreshSelected();
        }

        FrameUpdated?.Invoke(this, new NetworkFrame
        {
            Spikes = spikes,
            StdpEvents = stdp,
            SelectedFired = selectedFired,
            SlowUpdate = slow,
        });
    }

    private void HandleNarratives(List<StdpEvent> stdp)
    {
        double now = _wall.Elapsed.TotalMilliseconds;
        int sel = Network.SelectedNeuron;

        foreach (var ex in Network.TakeExplanations())
        {
            if (now - _lastFireNarrative < 1800) continue;
            _lastFireNarrative = now;
            AddNarrative(Loc.Format("Net.Narr.Fire", ex.Neuron, F(ex.SynapticCurrent, 0), F(ex.ExternalCurrent, 0),
                F(ex.NoiseCurrent, 0), F(Network.Parameters.Lif.Threshold, 0), ex.RecentInputs), NarrativeKind.Spike);
        }

        // Prefer plasticity events that involve the selected neuron; otherwise report one now and then.
        StdpEvent? pick = null;
        foreach (var ev in stdp)
            if (ev.Pre == sel || ev.Post == sel) pick = ev;
        if (pick is null && now - _lastStdpNarrative > 4000 && stdp.Count > 0)
            pick = stdp.MaxBy(e => Math.Abs(e.DeltaW));
        if (pick is { } p && now - _lastStdpNarrative > 1500)
        {
            _lastStdpNarrative = now;
            string text = p.DeltaW >= 0
                ? Loc.Format("Net.Narr.Ltp", p.Pre, F(p.DeltaT, 1), p.Post, F(p.DeltaW, 4), F(p.NewWeight, 3))
                : Loc.Format("Net.Narr.Ltd", p.Pre, F(-p.DeltaT, 1), p.Post, F(-p.DeltaW, 4), F(p.NewWeight, 3));
            AddNarrative(text, p.DeltaW >= 0 ? NarrativeKind.Ltp : NarrativeKind.Ltd);
            LatestStdpText = text;
        }

        if (Scenario.PhaseKey != _lastPhase)
        {
            _lastPhase = Scenario.PhaseKey;
            string key = $"Scenario.{Scenario.Id}.Phase.{Scenario.PhaseKey}";
            if (Loc.Has(key)) AddNarrative(Loc[key], NarrativeKind.Phase);
        }

        double chi = Network.Metrics.SynchronyIndex;
        if (!_syncAlerted && chi > SyncAlertLevel && Network.TimeMs > 1500)
        {
            _syncAlerted = true;
            AddNarrative(Loc.Format("Net.Narr.Sync", F(chi, 2)), NarrativeKind.Alert);
        }
        else if (_syncAlerted && chi < SyncAlertLevel * 0.6)
        {
            _syncAlerted = false;
            AddNarrative(Loc.Format("Net.Narr.Desync", F(chi, 2)), NarrativeKind.Info);
        }
    }

    private void AddNarrative(string text, NarrativeKind kind)
    {
        Narratives.Insert(0, new NarrativeEntry($"{F(Network.TimeMs / 1000.0, 2)} s", text, kind));
        while (Narratives.Count > 60) Narratives.RemoveAt(Narratives.Count - 1);
    }

    private void RefreshMetrics()
    {
        var m = Network.Metrics;
        TimeText = $"t = {F(Network.TimeMs / 1000.0, 2)} s";
        string phaseKey = $"Scenario.{Scenario.Id}.PhaseName.{Scenario.PhaseKey}";
        PhaseText = Loc.Has(phaseKey) ? Loc[phaseKey] : Loc["Scenario.PhaseName.Running"];
        HasProgress = Scenario.Progress.HasValue;
        PhaseProgress = Scenario.Progress ?? 0;
        SyncText = F(m.SynchronyIndex, 2);
        SyncLevel = m.SynchronyIndex;
        SyncBrush = m.SynchronyIndex > SyncAlertLevel ? new SolidColorBrush(Color.FromRgb(0xF4, 0x3F, 0x5E))
            : m.SynchronyIndex > 0.28 ? new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24))
            : new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99));
        RateText = F(m.MeanRateHz, 1);
        RateLevel = Math.Clamp(m.MeanRateHz / 40.0, 0, 1);
        double strong = Network.StrongSynapseFraction();
        StrongText = F(strong * 100, 0);
        StrongLevel = strong;
        double meanW = Network.MeanPlasticWeight();
        MeanWeightText = F(meanW, 3);
        MeanWeightLevel = meanW;
        SpikesText = Network.TotalSpikes.ToString("N0", Loc.Culture);
        AblatedText = Network.AblatedCount.ToString(Loc.Culture);
    }

    private void RefreshSelected()
    {
        int i = Network.SelectedNeuron;
        var lif = Network.Parameters.Lif;
        string type = Network.IsInhibitory(i) ? Loc["Net.Inhibitory"] : Loc["Net.Excitatory"];
        SelectedTitle = Loc.Format("Net.SelectedTitle", i, type);
        SelectedInfo = Loc.Format("Net.SelectedInfo", Network.Incoming(i).Count, Network.Outgoing(i).Count,
            Network.IsAblated(i) ? Loc["Net.Ablated"] : Network.ElectrodeTargets.Contains(i) ? Loc["Net.Stimulated"] : "—");
        double v = Network.GetVoltage(i);
        double syn = Network.GetSynapticCurrent(i), ext = Network.GetExternalCurrent(i), noise = Network.GetNoiseCurrent(i);
        VText = $"{F(v, 2)} mV";
        VBrush = new SolidColorBrush(NeuronLabViewModel.VoltageColor((v - lif.RestingPotential) / (lif.Threshold - lif.RestingPotential)));
        SynText = $"{F(syn, 0)} pA";
        ExtText = $"{F(ext, 0)} pA";
        NoiseText = $"{F(noise, 0)} pA";
        DvdtText = $"{F(lif.Derivative(v, syn + ext + noise), 3)} mV/ms";
        DebugLines = BioDebugger.Build(Network.TimeMs > 0 ? Network.LastSelectedStep : null, lif, networkMode: true);
    }

    /// <summary>Applies the active tool to a clicked neuron.</summary>
    public void HandleNeuronClick(int neuron, bool rightButton)
    {
        var tool = rightButton ? NetworkTool.Select : Tool;
        switch (tool)
        {
            case NetworkTool.Select:
                Network.SelectedNeuron = neuron;
                break;
            case NetworkTool.Stimulate:
                if (!Network.ElectrodeTargets.Remove(neuron)) Network.ElectrodeTargets.Add(neuron);
                if (!ElectrodeOn && Network.ElectrodeTargets.Count > 0) ElectrodeOn = true;
                AddNarrative(Loc.Format("Net.Narr.Electrode", neuron, Network.ElectrodeTargets.Count, F(ElectrodeCurrent, 0)), NarrativeKind.Info);
                break;
            case NetworkTool.Ablate:
                bool ablate = !Network.IsAblated(neuron);
                Network.SetAblated(neuron, ablate);
                AddNarrative(Loc.Format(ablate ? "Net.Narr.Ablate" : "Net.Narr.Revive", neuron), NarrativeKind.Alert);
                break;
        }
        OnPropertyChanged(nameof(StimulatedCount));
        RefreshSelected();
        RefreshMetrics();
    }

    [RelayCommand]
    private void TogglePlay() => IsRunning = !IsRunning;

    [RelayCommand]
    private void Step5()
    {
        IsRunning = false;
        bool fired = AdvanceNetwork(5);
        Publish(0, fired);
    }

    [RelayCommand]
    private void StepTiny()
    {
        IsRunning = false;
        bool fired = AdvanceNetwork(Network.Parameters.DtMs);
        Publish(0, fired);
    }

    [RelayCommand]
    private void Restart() => LoadScenario(Scenario.Id);

    [RelayCommand]
    private void ClearAblations()
    {
        for (int i = 0; i < Network.Count; i++) Network.SetAblated(i, false);
        RefreshMetrics();
    }

    [RelayCommand]
    private void ClearElectrodes()
    {
        Network.ElectrodeTargets.Clear();
        ElectrodeOn = false;
        OnPropertyChanged(nameof(StimulatedCount));
    }

    [RelayCommand]
    private void ClearLog() => Narratives.Clear();
}
