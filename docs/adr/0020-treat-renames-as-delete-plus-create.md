# Treat renames as delete plus create

Version one does not detect renames or moves as distinct operations. A renamed or moved file appears in the sync plan as a deletion at the old relative path and a new file at the new relative path, keeping the model based on relative file path identity.
