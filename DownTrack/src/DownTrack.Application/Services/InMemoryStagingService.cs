using DownTrack.Application.Abstractions;
using DownTrack.Core.Models;
using DownTrack.Core.State;

namespace DownTrack.Application.Services;

public sealed class InMemoryStagingService : IStagingService
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, LibraryNode> _nodes = [];
    private readonly List<StageOperation> _operations = [];

    public StagingSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                return new StagingSnapshot(
                    _nodes.Values.ToArray(),
                    _operations.ToArray());
            }
        }
    }

    public bool HasPendingChanges
    {
        get
        {
            lock (_gate)
            {
                return _operations.Any(x => x.State == StageOperationState.Pending);
            }
        }
    }

    public Guid StageCreateFolder(
        Guid? parentId,
        string name,
        string relativePath)
    {
        return StageNode(
            LibraryNode.CreateFolder(parentId, name, relativePath),
            StageOperationKind.CreateFolder,
            relativePath);
    }

    public Guid StageDownload(
        Guid? parentId,
        string fileName,
        string relativePath)
    {
        return StageNode(
            LibraryNode.CreateMedia(parentId, fileName, relativePath),
            StageOperationKind.Download,
            relativePath);
    }

    public bool Undo(Guid operationId)
    {
        lock (_gate)
        {
            var index = _operations.FindIndex(x => x.Id == operationId);

            if (index < 0)
            {
                return false;
            }

            var operation = _operations[index];

            if (operation.State != StageOperationState.Pending)
            {
                return false;
            }

            _operations.RemoveAt(index);

            if (operation.NodeId is Guid nodeId)
            {
                _nodes.Remove(nodeId);

                var dependentNodeIds = _operations
                    .Where(x => x.DependsOn?.Contains(operation.Id) == true)
                    .Select(x => x.NodeId)
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .ToArray();

                foreach (var dependentNodeId in dependentNodeIds)
                {
                    _nodes.Remove(dependentNodeId);
                }
            }

            return true;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _nodes.Clear();
            _operations.Clear();
        }
    }

    private Guid StageNode(
        LibraryNode node,
        StageOperationKind kind,
        string targetPath)
    {
        lock (_gate)
        {
            var dependencies = _operations
                .Where(x =>
                    x.State == StageOperationState.Pending &&
                    IsParentOperation(x.TargetPath, targetPath))
                .Select(x => x.Id)
                .ToArray();

            var operation = new StageOperation(
                Guid.NewGuid(),
                kind,
                targetPath,
                NodeId: node.Id,
                CreatedAt: DateTimeOffset.UtcNow,
                DependsOn: dependencies);

            _nodes[node.Id] = node;
            _operations.Add(operation);

            return operation.Id;
        }
    }

    private static bool IsParentOperation(
        string candidate,
        string target)
    {
        var normalizedCandidate =
            Path.TrimEndingDirectorySeparator(
                candidate.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));

        var normalizedTarget =
            Path.TrimEndingDirectorySeparator(
                target.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));

        if (string.Equals(normalizedCandidate, normalizedTarget, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return normalizedTarget.StartsWith(
            normalizedCandidate + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }
}
