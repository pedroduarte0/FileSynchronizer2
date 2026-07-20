namespace FileSynchronizer.Core;

public sealed record CopyFileSyncAction(string RelativePath) : SyncPlanAction(RelativePath);
