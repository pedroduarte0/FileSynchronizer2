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
        var listing = await provider.ListEntriesAsync(cancellationToken);

        return new SyncState(listing.Files, listing.Problems);
    }

    public async Task<SyncResult> ApplyPlanAsync(SyncPlan plan, CancellationToken cancellationToken)
    {
        var outcomes = new List<SyncActionOutcome>();
        var problems = new List<SyncLocationProblem>(plan.Problems);

        foreach (var action in plan.Actions)
        {
            try
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

                    case CreateDirectorySyncAction createDirectory:
                        await ApplyCreateDirectoryActionAsync(
                            plan.SyncPair,
                            createDirectory.RelativePath,
                            cancellationToken);
                        outcomes.Add(new SyncActionOutcome(createDirectory, SyncActionStatus.Applied));
                        break;

                    case DeleteDirectorySyncAction deleteDirectory:
                        await ApplyDeleteDirectoryActionAsync(
                            plan.SyncPair,
                            deleteDirectory.RelativePath,
                            cancellationToken);
                        outcomes.Add(new SyncActionOutcome(deleteDirectory, SyncActionStatus.Applied));
                        break;

                    default:
                        throw new NotSupportedException($"Unsupported sync plan action '{action.GetType().Name}'.");
                }
            }
            catch (SyncLocationProviderException exception)
            {
                outcomes.Add(new SyncActionOutcome(action, SyncActionStatus.Failed));
                problems.Add(exception.Problem);
            }
        }

        var updatedState = await UpdateStateAsync(plan.SyncPair.TargetLocation, cancellationToken);
        problems.AddRange(updatedState.Problems);

        return new SyncResult(outcomes, updatedState, problems);
    }

    private static async Task<SyncPlan> BuildPlanAsync(
        SyncPair syncPair,
        ISyncLocationProvider sourceProvider,
        ISyncLocationProvider targetProvider,
        CancellationToken cancellationToken)
    {
        var sourceListing = await sourceProvider.ListEntriesAsync(cancellationToken);
        var targetListing = await targetProvider.ListEntriesAsync(cancellationToken);
        var sourceFiles = sourceListing.Files;
        var targetFiles = targetListing.Files;
        var sourceDirectories = sourceListing.EmptyDirectories;
        var targetDirectories = targetListing.EmptyDirectories;
        var targetPaths = targetFiles
            .Select(file => file.RelativePath)
            .ToHashSet(StringComparer.Ordinal);
        var sourcePaths = sourceFiles
            .Select(file => file.RelativePath)
            .ToHashSet(StringComparer.Ordinal);
        var targetDirectoryPaths = targetDirectories
            .Select(directory => directory.RelativePath)
            .ToHashSet(StringComparer.Ordinal);
        var sourceDirectoryPaths = sourceDirectories
            .Select(directory => directory.RelativePath)
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

            .. sourceDirectories
            .Where(directory => !targetDirectoryPaths.Contains(directory.RelativePath))
            .Select(directory => new CreateDirectorySyncAction(directory.RelativePath)),

            .. targetDirectories
            .Where(directory => !sourceDirectoryPaths.Contains(directory.RelativePath))
            .Select(directory => new DeleteDirectorySyncAction(directory.RelativePath)),
        ];

        return new SyncPlan(
            syncPair,
            actions,
            [
                .. sourceListing.Problems,
                .. targetListing.Problems,
                .. ToUnsupportedSymbolicLinkProblems(
                    sourceListing.SymbolicLinks,
                    sourceProvider,
                    targetProvider),
                .. ToUnsupportedSymbolicLinkProblems(
                    targetListing.SymbolicLinks,
                    targetProvider,
                    sourceProvider),
            ]);
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
        var sourceListing = await sourceProvider.ListEntriesAsync(cancellationToken);
        var sourceFile = sourceListing.Files.SingleOrDefault(file => file.RelativePath == relativePath);
        if (sourceFile is null)
        {
            var problem = sourceListing.Problems.FirstOrDefault()
                ?? new SyncLocationProblem(
                    SyncLocationProblemKind.AccessProblem,
                    relativePath,
                    $"Source file '{relativePath}' is no longer available.");
            throw new SyncLocationProviderException(problem);
        }

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

    private Task ApplyCreateDirectoryActionAsync(
        SyncPair syncPair,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var targetProvider = GetProvider(syncPair.TargetLocation);

        return targetProvider.CreateDirectoryAsync(relativePath, cancellationToken);
    }

    private Task ApplyDeleteDirectoryActionAsync(
        SyncPair syncPair,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var targetProvider = GetProvider(syncPair.TargetLocation);

        return targetProvider.DeleteDirectoryAsync(relativePath, cancellationToken);
    }

    private static IEnumerable<SyncLocationProblem> ToUnsupportedSymbolicLinkProblems(
        IEnumerable<SyncSymbolicLink> symbolicLinks,
        ISyncLocationProvider containingProvider,
        ISyncLocationProvider otherProvider)
    {
        if (containingProvider.SupportsSymbolicLinkPreservation
            && otherProvider.SupportsSymbolicLinkPreservation)
        {
            return [];
        }

        return symbolicLinks.Select(link => new SyncLocationProblem(
            SyncLocationProblemKind.UnsupportedSymbolicLink,
            link.RelativePath,
            "Symbolic link preservation is not supported by one or both sync locations."));
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
