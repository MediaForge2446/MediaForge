using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MediaForge.App.Converters;

public sealed class DirectoryGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "▣" : "♪";

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class PendingBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
            return new SolidColorBrush(Color.FromRgb(255, 193, 7));
        return new SolidColorBrush(Color.FromRgb(73, 201, 125));
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
