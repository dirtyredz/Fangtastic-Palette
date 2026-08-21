using System.Collections.Generic;
using Chicken.Utilities;
using HarmonyLib;
using UnityEngine;

namespace FangtasticPalette
{
    /// <summary>
    /// Applies the Colors settings to the Bat Form body whenever it becomes visible. Patched on
    /// FormToolView&lt;BatToolAsset&gt;.HandleEnterVisuallySwitchedBodyViews - the closed generic,
    /// which scopes this to Bat Form only (Cat/Aqua get their own closed generics), and the first
    /// moment the async-loaded body is fully live. Exactly the hook the cat mod established; see
    /// 16-recolouring-characters.md §7.
    ///
    /// The bat body is simpler than the cat's: one hand-painted atlas (Bat_DIF) on one material
    /// (Bat, URP/Lit) drives the whole creature, and the eyes are separate renderers sharing that
    /// material. So parts are routed by RENDERER name, not material name:
    ///   - Bat_Body            -> a 4-region palette remap of Bat_DIF (see GetOrBuildBatBody):
    ///                            navy Body + the warm skin's Rim / Beige / Brown tones, each its
    ///                            own colour, so the ears and eye-surrounds keep their definition
    ///                            instead of flattening to one hue.
    ///   - Bat_lEye / Bat_rEye -> Eye / Pupil / Eye-Highlight, split by brightness on their own
    ///                            renderers (the eye mesh samples only the eye region of Bat_DIF).
    ///   - VFXBatWingDust      -> WingDustColor (a particle Color property, HSV-shifted, no regen),
    ///                            brightened by the Wing Dust Strength slider.
    ///
    /// Unlike the cat body, the probe reported HasPropertyBlock=FALSE on Bat_Body and the eyes -
    /// the game is not overriding these with a MaterialPropertyBlock, so a plain material write
    /// renders. The block is still written defensively (harmless, and correct if a block ever
    /// appears), the same read-modify-write the cat used.
    ///
    /// Still to do: the beige FANGS (low-saturation, not separable by the warm-hue skin gate).
    /// </summary>
    [HarmonyPatch(typeof(FormToolView<BatToolAsset>), "HandleEnterVisuallySwitchedBodyViews")]
    internal static class BatColorPatch
    {
        private const string BodyRendererName = "Bat_Body";
        private const string LeftEyeRendererName = "Bat_lEye";
        private const string RightEyeRendererName = "Bat_rEye";
        private const string WingDustRendererName = "VFXBatWingDust";

        // The Bat material carries two albedo slots at once - _BaseMap/_BaseColor (URP/Lit's own)
        // and _MainTex/_Color (Standard-shader legacy, still present because the material was
        // converted rather than authored fresh). The probe confirmed both point at Bat_DIF, so both
        // are regenerated or the untouched slot keeps showing the original texture. Same quirk the
        // cat's HellKitten01 had.
        private static readonly (string TexProperty, string TintProperty)[] BodyTexturePairs =
        {
            ("_BaseMap", "_BaseColor"),
            ("_MainTex", "_Color"),
        };

        // Tunables describing THIS character's art, not user preferences - the cat's equivalents
        // are documented at length in CatColorPatch. Starting values are the cat's; adjust once the
        // bat is seen in-game.
        //
        // BodyBrightnessFloor 1: CONFIRMED in-game - floor 0 (preserve source brightness) just
        // painted a dark shade of the target that read as shadow over the dark body and never
        // matched the picked colour, the exact symptom the cat hit before ITS breakthrough. The bat
        // albedo is dark like the cat's, and its shading comes from real-time lighting + the mesh,
        // not the albedo - so recolour at full brightness (discard the source's darkness) and the
        // colour lands vividly. Same value, same reason, as the cat's FurBrightnessFloor.
        private const float BodyBrightnessFloor = 1f;

        // The ears and eye-surrounds are one warm "skin" cluster in Bat_DIF, distinct from the navy
        // body, with three tones the player controls separately (Rim / Beige / Brown). A cluster
        // analysis of the exported texture placed them: navy body ~200 deg; warm skin ~320 deg
        // wrapping to ~30 deg (mauve #845158 / #B3868E and beige #B39E8C); near-white rim
        // #FBDFDE/#EFECEA at value ~0.94+. So the skin is a circular hue window centred ~355 deg,
        // +/-65 deg, at >= 0.15 saturation; within it, value >= 0.6 is Beige and below is Brown; and
        // the brightest pixels (value >= 0.8), any hue, are the Rim. These describe this texture's
        // art, not user taste - tune them here if a tone bleeds. Passed to TextureRecolor in 0..1
        // hue units. (Beige here is the ear/face beige; the low-saturation fangs are a later pass.)
        private const float SkinHueCenter = 355f / 360f;
        private const float SkinHueRange = 65f / 360f;
        private const float SkinMinSaturation = 0.15f;

        // RimValue 0.72 (was 0.80): a spatial check of the exported texture showed the visible rim
        // edge sits in the 0.72-0.80 band and was being swept into Beige, while the >=0.80 pixels are
        // the eye/fang whites (on the separate eye renderer). 0.72 hands the real rim to Rim Color.
        private const float RimValue = 0.72f;
        private const float BeigeMinValue = 0.60f;

        // Cross-fade width (in brightness) between Beige and Brown. 0 = a clean hard split at
        // BeigeMinValue, which is what reads best: a non-zero band was tried and rejected in-game -
        // it muddied the two tones into a wash rather than a crisp boundary.
        private const float BeigeBlendBand = 0f;

        // Spatial regions (fangs / mouth / face) can't be separated from the ear/eye highlights by
        // colour - same head material, same tones - so each is picked by LOCATION: a box in the
        // texture's UV space over the feature. GetPixels is bottom-up, so v = 1 - (top-down y)/height.
        // (u,v) = (uMin, uMax, vMin, vMax). Mouth is a flat rectangle (Mouth Intensity fades it), Fang
        // is a shaded rectangle, and Face is a feathered ELLIPSE (carves the ears colour off the face;
        // see the region build below).
        private static readonly (float UMin, float UMax, float VMin, float VMax) FangBox = (0.22f, 0.37f, 0.04f, 0.20f);
        private static readonly (float UMin, float UMax, float VMin, float VMax) FaceBox = (0.4888f, 0.7562f, 0.1941f, 0.5000f);

        // Combined nose+mouth ("Mouth") region - one colour + one intensity covers the whole mouth
        // area instead of two separate swatches. Dialed in live in the mirror, then baked. It overlaps
        // the fang box, so FangColor defaults to white and its region always claims the fangs on top.
        private static readonly (float UMin, float UMax, float VMin, float VMax) MouthBox = (0.0544f, 0.8402f, 0f, 0.1746f);

        // How soft the face oval's edge is - baked in (dialed live in the mirror). The face fades
        // into the ears/original over this fraction of the ellipse radius instead of a hard line;
        // it smooths the artifacting around the eyes. Not user-facing (Face Intensity is the knob).
        private const float FaceSoftness = 0.284f;

        // CONFINES the Ears colour to just this UV box of the head atlas - warm skin outside it is
        // left original, isolating the ears from the eye-surrounds. Dialed in live in the mirror:
        // only U Min was pulled off 0 (to ~0.766), so the ears take colour on the right of the atlas
        // and the eye-surround skin stays vanilla. Full-span [0,1]x[0,1] would mean no confinement.
        private static readonly (float UMin, float UMax, float VMin, float VMax) EarBox = (0.7663f, 1f, 0f, 1f);

        // Softens the hue boundary between the warm skin and the body. TRIED and REVERTED to 0: the
        // body/chest and wing share material 0 and the membrane's red->blue transition runs through
        // the same hues as the body, so any blend leaked Brown onto the body itself. The membrane
        // stays a flat patch; a true gradient isn't reachable without separating those meshes.
        private const float BodyEdgeSoftness = 0f;

        // The bat eye is a bright, DESATURATED blob (the white dots in Bat_DIF), not a saturated
        // iris like the cat's - confirmed in-game by Eye Highlight recolouring it while Eye Color
        // (which only touched saturated pixels) did nothing. So the eye is recoloured by routing
        // every eye pixel through the "desaturated" branch (see ApplyEyeColor): the bright mass
        // takes EyeColor at full brightness (floor 1, same fix as the body), and pixels darker than
        // this value are the pupil. The split runs ALWAYS, not just when PupilColor is set - that's
        // what keeps the pupil dark and defined by default (blank PupilColor = leave it original)
        // instead of Eye Color swallowing the whole eye, which was the reported symptom.
        private const float EyePupilValue = 0.45f;

        // Above this brightness a desaturated eye pixel is the bright glint (Eye Highlight Color)
        // rather than the eye's main mass (Eye Color). CALIBRATED from a value histogram of the
        // eye's bright pixels: they are overwhelmingly 0.95-1.0 (4120 px) with almost nothing in
        // 0.80-0.95 (~350 px) - the eye is a dark pupil plus a near-white mass, no gradient between.
        // So Eye Color must own that whole mass, and the highlight is only the pure-white specular:
        // 0.99. If Eye Highlight is left blank, the mass just goes to Eye Color (glint disabled),
        // which is the normal case. Nudge down slightly if a set Eye Highlight catches nothing.
        private const float EyeHighlightValue = 0.99f;

        // The eye glint bleeds because the highlight (pure-white) catches every white spot the eye
        // mesh samples, not just the central glint. Same fix as the fangs: confine the glint to the
        // texture patch where it's painted. Estimated from the exported Bat_DIF (eye-white dots at
        // ~x[185-280], y[482-507] of 512; v is bottom-up so v = 1 - y/512).
        private const float EyeGlintUMin = 0.32f;
        private const float EyeGlintUMax = 0.60f;
        private const float EyeGlintVMin = 0.00f;
        private const float EyeGlintVMax = 0.12f;

        // Particle Color properties on the wing-dust material (ParticleSmoke2x2Glow). _Color is HDR
        // (probe read 2.297) and _EmissionColor drives the additive glow; both get the hue/sat of
        // the target while keeping their own (often >1) brightness.
        private static readonly string[] WingDustColorProperties = { "_Color", "_EmissionColor" };

        // The vanilla dust is very faint. When a Wing Dust Color is set, its brightness is
        // multiplied by the Wing Dust Strength slider (FangtasticPalettePlugin.WingDustStrength) so
        // the recolour is visible in flight - applied to both the ParticleSystem startColor and the
        // material's HDR colour/emission. Blank colour leaves the dust at its vanilla intensity.

        // Original values, captured the first time a material+property is overridden, so clearing a
        // setting back to blank restores the real original instead of leaving the last override in
        // place. Keyed by the live Material instance - a fresh one exists after every body swap
        // (EntityCustomization.SetBodyView re-instantiates the prefab), so this never needs
        // explicit eviction.
        private static readonly Dictionary<(Material Material, string Property), Texture> OriginalTextures =
            new Dictionary<(Material, string), Texture>();
        private static readonly Dictionary<(Material Material, string Property), Color> OriginalTints =
            new Dictionary<(Material, string), Color>();
        private static readonly Dictionary<(Material Material, string Property), Color> OriginalWingDust =
            new Dictionary<(Material, string), Color>();

        // A particle system commonly drives its colour through the ParticleSystem module's
        // startColor, which is multiplied on top of the material colour - so tinting the material
        // alone can look like nothing changed. Captured for restore, keyed by the live system.
        private static readonly Dictionary<ParticleSystem, ParticleSystem.MinMaxGradient> OriginalWingDustStart =
            new Dictionary<ParticleSystem, ParticleSystem.MinMaxGradient>();

        private static bool suppressLogging;

        [HarmonyPostfix]
        private static void Postfix()
        {
            ApplyBatColors();
        }

        internal static void ApplyBatColors(bool logVerbose = true, bool includeEyes = true)
        {
            suppressLogging = !logVerbose;

            if (!MonoBehaviourSingleton<PlayerView>.Exists)
            {
                Debug("No PlayerView yet - skipping.");
                return;
            }

            var bodyView = MonoBehaviourSingleton<PlayerView>.Instance.Customization.BodyView;
            if (bodyView == null)
            {
                Debug("Customization.BodyView is null - skipping.");
                return;
            }

            ApplyToBody(bodyView, logVerbose, includeEyes);
        }

        /// <summary>
        /// Recolours any Bat Form body, not just the live player's - a wardrobe preview would
        /// instantiate its own copy with its own material instances, so it needs applying to
        /// explicitly. (No wardrobe tab yet; this signature is ready for one, matching the cat.)
        /// </summary>
        internal static void ApplyToBody(BodyViewAsset bodyView, bool logVerbose = true, bool includeEyes = true)
        {
            if (bodyView == null)
            {
                return;
            }

            suppressLogging = !logVerbose;

            var bodyColor = FangtasticPalettePlugin.BodyColor.Value;
            var earsColor = FangtasticPalettePlugin.EarsColor.Value;
            var fangColor = FangtasticPalettePlugin.FangColor.Value;
            var mouthColor = FangtasticPalettePlugin.MouthColor.Value;
            var faceColor = FangtasticPalettePlugin.FaceColor.Value;
            var eyeColor = FangtasticPalettePlugin.EyeColor.Value;
            var wingDustColor = FangtasticPalettePlugin.WingDustColor.Value;
            var bodyMatches = 0;
            var eyeMatches = 0;
            var dustMatches = 0;

            // Always walk every renderer even with a colour blank: a blank field still needs to
            // reach the apply methods so a previously-applied override on this instance gets
            // restored, not just skipped.
            foreach (var renderer in bodyView.GetComponentsInChildren<Renderer>(true))
            {
                var name = renderer.gameObject.name;

                if (name == LeftEyeRendererName || name == RightEyeRendererName)
                {
                    if (includeEyes)
                    {
                        eyeMatches++;
                        ApplyEyeColor(renderer, eyeColor);
                    }

                    continue;
                }

                if (name == WingDustRendererName)
                {
                    dustMatches++;
                    ApplyWingDustColor(renderer, wingDustColor);
                    continue;
                }

                if (name == BodyRendererName)
                {
                    var materials = renderer.materials;
                    for (var i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] == null)
                        {
                            continue;
                        }

                        bodyMatches++;
                        ApplyBodyColor(renderer, i, materials[i], bodyColor, earsColor,
                            fangColor, mouthColor, faceColor);
                    }
                }
            }

            Debug($"ApplyBatColors on '{bodyView.gameObject.name}': Body='{bodyColor}' Ears='{earsColor}' " +
                  $"matched {bodyMatches} material(s), EyeColor='{eyeColor}' matched {eyeMatches} renderer(s), " +
                  $"WingDustColor='{wingDustColor}' matched {dustMatches} renderer(s), includeEyes={includeEyes}.");
        }

        private static void Debug(string message)
        {
            if (!suppressLogging && FangtasticPalettePlugin.VerboseLogging.Value)
            {
                FangtasticPalettePlugin.Log.LogInfo($"[FangtasticPalette] {message}");
            }
        }

        private static void ApplyBodyColor(
            Renderer renderer, int materialIndex, Material material,
            string bodyHex, string earsHex, string fangHex, string mouthHex, string faceHex)
        {
            foreach (var (texProperty, tintProperty) in BodyTexturePairs)
            {
                if (material.HasProperty(texProperty))
                {
                    ApplyBodyPaletteSlot(renderer, materialIndex, material, texProperty, tintProperty,
                        bodyHex, earsHex, fangHex, mouthHex, faceHex);
                }
            }
        }

        // Adds a spatial region to the list when its colour is set. originalBlend fades the region's
        // recolour back toward the source (0 = full recolour, 1 = untouched) - the "intensity" knob.
        private static void AddRegion(
            System.Collections.Generic.List<TextureRecolor.SpatialRegion> list, Color? color,
            (float UMin, float UMax, float VMin, float VMax) box, bool flat, float originalBlend = 0f)
        {
            if (color.HasValue)
            {
                list.Add(new TextureRecolor.SpatialRegion(
                    color, box.UMin, box.UMax, box.VMin, box.VMax, flat, originalBlend: originalBlend));
            }
        }

        /// <summary>
        /// Recolours one albedo slot of the body material by remapping Bat_DIF's four regions (navy
        /// body + rim / beige / brown skin) each toward its own colour. All four blank restores the
        /// original texture; otherwise a single combined texture is regenerated with whichever
        /// colours are set and the rest of each region left original.
        /// </summary>
        private static void ApplyBodyPaletteSlot(
            Renderer renderer, int materialIndex, Material material, string texProperty, string tintProperty,
            string bodyHex, string earsHex, string fangHex, string mouthHex, string faceHex)
        {
            var key = (material, texProperty);

            var bodyColor = ParseOptionalColor(bodyHex, "BodyColor");
            var earsColor = ParseOptionalColor(earsHex, "EarsColor");
            var fangColor = ParseOptionalColor(fangHex, "FangColor");
            var mouthColor = ParseOptionalColor(mouthHex, "MouthColor");
            var faceColor = ParseOptionalColor(faceHex, "FaceColor");

            // Material 0 is the wings + lower body, material 1 is the head. The head carries the ears
            // (hue/value skin) + the spatial regions; the wing submesh is just one uniform body colour.
            var skinAsBody = materialIndex == 0;
            var bodyOriginalBlend = 1f - Mathf.Clamp01(FangtasticPalettePlugin.BodyIntensity.Value);

            // Spatial regions; first match wins, so smaller/specific ones first. Mouth (combined
            // nose+mouth) is a flat fill faded by Mouth Intensity toward the original texture.
            var mouthOriginalBlend = 1f - Mathf.Clamp01(FangtasticPalettePlugin.MouthIntensity.Value);
            var regions = new System.Collections.Generic.List<TextureRecolor.SpatialRegion>(4);
            AddRegion(regions, mouthColor, MouthBox, flat: true, originalBlend: mouthOriginalBlend);

            // The fang box sits INSIDE the mouth box, so a Mouth colour would swallow the fangs unless
            // the fang box re-claims itself ON TOP. Guard them: added after the mouth, using the Fang
            // colour, or white when a Mouth colour is set but no Fang colour was chosen (so a blank
            // "Default" fang swatch still can't leak the mouth onto the fangs). With no Mouth colour
            // there's nothing to bleed, so the region is only added then when a Fang colour was picked.
            var fangFill = fangColor ?? (mouthColor.HasValue ? (Color?)Color.white : null);
            AddRegion(regions, fangFill, FangBox, false);

            // Face is an EXCLUSION region: it always claims its box (the Face colour if set, else the
            // original texture) so the Ears colour below never reaches the face - the ears/eye skin
            // should stay off the face. The box (FaceBox) and edge softness (FaceSoftness) are baked
            // constants; Face Intensity is the live knob - 1 = full flat face colour, lower fades the
            // face toward the original texture (faceOriginalBlend = 1 - FaceIntensity). Added when the
            // face would otherwise be recoloured: a Face colour, or the Ears colour bleeding onto it.
            if (faceColor.HasValue || earsColor.HasValue)
            {
                var faceOriginalBlend = 1f - Mathf.Clamp01(FangtasticPalettePlugin.FaceIntensity.Value);
                regions.Add(new TextureRecolor.SpatialRegion(
                    faceColor,
                    FaceBox.UMin, FaceBox.UMax, FaceBox.VMin, FaceBox.VMax,
                    flat: true, alwaysClaim: true, ellipse: true,
                    feather: FaceSoftness, originalBlend: faceOriginalBlend));
            }

            var regionArray = regions.ToArray();

            if (!bodyColor.HasValue && !earsColor.HasValue && regionArray.Length == 0)
            {
                if (OriginalTextures.TryGetValue(key, out var originalMap))
                {
                    material.SetTexture(texProperty, originalMap);
                    if (tintProperty != null && OriginalTints.TryGetValue(key, out var originalTint))
                    {
                        material.SetColor(tintProperty, originalTint);
                    }

                    WriteToPropertyBlock(renderer, materialIndex, texProperty, originalMap, tintProperty,
                        OriginalTints.TryGetValue(key, out var tint) ? tint : (Color?)null);
                    Debug($"Body: restored '{material.name}' {texProperty} to original.");
                }

                return;
            }

            if (!OriginalTextures.ContainsKey(key))
            {
                OriginalTextures[key] = material.GetTexture(texProperty);
                if (tintProperty != null)
                {
                    OriginalTints[key] = material.GetColor(tintProperty);
                }
            }

            // The ears are one colour now, so the same earsColor is fed to the rim/beige/brown skin
            // slots - the hue/value split still runs but resolves to a single tone (with the source's
            // own shading preserved and softened by Ear Intensity: 1 = full flat colour, lower fades
            // toward the original shading, so skinOriginalBlend = 1 - EarIntensity).
            var skinOriginalBlend = 1f - Mathf.Clamp01(FangtasticPalettePlugin.EarIntensity.Value);
            var cacheDiscriminator = $"{bodyHex}/{earsHex}/{skinAsBody}/{bodyOriginalBlend:R}/{skinOriginalBlend:R}";
            var recolored = TextureRecolor.GetOrBuildBatBody(
                OriginalTextures[key], cacheDiscriminator, bodyColor, earsColor, earsColor, earsColor,
                BodyBrightnessFloor, SkinHueCenter, SkinHueRange, SkinMinSaturation, RimValue, BeigeMinValue,
                BeigeBlendBand, BodyEdgeSoftness, skinOriginalBlend, regionArray,
                skinAsBody, bodyOriginalBlend, EarBox.UMin, EarBox.UMax, EarBox.VMin, EarBox.VMax);

            material.SetTexture(texProperty, recolored);
            // The regenerated texture already encodes every colour via source luminance, so the
            // paired tint must be neutral white or it would multiply-darken the result.
            if (tintProperty != null)
            {
                material.SetColor(tintProperty, Color.white);
            }

            WriteToPropertyBlock(renderer, materialIndex, texProperty, recolored, tintProperty, Color.white);
            Debug($"Body: remapped '{material.name}' {texProperty} ({regionArray.Length} region(s)).");
        }

        /// <summary>
        /// Recolours the eye. The three eye controls are INDEPENDENT - each acts on its own
        /// brightness band whether or not the others are set (the old code gated everything on Eye
        /// Color being non-blank, which is why Eye Highlight "did nothing" until Eye Color was
        /// cleared). The bat eye is a desaturated blob, so all pixels go through the desaturated
        /// branch and split three ways by brightness:
        ///   - below EyePupilValue        -> pupil (PupilColor, else original)
        ///   - at/above EyeHighlightValue -> glint (EyeHighlightColor, else original)
        ///   - in between                 -> the eye's main mass (EyeColor, else original)
        /// All three blank restores the original texture.
        /// </summary>
        private static void ApplyEyeColor(Renderer renderer, string unusedHex)
        {
            var eyeColor = ParseOptionalColor(FangtasticPalettePlugin.EyeColor.Value, "EyeColor");
            var pupilColor = ParseOptionalColor(FangtasticPalettePlugin.PupilColor.Value, "PupilColor");
            var highlightColor = ParseOptionalColor(FangtasticPalettePlugin.EyeHighlightColor.Value, "EyeHighlightColor");
            var eyeHex = FangtasticPalettePlugin.EyeColor.Value;
            var pupilHex = FangtasticPalettePlugin.PupilColor.Value;
            var highlightHex = FangtasticPalettePlugin.EyeHighlightColor.Value;

            var materials = renderer.materials;
            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                var material = materials[materialIndex];
                if (material == null)
                {
                    continue;
                }

                // The eye uses the same two-albedo-slot Bat material as the body; recolour both.
                foreach (var (texProperty, tintProperty) in BodyTexturePairs)
                {
                    if (!material.HasProperty(texProperty))
                    {
                        continue;
                    }

                    var key = (material, texProperty);

                    if (!eyeColor.HasValue && !pupilColor.HasValue && !highlightColor.HasValue)
                    {
                        if (OriginalTextures.TryGetValue(key, out var originalMap))
                        {
                            material.SetTexture(texProperty, originalMap);
                            if (tintProperty != null && OriginalTints.TryGetValue(key, out var originalTint))
                            {
                                material.SetColor(tintProperty, originalTint);
                            }

                            WriteToPropertyBlock(renderer, materialIndex, texProperty, originalMap, tintProperty,
                                OriginalTints.TryGetValue(key, out var tint) ? tint : (Color?)null);
                            Debug($"Eyes: restored '{material.name}' {texProperty} to original.");
                        }

                        continue;
                    }

                    if (!OriginalTextures.ContainsKey(key))
                    {
                        OriginalTextures[key] = material.GetTexture(texProperty);
                        if (tintProperty != null)
                        {
                            OriginalTints[key] = material.GetColor(tintProperty);
                        }
                    }

                    // splitBelowSaturation 2: every eye pixel goes through the desaturated branch
                    // (no saturated iris on the bat). highlightColor here is the eye's main mass =
                    // EyeColor (nullable: null leaves the mid band original), glintColor is the
                    // Eye Highlight, pupilColor is the pupil. Floor 1 so the picked colours land at
                    // full brightness. The 'target' arg is unused (saturated branch never runs).
                    var recolored = TextureRecolor.GetOrBuild(
                        OriginalTextures[key], $"{eyeHex}/{pupilHex}/{highlightHex}", eyeColor ?? Color.black,
                        splitBelowSaturation: 2f,
                        highlightColor: eyeColor,
                        brightnessFloor: 1f,
                        splitBelowValue: EyePupilValue,
                        pupilColor: pupilColor,
                        splitAboveValue: EyeHighlightValue,
                        glintColor: highlightColor,
                        glintUMin: EyeGlintUMin, glintUMax: EyeGlintUMax,
                        glintVMin: EyeGlintVMin, glintVMax: EyeGlintVMax);
                    material.SetTexture(texProperty, recolored);
                    if (tintProperty != null)
                    {
                        material.SetColor(tintProperty, Color.white);
                    }

                    WriteToPropertyBlock(renderer, materialIndex, texProperty, recolored, tintProperty, Color.white);
                    Debug($"Eyes: remapped '{material.name}' {texProperty} (eye={eyeHex} pupil={pupilHex} " +
                          $"highlight={highlightHex}).");
                }
            }
        }

        /// <summary>
        /// HSV hue/saturation swap on the wing-dust particle colour, keeping each property's own
        /// original brightness - correct for HDR values too (RGBToHSV/HSVToRGB don't clamp, and the
        /// probe read _Color at 2.297). A plain Color property, no texture regeneration.
        /// </summary>
        private static void ApplyWingDustColor(Renderer renderer, string hex)
        {
            var intensity = FangtasticPalettePlugin.WingDustStrength.Value;

            // Drive the ParticleSystem's startColor too (multiplied on top of the material colour),
            // since that's what actually tints emitted dust in many setups.
            var system = renderer.GetComponent<ParticleSystem>();
            if (system != null)
            {
                var main = system.main;
                if (!OriginalWingDustStart.ContainsKey(system))
                {
                    OriginalWingDustStart[system] = main.startColor;
                }

                if (string.IsNullOrWhiteSpace(hex))
                {
                    main.startColor = OriginalWingDustStart[system];
                }
                else if (TryParseColor(hex, out var startTarget))
                {
                    // Keep the original start alpha so the dust's fade/opacity is unchanged.
                    var baseColor = OriginalWingDustStart[system].color;
                    Color.RGBToHSV(startTarget, out var h, out var s, out _);
                    Color.RGBToHSV(baseColor, out _, out _, out var v);
                    var tinted = Color.HSVToRGB(h, s, v * intensity, hdr: true);
                    tinted.a = baseColor.a;
                    main.startColor = new ParticleSystem.MinMaxGradient(tinted);
                }
            }

            foreach (var material in renderer.materials)
            {
                if (material == null)
                {
                    continue;
                }

                foreach (var property in WingDustColorProperties)
                {
                    if (!material.HasProperty(property))
                    {
                        continue;
                    }

                    var key = (material, property);

                    if (string.IsNullOrWhiteSpace(hex))
                    {
                        if (OriginalWingDust.TryGetValue(key, out var original))
                        {
                            material.SetColor(property, original);
                            Debug($"WingDust: restored '{material.name}' {property} to original.");
                        }

                        continue;
                    }

                    if (!TryParseColor(hex, out var color))
                    {
                        FangtasticPalettePlugin.Log.LogWarning($"[FangtasticPalette] WingDustColor '{hex}' is not a valid colour.");
                        continue;
                    }

                    if (!OriginalWingDust.ContainsKey(key))
                    {
                        OriginalWingDust[key] = material.GetColor(property);
                    }

                    Color.RGBToHSV(color, out var targetH, out var targetS, out _);
                    Color.RGBToHSV(OriginalWingDust[key], out _, out _, out var originalV);
                    var recolored = Color.HSVToRGB(targetH, targetS, originalV * intensity, hdr: true);
                    recolored.a = OriginalWingDust[key].a;
                    material.SetColor(property, recolored);
                    Debug($"WingDust: set '{material.name}' {property} to {recolored} (hue/sat from '{hex}').");
                }
            }
        }

        /// <summary>
        /// Writes through the renderer's MaterialPropertyBlock as well as the material. On the bat
        /// the probe reported HasPropertyBlock=false for the body and eyes, so the material write
        /// alone should render - but writing the block too is harmless and keeps this correct if the
        /// game ever attaches one. Read-modify-write per material index so anything the game set is
        /// preserved.
        /// </summary>
        private static void WriteToPropertyBlock(
            Renderer renderer, int materialIndex, string texProperty, Texture texture, string tintProperty, Color? tint)
        {
            if (renderer == null)
            {
                return;
            }

            var block = new MaterialPropertyBlock();
            if (renderer.HasPropertyBlock())
            {
                renderer.GetPropertyBlock(block, materialIndex);
            }

            if (texProperty != null && texture != null)
            {
                block.SetTexture(texProperty, texture);
            }

            if (tintProperty != null && tint.HasValue)
            {
                block.SetColor(tintProperty, tint.Value);
            }

            renderer.SetPropertyBlock(block, materialIndex);
        }

        private static Color? ParseOptionalColor(string hex, string settingName)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return null;
            }

            if (TryParseColor(hex, out var parsed))
            {
                return parsed;
            }

            FangtasticPalettePlugin.Log.LogWarning($"[FangtasticPalette] {settingName} '{hex}' is not a valid colour.");
            return null;
        }

        private static bool TryParseColor(string value, out Color color)
        {
            if (ColorUtility.TryParseHtmlString(value, out color))
            {
                return true;
            }

            return !value.StartsWith("#") && ColorUtility.TryParseHtmlString("#" + value, out color);
        }
    }
}
