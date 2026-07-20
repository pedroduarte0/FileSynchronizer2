# Use SQLite for state and JSON for configuration

Sync state is stored in SQLite because remembered file snapshots, sync results, and deletion baselines need reliable querying and updates. Sync configuration is stored as JSON so sync pairs, rules, sequences, and application settings remain inspectable and user-editable.
