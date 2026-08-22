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

Source is flat in `src/` (no `src/FangtasticPalette/`), 18 files, ~4,300 lines. It is the second entry
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
| **Plugin** | BepInEx entry; binds every config value; wires live-apply; installs Harmony + reapplier | `src/Plugin.cs` | BepInEx, Harmony |
| **Colour patch/router** | Harmony postfix on bat body-load; routes each renderer (`Bat_Body`/eyes/`VFXBatWingDust`) to its colour; holds all UV-box + HSV tuning constants; captures/restores original textures | `src/BatColorPatch.cs` (656) | TextureRecolor, Plugin statics |
| **Pixel engine** | HSV-colorize + eye brightness-split (`GetOrBuild`); 4-region bat-body palette remap + `SpatialRegion` UV compositing (`GetOrBuildBatBody`); `RenderTexture` read path; result cache | `src/TextureRecolor.cs` (544) | UnityEngine only |
| **Per-frame reapply** | Reapplies body+eyes every `Update()` as a revert safety net (cheap: cache hit) | `src/BatColorReapplier.cs` | BatColorPatch |
| **Wardrobe tab** | Injects the "Bat Form" tab; swaps the preview rig to a bat body; ownership-gates; live-preview snapshot + revert-on-cancel; hides VFX/bloom | `src/BatFormWardrobe.cs` (497) | BatColorPatch, BatFormColorPanel, TabIcon, PreviewBloomSuppressor |
| **Colour panel** | Builds the scrollable swatch/slider/toggle panel; selection state; opens the picker | `src/BatFormColorPanel.cs` (753) | BatFormSwatch, ColorPickerPopup, sprites, GameFonts, HeaderDecoration |
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

- **P1 — `TextureRecolor` parameter explosion.** `GetOrBuild` (~17 params) and `GetOrBuildBatBody`
  (~22 params) leak every classifier/mask/floor into callers; the doc comment itself apologises for
  the arity. Wants parameter-object value types (`EyeRecolorSpec`, `SkinPaletteSpec`) that own their
  own cache identity — which also fixes the fragile `hex`-proxy cache key (`target` is not in the key;
  safe today only because the eye path forces the desaturated branch) and the redundant
  caller-supplied `cacheDiscriminator`. *(Deferred: touches the pixel engine; wants in-game recolour
  verification.)*
- **P1 — `BatFormColorPanel.cs` (753 lines) is a God-file.** Screen composition + layout arithmetic +
  three widget builders (swatch/slider/toggle) + two swatch implementations + picker wiring +
  selection state. Extract: declarative `ColorControlSpec` (kills the label-string `switch`), a
  `SwatchGrid`/`SwatchView` owning cloned-vs-drawn fallback + refresh, and reusable slider/toggle row
  builders. Consistent with the mod's own pattern (ColorPickerPopup/HeaderDecoration are one-widget
  files). *(Deferred: UI refactor wants in-game layout verification.)*
- **P1 — Palette-setting knowledge is duplicated** across `Plugin` binds, `BatFormColorPanel.Rows`,
  `BatFormWardrobe.ManagedColors()`, and direct `FangtasticPalettePlugin.*Color.Value` reads in
  `BatColorPatch`. Adding a colour means coordinated edits in several files, and `Rows`↔`ManagedColors`
  drift would silently drop a setting from revert-on-cancel. Wants one palette-config owner exposing
  an immutable snapshot consumed by the engine (`ApplyToBody(body, BatPaletteValues)`), removing the
  plugin-as-service-locator smell. *(Deferred: architecture change.)*
- **P1 — Recolour caches have no lifecycle.** `TextureRecolor.Cache` and the four original-state
  dictionaries in `BatColorPatch` grow unbounded (new `Texture2D` per distinct palette; new dict keys
  per body-swap material) with no eviction or `Destroy`. A slow leak. Wants a plugin-owned
  `RecolorTextureCache` + `RendererOverrideStore` with explicit purge on teardown. *(Deferred: has a
  correctness angle; see BACKLOG.)*
- **P2 — Engine vs bat-specific code welded in `TextureRecolor.cs`.** The generic HSV engine (meant
  to be copied verbatim to the next mod) and the bat-only palette-remap/UV-compositing live in one
  file, so porting means hand-picking lines. Split into `TextureRecolor.cs` (generic) +
  `BatBodyPaletteRemap.cs` (bat-only). Pairs with the mermaid port.
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
