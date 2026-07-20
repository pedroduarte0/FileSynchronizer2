namespace FileSynchronizer.Core;

public sealed record SyncPair(SyncMode Mode, SyncLocation SourceLocation, SyncLocation TargetLocation);
