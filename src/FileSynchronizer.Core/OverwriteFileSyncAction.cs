namespace FileSynchronizer.Core;

public sealed record OverwriteFileSyncAction(string RelativePath) : SyncPlanAction(RelativePath);
