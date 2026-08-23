# DECISIONS — Fangtastic Palette

Design/architecture decisions worth not re-litigating. Newest first. Seeded from code + git history
+ the README; where rationale wasn't recoverable it's marked.

## 2026-08-22 — Cap the recolour working resolution (`MaxRecolorDimension = 1024`)
**Why:** The recolour is a per-pixel HSV loop on the **main thread**. In the sibling Purrtastic
Palette this froze the game ~1.7s per build on its 4096² cat-eye atlas (16.7M px), and builds fire on
every transform / wardrobe-open / colour change. Defensive port of that fix: downscale any source
atlas larger than 1024² (via the `Graphics.Blit` already in `ReadPixelsRobust`) before the pixel work,
in both `TextureRecolor.Build` (eyes) and `BuildBatBody`. The recoloured region is small on screen and
all region logic is normalised/proportional, so the downscale is imperceptible. **Profiled here first:**
every bat atlas is 512², so the cap is a **no-op today** (work size == source) — pure insurance against
a future large atlas, and it keeps the Palette mods consistent. Raise to 2048 if a region ever looks
soft. The recolour engine is copied per repo (shared only as docs, `../../16-recolouring-characters.md`),
so this was applied by hand, not shared as code.
**Rejected:** Moving the pixel loop off-thread — Unity texture APIs (`GetPixels`/`SetPixels`/`Apply`)
are main-thread only, so only the arithmetic could move and the marshalling cost isn't worth it at
these sizes. A GPU compute/fragment shader — far more machinery than a shipped small mod needs, and the
cap already removes the freeze. Leaving it uncapped — a large future atlas would reintroduce the freeze
silently.

## 2026-08-22 — Embed the tab icon in the DLL (v1.0.1)
**Why:** `pack.ps1` (a workspace-synced canonical) bundles only the DLL, so the release zip never
contained `tab-icon.png` — a fresh install fell back to a generated placeholder glyph, and the bat
icon only showed if the user manually placed a PNG. Embedding the PNG as an `EmbeddedResource`
(pinned `LogicalName` `FangtasticPalette.tab-icon.png`) ships the icon inside the one file the zip
does contain, so every install gets the bat with no setup. `TabIcon` resolution order is now: user
PNG override → embedded default → generated glyph (last resort). The source art is version-controlled
at `assets/tab-icon.png`.
**Rejected:** Editing the shared `pack.ps1`/pack template to bundle a config folder — a workspace-wide
change affecting every mod, out of scope for a single mod's fix. Loading only from the config path
(the pre-1.0.1 behaviour) — leaves fresh installs without the icon.

## 2026-08-22 — Full structural review recorded, minimal in-place fixes only
**Why:** First baseline structural review (componentization + abstraction + Codex). The mod is
shipped and works; the goal was to *document* debt, not refactor a working release. Only genuinely
cheap+safe fixes were applied (dead-code removal, misleading log/comment sweep); the larger seams
(spec value-types, panel split, palette-config owner, cache lifecycle) are backlogged because they
want in-game verification this session can't do.
**Rejected:** Refactoring the pixel engine / panel now — too risky without launching the game.
Extracting shared machinery into a cross-repo package — each mod is standalone; deferred to a
workspace-level item.

## ~2026-08 — Texture regeneration via HSV colorize, not RGB tint
**Why:** A tint multiply can't introduce a channel the source texture lacks (blue on an orange
texture stays black) and turns dark regions muddy. HSV colorize takes the target's hue+saturation and
keeps the source's own brightness, so any colour is reachable and shading is preserved. Ported from
PurrtasticPalette; the reusable write-up is `../../16-recolouring-characters.md`.
**Rejected:** `target.rgb * luminance` multiply (the earlier cat approach) — the failure mode above.

## ~2026-08 — Read textures via RenderTexture blit + ReadPixels
**Why:** The game's shipped textures don't have Read/Write Enabled, so `GetPixels()` would throw. A
blit into a temporary `RenderTexture` then `ReadPixels` works regardless.

## ~2026-08 — Separate bat parts by colour cluster AND by UV location
**Why:** The whole bat is one hand-painted atlas on one material (unlike the cat's separate
whisker/body materials). Body vs skin split by hue/saturation; fangs/mouth/face can't be told apart by
colour (same tones) so each is a baked UV box, dialed in live in the mirror during development.
**Rejected:** Colour-only separation (works for the cat, not the bat); mesh separation (not reachable
without re-authoring the model — see the reverted `BodyEdgeSoftness`).

## ~2026-08 — Reapply colours every frame (BatColorReapplier)
**Why:** Safety net against the game reinstantiating form materials on no fixed schedule (the cat
needed it). Cheap because textures are cached. Eyes are included because they reverted after leaving
and re-entering Bat Form.
**Rejected:** Apply-once-on-equip — the eyes reverted.

## ~2026-08 — Wardrobe swatches clone the game's own widgets
**Why:** Cloning `CustomizationOptionListWidget` / `SliderButton` / the category header carries the
real selection ring, checkmark, hover sound, fonts and decorated titles — hand-drawn approximations
never matched. A drawn fallback exists for when a template can't be found. Traps written up in
`../../17-wardrobe-ui.md`.

## ~2026-08 — Deterministic panel height instead of ContentSizeFitter auto-layout
**Why:** Feeding the panel into the wardrobe's own `ScrollRect` with an auto-fitted height rubber-banded
(the panel pinned its height to Content instead of driving it). Summing explicit row heights and
top-anchoring like a game row makes the existing ScrollRect scroll it, with no new mask (masks blanked
the panel twice before). This is why row-height arithmetic is hand-rolled — a deliberate tradeoff.

## ~2026-08 — Version single-sourced from csproj `<Version>`
**Why:** One source of truth; never hardcode a version in `Plugin.cs`. Generated into
`ModBuildInfo.Version` by `GenerateModBuildInfo` in `Directory.Build.props`. Workspace convention.

## ~2026-08 — Ownership-gate the wardrobe tab
**Why:** Recolouring a form the player can't turn into makes no sense; the widget has no disabled
state, so the tab is simply not added unless `GameInventory` contains the Bat Form item.

_Living doc — refresh with /project-docs when it drifts._
