namespace FileSynchronizer.Core;

public sealed record SyncState(
    IReadOnlyCollection<SyncFile> Files,
    IReadOnlyCollection<SyncLocationProblem> Problems)
{
    public SyncState(IReadOnlyCollection<SyncFile> files)
        : this(files, [])
    {
    }

    public bool ContainsFile(string relativePath)
    {
        return Files.Any(file => file.RelativePath == relativePath);
    }
}
