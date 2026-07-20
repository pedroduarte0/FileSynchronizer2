namespace FileSynchronizer.Core;

public sealed class SyncLocationProviderException : Exception
{
    public SyncLocationProviderException(SyncLocationProblem problem)
        : base(problem.Message)
    {
        Problem = problem;
    }

    public SyncLocationProblem Problem { get; }
}
