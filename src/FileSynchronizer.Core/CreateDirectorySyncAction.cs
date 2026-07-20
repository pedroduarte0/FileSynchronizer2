namespace FileSynchronizer.Core;

public sealed record CreateDirectorySyncAction(string RelativePath) : SyncPlanAction(RelativePath);
