using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using MediaForge.Core.Enums;

namespace MediaForge.Converters;

public sealed class FailedStatusToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is ChangeStatus.Failed ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
