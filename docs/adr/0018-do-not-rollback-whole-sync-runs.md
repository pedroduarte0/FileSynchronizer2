# Do not roll back whole sync runs

Version one does not provide full rollback for a sync run. Because sync actions may span local folders, FTPS, and SFTP endpoints, the app records detailed per-file sync results instead of promising transactional behavior it cannot reliably guarantee.
