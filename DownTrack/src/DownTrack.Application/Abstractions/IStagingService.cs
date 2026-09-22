using DownTrack.Core.Models;
using DownTrack.Core.State;

namespace DownTrack.Application.Abstractions;

public interface IStagingService
{
    StagingSnapshot Snapshot { get; }

    Guid StageCreateFolder(
        Guid? parentId,
        string name,
        string relativePath);

    Guid StageDownload(
        Guid? parentId,
        string fileName,
        string relativePath);

    bool Undo(Guid operationId);

    void Clear();

    bool HasPendingChanges { get; }
}
