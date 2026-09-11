using MediaForge.Core.Enums;
using Microsoft.UI.Xaml.Data;

namespace MediaForge.Converters;

public sealed class FileKindToGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is FileItemKind.Folder ? "\uE8B7" : "\uE8A5";

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
