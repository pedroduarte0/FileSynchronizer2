namespace FileSynchronizer.Core;

public sealed record SyncPlan(
    SyncPair SyncPair,
    IReadOnlyCollection<SyncPlanAction> Actions,
    IReadOnlyCollection<SyncLocationProblem> Problems)
{
    public SyncPlan(SyncPair syncPair, IReadOnlyCollection<SyncPlanAction> actions)
        : this(syncPair, actions, [])
    {
    }

    public static SyncPlan Empty(SyncPair syncPair) => new(syncPair, [], []);
}
