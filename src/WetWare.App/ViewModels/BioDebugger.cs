using WetWare.App.Localization;
using WetWare.Core.Neurons;

namespace WetWare.App.ViewModels;

/// <summary>One line of the step-by-step Bio-Debugger.</summary>
public sealed record DebugLine(int Number, string Title, string Formula, string Result, DebugLineKind Kind);

public enum DebugLineKind
{
    Normal,
    Spike,
    Refractory,
}

/// <summary>Turns an <see cref="IntegrationStep"/> into the four human-readable computation steps.</summary>
public static class BioDebugger
{
    public static IReadOnlyList<DebugLine> Build(IntegrationStep? step, LifParameters p, bool networkMode)
    {
        var loc = LocalizationService.Instance;
        if (step is null) return [new DebugLine(0, loc["Debug.NoStep"], string.Empty, string.Empty, DebugLineKind.Normal)];

        string N(double v, int d = 2) => v.ToString("F" + d, loc.Culture);
        string S(double v, int d = 3) => v.ToString((d == 3 ? "+0.000;−0.000" : "+0.00;−0.00"), loc.Culture);

        var lines = new List<DebugLine>
        {
            new(1, loc["Debug.Leak"],
                $"−(v − E_L) / τm = −({N(step.VOld)} − ({N(p.RestingPotential)})) / {N(p.TauM, 1)}",
                $"= {S(step.LeakRate)} mV/ms", DebugLineKind.Normal),
        };

        if (networkMode)
        {
            lines.Add(new(2, loc["Debug.Synaptic"],
                loc.Format("Debug.SynapticFormula", N(step.SynapticCurrent, 1), N(step.ExternalCurrent, 1), N(step.NoiseCurrent, 1), step.ArrivingSpikes),
                $"R_m·I / τm = {S(step.InputRate)} mV/ms", DebugLineKind.Normal));
        }
        else
        {
            lines.Add(new(2, loc["Debug.Input"],
                $"R_m·I / τm = {N(p.Resistance, 0)} MΩ · {N(step.TotalCurrent, 1)} pA / 1000 / {N(p.TauM, 1)}",
                $"= {S(step.InputRate)} mV/ms", DebugLineKind.Normal));
        }

        if (step.Refractory)
        {
            lines.Add(new(3, loc["Debug.Integrate"], loc["Debug.RefractoryFormula"], $"v = {N(step.ResetPotential)} mV", DebugLineKind.Refractory));
            lines.Add(new(4, loc["Debug.Threshold"], loc.Format("Debug.RefractoryCheck", N(p.RefractoryPeriod, 1)), loc["Debug.NoSpike"], DebugLineKind.Refractory));
            return lines;
        }

        lines.Add(new(3, loc["Debug.Integrate"],
            $"v_new = v_old + dt · dv/dt = {N(step.VOld)} + {N(step.DtMs, 1)} · ({S(step.Derivative)})",
            $"= {N(step.VIntegrated, 3)} mV", DebugLineKind.Normal));

        lines.Add(step.Fired
            ? new(4, loc["Debug.Threshold"], $"{N(step.VIntegrated)} ≥ {N(step.Threshold)} ?",
                loc.Format("Debug.Spike", N(step.ResetPotential)), DebugLineKind.Spike)
            : new(4, loc["Debug.Threshold"], $"{N(step.VIntegrated)} ≥ {N(step.Threshold)} ?",
                loc["Debug.NoSpike"], DebugLineKind.Normal));
        return lines;
    }
}
