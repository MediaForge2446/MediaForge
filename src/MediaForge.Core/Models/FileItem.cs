using MediaForge.Core.Enums;
using System.Globalization;

namespace MediaForge.Core.Models;

public sealed record FileItem
{
    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public required FileItemKind Kind { get; init; }

    public long? SizeBytes { get; init; }

    public DateTimeOffset LastModifiedUtc { get; init; }

    public ChangeStatus Status { get; init; } = ChangeStatus.Synced;

    public string Glyph => Kind == FileItemKind.Folder ? "\uE8B7" : "\uE8A5";

    public string DisplaySize => Kind == FileItemKind.Folder
        ? "Folder"
        : FormatSize(SizeBytes);

    public string StatusLabel => Status switch
    {
        ChangeStatus.Synced => "Synced",
        ChangeStatus.Pending => "Pending",
        ChangeStatus.Failed => "Failed",
        _ => "Unknown"
    };

    private static string FormatSize(long? sizeBytes)
    {
        if (!sizeBytes.HasValue)
        {
            return "—";
        }

        const double unit = 1024d;
        var value = sizeBytes.Value;
        return value switch
        {
            < 1024 => $"{value.ToString("N0", CultureInfo.CurrentCulture)} B",
            < 1024 * 1024 => $"{(value / unit):N1} KB",
            < 1024L * 1024 * 1024 => $"{(value / unit / unit):N1} MB",
            < 1024L * 1024 * 1024 * 1024 => $"{(value / unit / unit / unit):N1} GB",
            _ => $"{(value / unit / unit / unit / unit):N1} TB"
        };
    }
}
