namespace FileSynchronizer.Core.Tests;

public sealed class SyncReadinessTests
{
    [Fact]
    public void Supports_returns_true_for_configured_sync_mode()
    {
        // Arrange
        var readiness = new SyncReadiness([SyncMode.OneWay]);

        // Act
        var supportsOneWaySync = readiness.Supports(SyncMode.OneWay);

        // Assert
        Assert.True(supportsOneWaySync);
    }
}
