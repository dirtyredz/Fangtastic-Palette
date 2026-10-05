---
id: dk-c5e5fec8
type: task
created: 2026-08-22
status: done
since: 2026-08-22
area: palette
priority: P1
rank: zn
parent:
fixes: []
blocked_by: []
relates: []
---
# Single palette-config owner

From docs/BACKLOG.md (Done (2026-08-22))
- [x] **Single palette-config owner** (P1). New `src/BatPalette.cs` — canonical `Colors` list (panel
  rows + wardrobe revert derive from it) + `Snapshot()`/`BatPaletteValues` threaded through the engine
  so it no longer reads config statics. Config binding stays in `Plugin`. *In-game verify pending.*

Note: duplicates the ticked P1 entry above (kept separate, nothing merged).
