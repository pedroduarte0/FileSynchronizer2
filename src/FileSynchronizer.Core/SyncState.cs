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

    public IReadOnlyCollection<SyncFile> GetDeletedFiles(SyncState currentState)
    {
        var currentPaths = currentState.Files
            .Select(file => file.RelativePath)
            .ToHashSet(StringComparer.Ordinal);

        return Files
            .Where(file => !currentPaths.Contains(file.RelativePath))
            .ToList();
    }
}
