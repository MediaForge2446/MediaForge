using System.Text;
using MediaForge.Application.Abstractions;
using MediaForge.Core.Enums;
using MediaForge.Core.Interfaces;
using MediaForge.Core.Models;

namespace MediaForge.Application.Downloads;

public sealed class MediaImportService
{
    private readonly IMediaMetadataResolver _resolver;
    private readonly IStagingService _staging;

    public MediaImportService(IMediaMetadataResolver resolver, IStagingService staging)
    {
        _resolver = resolver;
        _staging = staging;
    }

    public Task<MediaResolveResult> ResolveAsync(
        string sourceUrl,
        CancellationToken cancellationToken = default)
        => _resolver.ResolveAsync(sourceUrl, cancellationToken);

    public async Task<IReadOnlyList<StagingOperation>> StageDownloadsAsync(
        IEnumerable<ResolvedMediaItem> items,
        string destinationDirectory,
        MediaFormat format,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
            throw new ArgumentException("A destination directory is required.", nameof(destinationDirectory));

        var directory = Path.GetFullPath(destinationDirectory.Trim());
        var staged = new List<StagingOperation>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedVideoIds = new HashSet<string>(
            _staging.Operations
                .Select(x => x.Payload?.VideoId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!),
            StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(item.VideoId))
                throw new InvalidOperationException("A media item is missing its source VideoId.");
            if (!usedVideoIds.Add(item.VideoId))
                continue;

            var baseName = SanitizeFileName(item.Metadata.Title);
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = item.VideoId;

            var extension = format switch
            {
                MediaFormat.Mp3 => ".mp3",
                MediaFormat.Mp4 => ".mp4",
                _ => throw new ArgumentOutOfRangeException(nameof(format))
            };

            var fileName = MakeUnique(baseName, extension, usedNames);
            var destinationPath = Path.Combine(directory, fileName);
            var operation = new StagingOperation(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                OperationType.Download,
                destinationPath,
                nameof(MediaState.Missing),
                nameof(MediaState.Pending),
                new StagingPayload(
                    SourceUrl: item.SourceUrl,
                    DestinationPath: destinationPath,
                    DesiredFormat: format,
                    VideoId: item.VideoId));

            await _staging.StageAsync(operation, cancellationToken).ConfigureAwait(false);
            staged.Add(operation);
        }

        return staged;
    }

    private static string MakeUnique(string baseName, string extension, ISet<string> usedNames)
    {
        var candidate = baseName + extension;
        var index = 2;
        while (!usedNames.Add(candidate))
            candidate = $"{baseName} ({index++}){extension}";
        return candidate;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
            builder.Append(invalid.Contains(character) ? '_' : character);

        var result = builder.ToString().Trim().TrimEnd('.', ' ');
        if (result.Length > 180)
            result = result[..180].TrimEnd('.', ' ');
        return result;
    }
}
