using MediaForge.Core.Enums;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace MediaForge.Converters;

public sealed class ChangeStatusToBrushConverter : IValueConverter
{
    private static readonly Brush SyncedBrush = new SolidColorBrush(Microsoft.UI.Colors.SeaGreen);
    private static readonly Brush PendingBrush = new SolidColorBrush(Microsoft.UI.Colors.Goldenrod);
    private static readonly Brush FailedBrush = new SolidColorBrush(Microsoft.UI.Colors.IndianRed);

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not ChangeStatus status)
        {
            return PendingBrush;
        }

        return status switch
        {
            ChangeStatus.Synced => SyncedBrush,
            ChangeStatus.Pending => PendingBrush,
            ChangeStatus.Failed => FailedBrush,
            _ => PendingBrush
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
