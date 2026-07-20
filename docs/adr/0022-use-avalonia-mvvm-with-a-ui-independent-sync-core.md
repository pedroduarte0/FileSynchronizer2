# Use Avalonia MVVM with a UI-independent sync core

The app uses Avalonia UI with MVVM for the cross-platform desktop interface on Windows and Linux. Synchronization planning, applying, state management, and location access live in UI-independent libraries so unit tests and Reqnroll integration tests can exercise the sync behavior without driving the UI.
