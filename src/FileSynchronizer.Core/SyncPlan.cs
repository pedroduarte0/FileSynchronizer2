namespace FileSynchronizer.Core;

public sealed record SyncPlan(IReadOnlyCollection<string> Actions)
{
    public static SyncPlan Empty { get; } = new([]);
}
