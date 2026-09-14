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
    private readonly IMediaIndex _mediaIndex;
    private readonly IFileSystem _fileSystem;

    public MediaImportService(
        IMediaMetadataResolver resolver,
        IStagingService staging,
        IMediaIndex mediaIndex,
        IFileSystem fileSystem)
    {
        _resolver = resolver;
        _staging = staging;
        _mediaIndex = mediaIndex;
        _fileSystem = fileSystem;
    }

    public Task<MediaResolveResult> ResolveAsync(string sourceUrl, CancellationToken cancellationToken = default)
        => _resolver.ResolveAsync(sourceUrl, cancellationToken);

    public async Task<IReadOnlyList<StagingOperation>> StageDownloadsAsync(
        IEnumerable<ResolvedMediaItem> items,
        string destinationDirectory,
        MediaFormat defaultFormat,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
            throw new ArgumentException("A destination directory is required.", nameof(destinationDirectory));

        var directory = Path.GetFullPath(destinationDirectory.Trim());
        var staged = new List<StagingOperation>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedVideoIds = new HashSet<string>(
            _staging.Operations.Select(x => x.Payload?.VideoId)
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

            var indexed = await _mediaIndex.FindByVideoIdAsync(item.VideoId, cancellationToken).ConfigureAwait(false);
            if (indexed is not null)
            {
                if (await _fileSystem.FileExistsAsync(indexed.PhysicalPath, cancellationToken).ConfigureAwait(false))
                    continue;

                await _mediaIndex.RemoveAsync(item.VideoId, cancellationToken).ConfigureAwait(false);
            }

            var format = item.DesiredFormat;
            if (!Enum.IsDefined(format)) format = defaultFormat;
            var baseName = SanitizeFileName(item.Metadata.Title);
            if (string.IsNullOrWhiteSpace(baseName)) baseName = item.VideoId;
            var extension = GetExtension(format);
            var fileName = await MakeUniqueAsync(baseName, extension, usedNames, _fileSystem, directory, cancellationToken).ConfigureAwait(false);
            var destinationPath = Path.Combine(directory, fileName);

            var operation = new StagingOperation(
                Guid.NewGuid(), DateTimeOffset.UtcNow, OperationType.Download, destinationPath,
                nameof(MediaState.Missing), nameof(MediaState.Pending),
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

    public static string GetExtension(MediaFormat format) => format switch
    {
        MediaFormat.Mp3 => ".mp3",
        MediaFormat.Mp4 => ".mp4",
        MediaFormat.Wav => ".wav",
        MediaFormat.M4a => ".m4a",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    private static async Task<string> MakeUniqueAsync(
        string baseName,
        string extension,
        ISet<string> usedNames,
        IFileSystem fileSystem,
        string directory,
        CancellationToken cancellationToken)
    {
        var index = 1;
        while (true)
        {
            var candidate = index == 1 ? baseName + extension : $"{baseName} ({index}){extension}";
            var path = Path.Combine(directory, candidate);
            if (usedNames.Add(candidate) && !await fileSystem.FileExistsAsync(path, cancellationToken).ConfigureAwait(false))
                return candidate;
            index++;
        }
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim()) builder.Append(invalid.Contains(character) ? '_' : character);
        var result = builder.ToString().Trim().TrimEnd('.', ' ');
        return result.Length > 180 ? result[..180].TrimEnd('.', ' ') : result;
    }
}
