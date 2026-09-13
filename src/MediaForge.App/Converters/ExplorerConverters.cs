using System.Globalization;
using WpfBinding = System.Windows.Data.Binding;
using IValueConverter = System.Windows.Data.IValueConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfColor = System.Windows.Media.Color;

namespace MediaForge.App.Converters;

public sealed class DirectoryGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "▣" : "♪";

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => WpfBinding.DoNothing;
}

public sealed class PendingBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
            return new SolidColorBrush(WpfColor.FromRgb(255, 193, 7));
        return new SolidColorBrush(WpfColor.FromRgb(73, 201, 125));
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => WpfBinding.DoNothing;
}
