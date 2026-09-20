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
        IMediaIndex? mediaIndex = null,
        IFileSystem? fileSystem = null)
    {
        _resolver = resolver;
        _staging = staging;
        _mediaIndex = mediaIndex ?? NullMediaIndex.Instance;
        _fileSystem = fileSystem ?? NullFileSystem.Instance;
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

            var format = Enum.IsDefined(item.DesiredFormat) ? item.DesiredFormat : defaultFormat;
            var baseName = SanitizeFileName(item.Metadata.Title);
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = item.VideoId;

            var extension = GetExtension(format);
            var fileName = await MakeUniqueAsync(
                baseName,
                extension,
                usedNames,
                _fileSystem,
                directory,
                cancellationToken).ConfigureAwait(false);

            var destinationPath = Path.Combine(directory, fileName);
            staged.Add(new StagingOperation(
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
                    VideoId: item.VideoId)));
        }

        if (staged.Count == 0)
            return staged;

        await _staging.StageManyAsync(staged, cancellationToken).ConfigureAwait(false);
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

    private sealed class NullMediaIndex : IMediaIndex
    {
        public static NullMediaIndex Instance { get; } = new();
        public IReadOnlyList<MediaIndexEntry> Entries => [];
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<MediaIndexEntry?> FindByVideoIdAsync(string videoId, CancellationToken cancellationToken = default) => Task.FromResult<MediaIndexEntry?>(null);
        public Task UpsertAsync(MediaIndexEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(string videoId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NullFileSystem : IFileSystem
    {
        public static NullFileSystem Instance { get; } = new();
        public Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(string path, bool recursive = false, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RenameAsync(string path, string newName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
