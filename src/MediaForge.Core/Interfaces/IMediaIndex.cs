using MediaForge.Core.Enums;
using MediaForge.Core.Models;

namespace MediaForge.Core.Interfaces;

public interface IMediaIndex
{
    IReadOnlyList<MediaIndexEntry> Entries { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<MediaIndexEntry?> FindByVideoIdAsync(string videoId, CancellationToken cancellationToken = default);
    Task UpsertAsync(MediaIndexEntry entry, CancellationToken cancellationToken = default);
    Task RemoveAsync(string videoId, CancellationToken cancellationToken = default);
}
