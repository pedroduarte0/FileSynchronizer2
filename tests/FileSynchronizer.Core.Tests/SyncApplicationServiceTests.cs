namespace FileSynchronizer.Core.Tests;

public sealed class SyncApplicationServiceTests
{
    [Fact]
    public void Preview_returns_empty_plan_for_empty_one_way_sync_pair()
    {
        // Arrange
        var service = new SyncApplicationService();
        var syncPair = new SyncPair(SyncMode.OneWay, "source", "target");

        // Act
        var plan = service.Preview(syncPair);

        // Assert
        Assert.Empty(plan.Actions);
    }
}
