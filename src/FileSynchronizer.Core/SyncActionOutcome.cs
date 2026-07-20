namespace FileSynchronizer.Core;

public sealed record SyncActionOutcome(SyncPlanAction Action, SyncActionStatus Status);
