using MediaForge.Core.Enums;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace MediaForge.Converters;

public sealed class PendingStatusVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is ChangeStatus.Pending ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
