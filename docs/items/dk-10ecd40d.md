---
id: dk-10ecd40d
type: task
created: 2026-08-22
status: done
since: 2026-08-22
area: recolor
priority: P1
rank: zt
parent:
fixes: []
blocked_by: []
relates: []
---
# Recolour cache lifecycle

From docs/BACKLOG.md (Done (2026-08-22))
- [x] **Recolour cache lifecycle** (P1). New `src/RecolorTextureCache.cs` (bounded LRU, Destroys
  evicted textures + purge-on-teardown) and `src/RendererOverrideStore.cs` (the four `Original*` maps
  + `Purge()`/`PruneDead()`). `Plugin.OnDestroy` purges both; reapplier prunes dead refs periodically.
  Fixes the slow texture/memory leak. *In-game verify pending (pink-out check on rapid changes).*

Note: duplicates the ticked P1 entry above (kept separate, nothing merged).
