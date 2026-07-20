# Preserve symbolic links without following them

Version one does not follow symbolic links while building sync plans, because following links can pull files from outside the selected sync location or create cycles. Symbolic links are preserved as links only when both sync locations support them; otherwise they appear as unsupported items in the sync plan.
