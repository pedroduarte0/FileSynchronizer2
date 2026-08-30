namespace FileSynchronizer.Core;

public interface ISyncStateStore
{
    Task<SyncPairState?> GetStateAsync(SyncPair syncPair, CancellationToken cancellationToken);

    Task SaveStateAsync(SyncPair syncPair, SyncPairState state, CancellationToken cancellationToken);
}
