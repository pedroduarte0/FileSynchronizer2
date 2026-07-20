namespace FileSynchronizer.Core;

public sealed record SyncLocationProblem(
    SyncLocationProblemKind Kind,
    string RelativePath,
    string Message);
