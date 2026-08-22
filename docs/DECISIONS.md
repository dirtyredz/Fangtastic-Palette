# DECISIONS — Fangtastic Palette

Design/architecture decisions worth not re-litigating. Newest first. Seeded from code + git history
+ the README; where rationale wasn't recoverable it's marked.

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
