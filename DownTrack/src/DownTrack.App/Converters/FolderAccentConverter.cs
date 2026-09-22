using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace DownTrack.App.Converters;

public sealed class FolderAccentConverter : IValueConverter
{
    private static readonly Brush[] Brushes =
    [
        Create("#FF7C5CFF", "#FFFF65B7"),
        Create("#FF11C9F5", "#FF4F7BFF"),
        Create("#FFFF7A59", "#FFFFC14D"),
        Create("#FF20D7A0", "#FF36B7FF")
    ];

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var text = value as string ?? string.Empty;
        var hash = 0;

        foreach (var character in text)
        {
            hash = unchecked((hash * 31) + character);
        }

        var index = Math.Abs(hash % Brushes.Length);
        return Brushes[index];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();

    private static LinearGradientBrush Create(string start, string end) =>
        new()
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 1),
            GradientStops =
            {
                new GradientStop { Color = Parse(start), Offset = 0 },
                new GradientStop { Color = Parse(end), Offset = 1 }
            }
        };

    private static Windows.UI.Color Parse(string hex)
    {
        var hexColor = hex.TrimStart('#');
        var alpha = byte.Parse(hexColor[..2], System.Globalization.NumberStyles.HexNumber);
        var red = byte.Parse(hexColor[2..4], System.Globalization.NumberStyles.HexNumber);
        var green = byte.Parse(hexColor[4..6], System.Globalization.NumberStyles.HexNumber);
        var blue = byte.Parse(hexColor[6..8], System.Globalization.NumberStyles.HexNumber);

        return Windows.UI.Color.FromArgb(alpha, red, green, blue);
    }
}
