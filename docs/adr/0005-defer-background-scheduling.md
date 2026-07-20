# Defer background scheduling

The first version supports manual execution of sync runs and saved sync sequences, but does not include a background scheduler. Scheduling is deferred because it brings cross-platform service behavior, startup integration, credential handling, retries, and notifications that would distract from proving the sync planning and apply workflow.
