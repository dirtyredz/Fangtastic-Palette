using System.Globalization;
using UnityEngine;

namespace FangtasticPalette
{
    /// <summary>
    /// Lossless, culture-invariant serialization for the recolour cache keys. Two things a naive
    /// <c>Color.ToString()</c> / <c>float.ToString("R")</c> get wrong for a machine key:
    /// <c>Color.ToString()</c> rounds each component to 3 dp (so two near-identical picked colours
    /// could share a cache entry), and the default <c>"R"</c> format is culture-sensitive (a
    /// decimal-comma locale would emit commas that blur the comma-separated fields in a region key).
    /// Every key field goes through here so identity is exact and stable regardless of locale.
    /// </summary>
    internal static class KeyFormat
    {
        internal static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);

        internal static string Col(Color? c) => c is Color k
            ? F(k.r) + "," + F(k.g) + "," + F(k.b) + "," + F(k.a)
            : "-";
    }
    /// <summary>
    /// Parameter object for <see cref="TextureRecolor.GetOrBuild"/> - the HSV colorize + eye
    /// brightness-split path. Groups the classifier/mask/floor knobs that used to be ~17 loose
    /// parameters into one value type that also OWNS its cache identity via <see cref="Key"/>.
    ///
    /// Owning the key here fixes a latent bug in the old signature: the cache key was a caller-
    /// supplied <c>hex</c> proxy string that did NOT include <c>target</c>, so two calls that
    /// differed only in target colour would have collided. It was safe only because the sole
    /// consumer (the eye path) forces the desaturated branch, where <c>target</c> is never read.
    /// <see cref="Key"/> folds in every field - target included - so the cache is correct by
    /// construction rather than by luck.
    /// </summary>
    internal readonly struct EyeRecolorSpec
    {
        /// <param name="target">
        /// The main (saturated-branch) colour. Only read for pixels ABOVE
        /// <paramref name="splitBelowSaturation"/>; the eye path disables that branch, but the value
        /// is still part of the cache key so any future saturated caller caches correctly.
        /// </param>
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
        /// <param name="brightnessFloor">
        /// Source value is remapped from [0, 1] to [brightnessFloor, 1], then multiplied by the
        /// TARGET colour's own value - so the output is always anchored to how bright the colour
        /// you actually picked is, not an independent brightness computed from the floor alone.
        /// 0 = no floor, full original shading range preserved.
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
        /// <param name="splitAboveValue">
        /// Within the desaturated region, pixels at or above this HSV value are the "glint" (split
        /// off from the main highlight so the eye's bright mass and its tiny bright glint can be
        /// coloured separately). Default 2 disables it (no pixel exceeds value 1).
        /// </param>
        /// <param name="glintColor">
        /// What the glint region is remapped toward. Null leaves those pixels as the highlight.
        /// </param>
        internal EyeRecolorSpec(
            Color target, float splitBelowSaturation = -1f, Color? highlightColor = null,
            float brightnessFloor = 0f, float splitBelowValue = -1f, Color? pupilColor = null,
            float passHueCenter = -1f, float passHueRange = 0f, float passMinSaturation = 0f,
            float splitAboveValue = 2f, Color? glintColor = null,
            float glintUMin = 0f, float glintUMax = 1f, float glintVMin = 0f, float glintVMax = 1f)
        {
            Target = target;
            SplitBelowSaturation = splitBelowSaturation;
            HighlightColor = highlightColor;
            BrightnessFloor = brightnessFloor;
            SplitBelowValue = splitBelowValue;
            PupilColor = pupilColor;
            PassHueCenter = passHueCenter;
            PassHueRange = passHueRange;
            PassMinSaturation = passMinSaturation;
            SplitAboveValue = splitAboveValue;
            GlintColor = glintColor;
            GlintUMin = glintUMin;
            GlintUMax = glintUMax;
            GlintVMin = glintVMin;
            GlintVMax = glintVMax;
        }

        internal Color Target { get; }
        internal float SplitBelowSaturation { get; }
        internal Color? HighlightColor { get; }
        internal float BrightnessFloor { get; }
        internal float SplitBelowValue { get; }
        internal Color? PupilColor { get; }
        internal float PassHueCenter { get; }
        internal float PassHueRange { get; }
        internal float PassMinSaturation { get; }
        internal float SplitAboveValue { get; }
        internal Color? GlintColor { get; }
        internal float GlintUMin { get; }
        internal float GlintUMax { get; }
        internal float GlintVMin { get; }
        internal float GlintVMax { get; }

        // Cache identity for everything EXCEPT the source texture (the engine folds in the source's
        // instance id separately). Folds in every field, target included, so the cache never
        // collides two distinct recolours - the correctness fix this value type exists for.
        internal string Key => string.Join("|",
            KeyFormat.Col(Target), KeyFormat.F(SplitBelowSaturation),
            KeyFormat.Col(HighlightColor), KeyFormat.F(BrightnessFloor), KeyFormat.F(SplitBelowValue),
            KeyFormat.Col(PupilColor), KeyFormat.F(PassHueCenter), KeyFormat.F(PassHueRange),
            KeyFormat.F(PassMinSaturation), KeyFormat.F(SplitAboveValue), KeyFormat.Col(GlintColor),
            KeyFormat.F(GlintUMin), KeyFormat.F(GlintUMax), KeyFormat.F(GlintVMin), KeyFormat.F(GlintVMax));
    }

    /// <summary>
    /// A rectangular patch of the texture (in UV space) recoloured toward its own colour - the
    /// way features that can't be told apart by colour (fangs, nose, mouth, a face patch) get
    /// their own control. Flat paints the colour solid; otherwise it keeps the source's shading.
    /// Bat-body-specific: only <see cref="SkinPaletteSpec"/> / <see cref="TextureRecolor.GetOrBuildBatBody"/>
    /// use it, so it lives here with the spec rather than in the (to-be-generic) engine file.
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
            ? $"{(Color.HasValue ? KeyFormat.Col(Color) : "orig")}:" +
              $"{KeyFormat.F(UMin)},{KeyFormat.F(UMax)},{KeyFormat.F(VMin)},{KeyFormat.F(VMax)}," +
              $"{(Flat ? 1 : 0)},{(AlwaysClaim ? 1 : 0)},{(Ellipse ? 1 : 0)},{KeyFormat.F(Feather)},{KeyFormat.F(OriginalBlend)}"
            : "-";
    }

    /// <summary>
    /// Parameter object for <see cref="TextureRecolor.GetOrBuildBatBody"/> - the four-region bat-body
    /// palette remap (rim / beige / brown / body) plus its spatial regions and ear-box confinement.
    /// Replaces ~22 loose parameters AND the redundant caller-built <c>cacheDiscriminator</c>: the
    /// discriminator just restated fields the spec already carries, so <see cref="Key"/> derives the
    /// cache identity directly from the real inputs.
    /// </summary>
    internal readonly struct SkinPaletteSpec
    {
        /// <param name="regions">
        /// Rectangular/ellipse UV patches composited on top of the base palette (fangs, nose, mouth,
        /// face). May be null.
        /// </param>
        /// <param name="skinAsBody">
        /// On the wing submesh the warm membrane is just the body colour (one uniform body), so this
        /// short-circuits the head palette and paints everything the body colour.
        /// </param>
        internal SkinPaletteSpec(
            Color? bodyColor, Color? rimColor, Color? beigeColor, Color? brownColor,
            float bodyFloor, float skinHueCenter, float skinHueRange, float skinMinSaturation,
            float rimValue, float beigeMinValue, float beigeBlendBand, float bodyEdgeSoftness,
            float skinOriginalBlend, SpatialRegion[] regions, bool skinAsBody,
            float bodyOriginalBlend,
            float earUMin = 0f, float earUMax = 1f, float earVMin = 0f, float earVMax = 1f)
        {
            BodyColor = bodyColor;
            RimColor = rimColor;
            BeigeColor = beigeColor;
            BrownColor = brownColor;
            BodyFloor = bodyFloor;
            SkinHueCenter = skinHueCenter;
            SkinHueRange = skinHueRange;
            SkinMinSaturation = skinMinSaturation;
            RimValue = rimValue;
            BeigeMinValue = beigeMinValue;
            BeigeBlendBand = beigeBlendBand;
            BodyEdgeSoftness = bodyEdgeSoftness;
            SkinOriginalBlend = skinOriginalBlend;
            Regions = regions;
            SkinAsBody = skinAsBody;
            BodyOriginalBlend = bodyOriginalBlend;
            EarUMin = earUMin;
            EarUMax = earUMax;
            EarVMin = earVMin;
            EarVMax = earVMax;
        }

        internal Color? BodyColor { get; }
        internal Color? RimColor { get; }
        internal Color? BeigeColor { get; }
        internal Color? BrownColor { get; }
        internal float BodyFloor { get; }
        internal float SkinHueCenter { get; }
        internal float SkinHueRange { get; }
        internal float SkinMinSaturation { get; }
        internal float RimValue { get; }
        internal float BeigeMinValue { get; }
        internal float BeigeBlendBand { get; }
        internal float BodyEdgeSoftness { get; }
        internal float SkinOriginalBlend { get; }
        internal SpatialRegion[] Regions { get; }
        internal bool SkinAsBody { get; }
        internal float BodyOriginalBlend { get; }
        internal float EarUMin { get; }
        internal float EarUMax { get; }
        internal float EarVMin { get; }
        internal float EarVMax { get; }

        // Cache identity for everything EXCEPT the source texture. Folds in each region's own Key,
        // so a region colour/geometry change produces a distinct entry.
        internal string Key
        {
            get
            {
                var regionKey = Regions == null
                    ? "-"
                    : string.Join(";", System.Array.ConvertAll(Regions, r => r.Key));
                return string.Join("|",
                    KeyFormat.Col(BodyColor), KeyFormat.Col(RimColor), KeyFormat.Col(BeigeColor),
                    KeyFormat.Col(BrownColor), KeyFormat.F(BodyFloor),
                    KeyFormat.F(SkinHueCenter), KeyFormat.F(SkinHueRange), KeyFormat.F(SkinMinSaturation),
                    KeyFormat.F(RimValue), KeyFormat.F(BeigeMinValue), KeyFormat.F(BeigeBlendBand),
                    KeyFormat.F(BodyEdgeSoftness), KeyFormat.F(SkinOriginalBlend), regionKey,
                    SkinAsBody ? "1" : "0", KeyFormat.F(BodyOriginalBlend),
                    KeyFormat.F(EarUMin), KeyFormat.F(EarUMax), KeyFormat.F(EarVMin), KeyFormat.F(EarVMax));
            }
        }
    }
}
