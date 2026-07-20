namespace FileSynchronizer.Core;

public sealed record CopyFileSyncAction(
    SyncLocation SourceLocation,
    SyncLocation TargetLocation,
    string RelativePath) : SyncPlanAction(RelativePath);
