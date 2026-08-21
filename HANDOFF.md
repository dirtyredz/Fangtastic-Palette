# Hand-off — Fangtastic Palette (Bat Form recolour), mid-development

Second mod in the "...tastic Palette" set: **Purrtastic** (cat, shipped 1.1.0), **Fangtastic** (bat,
this mod, in progress), **Fintastic** (mermaid, planned). Recolours Bat Form. Reuse the settled
recolour methodology — do not rediscover it. See `../../16-recolouring-characters.md` (esp. §10 on
the spatial-mask + live-slider approach this mod pioneered).

Not a git repo (unlike the cat). Build: `cd src && dotnet build -c Release` — auto-deploys to
`…/Moonlight Peaks/BepInEx/plugins/MoonlightPeaksMods/FangtasticPalette`. 0 errors expected
(the `System.Net.Http` MSB3277 warnings and one obsolete-`enableWordWrapping` warning are benign).
**Never launch the game** — the user tests; end messages with "Ready for you to test."

## What the mod does (all working)

Recolours Bat Form via a Harmony postfix on `FormToolView<BatToolAsset>.HandleEnterVisuallySwitched
BodyViewAsset` + a per-frame reapplier. Full colour set, controllable from **Mod Nook** and from a
**Bat Form tab in the mirror wardrobe** (ported from the cat; live preview, revert-on-cancel, bat
tab-icon at `BepInEx/config/FangtasticPalette/tab-icon.png`).

**Body layout** (from the probe): one atlas `Bat_DIF` on one `Bat` URP/Lit material drives the whole
head (`Bat_Body`, 2 materials) + eyes (`Bat_lEye`/`Bat_rEye`, separate renderers). `HasPropertyBlock
=false`. Material split: **material 0 = wings + lower body/chest**, **material 1 = the whole head**.

### Colours / controls (all live via Config.SettingChanged)
- **Body** (+ **Body Intensity** slider: 1=full colour, lower fades toward the original shading).
  On material 0 (`skinAsBody`) the whole submesh is just Body colour.
- **Ears** — ONE colour for all warm head skin (ears + eye-surrounds), brightness preserved.
  (+ **Skin Blend** slider: how much original shows through.) Was 3 bands (rim/beige/brown);
  collapsed to one. Config keys `RimColor`/`BeigeColor`/`BrownColor` were REMOVED; `EarsColor` is fed
  to all three internal skin slots so `GetOrBuildBatBody`'s hue/value split resolves to one tone.
- **Eyes / Pupil / Eye Highlight** — the eye is a desaturated blob split by brightness (pupil <0.45,
  eye mid, glint ≥0.99). Glint is spatially masked (`EyeGlint*` box) so it doesn't bleed past the iris.
- **Wing Dust** (+ **Wing Dust Strength** slider) — flight-only VFX (material + ParticleSystem startColor).
- **Fangs / Nose / Mouth** — **spatial UV-rectangle masks** (can't separate by colour): baked
  constants `FangBox`/`NoseBox`/`MouthBox` in `BatColorPatch.cs`. Fang is shaded; nose/mouth flat.
- **Face** — a **feathered ELLIPSE exclusion** (carves the ears colour off the face; Face colour if
  set, else original). Box = `FaceBox*` config (still adjustable). **Face Softness** slider feathers
  the edge. Was rectangular → user wanted round → ellipse; feather smooths eye-area artifacting.
- **Body oval** — a positionable feathered ellipse painting the **Body** colour (body treatment) into
  the skin. Box = `BodyBox*` config, **Body Softness** slider. Additive (only when Body colour set).

### Spatial-mask engine (`TextureRecolor.cs`)
`GetOrBuildBatBody` takes `SpatialRegion[]`. Each region: UV box, colour, flags `Flat`/`AlwaysClaim`
(claim pixels even blank → carve out, used by Face)/`Ellipse`/`Feather`/`BodyStyle`. Loop computes a
BASE (ears/body hue-value result) then **composites regions over it by `Coverage`** (0..1, feathered
for ellipses). `Coverage` uses a GLSL-style `SmoothStep01` — **NOT** Unity's `Mathf.SmoothStep`
(which interpolates between its first two args; using it wrong made the feather a faint ~10% toggle —
fixed). `u = (i%w)/w`, `v = (i/w)/h`; **GetPixels is bottom-up** so v=0 is texture bottom.

### Dev / debug (Probe section, STRIP before release)
`ProbeKey`(F7 dump)/`GiveFormsKey`(Home = grant+equip Bat)/`ForceTestColor`, `EyeDebugBands`,
`BodyDebugBands` (floods regions: body=blue ears=green fang=magenta nose=cyan mouth=yellow face=orange),
`MaterialSplitDebug` (mat0=blue mat1=red), `UVDebug` (RGB=(u,v,0.25) to read a feature's u,v off a
photo), `ShowFaceBox` (shows Face+Body oval box sliders in the mirror WITHOUT the debug flood, to aim
against real colours). Face/Body **Box sliders** appear in the panel when `BodyDebugBands||ShowFaceBox`.

## Current status — awaiting the user's final mask values

The user is dialing in, in the mirror (`ShowFaceBox` on): **Face Box** (feathered oval over the face),
**Body Box** (body-colour oval into the skin), and **Face/Body Softness**. Baked-so-far constants
(BatColorPatch.cs): FangBox (0.22,0.37,0.04,0.20), NoseBox (0.0071,0.2337,0,0.2870), MouthBox
(0.5888,0.7988,0,0.1639). Face/Body boxes are still CONFIG (live-adjustable). **Next: the user hands
over final Face Box / Body Box / Face Softness / Body Softness values → bake them to constants in
BatColorPatch.cs, then remove the box config + the panel box sliders (like the other boxes were).**

## What's left before release
1. Bake the final Face Box / Body Box values + softness defaults; strip the box sliders/configs.
2. Pick final default colours (optional — blanks = vanilla is fine).
3. **Strip all dev/debug** (probe keys, GiveForms, *DebugBands, MaterialSplitDebug, UVDebug,
   ShowFaceBox, and ProbeController) — as the cat did for its release. They live in this doc/history.
4. Version → 1.0.0, README, `pack.ps1`, Nexus page + screenshots (drive with the `nexus-publish`
   skill; see the cat's `NEXUS.md`/`RELEASING.md` as the template). Bat tab-icon PNG is already at
   `BepInEx/config/FangtasticPalette/tab-icon.png` (user-made, processed transparent+square).

## Files (`src/`)
Plugin.cs (config + Harmony), BatColorPatch.cs (routing, spatial regions, baked boxes),
TextureRecolor.cs (SpatialRegion + GetOrBuildBatBody + eye recolour + Coverage/SmoothStep01),
BatColorReapplier.cs, ProbeController.cs (dev), BatFormWardrobe.cs / BatFormColorPanel.cs (mirror UI)
+ ported UI helpers (Palette, GameFonts, CircleSprite, ScrollForwarder, PreviewBloomSuppressor,
Templates, HeaderDecoration, ColorPickerPopup, PanelSprite, BatFormSwatch, PawSprite, TabIcon).

## Sibling note
The cat mod (`mods/PurrtasticPalette`) shipped **1.1.0** (Fur Intensity slider) and includes the
"hide sibling forms in the preview" fix so the bat/cat wardrobe tabs coexist (bat also hides others).
Delete this HANDOFF.md when the mod is released (not part of the shipped mod).
