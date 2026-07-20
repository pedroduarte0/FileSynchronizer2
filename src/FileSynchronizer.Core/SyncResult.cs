namespace FileSynchronizer.Core;

public sealed record SyncResult(IReadOnlyCollection<SyncActionOutcome> Outcomes, SyncState UpdatedState);
