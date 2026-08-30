namespace FileSynchronizer.Core;

public interface ISyncResultStore
{
    Task SaveResultAsync(
        SyncPair syncPair,
        RetainedSyncResult result,
        SyncResultRetention retention,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<RetainedSyncResult>> GetResultsAsync(
        SyncPair syncPair,
        CancellationToken cancellationToken);
}
