# Cancel between file actions

Cancellation stops scheduling new file actions for a sync run, while allowing the current in-flight file transfer or delete action to finish or fail naturally. This keeps cancellation responsive without intentionally creating partially applied file operations.
