# Blender Style Transform Tool

Minimal Blender-inspired transform tool for the Unity Scene View.

## Hotkeys

| Key | Action |
|-----|--------|
| `G` | Grab / Move selected objects |
| `R` | Rotate selected objects |
| `S` | Scale selected objects |
| `X` / `Y` / `Z` | Constrain to that axis |
| `Shift+X` / `Shift+Y` / `Shift+Z` | Exclude that axis (move on the other two) |
| `LMB` or `Enter` | Confirm transform |
| `RMB` or `Escape` | Cancel transform |

## Notes

- Works with multi-object selection.
- WASD + RMB navigation is not interrupted.
- `Ctrl+S` (save) and other modifier shortcuts are not blocked.
- Grab and Rotate use world-space imaginary plane projection for 1:1 mouse-to-world accuracy.
- Scale sensitivity adapts to camera distance.
- Confirmed transforms are fully undoable with `Ctrl+Z`.
