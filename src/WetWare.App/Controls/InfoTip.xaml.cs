using System.Windows;
using System.Windows.Controls;

namespace WetWare.App.Controls;

/// <summary>Small "i" badge that explains the neighbouring element in a tooltip.</summary>
public partial class InfoTip : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(InfoTip), new PropertyMetadata(string.Empty));

    public InfoTip() => InitializeComponent();

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
