using System.Globalization;
using System.Text.Json;
using FileSynchronizer.Core;
using Microsoft.Data.Sqlite;

namespace FileSynchronizer.Infrastructure;

public sealed class SqliteSyncStore : ISyncStateStore, ISyncResultStore
{
    private readonly string _connectionString;

    public SqliteSyncStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var directoryPath = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false,
        }.ToString();
    }

    public async Task<SyncPairState?> GetStateAsync(SyncPair syncPair, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        await using var stateCommand = connection.CreateCommand();
        stateCommand.CommandText = """
            SELECT 1
            FROM sync_states
            WHERE source_location = $sourceLocation
                AND target_location = $targetLocation
                AND sync_mode = $syncMode;
            """;
        AddSyncPairParameters(stateCommand, syncPair);
        if (await stateCommand.ExecuteScalarAsync(cancellationToken) is null)
        {
            return null;
        }

        await using var filesCommand = connection.CreateCommand();
        filesCommand.CommandText = """
            SELECT location_side, relative_path, last_modified_utc, size
            FROM sync_state_files
            WHERE source_location = $sourceLocation
                AND target_location = $targetLocation
                AND sync_mode = $syncMode
            ORDER BY location_side, relative_path;
            """;
        AddSyncPairParameters(filesCommand, syncPair);

        var sourceFiles = new List<SyncFile>();
        var targetFiles = new List<SyncFile>();
        await using var reader = await filesCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var locationSide = (SyncLocationSide)reader.GetInt32(0);
            var files = locationSide switch
            {
                SyncLocationSide.Source => sourceFiles,
                SyncLocationSide.Target => targetFiles,
                _ => throw new InvalidOperationException(
                    $"Stored sync location side '{reader.GetInt32(0)}' is invalid."),
            };
            files.Add(new SyncFile(
                reader.GetString(1),
                DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
                reader.GetInt64(3)));
        }

        return new SyncPairState(new SyncState(sourceFiles), new SyncState(targetFiles));
    }

    public async Task SaveStateAsync(
        SyncPair syncPair,
        SyncPairState state,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var stateCommand = connection.CreateCommand())
        {
            stateCommand.Transaction = transaction;
            stateCommand.CommandText = """
                INSERT INTO sync_states (source_location, target_location, sync_mode, updated_utc)
                VALUES ($sourceLocation, $targetLocation, $syncMode, $updatedUtc)
                ON CONFLICT(source_location, target_location, sync_mode)
                DO UPDATE SET updated_utc = excluded.updated_utc;
                """;
            AddSyncPairParameters(stateCommand, syncPair);
            stateCommand.Parameters.AddWithValue("$updatedUtc", DateTimeOffset.UtcNow.ToString("O"));
            await stateCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = """
                DELETE FROM sync_state_files
                WHERE source_location = $sourceLocation
                    AND target_location = $targetLocation
                    AND sync_mode = $syncMode;
                """;
            AddSyncPairParameters(deleteCommand, syncPair);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await SaveFilesAsync(
            connection,
            transaction,
            syncPair,
            SyncLocationSide.Source,
            state.SourceState.Files,
            cancellationToken);
        await SaveFilesAsync(
            connection,
            transaction,
            syncPair,
            SyncLocationSide.Target,
            state.TargetState.Files,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task SaveFilesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SyncPair syncPair,
        SyncLocationSide locationSide,
        IEnumerable<SyncFile> files,
        CancellationToken cancellationToken)
    {
        foreach (var file in files)
        {
            await using var fileCommand = connection.CreateCommand();
            fileCommand.Transaction = transaction;
            fileCommand.CommandText = """
                INSERT INTO sync_state_files (
                    source_location,
                    target_location,
                    sync_mode,
                    location_side,
                    relative_path,
                    last_modified_utc,
                    size)
                VALUES (
                    $sourceLocation,
                    $targetLocation,
                    $syncMode,
                    $locationSide,
                    $relativePath,
                    $lastModifiedUtc,
                    $size);
                """;
            AddSyncPairParameters(fileCommand, syncPair);
            fileCommand.Parameters.AddWithValue("$locationSide", (int)locationSide);
            fileCommand.Parameters.AddWithValue("$relativePath", file.RelativePath);
            fileCommand.Parameters.AddWithValue("$lastModifiedUtc", file.LastModifiedUtc.ToString("O"));
            fileCommand.Parameters.AddWithValue("$size", file.Size);
            await fileCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task SaveResultAsync(
        SyncPair syncPair,
        RetainedSyncResult result,
        SyncResultRetention retention,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO sync_results (
                    source_location,
                    target_location,
                    sync_mode,
                    completed_utc,
                    result_json)
                VALUES ($sourceLocation, $targetLocation, $syncMode, $completedUtc, $resultJson);
                """;
            AddSyncPairParameters(insertCommand, syncPair);
            insertCommand.Parameters.AddWithValue("$completedUtc", result.CompletedAtUtc.ToString("O"));
            var resultDocument = SyncResultDocument.FromDomain(result.Result);
            insertCommand.Parameters.AddWithValue("$resultJson", JsonSerializer.Serialize(resultDocument));
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var retentionCommand = connection.CreateCommand())
        {
            retentionCommand.Transaction = transaction;
            retentionCommand.CommandText = """
                DELETE FROM sync_results
                WHERE source_location = $sourceLocation
                    AND target_location = $targetLocation
                    AND sync_mode = $syncMode
                    AND id NOT IN
                    (
                        SELECT id
                        FROM sync_results
                        WHERE source_location = $sourceLocation
                            AND target_location = $targetLocation
                            AND sync_mode = $syncMode
                        ORDER BY completed_utc DESC, id DESC
                        LIMIT $maximumResults
                    );
                """;
            AddSyncPairParameters(retentionCommand, syncPair);
            retentionCommand.Parameters.AddWithValue("$maximumResults", retention.MaximumResults);
            await retentionCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<RetainedSyncResult>> GetResultsAsync(
        SyncPair syncPair,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT completed_utc, result_json
            FROM sync_results
            WHERE source_location = $sourceLocation
                AND target_location = $targetLocation
                AND sync_mode = $syncMode
            ORDER BY completed_utc DESC, id DESC;
            """;
        AddSyncPairParameters(command, syncPair);

        var results = new List<RetainedSyncResult>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var resultDocument = JsonSerializer.Deserialize<SyncResultDocument>(reader.GetString(1))
                ?? throw new InvalidOperationException("Stored sync result could not be deserialized.");
            results.Add(new RetainedSyncResult(
                DateTimeOffset.Parse(reader.GetString(0), CultureInfo.InvariantCulture),
                resultDocument.ToDomain()));
        }

        return results;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS sync_states
            (
                source_location TEXT NOT NULL,
                target_location TEXT NOT NULL,
                sync_mode INTEGER NOT NULL,
                updated_utc TEXT NOT NULL,
                PRIMARY KEY (source_location, target_location, sync_mode)
            );

            CREATE TABLE IF NOT EXISTS sync_state_files
            (
                source_location TEXT NOT NULL,
                target_location TEXT NOT NULL,
                sync_mode INTEGER NOT NULL,
                location_side INTEGER NOT NULL CHECK (location_side IN (0, 1)),
                relative_path TEXT NOT NULL,
                last_modified_utc TEXT NOT NULL,
                size INTEGER NOT NULL,
                PRIMARY KEY (
                    source_location,
                    target_location,
                    sync_mode,
                    location_side,
                    relative_path),
                FOREIGN KEY (source_location, target_location, sync_mode)
                    REFERENCES sync_states(source_location, target_location, sync_mode)
                    ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS sync_results
            (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                source_location TEXT NOT NULL,
                target_location TEXT NOT NULL,
                sync_mode INTEGER NOT NULL,
                completed_utc TEXT NOT NULL,
                result_json TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_sync_results_pair
            ON sync_results (source_location, target_location, sync_mode, completed_utc DESC, id DESC);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        return connection;
    }

    private static void AddSyncPairParameters(SqliteCommand command, SyncPair syncPair)
    {
        command.Parameters.AddWithValue("$sourceLocation", syncPair.SourceLocation.Value);
        command.Parameters.AddWithValue("$targetLocation", syncPair.TargetLocation.Value);
        command.Parameters.AddWithValue("$syncMode", (int)syncPair.Mode);
    }

    private enum SyncLocationSide
    {
        Source,
        Target,
    }
}
