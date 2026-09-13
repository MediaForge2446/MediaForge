using System.Globalization;
using System.Windows;
using WpfBinding = System.Windows.Data.Binding;
using IValueConverter = System.Windows.Data.IValueConverter;

namespace MediaForge.App.Converters;

public sealed class BooleanNotConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool boolean && !boolean;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool boolean ? !boolean : WpfBinding.DoNothing;
}

public sealed class BooleanToVisibilityConverterFallback : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility visibility ? visibility == Visibility.Visible : WpfBinding.DoNothing;
}

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is false ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility visibility ? visibility != Visibility.Visible : WpfBinding.DoNothing;
}
