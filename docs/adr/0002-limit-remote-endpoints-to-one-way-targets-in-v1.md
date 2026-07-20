# Limit remote endpoints to one-way targets in version one

The app will support FTP and SSH remote endpoints as one-way sync targets in the first version, while keeping local folders as the initial full participants for two-way sync. The sync core should still use sync-location language so remote endpoints can become interchangeable sources or two-way participants later without renaming the domain model.
