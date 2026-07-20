# Limit file metadata sync in version one

Version one syncs file contents and timestamps, but does not attempt to preserve ownership or full permission metadata across Windows, Linux, and remote endpoints. Cross-platform permission semantics vary enough that promising exact metadata synchronization would make the first version less predictable.
