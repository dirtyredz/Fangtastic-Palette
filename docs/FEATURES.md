# FEATURES — Fangtastic Palette

What the mod does. Status: ✅ shipped · 🛠 partial · 📋 planned.

## Recolour (Bat Form)
- ✅ **Body & wings** colour, with **Body Intensity** fade toward the original texture.
- ✅ **Ears** (and eye-surround skin) colour, confined to a UV box, with **Ear Intensity**.
- ✅ **Fangs** colour — defaults to white, always drawn on top of the mouth area so it can't be bled on.
- ✅ **Mouth** (nose+mouth combined) flat fill, with **Mouth Intensity**.
- ✅ **Face** — soft-edged feathered ellipse over the muzzle, with **Face Intensity**.
- ✅ **Eyes** split three ways, each independently recolourable: **Eye**, **Pupil**, **Eye Highlight**
  (glint, confined to its UV patch).
- ✅ **Wing dust** flight trail colour (HSV shift) with **Wing Dust Strength** brightness.
- ✅ Every colour accepts a hex code or HTML colour name; blank = leave vanilla.
- ✅ **Live apply** — Mod Nook or `.cfg` edits reapply instantly (no re-equip/restart).
- ✅ **Per-frame reapply** safety net keeps colours stuck across form/material churn.

## Wardrobe UI (mirror)
- ✅ **Bat Form tab** in the wardrobe bumper menu, **ownership-gated** (hidden until the player owns Bat Form).
- ✅ **Live preview** of the bat rig with the colours applied to a dedicated preview instance.
- ✅ **Swatch rows** per part — cloned game swatches (selection ring, checkmark, hover sound), preset
  colours + a "+" tile.
- ✅ **RGB colour picker** popup (clones the game's `SliderButton`).
- ✅ **Intensity/strength sliders** tucked under each relevant colour row; **Freeze Flap** toggle.
- ✅ **Decorated headers** cloned from the game's category header.
- ✅ **Revert-on-cancel** — picks preview live but only stick on Confirm (colours). *(⚠ intensity/
  strength sliders are **not** currently included in the revert snapshot — see BACKLOG.)*
- ✅ **Custom tab icon** override via `BepInEx/config/FangtasticPalette/tab-icon.png`.
- 🛠 **Fallback tab glyph** — currently a generated **cat paw** (`PawSprite`), not a bat glyph as the
  README claims (see BACKLOG P1).

## Packaging / release
- ✅ Published as **v1.0.0** on Nexus (mod 143). Build/pack via `pack.ps1` → `dist/…zip`.

_Living doc — refresh with /project-docs when it drifts._
