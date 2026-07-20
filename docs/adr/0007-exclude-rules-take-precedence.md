# Exclude rules take precedence

When a file matches both include and exclude rules, the exclude rule wins. This makes sync rules conservative by default and avoids copying files the user explicitly chose to leave out.
