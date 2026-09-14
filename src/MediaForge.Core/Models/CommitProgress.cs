namespace MediaForge.Core.Models;

public sealed record CommitProgress(
    Guid OperationId,
    double Percent,
    string Status,
    bool IsTerminal = false);
