# STRUCTURE — Fangtastic Palette

Code-shape map for the mod. For *how the system works* see [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md);
for *why* see [docs/DECISIONS.md](docs/DECISIONS.md). This file answers **where in the code**.

## Overview

A BepInEx 5 / HarmonyX plugin (netstandard2.1, Unity Mono) that recolours the game's **Bat Form**.
Two cooperating layers, cleanly separated (verified: the engine never references the UI):

- **Recolour engine** — reads config colours, regenerates the bat's textures pixel-by-pixel, and
  keeps them applied every frame.
- **Wardrobe UI** — injects a "Bat Form" tab into the mirror's wardrobe with a live preview, swatch
  pickers, sliders, and an RGB colour picker, all cloned from the game's own widgets.

Source is flat in `src/` (no `src/FangtasticPalette/`), 24 files, ~4,500 lines. It is the second entry
in the "…tastic Palette" set after the sibling **PurrtasticPalette** (cat); much of the machinery is a
port, and a third (Fintastic / mermaid) is planned to reuse it again.

## Architecture at a glance

```
Plugin.cs ── binds ConfigEntry<string> colours + float sliders (Mod Nook "Colors" section)
   │  Config.SettingChanged ─► BatColorPatch.ApplyBatColors() + BatFormWardrobe.ApplyFlapFreeze()
   │  AddComponent<BatColorReapplier>()  ·  Harmony.PatchAll(BatColorPatch, BatFormWardrobe)
   ▼
ENGINE                                        UI (wardrobe)
 BatColorPatch      ── Harmony postfix on      BatFormWardrobe ── Harmony patches on
   FormToolView<BatToolAsset> body-load;         WardrobeCustomizationScreen (OnShow/
   routes renderers → colours; owns all UV        HandleTabSelected/OnHide): adds the tab,
   boxes + HSV constants + original-state         swaps preview to a bat body, live-preview
   caches; calls ─►                               revert-on-cancel. Calls ─► BatColorPatch.ApplyToBody
 TextureRecolor     ── the pixel engine:        BatFormColorPanel ── builds the swatch/slider panel
   GetOrBuild (HSV colorize + eye split)          BatFormSwatch  ── clones a game swatch widget
   GetOrBuildBatBody (4-region palette +          ColorPickerPopup ── RGB picker (clones SliderButton)
   SpatialRegion UV compositing); caches         Templates ── game-widget cloning helpers
 BatColorReapplier  ── per-frame reapply         helpers: CircleSprite, PanelSprite, PawSprite,
 Palette            ── two brand colours           TabIcon, HeaderDecoration, GameFonts,
                                                    ScrollForwarder, PreviewBloomSuppressor
```

## Components

| Component | Responsibility | Key files | Depends on |
|---|---|---|---|
| **Plugin** | BepInEx entry; binds every config value (canonical set enumerated by BatPalette); wires live-apply; installs Harmony + reapplier; purges the recolour caches on teardown (`OnDestroy`) | `src/Plugin.cs` | BepInEx, Harmony, RecolorTextureCache, RendererOverrideStore |
| **Palette config owner** | Single source of truth for the colour set: canonical ordered `Colors` list (label + `ConfigEntry` + default swatch) that the panel rows & wardrobe revert both derive from; `Snapshot()` → immutable `BatPaletteValues` for the engine | `src/BatPalette.cs` | BepInEx, Plugin config entries |
| **Colour patch/router** | Harmony postfix on bat body-load; routes each renderer (`Bat_Body`/eyes/`VFXBatWingDust`) to its colour; holds all UV-box + HSV tuning constants; captures/restores original state via RendererOverrideStore; recolours from a `BatPaletteValues` snapshot (no direct config statics) | `src/BatColorPatch.cs` (639) | TextureRecolor, RendererOverrideStore, BatPalette |
| **Pixel engine** | HSV-colorize + eye brightness-split (`GetOrBuild`); 4-region bat-body palette remap + `SpatialRegion` UV compositing (`GetOrBuildBatBody`); `RenderTexture` read path; delegates result caching to RecolorTextureCache | `src/TextureRecolor.cs` (464) | UnityEngine, RecolorSpecs, RecolorTextureCache |
| **Recolour specs** | `EyeRecolorSpec` / `SkinPaletteSpec` value types — the engine's two parameter objects; each owns its cache `Key` (folds in every field incl. `target`), so the engine can't collide two distinct recolours. Also holds the bat-body `SpatialRegion` (its only consumer is `SkinPaletteSpec`) | `src/RecolorSpecs.cs` | UnityEngine |
| **Recolour texture cache** | Bounded LRU (cap 32) of regenerated textures; `Destroy`s evicted textures + purge-on-teardown, so wardrobe fiddling can't leak. LRU never evicts the active palette (reapplier keeps it MRU) | `src/RecolorTextureCache.cs` | UnityEngine |
| **Renderer override store** | The four `Original*` maps (texture/tint/wing-dust/particle-start) captured for restore-on-blank; `Purge()` on teardown + `PruneDead()` drops entries for destroyed Materials/ParticleSystems | `src/RendererOverrideStore.cs` | UnityEngine |
| **Per-frame reapply** | Reapplies body+eyes every `Update()` as a revert safety net (cheap: cache hit); prunes dead override entries every ~600 frames | `src/BatColorReapplier.cs` | BatColorPatch, RendererOverrideStore |
| **Wardrobe tab** | Injects the "Bat Form" tab; swaps the preview rig to a bat body; ownership-gates; live-preview snapshot + revert-on-cancel (colours from BatPalette); hides VFX/bloom | `src/BatFormWardrobe.cs` (486) | BatColorPatch, BatFormColorPanel, BatPalette, TabIcon, PreviewBloomSuppressor |
| **Colour panel (orchestrator)** | Composes the scrollable panel + deterministic height arithmetic; declarative colour→intensity-slider map (no label `switch`) | `src/BatFormColorPanel.cs` (150) | BatPalette, SwatchGrid, PanelControls, ColorPickerPopup |
| **Swatch grid** | One colour's swatch grid: cloned-or-drawn swatches, "+" custom tile + picker, selection state + `RefreshSelection` | `src/SwatchGrid.cs` | PanelControls, BatFormSwatch, ColorPickerPopup, sprites, GameFonts |
| **Panel controls** | Reusable control-row builders (label / toggle / slider) + RectTransform primitives (`Stretch`/`ThinCenteredBar`/`AddTrigger`) | `src/PanelControls.cs` | PanelSprite, CircleSprite, GameFonts, HeaderDecoration, ScrollForwarder |
| **Cloned swatch** | One colour swatch cloned from the game's own widget (ring/checkmark/hover sound) | `src/BatFormSwatch.cs` | Templates (reflection), ScrollForwarder |
| **Colour picker** | Modal RGB picker adapted from ModNook; clones the game `SliderButton` | `src/ColorPickerPopup.cs` | Templates, PanelSprite, GameFonts |
| **Widget cloning** | Sources & clones native game widgets (SliderButton) without waking them | `src/Templates.cs` | Chicken.UI |
| **UI helpers** | Generated sprites (`CircleSprite`, `PanelSprite`, `PawSprite`), `TabIcon` (PNG override + fallback), `HeaderDecoration` (cloned header), `GameFonts`, `ScrollForwarder`, `PreviewBloomSuppressor`, `Palette` | those files | UnityEngine, Chicken.UI |

## Key flows

- **Recolour (live player):** body-load postfix → `ApplyBatColors` → walk renderers → per part
  `ApplyBodyColor`/`ApplyEyeColor`/`ApplyWingDustColor` → `TextureRecolor.GetOrBuild*` (cached) →
  `material.SetTexture` + property block. Re-run every frame by `BatColorReapplier`.
- **Live config change:** Mod Nook / `.cfg` edit raises `Config.SettingChanged` → reapply immediately.
- **Wardrobe:** `OnShow` postfix adds the tab (if owned) → tab select instantiates a preview bat,
  hides other bodies/VFX, builds the panel → swatch pick writes the ConfigEntry (live preview) →
  Confirm keeps it / close reverts to the snapshot.

## Conventions

- Plugin `.cs` flat in `src/`; version single-sourced from `src/FangtasticPalette.csproj` `<Version>`
  via `ModBuildInfo.Version` (generated by `GenerateModBuildInfo` in `Directory.Build.props`).
  Never hardcode a version in `Plugin.cs`.
- `Directory.Build.props` and `pack.ps1` are **workspace-synced canonicals** — do not edit here; they
  are regenerated by `../../tools/sync-mod-files.ps1`.
- Recolour is always **texture regeneration**, never a tint multiply (see ARCHITECTURE / DECISIONS).
- Commit identity `dirtyredz <dirtyredz@live.com>`. Bump `<Version>` only when publishing.

## Where to find things

- **A colour looks wrong / bleeds** → the UV boxes + HSV constants at the top of `BatColorPatch.cs`.
- **The pixel maths** → `TextureRecolor.Build` / `GetOrBuildBatBody`.
- **The wardrobe tab doesn't appear** → `BatFormWardrobe.Postfix` (ownership gate, `bumperMenu.Show()`).
- **Panel layout / scrolling** → `BatFormColorPanel.Build` (deterministic height arithmetic).
- **Reusable cross-mod findings** → `../../16-recolouring-characters.md`, `../../17-wardrobe-ui.md`.

## Structural debt

Recorded by the full review of **2026-08-22** (componentization + abstraction lenses + Codex
cross-model). The mod is broadly well-shaped for its size — engine/UI layering is clean, no
wrong-direction dependencies, small helpers are correctly one-job-per-file. No P0 rot. The items
below are tracked in [docs/BACKLOG.md](docs/BACKLOG.md); nothing here is a blocker.

- **P1 — `TextureRecolor` parameter explosion. ✅ RESOLVED 2026-08-22.** `GetOrBuild` and
  `GetOrBuildBatBody` now take `EyeRecolorSpec` / `SkinPaletteSpec` parameter objects (new
  `src/RecolorSpecs.cs`), each owning its cache identity via a `Key` property. This fixed the fragile
  `hex`-proxy cache key (`target` is now folded into `EyeRecolorSpec.Key`) and removed the redundant
  caller-supplied `cacheDiscriminator`. Behaviour-preserving: the pixel loops are byte-identical
  (specs unpack to the same locals at the top). *In-game recolour verification still pending before
  the next release.*
- **P1 — `BatFormColorPanel.cs` (753 lines) is a God-file. ✅ RESOLVED 2026-08-22.** Split into three:
  `BatFormColorPanel.cs` (150 — orchestrator: composition + height arithmetic + a declarative
  colour→intensity-slider map that replaces the label-string `switch`), `SwatchGrid.cs` (353 — the
  swatch subsystem: cloned-vs-drawn swatches, custom tile + picker, selection state + `RefreshSelection`),
  and `PanelControls.cs` (289 — reusable label/toggle/slider row builders + RectTransform primitives).
  Dependencies are one-directional (SwatchGrid→PanelControls; orchestrator→both), with
  `OnColorChanged`/`RequestRebuild` callbacks injected so the sub-components never reference the
  orchestrator. *(Verified in-game: all rows render, live preview, revert-on-cancel, custom picker.)*
- **P1 — Palette-setting knowledge is duplicated. ✅ RESOLVED 2026-08-22.** Introduced `BatPalette`
  as the single source of truth: a canonical ordered `Colors` list that `BatFormColorPanel` (rows) and
  `BatFormWardrobe.ManagedColors()` (revert) both derive from — closing the `Rows`↔`ManagedColors`
  drift trap — plus `Snapshot()` → immutable `BatPaletteValues` threaded through
  `ApplyToBody(body, in BatPaletteValues)` and the whole apply chain, so the engine no longer reads
  `FangtasticPalettePlugin.*` statics. Config binding stays in `Plugin` (keys/defaults/Mod Nook tags
  unchanged — no `.cfg` compat risk); `BatPalette` references the bound entries. **Residual asymmetry
  (accepted):** only the `Rows`↔`ManagedColors` trap is fully closed (both derive from `Colors`).
  `BatPaletteValues`/`Snapshot()` is a separate named enumeration — the engine needs per-part fields
  (each part has bespoke routing) and the snapshot also carries the 4 intensities + wing strength that
  aren't in `Colors` — so adding a colour still means a `BatPaletteValues` field + engine routing
  (inherent, not incidental duplication). *(Verified in-game: panel rows, live preview, revert-on-cancel.)*
- **P1 — Recolour caches have no lifecycle. ✅ RESOLVED 2026-08-22.** Extracted `RecolorTextureCache`
  (bounded LRU that `Destroy`s evicted textures — caps the per-palette leak — plus purge-on-teardown)
  and `RendererOverrideStore` (the four original-state maps, with `Purge()` on teardown and
  `PruneDead()` for destroyed Materials/ParticleSystems, run periodically by the reapplier). Plugin
  `OnDestroy` purges both. LRU eviction is safe against in-use textures because the per-frame reapplier
  keeps active palettes most-recently-used (with a wardrobe-preview caveat — see docs/GOTCHAS.md).
  *(Correctness fix; verified in-game — 30+ rapid colour changes, no pink-out.)*
- **P2 — Engine vs bat-specific code welded in `TextureRecolor.cs`.** The generic HSV engine (meant
  to be copied verbatim to the next mod) and the bat-only palette-remap/UV-compositing (`GetOrBuildBatBody`)
  still live in one file, so porting means hand-picking lines. Split into `TextureRecolor.cs` (generic) +
  `BatBodyPaletteRemap.cs` (bat-only). Pairs with the mermaid port. *(Partly seamed 2026-08-22: the
  bat-only `SpatialRegion` moved to `RecolorSpecs.cs` next to `SkinPaletteSpec`, so the engine no longer
  back-references it — the eventual split's bat-only spec+region pieces already sit together.)*
- **P2 — `BatColorPatch.cs` (656 lines)** bundles Harmony routing with three self-contained appliers
  (body/eye/wing-dust), each with its own caches + constant block. Extractable along the existing
  comment-delimited seams; Codex judged it "large but not yet a God-file" — lower priority than the
  panel.
- **P2 — Duplicated `capture-original / restore-on-blank` logic** (3 near-identical blocks + 4 dicts
  in `BatColorPatch`) → a small `OriginalValueCache<TKey,TValue>`. Cheap, low-risk when tackled.
- **P2 — Small duplication/dead code:** `AddTrigger` is byte-identical in `BatFormColorPanel` and
  `BatFormSwatch`; two colour-parsers have **drifted** (`BatColorPatch.TryParseColor` retries a
  missing `#`, `BatFormColorPanel.ParseOr` doesn't); unused generality (`TextureRecolor` passHue* is
  never exercised by the bat). Want a shared `ColorHex`/UI-helper.

**Shared-methodology note (do NOT refactor across repos):** `TextureRecolor`'s robust readback +
cache, `Templates`, `GameFonts`, `PanelSprite`, `CircleSprite`, `ScrollForwarder`, the swatch-clone
pattern, and the capture/restore concept are all ports shared with PurrtasticPalette (and the planned
Fintastic). Several findings above recur in the sibling. Each mod is a **standalone git repo**; the
eventual right home is a shared workspace package, tracked as a workspace-level backlog item, not a
cross-repo edit from here.

_Last full review: 2026-08-22_

_Living doc — refresh with /project-docs when it drifts._
