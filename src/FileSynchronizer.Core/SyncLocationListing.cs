namespace FileSynchronizer.Core;

public sealed record SyncLocationListing(
    IReadOnlyCollection<SyncFile> Files,
    IReadOnlyCollection<SyncDirectory> EmptyDirectories,
    IReadOnlyCollection<SyncSymbolicLink> SymbolicLinks,
    IReadOnlyCollection<SyncLocationProblem> Problems,
    IReadOnlyCollection<string>? HiddenEntries = null)
{
    public IReadOnlyCollection<string> HiddenEntries { get; init; } = HiddenEntries ?? [];
}
