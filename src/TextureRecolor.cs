using System.Collections.Generic;
using UnityEngine;

namespace FangtasticPalette
{
    /// <summary>
    /// HSV colorize: for each source pixel, take the TARGET colour's hue and saturation but keep
    /// the source pixel's own value (brightness) for shading. This replaced an earlier
    /// RGB-multiply-by-luminance approach (target.rgb * luminance) that turned out to have a real
    /// problem: a source region that's naturally dark can only ever produce a dark, desaturated,
    /// muddy result under multiplication, no matter how saturated the target colour is, while
    /// naturally bright regions recoloured strongly. HSV colorize doesn't have that failure mode:
    /// a dark source pixel becomes a *darker version of the exact target hue*, not a grayed-out
    /// blend, so shading is preserved without washing out the colour itself.
    ///
    /// Also fixes what a flat multiply-tint can't do at all: introduce a channel the source barely
    /// has (blue, on an orange texture) - HSV colorize uses the target's own hue outright, so any
    /// colour is reachable regardless of the source texture's own channel makeup.
    ///
    /// Reads pixels via a RenderTexture blit + ReadPixels round-trip rather than
    /// Texture2D.GetPixels() directly, so this works regardless of whether the source asset has
    /// "Read/Write Enabled" set - the game's shipped textures almost certainly don't.
    ///
    /// Copied wholesale from mods/PurrtasticPalette - this is character-agnostic and is exactly the
    /// machinery 16-recolouring-characters.md says to reuse rather than rediscover.
    /// </summary>
    internal static class TextureRecolor
    {
        // String-keyed because the parameter set has grown past a comfortable ValueTuple arity
        // (hue passthrough added three more). The key folds in the texture's instance id and every
        // recolour parameter, so any change produces a distinct cache entry.
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        /// <param name="splitBelowSaturation">
        /// Source pixels whose HSV saturation is at or below this are treated as the separate
        /// "pupil" region; everything else takes the main target colour. Saturation, not
        /// brightness, is the right axis on a GradientAtlas swatch texture: within a strip the
        /// VALUE varies top-to-bottom while hue/saturation stay ~constant, so a luminance split
        /// slices across the gradient instead of between strips.
        ///
        /// Pass a NEGATIVE value to disable the split - not 0. Saturation is exactly 0 for any
        /// pure black/grey pixel, so a 0 threshold still matches every such pixel and copies it
        /// through unrecoloured.
        /// </param>
        /// <param name="highlightColor">
        /// What the desaturated-and-BRIGHT region is remapped toward. Null leaves those pixels
        /// exactly as they were.
        /// </param>
        /// <param name="splitBelowValue">
        /// Within the desaturated region, pixels at or below this HSV value are the actual pupil
        /// rather than the highlight. Saturation alone cannot tell these apart: a white highlight
        /// and a black pupil are both fully desaturated. Pass a negative value to disable the
        /// pupil split, leaving the whole desaturated region as highlight.
        /// </param>
        /// <param name="pupilColor">
        /// What the desaturated-and-DARK region is remapped toward. Null leaves those pixels
        /// exactly as they were.
        /// </param>
        /// <param name="brightnessFloor">
        /// Source value is remapped from [0, 1] to [brightnessFloor, 1], then multiplied by the
        /// TARGET colour's own value - so the output is always anchored to how bright the colour
        /// you actually picked is, not an independent brightness computed from the floor alone.
        /// 0 = no floor, full original shading range preserved.
        /// </param>
        /// <param name="passHueCenter">
        /// Hue (0..1) of a region to LEAVE UNTOUCHED - passed through at its original colour instead
        /// of recoloured. Negative disables passthrough. This is how one shared atlas can recolour
        /// only some of its regions: the bat's whole body is one texture, so recolouring the navy
        /// toward Body Color while passing the pink ears/skin (a distinct hue) through is what keeps
        /// their definition instead of flattening the whole creature to one colour. Circular
        /// distance, so a centre near 1.0 correctly matches hues that wrap past 0 (pink/red).
        /// </param>
        /// <param name="passHueRange">Half-width (0..1) of the passthrough hue window.</param>
        /// <param name="passMinSaturation">
        /// Only pixels at least this saturated are eligible for passthrough - near-grey pixels have
        /// an unreliable hue, and the body's own dark/desaturated pixels must still recolour.
        /// </param>
        internal static Texture2D GetOrBuild(
            Texture source, string hex, Color target, float splitBelowSaturation = -1f, Color? highlightColor = null,
            float brightnessFloor = 0f, float splitBelowValue = -1f, Color? pupilColor = null,
            float passHueCenter = -1f, float passHueRange = 0f, float passMinSaturation = 0f,
            float splitAboveValue = 2f, Color? glintColor = null,
            float glintUMin = 0f, float glintUMax = 1f, float glintVMin = 0f, float glintVMax = 1f)
        {
            var key = string.Join("|",
                source.GetInstanceID().ToString(), hex, splitBelowSaturation.ToString("R"),
                highlightColor?.ToString() ?? "-", brightnessFloor.ToString("R"), splitBelowValue.ToString("R"),
                pupilColor?.ToString() ?? "-", passHueCenter.ToString("R"), passHueRange.ToString("R"),
                passMinSaturation.ToString("R"), splitAboveValue.ToString("R"), glintColor?.ToString() ?? "-",
                glintUMin.ToString("R"), glintUMax.ToString("R"), glintVMin.ToString("R"), glintVMax.ToString("R"));
            if (Cache.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var result = Build(source, target, hex, splitBelowSaturation, highlightColor, brightnessFloor,
                splitBelowValue, pupilColor, passHueCenter, passHueRange, passMinSaturation, splitAboveValue, glintColor,
                glintUMin, glintUMax, glintVMin, glintVMax);
            Cache[key] = result;
            return result;
        }

        private static Texture2D Build(
            Texture source, Color target, string cacheKey, float splitBelowSaturation, Color? highlightColor,
            float brightnessFloor, float splitBelowValue, Color? pupilColor,
            float passHueCenter, float passHueRange, float passMinSaturation,
            float splitAboveValue, Color? glintColor,
            float glintUMin, float glintUMax, float glintVMin, float glintVMax)
        {
            var width = source.width;
            var height = source.height;
            var sourcePixels = ReadPixelsRobust(source, width, height);

            Color.RGBToHSV(target, out var targetH, out var targetS, out var targetV);
            var haveHighlight = highlightColor is Color;
            var highlightH = 0f;
            var highlightS = 0f;
            var highlightV = 0f;
            if (highlightColor is Color hc)
            {
                Color.RGBToHSV(hc, out highlightH, out highlightS, out highlightV);
            }

            var havePupil = pupilColor is Color;
            var pupilH = 0f;
            var pupilS = 0f;
            var pupilV = 0f;
            if (pupilColor is Color pc)
            {
                Color.RGBToHSV(pc, out pupilH, out pupilS, out pupilV);
            }

            // Optional "glint": the very brightest pixels of the desaturated region, split off from
            // the main highlight so the eye's bright mass and its tiny bright glint can be coloured
            // separately (the bat's Eye Color vs Eye Highlight Color).
            var haveGlint = glintColor is Color;
            var glintH = 0f;
            var glintS = 0f;
            var glintV = 0f;
            if (glintColor is Color gc)
            {
                Color.RGBToHSV(gc, out glintH, out glintS, out glintV);
            }

            var outPixels = new Color[sourcePixels.Length];
            for (var i = 0; i < sourcePixels.Length; i++)
            {
                var src = sourcePixels[i];
                Color.RGBToHSV(src, out var sourceH, out var sourceS, out var sourceV);

                // Leave a distinct-hued region (e.g. the bat's pink ears) exactly as it was, so
                // recolouring the body doesn't paint over it and flatten the definition.
                if (passHueCenter >= 0f && sourceS >= passMinSaturation)
                {
                    var hueDistance = Mathf.Abs(sourceH - passHueCenter);
                    hueDistance = Mathf.Min(hueDistance, 1f - hueDistance); // circular on 0..1
                    if (hueDistance <= passHueRange)
                    {
                        outPixels[i] = src;
                        continue;
                    }
                }

                var shade = brightnessFloor + sourceV * (1f - brightnessFloor);

                if (sourceS <= splitBelowSaturation)
                {
                    // Desaturated: either the bright highlight or the dark pupil. Both are fully
                    // desaturated, so only brightness can tell them apart.
                    var isPupil = sourceV <= splitBelowValue;
                    if (isPupil)
                    {
                        if (havePupil)
                        {
                            // A near-black region's own value can't scale a colour into visibility -
                            // use the picked colour's value directly instead of multiplying by
                            // shade, or it would stay black whatever colour was chosen.
                            var recolouredPupil = Color.HSVToRGB(pupilH, pupilS, pupilV);
                            recolouredPupil.a = src.a;
                            outPixels[i] = recolouredPupil;
                        }
                        else
                        {
                            outPixels[i] = src;
                        }
                    }
                    else if (haveGlint && sourceV >= splitAboveValue
                             && InBox(i, width, height, glintUMin, glintUMax, glintVMin, glintVMax))
                    {
                        var recoloredGlint = Color.HSVToRGB(glintH, glintS, glintV * shade);
                        recoloredGlint.a = src.a;
                        outPixels[i] = recoloredGlint;
                    }
                    else if (haveHighlight)
                    {
                        var recoloredHighlight = Color.HSVToRGB(highlightH, highlightS, highlightV * shade);
                        recoloredHighlight.a = src.a;
                        outPixels[i] = recoloredHighlight;
                    }
                    else
                    {
                        outPixels[i] = src;
                    }
                }
                else
                {
                    var recolored = Color.HSVToRGB(targetH, targetS, targetV * shade);
                    recolored.a = src.a;
                    outPixels[i] = recolored;
                }
            }

            var result = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                name = $"FangtasticPalette_Recolor_{cacheKey}",
                wrapMode = source.wrapMode,
                filterMode = source.filterMode,
            };
            result.SetPixels(outPixels);
            result.Apply();
            return result;
        }

        /// <summary>
        /// Palette remap for the bat body, which packs four separately-controllable regions into one
        /// Bat_DIF texture. Each pixel is classified and recoloured toward its region's colour (or
        /// left original when that colour is blank):
        ///   - Rim   : the brightest near-white pixels (value >= rimValue), any hue.
        ///   - Beige : warm-hued skin at or above beigeMinValue.
        ///   - Brown : warm-hued skin below beigeMinValue (the darker rose/brown).
        ///   - Body  : everything else (the navy body/wings), recoloured at bodyFloor.
        /// Rim/beige/brown use floor 0 so their light/dark relationship survives - that's what keeps
        /// the ears and eye-surrounds reading as three tones rather than one flat colour. The body
        /// uses a high floor (its albedo is dark, so it must recolour at full brightness).
        /// "Warm" is a circular hue window (skinHueCenter +/- skinHueRange) at >= skinMinSaturation.
        /// </summary>
        /// <summary>
        /// A rectangular patch of the texture (in UV space) recoloured toward its own colour - the
        /// way features that can't be told apart by colour (fangs, nose, mouth, a face patch) get
        /// their own control. Flat paints the colour solid; otherwise it keeps the source's shading.
        /// </summary>
        internal readonly struct SpatialRegion
        {
            internal SpatialRegion(Color? color, float uMin, float uMax, float vMin, float vMax, bool flat,
                bool alwaysClaim = false, bool ellipse = false, float feather = 0f, float originalBlend = 0f)
            {
                Color = color;
                UMin = uMin;
                UMax = uMax;
                VMin = vMin;
                VMax = vMax;
                Flat = flat;
                AlwaysClaim = alwaysClaim;
                Ellipse = ellipse;
                Feather = feather;
                OriginalBlend = originalBlend;
            }

            internal Color? Color { get; }
            internal float UMin { get; }
            internal float UMax { get; }
            internal float VMin { get; }
            internal float VMax { get; }
            internal bool Flat { get; }

            // When true the box claims its pixels even with no colour set (leaving them ORIGINAL),
            // carving that region out of the later hue/value bands. Used to keep the ears colour off
            // the face: the Face box always claims, so the ears colour never reaches it.
            internal bool AlwaysClaim { get; }

            // When true the region is the ELLIPSE inscribed in its box rather than the full rectangle,
            // so it matches a round feature (the face) without square corners spilling into the skin.
            internal bool Ellipse { get; }

            // Soft edge (0..~0.5): the region fades from full coverage in its centre to 0 at its edge
            // over this fraction, so the boundary is a gradient into the surrounding skin, not a hard
            // cut. Used on the face and body ellipses.
            internal float Feather { get; }

            // Blends the region's recoloured result back toward the original texture by this amount
            // (0 = full recolour, 1 = untouched original) - the "intensity" fade for the face oval,
            // applied before the feather composites the region over the base.
            internal float OriginalBlend { get; }

            internal string Key => Color.HasValue || AlwaysClaim
                ? $"{Color?.ToString() ?? "orig"}:{UMin:R},{UMax:R},{VMin:R},{VMax:R}," +
                  $"{(Flat ? 1 : 0)},{(AlwaysClaim ? 1 : 0)},{(Ellipse ? 1 : 0)},{Feather:R},{OriginalBlend:R}"
                : "-";
        }

        internal static Texture2D GetOrBuildBatBody(
            Texture source, string cacheKey, Color? bodyColor, Color? rimColor, Color? beigeColor, Color? brownColor,
            float bodyFloor, float skinHueCenter, float skinHueRange, float skinMinSaturation,
            float rimValue, float beigeMinValue, float beigeBlendBand, float bodyEdgeSoftness,
            float skinOriginalBlend, SpatialRegion[] regions, bool skinAsBody, float bodyOriginalBlend,
            float earUMin = 0f, float earUMax = 1f, float earVMin = 0f, float earVMax = 1f)
        {
            var regionKey = regions == null ? "-" : string.Join(";", System.Array.ConvertAll(regions, r => r.Key));
            var key = string.Join("|", "batbody", source.GetInstanceID().ToString(),
                bodyColor?.ToString() ?? "-", rimColor?.ToString() ?? "-", beigeColor?.ToString() ?? "-",
                brownColor?.ToString() ?? "-", bodyFloor.ToString("R"),
                skinHueCenter.ToString("R"), skinHueRange.ToString("R"), skinMinSaturation.ToString("R"),
                rimValue.ToString("R"), beigeMinValue.ToString("R"), beigeBlendBand.ToString("R"),
                bodyEdgeSoftness.ToString("R"), skinOriginalBlend.ToString("R"), regionKey,
                skinAsBody ? "1" : "0", bodyOriginalBlend.ToString("R"),
                earUMin.ToString("R"), earUMax.ToString("R"), earVMin.ToString("R"), earVMax.ToString("R"), cacheKey);
            if (Cache.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var width = source.width;
            var height = source.height;
            var sourcePixels = ReadPixelsRobust(source, width, height);
            var outPixels = new Color[sourcePixels.Length];

            for (var i = 0; i < sourcePixels.Length; i++)
            {
                var src = sourcePixels[i];
                Color.RGBToHSV(src, out var h, out var s, out var v);

                // The wings + lower body are one submesh (material 0); the head is the other. On the
                // wing submesh the warm membrane should just be the body colour (one uniform body),
                // not carved into beige/brown - so skinAsBody short-circuits the whole head palette
                // and paints everything the body colour (with the body overlay).
                if (skinAsBody)
                {
                    outPixels[i] = Overlay(Remap(bodyColor, src, v, bodyFloor), src, bodyOriginalBlend);
                    continue;
                }

                // Ear-box confinement (exploration): warm/rim SKIN outside this UV box keeps its
                // original colour, so the ears can be isolated from the eye-surrounds. Full-span
                // [0,1] = every skin pixel is "in", i.e. no confinement. Body pixels are unaffected.
                var uEar = (i % width) / (float)width;
                var vEar = (i / width) / (float)height;
                var inEarBox = uEar >= earUMin && uEar <= earUMax && vEar >= earVMin && vEar <= earVMax;

                // BASE result = the hue/value skin (ears) or the body, as if no spatial region applied.
                Color baseResult;
                if (v >= rimValue)
                {
                    // Bright edges fall back to Beige when no Rim colour is set (they're skin).
                    baseResult = inEarBox
                        ? Overlay(Remap(rimColor ?? beigeColor, src, v, 0f), src, skinOriginalBlend)
                        : src;
                }
                else
                {
                    var hueDistance = Mathf.Abs(h - skinHueCenter);
                    hueDistance = Mathf.Min(hueDistance, 1f - hueDistance);
                    var warmthHue = bodyEdgeSoftness > 0f
                        ? 1f - Mathf.SmoothStep(skinHueRange - bodyEdgeSoftness, skinHueRange + bodyEdgeSoftness, hueDistance)
                        : (hueDistance <= skinHueRange ? 1f : 0f);
                    var warmth = s >= skinMinSaturation ? warmthHue : 0f;

                    if (warmth > 0f)
                    {
                        var beigeResult = Remap(beigeColor, src, v, 0f);
                        var brownResult = Remap(brownColor, src, v, 0f);
                        var t = beigeBlendBand > 0f
                            ? Mathf.SmoothStep(beigeMinValue - beigeBlendBand, beigeMinValue + beigeBlendBand, v)
                            : (v >= beigeMinValue ? 1f : 0f);
                        // Outside the ear box the skin stays original; inside it takes the Ears colour.
                        var skinResult = inEarBox
                            ? Overlay(Color.Lerp(brownResult, beigeResult, t), src, skinOriginalBlend)
                            : src;
                        baseResult = warmth >= 1f
                            ? skinResult
                            : Color.Lerp(Overlay(Remap(bodyColor, src, v, bodyFloor), src, bodyOriginalBlend), skinResult, warmth);
                    }
                    else
                    {
                        baseResult = Overlay(Remap(bodyColor, src, v, bodyFloor), src, bodyOriginalBlend);
                    }
                }

                // Composite the spatial regions ON TOP of the base, each by its coverage. A feathered
                // region (the face) blends from its own colour/original at the centre to the base
                // (ears) at the edge - a soft gradient instead of a hard cut. Hard regions have
                // coverage 1 inside / 0 outside, so they override cleanly. Later regions layer over
                // earlier ones where they overlap.
                var pixelResult = baseResult;
                if (regions != null)
                {
                    for (var r = 0; r < regions.Length; r++)
                    {
                        var reg = regions[r];
                        var cov = Coverage(i, width, height, reg);
                        if (cov <= 0f)
                        {
                            continue;
                        }

                        Color regionResult;
                        if (reg.Color.HasValue)
                        {
                            regionResult = reg.Flat ? reg.Color.Value : Remap(reg.Color, src, v, 0f);
                            regionResult = Overlay(regionResult, src, reg.OriginalBlend);
                        }
                        else if (reg.AlwaysClaim)
                        {
                            regionResult = src; // no colour: keep the original, just carve it out
                        }
                        else
                        {
                            continue; // additive region with no colour
                        }

                        pixelResult = cov >= 1f ? regionResult : Color.Lerp(pixelResult, regionResult, cov);
                    }
                }

                pixelResult.a = src.a;
                outPixels[i] = pixelResult;
            }

            var result = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                name = $"FangtasticPalette_BatBody_{cacheKey}",
                wrapMode = source.wrapMode,
                filterMode = source.filterMode,
            };
            result.SetPixels(outPixels);
            result.Apply();
            Cache[key] = result;
            return result;
        }

        // Whether pixel index i (GetPixels order, bottom-up) falls inside a UV rectangle. Used to
        // confine the eye glint to the small spot in the texture where it's painted, so the highlight
        // stops bleeding onto the other white bits the eye mesh samples.
        private static bool InBox(int i, int width, int height, float uMin, float uMax, float vMin, float vMax)
        {
            var u = (i % width) / (float)width;
            var v = (i / width) / (float)height;
            return u >= uMin && u <= uMax && v >= vMin && v <= vMax;
        }

        // How much a region covers pixel i: 1 fully inside, 0 outside, and a feathered ramp in
        // between for a soft edge. Rectangle regions are 1 inside / 0 outside; ellipse regions use
        // the normalised radial distance (1 on the ellipse edge), fading over reg.Feather.
        private static float Coverage(int i, int width, int height, SpatialRegion reg)
        {
            var u = (i % width) / (float)width;
            var v = (i / width) / (float)height;
            if (u < reg.UMin || u > reg.UMax || v < reg.VMin || v > reg.VMax)
            {
                return 0f;
            }

            if (!reg.Ellipse)
            {
                return 1f;
            }

            var rx = (reg.UMax - reg.UMin) * 0.5f;
            var ry = (reg.VMax - reg.VMin) * 0.5f;
            if (rx <= 0f || ry <= 0f)
            {
                return 1f;
            }

            var dx = (u - (reg.UMin + reg.UMax) * 0.5f) / rx;
            var dy = (v - (reg.VMin + reg.VMax) * 0.5f) / ry;
            var dist = Mathf.Sqrt(dx * dx + dy * dy);

            if (reg.Feather <= 0f)
            {
                return dist <= 1f ? 1f : 0f;
            }

            // Full coverage until (1 - feather) of the way out, ramping smoothly to 0 at the ellipse
            // edge. (GLSL-style smoothstep - NOT Unity's Mathf.SmoothStep, which interpolates between
            // its first two args and would just scale coverage by the feather amount.)
            return 1f - SmoothStep01(1f - reg.Feather, 1f, dist);
        }

        // GLSL smoothstep: 0 below edge0, 1 above edge1, smooth Hermite ramp between.
        private static float SmoothStep01(float edge0, float edge1, float x)
        {
            if (edge1 <= edge0)
            {
                return x < edge0 ? 0f : 1f;
            }

            var t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        // Blends a recoloured pixel back toward the original by `originalBlend` (0 = keep the full
        // recolour, 1 = original untouched), so the source texture's gradients soften the flat tint.
        private static Color Overlay(Color recolored, Color src, float originalBlend)
        {
            if (originalBlend <= 0f)
            {
                return recolored;
            }

            var blended = Color.Lerp(recolored, src, originalBlend);
            blended.a = src.a;
            return blended;
        }

        // HSV colorize one pixel toward target (keeping its own brightness, floored), or return it
        // unchanged when target is blank. Shared by the bat-body region remap.
        private static Color Remap(Color? target, Color src, float sourceV, float floor)
        {
            if (!(target is Color t))
            {
                return src;
            }

            Color.RGBToHSV(t, out var th, out var ts, out var tv);
            var shade = floor + sourceV * (1f - floor);
            var outColor = Color.HSVToRGB(th, ts, tv * shade);
            outColor.a = src.a;
            return outColor;
        }

        // A read path that ignores the source texture's Read/Write flag (all of the game's shipped
        // textures have it off): blit into a temporary RenderTexture and ReadPixels from that.
        // Internal so the probe's PNG export can reuse the exact same read path.
        internal static Color[] ReadPixelsRobust(Texture source, int width, int height)
        {
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            var previousActive = RenderTexture.active;

            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            var readable = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
            readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readable.Apply();

            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(rt);

            var pixels = readable.GetPixels();
            Object.Destroy(readable);
            return pixels;
        }
    }
}
