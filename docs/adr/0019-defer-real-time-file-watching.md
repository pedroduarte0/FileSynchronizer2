# Defer real-time file watching

Version one only supports explicit manual sync runs and does not watch files in real time. File watching is deferred because cross-platform recursive watching, remote endpoints, missed events, rename handling, and long-running app behavior add complexity beyond the preview-and-apply workflow.
