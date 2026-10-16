using System.Globalization;
using System.Windows.Data;

namespace WetWare.App.ViewModels;

/// <summary>Maps a 0–1 level to a bar height in pixels (parameter = full height, default 110).</summary>
public sealed class LevelToHeight : IValueConverter
{
    public static LevelToHeight Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double full = parameter is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double p) ? p : 110;
        return value is double level ? Math.Max(2, Math.Clamp(level, 0, 1) * full) : 2.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
