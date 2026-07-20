using System.Text;
using FileSynchronizer.Core;

namespace FileSynchronizer.Infrastructure.Tests;

public sealed class LocalSyncLocationProviderTests : IDisposable
{
    private readonly string _rootPath;

    public LocalSyncLocationProviderTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "FileSynchronizer2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public async Task ListEntriesAsync_returns_files_metadata_and_empty_directories()
    {
        // Arrange
        var provider = CreateProvider();
        var filePath = Path.Combine(_rootPath, "docs", "notes.txt");
        var emptyDirectoryPath = Path.Combine(_rootPath, "empty");
        var lastModifiedUtc = DateTimeOffset.Parse("2026-07-20T10:00:00Z");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        Directory.CreateDirectory(emptyDirectoryPath);
        await File.WriteAllTextAsync(filePath, "hello");
        File.SetLastWriteTimeUtc(filePath, lastModifiedUtc.UtcDateTime);

        // Act
        var listing = await provider.ListEntriesAsync(CancellationToken.None);

        // Assert
        var file = Assert.Single(listing.Files);
        Assert.Equal("docs/notes.txt", file.RelativePath);
        Assert.Equal(5, file.Size);
        Assert.Equal(lastModifiedUtc, file.LastModifiedUtc);
        Assert.Contains(listing.EmptyDirectories, directory => directory.RelativePath == "empty");
        Assert.Empty(listing.SymbolicLinks);
        Assert.Empty(listing.Problems);
    }

    [Fact]
    public async Task OpenReadAsync_streams_file_contents()
    {
        // Arrange
        var provider = CreateProvider();
        var filePath = Path.Combine(_rootPath, "notes.txt");
        await File.WriteAllTextAsync(filePath, "hello");

        // Act
        await using var stream = await provider.OpenReadAsync("notes.txt", CancellationToken.None);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var contents = await reader.ReadToEndAsync();

        // Assert
        Assert.Equal("hello", contents);
    }

    [Fact]
    public async Task OpenReadAsync_reports_missing_file_as_access_problem()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var exception = await Assert.ThrowsAsync<SyncLocationProviderException>(
            () => provider.OpenReadAsync("missing.txt", CancellationToken.None));

        // Assert
        Assert.Equal(SyncLocationProblemKind.AccessProblem, exception.Problem.Kind);
        Assert.Equal("missing.txt", exception.Problem.RelativePath);
    }

    [Fact]
    public async Task WriteFileAsync_writes_contents_and_last_modified_time()
    {
        // Arrange
        var provider = CreateProvider();
        var lastModifiedUtc = DateTimeOffset.Parse("2026-07-20T10:00:00Z");
        await using var contents = new MemoryStream(Encoding.UTF8.GetBytes("hello"));

        // Act
        await provider.WriteFileAsync("docs/notes.txt", contents, lastModifiedUtc, CancellationToken.None);

        // Assert
        var writtenPath = Path.Combine(_rootPath, "docs", "notes.txt");
        Assert.Equal("hello", await File.ReadAllTextAsync(writtenPath));
        Assert.Equal(lastModifiedUtc, new DateTimeOffset(File.GetLastWriteTimeUtc(writtenPath), TimeSpan.Zero));
    }

    [Fact]
    public async Task DeleteFileAsync_deletes_local_file()
    {
        // Arrange
        var provider = CreateProvider();
        var filePath = Path.Combine(_rootPath, "notes.txt");
        await File.WriteAllTextAsync(filePath, "hello");

        // Act
        await provider.DeleteFileAsync("notes.txt", CancellationToken.None);

        // Assert
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task CreateDirectoryAsync_creates_local_directory()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        await provider.CreateDirectoryAsync("empty", CancellationToken.None);

        // Assert
        Assert.True(Directory.Exists(Path.Combine(_rootPath, "empty")));
    }

    [Fact]
    public async Task DeleteDirectoryAsync_deletes_empty_local_directory()
    {
        // Arrange
        var provider = CreateProvider();
        var directoryPath = Path.Combine(_rootPath, "empty");
        Directory.CreateDirectory(directoryPath);

        // Act
        await provider.DeleteDirectoryAsync("empty", CancellationToken.None);

        // Assert
        Assert.False(Directory.Exists(directoryPath));
    }

    [Fact]
    public async Task ListEntriesAsync_reports_access_problem_for_missing_root_without_throwing()
    {
        // Arrange
        var missingRoot = Path.Combine(_rootPath, "missing");
        var provider = new LocalSyncLocationProvider(new SyncLocation(missingRoot));

        // Act
        var listing = await provider.ListEntriesAsync(CancellationToken.None);

        // Assert
        var problem = Assert.Single(listing.Problems);
        Assert.Equal(SyncLocationProblemKind.AccessProblem, problem.Kind);
    }

    [Fact]
    public async Task ListEntriesAsync_reports_symbolic_links_without_following_them_when_links_can_be_created()
    {
        // Arrange
        var provider = CreateProvider();
        var targetPath = Path.Combine(_rootPath, "target.txt");
        var linkPath = Path.Combine(_rootPath, "link.txt");
        await File.WriteAllTextAsync(targetPath, "hello");

        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return;
        }

        // Act
        var listing = await provider.ListEntriesAsync(CancellationToken.None);

        // Assert
        var symbolicLink = Assert.Single(listing.SymbolicLinks);
        Assert.Equal("link.txt", symbolicLink.RelativePath);
        Assert.Equal(targetPath, symbolicLink.TargetPath);
        Assert.DoesNotContain(listing.Files, file => file.RelativePath == "link.txt");
        Assert.Empty(listing.Problems);
    }

    [Fact]
    public async Task OpenReadAsync_reports_symbolic_link_as_unsupported_when_links_can_be_created()
    {
        // Arrange
        var provider = CreateProvider();
        var targetPath = Path.Combine(_rootPath, "target.txt");
        var linkPath = Path.Combine(_rootPath, "link.txt");
        await File.WriteAllTextAsync(targetPath, "hello");

        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return;
        }

        // Act
        var providerException = await Assert.ThrowsAsync<SyncLocationProviderException>(
            () => provider.OpenReadAsync("link.txt", CancellationToken.None));

        // Assert
        Assert.Equal(SyncLocationProblemKind.UnsupportedSymbolicLink, providerException.Problem.Kind);
        Assert.Equal("link.txt", providerException.Problem.RelativePath);
    }

    [Fact]
    public async Task WriteFileAsync_reports_parent_directory_symbolic_link_as_unsupported_when_links_can_be_created()
    {
        // Arrange
        var provider = CreateProvider();
        var targetDirectoryPath = Path.Combine(_rootPath, "target");
        var linkPath = Path.Combine(_rootPath, "docs");
        Directory.CreateDirectory(targetDirectoryPath);

        try
        {
            Directory.CreateSymbolicLink(linkPath, targetDirectoryPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return;
        }

        await using var contents = new MemoryStream(Encoding.UTF8.GetBytes("hello"));

        // Act
        var providerException = await Assert.ThrowsAsync<SyncLocationProviderException>(
            () => provider.WriteFileAsync(
                "docs/notes.txt",
                contents,
                DateTimeOffset.Parse("2026-07-20T10:00:00Z"),
                CancellationToken.None));

        // Assert
        Assert.Equal(SyncLocationProblemKind.UnsupportedSymbolicLink, providerException.Problem.Kind);
        Assert.Equal("docs", providerException.Problem.RelativePath);
        Assert.False(File.Exists(Path.Combine(targetDirectoryPath, "notes.txt")));
    }

    [Fact]
    public async Task DeleteFileAsync_reports_symbolic_link_as_unsupported_when_links_can_be_created()
    {
        // Arrange
        var provider = CreateProvider();
        var targetPath = Path.Combine(_rootPath, "target.txt");
        var linkPath = Path.Combine(_rootPath, "link.txt");
        await File.WriteAllTextAsync(targetPath, "hello");

        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return;
        }

        // Act
        var providerException = await Assert.ThrowsAsync<SyncLocationProviderException>(
            () => provider.DeleteFileAsync("link.txt", CancellationToken.None));

        // Assert
        Assert.Equal(SyncLocationProblemKind.UnsupportedSymbolicLink, providerException.Problem.Kind);
        Assert.Equal("link.txt", providerException.Problem.RelativePath);
        Assert.True(File.Exists(linkPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private LocalSyncLocationProvider CreateProvider()
    {
        return new LocalSyncLocationProvider(new SyncLocation(_rootPath));
    }
}
