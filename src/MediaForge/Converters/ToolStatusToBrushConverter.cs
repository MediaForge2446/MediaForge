using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using MediaForge.Core.Models;

namespace MediaForge.Converters;

public sealed class ToolStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not ToolStatus status)
        {
            return new SolidColorBrush(Colors.Gray);
        }

        if (!string.IsNullOrWhiteSpace(status.ErrorMessage))
        {
            return new SolidColorBrush(Colors.Firebrick);
        }

        if (status.UpdateAvailable)
        {
            return new SolidColorBrush(Colors.Goldenrod);
        }

        return status.IsInstalled
            ? new SolidColorBrush(Colors.ForestGreen)
            : new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
