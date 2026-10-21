using WetWare.App.Services;
using WetWare.App.ViewModels.Course.Lessons;
using WetWare.Core.Course.Unsupervised;

namespace WetWare.App.ViewModels.Course;

/// <summary>Module 6 – synapses, feedforward and recurrent networks.</summary>
public sealed class Module6ViewModel(FrameClock clock) : CourseModuleViewModel(clock, "#22D3EE", "#6366F1",
[
    new SynapseLesson(),
    new FeedforwardLesson(),
    new LinearRecurrentLesson(),
    new NonlinearRecurrentLesson(),
    new OscillationLesson(),
])
{
    public override string Key => "M6";
    public override string Glyph => "";
    public override string ModuleNumber => "6";
}

/// <summary>Module 7 – synaptic plasticity, unsupervised learning, sparse and predictive coding.</summary>
public sealed class Module7ViewModel(FrameClock clock) : CourseModuleViewModel(clock, "#A78BFA", "#F472B6", CreateLessons())
{
    /// <summary>The sparse-coding and the predictive-coding lessons share one dictionary: what 7.4 learns, 7.5 uses.</summary>
    private static LessonViewModel[] CreateLessons()
    {
        var model = new SparseCodingModel();
        return [new HebbLesson(), new CompetitiveLesson(), new MixtureLesson(), new SparseCodingLesson(model), new PredictiveCodingLesson(model)];
    }

    public override string Key => "M7";
    public override string Glyph => "";
    public override string ModuleNumber => "7";
}

/// <summary>Module 8 – supervised learning, reinforcement learning and bidirectional brain–computer interfaces.</summary>
public sealed class Module8ViewModel(FrameClock clock) : CourseModuleViewModel(clock, "#FBBF24", "#FB7185",
[
    new PerceptronLesson(),
    new TdLesson(),
    new ActorCriticLesson(),
    new BciLesson(),
])
{
    public override string Key => "M8";
    public override string Glyph => "";
    public override string ModuleNumber => "8";
}
