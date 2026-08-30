namespace FileSynchronizer.Core;

public sealed record SyncPairState(SyncState SourceState, SyncState TargetState);
