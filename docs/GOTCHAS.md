# GOTCHAS — Fangtastic Palette

Non-obvious traps. Format: **trap → why → do instead**.

- **Don't tint with an RGB multiply → dark/again-coloured regions go muddy and unreachable colours stay
  black → use the HSV colorize in `TextureRecolor` (regenerate the texture).**
- **Don't call `Texture2D.GetPixels()` on game textures → they aren't Read/Write-enabled and it throws
  → read via `TextureRecolor.ReadPixelsRobust` (RenderTexture blit + ReadPixels).**
- **The two albedo slots must both be written → the "Bat" material carries `_BaseMap`/`_BaseColor` AND
  `_MainTex`/`_Color`; writing one leaves the other showing the original → always loop `BodyTexturePairs`.**
- **Don't add a colour to `BatFormColorPanel.Rows` without also adding it to
  `BatFormWardrobe.ManagedColors()` → they're two hand-synced lists; a missed entry silently drops the
  colour from revert-on-cancel → (better: give them a single source of truth — BACKLOG).**
- **Intensity/strength sliders don't currently revert on wardrobe-cancel → the snapshot only captures
  the 9 colour strings → if you touch revert logic, include the float configs too (BACKLOG).**
- **Don't rely on the wardrobe's auto-layout to size the panel → it rubber-bands the ScrollRect → sum
  explicit row heights and top-anchor the panel like a game row (see `BatFormColorPanel.Build`).**
- **Don't cache a game template by a one-time bool → Unity `== null` is true for a destroyed object, so
  a second mirror visit hands back a dead reference and the panel silently drops to the drawn fallback →
  re-search when the cached reference is null (as `BatFormSwatch`/`HeaderDecoration` do).**
- **A throwing handler on a Chicken `Signal`/multicast kills every listener after it → wrap cloned-widget
  callbacks in try/catch (see `Templates.CloneButton`'s guard, and `ColorPickerPopup.Step`).**
- **`SliderButton.Setup` NREs on a null `MaxValueTextOverride` once the slider can reach its max → pass a
  non-null empty `SinglelineLocalizedText()` (see `ColorPickerPopup.AddChannel`).**
- **Don't mutate the scene's shared `VolumeProfile` to kill preview bloom → it disables bloom game-wide
  until restart → add a throwaway high-priority global `Volume` and destroy it on close
  (`PreviewBloomSuppressor`).**
- **The preview bat is a separate instance from the live player → colours applied to the player don't
  carry over → call `BatColorPatch.ApplyToBody(previewInstance)` explicitly.**
- **The sibling Cat Form tab shares the preview parent and gets no teardown when our tab is picked →
  switching Cat→Bat can leave the cat on screen → deactivate every `BodyViewAsset` under the parent,
  then show only ours.**
- **`Directory.Build.props` and `pack.ps1` are workspace-synced canonicals → edits here get overwritten
  by `../../tools/sync-mod-files.ps1` → change them at the workspace source, not in the mod.**
- **Stale "cat"/"Cat Form" strings were ported from PurrtasticPalette → some remain (see STRUCTURE debt).
  Keep legitimate comparisons to the sibling methodology; only the ones describing *this* object as a cat
  are wrong.**

_Living doc — refresh with /project-docs when it drifts._
