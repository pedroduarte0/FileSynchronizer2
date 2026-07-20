# Preserve case and detect case collisions

File identity preserves relative path casing, so case-sensitive sync locations can contain distinct files whose names differ only by case. When a sync plan targets a case-insensitive location that cannot represent both paths, the plan marks a case collision instead of applying an unsafe overwrite.
