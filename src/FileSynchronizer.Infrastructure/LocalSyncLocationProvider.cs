using FileSynchronizer.Core;

namespace FileSynchronizer.Infrastructure;

public sealed class LocalSyncLocationProvider : ISyncLocationProvider
{
    private readonly string _rootPath;

    public LocalSyncLocationProvider(SyncLocation location)
    {
        Location = location;
        _rootPath = Path.GetFullPath(location.Value);
    }

    public SyncLocation Location { get; }

    public bool SupportsSymbolicLinkPreservation => false;

    public async Task<IReadOnlyCollection<SyncFile>> ListFilesAsync(CancellationToken cancellationToken)
    {
        var listing = await ListEntriesAsync(cancellationToken);

        return listing.Files;
    }

    public Task<SyncLocationListing> ListEntriesAsync(CancellationToken cancellationToken)
    {
        var files = new List<SyncFile>();
        var emptyDirectories = new List<SyncDirectory>();
        var symbolicLinks = new List<SyncSymbolicLink>();
        var problems = new List<SyncLocationProblem>();

        if (!Directory.Exists(_rootPath))
        {
            problems.Add(new SyncLocationProblem(
                SyncLocationProblemKind.AccessProblem,
                string.Empty,
                $"Local sync location '{_rootPath}' does not exist."));
            return Task.FromResult(new SyncLocationListing(files, emptyDirectories, symbolicLinks, problems));
        }

        AddDirectoryEntries(_rootPath, files, emptyDirectories, symbolicLinks, problems, cancellationToken);

        return Task.FromResult(new SyncLocationListing(files, emptyDirectories, symbolicLinks, problems));
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
    {
        var path = ResolvePath(relativePath);
        ThrowIfPathContainsSymbolicLink(path);

        try
        {
            Stream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

            return Task.FromResult(stream);
        }
        catch (Exception exception) when (IsAccessException(exception))
        {
            throw CreateException(SyncLocationProblemKind.AccessProblem, path, exception);
        }
    }

    public async Task WriteFileAsync(
        string relativePath,
        Stream content,
        DateTimeOffset lastModifiedUtc,
        CancellationToken cancellationToken)
    {
        var path = ResolvePath(relativePath);
        ThrowIfPathContainsSymbolicLink(path);

        try
        {
            var directoryPath = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            await using (var output = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                await content.CopyToAsync(output, cancellationToken);
            }

            File.SetLastWriteTimeUtc(path, lastModifiedUtc.UtcDateTime);
        }
        catch (Exception exception) when (IsAccessException(exception))
        {
            throw CreateException(SyncLocationProblemKind.AccessProblem, path, exception);
        }
    }

    public Task DeleteFileAsync(string relativePath, CancellationToken cancellationToken)
    {
        var path = ResolvePath(relativePath);
        ThrowIfPathContainsSymbolicLink(path);

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (IsAccessException(exception))
        {
            throw CreateException(SyncLocationProblemKind.AccessProblem, path, exception);
        }

        return Task.CompletedTask;
    }

    public Task CreateDirectoryAsync(string relativePath, CancellationToken cancellationToken)
    {
        var path = ResolvePath(relativePath);
        ThrowIfPathContainsSymbolicLink(path);

        try
        {
            Directory.CreateDirectory(path);
        }
        catch (Exception exception) when (IsAccessException(exception))
        {
            throw CreateException(SyncLocationProblemKind.AccessProblem, path, exception);
        }

        return Task.CompletedTask;
    }

    public Task DeleteDirectoryAsync(string relativePath, CancellationToken cancellationToken)
    {
        var path = ResolvePath(relativePath);
        ThrowIfPathContainsSymbolicLink(path);

        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path);
            }
        }
        catch (Exception exception) when (IsAccessException(exception))
        {
            throw CreateException(SyncLocationProblemKind.AccessProblem, path, exception);
        }

        return Task.CompletedTask;
    }

    private void AddDirectoryEntries(
        string directoryPath,
        List<SyncFile> files,
        List<SyncDirectory> emptyDirectories,
        List<SyncSymbolicLink> symbolicLinks,
        List<SyncLocationProblem> problems,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        FileSystemInfo[] entries;
        try
        {
            entries = new DirectoryInfo(directoryPath).GetFileSystemInfos();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            problems.Add(CreateProblem(SyncLocationProblemKind.AccessProblem, directoryPath, exception));
            return;
        }

        if (entries.Length == 0 && !PathsEqual(directoryPath, _rootPath))
        {
            emptyDirectories.Add(new SyncDirectory(ToRelativePath(directoryPath)));
            return;
        }

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsSymbolicLink(entry))
            {
                symbolicLinks.Add(new SyncSymbolicLink(
                    ToRelativePath(entry.FullName),
                    entry.LinkTarget ?? string.Empty));
                continue;
            }

            if (entry is DirectoryInfo directory)
            {
                AddDirectoryEntries(
                    directory.FullName,
                    files,
                    emptyDirectories,
                    symbolicLinks,
                    problems,
                    cancellationToken);
                continue;
            }

            if (entry is FileInfo file)
            {
                AddFile(file, files, problems);
            }
        }
    }

    private void AddFile(
        FileInfo file,
        List<SyncFile> files,
        List<SyncLocationProblem> problems)
    {
        try
        {
            files.Add(new SyncFile(
                ToRelativePath(file.FullName),
                new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero),
                file.Length));
        }
        catch (Exception exception) when (IsAccessException(exception))
        {
            problems.Add(CreateProblem(SyncLocationProblemKind.AccessProblem, file.FullName, exception));
        }
    }

    private SyncLocationProblem CreateProblem(
        SyncLocationProblemKind kind,
        string path,
        Exception exception)
    {
        return CreateProblem(kind, path, exception.Message);
    }

    private SyncLocationProblem CreateProblem(
        SyncLocationProblemKind kind,
        string path,
        string message)
    {
        return new SyncLocationProblem(kind, ToRelativePath(path), message);
    }

    private string ResolvePath(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        var rootWithSeparator = _rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!path.StartsWith(rootWithSeparator, PathComparison)
            && !PathsEqual(path, _rootPath))
        {
            throw new InvalidOperationException($"Relative path '{relativePath}' escapes the sync location.");
        }

        return path;
    }

    private string ToRelativePath(string path)
    {
        if (PathsEqual(path, _rootPath))
        {
            return string.Empty;
        }

        return Path.GetRelativePath(_rootPath, path).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static bool IsSymbolicLink(FileSystemInfo entry)
    {
        return entry.LinkTarget is not null;
    }

    private static bool IsAccessException(Exception exception)
    {
        return exception is UnauthorizedAccessException or IOException;
    }

    private void ThrowIfPathContainsSymbolicLink(string path)
    {
        var relativePath = Path.GetRelativePath(_rootPath, path);
        if (relativePath == ".")
        {
            return;
        }

        var currentPath = _rootPath;
        foreach (var segment in relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            if (!File.Exists(currentPath) && !Directory.Exists(currentPath))
            {
                continue;
            }

            try
            {
                if ((File.GetAttributes(currentPath) & FileAttributes.ReparsePoint) == 0)
                {
                    continue;
                }
            }
            catch (Exception exception) when (IsAccessException(exception))
            {
                throw CreateException(SyncLocationProblemKind.AccessProblem, currentPath, exception);
            }

            throw CreateException(
                SyncLocationProblemKind.UnsupportedSymbolicLink,
                currentPath,
                "Symbolic links are not supported by file content operations.");
        }
    }

    private SyncLocationProviderException CreateException(
        SyncLocationProblemKind kind,
        string path,
        Exception exception)
    {
        return CreateException(kind, path, exception.Message);
    }

    private SyncLocationProviderException CreateException(
        SyncLocationProblemKind kind,
        string path,
        string message)
    {
        return new SyncLocationProviderException(CreateProblem(kind, path, message));
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            PathComparison);
    }

    private static StringComparison PathComparison
        => OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
}
