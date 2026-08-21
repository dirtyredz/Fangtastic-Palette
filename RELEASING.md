# Releasing Fangtastic Palette

Repo-wide rules live at the root; this file only covers what is specific to this mod.

- Versioning and archive layout: [12-versioning-and-release.md](../../12-versioning-and-release.md)
- Visual integration: [10-visual-integration.md](../../10-visual-integration.md)
- Save safety: [11-mod-data-and-saves.md](../../11-mod-data-and-saves.md)
- Nexus page: [13-nexus-page-standard.md](../../13-nexus-page-standard.md),
  [14-description-review.md](../../14-description-review.md), [15-page-style.md](../../15-page-style.md)

The first published version is **1.0.0**; bump only when publishing, one CHANGELOG entry per
release. The source directory, shipped mod, assembly, GUID and config folder are all
**FangtasticPalette**.

## Build a release

```powershell
powershell -File pack.ps1
```

Produces `dist/FangtasticPalette-<version>.zip` laid out as
`BepInEx/plugins/FangtasticPalette/FangtasticPalette.dll`, reading the version from the csproj and
refusing to pack if `PluginVersion` in `Plugin.cs` disagrees.

There is no test project — every path reads live game state (the wardrobe screen, the preview
rig, the inventory). The checklist below carries the weight.

## Pre-release checklist

Root checklist first: [12-versioning-and-release.md](../../12-versioning-and-release.md). Then:

### The colours (config path)

- [ ] Each of Body, Ears, Fangs, Mouth, Face, Eye, Pupil, Eye Highlight and Wing Dust applies in
      Bat Form from the `.cfg` / Mod Nook, live, with no re-equip
- [ ] Blank leaves that part vanilla; a bad hex is ignored rather than crashing
- [ ] Body / Ear / Face / Mouth Intensity each fade their colour toward the original; Wing Dust
      Strength brightens the flight trail

### The regions

- [ ] Fangs stay fang-coloured over a Mouth colour (fang box on top); a blank Fang swatch still
      shows white, not mouth bleed
- [ ] The Face oval sits on the muzzle and feathers into the ears colour around the eyes
- [ ] The Ears colour stays on the ears/eye-surrounds and does not flood the whole face

### The wardrobe tab

- [ ] The **Bat Form** tab appears in the mirror only when you **own Bat Form** (gated on
      inventory); it does **not** appear in new-character creation
- [ ] Preview shows the bat; every swatch row recolours it live, and each intensity slider sits
      under its colour
- [ ] Panel **scrolls** (mouse wheel over a swatch, and drag)
- [ ] Pick is a live preview: **Confirm keeps it, closing without Confirm reverts** the colours
      (check the world bat too, not just the preview)
- [ ] The custom `+` tile opens the RGB picker; the chosen colour writes back
- [ ] **Freeze Flap** holds the preview bat still and releasing it resumes the flap
- [ ] Bat tab icon shows (from `config/FangtasticPalette/tab-icon.png` if present, else the
      generated glyph); the tab strip is scrolled to the first tab on open, not centred
- [ ] Leaving and re-entering the mirror keeps everything working (no stale template cache)

### Compatibility

- [ ] Works with **Serena's Enchanted Studio disabled** — the tab and swatches still function
      (cloned-swatch template absent → drawn-swatch fallback, no errors)
- [ ] Switching between form tabs (e.g. Purrtastic's Cat Form tab) leaves only the bat in the
      preview, not both bodies at once

### Housekeeping

- [ ] `<Version>` and `PluginVersion` match — `pack.ps1` enforces this
- [ ] CHANGELOG has one entry for this version
- [ ] `Colors/VerboseLogging` defaults to `false`; a normal session logs only the load line
- [ ] **Dev tools stripped** — no probe key, form-grant key, debug bands, or UV-box sliders remain
- [ ] Fresh install: delete `BepInEx/config/com.dirtyredz.moonlightpeaks.fangtasticpalette.cfg`,
      launch, confirm sensible defaults are written (and no `[Probe]` / `[Debug]` sections)
- [ ] Screenshots show the current build; thumbnail composed at **16:9**
- [ ] Archive extracted onto a clean install and verified in game

## Save safety

The mod stores nothing of its own and adds no persistence type. It only writes its own
`ConfigEntry` values (hex colours and slider floats) and, in the wardrobe, snapshots/restores
those same values around Confirm. No save collection is touched. See
[11-mod-data-and-saves.md](../../11-mod-data-and-saves.md).

## Licence

**MIT** — see [LICENSE](LICENSE). Set the Nexus permissions to agree with it:

| Nexus permission | Set to |
|---|---|
| Upload to other sites | Allowed |
| Convert to other games | Allowed |
| Modify and release | Allowed |
| Use assets in own files | Allowed |
| Include in mod packs / collections | Allowed |

## Editing note

Do not round-trip these files through `Get-Content -Raw | Set-Content` in PowerShell — it
re-encodes non-ASCII characters and has corrupted em-dashes in this repo before.
