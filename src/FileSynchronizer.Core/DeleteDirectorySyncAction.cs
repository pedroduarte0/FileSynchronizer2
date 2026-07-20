namespace FileSynchronizer.Core;

public sealed record DeleteDirectorySyncAction(string RelativePath) : SyncPlanAction(RelativePath);
