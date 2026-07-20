# Use sync-pair timestamp tolerance

Timestamp tolerance is configured per sync pair. Local-to-local sync defaults to zero seconds, while any sync involving a remote endpoint defaults to two seconds, because remote protocols and filesystems often expose coarser timestamp precision than local filesystems.
