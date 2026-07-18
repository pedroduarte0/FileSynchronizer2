using FileSynchronizer.Core;
using FileSynchronizer.Infrastructure;
using Reqnroll;

namespace FileSynchronizer.IntegrationTests.Steps;

[Binding]
public sealed class ProjectScaffoldSteps
{
    private SyncReadinessProvider? _provider;
    private SyncReadiness? _readiness;

    [Given("the sync readiness provider is available")]
    public void GivenTheSyncReadinessProviderIsAvailable()
    {
        // Arrange
        var provider = new SyncReadinessProvider();

        // Act
        _provider = provider;
    }

    [When("the integration test asks which sync modes are supported")]
    public void WhenTheIntegrationTestAsksWhichSyncModesAreSupported()
    {
        // Arrange
        Assert.NotNull(_provider);

        // Act
        _readiness = _provider.GetReadiness();
    }

    [Then("one-way sync should be supported")]
    public void ThenOneWaySyncShouldBeSupported()
    {
        // Assert
        Assert.NotNull(_readiness);
        Assert.True(_readiness.Supports(SyncMode.OneWay));
    }

    [Then("two-way sync should be supported")]
    public void ThenTwoWaySyncShouldBeSupported()
    {
        // Assert
        Assert.NotNull(_readiness);
        Assert.True(_readiness.Supports(SyncMode.TwoWay));
    }
}
