using System.Text;

namespace FileSynchronizer.Core.Tests;

public sealed class SyncApplicationServiceTests
{
    [Fact]
    public async Task BuildPlanAsync_returns_copy_action_for_file_missing_from_target()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var sourceProvider = InMemorySyncLocationProvider.WithFiles(
            sourceLocation,
            new SyncFile("notes.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5));
        var targetProvider = InMemorySyncLocationProvider.Empty(targetLocation);
        var service = new SyncApplicationService([sourceProvider, targetProvider]);

        // Act
        var plan = await service.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        var action = Assert.Single(plan.Actions);
        var copy = Assert.IsType<CopyFileSyncAction>(action);
        Assert.Equal("notes.txt", copy.RelativePath);
    }

    [Fact]
    public async Task BuildPlanAsync_returns_delete_action_for_target_file_missing_from_source()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var targetProvider = InMemorySyncLocationProvider.WithFiles(
            targetLocation,
            new SyncFile("stale.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5));
        var service = new SyncApplicationService(
            [InMemorySyncLocationProvider.Empty(sourceLocation), targetProvider]);

        // Act
        var plan = await service.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        var action = Assert.Single(plan.Actions);
        var delete = Assert.IsType<DeleteFileSyncAction>(action);
        Assert.Equal("stale.txt", delete.RelativePath);
    }

    [Fact]
    public async Task BuildPlanAsync_returns_overwrite_action_for_same_path_file_with_different_metadata()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var sourceProvider = InMemorySyncLocationProvider.WithFiles(
            sourceLocation,
            new SyncFile("notes.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 20));
        var targetProvider = InMemorySyncLocationProvider.WithFiles(
            targetLocation,
            new SyncFile("notes.txt", DateTimeOffset.Parse("2026-07-20T09:00:00Z"), 10));
        var service = new SyncApplicationService([sourceProvider, targetProvider]);

        // Act
        var plan = await service.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        var action = Assert.Single(plan.Actions);
        var overwrite = Assert.IsType<OverwriteFileSyncAction>(action);
        Assert.Equal("notes.txt", overwrite.RelativePath);
    }

    [Fact]
    public async Task BuildPlanAsync_matches_case_only_path_differences_for_case_insensitive_target()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var sourceProvider = new PlanningSyncLocationProvider(
            sourceLocation,
            files: [new SyncFile("Notes.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 10)]);
        var targetProvider = new PlanningSyncLocationProvider(
            targetLocation,
            files: [new SyncFile("notes.txt", DateTimeOffset.Parse("2026-07-20T09:00:00Z"), 5)],
            relativePathComparer: StringComparer.OrdinalIgnoreCase);
        var service = new SyncApplicationService([sourceProvider, targetProvider]);

        // Act
        var plan = await service.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        var action = Assert.Single(plan.Actions);
        var overwrite = Assert.IsType<OverwriteFileSyncAction>(action);
        Assert.Equal("Notes.txt", overwrite.RelativePath);
    }

    [Fact]
    public async Task BuildPlanAsync_reports_case_collision_for_case_insensitive_target()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var sourceProvider = new PlanningSyncLocationProvider(
            sourceLocation,
            files:
            [
                new SyncFile("Notes.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5),
                new SyncFile("notes.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5),
            ]);
        var targetProvider = new PlanningSyncLocationProvider(
            targetLocation,
            relativePathComparer: StringComparer.OrdinalIgnoreCase);
        var service = new SyncApplicationService([sourceProvider, targetProvider]);

        // Act
        var plan = await service.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        Assert.Empty(plan.Actions);
        Assert.Contains(plan.Problems, problem => problem.Kind == SyncLocationProblemKind.CaseCollision);
    }

    [Fact]
    public async Task BuildPlanAsync_blocks_overlapping_local_locations_before_enumeration()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var sourceRoot = Path.Combine(Path.GetTempPath(), "sync");
        var sourceProvider = new PlanningSyncLocationProvider(sourceLocation, sourceRoot);
        var targetProvider = new PlanningSyncLocationProvider(
            targetLocation,
            Path.Combine(sourceRoot, "archive"));
        var service = new SyncApplicationService([sourceProvider, targetProvider]);

        // Act
        var plan = await service.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        Assert.Empty(plan.Actions);
        Assert.Equal(0, sourceProvider.ListEntriesCallCount);
        Assert.Equal(0, targetProvider.ListEntriesCallCount);
        Assert.Contains(plan.Problems, problem => problem.Kind == SyncLocationProblemKind.OverlappingLocation);
    }

    [Fact]
    public async Task BuildPlanAsync_reports_hidden_entries_empty_directories_and_storage_risk()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var sourceProvider = new PlanningSyncLocationProvider(
            sourceLocation,
            files: [new SyncFile(".settings", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 10)],
            directories: [new SyncDirectory("empty")],
            hiddenEntries: [".settings"]);
        var targetProvider = new PlanningSyncLocationProvider(targetLocation, availableStorageBytes: 5);
        var service = new SyncApplicationService([sourceProvider, targetProvider]);

        // Act
        var plan = await service.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        Assert.Contains(plan.Problems, problem => problem.Kind == SyncLocationProblemKind.HiddenItem);
        Assert.Contains(plan.Problems, problem => problem.Kind == SyncLocationProblemKind.EmptyDirectory);
        Assert.Contains(plan.Problems, problem => problem.Kind == SyncLocationProblemKind.StorageRisk);
    }

    [Fact]
    public async Task UpdateStateAsync_returns_files_observed_at_sync_location()
    {
        // Arrange
        var location = new SyncLocation("source");
        var provider = InMemorySyncLocationProvider.WithFiles(
            location,
            new SyncFile("notes.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5));
        var service = new SyncApplicationService([provider]);

        // Act
        var state = await service.UpdateStateAsync(location, CancellationToken.None);

        // Assert
        Assert.True(state.ContainsFile("notes.txt"));
    }

    [Fact]
    public async Task BuildPlanAsync_reports_provider_listing_problems()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var problem = new SyncLocationProblem(
            SyncLocationProblemKind.AccessProblem,
            string.Empty,
            "Source cannot be listed.");
        var sourceProvider = InMemorySyncLocationProvider.Empty(sourceLocation, problem);
        var targetProvider = InMemorySyncLocationProvider.Empty(targetLocation);
        var service = new SyncApplicationService([sourceProvider, targetProvider]);

        // Act
        var plan = await service.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        Assert.Empty(plan.Actions);
        Assert.Equal(problem, Assert.Single(plan.Problems));
    }

    [Fact]
    public async Task BuildPlanAsync_does_not_plan_deletions_when_source_has_access_problem()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var accessProblem = new SyncLocationProblem(
            SyncLocationProblemKind.AccessProblem,
            string.Empty,
            "Source cannot be listed.");
        var sourceProvider = InMemorySyncLocationProvider.Empty(sourceLocation, accessProblem);
        var targetProvider = InMemorySyncLocationProvider.WithFiles(
            targetLocation,
            new SyncFile("important.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5));
        var service = new SyncApplicationService([sourceProvider, targetProvider]);

        // Act
        var plan = await service.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        Assert.Empty(plan.Actions);
        Assert.Contains(plan.Problems, problem => problem == accessProblem);
    }

    [Fact]
    public async Task BuildPlanAsync_reports_symbolic_links_as_unsupported_plan_problems()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var sourceProvider = InMemorySyncLocationProvider.WithSymbolicLinks(
            sourceLocation,
            new SyncSymbolicLink("link.txt", "target.txt"));
        var targetProvider = InMemorySyncLocationProvider.Empty(targetLocation);
        var service = new SyncApplicationService([sourceProvider, targetProvider]);

        // Act
        var plan = await service.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        var problem = Assert.Single(plan.Problems);
        Assert.Equal(SyncLocationProblemKind.UnsupportedSymbolicLink, problem.Kind);
        Assert.Equal("link.txt", problem.RelativePath);
    }

    [Fact]
    public async Task BuildPlanAsync_does_not_plan_deletions_when_source_root_is_unsupported_symbolic_link()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var sourceProvider = InMemorySyncLocationProvider.WithSymbolicLinks(
            sourceLocation,
            new SyncSymbolicLink(string.Empty, "linked-source"));
        var targetProvider = InMemorySyncLocationProvider.WithFiles(
            targetLocation,
            new SyncFile("important.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5));
        var service = new SyncApplicationService([sourceProvider, targetProvider]);

        // Act
        var plan = await service.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        Assert.Empty(plan.Actions);
        Assert.Contains(plan.Problems, problem => problem.Kind == SyncLocationProblemKind.UnsupportedSymbolicLink);
    }

    [Fact]
    public async Task BuildPlanAsync_protects_only_nested_unsupported_symbolic_link_path_from_deletion()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var sourceProvider = InMemorySyncLocationProvider.WithSymbolicLinks(
            sourceLocation,
            new SyncSymbolicLink("linked", "linked-source"));
        var targetProvider = InMemorySyncLocationProvider.WithFiles(
            targetLocation,
            new SyncFile("linked/important.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5),
            new SyncFile("stale.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5));
        var service = new SyncApplicationService([sourceProvider, targetProvider]);

        // Act
        var plan = await service.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        var action = Assert.Single(plan.Actions);
        var delete = Assert.IsType<DeleteFileSyncAction>(action);
        Assert.Equal("stale.txt", delete.RelativePath);
    }

    [Fact]
    public async Task BuildPlanAsync_does_not_report_symbolic_links_when_both_locations_support_preservation()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var sourceProvider = InMemorySyncLocationProvider.WithSupportedSymbolicLinks(
            sourceLocation,
            new SyncSymbolicLink("link.txt", "target.txt"));
        var targetProvider = InMemorySyncLocationProvider.WithSupportedSymbolicLinks(targetLocation);
        var service = new SyncApplicationService([sourceProvider, targetProvider]);

        // Act
        var plan = await service.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        Assert.DoesNotContain(
            plan.Problems,
            problem => problem.Kind == SyncLocationProblemKind.UnsupportedSymbolicLink);
    }

    [Fact]
    public async Task BuildPlanAsync_returns_create_directory_action_for_empty_directory_missing_from_target()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var sourceProvider = InMemorySyncLocationProvider.WithDirectories(
            sourceLocation,
            new SyncDirectory("empty"));
        var targetProvider = InMemorySyncLocationProvider.Empty(targetLocation);
        var service = new SyncApplicationService([sourceProvider, targetProvider]);

        // Act
        var plan = await service.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        var action = Assert.Single(plan.Actions);
        var createDirectory = Assert.IsType<CreateDirectorySyncAction>(action);
        Assert.Equal("empty", createDirectory.RelativePath);
    }

    [Fact]
    public async Task BuildPlanAsync_returns_delete_directory_action_for_empty_target_directory_missing_from_source()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var sourceProvider = InMemorySyncLocationProvider.Empty(sourceLocation);
        var targetProvider = InMemorySyncLocationProvider.WithDirectories(
            targetLocation,
            new SyncDirectory("stale"));
        var service = new SyncApplicationService([sourceProvider, targetProvider]);

        // Act
        var plan = await service.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        var action = Assert.Single(plan.Actions);
        var deleteDirectory = Assert.IsType<DeleteDirectorySyncAction>(action);
        Assert.Equal("stale", deleteDirectory.RelativePath);
    }

    [Fact]
    public async Task BuildPlanAsync_deletes_inferred_target_directories_deepest_first()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var sourceProvider = new PlanningSyncLocationProvider(sourceLocation);
        var targetProvider = new PlanningSyncLocationProvider(
            targetLocation,
            files: [new SyncFile("archive/old/stale.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5)]);
        var service = new SyncApplicationService([sourceProvider, targetProvider]);

        // Act
        var plan = await service.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        Assert.Collection(
            plan.Actions,
            action => Assert.IsType<DeleteFileSyncAction>(action),
            action => Assert.Equal("archive/old", Assert.IsType<DeleteDirectorySyncAction>(action).RelativePath),
            action => Assert.Equal("archive", Assert.IsType<DeleteDirectorySyncAction>(action).RelativePath));
    }

    [Fact]
    public async Task BuildPlanAsync_does_not_delete_empty_target_directory_that_contains_source_files()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var sourceProvider = new PlanningSyncLocationProvider(
            sourceLocation,
            files: [new SyncFile("docs/notes.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5)]);
        var targetProvider = new PlanningSyncLocationProvider(
            targetLocation,
            directories: [new SyncDirectory("docs")]);
        var service = new SyncApplicationService([sourceProvider, targetProvider]);

        // Act
        var plan = await service.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        Assert.Contains(plan.Actions, action => action is CopyFileSyncAction && action.RelativePath == "docs/notes.txt");
        Assert.DoesNotContain(plan.Actions, action => action is DeleteDirectorySyncAction && action.RelativePath == "docs");
    }

    [Fact]
    public async Task BuildPlanAsync_reports_source_file_and_target_directory_as_unsupported_path_type()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var sourceProvider = new PlanningSyncLocationProvider(
            sourceLocation,
            files: [new SyncFile("docs", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5)]);
        var targetProvider = new PlanningSyncLocationProvider(
            targetLocation,
            directories: [new SyncDirectory("docs")]);
        var service = new SyncApplicationService([sourceProvider, targetProvider]);

        // Act
        var plan = await service.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        Assert.Empty(plan.Actions);
        Assert.Contains(plan.Problems, problem => problem.Kind == SyncLocationProblemKind.UnsupportedPathType);
    }

    [Fact]
    public async Task BuildPlanAsync_reports_source_directory_and_target_file_as_unsupported_path_type()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var sourceProvider = new PlanningSyncLocationProvider(
            sourceLocation,
            files: [new SyncFile("docs/notes.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5)]);
        var targetProvider = new PlanningSyncLocationProvider(
            targetLocation,
            files: [new SyncFile("docs", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5)]);
        var service = new SyncApplicationService([sourceProvider, targetProvider]);

        // Act
        var plan = await service.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        Assert.Empty(plan.Actions);
        Assert.Contains(plan.Problems, problem => problem.Kind == SyncLocationProblemKind.UnsupportedPathType);
    }

    [Fact]
    public async Task ApplyPlanAsync_copies_planned_file_and_updates_sync_state()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var sourceFile = new SyncFile("notes.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5);
        var sourceProvider = InMemorySyncLocationProvider.WithFiles(sourceLocation, sourceFile);
        var targetProvider = InMemorySyncLocationProvider.Empty(targetLocation);
        var service = new SyncApplicationService([sourceProvider, targetProvider]);
        var plan = new SyncPlan(
            syncPair,
            [new CopyFileSyncAction(sourceFile.RelativePath)]);

        // Act
        var result = await service.ApplyPlanAsync(plan, CancellationToken.None);

        // Assert
        var outcome = Assert.Single(result.Outcomes);
        Assert.Equal(SyncActionStatus.Applied, outcome.Status);
        Assert.True(targetProvider.ContainsFile("notes.txt"));
        Assert.True(result.UpdatedState.ContainsFile("notes.txt"));
    }

    [Fact]
    public async Task ApplyPlanAsync_creates_planned_empty_directory()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var targetProvider = InMemorySyncLocationProvider.Empty(targetLocation);
        var service = new SyncApplicationService(
            [InMemorySyncLocationProvider.Empty(sourceLocation), targetProvider]);
        var plan = new SyncPlan(syncPair, [new CreateDirectorySyncAction("empty")]);

        // Act
        var result = await service.ApplyPlanAsync(plan, CancellationToken.None);

        // Assert
        var outcome = Assert.Single(result.Outcomes);
        Assert.Equal(SyncActionStatus.Applied, outcome.Status);
        Assert.True(targetProvider.ContainsDirectory("empty"));
    }

    [Fact]
    public async Task ApplyPlanAsync_reports_provider_failures_without_crashing_run()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var sourceFile = new SyncFile("notes.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5);
        var problem = new SyncLocationProblem(
            SyncLocationProblemKind.AccessProblem,
            "notes.txt",
            "Cannot read notes.txt.");
        var sourceProvider = InMemorySyncLocationProvider.WithReadFailure(sourceLocation, sourceFile, problem);
        var targetProvider = InMemorySyncLocationProvider.Empty(targetLocation);
        var service = new SyncApplicationService([sourceProvider, targetProvider]);
        var plan = new SyncPlan(
            syncPair,
            [new CopyFileSyncAction(sourceFile.RelativePath)]);

        // Act
        var result = await service.ApplyPlanAsync(plan, CancellationToken.None);

        // Assert
        var outcome = Assert.Single(result.Outcomes);
        Assert.Equal(SyncActionStatus.Failed, outcome.Status);
        Assert.False(targetProvider.ContainsFile("notes.txt"));
        Assert.Equal(problem, Assert.Single(result.Problems));
    }

    [Fact]
    public async Task ApplyPlanAsync_deletes_planned_file_and_updates_sync_state()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var targetProvider = InMemorySyncLocationProvider.WithFiles(
            targetLocation,
            new SyncFile("stale.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5));
        var service = new SyncApplicationService(
            [InMemorySyncLocationProvider.Empty(sourceLocation), targetProvider]);
        var plan = new SyncPlan(
            syncPair,
            [new DeleteFileSyncAction("stale.txt")]);

        // Act
        var result = await service.ApplyPlanAsync(plan, CancellationToken.None);

        // Assert
        var outcome = Assert.Single(result.Outcomes);
        Assert.Equal(SyncActionStatus.Applied, outcome.Status);
        Assert.False(targetProvider.ContainsFile("stale.txt"));
        Assert.False(result.UpdatedState.ContainsFile("stale.txt"));
    }

    private sealed class InMemorySyncLocationProvider : ISyncLocationProvider
    {
        private readonly Dictionary<string, StoredFile> _files;
        private readonly HashSet<string> _directories;
        private readonly IReadOnlyCollection<SyncSymbolicLink> _symbolicLinks;
        private readonly IReadOnlyCollection<SyncLocationProblem> _problems;
        private readonly SyncLocationProblem? _readFailure;
        private readonly bool _supportsSymbolicLinkPreservation;

        private InMemorySyncLocationProvider(
            SyncLocation location,
            IEnumerable<StoredFile> files,
            IEnumerable<SyncDirectory> directories,
            IEnumerable<SyncSymbolicLink> symbolicLinks,
            IEnumerable<SyncLocationProblem> problems,
            SyncLocationProblem? readFailure,
            bool supportsSymbolicLinkPreservation = false)
        {
            Location = location;
            _files = files.ToDictionary(file => file.Metadata.RelativePath, StringComparer.Ordinal);
            _directories = directories
                .Select(directory => directory.RelativePath)
                .ToHashSet(StringComparer.Ordinal);
            _symbolicLinks = symbolicLinks.ToList();
            _problems = problems.ToList();
            _readFailure = readFailure;
            _supportsSymbolicLinkPreservation = supportsSymbolicLinkPreservation;
        }

        public SyncLocation Location { get; }

        public bool SupportsSymbolicLinkPreservation => _supportsSymbolicLinkPreservation;

        public static InMemorySyncLocationProvider Empty(
            SyncLocation location,
            params SyncLocationProblem[] problems)
        {
            return new InMemorySyncLocationProvider(location, [], [], [], problems, readFailure: null);
        }

        public static InMemorySyncLocationProvider WithFiles(SyncLocation location, params SyncFile[] files)
        {
            return new InMemorySyncLocationProvider(
                location,
                files.Select(file => new StoredFile(file, Encoding.UTF8.GetBytes(file.RelativePath))),
                [],
                [],
                [],
                readFailure: null);
        }

        public static InMemorySyncLocationProvider WithDirectories(
            SyncLocation location,
            params SyncDirectory[] directories)
        {
            return new InMemorySyncLocationProvider(location, [], directories, [], [], readFailure: null);
        }

        public static InMemorySyncLocationProvider WithSymbolicLinks(
            SyncLocation location,
            params SyncSymbolicLink[] symbolicLinks)
        {
            return new InMemorySyncLocationProvider(location, [], [], symbolicLinks, [], readFailure: null);
        }

        public static InMemorySyncLocationProvider WithSupportedSymbolicLinks(
            SyncLocation location,
            params SyncSymbolicLink[] symbolicLinks)
        {
            return new InMemorySyncLocationProvider(
                location,
                [],
                [],
                symbolicLinks,
                [],
                readFailure: null,
                supportsSymbolicLinkPreservation: true);
        }

        public static InMemorySyncLocationProvider WithReadFailure(
            SyncLocation location,
            SyncFile file,
            SyncLocationProblem problem)
        {
            return new InMemorySyncLocationProvider(
                location,
                [new StoredFile(file, Encoding.UTF8.GetBytes(file.RelativePath))],
                [],
                [],
                [],
                problem);
        }

        public Task<IReadOnlyCollection<SyncFile>> ListFilesAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<SyncFile> files = _files.Values.Select(file => file.Metadata).ToList();
            return Task.FromResult(files);
        }

        public Task<SyncLocationListing> ListEntriesAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<SyncFile> files = _files.Values.Select(file => file.Metadata).ToList();
            IReadOnlyCollection<SyncDirectory> directories = _directories
                .Select(directory => new SyncDirectory(directory))
                .ToList();

            return Task.FromResult(new SyncLocationListing(files, directories, _symbolicLinks, _problems));
        }

        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
        {
            if (_readFailure is not null)
            {
                throw new SyncLocationProviderException(_readFailure);
            }

            var content = _files[relativePath].Content;
            return Task.FromResult<Stream>(new MemoryStream(content, writable: false));
        }

        public async Task WriteFileAsync(
            string relativePath,
            Stream content,
            DateTimeOffset lastModifiedUtc,
            CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);

            var bytes = buffer.ToArray();
            _files[relativePath] = new StoredFile(
                new SyncFile(relativePath, lastModifiedUtc, bytes.Length),
                bytes);
        }

        public Task DeleteFileAsync(string relativePath, CancellationToken cancellationToken)
        {
            _files.Remove(relativePath);
            return Task.CompletedTask;
        }

        public Task CreateDirectoryAsync(string relativePath, CancellationToken cancellationToken)
        {
            _directories.Add(relativePath);
            return Task.CompletedTask;
        }

        public Task DeleteDirectoryAsync(string relativePath, CancellationToken cancellationToken)
        {
            _directories.Remove(relativePath);
            return Task.CompletedTask;
        }

        public bool ContainsFile(string relativePath)
        {
            return _files.ContainsKey(relativePath);
        }

        public bool ContainsDirectory(string relativePath)
        {
            return _directories.Contains(relativePath);
        }

        private sealed record StoredFile(SyncFile Metadata, byte[] Content);
    }

    private sealed class PlanningSyncLocationProvider : ISyncLocationProvider
    {
        private readonly SyncLocationListing _listing;
        private readonly long? _availableStorageBytes;

        public PlanningSyncLocationProvider(
            SyncLocation location,
            string? localRootPath = null,
            IReadOnlyCollection<SyncFile>? files = null,
            IReadOnlyCollection<SyncDirectory>? directories = null,
            IReadOnlyCollection<string>? hiddenEntries = null,
            long? availableStorageBytes = null,
            StringComparer? relativePathComparer = null)
        {
            Location = location;
            LocalRootPath = localRootPath;
            RelativePathComparer = relativePathComparer ?? StringComparer.Ordinal;
            _listing = new SyncLocationListing(
                files ?? [],
                directories ?? [],
                [],
                [],
                hiddenEntries ?? []);
            _availableStorageBytes = availableStorageBytes;
        }

        public SyncLocation Location { get; }

        public string? LocalRootPath { get; }

        public StringComparer RelativePathComparer { get; }

        public int ListEntriesCallCount { get; private set; }

        public Task<IReadOnlyCollection<SyncFile>> ListFilesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_listing.Files);
        }

        public Task<SyncLocationListing> ListEntriesAsync(CancellationToken cancellationToken)
        {
            ListEntriesCallCount++;
            return Task.FromResult(_listing);
        }

        public Task<long?> GetAvailableStorageBytesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_availableStorageBytes);
        }

        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task WriteFileAsync(
            string relativePath,
            Stream content,
            DateTimeOffset lastModifiedUtc,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task DeleteFileAsync(string relativePath, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
