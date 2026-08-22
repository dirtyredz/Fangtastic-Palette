# ARCHITECTURE — Fangtastic Palette

How the system works (the runtime shape and data flow). For the code map see
[../STRUCTURE.md](../STRUCTURE.md).

## System overview

The mod hooks the game at two points via HarmonyX and drives everything off BepInEx config:

1. **Body-load hook** — a postfix on `FormToolView<BatToolAsset>.HandleEnterVisuallySwitchedBodyViews`
   (the closed generic scopes it to Bat Form only). This is the first moment the async-loaded bat body
   is fully live. It applies the configured colours.
2. **Wardrobe hook** — postfixes/prefixes on `WardrobeCustomizationScreen` inject a "Bat Form" tab
   into the mirror UI and manage a live preview.

Colours live as a BepInEx `Colors` config section (rendered as a menu by Mod Nook). Every change
raises `Config.SettingChanged`, which reapplies immediately — no re-equip, no restart.

## Data model

- **Inputs:** 9 colour `ConfigEntry<string>` (hex or HTML name, blank = vanilla), 4 intensity + 1
  wing-dust-strength `ConfigEntry<float>`, a `FreezePreviewFlap` bool, a `VerboseLogging` bool.
- **Bat rig:** one hand-painted atlas `Bat_DIF` on one URP/Lit "Bat" material drives the whole
  creature. Renderer `Bat_Body` (2 submeshes: wings+lower body / head), separate `Bat_lEye`/`Bat_rEye`,
  and `VFXBatWingDust` particles. The material carries **two** albedo slots (`_BaseMap`/`_BaseColor`
  and `_MainTex`/`_Color`); both are written or the untouched one shows the original.
- **Outputs:** regenerated `Texture2D`s assigned to the materials (+ mirrored into a
  `MaterialPropertyBlock`), and HSV-shifted particle colours for the dust.

## Recolour method (the core design)

Everything is a **texture regeneration**, not a tint:

- **HSV colorize** — each output pixel takes the target colour's hue+saturation but keeps the source
  pixel's own brightness (floored). An RGB multiply can't introduce a channel the source lacks and
  turns dark regions to mud; HSV colorize reaches any colour and preserves shading.
- **Robust read** — source textures are read via a `RenderTexture` blit + `ReadPixels`, so it works on
  the game's non-Read/Write-enabled shipped textures.
- **Region separation** — the whole bat is one atlas, so parts that share it are separated two ways:
  **by colour cluster** (navy body vs warm skin, told apart by hue/saturation into Body/Rim/Beige/Brown)
  and **by location** (fangs, mouth, face — UV boxes over the feature; the face is a feathered ellipse;
  the ears colour is confined to a UV box). The eye is split by brightness into pupil / eye / glint.
- **Caching** — every regenerated texture is cached by (source, all colours, all thresholds/boxes), so
  the per-frame reapply is a dictionary lookup, not a rebuild.

## Key flows / sequences

**Live recolour**
```
body loads ─► BatColorPatch.Postfix ─► ApplyBatColors ─► ApplyToBody(bodyView)
   for each renderer: Bat_Body → ApplyBodyColor → TextureRecolor.GetOrBuildBatBody (cached)
                      eyes     → ApplyEyeColor  → TextureRecolor.GetOrBuild   (cached)
                      dust     → ApplyWingDustColor (HSV shift, no regen)
   BatColorReapplier.Update() re-runs body+eyes every frame as a safety net
```

**Wardrobe preview (mirror)**
```
WardrobeCustomizationScreen.OnShow ─► add "Bat Form" tab (only if player owns Bat Form)
   tab select ─► instantiate a bat body into the preview rig, hide other bodies + VFX + bloom
             ─► ApplyToBody(previewInstance)  (separate material instances from the live player)
             ─► build the swatch/slider panel
   swatch pick ─► write ConfigEntry (live preview) ─► recolour preview + live player
   Confirm ─► keep;  close-without-confirm ─► restore the colour snapshot (reverts world too)
   vanilla tab / OnHide ─► tear down panel, restore human body + bloom
```

## External interfaces

- **BepInEx** config file `com.dirtyredz.moonlightpeaks.fangtasticpalette.cfg` and Mod Nook menu.
- **Optional user asset** `BepInEx/config/FangtasticPalette/tab-icon.png` overrides the tab icon.
- **Game types** reached by Harmony/reflection: `FormToolView<BatToolAsset>`, `WardrobeCustomizationScreen`,
  `CustomizationOptionListWidget`, `CustomizationCategoryListWidget`, `SliderButton`, `BumperMenuWidget`,
  `EntityCustomization`/`PlayerView`, `GameInventory`. Names/fields are resolved defensively so a game
  update degrades gracefully (tab hidden, drawn-swatch fallback) rather than crashing.

## Design notes

- **Reapply-every-frame** is belt-and-suspenders; the bat reports `HasPropertyBlock=false` (unlike the
  cat), so a plain material write should stick — but the loop is cheap when cached.
- **Preview body is a separate instance** from the live player (`CustomizationCharacterPreview` holds its
  own `BodyViewAsset`), so it must be coloured explicitly.
- **Bloom is suppressed** in the preview via a throwaway high-priority global `Volume`, never by mutating
  the shared scene `VolumeProfile`.

_Living doc — refresh with /project-docs when it drifts._
