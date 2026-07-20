# Store sync state for deletion support

The app supports deletions in both one-way sync and two-way sync, while treating both sync modes as first-class. We will keep a small local sync state so the app can distinguish a newly created file on one side from a file deleted on the other side; without remembered state, the same filesystem shape is ambiguous.
