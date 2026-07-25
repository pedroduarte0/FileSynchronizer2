namespace FileSynchronizer.Core;

public enum SyncLocationProblemKind
{
    AccessProblem,
    UnsupportedSymbolicLink,
    UnsupportedPathType,
    HiddenItem,
    EmptyDirectory,
    CaseCollision,
    OverlappingLocation,
    StorageRisk,
}
