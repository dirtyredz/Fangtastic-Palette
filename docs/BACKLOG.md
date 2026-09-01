# BACKLOG — Fangtastic Palette

Prioritized trough of deferred work. Most items came from the **2026-08-22 full structural review**
(see [../STRUCTURE.md](../STRUCTURE.md) → Structural debt). `[x]` = done this session.

## P0 — blockers
_None._ The review found no P0 structural rot.

## P1 — worth doing
- [x] **`TextureRecolor` parameter-object refactor.** ✅ 2026-08-22 — `GetOrBuild` /
  `GetOrBuildBatBody` now take `EyeRecolorSpec` / `SkinPaletteSpec` value types (new
  `src/RecolorSpecs.cs`) that own their cache identity via a `Key` property. Fixed the fragile
  `hex`-proxy cache key (`target` is now folded into the key) and removed the redundant caller-built
  `cacheDiscriminator`. Behaviour-preserving by construction (pixel loops byte-identical; cache keys
  equivalent-or-more-precise). *Still wants in-game recolour verification before the next release.*
- [x] **Split `BatFormColorPanel.cs` (753 lines).** ✅ 2026-08-22 — split into `BatFormColorPanel.cs`
  (150, orchestrator + declarative colour→intensity map replacing the `switch`), `SwatchGrid.cs` (353,
  swatch subsystem + selection), and `PanelControls.cs` (289, reusable row builders + primitives).
  One-directional deps, callbacks injected. *Verified in-game (rows, live preview, revert, picker).*
- [x] **Single palette-config owner + immutable snapshot.** ✅ 2026-08-22 — new `src/BatPalette.cs`:
  canonical `Colors` list (panel rows + wardrobe `ManagedColors` both derive from it → drift trap
  closed) + `Snapshot()` → `BatPaletteValues` threaded through `ApplyToBody(body, in BatPaletteValues)`
  and the apply chain, so the engine no longer reads `FangtasticPalettePlugin.*` statics. Config
  binding stays in `Plugin` (no `.cfg` compat risk). *(Wants in-game verify.)*
- [x] **Give recolour caches a lifecycle.** ✅ 2026-08-22 — extracted `RecolorTextureCache` (bounded
  LRU, cap 32, `Destroy`s evicted textures + purge-on-teardown) and `RendererOverrideStore` (the four
  `Original*` maps; `Purge()` on teardown + `PruneDead()` for destroyed Materials/ParticleSystems).
  `Plugin.OnDestroy` purges both; the reapplier prunes every ~600 frames. *(Correctness: fixes the slow
  texture/memory leak. Wants in-game verify — rapid colour changes must not pink out.)*

## P2 — nice to have
- [ ] **Split `TextureRecolor.cs`** into generic engine + `BatBodyPaletteRemap.cs` (bat-only), so the
  next "…tastic Palette" port copies a clean file. Pairs with the mermaid port.
- [ ] **Extract the three appliers** in `BatColorPatch.cs` (body/eye/wing-dust) along the existing
  comment seams. Lower priority — cohesive today.
- [ ] **`OriginalValueCache<TKey,TValue>`** for the 3 duplicated capture/restore blocks + 4 dicts.
- [ ] **Unify colour parsing.** `BatColorPatch.TryParseColor` retries a missing `#`; `SwatchGrid.ParseOr`
  doesn't — they've drifted. Add a shared `ColorHex` helper.
- [ ] **Dedupe `AddTrigger`** (byte-identical in `PanelControls` + `BatFormSwatch`).
- [ ] **Trim dead generality:** `TextureRecolor` passHue* params are never exercised by the bat.
- [ ] **Intensity/strength sliders don't revert on wardrobe-cancel.** Include the float configs in the
  snapshot/revert. *(Correctness/UX; confirmed by Codex.)*
- [ ] **Finish the cat→bat terminology sweep.** The blatant runtime log strings + misleading comments
  were fixed 2026-08-22; a few descriptive "cat" mentions may remain. Keep legitimate sibling-methodology
  comparisons.

## Workspace-level (NOT a cross-repo edit from here)
- [ ] **Shared "…tastic Palette" package.** `TextureRecolor` readback/cache, `Templates`, `GameFonts`,
  `PanelSprite`, `CircleSprite`, `ScrollForwarder`, the swatch-clone pattern, and the capture/restore
  concept are ports shared with PurrtasticPalette and the planned Fintastic. Several review findings
  recur across siblings. The right long-term home is a shared workspace package — a workspace decision,
  since each mod is a standalone repo.

## Done (2026-08-22)
- [x] **Cap recolour resolution** (defensive port from Purrtastic). `MaxRecolorDimension = 1024` in
  `TextureRecolor` downscales oversized atlases before the main-thread HSV loop (both `Build` +
  `BuildBatBody`). Profiled in-game: every bat atlas is 512², so it's a no-op today — insurance against
  a future large atlas + Palette-mod consistency. See DECISIONS.md / GOTCHAS.md.
- [x] **Split the `BatFormColorPanel` God-file** (P1). 753 → `BatFormColorPanel.cs` (150, orchestrator +
  declarative intensity map, no `switch`) + `SwatchGrid.cs` (353, swatches + selection) + `PanelControls.cs`
  (289, reusable row builders). One-directional deps; callbacks injected. *In-game verify pending.*
- [x] **Single palette-config owner** (P1). New `src/BatPalette.cs` — canonical `Colors` list (panel
  rows + wardrobe revert derive from it) + `Snapshot()`/`BatPaletteValues` threaded through the engine
  so it no longer reads config statics. Config binding stays in `Plugin`. *In-game verify pending.*
- [x] **Recolour cache lifecycle** (P1). New `src/RecolorTextureCache.cs` (bounded LRU, Destroys
  evicted textures + purge-on-teardown) and `src/RendererOverrideStore.cs` (the four `Original*` maps
  + `Purge()`/`PruneDead()`). `Plugin.OnDestroy` purges both; reapplier prunes dead refs periodically.
  Fixes the slow texture/memory leak. *In-game verify pending (pink-out check on rapid changes).*
- [x] **`TextureRecolor` parameter-object refactor** (P1). `EyeRecolorSpec`/`SkinPaletteSpec` in new
  `src/RecolorSpecs.cs`; specs own their cache `Key`. Killed the `hex`-proxy cache key and the
  redundant `cacheDiscriminator`. Engine 544→460 lines; builds clean. Verified in-game (Body/Eyes
  remap ran, no exceptions). **Structure-review follow-ups (same day):** moved bat-only `SpatialRegion`
  into `RecolorSpecs.cs` to break a spec↔engine dependency cycle (componentization lens); added a
  `KeyFormat` helper so all cache keys serialise colours losslessly and floats culture-invariantly
  (Codex sign-off — was latent/pre-existing, hardened now that key-building is centralised).
- [x] Install the pre-push structure-review gate + bootstrap the living-doc set.
- [x] Remove dead `Templates.CloneButton` + its exclusive `SetLabel` helper (port residue).
- [x] Fix misleading user-visible cat log strings + the most misleading stale comments
  (`BatColorPatch` "No wardrobe tab yet" / "Still to do: FANGS", `BatFormWardrobe` cat logs).
- [x] Version-control the shipped bat tab icon: added `assets/tab-icon.png` to the repo (previously
  only lived in the game's config folder). Struck a bogus "cat-paw fallback" finding that treated a
  working shipped feature as a defect — out of scope for a code-structure review.

_Living doc — refresh with /project-docs when it drifts._

## Placement follow-up (from the 2026-09-01 structure review)

- **P2 — `src/ui/` sits at exactly 12 files, the flat-bucket cap.** Not a violation, but the next UI
  file added forces a split, so it is worth doing deliberately rather than under pressure. The seam is
  already latent in this doc's own file-by-file grouping: panel/swatch *composition* versus *drawing
  primitives* (sprites, icons, palette, scroll/pointer helpers). Roughly 6 files each.
