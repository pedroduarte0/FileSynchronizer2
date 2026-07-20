# Skip files that change during sync

When a file no longer matches the metadata captured in the sync plan at the moment an action would be applied, the app skips that file, reports it as changed during sync, and continues the sync run. This avoids applying stale actions while still allowing unrelated files in the run to complete.
