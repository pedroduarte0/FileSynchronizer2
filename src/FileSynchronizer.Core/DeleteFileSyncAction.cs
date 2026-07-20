namespace FileSynchronizer.Core;

public sealed record DeleteFileSyncAction(
    SyncLocation TargetLocation,
    string RelativePath) : SyncPlanAction(RelativePath);
