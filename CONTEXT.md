# File Synchronization

This context describes the language used for comparing and reconciling files between folders.

## Language

**Sync Mode**:
The direction and behavior a user chooses for a synchronization run. Two-way sync and one-way sync are both first-class sync modes.
_Avoid_: Profile type, backup type

**Two-Way Sync**:
A sync mode where changes from either folder may be applied to the other folder.
_Avoid_: Bidirectional backup, mirror

**One-Way Sync**:
A sync mode where changes flow from a source location to a target location. By default, one-way sync mirrors the source, so target files that no longer exist in the source are deleted.
_Avoid_: Backup, export

**Sync Location**:
A local folder or remote endpoint that participates in a sync.
_Avoid_: Folder, path

**Source Location**:
The sync location that provides changes in one-way sync.
_Avoid_: Origin, input folder

**Target Location**:
The sync location that receives changes in one-way sync.
_Avoid_: Destination folder, output folder

**Remote Endpoint**:
A sync location accessed over a remote protocol such as FTP or SSH.
_Avoid_: Server, network folder

**Sync Location Provider**:
An internal implementation that lets the app list, read, write, and delete files for a kind of sync location.
_Avoid_: Plugin, driver

**Fake Remote Provider**:
A test sync location provider that simulates remote endpoint behavior without connecting to a real server.
_Avoid_: Mock server, test FTP

**SFTP Endpoint**:
A remote endpoint accessed through SFTP for file listing, upload, download, deletion, and timestamp handling.
_Avoid_: SSH endpoint, SCP endpoint

**FTPS Endpoint**:
A remote endpoint accessed through FTP over TLS.
_Avoid_: FTP endpoint, plain FTP

**Remote Credential**:
A secret used to access a remote endpoint.
_Avoid_: Password setting, connection string

**Deletion**:
A sync event where a file that existed after the previous successful sync is removed from one or both folders.
_Avoid_: Missing file, cleanup

**Rename**:
A change where a file's relative path changes and is represented in version one as a deletion plus a new file.
_Avoid_: Move

**Conflict**:
A state where matching files have the same last modified time but differ in size, so the app needs an explicit user choice before applying a resolution.
_Avoid_: Collision, merge

**Conflict Resolution**:
A user choice for how to handle one conflict or a set of conflicts before the affected sync run can continue.
_Avoid_: Merge strategy, fix

**Newer Wins**:
The default two-way sync rule where the file with the later last modified time replaces the older matching file.
_Avoid_: Merge, reconcile

**File Identity**:
A file is matched between sync locations by its relative file path, including filename and casing.
_Avoid_: File name only, absolute path

**Case Collision**:
A state where two files have relative paths that differ only by casing and cannot both be represented on a case-insensitive sync location.
_Avoid_: Duplicate file, name conflict

**Sync State**:
The remembered record of files observed after a successful sync, used to distinguish new files from deletions on later runs.
_Avoid_: File index, cache

**Application Settings**:
Per-user choices that configure app behavior across sync pairs and sync sequences.
_Avoid_: Preferences, options

**Sync Configuration**:
The saved user-editable definition of sync pairs, sync rules, sync sequences, and related settings.
_Avoid_: Sync state, profile

**Configuration Export**:
A JSON copy of sync configuration that excludes remote credentials.
_Avoid_: Backup, profile export

**Sync Pair**:
The saved definition of two sync locations, sync mode, sync rules, comparison settings, and apply settings.
_Avoid_: Job, profile

**Results History**:
The retained recent sync results available for review after sync runs complete.
_Avoid_: Logs, audit trail

**Diagnostic Log**:
A local troubleshooting record produced by the app and kept on the user's machine.
_Avoid_: Telemetry, analytics

**Access Problem**:
A condition where the app cannot read, write, delete, or inspect an item with the current user's permissions.
_Avoid_: Permission failure, admin requirement

**Overlapping Location**:
A local sync location that is the same as, contains, or is contained by another local sync location in the same sync pair.
_Avoid_: Nested folder, recursive sync

**Temporary Write**:
A write strategy where file contents are written to a temporary sibling file before replacing the final target file.
_Avoid_: Atomic write, safe write

**Sync Plan**:
The preview of copy, overwrite, delete, conflict, and storage-risk actions the app intends to perform before applying a sync.
_Avoid_: Dry run, changeset

**Sync Result**:
The per-file outcome record produced after applying a sync plan.
_Avoid_: Log, report

**Sync Sequence**:
An ordered, reusable workflow of sync runs where each run starts after the previous run has completed.
_Avoid_: Batch, queue

**Storage Risk**:
A condition where a sync plan may exceed the available storage on a target location.
_Avoid_: Disk warning, capacity issue

**Changed During Sync**:
A condition where a file no longer matches the metadata captured in the sync plan when the app is about to apply an action for that file.
_Avoid_: Race condition, stale file

**Cancellation**:
A user request to stop a sync run after the current in-flight file action finishes or fails.
_Avoid_: Abort, kill

**Sync Rule**:
A per-sync-pair include or exclude rule that decides whether a file is eligible for synchronization.
_Avoid_: Filter, ignore

**Hidden File**:
A file or folder marked hidden by the operating system or named with a leading dot.
_Avoid_: System file, ignored file

**Symbolic Link**:
A filesystem entry that points to another file or folder and may not be representable on every sync location.
_Avoid_: Shortcut, alias

**File Metadata**:
Information about a file other than its contents, such as timestamps, permissions, ownership, and attributes.
_Avoid_: Properties, details

**Empty Directory**:
A directory with no eligible files or subdirectories after sync rules are applied.
_Avoid_: Folder placeholder

**Timestamp Tolerance**:
The allowed difference between last modified times when deciding whether matching files should be treated as having the same timestamp.
_Avoid_: Date slack, clock drift

**Content Hash**:
A digest of file contents used as an optional comparison signal when timestamp-based comparison is not enough.
_Avoid_: Checksum, fingerprint
