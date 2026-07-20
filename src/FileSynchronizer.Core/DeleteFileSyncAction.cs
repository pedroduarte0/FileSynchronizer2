namespace FileSynchronizer.Core;

public sealed record DeleteFileSyncAction(string RelativePath) : SyncPlanAction(RelativePath);
