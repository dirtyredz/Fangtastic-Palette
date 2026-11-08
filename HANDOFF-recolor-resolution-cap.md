# Handoff: cap recolour resolution to prevent the eye-atlas freeze

Port of a fix landed in the sibling **Purrtastic Palette** mod. The recolour engine is copied per repo
(shared only as docs — see `../../16-recolouring-characters.md`), so this must be applied here by hand;
do **not** try to share code across the mods.

## The problem (as seen in Purrtastic)
Recolouring a large source texture at native resolution runs a per-pixel HSV loop on the **main
thread**. Purrtastic's cat eye atlas ships at **4096² = 16.7M pixels** → **~1.7s per build**, and
~85% of that is the CPU pixel loop (profiled with a stopwatch; the GPU read-back and upload are
minor). Two eye atlases rebuild on every form-transform, wardrobe tab-open, and eye-colour change —
so ~3.4s freezes throughout. Confirmed pre-existing (reproduced on a first-version machine) and
independent of any other mod.

## Does Fangtastic actually need it?
The bat eye atlas is currently smaller, so Fangtastic doesn't freeze **today**. This is a
**defensive** port:

- The cap **only downscales textures above the cap** — anything already ≤ 512² is byte-for-byte
  untouched, so it's a no-op for small atlases and cannot regress current behaviour.
- It protects against any large source (a big bat-body skin atlas, future assets) and keeps the
  Palette mods consistent.

**Recommended first step:** profile before changing anything, exactly as Purrtastic did — drop a
stopwatch into each `Build` and log `width x height` + phase timings, run the game, switch eye/body
colours, and read `BepInEx/LogOutput.log`. If no atlas exceeds 512², this change is pure insurance;
if one does (e.g. a 2048²/4096² atlas), you'll see the multi-hundred-ms/second builds and the cap
will fix them. Profiling snippet (temporary — remove after):

```csharp
var sw = System.Diagnostics.Stopwatch.StartNew();
// ... existing Build body ...
sw.Stop();
Purrtastic... -> FangtasticPalettePlugin.Log.LogInfo(
    $"[FangtasticPalette] TIMING Build {width}x{height} ({sourcePixels.Length} px): {sw.ElapsedMilliseconds}ms.");
```

## The fix — `src/TextureRecolor.cs`

**1.** Add a constant near the cache field (top of the class):

```csharp
// Cap the recolour working resolution. A large source atlas (e.g. Purrtastic's 4096² eye atlas)
// run through the per-pixel HSV loop froze the game ~1.7s per build. The recoloured region is tiny
// on screen, so full res is waste. Downscale anything above this cap before the pixel work; textures
// already <= the cap are untouched. Raise it if a region looks soft; lower it for more speed.
// NOTE this is a GLOBAL cap: at 512 it also downscales any 1024² atlas (Purrtastic's fur/whiskers),
// which was confirmed still crisp in-game. If a region needs to stay sharp, make the cap per-caller
// (via the spec/factory) rather than raising the global value.
private const int MaxRecolorDimension = 512;   // Purrtastic settled here after in-game A/B; tune per your atlases.
```

**2.** In **BOTH** build methods — `Build(...)` (the eye recolour, ~line 51) **and**
`BuildBatBody(...)` (the body recolour, ~line 210) — replace these three lines:

```csharp
var width = source.width;
var height = source.height;
var sourcePixels = ReadPixelsRobust(source, width, height);
```

with:

```csharp
// Downscale oversized atlases to the cap before the pixel work (see MaxRecolorDimension).
// ReadPixelsRobust blits the source into a width×height RenderTexture, so a smaller target here
// means the GPU downscales on the blit and the loop/output run at that size.
var scale = Mathf.Min(1f, (float)MaxRecolorDimension / Mathf.Max(source.width, source.height));
var width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
var height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
var sourcePixels = ReadPixelsRobust(source, width, height);
```

Nothing else changes. Everything downstream already keys off `width`/`height`: the pixel loop, the
output `new Texture2D(width, height, ...)`, and — importantly for `BuildBatBody` — the spatial-region
helpers `InBox`/`Coverage` (they compute pixel position from the index and `width`/`height`, so they
stay proportional at the smaller size). `ReadPixelsRobust` needs no change; the `Graphics.Blit` inside
it does the downscale.

## Why it's correct
- `ReadPixelsRobust` blits `source` (any size) into a `width×height` RenderTexture, so a smaller
  target = automatic bilinear downscale on the GPU.
- The recoloured `Texture2D` replaces the atlas; its **normalised** UVs sample the smaller texture
  fine — just at lower resolution.
- The eye region split (saturation/value thresholds) and the bat-body spatial UV regions are all
  proportional/normalised, so they survive the downscale.

## Verify in-game (cannot be done from CI — the owner runs the game)
1. Transform into **Bat Form** → no freeze.
2. Open the **Bat Form wardrobe tab** → no freeze on enter.
3. Switch **eye** colours and **bat-body** colours → snappy.
4. Look closely: eye iris/pupil/highlight and bat-body region boundaries still crisp, **no colour
   fringe or blur at region edges**. If any region looks soft/fringed, raise `MaxRecolorDimension`
   to `2048` (still far faster than native 4096²) and re-check.

## Reference (Purrtastic Palette)
- Commit: `perf: cap recolour resolution to fix the eye-atlas freeze` (adds `MaxRecolorDimension` to
  `src/TextureRecolor.cs`).
- Docs: `docs/DECISIONS.md` **ADR-010**, `docs/GOTCHAS.md` ("the cat eye atlas is 4096²"),
  `docs/BACKLOG.md` (done entry). Rejected alternatives (off-thread loop; GPU shader) are recorded in
  ADR-010 with rationale.
