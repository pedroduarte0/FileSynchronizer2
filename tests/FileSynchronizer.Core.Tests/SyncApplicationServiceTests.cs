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

        private InMemorySyncLocationProvider(SyncLocation location, IEnumerable<StoredFile> files)
        {
            Location = location;
            _files = files.ToDictionary(file => file.Metadata.RelativePath, StringComparer.Ordinal);
        }

        public SyncLocation Location { get; }

        public static InMemorySyncLocationProvider Empty(SyncLocation location)
        {
            return new InMemorySyncLocationProvider(location, []);
        }

        public static InMemorySyncLocationProvider WithFiles(SyncLocation location, params SyncFile[] files)
        {
            return new InMemorySyncLocationProvider(
                location,
                files.Select(file => new StoredFile(file, Encoding.UTF8.GetBytes(file.RelativePath))));
        }

        public Task<IReadOnlyCollection<SyncFile>> ListFilesAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<SyncFile> files = _files.Values.Select(file => file.Metadata).ToList();
            return Task.FromResult(files);
        }

        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
        {
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

        public bool ContainsFile(string relativePath)
        {
            return _files.ContainsKey(relativePath);
        }

        private sealed record StoredFile(SyncFile Metadata, byte[] Content);
    }
}
