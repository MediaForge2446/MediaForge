using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MediaForge.App.Converters;

public sealed class BooleanNotConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool boolean && !boolean;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool boolean ? !boolean : Binding.DoNothing;
}

public sealed class BooleanToVisibilityConverterFallback : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility visibility ? visibility == Visibility.Visible : Binding.DoNothing;
}

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is false ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility visibility ? visibility != Visibility.Visible : Binding.DoNothing;
}
