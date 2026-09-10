using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using MediaForge.Core.Enums;

namespace MediaForge.Converters;

public sealed class ChangeStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            ChangeStatus.Synced => new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.ForestGreen),
            ChangeStatus.Pending => new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Goldenrod),
            ChangeStatus.Failed => new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Firebrick),
            _ => new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Gray)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
