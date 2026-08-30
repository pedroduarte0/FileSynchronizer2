namespace FileSynchronizer.Core;

public sealed record SyncResultRetention
{
    public SyncResultRetention(int maximumResults)
    {
        if (maximumResults <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumResults),
                "Result retention must keep at least one sync result.");
        }

        MaximumResults = maximumResults;
    }

    public int MaximumResults { get; }
}
