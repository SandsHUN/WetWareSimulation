using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WetWare.App.Controls;

/// <summary>
/// One variable of an interactive equation: math symbol, live value underneath, highlight and
/// biological explanation on hover.
/// </summary>
public partial class EquationTerm : UserControl
{
    public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(nameof(Symbol), typeof(string), typeof(EquationTerm));
    public static readonly DependencyProperty SubProperty = DependencyProperty.Register(nameof(Sub), typeof(string), typeof(EquationTerm));
    public static readonly DependencyProperty ValueTextProperty = DependencyProperty.Register(nameof(ValueText), typeof(string), typeof(EquationTerm));
    public static readonly DependencyProperty TipProperty = DependencyProperty.Register(nameof(Tip), typeof(string), typeof(EquationTerm));
    public static readonly DependencyProperty ValueBrushProperty =
        DependencyProperty.Register(nameof(ValueBrush), typeof(Brush), typeof(EquationTerm), new PropertyMetadata(Brushes.LightSkyBlue));

    public EquationTerm() => InitializeComponent();

    public string Symbol { get => (string)GetValue(SymbolProperty); set => SetValue(SymbolProperty, value); }
    public string Sub { get => (string)GetValue(SubProperty); set => SetValue(SubProperty, value); }
    public string ValueText { get => (string)GetValue(ValueTextProperty); set => SetValue(ValueTextProperty, value); }
    public string Tip { get => (string)GetValue(TipProperty); set => SetValue(TipProperty, value); }
    public Brush ValueBrush { get => (Brush)GetValue(ValueBrushProperty); set => SetValue(ValueBrushProperty, value); }
}
