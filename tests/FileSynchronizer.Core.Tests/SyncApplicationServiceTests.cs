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
        Assert.Equal(DeleteFileReason.TargetOnlyFile, delete.Reason);
    }

    [Fact]
    public async Task BuildPlanAsync_blocks_overlapping_local_locations()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var sourceRootPath = Path.Combine(Path.GetTempPath(), "FileSynchronizer2", "source");
        var service = new SyncApplicationService(
            [
                InMemorySyncLocationProvider.WithLocalRoot(sourceLocation, sourceRootPath),
                InMemorySyncLocationProvider.WithLocalRoot(
                    targetLocation,
                    Path.Combine(sourceRootPath, "target")),
            ]);

        // Act
        var plan = await service.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        Assert.Empty(plan.Actions);
        Assert.Contains(
            plan.Problems,
            problem => problem.Kind == SyncLocationProblemKind.OverlappingLocation);
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
    public void GetDeletedFiles_returns_files_present_in_previous_state_but_missing_from_current_state()
    {
        // Arrange
        var lastModifiedUtc = DateTimeOffset.Parse("2026-07-20T10:00:00Z");
        var previousState = new SyncState(
        [
            new SyncFile("deleted.txt", lastModifiedUtc, 5),
            new SyncFile("retained.txt", lastModifiedUtc, 5),
        ]);
        var currentState = new SyncState([new SyncFile("retained.txt", lastModifiedUtc, 5)]);

        // Act
        var deletedFiles = previousState.GetDeletedFiles(currentState);

        // Assert
        var deletedFile = Assert.Single(deletedFiles);
        Assert.Equal("deleted.txt", deletedFile.RelativePath);
    }

    [Fact]
    public async Task BuildPlanAsync_does_not_persist_preview_state_or_result()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var stateStore = new InMemorySyncStateStore();
        var resultStore = new InMemorySyncResultStore();
        var service = new SyncApplicationService(
            [InMemorySyncLocationProvider.Empty(sourceLocation), InMemorySyncLocationProvider.Empty(targetLocation)],
            stateStore,
            resultStore);

        // Act
        await service.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        Assert.Empty(stateStore.SavedStates);
        Assert.Empty(resultStore.Results);
    }

    [Fact]
    public async Task BuildPlanAsync_loads_pair_state_to_detect_a_source_deletion_across_runs()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var deletedFile = new SyncFile("deleted.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5);
        var stateStore = new InMemorySyncStateStore();
        stateStore.SavedStates[syncPair] = new SyncPairState(
            new SyncState([deletedFile]),
            new SyncState([deletedFile]));
        var serviceAfterRestart = new SyncApplicationService(
            [
                InMemorySyncLocationProvider.Empty(sourceLocation),
                InMemorySyncLocationProvider.WithFiles(targetLocation, deletedFile),
            ],
            stateStore);

        // Act
        var plan = await serviceAfterRestart.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        Assert.Equal(syncPair, Assert.Single(stateStore.GetRequests));
        var delete = Assert.IsType<DeleteFileSyncAction>(Assert.Single(plan.Actions));
        Assert.Equal("deleted.txt", delete.RelativePath);
        Assert.Equal(DeleteFileReason.SourceDeletion, delete.Reason);
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
    public async Task ApplyPlanAsync_persists_snapshots_and_result_after_successful_action()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var sourceFile = new SyncFile("notes.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5);
        var sourceProvider = InMemorySyncLocationProvider.WithFiles(sourceLocation, sourceFile);
        var targetProvider = InMemorySyncLocationProvider.Empty(targetLocation);
        var stateStore = new InMemorySyncStateStore();
        var resultStore = new InMemorySyncResultStore();
        var service = new SyncApplicationService(
            [sourceProvider, targetProvider],
            stateStore,
            resultStore,
            new SyncResultRetention(2));
        var plan = new SyncPlan(syncPair, [new CopyFileSyncAction(sourceFile.RelativePath)]);

        // Act
        await service.ApplyPlanAsync(plan, CancellationToken.None);

        // Assert
        var savedState = stateStore.SavedStates[syncPair];
        Assert.True(savedState.SourceState.ContainsFile("notes.txt"));
        Assert.True(savedState.TargetState.ContainsFile("notes.txt"));
        var retainedResult = Assert.Single(resultStore.Results);
        Assert.Equal(2, retainedResult.Retention.MaximumResults);
    }

    [Fact]
    public async Task ApplyPlanAsync_persists_initial_baseline_after_successful_no_change_run()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var stateStore = new InMemorySyncStateStore();
        var service = new SyncApplicationService(
            [InMemorySyncLocationProvider.Empty(sourceLocation), InMemorySyncLocationProvider.Empty(targetLocation)],
            stateStore);

        // Act
        await service.ApplyPlanAsync(SyncPlan.Empty(syncPair), CancellationToken.None);

        // Assert
        var savedState = stateStore.SavedStates[syncPair];
        Assert.Empty(savedState.SourceState.Files);
        Assert.Empty(savedState.TargetState.Files);
    }

    [Fact]
    public async Task ApplyPlanAsync_does_not_replace_baseline_after_partially_failed_run()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var stateStore = new InMemorySyncStateStore();
        var previousFile = new SyncFile("previous.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5);
        var previousState = new SyncPairState(new SyncState([previousFile]), new SyncState([previousFile]));
        stateStore.SavedStates[syncPair] = previousState;
        var service = new SyncApplicationService(
            [InMemorySyncLocationProvider.Empty(sourceLocation), InMemorySyncLocationProvider.Empty(targetLocation)],
            stateStore);
        var plan = new SyncPlan(
            syncPair,
            [new CopyFileSyncAction("missing.txt"), new CreateDirectorySyncAction("created")]);

        // Act
        var result = await service.ApplyPlanAsync(plan, CancellationToken.None);

        // Assert
        Assert.Contains(result.Outcomes, outcome => outcome.Status == SyncActionStatus.Failed);
        Assert.Contains(result.Outcomes, outcome => outcome.Status == SyncActionStatus.Applied);
        Assert.Same(previousState, stateStore.SavedStates[syncPair]);
    }

    [Fact]
    public async Task ApplyPlanAsync_does_not_persist_incomplete_snapshots()
    {
        // Arrange
        var sourceLocation = new SyncLocation("source");
        var targetLocation = new SyncLocation("target");
        var syncPair = new SyncPair(SyncMode.OneWay, sourceLocation, targetLocation);
        var sourceFile = new SyncFile("notes.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5);
        var listingProblem = new SyncLocationProblem(
            SyncLocationProblemKind.AccessProblem,
            string.Empty,
            "Source listing is incomplete.");
        var sourceProvider = InMemorySyncLocationProvider.WithFilesAndProblems(
            sourceLocation,
            [sourceFile],
            [listingProblem]);
        var targetProvider = InMemorySyncLocationProvider.Empty(targetLocation);
        var stateStore = new InMemorySyncStateStore();
        var service = new SyncApplicationService([sourceProvider, targetProvider], stateStore);
        var plan = new SyncPlan(syncPair, [new CopyFileSyncAction(sourceFile.RelativePath)]);

        // Act
        await service.ApplyPlanAsync(plan, CancellationToken.None);

        // Assert
        Assert.Empty(stateStore.SavedStates);
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
        private readonly string? _localRootPath;

        private InMemorySyncLocationProvider(
            SyncLocation location,
            IEnumerable<StoredFile> files,
            IEnumerable<SyncDirectory> directories,
            IEnumerable<SyncSymbolicLink> symbolicLinks,
            IEnumerable<SyncLocationProblem> problems,
            SyncLocationProblem? readFailure,
            bool supportsSymbolicLinkPreservation = false,
            string? localRootPath = null)
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
            _localRootPath = localRootPath;
        }

        public SyncLocation Location { get; }

        public bool SupportsSymbolicLinkPreservation => _supportsSymbolicLinkPreservation;

        public string? LocalRootPath => _localRootPath;

        public static InMemorySyncLocationProvider Empty(
            SyncLocation location,
            params SyncLocationProblem[] problems)
        {
            return new InMemorySyncLocationProvider(location, [], [], [], problems, readFailure: null);
        }

        public static InMemorySyncLocationProvider WithLocalRoot(SyncLocation location, string localRootPath)
        {
            return new InMemorySyncLocationProvider(
                location,
                [],
                [],
                [],
                [],
                readFailure: null,
                localRootPath: localRootPath);
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

        public static InMemorySyncLocationProvider WithFilesAndProblems(
            SyncLocation location,
            IReadOnlyCollection<SyncFile> files,
            IReadOnlyCollection<SyncLocationProblem> problems)
        {
            return new InMemorySyncLocationProvider(
                location,
                files.Select(file => new StoredFile(file, Encoding.UTF8.GetBytes(file.RelativePath))),
                [],
                [],
                problems,
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

    private sealed class InMemorySyncStateStore : ISyncStateStore
    {
        public Dictionary<SyncPair, SyncPairState> SavedStates { get; } = [];

        public List<SyncPair> GetRequests { get; } = [];

        public Task<SyncPairState?> GetStateAsync(SyncPair syncPair, CancellationToken cancellationToken)
        {
            GetRequests.Add(syncPair);
            return Task.FromResult(SavedStates.GetValueOrDefault(syncPair));
        }

        public Task SaveStateAsync(SyncPair syncPair, SyncPairState state, CancellationToken cancellationToken)
        {
            SavedStates[syncPair] = state;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySyncResultStore : ISyncResultStore
    {
        public List<(RetainedSyncResult Result, SyncResultRetention Retention)> Results { get; } = [];

        public Task SaveResultAsync(
            SyncPair syncPair,
            RetainedSyncResult result,
            SyncResultRetention retention,
            CancellationToken cancellationToken)
        {
            Results.Add((result, retention));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<RetainedSyncResult>> GetResultsAsync(
            SyncPair syncPair,
            CancellationToken cancellationToken)
        {
            IReadOnlyCollection<RetainedSyncResult> results = Results
                .Select(entry => entry.Result)
                .ToList();
            return Task.FromResult(results);
        }
    }
}
