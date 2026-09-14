using MediaForge.Core.Enums;
using MediaForge.Core.Models;

namespace MediaForge.Application.Explorer;

public sealed record ProjectedExplorerEntry(
    ExplorerEntry Entry,
    bool IsPending,
    Guid? PendingOperationId,
    bool MarkedForDeletion);

/// <summary>
/// Projects the user's staged intent over the physical filesystem without touching disk.
/// The resulting view is what the Explorer should display before Save Changes is committed.
/// </summary>
public sealed class ExplorerProjectionService
{
    public IReadOnlyList<ProjectedExplorerEntry> Project(
        string currentPath,
        IEnumerable<ExplorerEntry> actualEntries,
        IEnumerable<StagingOperation> operations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPath);
        ArgumentNullException.ThrowIfNull(actualEntries);
        ArgumentNullException.ThrowIfNull(operations);

        var normalizedCurrentPath = NormalizeRequired(currentPath);
        var projected = actualEntries.ToDictionary(
            entry => NormalizeRequired(entry.FullPath),
            entry => new MutableEntry(entry),
            StringComparer.OrdinalIgnoreCase);

        foreach (var operation in operations.OrderBy(x => x.CreatedAt))
            Apply(projected, normalizedCurrentPath, operation);

        return projected.Values
            .Where(x => IsDirectChild(x.Entry.FullPath, normalizedCurrentPath))
            .OrderByDescending(x => x.Entry.IsDirectory)
            .ThenBy(x => x.Entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => new ProjectedExplorerEntry(x.Entry, x.IsPending, x.PendingOperationId, x.MarkedForDeletion))
            .ToArray();
    }

    private static void Apply(
        IDictionary<string, MutableEntry> projected,
        string currentPath,
        StagingOperation operation)
    {
        var payload = operation.Payload;
        if (payload is null) return;

        switch (operation.OperationType)
        {
            case OperationType.CreateDirectory:
            {
                var path = Normalize(payload.DirectoryPath);
                if (path is null || !IsDirectChild(path, currentPath)) return;
                if (!projected.ContainsKey(path))
                {
                    projected[path] = new MutableEntry(
                        new ExplorerEntry(Path.GetFileName(path), path, true, 0, DateTimeOffset.UtcNow));
                }
                projected[path].MarkPending(operation.OperationId);
                break;
            }
            case OperationType.Download:
            {
                var path = Normalize(payload.DestinationPath);
                if (path is null || !IsDirectChild(path, currentPath)) return;
                if (!projected.ContainsKey(path))
                {
                    projected[path] = new MutableEntry(
                        new ExplorerEntry(Path.GetFileName(path), path, false, 0, DateTimeOffset.UtcNow));
                }
                projected[path].MarkPending(operation.OperationId);
                break;
            }
            case OperationType.Delete:
            {
                var source = Normalize(payload.SourcePath);
                if (source is null) return;
                if (projected.TryGetValue(source, out var target))
                {
                    target.MarkPending(operation.OperationId);
                    target.MarkedForDeletion = true;
                }
                break;
            }
            case OperationType.Rename:
            {
                var source = Normalize(payload.SourcePath);
                var destination = Normalize(payload.DestinationPath)
                    ?? (source is null || string.IsNullOrWhiteSpace(payload.NewName)
                        ? null
                        : Normalize(Path.Combine(Path.GetDirectoryName(source) ?? currentPath, payload.NewName)));
                if (source is null || destination is null) return;

                if (projected.Remove(source, out var sourceEntry))
                {
                    sourceEntry.Entry = sourceEntry.Entry with
                    {
                        Name = Path.GetFileName(destination),
                        FullPath = destination,
                        LastModifiedUtc = DateTimeOffset.UtcNow
                    };
                    sourceEntry.MarkPending(operation.OperationId);
                    projected[destination] = sourceEntry;
                }
                break;
            }
            case OperationType.Move:
            {
                var source = Normalize(payload.SourcePath);
                var destination = Normalize(payload.DestinationPath);
                if (source is null || destination is null) return;

                if (projected.Remove(source, out var sourceEntry))
                {
                    sourceEntry.Entry = sourceEntry.Entry with
                    {
                        Name = Path.GetFileName(destination),
                        FullPath = destination,
                        LastModifiedUtc = DateTimeOffset.UtcNow
                    };
                    sourceEntry.MarkPending(operation.OperationId);
                    projected[destination] = sourceEntry;
                }
                break;
            }
        }
    }

    private static string? Normalize(string? path)
        => string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);

    private static string NormalizeRequired(string path)
        => Path.GetFullPath(path);

    private static bool IsDirectChild(string path, string directory)
    {
        var parent = Path.GetDirectoryName(path);
        return parent is not null
            && string.Equals(NormalizeRequired(parent), directory, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class MutableEntry(ExplorerEntry entry)
    {
        public ExplorerEntry Entry { get; set; } = entry;
        public bool IsPending { get; private set; }
        public Guid? PendingOperationId { get; private set; }
        public bool MarkedForDeletion { get; set; }

        public void MarkPending(Guid operationId)
        {
            IsPending = true;
            PendingOperationId = operationId;
        }
    }
}
