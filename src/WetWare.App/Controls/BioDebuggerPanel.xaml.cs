using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace WetWare.App.Controls;

/// <summary>Renders the numbered computation steps of the Bio-Debugger.</summary>
public partial class BioDebuggerPanel : UserControl
{
    public static readonly DependencyProperty LinesProperty =
        DependencyProperty.Register(nameof(Lines), typeof(IEnumerable), typeof(BioDebuggerPanel));

    public BioDebuggerPanel() => InitializeComponent();

    public IEnumerable? Lines
    {
        get => (IEnumerable?)GetValue(LinesProperty);
        set => SetValue(LinesProperty, value);
    }
}
