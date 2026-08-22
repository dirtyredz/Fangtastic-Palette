# Fangtastic Palette

Recolours Bat Form: body and wings, ears, fangs, the nose/mouth area, the face, the eyes — iris,
pupil and glint each on their own — and the dust trail the bat leaves in flight. Two ways to set
the colours: config values, and a **Bat Form tab in the mirror's wardrobe** with a live preview
and swatch pickers.

**Status:** 🚀 **Published** — v1.0.0 live on Nexus as
[mod 143](https://www.nexusmods.com/moonlightpeaks/mods/143).

Config settings live under **Colors** in Mod Nook (or
`BepInEx/config/com.dirtyredz.moonlightpeaks.fangtasticpalette.cfg`). Every colour is a hex code
(`#8800FF`) or an HTML colour name (`purple`); blank means "leave vanilla". Changes apply
immediately — no re-equip, no restart.

| Setting | Colours | Vanilla |
|---|---|---|
| `BodyColor` | the body and wings | dark navy |
| `EarsColor` | the ears and the skin around the eyes | tan |
| `FangColor` | the fangs | white |
| `MouthColor` | the whole mouth area (nose + mouth, combined) | rose |
| `FaceColor` | the face patch over the muzzle/cheeks | tan, or the default orange band |
| `EyeColor` | the eyes | lavender |
| `PupilColor` | the dark centre of the eye | near-black |
| `EyeHighlightColor` | the small bright glint | white |
| `WingDustColor` | the dust trail while flying | pale |

### Intensity sliders

Four colours can be softened rather than painted flat, so the creature's own shading shows through:

| Slider | Fades | Default |
|---|---|---|
| `BodyIntensity` | the body colour toward the original texture | 0.70 |
| `EarIntensity` | the ears colour toward the original skin shading | 0.95 |
| `FaceIntensity` | the face colour toward the original | 0.80 |
| `MouthIntensity` | the mouth colour toward the original | 0.40 |
| `WingDustStrength` | *(brightness, not a fade)* how strongly the wing dust glows | 4.0 |

`FangColor` defaults to white and always sits **on top** of the mouth area, so the fangs read as
fangs even when a Mouth colour would otherwise cover them. The `FaceColor` ships as a soft orange
oval over the muzzle; set it to the Default swatch to leave the face vanilla.

## The wardrobe tab

Interact with the mirror the game places in the player's house, choose "Change clothes", and a
**Bat Form** tab appears at the end of the tab strip. Selecting it shows the bat in the preview
(the same rig the character creator uses) with a swatch panel — one row per colourable part,
preset colours plus a "+" tile that opens an RGB picker, and each part's intensity slider tucked
directly under its colour. Picking a swatch writes straight to the config entry, so the wardrobe,
the `.cfg`, and Mod Nook can never disagree. The swatches and row headers are clones of the game's
own customization widgets, so they carry the real selection ring, checkmark, hover sound and
decorated titles rather than hand-drawn approximations.

A swatch pick is a live **preview**: it applies immediately but only sticks if you press Confirm —
closing the wardrobe without confirming reverts to the colours from before you opened it, the same
as the game's own try-on clothing.

The panel feeds itself into the wardrobe screen's own `ScrollRect` (mouse wheel and drag) rather
than building a clip mask. There is a **Freeze Flap** toggle at the top of the panel to hold the
preview bat still while you judge colours; it affects only the preview, not the game.

The **Bat Form** tab shows a bat icon, bundled inside the plugin so it appears on a fresh install
with no setup. Drop a PNG at `BepInEx/config/FangtasticPalette/tab-icon.png` to override it
(transparent, square, ~256px, art kept within a centred circle since the tab is a diamond).

## Status

Working and confirmed in-game: every colour, the four intensity sliders, and the wardrobe tab
(preview, swatches, picker, decorated headers, live-preview with revert-on-cancel, bat tab icon,
ownership-gated, stable across leaving and re-entering the mirror). The **1.0.0** release: dev
tools stripped, packaging in `pack.ps1` / `RELEASING.md`. The bat entry in a themed set after
[Purrtastic Palette](../PurrtasticPalette) (cat) and before the planned Fintastic Palette (mermaid).

## How it works

Bat Form is not a recoloured version of the player — equipping it swaps the entire body for a
different prefab via `EntityCustomization.SetBodyView`. Colours are applied by a Harmony postfix
on the form-tool's body-load, the first moment the new body is fully loaded and active, and then
re-applied every frame from `BatColorReapplier`.

Everything is a **texture regeneration**, not a tint. Each source texture is read, recoloured
pixel by pixel, and the result assigned back:

- **HSV colorize** — each pixel takes the target colour's hue and saturation but keeps its own
  brightness. An RGB multiply cannot introduce a channel the source lacks (blue onto a red
  texture stays black) and turns dark regions to mud.
- **Reading is done through a `RenderTexture` blit + `ReadPixels`**, so it works on textures
  without "Read/Write Enabled" — which is all of the game's shipped textures.
- **Results are cached** by (source texture, every colour, every threshold and mask box), so the
  per-frame reapply is a dictionary lookup rather than a full rebuild.

### The material targets

| Part | Renderer | Texture property |
|---|---|---|
| Body / ears / fangs / mouth / face | `Bat_Body` | `_BaseMap` **and** `_MainTex` |
| Eyes | `Bat_lEye`, `Bat_rEye` | `_BaseMap` **and** `_MainTex` |
| Wing dust | `VFXBatWingDust` | colour properties, no texture |

Unlike the cat — whose body and whiskers were separate materials that could be coloured with no
masking — the whole bat is a **single hand-painted atlas (`Bat_DIF`) on one material**. So the parts
that share that texture are separated two ways:

1. **By colour cluster.** The navy body/wings and the warm "skin" (ears + eye-surrounds) are told
   apart by hue and saturation, the same HSV logic the cat used for its eye.
2. **By location.** The fangs, the mouth area and the face can't be told apart by colour — same
   material, same tones — so each is a **box in the texture's UV space** over the feature, dialed
   in live in the mirror during development and baked to constants. The mouth is a flat fill (faded
   by Mouth Intensity), the face is a **feathered ellipse** that carves the ears colour off the
   face and softens the edge around the eyes, and the ears colour is **confined to a UV box** so it
   stays on the ears instead of bleeding across the whole skin.

`Bat` carries **two** albedo slots — `_BaseMap`/`_BaseColor` (URP) and `_MainTex`/`_Color`
(Standard-shader leftovers from a converted material). Both are written, because recolouring only
one leaves the other showing the original.

### Splitting the eye

The eye is a bright, **desaturated** blob in `Bat_DIF` (not a saturated iris like the cat's). Every
eye pixel is routed through a brightness split: the dark centre is the pupil, the bright mass is the
eye, and the pure-white specular is the glint — each recolourable on its own, or left original when
blank. The thresholds are constants in `BatColorPatch.cs`: they describe this character's art, and
the values in between only reproduce bugs.

## Gotchas worth knowing

**The bat does not revert like the cat.** The cat body applied its customization through a
`MaterialPropertyBlock` that overwrote plain `material.SetTexture` calls every frame. The bat body
and eyes report `HasPropertyBlock=false`, so a material write actually renders — but colours are
still reapplied every frame from `BatColorReapplier` as belt-and-suspenders against anything else
that might touch the material.

**The body albedo is near-black by design.** It is recoloured at full brightness (the internal
brightness floor is 1.0). That looks correct rather than flat because the body's shading comes from
real-time lighting and its normal map, not from the texture — which is also why Body Intensity
fades toward the texture rather than toward black.

**The fang box sits inside the mouth box.** A Mouth colour would swallow the fangs, so the fang box
re-claims itself on top — using the Fang colour, or white when it's blank — so the fangs are never
bled on.

## History

The bat body was mapped with a developer probe (a renderer/material/texture dump, a texture export,
and a form-grant key) plus a set of live debug bands and draggable UV-box sliders in the mirror. All
of it was **removed for the 1.0.0 release**; it lives in git history if the sibling mermaid mod
needs it again.

## Reusable findings

The parts that apply to any Moonlight Peaks mod touching character colour are written up at the repo
root: [16-recolouring-characters.md](../../16-recolouring-characters.md).
