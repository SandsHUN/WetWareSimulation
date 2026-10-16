using System.Windows;
using System.Windows.Controls;

namespace WetWare.App.Controls;

/// <summary>Card title row: icon badge, title, explanatory info tip and optional right-aligned actions.</summary>
public partial class SectionHeader : UserControl
{
    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(SectionHeader), new PropertyMetadata(""));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(SectionHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TipProperty =
        DependencyProperty.Register(nameof(Tip), typeof(string), typeof(SectionHeader),
            new PropertyMetadata(string.Empty, (d, e) => ((SectionHeader)d).Info.Visibility =
                string.IsNullOrEmpty(e.NewValue as string) ? Visibility.Collapsed : Visibility.Visible));

    public static readonly DependencyProperty ActionsProperty =
        DependencyProperty.Register(nameof(Actions), typeof(object), typeof(SectionHeader));

    public SectionHeader()
    {
        InitializeComponent();
        Info.Visibility = Visibility.Collapsed;
    }

    public string Glyph { get => (string)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Tip { get => (string)GetValue(TipProperty); set => SetValue(TipProperty, value); }
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }
}
