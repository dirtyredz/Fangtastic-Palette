# Nexus Mod Page — Fangtastic Palette

> **Pasting into the upload form? Use [nexus-paste.md](nexus-paste.md), not this file.**
> The copy here is wrapped for reading; the editor turns every wrap into a `<br>`. The paste file
> is the literal BBCode. Structure: [13-nexus-page-standard.md](../../13-nexus-page-standard.md);
> look: [15-page-style.md](../../15-page-style.md).

Draft copy for the Nexus listing. Same shape as the sibling [Purrtastic Palette](../PurrtasticPalette/NEXUS.md)
page — read its notes on the upload form, thumbnail ratio and art direction first; they all still apply.

---

## Fields

| Field | Value |
|---|---|
| **Name** | Fangtastic Palette |
| **Summary** (short, shows in listings) | your bat form is always the same little vampire bat — recolour its body, wings, ears, fangs, face and flight-dust, live, from a picker in the mirror |
| **Category** | Confirm against the game's list. Best fit is a visual/cosmetic category (where recolour and reskin mods sit); fall back to Gameplay if there is no cosmetic one. |
| **Version** | 1.0.0 |
| **Nexus page** | *(new — no mod id yet)* |
| **Requirements** | BepInEx 5 (win_x64), 5.4.23.5 or newer — required |
| | [Mod Nook](https://www.nexusmods.com/moonlightpeaks/mods/127) — optional, for in-game settings |
| | Mod Menu — optional, the alternative to Mod Nook |
| **Tags** | customization, cosmetic, quality of life *(Nexus tags are a fixed vocabulary — confirm each exists before relying on it)* |
| **Licence** | MIT |

**Searchable words.** The name is kept clean. The words a player would search — *recolour*, *bat
form*, *vampire bat*, *colour* — are carried by the summary instead, so they still land in search
without turning the title into a sentence.

---

## Full description — paste into Nexus

### Description

Every bat form in Moonlight Peaks is the same little bat. You unlock it, you turn into it, and it
looks exactly like everyone else's.

Fangtastic Palette lets you make it yours. Recolour the body and wings, the ears, the fangs, the
nose-and-mouth, the face, the eyes — iris, pupil and the bright glint each on their own — and the
dust the bat trails when it flies.

There are two ways to set the colours. Open the mirror in your house, choose Change clothes, and a
**Bat Form** tab is waiting at the end of the row: it shows your bat and gives a swatch picker for
each part, presets plus a **+** tile for any colour you like. Or set them as plain hex codes or
colour names in the config — or in Mod Nook, in game — and they apply on the spot.

Four Intensity sliders (body, ears, face, mouth) fade a colour toward the bat's own shading, so you
can go from a soft tint to a full vivid recolour.

In the mirror the colours are a live preview: they change the bat as you browse, and only stick when
you press **Confirm**. Back out without confirming and everything returns to how it was, the same as
trying on clothes.

Nothing new is written to your save — only your own colour settings.

---

### Main features

- Recolour nine parts of Bat Form: body & wings, ears, fangs, the nose/mouth area, the face, iris, pupil, the bright eye glint, and the flight dust
- Four Intensity sliders — body, ears, face and mouth — to fade a colour toward the bat's own shading for a softer, less flat look
- A Bat Form tab in the mirror's wardrobe, with a live preview of your bat and each slider under its colour
- Preset swatches per part, plus a + tile that opens a full RGB colour picker
- Colours preview live and only stick on Confirm — leave without confirming and they revert
- Or set them as hex codes or colour names in the config / Mod Nook, applied instantly with no re-equip
- The tab appears only once you have unlocked Bat Form
- Fangs stay white by default and sit on top of the mouth colour, so they can't be painted over
- Save-safe — only your own colour settings are stored; nothing new is written to your save
- Works with or without Serena's Enchanted Studio

---

### Requirements

**Required**

- BepInEx 5 (win_x64), version 5.4.23.5 or newer

**Recommended companion**

- **Mod Nook** — my in-game settings menu. Fangtastic Palette's colours show up in it as editable
  fields, so you can change them from the pause menu instead of a text file — handy when you want
  the exact same colour on the world bat as in the mirror. Not needed; the mirror picker is the
  main way in, and without Mod Nook the settings live in a plain config file.
  https://www.nexusmods.com/moonlightpeaks/mods/127
- **Mod Menu** by Elsiabeth does the same job and is also supported. Mod Nook and Mod Menu can both
  be installed — each adds its own button and neither interferes with the other.

PC/Steam only. The Switch and mobile builds cannot load BepInEx.

**Compatibility**

Serena's Enchanted Studio touches the same mirror, but the two do not conflict — install them
together or run Fangtastic Palette on its own. Without Serena's, if the swatch template it clones
can't be found, the picker falls back to plain drawn swatches rather than failing. It also sits
happily beside Purrtastic Palette (my Cat Form recolour) — both add their own tab to the mirror.

---

### Installation instructions

**With Vortex**

Open the Files tab, click the Vortex button, and enable the mod. Done.

**Manually**

1. Install BepInEx 5 (win_x64) into your Moonlight Peaks folder, if you do not have it already.
   The BepInEx folder sits beside Moonlight Peaks.exe.
2. Launch the game once, then quit. This creates the BepInEx/plugins folder.
3. Download the archive from the Files tab and extract it over your Moonlight Peaks folder, so the
   file ends up at BepInEx/plugins/FangtasticPalette/FangtasticPalette.dll.
4. Launch the game.

Settings are written to BepInEx/config/com.dirtyredz.moonlightpeaks.fangtasticpalette.cfg on first
launch. With Mod Nook installed you never need to open it — every colour appears under
Pause > Mod Nook and applies immediately, without a restart.

To uninstall, delete the BepInEx/plugins/FangtasticPalette folder. Your bat goes back to its
default colours; nothing was ever written to your save.

---

### Configuration

Settings are written to BepInEx/config/com.dirtyredz.moonlightpeaks.fangtasticpalette.cfg on first
launch. Every colour value is a hex code (#8800FF) or an HTML colour name (purple); leave one blank
to keep that part vanilla.

Install Mod Nook and you can change them in game instead. Fangtastic Palette shows up in it on its
own, under Colors — the same colours the mirror picker sets, so the world bat and the mirror can
never disagree. Nothing here needs it; it just makes the mod easier to live with.

---

### Shout outs

- **Little Chicken Game Company** for making a game worth spending this much time inside.
- The **BepInEx** and **HarmonyX** teams, without whom none of this scene exists.
- **SerenaEnchanted**, whose Enchanted Studio does the mirror-recolour work for the human side and
  showed how much nicer picking a colour in game is than editing a file.
- **Elsiabeth** for Mod Menu, which made the case that in-game settings were worth having.
- **My Mate**, for being my inspiration.

---

## Changelog entries for the Nexus page

Player-facing. Describe the **symptom**, not the cause — the repo README names the internals.

### 1.0.0

```
First release.

- Recolour your bat form: body & wings, ears, fangs, the nose/mouth area, the face, the eyes (iris, pupil and glint), and the dust trail in flight.
- Fade any of the body, ears, face or mouth colours toward the bat's own shading with an Intensity slider.
- Pick colours in the mirror's new Bat Form tab, with a live preview, or set them in the config.
- In the mirror, colours only stick when you press Confirm; back out and they revert.
- Nothing new is written to your save.
```

---

## Screenshots

Files live in `screenshots/`. The thumbnail is set separately in the upload form. Not yet delivered —
capture from the current build before publishing.

| # | Shot | Notes |
|---|---|---|
| - | Thumbnail, **16:9** | title art must read "Fangtastic Palette" exactly; proofread lettering at 4–8x |
| - | Title banner | matches the bat tab icon / crest |
| 1 | The money shot: Bat Form tab, swatch rows + intensity sliders, a recoloured bat | tab, decorated headers, selected swatch + `+` tile, scrollbar, live preview, Confirm/Cancel prompts in one |
| 2 | Another colourway | |
| 3 | Another colourway | |
| 4 | The custom RGB picker | shows the `+` tile's picker |
| 5 | The bat tab in the strip | shows the tab icon sitting with the vanilla tabs |

### Thumbnail must be composed at 16:9

Listing tiles use `object-fit: fill`, so an off-ratio thumbnail is **stretched, not cropped**.
Proofread any generated lettering at 5x or more before accepting it — the title art reads
"Fangtastic Palette", which must match the mod name exactly.

### Art direction

Palette is fixed by [10-visual-integration.md](../../10-visual-integration.md): plum fill, gold
rim, warm gold text — matching the bat tab icon in game and the sibling Purrtastic page.

---

## Notes before publishing

- **Play-test** the full RELEASING.md checklist in game before uploading.
- State plainly that it is **save-safe** — this community reads for that. Only the mod's own colour
  settings are stored.
- Say **cosmetic, not a cheat**: it recolours the form, nothing else.
- List BepInEx as **required** and Mod Nook / Mod Menu as **optional**.
- Set the Nexus permissions to agree with the MIT licence — see [RELEASING.md](RELEASING.md).
- Run the [RELEASING.md](RELEASING.md) checklist, then `pack.ps1`, and upload
  `dist/FangtasticPalette-1.0.0.zip`.
