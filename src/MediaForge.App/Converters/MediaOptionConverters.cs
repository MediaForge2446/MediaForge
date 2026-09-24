using System.Globalization;
using System.Windows.Data;
using MediaForge.Core.Enums;

namespace MediaForge.App.Converters;

public sealed class MediaFormatDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is MediaFormat format
            ? format switch
            {
                MediaFormat.Mp3 => "MP3 · אודיו",
                MediaFormat.Mp4 => "MP4 · וידאו",
                MediaFormat.Wav => "WAV · אודיו",
                MediaFormat.M4a => "M4A · אודיו",
                _ => value.ToString() ?? string.Empty
            }
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class MediaQualityDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is MediaQuality quality
            ? quality switch
            {
                MediaQuality.Standard128K => "128 kbps · חסכוני",
                MediaQuality.High192K => "192 kbps · מומלץ",
                MediaQuality.VeryHigh256K => "256 kbps · גבוה",
                MediaQuality.Maximum320K => "320 kbps · מקסימלי",
                _ => value.ToString() ?? string.Empty
            }
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class MediaVideoQualityDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is MediaVideoQuality quality
            ? quality switch
            {
                MediaVideoQuality.DataSaver480p => "480p · חסכוני",
                MediaVideoQuality.Balanced720p => "720p · מומלץ",
                MediaVideoQuality.High1080p => "1080p · גבוה",
                MediaVideoQuality.BestAvailable => "מקור · מקסימלי",
                _ => value.ToString() ?? string.Empty
            }
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
