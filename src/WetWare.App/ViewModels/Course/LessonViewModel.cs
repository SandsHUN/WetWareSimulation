using CommunityToolkit.Mvvm.ComponentModel;
using WetWare.App.Localization;

namespace WetWare.App.ViewModels.Course;

/// <summary>A titled paragraph of the theory panel.</summary>
public sealed record ConceptBlock(string Title, string Text);

/// <summary>A displayed equation with its plain-language reading.</summary>
public sealed record FormulaLine(string Formula, string Meaning);

/// <summary>Self-check question; the answer is revealed on demand.</summary>
public sealed partial class QuizItem : ObservableObject
{
    public QuizItem(int number, string question, string answer)
    {
        Number = number;
        Question = question;
        Answer = answer;
    }

    public int Number { get; }
    public string Question { get; }
    public string Answer { get; }

    [ObservableProperty]
    private bool _isOpen;
}

/// <summary>
/// One lesson of a course module: theory texts (all from <c>{Key}.*</c> localization keys) plus an interactive lab.
/// Texts: Title, Short, Lead, C1..Cn.Title/Text, Formulas ("formula | meaning" per line), Bio, ML, Takeaways,
/// Q1..Qn/A1..An, Guide, SimpleTitle, Simple.
/// </summary>
public abstract partial class LessonViewModel : ObservableObject
{
    protected LessonViewModel(string key, string number)
    {
        Key = key;
        Number = number;
        Loc.LanguageChanged += (_, _) =>
        {
            BuildTexts();
            OnLanguageChanged();
            OnPropertyChanged(string.Empty);
        };
        BuildTexts();
    }

    protected static LocalizationService Loc => LocalizationService.Instance;

    public string Key { get; }

    /// <summary>Lesson number as printed, e.g. "6.3".</summary>
    public string Number { get; }

    /// <summary>Position inside the module (set by the module).</summary>
    public int Index { get; internal set; }

    /// <summary>The owning module's accent colour (set by the module).</summary>
    public System.Windows.Media.Brush? AccentBrush { get; internal set; }

    public string Title => Loc[$"{Key}.Title"];
    public string Short => Loc[$"{Key}.Short"];
    public string Lead => Loc[$"{Key}.Lead"];
    public string Biology => Loc.Has($"{Key}.Bio") ? Loc[$"{Key}.Bio"] : string.Empty;
    public bool HasBiology => Loc.Has($"{Key}.Bio");
    public string MachineLearning => Loc.Has($"{Key}.ML") ? Loc[$"{Key}.ML"] : string.Empty;
    public bool HasMachineLearning => Loc.Has($"{Key}.ML");

    public IReadOnlyList<ConceptBlock> Concepts { get; private set; } = Array.Empty<ConceptBlock>();
    public IReadOnlyList<FormulaLine> Formulas { get; private set; } = Array.Empty<FormulaLine>();
    public IReadOnlyList<string> Takeaways { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<QuizItem> Quiz { get; private set; } = Array.Empty<QuizItem>();
    public IReadOnlyList<string> GuideSteps { get; private set; } = Array.Empty<string>();

    public string SimpleTitle => Loc[$"{Key}.SimpleTitle"];
    public string SimpleText => Loc[$"{Key}.Simple"];

    [ObservableProperty]
    private bool _isSelected;

    public bool IsActive { get; private set; }

    /// <summary>Raised whenever the lab's visuals should be redrawn.</summary>
    public event EventHandler? Updated;

    protected void RaiseUpdated() => Updated?.Invoke(this, EventArgs.Empty);

    public void Activate()
    {
        IsActive = true;
        OnActivated();
    }

    public void Deactivate()
    {
        IsActive = false;
        OnDeactivated();
    }

    /// <summary>Called every display frame while the lesson is visible.</summary>
    public virtual void Tick(double realMs) { }

    protected virtual void OnActivated() { }

    protected virtual void OnDeactivated() { }

    protected virtual void OnLanguageChanged() { }

    protected static string F(double value, int decimals = 1) => value.ToString("F" + decimals, Loc.Culture);

    private void BuildTexts()
    {
        var concepts = new List<ConceptBlock>();
        for (int i = 1; Loc.Has($"{Key}.C{i}.Title"); i++) concepts.Add(new(Loc[$"{Key}.C{i}.Title"], Loc[$"{Key}.C{i}.Text"]));
        Concepts = concepts;

        Formulas = Lines("Formulas")
            .Select(line =>
            {
                int bar = line.IndexOf(" | ", StringComparison.Ordinal);
                return bar < 0 ? new FormulaLine(line, string.Empty) : new FormulaLine(line[..bar].Trim(), line[(bar + 3)..].Trim());
            })
            .ToList();
        Takeaways = Lines("Takeaways");
        GuideSteps = Lines("Guide");

        var quiz = new List<QuizItem>();
        for (int i = 1; Loc.Has($"{Key}.Q{i}"); i++) quiz.Add(new QuizItem(i, Loc[$"{Key}.Q{i}"], Loc[$"{Key}.A{i}"]));
        Quiz = quiz;
    }

    private IReadOnlyList<string> Lines(string suffix) =>
        Loc.Has($"{Key}.{suffix}")
            ? Loc[$"{Key}.{suffix}"].Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : Array.Empty<string>();
}
