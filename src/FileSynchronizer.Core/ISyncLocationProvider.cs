namespace FileSynchronizer.Core;

public interface ISyncLocationProvider
{
    SyncLocation Location { get; }

    string? LocalRootPath => null;

    StringComparer RelativePathComparer => StringComparer.Ordinal;

    bool SupportsSymbolicLinkPreservation => false;

    Task<long?> GetAvailableStorageBytesAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<long?>(null);
    }

    Task<IReadOnlyCollection<SyncFile>> ListFilesAsync(CancellationToken cancellationToken);

    async Task<SyncLocationListing> ListEntriesAsync(CancellationToken cancellationToken)
    {
        var files = await ListFilesAsync(cancellationToken);

        return new SyncLocationListing(files, [], [], []);
    }

    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken);

    Task WriteFileAsync(
        string relativePath,
        Stream content,
        DateTimeOffset lastModifiedUtc,
        CancellationToken cancellationToken);

    Task DeleteFileAsync(string relativePath, CancellationToken cancellationToken);

    Task CreateDirectoryAsync(string relativePath, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    Task DeleteDirectoryAsync(string relativePath, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
