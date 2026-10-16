using WetWare.Core.Network;
using WetWare.Core.Neurons;
using WetWare.Core.Plasticity;
using WetWare.Core.Recognition;
using WetWare.Core.Scenarios;
using WetWare.Core.Vision;

namespace WetWare.Core.Tests;

public class LifTests
{
    [Fact]
    public void SubThresholdCurrent_SettlesAtFixedPoint()
    {
        var sim = new SingleNeuronSimulator();
        sim.Stimulus.Amplitude = 100; // 10 mV drive, below the 20 mV needed
        sim.Advance(300);
        Assert.Equal(0, sim.TotalSpikes);
        Assert.Equal(sim.Parameters.FixedPoint(100), sim.V, 1);
    }

    [Theory]
    [InlineData(250)]
    [InlineData(400)]
    public void SimulatedRate_MatchesAnalyticalFiringRate(double current)
    {
        var sim = new SingleNeuronSimulator();
        sim.Stimulus.Amplitude = current;
        sim.Advance(2000);
        double expected = sim.Parameters.FiringRate(current);
        double measured = sim.MeasuredRate;
        Assert.InRange(measured, expected * 0.9, expected * 1.1 + 1);
    }

    [Fact]
    public void Rheobase_IsThresholdDistanceOverResistance()
    {
        var p = new LifParameters();
        Assert.Equal(200, p.Rheobase, 6);
        Assert.Equal(0, p.FiringRate(199));
        Assert.True(p.FiringRate(210) > 0);
    }

    [Fact]
    public void IntegrationStep_FollowsEulerFormula()
    {
        var sim = new SingleNeuronSimulator();
        sim.Stimulus.Amplitude = 150;
        var step = sim.Step();
        Assert.Equal(step.VOld + step.DtMs * step.Derivative, step.VIntegrated, 9);
    }
}

public class StdpTests
{
    [Fact]
    public void CausalPairing_Potentiates_AntiCausal_Depresses()
    {
        var p = new StdpParameters();
        Assert.True(p.Window(5) > 0);
        Assert.True(p.Window(-5) < 0);
        Assert.True(p.Window(5) > p.Window(30));
    }

    [Fact]
    public void PairingExperiment_MovesWeightInExpectedDirection()
    {
        var ltp = new StdpPairingExperiment { DeltaT = 10, JitterMs = 0 };
        for (int i = 0; i < 20; i++) ltp.Pair();
        Assert.True(ltp.Weight > ltp.InitialWeight);

        var ltd = new StdpPairingExperiment { DeltaT = -10, JitterMs = 0 };
        for (int i = 0; i < 20; i++) ltd.Pair();
        Assert.True(ltd.Weight < ltd.InitialWeight);
    }
}

public class NetworkTests
{
    [Fact]
    public void Network_HasSmallWorldStructureAndDalesLaw()
    {
        var net = new SpikingNetwork();
        Assert.Equal(100, net.Count);
        Assert.Equal(20, Enumerable.Range(0, 100).Count(net.IsInhibitory));
        Assert.InRange(net.SynapseCount, 1100, 1200);
        for (int s = 0; s < net.SynapseCount; s++)
            Assert.Equal(!net.IsInhibitory(net.SynapsePre[s]), net.SynapsePlastic[s]);
    }

    [Fact]
    public void FreeNetwork_FiresAtBiologicalRates()
    {
        var net = new SpikingNetwork();
        net.Advance(3000);
        Assert.InRange(net.Metrics.MeanRateHz, 1, 20);
    }

    [Fact]
    public void Network_IsDeterministicForSameSeed()
    {
        var a = new SpikingNetwork();
        var b = new SpikingNetwork();
        a.Advance(500);
        b.Advance(500);
        Assert.Equal(a.TotalSpikes, b.TotalSpikes);
    }

    [Fact]
    public void AblatedNeuron_NeverFires()
    {
        var net = new SpikingNetwork();
        net.SetAblated(3, true);
        net.Advance(1000);
        Assert.DoesNotContain(net.TakeSpikes(), s => s.Neuron == 3);
    }

    [Fact]
    public void PavlovScenario_LearnsAssociation()
    {
        var scenario = new PavlovScenario();
        var net = scenario.CreateNetwork();
        double before = net.MeanWeightBetween(scenario.GroupA, scenario.GroupB);
        net.Advance(PavlovScenario.TrainingEndMs, scenario.Update);
        double after = net.MeanWeightBetween(scenario.GroupA, scenario.GroupB);
        Assert.True(after > before + 0.5, $"A→B weight {before:F2} → {after:F2}");
    }

    [Fact]
    public void SeizureScenario_IncreasesSynchrony()
    {
        var scenario = new SeizureScenario();
        var net = scenario.CreateNetwork();
        net.Advance(SeizureScenario.SeizureStartMs, scenario.Update);
        double normal = net.Metrics.SynchronyIndex;
        net.Advance(6000, scenario.Update);
        double seizure = net.Metrics.SynchronyIndex;
        Assert.True(seizure > normal * 1.5, $"χ {normal:F2} → {seizure:F2}");
    }
}

public class VisionTests
{
    [Fact]
    public void CenterSurround_IgnoresUniformLight()
    {
        var img = new GrayImage(32, 32);
        Array.Fill(img.Data, 0.7f);
        var result = VisualPathway.Process(img, new VisualPathwayParameters());
        Assert.All(result.CenterSurround.Data, v => Assert.InRange(v, -1e-3f, 1e-3f));
    }

    [Fact]
    public void VerticalGrating_ActivatesVerticalOrientationMost()
    {
        var img = StimulusLibrary.Create(VisualStimulus.Grating, angleDegrees: 0);
        var result = VisualPathway.Process(img, new VisualPathwayParameters());
        double[] energy = result.Orientations.Select(o => (double)o.Data.Sum()).ToArray();
        // A grating varying along x (angle 0) consists of vertical bars → 90° channel.
        Assert.Equal(2, Array.IndexOf(energy, energy.Max()));
    }
}

public class RecognitionTests
{
    [Fact]
    public void Templates_AreDistinct()
    {
        for (int a = 0; a < 10; a++)
            for (int b = a + 1; b < 10; b++)
                Assert.NotEqual(DigitPatterns.Template(a), DigitPatterns.Template(b));
    }

    [Fact]
    public void Classifier_LearnsDigitsWellAboveChance()
    {
        var classifier = new SpikingClassifier(20, 1);
        for (int i = 0; i < 400; i++) classifier.TrainStep(0.04);
        Assert.True(classifier.Evaluate(5, 0.05) > 0.6);
    }
}
