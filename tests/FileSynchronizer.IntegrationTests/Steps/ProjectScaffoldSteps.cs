using FileSynchronizer.Core;
using Reqnroll;

namespace FileSynchronizer.IntegrationTests.Steps;

[Binding]
public sealed class ProjectScaffoldSteps
{
    private readonly SyncApplicationService _syncApplicationService = new();
    private SyncPair? _syncPair;
    private SyncPlan? _syncPlan;

    [Given("an empty one-way sync pair")]
    public void GivenAnEmptyOneWaySyncPair()
    {
        // Arrange
        var syncPair = new SyncPair(SyncMode.OneWay, "source", "target");

        // Act
        _syncPair = syncPair;
    }

    [When("the sync preview is requested")]
    public void WhenTheSyncPreviewIsRequested()
    {
        // Arrange
        Assert.NotNull(_syncPair);

        // Act
        _syncPlan = _syncApplicationService.Preview(_syncPair);
    }

    [Then("the sync plan should contain no file actions")]
    public void ThenTheSyncPlanShouldContainNoFileActions()
    {
        // Assert
        Assert.NotNull(_syncPlan);
        Assert.Empty(_syncPlan.Actions);
    }
}
