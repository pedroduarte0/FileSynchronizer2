using FileSynchronizer.Core;

namespace FileSynchronizer.Infrastructure.Tests;

public sealed class SqliteSyncStoreTests : IDisposable
{
    private readonly string _rootPath;

    public SqliteSyncStoreTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "FileSynchronizer2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public async Task SaveStateAsync_persists_pair_snapshot_and_distinguishes_deleted_files()
    {
        // Arrange
        var store = CreateStore("state.db");
        var syncPair = CreatePair("target");
        var file = new SyncFile("notes.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5);
        var previousState = new SyncPairState(new SyncState([file]), new SyncState([file]));

        // Act
        await store.SaveStateAsync(syncPair, previousState, CancellationToken.None);
        var savedState = await store.GetStateAsync(syncPair, CancellationToken.None);
        await store.SaveStateAsync(
            syncPair,
            new SyncPairState(new SyncState([]), new SyncState([file])),
            CancellationToken.None);
        var currentState = await store.GetStateAsync(syncPair, CancellationToken.None);

        // Assert
        Assert.NotNull(savedState);
        Assert.NotNull(currentState);
        Assert.Equal(
            "notes.txt",
            Assert.Single(savedState.SourceState.GetDeletedFiles(currentState.SourceState)).RelativePath);
    }

    [Fact]
    public async Task SaveStateAsync_keeps_snapshots_separate_for_pairs_that_share_a_location()
    {
        // Arrange
        var store = CreateStore("pairs.db");
        var firstPair = CreatePair("target-a");
        var secondPair = CreatePair("target-b");
        var firstFile = new SyncFile("first.txt", DateTimeOffset.Parse("2026-07-20T10:00:00Z"), 5);
        var secondFile = new SyncFile("second.txt", DateTimeOffset.Parse("2026-07-20T11:00:00Z"), 6);

        // Act
        await store.SaveStateAsync(
            firstPair,
            new SyncPairState(new SyncState([firstFile]), new SyncState([firstFile])),
            CancellationToken.None);
        await store.SaveStateAsync(
            secondPair,
            new SyncPairState(new SyncState([secondFile]), new SyncState([secondFile])),
            CancellationToken.None);
        var firstState = await store.GetStateAsync(firstPair, CancellationToken.None);
        var secondState = await store.GetStateAsync(secondPair, CancellationToken.None);

        // Assert
        Assert.Equal("first.txt", Assert.Single(Assert.IsType<SyncPairState>(firstState).SourceState.Files).RelativePath);
        Assert.Equal("second.txt", Assert.Single(Assert.IsType<SyncPairState>(secondState).SourceState.Files).RelativePath);
    }

    [Fact]
    public async Task Stored_baseline_identifies_source_deletion_after_service_restart()
    {
        // Arrange
        var sourcePath = Path.Combine(_rootPath, "source");
        var targetPath = Path.Combine(_rootPath, "target");
        var databasePath = Path.Combine(_rootPath, "restart.db");
        Directory.CreateDirectory(sourcePath);
        Directory.CreateDirectory(targetPath);
        var sourceFilePath = Path.Combine(sourcePath, "notes.txt");
        var targetFilePath = Path.Combine(targetPath, "notes.txt");
        await File.WriteAllTextAsync(sourceFilePath, "hello");
        await File.WriteAllTextAsync(targetFilePath, "hello");
        var lastModifiedUtc = DateTime.Parse("2026-07-20T10:00:00Z").ToUniversalTime();
        File.SetLastWriteTimeUtc(sourceFilePath, lastModifiedUtc);
        File.SetLastWriteTimeUtc(targetFilePath, lastModifiedUtc);
        var syncPair = new SyncPair(
            SyncMode.OneWay,
            new SyncLocation(sourcePath),
            new SyncLocation(targetPath));
        var initialStore = new SqliteSyncStore(databasePath);
        var initialService = new SyncApplicationService(
            [
                new LocalSyncLocationProvider(syncPair.SourceLocation),
                new LocalSyncLocationProvider(syncPair.TargetLocation),
            ],
            initialStore);
        var initialPlan = await initialService.BuildPlanAsync(syncPair, CancellationToken.None);
        await initialService.ApplyPlanAsync(initialPlan, CancellationToken.None);
        File.Delete(sourceFilePath);

        // Act
        var restartedStore = new SqliteSyncStore(databasePath);
        var restartedService = new SyncApplicationService(
            [
                new LocalSyncLocationProvider(syncPair.SourceLocation),
                new LocalSyncLocationProvider(syncPair.TargetLocation),
            ],
            restartedStore);
        var planAfterRestart = await restartedService.BuildPlanAsync(syncPair, CancellationToken.None);

        // Assert
        var delete = Assert.IsType<DeleteFileSyncAction>(Assert.Single(planAfterRestart.Actions));
        Assert.Equal(DeleteFileReason.SourceDeletion, delete.Reason);
    }

    [Fact]
    public async Task SaveResultAsync_retains_only_configured_number_of_results()
    {
        // Arrange
        var store = CreateStore("results.db");
        var syncPair = CreatePair("target");
        var retention = new SyncResultRetention(2);
        var initialTime = DateTimeOffset.Parse("2026-07-20T10:00:00Z");

        // Act
        for (var index = 0; index < 3; index++)
        {
            var result = new SyncResult(
                [new SyncActionOutcome(new CopyFileSyncAction($"notes-{index}.txt"), SyncActionStatus.Applied)],
                new SyncState([]),
                []);
            await store.SaveResultAsync(
                syncPair,
                new RetainedSyncResult(initialTime.AddMinutes(index), result),
                retention,
                CancellationToken.None);
        }

        var results = await store.GetResultsAsync(syncPair, CancellationToken.None);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal(initialTime.AddMinutes(2), results.First().CompletedAtUtc);
        Assert.IsType<CopyFileSyncAction>(Assert.Single(results.First().Result.Outcomes).Action);
    }

    [Fact]
    public async Task SaveResultAsync_round_trips_all_result_details()
    {
        // Arrange
        var store = CreateStore("result-details.db");
        var syncPair = CreatePair("target");
        var completedAtUtc = DateTimeOffset.Parse("2026-07-20T10:00:00Z");
        var file = new SyncFile("notes.txt", completedAtUtc, 5);
        var problem = new SyncLocationProblem(
            SyncLocationProblemKind.AccessProblem,
            "blocked.txt",
            "The file cannot be accessed.");
        var result = new SyncResult(
            [
                new SyncActionOutcome(new CopyFileSyncAction("copy.txt"), SyncActionStatus.Applied),
                new SyncActionOutcome(new OverwriteFileSyncAction("overwrite.txt"), SyncActionStatus.Applied),
                new SyncActionOutcome(
                    new DeleteFileSyncAction("delete.txt", DeleteFileReason.SourceDeletion),
                    SyncActionStatus.Applied),
                new SyncActionOutcome(new CreateDirectorySyncAction("create"), SyncActionStatus.Applied),
                new SyncActionOutcome(new DeleteDirectorySyncAction("remove"), SyncActionStatus.Failed),
            ],
            new SyncState([file], [problem]),
            [problem]);

        // Act
        await store.SaveResultAsync(
            syncPair,
            new RetainedSyncResult(completedAtUtc, result),
            new SyncResultRetention(1),
            CancellationToken.None);
        var storedResult = Assert.Single(await store.GetResultsAsync(syncPair, CancellationToken.None));

        // Assert
        Assert.Equal(completedAtUtc, storedResult.CompletedAtUtc);
        Assert.Collection(
            storedResult.Result.Outcomes,
            outcome => Assert.IsType<CopyFileSyncAction>(outcome.Action),
            outcome => Assert.IsType<OverwriteFileSyncAction>(outcome.Action),
            outcome => Assert.Equal(
                DeleteFileReason.SourceDeletion,
                Assert.IsType<DeleteFileSyncAction>(outcome.Action).Reason),
            outcome => Assert.IsType<CreateDirectorySyncAction>(outcome.Action),
            outcome =>
            {
                Assert.IsType<DeleteDirectorySyncAction>(outcome.Action);
                Assert.Equal(SyncActionStatus.Failed, outcome.Status);
            });
        Assert.Equal(file, Assert.Single(storedResult.Result.UpdatedState.Files));
        Assert.Equal(problem, Assert.Single(storedResult.Result.UpdatedState.Problems));
        Assert.Equal(problem, Assert.Single(storedResult.Result.Problems));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private SqliteSyncStore CreateStore(string databaseName)
    {
        return new SqliteSyncStore(Path.Combine(_rootPath, databaseName));
    }

    private static SyncPair CreatePair(string targetLocation)
    {
        return new SyncPair(
            SyncMode.OneWay,
            new SyncLocation("source"),
            new SyncLocation(targetLocation));
    }
}
