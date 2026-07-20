# Use internal sync location providers

The app uses an internal sync location provider abstraction for local folders, FTPS endpoints, and SFTP endpoints. Version one does not include a public plugin system, avoiding extension loading, versioning, signing, and third-party compatibility concerns while preserving a clean boundary inside the codebase.
