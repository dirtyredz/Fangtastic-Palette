# Changelog

## 1.0.1 — 2026-08-22

Bug-fix release: the Bat Form tab icon now ships **inside the mod**.

- **The bat tab icon is bundled in the plugin.** Previously the release only contained the DLL, so a
  fresh install with no `tab-icon.png` fell back to a generated placeholder glyph — the bat icon only
  showed if you happened to place a PNG yourself. The icon is now embedded in the DLL, so **everyone
  gets the bat on the Bat Form tab out of the box**, no manual file drop needed.
- Dropping your own PNG at `BepInEx/config/FangtasticPalette/tab-icon.png` still overrides the
  built-in icon, exactly as before.
- Internal cleanup only, no gameplay change: removed dead code left over from the shared UI helpers
  and corrected stale log/comment text.

## 1.0.0 — 2026-08-20

First release. Recolour Bat Form — the whole bat, not just a tint.

- **Nine colours, from config or the mirror:** body & wings, ears, fangs, the nose/mouth area, the
  face, the eyes, the pupil, the eye glint, and the dust trail in flight. Set them as hex
  (`#8800FF`) or colour names (`purple`) under **Colors** in Mod Nook / the `.cfg`, or pick them
  visually in the wardrobe.
- **Four intensity sliders** — Body, Ear, Face and Mouth — fade each colour toward the creature's
  own shading instead of a flat fill, plus a Wing Dust Strength slider for the flight trail.
- **Changes apply live** — no re-equip, no restart. Blank leaves a part vanilla; the fangs default
  to white and always sit over the mouth area so they can't be covered.
- **A Bat Form tab in the mirror's wardrobe**, shown only once you own Bat Form. It previews the bat
  and gives one swatch row per part — presets plus a `+` tile that opens an RGB picker — with each
  part's intensity slider directly beneath its colour, and a Freeze Flap toggle to hold the preview
  still while you pick.
- **Picks are a live preview:** they apply immediately as you browse but only stick if you press
  Confirm; leaving the wardrobe without confirming reverts to the colours you started with, the same
  as the game's own try-on clothing.
- **The swatches are the game's own widgets**, cloned — so they carry the real selection ring,
  applied checkmark, hover sound and decorated row titles rather than hand-drawn stand-ins.
- **A bat tab icon**, overridable by dropping a PNG at `config/FangtasticPalette/tab-icon.png`.
- Independent of other mirror mods; if the swatch template can't be found it falls back to drawn
  swatches rather than failing.

Writes nothing new to your save — only its own colour settings.

### How the colour actually works

The whole bat is one hand-painted atlas (`Bat_DIF`) on a single material, so parts are separated
two ways: the navy body and the warm skin are told apart by hue/saturation, and the fangs, mouth
area and face — which share the same tones — are picked out by **UV boxes** over each feature
(the face a feathered ellipse, the ears colour confined to its own box). Each part is a **texture
regeneration**, not an RGB multiply: every pixel takes the target hue and saturation but keeps its
own brightness, so a colour the source lacks still shows.

### Folded in from development

Kept because the reasoning is worth having; none of these were published.

- **The bat body was mapped with a developer probe** (a renderer/material/texture dump and a
  form-grant key), plus live debug bands and draggable UV-box sliders in the mirror to place the
  fang / mouth / face / ear boxes. All of it was stripped for release; it lives in git history.
- **Nose and mouth started as two separate colours** and were combined into one **Mouth** control
  with its own intensity, after they read as one physical area on the bat.
- **The ears colour was leaking across the whole warm skin.** It is now confined to a UV box so it
  stays on the ears rather than tinting the eye-surrounds.
