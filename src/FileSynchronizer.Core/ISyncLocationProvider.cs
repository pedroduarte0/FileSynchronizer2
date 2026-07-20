namespace FileSynchronizer.Core;

public interface ISyncLocationProvider
{
    SyncLocation Location { get; }

    Task<IReadOnlyCollection<SyncFile>> ListFilesAsync(CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken);

    Task WriteFileAsync(
        string relativePath,
        Stream content,
        DateTimeOffset lastModifiedUtc,
        CancellationToken cancellationToken);

    Task DeleteFileAsync(string relativePath, CancellationToken cancellationToken);
}
