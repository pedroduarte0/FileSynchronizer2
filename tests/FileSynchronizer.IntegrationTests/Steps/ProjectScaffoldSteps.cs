using FileSynchronizer.Core;
using Reqnroll;

namespace FileSynchronizer.IntegrationTests.Steps;

[Binding]
public sealed class ProjectScaffoldSteps
{
    private SyncPair? _syncPair;
    private SyncPlan? _syncPlan;

    [Given("an empty one-way sync pair")]
    public void GivenAnEmptyOneWaySyncPair()
    {
        // Arrange
        var syncPair = new SyncPair(
            SyncMode.OneWay,
            new SyncLocation("source"),
            new SyncLocation("target"));

        // Act
        _syncPair = syncPair;
    }

    [When("the sync preview is requested")]
    public void WhenTheSyncPreviewIsRequested()
    {
        // Arrange
        Assert.NotNull(_syncPair);
        var service = new SyncApplicationService(
            [
                EmptySyncLocationProvider.For(_syncPair.SourceLocation),
                EmptySyncLocationProvider.For(_syncPair.TargetLocation),
            ]);

        // Act
        _syncPlan = service.BuildPlanAsync(_syncPair, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Then("the sync plan should contain no file actions")]
    public void ThenTheSyncPlanShouldContainNoFileActions()
    {
        // Assert
        Assert.NotNull(_syncPlan);
        Assert.Empty(_syncPlan.Actions);
    }

    private sealed class EmptySyncLocationProvider : ISyncLocationProvider
    {
        private EmptySyncLocationProvider(SyncLocation location)
        {
            Location = location;
        }

        public SyncLocation Location { get; }

        public static EmptySyncLocationProvider For(SyncLocation location)
        {
            return new EmptySyncLocationProvider(location);
        }

        public Task<IReadOnlyCollection<SyncFile>> ListFilesAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<SyncFile> files = [];
            return Task.FromResult(files);
        }

        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The empty provider has no files to read.");
        }

        public Task WriteFileAsync(
            string relativePath,
            Stream content,
            DateTimeOffset lastModifiedUtc,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The empty provider should not receive writes.");
        }

        public Task DeleteFileAsync(string relativePath, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The empty provider has no files to delete.");
        }
    }
}
