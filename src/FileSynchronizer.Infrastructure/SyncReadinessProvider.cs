using FileSynchronizer.Core;

namespace FileSynchronizer.Infrastructure;

public sealed class SyncReadinessProvider
{
    public SyncReadiness GetReadiness()
    {
        return new SyncReadiness([SyncMode.OneWay, SyncMode.TwoWay]);
    }
}
