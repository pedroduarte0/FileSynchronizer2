namespace FileSynchronizer.Core;

public sealed record SyncPair(SyncMode Mode, string SourceLocation, string TargetLocation);
