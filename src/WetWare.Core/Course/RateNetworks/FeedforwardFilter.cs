using WetWare.Core.Mathematics;

namespace WetWare.Core.Course.RateNetworks;

public enum InputProfile
{
    Step,
    Bar,
    Staircase,
    Smooth,
    NoisyBar,
}

public enum FilterKernel
{
    /// <summary>[0, −1, 1, 0, 0] from the lecture: a discrete derivative, i.e. an edge detector.</summary>
    Derivative,

    /// <summary>[−½, 1, −½]: centre–surround (retina / LGN-like), responds to both sides of an edge and to thin bars.</summary>
    CenterSurround,

    /// <summary>[¼, ½, ¼]: local averaging, smooths noise away.</summary>
    Smoothing,

    /// <summary>W = I: the output copies the input.</summary>
    Identity,
}

/// <summary>
/// Linear feedforward firing-rate network (lecture 6.2): τ dv/dt = −v + W u, steady state v_ss = W u.
/// W is Toeplitz – every output neuron applies the same local kernel to its own neighbourhood of the input.
/// </summary>
public sealed class FeedforwardFilter
{
    public const int Size = 48;

    public FeedforwardFilter() => Rebuild();

    public InputProfile Profile { get; private set; } = InputProfile.Bar;
    public FilterKernel Kernel { get; private set; } = FilterKernel.Derivative;
    public double TauMs { get; set; } = 10;
    public int NoiseSeed { get; set; } = 5;

    public double[] Input { get; } = new double[Size];
    public double[,] Weights { get; } = new double[Size, Size];

    /// <summary>Current (relaxing) output rates.</summary>
    public double[] Output { get; } = new double[Size];

    public double[] SteadyState { get; } = new double[Size];

    public static double[] KernelTaps(FilterKernel kernel) => kernel switch
    {
        FilterKernel.Derivative => [0, -1, 1, 0, 0],
        FilterKernel.CenterSurround => [0, -0.5, 1, -0.5, 0],
        FilterKernel.Smoothing => [0, 0.25, 0.5, 0.25, 0],
        _ => [0, 0, 1, 0, 0],
    };

    public void SetProfile(InputProfile profile)
    {
        Profile = profile;
        Rebuild();
    }

    public void SetKernel(FilterKernel kernel)
    {
        Kernel = kernel;
        Rebuild();
    }

    /// <summary>Relaxes the outputs towards W u.</summary>
    public void Advance(double ms)
    {
        double k = 1 - Math.Exp(-ms / TauMs);
        for (int i = 0; i < Size; i++) Output[i] += (SteadyState[i] - Output[i]) * k;
    }

    /// <summary>Restart the relaxation from v = 0 (to see the network settle).</summary>
    public void Restart() => Array.Clear(Output);

    private void Rebuild()
    {
        var random = new RandomSource(NoiseSeed);
        for (int i = 0; i < Size; i++)
        {
            double x = i / (double)(Size - 1);
            Input[i] = Profile switch
            {
                InputProfile.Step => x < 0.5 ? 0.2 : 1.0,
                InputProfile.Bar => x is > 0.35 and < 0.65 ? 1.0 : 0.2,
                InputProfile.Staircase => 0.15 + Math.Floor(x * 4) / 4.0 * 0.85,
                InputProfile.Smooth => 0.55 + 0.4 * Math.Sin(2 * Math.PI * x),
                _ => (x is > 0.35 and < 0.65 ? 1.0 : 0.2) + 0.12 * random.NextGaussian(),
            };
        }

        var taps = KernelTaps(Kernel);
        int centre = taps.Length / 2;
        for (int i = 0; i < Size; i++)
        {
            for (int j = 0; j < Size; j++) Weights[i, j] = 0;
            for (int k = 0; k < taps.Length; k++)
            {
                // Clamp at the borders so a constant input gives zero derivative everywhere.
                int j = Math.Clamp(i + k - centre, 0, Size - 1);
                Weights[i, j] += taps[k];
            }
        }

        for (int i = 0; i < Size; i++)
        {
            double sum = 0;
            for (int j = 0; j < Size; j++) sum += Weights[i, j] * Input[j];
            SteadyState[i] = sum;
        }
    }
}
