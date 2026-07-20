namespace FileSynchronizer.Core;

public sealed record SyncState(IReadOnlyCollection<SyncFile> Files)
{
    public bool ContainsFile(string relativePath)
    {
        return Files.Any(file => file.RelativePath == relativePath);
    }
}
