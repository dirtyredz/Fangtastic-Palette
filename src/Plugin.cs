using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace FangtasticPalette
{
    /// <summary>
    /// Fangtastic Palette recolours Bat Form - the second entry in the "...tastic Palette" set,
    /// after mods/PurrtasticPalette (Cat Form, shipped) and before the planned Fintastic Palette
    /// (Aqua/mermaid). The recolour machinery is the one hard-won on the cat and written up at
    /// 16-recolouring-characters.md; none of it is rediscovered here.
    ///
    /// The Bat body is one atlas Bat_DIF on one URP/Lit "Bat" material driving the whole creature,
    /// the eyes are separate renderers, and VFXBatWingDust is the dust aura. See BatColorPatch for
    /// the routing. (This was mapped with a probe/debug harness during development, stripped for
    /// release exactly as the cat's was.)
    ///
    /// Colours live under a "Colors" section that Mod Nook renders as a menu; each is a hex code
    /// ("#8800FF") or an HTML colour name ("purple"), blank = leave vanilla. Changes apply
    /// immediately via Config.SettingChanged, the same live UX as the cat.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("Moonlight Peaks.exe")]
    public sealed class FangtasticPalettePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.dirtyredz.moonlightpeaks.fangtasticpalette";
        public const string PluginName = "Fangtastic Palette";
        public const string PluginVersion = ModBuildInfo.Version;

        internal static ManualLogSource Log;

        // --- Colours (Mod Nook renders these; see mods/FormLock for the ModMenu.* tag convention) ---
        internal static ConfigEntry<string> BodyColor;
        internal static ConfigEntry<string> EarsColor;
        internal static ConfigEntry<string> FangColor;
        internal static ConfigEntry<string> MouthColor;
        internal static ConfigEntry<string> FaceColor;
        internal static ConfigEntry<string> EyeColor;
        internal static ConfigEntry<string> PupilColor;
        internal static ConfigEntry<string> EyeHighlightColor;
        internal static ConfigEntry<string> WingDustColor;
        internal static ConfigEntry<float> WingDustStrength;
        internal static ConfigEntry<float> EarIntensity;
        internal static ConfigEntry<float> BodyIntensity;
        internal static ConfigEntry<float> FaceIntensity;
        internal static ConfigEntry<float> MouthIntensity;
        internal static ConfigEntry<bool> FreezePreviewFlap;
        internal static ConfigEntry<bool> VerboseLogging;

        private const string ColorsSection = "ModMenu.Section=Colors";

        private Harmony harmony;

        private void Awake()
        {
            Log = Logger;

            BodyColor = Config.Bind(
                "Colors", "BodyColor", "",
                new ConfigDescription(
                    "Colour for the bat's body and wings. Use a hex code (\"#8800FF\") or a colour " +
                    "name (\"purple\"). Leave blank to keep the default dark navy.",
                    null,
                    ColorsSection, "ModMenu.Label=Body Color"));

            EarsColor = Config.Bind(
                "Colors", "EarsColor", "",
                new ConfigDescription(
                    "Colour for the ears (and the rings around the eyes). One colour for the whole " +
                    "skin - use the Ear Intensity slider to let the original shading show through. " +
                    "Leave blank to keep it vanilla. The face is separate (see Face Color).",
                    null,
                    ColorsSection, "ModMenu.Label=Ears Color"));

            FangColor = Config.Bind(
                "Colors", "FangColor", "#FFFFFF",
                new ConfigDescription(
                    "Colour for the fangs (the white teeth). Defaults to white so the fangs always " +
                    "stay fang-coloured and the Mouth colour can't bleed onto them (the fang box sits " +
                    "inside the mouth area). Pick another colour, or the Default swatch to leave the " +
                    "fangs vanilla.",
                    null,
                    ColorsSection, "ModMenu.Label=Fang Color"));

            MouthColor = Config.Bind(
                "Colors", "MouthColor", "",
                new ConfigDescription(
                    "Colour for the whole mouth area (nose + mouth, combined into one). Flat solid " +
                    "colour; use Mouth Intensity to fade it toward the original. Leave blank for vanilla.",
                    null,
                    ColorsSection, "ModMenu.Label=Mouth Color"));

            FaceColor = Config.Bind(
                "Colors", "FaceColor", "#FF8000",
                new ConfigDescription(
                    "Colour for the face - a soft-edged oval over the muzzle/cheeks that fades into " +
                    "the surrounding skin (the banded face look). Defaults to a warm orange; pick any " +
                    "colour to recolour it, or use the Default swatch to leave the face vanilla.",
                    null,
                    ColorsSection, "ModMenu.Label=Face Color"));

            EyeColor = Config.Bind(
                "Colors", "EyeColor", "",
                new ConfigDescription(
                    "Colour for the eyes. Leave blank to keep the default. The pupil and the bright " +
                    "glint have their own settings below.",
                    null,
                    ColorsSection, "ModMenu.Label=Eye Color"));

            PupilColor = Config.Bind(
                "Colors", "PupilColor", "",
                new ConfigDescription(
                    "Colour for the pupil - the dark centre of the eye. Leave blank to keep the " +
                    "default. Needs Eye Color set to have any effect.",
                    null,
                    ColorsSection, "ModMenu.Label=Pupil Color"));

            EyeHighlightColor = Config.Bind(
                "Colors", "EyeHighlightColor", "",
                new ConfigDescription(
                    "Colour for the small bright glint on the eye. Leave blank to keep the default " +
                    "white. Needs Eye Color set to have any effect.",
                    null,
                    ColorsSection, "ModMenu.Label=Eye Highlight Color"));

            WingDustColor = Config.Bind(
                "Colors", "WingDustColor", "",
                new ConfigDescription(
                    "Colour for the dust that trails off the wings in flight. Leave blank to keep " +
                    "the default. Only visible while moving.",
                    null,
                    ColorsSection, "ModMenu.Label=Wing Dust Color"));

            WingDustStrength = Config.Bind(
                "Colors", "WingDustStrength", 4f,
                new ConfigDescription(
                    "How much brighter to make the wing dust when Wing Dust Color is set. 1 = the " +
                    "game's own faint intensity; higher is easier to see. No effect while Wing Dust " +
                    "Color is blank.",
                    new AcceptableValueRange<float>(1f, 6f),
                    ColorsSection, "ModMenu.Label=Wing Dust Strength"));

            EarIntensity = Config.Bind(
                "Colors", "EarIntensity", 0.95f,
                new ConfigDescription(
                    "How strong the Ears colour is. 1 = full flat colour; lower fades it toward the " +
                    "original texture so the skin's own shading shows through. Only affects the Ears " +
                    "colour, not Body, Face, Eyes or Fangs.",
                    new AcceptableValueRange<float>(0f, 1f),
                    ColorsSection, "ModMenu.Label=Ear Intensity"));

            BodyIntensity = Config.Bind(
                "Colors", "BodyIntensity", 0.7f,
                new ConfigDescription(
                    "How strong the Body colour is. 1 = full colour blast (the flat vivid look); " +
                    "lower fades it toward the original texture so the body's own shading shows " +
                    "through. Affects the whole body and wings.",
                    new AcceptableValueRange<float>(0f, 1f),
                    ColorsSection, "ModMenu.Label=Body Intensity"));

            FaceIntensity = Config.Bind(
                "Colors", "FaceIntensity", 0.8f,
                new ConfigDescription(
                    "How strong the Face colour is. 1 = full flat colour; lower fades it toward the " +
                    "original texture so the face's own shading shows through. Only affects the Face " +
                    "oval (its soft edge is fixed).",
                    new AcceptableValueRange<float>(0f, 1f),
                    ColorsSection, "ModMenu.Label=Face Intensity"));

            MouthIntensity = Config.Bind(
                "Colors", "MouthIntensity", 0.4f,
                new ConfigDescription(
                    "How strong the Mouth colour is. 1 = full flat colour; lower fades it toward the " +
                    "original texture so the mouth's own shading shows through. Only affects the Mouth " +
                    "area.",
                    new AcceptableValueRange<float>(0f, 1f),
                    ColorsSection, "ModMenu.Label=Mouth Intensity"));

            FreezePreviewFlap = Config.Bind(
                "Colors", "FreezePreviewFlap", false,
                new ConfigDescription(
                    "Hold the bat still in the wardrobe preview instead of letting it flap, so " +
                    "colours are easier to judge. Only affects the mirror preview, not the game.",
                    null,
                    ColorsSection, "ModMenu.Label=Freeze Preview Flap"));

            VerboseLogging = Config.Bind(
                "Colors", "VerboseLogging", false,
                new ConfigDescription(
                    "Write details of every colour change to the BepInEx log. Only useful when " +
                    "reporting a problem.",
                    null,
                    ColorsSection, "ModMenu.Label=Verbose Logging"));

            // Live-apply: Mod Nook (and hand-editing the .cfg while the game runs) both change a
            // ConfigEntry's .Value and raise this - reapply immediately rather than requiring a
            // re-equip, matching the cat and Serena's Enchanted Studio.
            Config.SettingChanged += (_, _) =>
            {
                BatColorPatch.ApplyBatColors();
                BatFormWardrobe.ApplyFlapFreeze();
            };

            gameObject.AddComponent<BatColorReapplier>();

            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(BatColorPatch));
            harmony.PatchAll(typeof(BatFormWardrobe));

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Set the Colors settings in Mod Nook " +
                        $"(or the .cfg) to recolour Bat Form; changes apply immediately.");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }
}
