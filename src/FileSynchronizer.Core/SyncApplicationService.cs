namespace FileSynchronizer.Core;

public sealed class SyncApplicationService
{
    private readonly IReadOnlyDictionary<SyncLocation, ISyncLocationProvider> _providers;

    public SyncApplicationService(IEnumerable<ISyncLocationProvider> providers)
    {
        _providers = providers.ToDictionary(provider => provider.Location);
    }

    public Task<SyncPlan> BuildPlanAsync(SyncPair syncPair, CancellationToken cancellationToken)
    {
        if (syncPair.Mode != SyncMode.OneWay)
        {
            throw new NotSupportedException("Only one-way sync planning is implemented in the first sync slice.");
        }

        var sourceProvider = GetProvider(syncPair.SourceLocation);
        var targetProvider = GetProvider(syncPair.TargetLocation);

        return BuildPlanAsync(syncPair, sourceProvider, targetProvider, cancellationToken);
    }

    public async Task<SyncState> UpdateStateAsync(SyncLocation location, CancellationToken cancellationToken)
    {
        var provider = GetProvider(location);
        var files = await provider.ListFilesAsync(cancellationToken);

        return new SyncState(files);
    }

    public async Task<SyncResult> ApplyPlanAsync(SyncPlan plan, CancellationToken cancellationToken)
    {
        var outcomes = new List<SyncActionOutcome>();

        foreach (var action in plan.Actions)
        {
            switch (action)
            {
                case CopyFileSyncAction copy:
                    await ApplyCopyActionAsync(copy, cancellationToken);
                    outcomes.Add(new SyncActionOutcome(copy, SyncActionStatus.Applied));
                    break;

                case DeleteFileSyncAction delete:
                    await ApplyDeleteActionAsync(delete, cancellationToken);
                    outcomes.Add(new SyncActionOutcome(delete, SyncActionStatus.Applied));
                    break;

                default:
                    throw new NotSupportedException($"Unsupported sync plan action '{action.GetType().Name}'.");
            }
        }

        var updatedState = await UpdateStateAsync(plan.SyncPair.TargetLocation, cancellationToken);

        return new SyncResult(outcomes, updatedState);
    }

    private static async Task<SyncPlan> BuildPlanAsync(
        SyncPair syncPair,
        ISyncLocationProvider sourceProvider,
        ISyncLocationProvider targetProvider,
        CancellationToken cancellationToken)
    {
        var sourceFiles = await sourceProvider.ListFilesAsync(cancellationToken);
        var targetFiles = await targetProvider.ListFilesAsync(cancellationToken);
        var targetPaths = targetFiles
            .Select(file => file.RelativePath)
            .ToHashSet(StringComparer.Ordinal);
        var sourcePaths = sourceFiles
            .Select(file => file.RelativePath)
            .ToHashSet(StringComparer.Ordinal);

        List<SyncPlanAction> actions =
        [
            .. sourceFiles
            .Where(file => !targetPaths.Contains(file.RelativePath))
            .Select(file => new CopyFileSyncAction(
                syncPair.SourceLocation,
                syncPair.TargetLocation,
                file.RelativePath)),

            .. targetFiles
            .Where(file => !sourcePaths.Contains(file.RelativePath))
            .Select(file => new DeleteFileSyncAction(
                syncPair.TargetLocation,
                file.RelativePath)),
        ];

        return new SyncPlan(syncPair, actions);
    }

    private async Task ApplyCopyActionAsync(CopyFileSyncAction action, CancellationToken cancellationToken)
    {
        var sourceProvider = GetProvider(action.SourceLocation);
        var targetProvider = GetProvider(action.TargetLocation);
        var sourceFiles = await sourceProvider.ListFilesAsync(cancellationToken);
        var sourceFile = sourceFiles.Single(file => file.RelativePath == action.RelativePath);

        await using var content = await sourceProvider.OpenReadAsync(action.RelativePath, cancellationToken);
        await targetProvider.WriteFileAsync(
            action.RelativePath,
            content,
            sourceFile.LastModifiedUtc,
            cancellationToken);
    }

    private Task ApplyDeleteActionAsync(DeleteFileSyncAction action, CancellationToken cancellationToken)
    {
        var targetProvider = GetProvider(action.TargetLocation);

        return targetProvider.DeleteFileAsync(action.RelativePath, cancellationToken);
    }

    private ISyncLocationProvider GetProvider(SyncLocation location)
    {
        if (_providers.TryGetValue(location, out var provider))
        {
            return provider;
        }

        throw new InvalidOperationException($"No sync location provider is registered for '{location.Value}'.");
    }
}
