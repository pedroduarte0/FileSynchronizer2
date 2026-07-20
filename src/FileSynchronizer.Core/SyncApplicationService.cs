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
                    await ApplyCopyActionAsync(plan.SyncPair, copy.RelativePath, cancellationToken);
                    outcomes.Add(new SyncActionOutcome(copy, SyncActionStatus.Applied));
                    break;

                case OverwriteFileSyncAction overwrite:
                    await ApplyCopyActionAsync(plan.SyncPair, overwrite.RelativePath, cancellationToken);
                    outcomes.Add(new SyncActionOutcome(overwrite, SyncActionStatus.Applied));
                    break;

                case DeleteFileSyncAction delete:
                    await ApplyDeleteActionAsync(plan.SyncPair, delete.RelativePath, cancellationToken);
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
        var targetFilesByPath = targetFiles.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);

        List<SyncPlanAction> actions =
        [
            .. sourceFiles
            .Where(file => !targetPaths.Contains(file.RelativePath))
            .Select(file => new CopyFileSyncAction(file.RelativePath)),

            .. sourceFiles
            .Where(file =>
                targetFilesByPath.TryGetValue(file.RelativePath, out var targetFile)
                && HasDifferentMetadata(file, targetFile))
            .Select(file => new OverwriteFileSyncAction(file.RelativePath)),

            .. targetFiles
            .Where(file => !sourcePaths.Contains(file.RelativePath))
            .Select(file => new DeleteFileSyncAction(file.RelativePath)),
        ];

        return new SyncPlan(syncPair, actions);
    }

    private static bool HasDifferentMetadata(SyncFile sourceFile, SyncFile targetFile)
    {
        return sourceFile.LastModifiedUtc != targetFile.LastModifiedUtc
            || sourceFile.Size != targetFile.Size;
    }

    private async Task ApplyCopyActionAsync(
        SyncPair syncPair,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var sourceProvider = GetProvider(syncPair.SourceLocation);
        var targetProvider = GetProvider(syncPair.TargetLocation);
        var sourceFiles = await sourceProvider.ListFilesAsync(cancellationToken);
        var sourceFile = sourceFiles.Single(file => file.RelativePath == relativePath);

        await using var content = await sourceProvider.OpenReadAsync(relativePath, cancellationToken);
        await targetProvider.WriteFileAsync(
            relativePath,
            content,
            sourceFile.LastModifiedUtc,
            cancellationToken);
    }

    private Task ApplyDeleteActionAsync(
        SyncPair syncPair,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var targetProvider = GetProvider(syncPair.TargetLocation);

        return targetProvider.DeleteFileAsync(relativePath, cancellationToken);
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
