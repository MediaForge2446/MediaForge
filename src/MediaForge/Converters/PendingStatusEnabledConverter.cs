using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using MediaForge.Core.Enums;

namespace MediaForge.Converters;

public sealed class PendingStatusEnabledConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is ChangeStatus status && status == ChangeStatus.Pending;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
