namespace FileSynchronizer.Core;

public sealed record SyncPlan(SyncPair SyncPair, IReadOnlyCollection<SyncPlanAction> Actions)
{
    public static SyncPlan Empty(SyncPair syncPair) => new(syncPair, []);
}
