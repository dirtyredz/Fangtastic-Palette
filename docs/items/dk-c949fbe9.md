---
id: dk-c949fbe9
type: task
created: 2026-08-22
status: done
since: 2026-08-22
area: ui
priority: P1
rank: z
parent:
fixes: []
blocked_by: []
relates: []
---
# Split the `BatFormColorPanel` God-file

From docs/BACKLOG.md (Done (2026-08-22))
- [x] **Split the `BatFormColorPanel` God-file** (P1). 753 → `BatFormColorPanel.cs` (150, orchestrator +
  declarative intensity map, no `switch`) + `SwatchGrid.cs` (353, swatches + selection) + `PanelControls.cs`
  (289, reusable row builders). One-directional deps; callbacks injected. *In-game verify pending.*

Note: duplicates the ticked P1 entry above (kept separate, nothing merged).
