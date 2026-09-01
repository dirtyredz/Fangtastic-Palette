using System;
using BepInEx.Configuration;
using UnityEngine;

namespace FangtasticPalette
{
    /// <summary>
    /// Single source of truth for the bat's colour set. The config values themselves are still bound
    /// (with their full descriptions / Mod Nook tags) in <see cref="FangtasticPalettePlugin"/>; this
    /// owns the CANONICAL ORDERED LIST of which colours exist plus their panel metadata, and produces
    /// the immutable <see cref="BatPaletteValues"/> snapshot the recolour engine consumes.
    ///
    /// Everything that needs "the colour set" derives from <see cref="Colors"/> - the wardrobe panel's
    /// swatch rows and the wardrobe's revert-on-cancel list (<see cref="ManagedColors"/>) - so the two
    /// can no longer drift (adding a colour here reaches both). The engine reads a snapshot rather than
    /// BepInEx statics, so its recolour maths doesn't depend on the config layer.
    /// </summary>
    internal static class BatPalette
    {
        // Canonical ordered colour set: label, the bound ConfigEntry (via a lazy accessor - the entries
        // exist only after Plugin.Awake binds them), and the panel's "Default" swatch colour (the
        // vanilla tone sampled from Bat_DIF, shown so a blank value reads as "keep vanilla").
        internal static readonly (string Label, Func<ConfigEntry<string>> Setting, Color DefaultColor)[] Colors =
        {
            ("Body", () => FangtasticPalettePlugin.BodyColor, new Color32(0x22, 0x30, 0x49, 0xFF)),
            ("Ears", () => FangtasticPalettePlugin.EarsColor, new Color32(0xB3, 0x98, 0x8C, 0xFF)),
            ("Fangs", () => FangtasticPalettePlugin.FangColor, new Color32(0xFF, 0xFF, 0xFF, 0xFF)),
            ("Mouth", () => FangtasticPalettePlugin.MouthColor, new Color32(0x9A, 0x5A, 0x60, 0xFF)),
            ("Face", () => FangtasticPalettePlugin.FaceColor, new Color32(0xC8, 0x9A, 0x8C, 0xFF)),
            ("Eyes", () => FangtasticPalettePlugin.EyeColor, new Color32(0xC9, 0xB6, 0xE8, 0xFF)),
            ("Pupil", () => FangtasticPalettePlugin.PupilColor, new Color32(0x14, 0x10, 0x1A, 0xFF)),
            ("Eye Highlight", () => FangtasticPalettePlugin.EyeHighlightColor, new Color32(0xFF, 0xFF, 0xFF, 0xFF)),
            ("Wing Dust", () => FangtasticPalettePlugin.WingDustColor, new Color32(0xED, 0xED, 0xED, 0xFF)),
        };

        // The colour ConfigEntries managed for wardrobe revert-on-cancel, derived from Colors so it
        // can never fall out of sync with the panel's rows.
        internal static ConfigEntry<string>[] ManagedColors()
        {
            var entries = new ConfigEntry<string>[Colors.Length];
            for (var i = 0; i < Colors.Length; i++)
            {
                entries[i] = Colors[i].Setting();
            }

            return entries;
        }

        // Immutable snapshot of every palette value the recolour engine reads. Built at the apply
        // boundary (BatColorPatch.ApplyBatColors / the wardrobe preview) so the engine's routing and
        // pixel maths take values, not BepInEx statics.
        internal static BatPaletteValues Snapshot() => new BatPaletteValues(
            bodyColor: FangtasticPalettePlugin.BodyColor.Value,
            earsColor: FangtasticPalettePlugin.EarsColor.Value,
            fangColor: FangtasticPalettePlugin.FangColor.Value,
            mouthColor: FangtasticPalettePlugin.MouthColor.Value,
            faceColor: FangtasticPalettePlugin.FaceColor.Value,
            eyeColor: FangtasticPalettePlugin.EyeColor.Value,
            pupilColor: FangtasticPalettePlugin.PupilColor.Value,
            eyeHighlightColor: FangtasticPalettePlugin.EyeHighlightColor.Value,
            wingDustColor: FangtasticPalettePlugin.WingDustColor.Value,
            bodyIntensity: FangtasticPalettePlugin.BodyIntensity.Value,
            earIntensity: FangtasticPalettePlugin.EarIntensity.Value,
            faceIntensity: FangtasticPalettePlugin.FaceIntensity.Value,
            mouthIntensity: FangtasticPalettePlugin.MouthIntensity.Value,
            wingDustStrength: FangtasticPalettePlugin.WingDustStrength.Value);
    }

    /// <summary>
    /// Immutable snapshot of the palette-affecting config values, taken once at the apply boundary and
    /// threaded through the recolour engine so its maths never reads BepInEx statics directly. Colour
    /// fields are the raw config strings (hex or HTML name, blank = leave vanilla); the engine parses
    /// them where it needs a <see cref="Color"/>.
    /// </summary>
    internal readonly struct BatPaletteValues
    {
        internal BatPaletteValues(
            string bodyColor, string earsColor, string fangColor, string mouthColor, string faceColor,
            string eyeColor, string pupilColor, string eyeHighlightColor, string wingDustColor,
            float bodyIntensity, float earIntensity, float faceIntensity, float mouthIntensity,
            float wingDustStrength)
        {
            BodyColor = bodyColor;
            EarsColor = earsColor;
            FangColor = fangColor;
            MouthColor = mouthColor;
            FaceColor = faceColor;
            EyeColor = eyeColor;
            PupilColor = pupilColor;
            EyeHighlightColor = eyeHighlightColor;
            WingDustColor = wingDustColor;
            BodyIntensity = bodyIntensity;
            EarIntensity = earIntensity;
            FaceIntensity = faceIntensity;
            MouthIntensity = mouthIntensity;
            WingDustStrength = wingDustStrength;
        }

        internal string BodyColor { get; }
        internal string EarsColor { get; }
        internal string FangColor { get; }
        internal string MouthColor { get; }
        internal string FaceColor { get; }
        internal string EyeColor { get; }
        internal string PupilColor { get; }
        internal string EyeHighlightColor { get; }
        internal string WingDustColor { get; }
        internal float BodyIntensity { get; }
        internal float EarIntensity { get; }
        internal float FaceIntensity { get; }
        internal float MouthIntensity { get; }
        internal float WingDustStrength { get; }
    }
}
