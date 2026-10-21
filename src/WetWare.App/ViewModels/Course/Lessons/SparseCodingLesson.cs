using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WetWare.Core.Course.Unsupervised;

namespace WetWare.App.ViewModels.Course.Lessons;

/// <summary>7.4 – sparse coding of natural image patches: the dictionary learns V1-like local, oriented features.</summary>
public sealed partial class SparseCodingLesson : LessonViewModel
{
    private (double[] Variance, double[][] Components)? _pca;
    private int _frames;

    public SparseCodingLesson(SparseCodingModel model) : base("M7.Sparse", "7.4")
    {
        Model = model;
        Encode();
    }

    public SparseCodingModel Model { get; }

    /// <summary>Sparse code v of the current patch, its prediction Gv and the residual u − Gv.</summary>
    public double[] Code { get; private set; } = new double[SparseCodingModel.Bases];
    public double[] Prediction { get; private set; } = new double[SparseCodingModel.Dim];
    public double[] Residual { get; private set; } = new double[SparseCodingModel.Dim];

    public (double[] Variance, double[][] Components) Pca => _pca ??= Model.PrincipalComponents();

    [ObservableProperty] private double _lambda = 0.8;
    [ObservableProperty] private double _learningRate = 0.06;
    [ObservableProperty] private double _speed = 2;
    [ObservableProperty] private bool _isLearning;
    [ObservableProperty] private bool _showPca;
    [ObservableProperty] private string _codeText = "";
    [ObservableProperty] private string _progress = "";

    partial void OnLambdaChanged(double value)
    {
        Model.Lambda = value;
        Encode();
        RaiseUpdated();
    }

    partial void OnLearningRateChanged(double value) => Model.LearningRate = value;

    partial void OnShowPcaChanged(bool value) => RaiseUpdated();

    public override void Tick(double realMs)
    {
        if (!IsLearning) return;
        for (int s = 0; s < (int)Speed; s++) Model.LearnStep();
        if (++_frames % 6 == 0) Encode();
        RefreshTexts();
        RaiseUpdated();
    }

    /// <summary>The first visit starts learning right away, so the features can be seen emerging.</summary>
    protected override void OnActivated()
    {
        if (Model.Steps == 0) IsLearning = true;
    }

    protected override void OnDeactivated() => IsLearning = false;

    [RelayCommand]
    private void ToggleLearning() => IsLearning = !IsLearning;

    /// <summary>Runs 250 learning steps at once (about a second) to skip ahead.</summary>
    [RelayCommand]
    private void FastForward()
    {
        for (int s = 0; s < 250; s++) Model.LearnStep();
        Encode();
        RaiseUpdated();
    }

    [RelayCommand]
    private void Reset()
    {
        IsLearning = false;
        Model.Reset(Environment.TickCount);
        Encode();
        RaiseUpdated();
    }

    [RelayCommand]
    private void NextPatch()
    {
        Model.NextPatch();
        Encode();
        RaiseUpdated();
    }

    public void PickPatch(int x, int y)
    {
        var (img, _, _) = Model.CurrentPatchLocation;
        Model.SelectPatch(img, x, y);
        Encode();
        RaiseUpdated();
    }

    private void Encode()
    {
        var u = Model.CurrentPatch;
        Code = Model.Infer(u);
        Prediction = Model.Predict(Code);
        Residual = u.Zip(Prediction, (a, b) => a - b).ToArray();
        RefreshTexts();
    }

    protected override void OnLanguageChanged() => RefreshTexts();

    private void RefreshTexts()
    {
        int active = Code.Count(v => v != 0);
        var (rec, sparse) = Model.Energy(Model.CurrentPatch, Code);
        double power = Model.CurrentPatch.Sum(v => v * v);
        CodeText = Loc.Format("M7.Sparse.CodeText", active, SparseCodingModel.Bases, F(100 * (1 - 2 * rec / Math.Max(power, 1e-9)), 0));
        Progress = Loc.Format("M7.Sparse.Progress", Model.Steps, Model.Steps * Model.BatchSize,
            Model.ErrorHistory.Count > 0 ? F(Model.ErrorHistory.Last, 3) : "—");
    }
}
