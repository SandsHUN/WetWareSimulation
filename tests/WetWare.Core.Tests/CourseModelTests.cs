using WetWare.Core.Course.Bci;
using WetWare.Core.Course.RateNetworks;
using WetWare.Core.Course.Reinforcement;
using WetWare.Core.Course.Supervised;
using WetWare.Core.Course.Synapses;
using WetWare.Core.Course.Unsupervised;
using WetWare.Core.Mathematics;

namespace WetWare.Core.Tests;

public class SynapseModelTests
{
    [Fact]
    public void AlphaKernel_PeaksAtOneAtTau()
    {
        var filter = new SynapticFilter { Kernel = SynapseKernel.Alpha, TauMs = 10 };
        filter.Spike();
        double peak = 0, peakTime = 0;
        for (int i = 1; i <= 600; i++)
        {
            filter.Step(0.1);
            if (filter.P > peak)
            {
                peak = filter.P;
                peakTime = i * 0.1;
            }
        }
        Assert.Equal(1.0, peak, 2);
        Assert.InRange(peakTime, 9.5, 10.5);
    }

    [Fact]
    public void ReversalPotential_DecidesExcitationOrInhibition()
    {
        double MeanV(double reversal)
        {
            var sim = new ConductanceSynapseSimulator { ReversalPotential = reversal, BackgroundMv = 8, Strength = 2 };
            sim.Advance(2000);
            return Enumerable.Range(0, sim.Voltage.Count).Average(i => sim.Voltage[i]);
        }

        double rest = new ConductanceSynapseSimulator().RestingPotential + 8;
        Assert.True(MeanV(0) > rest + 1);
        Assert.True(MeanV(-80) < rest - 1);
    }

    [Theory]
    [InlineData(true, 0.3, 0.5)]
    [InlineData(false, 0.0, 0.12)]
    public void CoupledPair_ExcitationAlternates_InhibitionSynchronizes(bool excitatory, double min, double max)
    {
        var pair = new CoupledNeuronPair { Excitatory = excitatory };
        pair.Advance(4000);
        Assert.InRange(pair.RelativePhase, min, max);
    }
}

public class RateNetworkTests
{
    [Fact]
    public void DerivativeKernel_RespondsOnlyAtEdges()
    {
        var filter = new FeedforwardFilter();
        filter.SetProfile(InputProfile.Step);
        filter.SetKernel(FilterKernel.Derivative);
        int nonZero = filter.SteadyState.Count(v => Math.Abs(v) > 1e-9);
        Assert.Equal(1, nonZero);
        Assert.True(filter.SteadyState.Max() > 0.5);
    }

    [Fact]
    public void Eigenmode_IntegratesAtLambdaOne_AndLeaksBelow()
    {
        var integrator = new EigenmodeIntegrator { Lambda = 1 };
        integrator.Pulse(1);
        integrator.Advance(1000);
        Assert.Equal(50.0 / 20.0, integrator.C, 1);

        var leaky = new EigenmodeIntegrator { Lambda = 0.5 };
        leaky.Pulse(1);
        leaky.Advance(1000);
        Assert.True(Math.Abs(leaky.C) < 0.01);
    }

    [Fact]
    public void Eigenmode_SteadyStateGain_IsOneOverOneMinusLambda()
    {
        var mode = new EigenmodeIntegrator { Lambda = 0.9, Sustained = 0.1 };
        mode.Advance(5000);
        Assert.Equal(0.1 / (1 - 0.9), mode.C, 2);
        Assert.Equal(EigenmodeIntegrator.StepResponse(0.9, 20, 0.1, 5000), mode.C, 2);
    }

    [Fact]
    public void Ring_WinnerTakesAll_KeepsTheStrongerCue()
    {
        var ring = new RingNetwork { Nonlinearity = RateNonlinearity.Saturating, J1 = 2.5, J0 = 4 };
        ring.Bumps.Clear();
        ring.Bumps.Add(new RingBump(-90, 10));
        ring.Bumps.Add(new RingBump(90, 9));
        ring.Advance(800);
        Assert.InRange(ring.PopulationVector, -110, -70);
        int loser = Array.FindIndex(ring.Preferred, p => p >= 90);
        Assert.True(ring.Rates[loser] < 0.5);
    }

    [Fact]
    public void Ring_StrongRecurrence_HoldsAMemoryAfterTheCue()
    {
        var ring = new RingNetwork { Nonlinearity = RateNonlinearity.Saturating, J1 = 3, J0 = 1 };
        ring.Bumps[0] = new RingBump(45, 10);
        ring.Advance(300);
        ring.InputOn = false;
        ring.Advance(1500);
        Assert.True(ring.Rates.Max() > 20);
        Assert.InRange(ring.PopulationVector, 30, 60);
    }

    [Fact]
    public void ExcitatoryInhibitory_HopfAtFortyMilliseconds()
    {
        var net = new ExcitatoryInhibitoryNetwork();
        Assert.Equal(26.67, net.FixedPoint.E, 2);
        Assert.Equal(16.67, net.FixedPoint.I, 2);
        Assert.Equal(40, net.CriticalTauI, 6);
        net.TauI = 30;
        Assert.True(net.IsStable);
        Assert.True(net.PredictedFrequency > 0);
        net.TauI = 50;
        Assert.False(net.IsStable);

        // Above the bifurcation the activity keeps oscillating instead of settling.
        net.Reset();
        net.Advance(3000);
        double min = double.MaxValue, max = double.MinValue;
        for (int i = 0; i < net.TraceE.Count; i++)
        {
            min = Math.Min(min, net.TraceE[i]);
            max = Math.Max(max, net.TraceE[i]);
        }
        Assert.True(max - min > 10);
    }
}

public class UnsupervisedLearningTests
{
    [Fact]
    public void Oja_NormalizesAndFindsThePrincipalAxis()
    {
        var learner = new HebbianLearner { Rule = HebbianRule.Oja, Alpha = 1 };
        learner.Step(6000);
        Assert.Equal(1.0, learner.Norm, 1);
        Assert.True(learner.AngleToPrincipal < 6);
    }

    [Fact]
    public void Hebb_GrowsWithoutBound()
    {
        var learner = new HebbianLearner { Rule = HebbianRule.Hebb };
        learner.Step(3000);
        Assert.True(learner.Norm > 100);
    }

    [Fact]
    public void KMeans_DistortionNeverIncreases()
    {
        var learner = new PrototypeLearner { Mode = PrototypeMode.KMeans };
        learner.SetCount(3);
        double previous = learner.Distortion;
        for (int i = 0; i < 10; i++)
        {
            learner.Step();
            Assert.True(learner.Distortion <= previous + 1e-9);
            previous = learner.Distortion;
        }
    }

    [Fact]
    public void Em_LogLikelihoodNeverDecreases()
    {
        var em = new GaussianMixtureEm();
        for (int i = 0; i < 25; i++) em.Iterate();
        var ll = em.LogLikelihoodHistory;
        for (int i = 1; i < ll.Count; i++) Assert.True(ll[i] >= ll[i - 1] - 1e-9);
        Assert.Equal(1.0, em.Components.Sum(c => c.Pi), 6);
    }

    [Fact]
    public void SymmetricEigen_ReconstructsTheMatrix()
    {
        var random = new RandomSource(3);
        int n = 6;
        var a = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = i; j < n; j++)
                a[i, j] = a[j, i] = random.NextGaussian();
        var (values, vectors) = SymmetricEigen.Decompose(a);
        for (int k = 1; k < n; k++) Assert.True(values[k] <= values[k - 1]);
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                double sum = 0;
                for (int k = 0; k < n; k++) sum += vectors[i, k] * values[k] * vectors[j, k];
                Assert.Equal(a[i, j], sum, 6);
            }
        }
    }

    [Fact]
    public void SparseCoding_LearningReducesErrorWithASparseCode()
    {
        var model = new SparseCodingModel();
        model.LearnStep();
        double first = model.ErrorHistory.Last;
        for (int i = 0; i < 200; i++) model.LearnStep();
        Assert.True(model.ErrorHistory.Last < first * 0.6);
        var code = model.Infer(model.CurrentPatch);
        Assert.True(code.Count(v => v != 0) < SparseCodingModel.Bases / 3);
    }
}

public class SupervisedAndReinforcementTests
{
    [Fact]
    public void Perceptron_SolvesAnd_ButNotXor()
    {
        var and = PlaneDatasets.Generate(PlaneDataset.And);
        var perceptron = new Perceptron();
        perceptron.Reset();
        for (int i = 0; i < 2000; i++) perceptron.Step(and);
        Assert.Equal(1.0, perceptron.Accuracy(and));

        var xor = PlaneDatasets.Generate(PlaneDataset.Xor);
        perceptron.Reset();
        for (int i = 0; i < 2000; i++) perceptron.Step(xor);
        Assert.True(perceptron.Accuracy(xor) < 1.0);
    }

    [Fact]
    public void Mlp_SolvesXor()
    {
        var xor = PlaneDatasets.Generate(PlaneDataset.Xor);
        var mlp = new MultilayerPerceptron();
        for (int e = 0; e < 400; e++) mlp.TrainEpoch(xor);
        Assert.Equal(1.0, mlp.Accuracy(xor));
    }

    [Fact]
    public void Td_ErrorMovesToTheCue_AndOmissionGivesADip()
    {
        var td = new TdConditioning();
        var first = td.RunTrial();
        Assert.Equal(1.0, td.Peaks(first).AtReward, 6);
        for (int i = 0; i < 300; i++) td.RunTrial();
        var (cue, reward) = td.Peaks(td.Last!);
        Assert.True(cue > 0.9);
        Assert.True(Math.Abs(reward) < 0.05);
        var omission = td.RunTrial(TrialKind.Omission);
        Assert.True(omission.Delta[td.RewardTime] < -0.9);
    }

    [Fact]
    public void BarnMaze_PolicyEvaluation_MatchesRandomPolicyValues()
    {
        var maze = new BarnMaze { ActorLearns = false, CriticRate = 0.05 };
        for (int i = 0; i < 3000; i++) maze.RunTrial();
        // Single TD estimates fluctuate with the random rewards; their average over many trials is the true value.
        var recent = maze.ValueHistory.Skip(maze.ValueHistory.Count - 800).ToList();
        var target = maze.RandomPolicyValues;
        Assert.InRange(recent.Average(h => h.VA), target.A - 0.3, target.A + 0.3);
        Assert.InRange(recent.Average(h => h.VB), target.B - 0.3, target.B + 0.3);
        Assert.InRange(recent.Average(h => h.VC), target.C - 0.3, target.C + 0.3);
    }

    [Fact]
    public void BarnMaze_ActorCritic_FindsTheBestRoute()
    {
        var maze = new BarnMaze();
        for (int i = 0; i < 400; i++) maze.RunTrial();
        Assert.True(maze.ProbabilityRight(BarnState.A) < 0.2); // left to B
        Assert.True(maze.ProbabilityRight(BarnState.B) > 0.8); // right to the 5
        Assert.InRange(maze.Value(BarnState.A), 4, 5.5);
    }
}

public class BciTests
{
    [Fact]
    public void FesBridge_RestoresMovementOnlyWhenEnabled()
    {
        var on = new FesBridge();
        on.Advance(12000);
        var off = new FesBridge { BciEnabled = false };
        off.Advance(12000);
        Assert.True(on.TrackingError < off.TrackingError * 0.6);
        Assert.Equal(0, off.WristAngle, 6);
    }

    [Fact]
    public void Conditioning_ShortDelaysStrengthenMost_RandomTimingDoesNot()
    {
        double Ratio(double delay, bool random = false)
        {
            var c = new SpikeTriggeredConditioning { DelayMs = delay, RandomControl = random };
            c.Condition(10);
            return c.Strength / c.InitialStrength;
        }

        double shortDelay = Ratio(10), longDelay = Ratio(90), control = Ratio(10, random: true);
        Assert.True(shortDelay > 1.3);
        Assert.True(longDelay < shortDelay);
        Assert.InRange(control, 0.9, 1.1);
    }
}
