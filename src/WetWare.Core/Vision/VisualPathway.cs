using WetWare.Core.Mathematics;

namespace WetWare.Core.Vision;

/// <summary>Tunable parameters of the retina → V1 model.</summary>
public sealed class VisualPathwayParameters
{
    /// <summary>Radius (σ) of the receptive-field centre in pixels.</summary>
    public double CenterSigma { get; set; } = 1.0;

    /// <summary>Radius (σ) of the inhibitory surround in pixels.</summary>
    public double SurroundSigma { get; set; } = 3.0;

    /// <summary>Strength of the surround relative to the centre (lateral inhibition, 0–1.2).</summary>
    public double SurroundWeight { get; set; } = 1.0;

    /// <summary>Wavelength of the V1 Gabor filters (pixels).</summary>
    public double GaborWavelength { get; set; } = 8.0;

    /// <summary>Peak firing rate of a ganglion cell (Hz).</summary>
    public double MaxRateHz { get; set; } = 120.0;

    /// <summary>Response that saturates the firing rate.</summary>
    public double Gain { get; set; } = 6.0;
}

/// <summary>Outputs of every processing stage, kept for visualisation.</summary>
public sealed class VisualPathwayResult
{
    public required GrayImage Input { get; init; }

    /// <summary>Signed centre–surround (DoG) response.</summary>
    public required GrayImage CenterSurround { get; init; }

    /// <summary>Rectified ON-centre ganglion responses (light centre, dark surround).</summary>
    public required GrayImage OnCells { get; init; }

    /// <summary>Rectified OFF-centre ganglion responses (dark centre, light surround).</summary>
    public required GrayImage OffCells { get; init; }

    /// <summary>Orientation-energy maps of V1 complex cells at 0°, 45°, 90°, 135°.</summary>
    public required GrayImage[] Orientations { get; init; }

    /// <summary>Index (0–3) of the strongest orientation per pixel.</summary>
    public required int[] DominantOrientation { get; init; }

    /// <summary>Strength of the strongest orientation per pixel (edge magnitude).</summary>
    public required GrayImage EdgeStrength { get; init; }

    public static readonly double[] OrientationAngles = [0, 45, 90, 135];
}

/// <summary>
/// Early visual system model: photoreceptors → ON/OFF ganglion cells (Difference of Gaussians, lateral
/// inhibition) → V1 orientation-selective complex cells (Gabor energy, Hubel &amp; Wiesel).
/// </summary>
public static class VisualPathway
{
    public static VisualPathwayResult Process(GrayImage input, VisualPathwayParameters p)
    {
        var dog = Kernels.DifferenceOfGaussians(p.CenterSigma, p.SurroundSigma, p.SurroundWeight);
        var cs = Kernels.Convolve(input, dog);
        int n = input.Data.Length;
        var on = new GrayImage(input.Width, input.Height);
        var off = new GrayImage(input.Width, input.Height);
        for (int i = 0; i < n; i++)
        {
            float v = cs.Data[i];
            on.Data[i] = Math.Max(0, v);
            off.Data[i] = Math.Max(0, -v);
        }

        var orientations = new GrayImage[4];
        double sigma = p.GaborWavelength * 0.45;
        for (int k = 0; k < 4; k++)
        {
            // Gabor kernel θ is the direction across the bar; add 90° so the label is the bar orientation.
            double theta = (VisualPathwayResult.OrientationAngles[k] + 90) * Math.PI / 180.0;
            var even = Kernels.Convolve(input, Kernels.Gabor(theta, p.GaborWavelength, sigma, 0.6, 0));
            var odd = Kernels.Convolve(input, Kernels.Gabor(theta, p.GaborWavelength, sigma, 0.6, Math.PI / 2));
            var energy = new GrayImage(input.Width, input.Height);
            for (int i = 0; i < n; i++)
                energy.Data[i] = (float)Math.Sqrt(even.Data[i] * even.Data[i] + odd.Data[i] * odd.Data[i]);
            orientations[k] = energy;
        }

        var dominant = new int[n];
        var strength = new GrayImage(input.Width, input.Height);
        for (int i = 0; i < n; i++)
        {
            int best = 0;
            float bestValue = orientations[0].Data[i];
            float sum = bestValue;
            for (int k = 1; k < 4; k++)
            {
                float value = orientations[k].Data[i];
                sum += value;
                if (value > bestValue)
                {
                    bestValue = value;
                    best = k;
                }
            }
            dominant[i] = best;
            // Selectivity: how much the winner stands out from the mean of all orientations.
            strength.Data[i] = Math.Max(0, bestValue - 0.6f * sum / 4);
        }

        return new VisualPathwayResult
        {
            Input = input,
            CenterSurround = cs,
            OnCells = on,
            OffCells = off,
            Orientations = orientations,
            DominantOrientation = dominant,
            EdgeStrength = strength,
        };
    }

    /// <summary>
    /// Samples one time step of Poisson spikes from the ON and OFF ganglion cell mosaics.
    /// Returns the pixel indices of cells that fired.
    /// </summary>
    public static (List<int> On, List<int> Off) SampleSpikes(VisualPathwayResult result, VisualPathwayParameters p, double dtMs, RandomSource random)
    {
        var on = new List<int>();
        var off = new List<int>();
        double scale = p.MaxRateHz * dtMs / 1000.0;
        double gain = p.Gain;
        var onData = result.OnCells.Data;
        var offData = result.OffCells.Data;
        for (int i = 0; i < onData.Length; i++)
        {
            double baseline = 0.004; // spontaneous activity
            if (random.NextDouble() < scale * (Math.Min(1.0, onData[i] * gain) + baseline)) on.Add(i);
            if (random.NextDouble() < scale * (Math.Min(1.0, offData[i] * gain) + baseline)) off.Add(i);
        }
        return (on, off);
    }
}
