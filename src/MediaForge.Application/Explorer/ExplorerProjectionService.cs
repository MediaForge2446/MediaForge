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
                ProjectCreateDirectory(projected, currentPath, operation);
                break;
            case OperationType.Download:
                ProjectDownload(projected, currentPath, operation);
                break;
            case OperationType.Delete:
                ProjectDelete(projected, operation);
                break;
            case OperationType.Rename:
                ProjectRenameOrMove(projected, currentPath, operation, isMove: false);
                break;
            case OperationType.Move:
                ProjectRenameOrMove(projected, currentPath, operation, isMove: true);
                break;
        }
    }

    private static void ProjectCreateDirectory(
        IDictionary<string, MutableEntry> projected,
        string currentPath,
        StagingOperation operation)
    {
        var path = Normalize(operation.Payload?.DirectoryPath);
        if (path is null || !IsDirectChild(path, currentPath)) return;

        if (!projected.ContainsKey(path))
        {
            projected[path] = new MutableEntry(
                new ExplorerEntry(Path.GetFileName(path), path, true, 0, operation.CreatedAt));
        }

        projected[path].MarkPending(operation.OperationId);
    }

    private static void ProjectDownload(
        IDictionary<string, MutableEntry> projected,
        string currentPath,
        StagingOperation operation)
    {
        var path = Normalize(operation.Payload?.DestinationPath);
        if (path is null || !IsDirectChild(path, currentPath)) return;

        if (!projected.ContainsKey(path))
        {
            projected[path] = new MutableEntry(
                new ExplorerEntry(Path.GetFileName(path), path, false, 0, operation.CreatedAt));
        }

        projected[path].MarkPending(operation.OperationId);
    }

    private static void ProjectDelete(
        IDictionary<string, MutableEntry> projected,
        StagingOperation operation)
    {
        var source = Normalize(operation.Payload?.SourcePath);
        if (source is null || !projected.TryGetValue(source, out var target)) return;

        target.MarkPending(operation.OperationId);
        target.MarkedForDeletion = true;
    }

    private static void ProjectRenameOrMove(
        IDictionary<string, MutableEntry> projected,
        string currentPath,
        StagingOperation operation,
        bool isMove)
    {
        var source = Normalize(operation.Payload?.SourcePath);
        var destination = Normalize(operation.Payload?.DestinationPath)
            ?? (source is null || string.IsNullOrWhiteSpace(operation.Payload?.NewName)
                ? null
                : Normalize(Path.Combine(Path.GetDirectoryName(source) ?? currentPath, operation.Payload.NewName)));

        if (source is null || destination is null) return;

        if (!projected.Remove(source, out var sourceEntry))
        {
            // The source may live outside the currently displayed folder. In that case
            // there is no entry to move, but the destination still needs to appear in
            // the destination folder's projection.
            if (IsDirectChild(destination, currentPath) && operation.Payload?.IsDirectory is not null)
            {
                sourceEntry = new MutableEntry(
                    new ExplorerEntry(
                        Path.GetFileName(destination),
                        destination,
                        operation.Payload.IsDirectory.Value,
                        0,
                        operation.CreatedAt));
                sourceEntry.MarkPending(operation.OperationId);
                projected[destination] = sourceEntry;
            }

            return;
        }

        // A persisted or previously staged Delete must not be resurrected by a later
        // Rename/Move operation in the visual projection.
        if (sourceEntry.MarkedForDeletion)
        {
            projected[source] = sourceEntry;
            return;
        }

        sourceEntry.Entry = sourceEntry.Entry with
        {
            Name = Path.GetFileName(destination),
            FullPath = destination,
            LastModifiedUtc = operation.CreatedAt
        };
        sourceEntry.MarkPending(operation.OperationId);
        sourceEntry.MarkedForDeletion = false;
        projected[destination] = sourceEntry;
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
