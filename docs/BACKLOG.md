# BACKLOG — Fangtastic Palette

Prioritized trough of deferred work. Most items came from the **2026-08-22 full structural review**
(see [../STRUCTURE.md](../STRUCTURE.md) → Structural debt). `[x]` = done this session.

## P0 — blockers
_None._ The review found no P0 structural rot.

## P1 — worth doing
- [ ] **`TextureRecolor` parameter-object refactor.** Replace the ~17/~22-param `GetOrBuild` /
  `GetOrBuildBatBody` with `EyeRecolorSpec` / `SkinPaletteSpec` value types that own their cache
  identity. Fixes the fragile `hex`-proxy cache key (`target` isn't in the key) and the redundant
  `cacheDiscriminator`. *Wants in-game recolour verification.*
- [ ] **Split `BatFormColorPanel.cs` (753 lines).** Extract a declarative `ColorControlSpec` (kills the
  label-string `switch`), a `SwatchGrid`/`SwatchView` (cloned-vs-drawn fallback + refresh), and reusable
  slider/toggle row builders. *Wants in-game layout verification.*
- [ ] **Single palette-config owner + immutable snapshot.** One source of truth for the colour set
  (dedupe `Plugin` binds / `Rows` / `ManagedColors` / direct static reads); pass a `BatPaletteValues`
  snapshot into `ApplyToBody` so the engine doesn't depend on BepInEx statics. Also closes the
  `Rows`↔`ManagedColors` drift trap.
- [ ] **Give recolour caches a lifecycle.** `TextureRecolor.Cache` + the four `Original*` dictionaries
  grow unbounded with no eviction/`Destroy`. Extract `RecolorTextureCache` + `RendererOverrideStore`
  with purge-on-teardown. *(Correctness: a slow texture/memory leak.)*

## P2 — nice to have
- [ ] **Split `TextureRecolor.cs`** into generic engine + `BatBodyPaletteRemap.cs` (bat-only), so the
  next "…tastic Palette" port copies a clean file. Pairs with the mermaid port.
- [ ] **Extract the three appliers** in `BatColorPatch.cs` (body/eye/wing-dust) along the existing
  comment seams. Lower priority — cohesive today.
- [ ] **`OriginalValueCache<TKey,TValue>`** for the 3 duplicated capture/restore blocks + 4 dicts.
- [ ] **Unify colour parsing.** `BatColorPatch.TryParseColor` retries a missing `#`; `BatFormColorPanel.ParseOr`
  doesn't — they've drifted. Add a shared `ColorHex` helper.
- [ ] **Dedupe `AddTrigger`** (byte-identical in `BatFormColorPanel` + `BatFormSwatch`).
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
- [x] Install the pre-push structure-review gate + bootstrap the living-doc set.
- [x] Remove dead `Templates.CloneButton` + its exclusive `SetLabel` helper (port residue).
- [x] Fix misleading user-visible cat log strings + the most misleading stale comments
  (`BatColorPatch` "No wardrobe tab yet" / "Still to do: FANGS", `BatFormWardrobe` cat logs).
- [x] Version-control the shipped bat tab icon: added `assets/tab-icon.png` to the repo (previously
  only lived in the game's config folder). Struck a bogus "cat-paw fallback" finding that treated a
  working shipped feature as a defect — out of scope for a code-structure review.

_Living doc — refresh with /project-docs when it drifts._
