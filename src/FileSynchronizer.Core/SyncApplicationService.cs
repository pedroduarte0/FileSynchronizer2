namespace FileSynchronizer.Core;

public sealed class SyncApplicationService
{
    private readonly IReadOnlyDictionary<SyncLocation, ISyncLocationProvider> _providers;
    private readonly ISyncStateStore? _stateStore;
    private readonly ISyncResultStore? _resultStore;
    private readonly SyncResultRetention _resultRetention;

    public SyncApplicationService(
        IEnumerable<ISyncLocationProvider> providers,
        ISyncStateStore? stateStore = null,
        ISyncResultStore? resultStore = null,
        SyncResultRetention? resultRetention = null)
    {
        _providers = providers.ToDictionary(provider => provider.Location);
        _stateStore = stateStore;
        _resultStore = resultStore;
        _resultRetention = resultRetention ?? new SyncResultRetention(20);
    }

    public async Task<SyncPlan> BuildPlanAsync(SyncPair syncPair, CancellationToken cancellationToken)
    {
        if (syncPair.Mode != SyncMode.OneWay)
        {
            throw new NotSupportedException("Only one-way sync planning is implemented in the first sync slice.");
        }

        var sourceProvider = GetProvider(syncPair.SourceLocation);
        var targetProvider = GetProvider(syncPair.TargetLocation);

        var overlappingLocationProblem = GetOverlappingLocationProblem(sourceProvider, targetProvider);
        if (overlappingLocationProblem is not null)
        {
            return new SyncPlan(syncPair, [], [overlappingLocationProblem]);
        }

        var previousState = _stateStore is null
            ? null
            : await _stateStore.GetStateAsync(syncPair, cancellationToken);

        return await BuildPlanAsync(
            syncPair,
            sourceProvider,
            targetProvider,
            previousState,
            cancellationToken);
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

        var allActionsApplied = outcomes.All(outcome => outcome.Status == SyncActionStatus.Applied);
        if (_stateStore is not null && allActionsApplied && problems.Count == 0)
        {
            var sourceState = await UpdateStateAsync(plan.SyncPair.SourceLocation, cancellationToken);
            problems.AddRange(sourceState.Problems);
            if (sourceState.Problems.Count == 0 && updatedState.Problems.Count == 0)
            {
                await _stateStore.SaveStateAsync(
                    plan.SyncPair,
                    new SyncPairState(sourceState, updatedState),
                    cancellationToken);
            }
        }

        var result = new SyncResult(outcomes, updatedState, problems);

        if (_resultStore is not null)
        {
            await _resultStore.SaveResultAsync(
                plan.SyncPair,
                new RetainedSyncResult(DateTimeOffset.UtcNow, result),
                _resultRetention,
                cancellationToken);
        }

        return result;
    }

    private static async Task<SyncPlan> BuildPlanAsync(
        SyncPair syncPair,
        ISyncLocationProvider sourceProvider,
        ISyncLocationProvider targetProvider,
        SyncPairState? previousState,
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
        var currentSourceState = new SyncState(sourceFiles, sourceListing.Problems);
        var knownSourceDeletionPaths = previousState?.SourceState
            .GetDeletedFiles(currentSourceState)
            .Select(file => file.RelativePath)
            .ToHashSet(StringComparer.Ordinal)
            ?? [];
        var targetFilesForKnownSourceDeletions = targetFiles
            .Where(file => knownSourceDeletionPaths.Contains(file.RelativePath));
        var otherTargetOnlyFiles = targetFiles
            .Where(file =>
                !sourcePaths.Contains(file.RelativePath)
                && !knownSourceDeletionPaths.Contains(file.RelativePath));

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

            .. targetFilesForKnownSourceDeletions
            .Select(file => new DeleteFileSyncAction(file.RelativePath, DeleteFileReason.SourceDeletion)),

            .. otherTargetOnlyFiles
            .Select(file => new DeleteFileSyncAction(file.RelativePath, DeleteFileReason.TargetOnlyFile)),

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

    private static SyncLocationProblem? GetOverlappingLocationProblem(
        ISyncLocationProvider sourceProvider,
        ISyncLocationProvider targetProvider)
    {
        if (sourceProvider.LocalRootPath is not { } sourceRootPath
            || targetProvider.LocalRootPath is not { } targetRootPath
            || !AreSameOrNestedPaths(sourceRootPath, targetRootPath))
        {
            return null;
        }

        return new SyncLocationProblem(
            SyncLocationProblemKind.OverlappingLocation,
            string.Empty,
            "Local sync locations must not be the same location or nested within one another.");
    }

    private static bool AreSameOrNestedPaths(string firstPath, string secondPath)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var firstRootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(firstPath));
        var secondRootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(secondPath));

        return firstRootPath.Equals(secondRootPath, comparison)
            || IsNestedWithin(firstRootPath, secondRootPath, comparison)
            || IsNestedWithin(secondRootPath, firstRootPath, comparison);
    }

    private static bool IsNestedWithin(
        string candidatePath,
        string potentialParentPath,
        StringComparison comparison)
    {
        var parentPathWithSeparator = potentialParentPath.EndsWith(Path.DirectorySeparatorChar)
            ? potentialParentPath
            : potentialParentPath + Path.DirectorySeparatorChar;

        return candidatePath.StartsWith(parentPathWithSeparator, comparison);
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
