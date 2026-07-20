# Use separated app, core, infrastructure, and test projects

The solution is organized into separate projects for the Avalonia app, UI-independent sync core, infrastructure adapters, unit tests, and Reqnroll integration tests. This keeps domain behavior testable without UI dependencies and gives remote endpoint implementations a clear home outside the core model.
