namespace FileSynchronizer.Core;

public sealed record DeleteFileSyncAction(
    string RelativePath,
    DeleteFileReason Reason) : SyncPlanAction(RelativePath)
{
    public DeleteFileSyncAction(string relativePath)
        : this(relativePath, DeleteFileReason.TargetOnlyFile)
    {
    }
}
