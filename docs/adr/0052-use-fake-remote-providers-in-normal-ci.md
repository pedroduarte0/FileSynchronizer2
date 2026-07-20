# Use fake remote providers in normal CI

Normal CI uses fake remote providers for Reqnroll integration tests instead of real FTPS or SFTP servers. Containerized protocol tests can be added later once the provider layer stabilizes, but the default pipeline should stay deterministic and inexpensive.
