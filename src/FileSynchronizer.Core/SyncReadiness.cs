namespace FileSynchronizer.Core;

public sealed record SyncReadiness(IReadOnlyCollection<SyncMode> SupportedSyncModes)
{
    public bool Supports(SyncMode syncMode) => SupportedSyncModes.Contains(syncMode);
}
