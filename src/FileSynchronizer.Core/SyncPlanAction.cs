namespace FileSynchronizer.Core;

public abstract record SyncPlanAction
{
    private protected SyncPlanAction(string relativePath)
    {
        RelativePath = relativePath;
    }

    public string RelativePath { get; }
}
