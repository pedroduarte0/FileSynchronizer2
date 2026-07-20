namespace FileSynchronizer.Core;

public sealed record SyncFile(string RelativePath, DateTimeOffset LastModifiedUtc, long Size);
