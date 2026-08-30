namespace FileSynchronizer.Core;

public sealed record RetainedSyncResult(DateTimeOffset CompletedAtUtc, SyncResult Result);
