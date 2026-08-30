using FileSynchronizer.Core;

namespace FileSynchronizer.Infrastructure;

internal sealed record SyncResultDocument(
    IReadOnlyCollection<SyncActionOutcomeDocument> Outcomes,
    SyncStateDocument UpdatedState,
    IReadOnlyCollection<SyncLocationProblemDocument> Problems)
{
    public static SyncResultDocument FromDomain(SyncResult result)
    {
        return new SyncResultDocument(
            result.Outcomes.Select(SyncActionOutcomeDocument.FromDomain).ToList(),
            SyncStateDocument.FromDomain(result.UpdatedState),
            result.Problems.Select(SyncLocationProblemDocument.FromDomain).ToList());
    }

    public SyncResult ToDomain()
    {
        return new SyncResult(
            Outcomes.Select(outcome => outcome.ToDomain()).ToList(),
            UpdatedState.ToDomain(),
            Problems.Select(problem => problem.ToDomain()).ToList());
    }
}

internal sealed record SyncActionOutcomeDocument(
    SyncActionKind ActionKind,
    string RelativePath,
    SyncActionStatus Status,
    DeleteFileReason? DeleteReason)
{
    public static SyncActionOutcomeDocument FromDomain(SyncActionOutcome outcome)
    {
        var actionKind = outcome.Action switch
        {
            CopyFileSyncAction => SyncActionKind.CopyFile,
            OverwriteFileSyncAction => SyncActionKind.OverwriteFile,
            DeleteFileSyncAction => SyncActionKind.DeleteFile,
            CreateDirectorySyncAction => SyncActionKind.CreateDirectory,
            DeleteDirectorySyncAction => SyncActionKind.DeleteDirectory,
            _ => throw new InvalidOperationException(
                $"Sync action '{outcome.Action.GetType().Name}' cannot be persisted."),
        };

        var deleteReason = (outcome.Action as DeleteFileSyncAction)?.Reason;
        return new SyncActionOutcomeDocument(
            actionKind,
            outcome.Action.RelativePath,
            outcome.Status,
            deleteReason);
    }

    public SyncActionOutcome ToDomain()
    {
        SyncPlanAction action = ActionKind switch
        {
            SyncActionKind.CopyFile => new CopyFileSyncAction(RelativePath),
            SyncActionKind.OverwriteFile => new OverwriteFileSyncAction(RelativePath),
            SyncActionKind.DeleteFile => new DeleteFileSyncAction(
                RelativePath,
                DeleteReason ?? throw new InvalidOperationException("Stored delete action has no reason.")),
            SyncActionKind.CreateDirectory => new CreateDirectorySyncAction(RelativePath),
            SyncActionKind.DeleteDirectory => new DeleteDirectorySyncAction(RelativePath),
            _ => throw new InvalidOperationException($"Stored sync action kind '{ActionKind}' is invalid."),
        };

        return new SyncActionOutcome(action, Status);
    }
}

internal sealed record SyncStateDocument(
    IReadOnlyCollection<SyncFileDocument> Files,
    IReadOnlyCollection<SyncLocationProblemDocument> Problems)
{
    public static SyncStateDocument FromDomain(SyncState state)
    {
        return new SyncStateDocument(
            state.Files.Select(SyncFileDocument.FromDomain).ToList(),
            state.Problems.Select(SyncLocationProblemDocument.FromDomain).ToList());
    }

    public SyncState ToDomain()
    {
        return new SyncState(
            Files.Select(file => file.ToDomain()).ToList(),
            Problems.Select(problem => problem.ToDomain()).ToList());
    }
}

internal sealed record SyncFileDocument(string RelativePath, DateTimeOffset LastModifiedUtc, long Size)
{
    public static SyncFileDocument FromDomain(SyncFile file)
    {
        return new SyncFileDocument(file.RelativePath, file.LastModifiedUtc, file.Size);
    }

    public SyncFile ToDomain()
    {
        return new SyncFile(RelativePath, LastModifiedUtc, Size);
    }
}

internal sealed record SyncLocationProblemDocument(
    SyncLocationProblemKind Kind,
    string RelativePath,
    string Message)
{
    public static SyncLocationProblemDocument FromDomain(SyncLocationProblem problem)
    {
        return new SyncLocationProblemDocument(problem.Kind, problem.RelativePath, problem.Message);
    }

    public SyncLocationProblem ToDomain()
    {
        return new SyncLocationProblem(Kind, RelativePath, Message);
    }
}

internal enum SyncActionKind
{
    CopyFile,
    OverwriteFile,
    DeleteFile,
    CreateDirectory,
    DeleteDirectory,
}
