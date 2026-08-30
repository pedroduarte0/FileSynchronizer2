using FileSynchronizer.Core;
using FileSynchronizer.Infrastructure;
using Reqnroll;

namespace FileSynchronizer.IntegrationTests.Steps;

[Binding]
public sealed class ProjectScaffoldSteps : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "FileSynchronizer2",
        Guid.NewGuid().ToString("N"));
    private SyncPair? _syncPair;
    private SyncPlan? _syncPlan;

    [Given("a local one-way sync pair")]
    public void GivenALocalOneWaySyncPair()
    {
        // Arrange
        _syncPair = new SyncPair(
            SyncMode.OneWay,
            new SyncLocation(Path.Combine(_rootPath, "source")),
            new SyncLocation(Path.Combine(_rootPath, "target")));
        Directory.CreateDirectory(_syncPair.SourceLocation.Value);
        Directory.CreateDirectory(_syncPair.TargetLocation.Value);
    }

    [Given("the source location contains {string} with content {string}")]
    public void GivenTheSourceLocationContainsWithContent(string relativePath, string content)
    {
        WriteFile(_syncPair!.SourceLocation, relativePath, content);
    }

    [Given("the target location contains {string} with content {string}")]
    public void GivenTheTargetLocationContainsWithContent(string relativePath, string content)
    {
        WriteFile(_syncPair!.TargetLocation, relativePath, content);
    }

    [Given("overlapping local sync locations")]
    public void GivenOverlappingLocalSyncLocations()
    {
        var sourcePath = Path.Combine(_rootPath, "source");
        _syncPair = new SyncPair(
            SyncMode.OneWay,
            new SyncLocation(sourcePath),
            new SyncLocation(Path.Combine(sourcePath, "target")));
        Directory.CreateDirectory(_syncPair.SourceLocation.Value);
        Directory.CreateDirectory(_syncPair.TargetLocation.Value);
    }

    [When("the sync preview is requested")]
    public void WhenTheSyncPreviewIsRequested()
    {
        // Arrange
        Assert.NotNull(_syncPair);
        var service = new SyncApplicationService(
            [
                new LocalSyncLocationProvider(_syncPair.SourceLocation),
                new LocalSyncLocationProvider(_syncPair.TargetLocation),
            ]);

        // Act
        _syncPlan = service.BuildPlanAsync(_syncPair, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Then("the sync plan should contain a copy action for {string}")]
    public void ThenTheSyncPlanShouldContainACopyActionFor(string relativePath)
    {
        // Assert
        Assert.NotNull(_syncPlan);
        Assert.Contains(_syncPlan.Actions, action => action is CopyFileSyncAction && action.RelativePath == relativePath);
    }

    [Then("the sync plan should contain an overwrite action for {string}")]
    public void ThenTheSyncPlanShouldContainAnOverwriteActionFor(string relativePath)
    {
        Assert.NotNull(_syncPlan);
        Assert.Contains(_syncPlan.Actions, action => action is OverwriteFileSyncAction && action.RelativePath == relativePath);
    }

    [Then("the sync plan should contain a delete action for {string}")]
    public void ThenTheSyncPlanShouldContainADeleteActionFor(string relativePath)
    {
        Assert.NotNull(_syncPlan);
        Assert.Contains(_syncPlan.Actions, action => action is DeleteFileSyncAction && action.RelativePath == relativePath);
    }

    [Then("the sync plan should report an overlapping location")]
    public void ThenTheSyncPlanShouldReportAnOverlappingLocation()
    {
        Assert.NotNull(_syncPlan);
        Assert.Contains(_syncPlan.Problems, problem => problem.Kind == SyncLocationProblemKind.OverlappingLocation);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private static void WriteFile(SyncLocation location, string relativePath, string content)
    {
        var path = Path.Combine(location.Value, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
    }
}
