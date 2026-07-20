# Store state and settings in user app data

Sync state and application settings are stored in the current user's application data location, outside the folders being synchronized. This keeps user content clean, avoids syncing the app's own bookkeeping files, and follows platform conventions such as AppData on Windows and XDG locations on Linux.
