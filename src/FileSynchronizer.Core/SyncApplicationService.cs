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
        if (AreOverlappingLocalLocations(sourceProvider, targetProvider))
        {
            return new SyncPlan(
                syncPair,
                [],
                [new SyncLocationProblem(
                    SyncLocationProblemKind.OverlappingLocation,
                    string.Empty,
                    "The source and target locations overlap. Choose two separate local locations.")]);
        }

        var sourceListing = await sourceProvider.ListEntriesAsync(cancellationToken);
        var targetListing = await targetProvider.ListEntriesAsync(cancellationToken);
        var sourceFiles = sourceListing.Files;
        var targetFiles = targetListing.Files;
        var sourceDirectories = sourceListing.EmptyDirectories;
        var targetDirectories = targetListing.EmptyDirectories;
        var targetPathComparer = targetProvider.RelativePathComparer;
        var targetPaths = targetFiles
            .Select(file => file.RelativePath)
            .ToHashSet(targetPathComparer);
        var sourcePaths = sourceFiles
            .Select(file => file.RelativePath)
            .ToHashSet(targetPathComparer);
        var targetDirectoryPaths = GetDirectoryPaths(targetFiles, targetDirectories, targetPathComparer);
        var sourceDirectoryPaths = GetDirectoryPaths(sourceFiles, sourceDirectories, targetPathComparer);
        var targetFilesByPath = targetFiles.ToDictionary(file => file.RelativePath, targetPathComparer);
        var sourceCaseCollisionPaths = GetCaseCollisionPaths(sourceFiles, targetPathComparer);
        var unsupportedSourceSymbolicLinkPaths = GetUnsupportedSymbolicLinkPaths(
            sourceListing.SymbolicLinks,
            sourceProvider,
            targetProvider,
            targetPathComparer);
        var pathTypeCollisionPaths = GetPathTypeCollisionPaths(
            sourceFiles,
            targetFiles,
            sourceDirectoryPaths,
            targetDirectoryPaths,
            targetPathComparer);
        var sourceHasAccessProblem = sourceListing.Problems
            .Any(problem => problem.Kind == SyncLocationProblemKind.AccessProblem);

        List<SyncPlanAction> actions =
        [
            .. sourceFiles
            .Where(file => !sourceCaseCollisionPaths.Contains(file.RelativePath)
                && !HasCollidingAncestor(file.RelativePath, pathTypeCollisionPaths)
                && !targetPaths.Contains(file.RelativePath))
            .Select(file => new CopyFileSyncAction(file.RelativePath)),

            .. sourceFiles
            .Where(file =>
                !sourceCaseCollisionPaths.Contains(file.RelativePath)
                && !HasCollidingAncestor(file.RelativePath, pathTypeCollisionPaths)
                && targetFilesByPath.TryGetValue(file.RelativePath, out var targetFile)
                && HasDifferentMetadata(file, targetFile))
            .Select(file => new OverwriteFileSyncAction(file.RelativePath)),

            .. targetFiles
            .Where(file => !sourceHasAccessProblem
                && !HasCollidingAncestor(file.RelativePath, unsupportedSourceSymbolicLinkPaths)
                && !HasCollidingAncestor(file.RelativePath, pathTypeCollisionPaths)
                && !sourcePaths.Contains(file.RelativePath))
            .Select(file => new DeleteFileSyncAction(file.RelativePath)),

            .. sourceDirectories
            .Where(directory => !HasCollidingAncestor(directory.RelativePath, pathTypeCollisionPaths)
                && !targetDirectoryPaths.Contains(directory.RelativePath))
            .Select(directory => new CreateDirectorySyncAction(directory.RelativePath)),

            .. targetDirectoryPaths
            .Where(directoryPath => !sourceHasAccessProblem
                && !HasCollidingAncestor(directoryPath, unsupportedSourceSymbolicLinkPaths)
                && !HasCollidingAncestor(directoryPath, pathTypeCollisionPaths)
                && !sourceDirectoryPaths.Contains(directoryPath))
            .OrderByDescending(directoryPath => directoryPath.Count(character => character == '/'))
            .ThenBy(directoryPath => directoryPath, targetPathComparer)
            .Select(directoryPath => new DeleteDirectorySyncAction(directoryPath)),
        ];

        var requiredStorageBytes = actions
            .OfType<CopyFileSyncAction>()
            .Select(action => sourceFiles.Single(file => file.RelativePath == action.RelativePath).Size)
            .Concat(actions
                .OfType<OverwriteFileSyncAction>()
                .Select(action =>
                {
                    var sourceFile = sourceFiles.Single(file => file.RelativePath == action.RelativePath);
                    var targetFile = targetFilesByPath[action.RelativePath];
                    return Math.Max(0, sourceFile.Size - targetFile.Size);
                }))
            .Sum();
        var availableStorageBytes = await targetProvider.GetAvailableStorageBytesAsync(cancellationToken);

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
                .. ToHiddenItemProblems(sourceListing.HiddenEntries, "source"),
                .. ToHiddenItemProblems(targetListing.HiddenEntries, "target"),
                .. ToEmptyDirectoryProblems(sourceDirectories, "source"),
                .. ToEmptyDirectoryProblems(targetDirectories, "target"),
                .. ToCaseCollisionProblems(sourceCaseCollisionPaths),
                .. ToUnsupportedPathTypeProblems(pathTypeCollisionPaths),
                .. ToStorageRiskProblems(availableStorageBytes, requiredStorageBytes),
            ]);
    }

    private static bool AreOverlappingLocalLocations(
        ISyncLocationProvider sourceProvider,
        ISyncLocationProvider targetProvider)
    {
        if (sourceProvider.LocalRootPath is null || targetProvider.LocalRootPath is null)
        {
            return false;
        }

        var sourceRoot = Path.GetFullPath(sourceProvider.LocalRootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var targetRoot = Path.GetFullPath(targetProvider.LocalRootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(sourceRoot, targetRoot, comparison)
            || IsWithin(sourceRoot, targetRoot, comparison)
            || IsWithin(targetRoot, sourceRoot, comparison);
    }

    private static bool IsWithin(string childPath, string parentPath, StringComparison comparison)
    {
        var parentWithSeparator = parentPath + Path.DirectorySeparatorChar;
        return childPath.StartsWith(parentWithSeparator, comparison);
    }

    private static IEnumerable<SyncLocationProblem> ToHiddenItemProblems(
        IEnumerable<string> hiddenEntries,
        string locationRole)
    {
        return hiddenEntries.Select(relativePath => new SyncLocationProblem(
            SyncLocationProblemKind.HiddenItem,
            relativePath,
            $"Hidden {locationRole} item '{relativePath}' is included in the sync plan."));
    }

    private static IEnumerable<SyncLocationProblem> ToEmptyDirectoryProblems(
        IEnumerable<SyncDirectory> emptyDirectories,
        string locationRole)
    {
        return emptyDirectories.Select(directory => new SyncLocationProblem(
            SyncLocationProblemKind.EmptyDirectory,
            directory.RelativePath,
            $"Empty {locationRole} directory '{directory.RelativePath}' is included in the sync plan."));
    }

    private static IEnumerable<SyncLocationProblem> ToStorageRiskProblems(
        long? availableStorageBytes,
        long requiredStorageBytes)
    {
        if (availableStorageBytes is null || requiredStorageBytes <= availableStorageBytes)
        {
            return [];
        }

        return [new SyncLocationProblem(
            SyncLocationProblemKind.StorageRisk,
            string.Empty,
            $"The sync plan needs {requiredStorageBytes} additional bytes, but the target has only {availableStorageBytes} bytes available.")];
    }

    private static bool HasDifferentMetadata(SyncFile sourceFile, SyncFile targetFile)
    {
        return sourceFile.LastModifiedUtc != targetFile.LastModifiedUtc
            || sourceFile.Size != targetFile.Size;
    }

    private static HashSet<string> GetDirectoryPaths(
        IEnumerable<SyncFile> files,
        IEnumerable<SyncDirectory> emptyDirectories,
        StringComparer pathComparer)
    {
        var directoryPaths = emptyDirectories
            .Select(directory => directory.RelativePath)
            .ToHashSet(pathComparer);

        foreach (var file in files)
        {
            var separatorIndex = file.RelativePath.LastIndexOf('/');
            while (separatorIndex >= 0)
            {
                directoryPaths.Add(file.RelativePath[..separatorIndex]);
                separatorIndex = file.RelativePath.LastIndexOf('/', separatorIndex - 1);
            }
        }

        return directoryPaths;
    }

    private static HashSet<string> GetCaseCollisionPaths(
        IEnumerable<SyncFile> files,
        StringComparer pathComparer)
    {
        if (pathComparer.Equals(StringComparer.Ordinal))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return files
            .GroupBy(file => file.RelativePath, pathComparer)
            .Where(group => group.Select(file => file.RelativePath).Distinct(StringComparer.Ordinal).Skip(1).Any())
            .SelectMany(group => group.Select(file => file.RelativePath))
            .ToHashSet(pathComparer);
    }

    private static IEnumerable<SyncLocationProblem> ToCaseCollisionProblems(
        IEnumerable<string> relativePaths)
    {
        return relativePaths.Select(relativePath => new SyncLocationProblem(
            SyncLocationProblemKind.CaseCollision,
            relativePath,
            $"The target location cannot represent '{relativePath}' because another source path differs only by casing."));
    }

    private static HashSet<string> GetPathTypeCollisionPaths(
        IEnumerable<SyncFile> sourceFiles,
        IEnumerable<SyncFile> targetFiles,
        IReadOnlySet<string> sourceDirectoryPaths,
        IReadOnlySet<string> targetDirectoryPaths,
        StringComparer pathComparer)
    {
        return sourceFiles
            .Where(file => targetDirectoryPaths.Contains(file.RelativePath))
            .Select(file => file.RelativePath)
            .Concat(targetFiles
                .Where(file => sourceDirectoryPaths.Contains(file.RelativePath))
                .Select(file => file.RelativePath))
            .ToHashSet(pathComparer);
    }

    private static bool HasCollidingAncestor(
        string relativePath,
        IReadOnlySet<string> collisionPaths)
    {
        if (collisionPaths.Contains(string.Empty))
        {
            return true;
        }

        var currentPath = relativePath;
        while (true)
        {
            if (collisionPaths.Contains(currentPath))
            {
                return true;
            }

            var separatorIndex = currentPath.LastIndexOf('/');
            if (separatorIndex < 0)
            {
                return false;
            }

            currentPath = currentPath[..separatorIndex];
        }
    }

    private static IEnumerable<SyncLocationProblem> ToUnsupportedPathTypeProblems(
        IEnumerable<string> relativePaths)
    {
        return relativePaths.Select(relativePath => new SyncLocationProblem(
            SyncLocationProblemKind.UnsupportedPathType,
            relativePath,
            $"The source and target use different item types for '{relativePath}'."));
    }

    private static HashSet<string> GetUnsupportedSymbolicLinkPaths(
        IEnumerable<SyncSymbolicLink> symbolicLinks,
        ISyncLocationProvider containingProvider,
        ISyncLocationProvider otherProvider,
        StringComparer pathComparer)
    {
        if (containingProvider.SupportsSymbolicLinkPreservation
            && otherProvider.SupportsSymbolicLinkPreservation)
        {
            return new HashSet<string>(pathComparer);
        }

        return symbolicLinks
            .Select(link => link.RelativePath)
            .ToHashSet(pathComparer);
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
