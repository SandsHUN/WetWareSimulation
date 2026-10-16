using System.Windows;
using System.Windows.Controls;
using WetWare.App.Localization;

namespace WetWare.App.Controls;

/// <summary>Labelled parameter slider with unit, live value read-out and explanatory tooltip.</summary>
public partial class ParamSlider : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(ParamSlider), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TipProperty =
        DependencyProperty.Register(nameof(Tip), typeof(string), typeof(ParamSlider),
            new PropertyMetadata(string.Empty, (d, e) => ((ParamSlider)d).Info.Visibility =
                string.IsNullOrEmpty(e.NewValue as string) ? Visibility.Collapsed : Visibility.Visible));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(ParamSlider),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ParamSlider)d).UpdateText()));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(ParamSlider), new PropertyMetadata(0.0));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(ParamSlider), new PropertyMetadata(100.0));

    public static readonly DependencyProperty StepProperty =
        DependencyProperty.Register(nameof(Step), typeof(double), typeof(ParamSlider), new PropertyMetadata(1.0));

    public static readonly DependencyProperty SnapProperty =
        DependencyProperty.Register(nameof(Snap), typeof(bool), typeof(ParamSlider), new PropertyMetadata(true));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(ParamSlider),
            new PropertyMetadata(string.Empty, (d, _) => ((ParamSlider)d).UpdateText()));

    public static readonly DependencyProperty DecimalsProperty =
        DependencyProperty.Register(nameof(Decimals), typeof(int), typeof(ParamSlider),
            new PropertyMetadata(1, (d, _) => ((ParamSlider)d).UpdateText()));

    public ParamSlider()
    {
        InitializeComponent();
        Info.Visibility = Visibility.Collapsed;
        Loaded += (_, _) =>
        {
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            UpdateText();
        };
        Unloaded += (_, _) => LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
    }

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Tip { get => (string)GetValue(TipProperty); set => SetValue(TipProperty, value); }
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public bool Snap { get => (bool)GetValue(SnapProperty); set => SetValue(SnapProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public int Decimals { get => (int)GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }

    private void OnLanguageChanged(object? sender, EventArgs e) => UpdateText();

    private void UpdateText()
    {
        if (ValueText is null) return;
        string number = Value.ToString("F" + Math.Max(0, Decimals), LocalizationService.Instance.Culture);
        ValueText.Text = string.IsNullOrEmpty(Unit) ? number : $"{number} {Unit}";
    }
}
