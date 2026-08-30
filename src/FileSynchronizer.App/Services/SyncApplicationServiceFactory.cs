using System.Collections.Generic;
using FileSynchronizer.Core;

namespace FileSynchronizer.App.Services;

public sealed class SyncApplicationServiceFactory
{
    private readonly ISyncStateStore _stateStore;
    private readonly ISyncResultStore _resultStore;
    private readonly SyncResultRetention _resultRetention;

    internal SyncApplicationServiceFactory(
        ISyncStateStore stateStore,
        ISyncResultStore resultStore,
        SyncResultRetention resultRetention)
    {
        _stateStore = stateStore;
        _resultStore = resultStore;
        _resultRetention = resultRetention;
    }

    public SyncApplicationService Create(IEnumerable<ISyncLocationProvider> providers)
    {
        return new SyncApplicationService(
            providers,
            _stateStore,
            _resultStore,
            _resultRetention);
    }
}
