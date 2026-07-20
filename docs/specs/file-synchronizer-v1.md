## Problem Statement

Users need a cross-platform desktop application for synchronizing files between locations on Windows and Linux. The app must support both one-way sync and two-way sync as first-class sync modes, give users a clear sync plan before changes are applied by default, handle deletions safely through sync state, and support local folders first while leaving room for encrypted remote targets.

The first version should prove the synchronization core with a local-to-local one-way mirror vertical slice, backed by unit tests and Reqnroll integration tests. The application should be built with C# on .NET 10, use Avalonia for the desktop UI, and keep synchronization behavior outside the UI so it can be tested through application services.

## Solution

Build a .NET 10 file synchronization application with an Avalonia MVVM desktop UI for Windows and Linux. The solution will separate the Avalonia app, sync core, infrastructure providers, unit tests, and Reqnroll integration tests.

Users will define sync pairs containing two sync locations, a sync mode, sync rules, comparison settings, and apply settings. A sync run will build a sync plan that previews copy, overwrite, delete, conflict, access problem, unsupported item, changed-during-sync, and storage-risk outcomes before applying changes. By default, users explicitly apply a previewed plan, while application settings can allow automatic apply for safe actions. Conflicts and storage risks always pause the sync run and any containing sync sequence.

The first implementation slice is local-to-local one-way mirror sync with preview, apply, sync state, sync result, unit tests, and Reqnroll scenarios. Later increments can add two-way sync, sync sequences, conflict resolution, FTPS and SFTP targets, import/export, packaging, and CI coverage.

## User Stories

1. As a desktop user, I want to create a sync pair, so that I can synchronize files between two sync locations.
2. As a desktop user, I want one-way sync to be a first-class sync mode, so that I can mirror a source location to a target location.
3. As a desktop user, I want two-way sync to be a first-class sync mode, so that changes from either side can be applied to the other side.
4. As a desktop user, I want one-way sync to delete target files that no longer exist in the source, so that the target remains a true mirror.
5. As a cautious user, I want every sync run to produce a sync plan by default, so that I can review changes before applying them.
6. As a power user, I want automatic apply to be configurable in application settings, so that safe sync plans can run with less manual confirmation.
7. As a user relying on automatic apply, I want conflicts and storage risks to pause the sync run, so that unsafe actions are not applied without my decision.
8. As a user, I want the sync plan to show copies, overwrites, deletions, conflicts, access problems, unsupported items, changed-during-sync files, and storage risks, so that I understand exactly what will happen.
9. As a user, I want the sync result to show per-file outcomes, so that I can verify what completed, failed, or was skipped.
10. As a user, I want recent sync results retained, so that I can troubleshoot recent sync activity.
11. As a user, I want preview sync plans to remain transient unless applied, so that simply inspecting a plan does not clutter history.
12. As a user, I want sync state to remember files after successful syncs, so that deletions can be distinguished from new files.
13. As a user, I want file identity to use relative path including filename and casing, so that files are matched predictably between sync locations.
14. As a Windows and Linux user, I want case collisions detected before apply, so that files differing only by case are not lost on case-insensitive locations.
15. As a user, I want newer wins to be the default two-way rule, so that the later modified file replaces the older matching file.
16. As a user, I want matching files with the same timestamp but different size to become conflicts, so that ambiguous differences require my choice.
17. As a user, I want conflict resolution to support per-file choices, so that important conflicts can be handled carefully.
18. As a user with many conflicts, I want bulk conflict actions, so that I can resolve common cases efficiently.
19. As a user, I want conflicting files to open externally, so that I can inspect them with my preferred tools.
20. As a user, I want include and exclude sync rules per sync pair, so that I can control which files are eligible for synchronization.
21. As a user, I want exclude rules to take precedence over include rules, so that explicitly excluded files are not synchronized.
22. As a user, I want hidden files and folders synchronized by default, so that the app does not silently omit hidden user content.
23. As a user, I want settings and sync rules to let me exclude hidden files or folders, so that I can avoid hidden content when needed.
24. As a user, I want symbolic links preserved only when both sync locations support them, so that link behavior is not faked unsafely.
25. As a user, I want symbolic links not to be followed in version one, so that sync does not escape the selected sync location.
26. As a user, I want file contents and timestamps synchronized in version one, so that core file data is preserved.
27. As a user, I want ownership and full permission metadata out of scope for version one, so that cross-platform sync behavior stays predictable.
28. As a user, I want timestamp tolerance to be configurable per sync pair, so that local and remote locations can be compared appropriately.
29. As a local-to-local user, I want timestamp tolerance to default to zero seconds, so that local comparisons are exact by default.
30. As a remote-target user, I want timestamp tolerance to default to two seconds, so that remote timestamp precision does not create noisy plans.
31. As an advanced user, I want optional content hashing, so that I can compare file contents when timestamps are not enough.
32. As a user with large files, I want copy, upload, and download operations to stream file contents, so that large files do not have to fit in memory.
33. As a local-to-local user, I want temporary writes enabled by default, so that completed local transfers are less likely to leave corrupt final files.
34. As a remote-target user, I want temporary writes configurable and off by default, so that unsupported remote rename behavior does not break syncs.
35. As a user, I want empty directories preserved for local-to-local sync by default, so that folder structure can be mirrored.
36. As a remote-target user, I want empty directory preservation optional and off by default, so that remote endpoint differences do not surprise me.
37. As a user, I want a sync run to skip files that changed during sync, so that stale planned actions are not applied.
38. As a user, I want cancellation to stop new file actions after the current file action completes or fails, so that cancellation is responsive without intentionally corrupting in-flight work.
39. As a user, I want file actions applied sequentially in version one, so that results and cancellation are easier to understand.
40. As a user, I want no recovery area in version one, so that safety is focused on preview, explicit apply, and clear results rather than hidden backups.
41. As a user, I want no full rollback in version one, so that the app is honest about cross-location file operations.
42. As a user, I want sync sequences to be reusable workflows, so that I can run multiple sync pairs in a defined order.
43. As a user, I want a sync sequence to stop on the first failure, conflict pause, or storage-risk pause, so that later sync runs do not proceed after an earlier problem.
44. As a user, I want manual execution in version one, so that sync runs happen when I explicitly start them.
45. As a user, I want scheduling deferred, so that the first version focuses on reliable planning and applying.
46. As a user, I want real-time file watching deferred, so that the app does not run background behavior before the core workflow is proven.
47. As a user, I want renames represented as delete plus create in version one, so that sync behavior stays simple and visible.
48. As a user, I want sync state and application settings stored in per-user app data, so that synchronized folders are not polluted by app bookkeeping.
49. As a user, I want sync configuration saved as JSON, so that sync pairs, rules, sequences, and settings are inspectable and portable.
50. As a user, I want sync state saved in SQLite, so that remembered file snapshots and results can be queried reliably.
51. As a user, I want to export and import sync configuration without credentials, so that I can move configuration between machines safely.
52. As a user, I want remote credentials stored through the OS credential store where available, so that secrets are not stored directly in settings.
53. As a user, I want the option not to save remote credentials, so that I can enter them only when needed.
54. As a user, I want SFTP support for SSH-based remote file transfer, so that SSH destinations use a clear file-oriented protocol.
55. As a user, I want FTPS support, so that FTP-style remote targets can use encrypted transfer.
56. As a security-conscious user, I want plain FTP excluded by default, so that credentials and file data are not normally sent unencrypted.
57. As a user, I want FTP and SSH remote endpoints limited to one-way targets in version one, so that remote support starts with a realistic scope.
58. As a developer, I want an internal sync location provider abstraction, so that local folders, FTPS endpoints, and SFTP endpoints have a shared boundary.
59. As a developer, I want no public plugin system in version one, so that extension loading and compatibility concerns do not distract from sync behavior.
60. As a user, I want normal sync operations to run as the current user, so that the app does not require administrator or root privileges.
61. As a user, I want access problems reported in sync plans or sync results, so that permission issues are visible and actionable.
62. As a user, I want warnings for system folders and entire drives, so that risky sync locations are clearly called out.
63. As a user, I want system folders and entire drives allowed when I have access, so that legitimate broad sync workflows are possible.
64. As a user, I want overlapping local sync locations blocked, so that recursive copies and unstable delete behavior are prevented.
65. As a user, I want one global per-user configuration in version one, so that organization stays simple.
66. As a user, I want portable mode deferred, so that state, settings, and credentials follow OS conventions.
67. As a Windows user, I want a packaged Windows release, so that I can install and run the app outside a development environment.
68. As a Linux user, I want a packaged Linux release, so that I can run the app outside a development environment.
69. As a user, I want the app to start on a dashboard, so that I can immediately see configured sync pairs, sync sequences, and recent activity.
70. As a first-time user, I want the dashboard empty state to guide me toward creating a sync pair, so that I can get started without a separate wizard.
71. As a user, I want a Sync Pair Editor, so that I can configure locations, mode, rules, comparison settings, and apply settings.
72. As a user, I want a Sequence Editor, so that I can create, name, reorder, and run sync sequences.
73. As a user, I want a Preview screen, so that I can inspect a sync plan before applying it.
74. As a user, I want a Conflict Resolution screen, so that paused conflicts can be resolved.
75. As a user, I want Results History, so that I can inspect retained sync results.
76. As a user, I want a Settings page, so that I can configure preview behavior, hidden-file behavior, retention, and other app-level choices.
77. As a user, I want English UI text in version one, so that the first release has a single supported language.
78. As a future maintainer, I want UI text structured for localization, so that additional languages can be added later.
79. As a user, I want in-app status rather than OS notifications in version one, so that completion and failures are visible while I am using the app.
80. As a privacy-conscious user, I want no telemetry or crash reporting in version one, so that no diagnostic information leaves my machine automatically.
81. As a user troubleshooting a problem, I want local diagnostic logs, so that I can inspect or share useful information manually.
82. As a developer, I want unit tests to follow Arrange-Act-Assert, so that test intent stays readable.
83. As a developer, I want Reqnroll scenarios to describe synchronization behavior below the UI, so that user-visible behavior is tested without brittle UI automation.
84. As a developer, I want fake remote providers in normal CI, so that remote semantics can be tested without real FTPS or SFTP servers.
85. As a developer, I want Windows and Linux CI, so that cross-platform filesystem behavior is tested continuously.
86. As a developer, I want the first vertical slice to be local-to-local one-way mirror sync, so that the plan, apply, state, result, and test workflow is proven before expanding scope.

## Implementation Decisions

- The solution targets .NET 10 only.
- The app uses Avalonia UI with MVVM for the Windows and Linux desktop interface.
- Synchronization behavior lives in UI-independent libraries rather than in the Avalonia UI.
- The solution is organized into separate app, sync core, infrastructure, unit test, and Reqnroll integration test projects.
- The sync core exposes a public application service seam that builds a sync plan, applies a sync plan, updates sync state, and returns a sync result.
- Sync location access is implemented behind internal sync location providers.
- Version one supports local folders as full sync locations.
- Version one supports FTPS and SFTP remote endpoints only as one-way targets.
- Plain FTP is excluded by default.
- SSH-based file transfer means SFTP only in version one.
- Sync pairs define sync locations, sync mode, sync rules, comparison settings, and apply settings.
- One-way sync mirrors the source location to the target location, including deletion of target extras.
- Two-way sync treats both locations as first-class change sources.
- Sync state is required so deletions can be distinguished from files that are merely missing on one side.
- File identity uses relative path including filename and casing.
- Newer wins is the default two-way sync rule.
- A conflict occurs when matching files have the same last modified time but differ in size.
- Case collisions are detected before applying actions to case-insensitive locations.
- Sync plans are generated before applying changes by default.
- Application settings can allow automatic apply for safe planned actions.
- Conflicts and storage risks always pause a sync run and any containing sync sequence.
- Sync sequences are ordered reusable workflows of sync runs.
- Sync sequences stop on the first failure, conflict pause, or storage-risk pause.
- Scheduling is out of scope for version one.
- Real-time file watching is out of scope for version one.
- Renames and moves are represented as delete plus create in version one.
- Include and exclude sync rules are supported per sync pair.
- Exclude rules take precedence over include rules.
- Hidden files and folders are synchronized by default, with settings and rules available to change that behavior.
- Symbolic links are not followed in version one.
- Symbolic links are preserved as links only when both sync locations support them; otherwise they are unsupported items in the sync plan.
- Version one syncs file contents and timestamps, but not ownership or full permission metadata.
- Timestamp tolerance is configurable per sync pair.
- Local-to-local sync defaults timestamp tolerance to zero seconds.
- Any sync involving a remote endpoint defaults timestamp tolerance to two seconds.
- Content hashing is optional and off by default.
- Copy, upload, and download operations stream file contents.
- Temporary writes are configurable per sync pair.
- Local-to-local sync defaults temporary writes on.
- Remote targets default temporary writes off until provider support is proven.
- Empty directories are supported.
- Local-to-local sync preserves empty directories by default.
- Remote targets make empty directory preservation optional and off by default.
- Files changed during sync are skipped, reported, and do not stop unrelated file actions.
- Cancellation stops scheduling new file actions after the current in-flight action finishes or fails.
- File actions are applied sequentially in version one.
- Version one does not keep a recovery area for overwritten or deleted files.
- Version one does not roll back whole sync runs.
- Sync state and application settings are stored in per-user application data outside synchronized folders.
- Sync state uses SQLite.
- Sync configuration uses JSON.
- Recent sync results are retained with configurable retention.
- Preview sync plans are transient unless applied.
- Remote credentials are stored through the OS credential store where available.
- Application settings may reference credentials but must not store FTP or SSH secrets directly.
- Users can choose not to save remote credentials.
- Configuration export/import is supported in version one.
- Configuration exports exclude remote credentials.
- Portable mode is out of scope for version one.
- Normal sync operations run as the current user and do not require administrator or root privileges.
- Access problems are reported in the sync plan or sync result.
- System folders and entire drives are allowed when the current user has access, but the app warns before planning or applying them.
- Overlapping local sync locations are blocked.
- Version one uses one global per-user configuration.
- Version one provides packaged releases for Windows and Linux.
- CI builds and tests on Windows and Linux.
- Normal CI uses fake remote providers for remote semantics rather than real FTPS or SFTP servers.
- Containerized protocol tests may be added later once providers stabilize.
- The main UI areas are Dashboard, Sync Pair Editor, Sequence Editor, Preview, Results History, Settings, and Conflict Resolution.
- The app starts on the Dashboard.
- First-run empty state guides the user to create a sync pair.
- Conflict resolution supports per-file choices and bulk actions.
- Conflicting files can be opened externally.
- An in-app diff viewer is out of scope for version one.
- Version one ships in English only.
- UI text should be structured so localization can be added later.
- OS notifications are out of scope for version one.
- Version one does not define explicit accessibility acceptance requirements beyond default Avalonia and operating system behavior.
- Version one does not include telemetry or crash reporting.
- Version one includes local diagnostic logs.
- The first implementation slice is local-to-local one-way mirror sync with preview, apply, sync state, unit tests, and Reqnroll scenarios.

## Testing Decisions

- Tests should verify external behavior at the highest practical seam rather than internal implementation details.
- The primary test seam is the public sync application service that builds sync plans, applies sync plans, updates sync state, and returns sync results.
- Reqnroll integration tests should exercise synchronization behavior through the public application service rather than Avalonia UI automation.
- Unit tests should use xUnit.net and follow Arrange-Act-Assert in test bodies.
- Unit tests should cover focused core behavior including sync rule precedence, file identity, timestamp tolerance, one-way mirror planning, deletion detection with sync state, storage-risk calculation, changed-during-sync handling, cancellation boundaries, and sync result generation.
- Reqnroll scenarios should cover user-visible sync behavior such as one-way mirror copies new source files, deletes target extras, skips files changed during sync, reports access problems, reports storage risk, retains sync results, and blocks overlapping local sync locations.
- Reqnroll scenarios should later cover two-way newer-wins behavior, conflict pause behavior, sync sequence stop behavior, and remote target behavior through fake remote providers.
- Normal CI should use fake remote providers rather than real FTPS or SFTP servers.
- CI should run on Windows and Linux to catch filesystem differences around casing, timestamps, paths, symlinks, and permissions.
- Avalonia UI tests are not the main verification seam for synchronization behavior in version one.
- Existing prior art is minimal because the repository has not yet been scaffolded; tests should establish the project conventions as the implementation is created.

## Out of Scope

- Background scheduling is out of scope for version one.
- Real-time file watching is out of scope for version one.
- Command-line interface support is out of scope for version one.
- Public plugin system support is out of scope for version one.
- Remote endpoints as two-way sync participants are out of scope for version one.
- Remote endpoints as one-way sources are out of scope for version one.
- Plain FTP is excluded from normal version one support.
- SCP and arbitrary SSH command execution are out of scope.
- Full ownership and permission metadata synchronization is out of scope for version one.
- Full sync-run rollback is out of scope for version one.
- Recovery area or backup copies for overwritten/deleted files are out of scope for version one.
- Rename and move detection as distinct operations is out of scope for version one.
- In-app diff viewer is out of scope for version one.
- Background OS notifications are out of scope for version one.
- Telemetry and crash reporting are out of scope for version one.
- Portable mode is out of scope for version one.
- Named configuration profiles are out of scope for version one.
- Explicit accessibility acceptance requirements beyond default Avalonia and operating system behavior are out of scope for version one.
- Multi-targeting runtimes other than .NET 10 is out of scope.
- Real FTPS/SFTP servers in normal CI are out of scope for the initial test pipeline.

## Further Notes

- The implementation should start with the vertical slice from ADR-0053: local-to-local one-way mirror sync with preview, apply, sync state, unit tests, and Reqnroll scenarios.
- The first slice should still shape the sync core around sync location providers, so remote target support can be added without rewriting the core model.
- The UI should display the actual operational surface immediately rather than a marketing or onboarding page.
- The spec uses the glossary from `CONTEXT.md` and respects the ADRs under `docs/adr/`.
