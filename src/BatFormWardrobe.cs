using System;
using System.Linq;
using BepInEx.Configuration;
using Chicken.Utilities;
using HarmonyLib;
using UnityEngine;

namespace FangtasticPalette
{
    /// <summary>
    /// Puts a "Bat Form" tab in the mirror's wardrobe: picking it swaps the preview to the Bat Form
    /// body and replaces the category rows with this mod's colour-swatch panel, with a live preview
    /// that reverts on cancel. A direct port of Purrtastic Palette's Cat Form tab; the wardrobe
    /// traps it solved are written up at repo root 17-wardrobe-ui.md.
    ///
    /// Note: CustomizationCharacterPreview is NOT the player entity - it's a standalone rig with its
    /// own private BodyViewAsset field, not driven by EntityCustomization.SetBodyView. So the Bat
    /// body is instantiated into the rig by hand and coloured explicitly (the live-player path does
    /// not touch it). The rig's framing was built for the human/cat body; if the bat renders
    /// off-centre or mis-scaled, adjust the transform copy in ShowBatInPreview.
    /// </summary>
    [HarmonyPatch(typeof(WardrobeCustomizationScreen), "OnShow")]
    internal static class BatFormWardrobe
    {
        private const string BatBodyViewNameFragment = "BatBodyView";

        private static readonly AccessTools.FieldRef<WardrobeCustomizationScreen, BumperMenuWidget> BumperMenuRef =
            AccessTools.FieldRefAccess<WardrobeCustomizationScreen, BumperMenuWidget>("bumperMenuWidget");

        private static readonly AccessTools.FieldRef<CustomizationCharacterPreview, BodyViewAsset> PreviewBodyRef =
            AccessTools.FieldRefAccess<CustomizationCharacterPreview, BodyViewAsset>("bodyView");

        private static readonly AccessTools.FieldRef<WardrobeCustomizationScreen, CustomizationCategoryListWidget> CategoryListRef =
            AccessTools.FieldRefAccess<WardrobeCustomizationScreen, CustomizationCategoryListWidget>("categoryListWidget");

        // The open screen, so the Bat Form tab's own handler can reach its widgets. The tab's
        // OnSelect is a plain callback with no screen argument, unlike the game's own tabs which
        // close over their Tab data.
        private static WardrobeCustomizationScreen activeScreen;

        // The instantiated cat body, so repeated tab presses reuse it and hiding it again is
        // possible. Cleared when the screen closes, since the preview rig itself is torn down.
        private static BodyViewAsset batBodyInstance;

        // Live-preview support: a swatch pick applies immediately (so the cat updates as you browse),
        // but is only KEPT if the player presses Confirm. Snapshot the colour config when the
        // wardrobe opens and restore it on close-without-confirm - the same revert-on-cancel the
        // game gives its own try-on clothing.
        private static string[] colorSnapshot;
        private static bool customizationsConfirmed;

        private static ConfigEntry<string>[] ManagedColors() => new[]
        {
            FangtasticPalettePlugin.BodyColor,
            FangtasticPalettePlugin.EarsColor,
            FangtasticPalettePlugin.FangColor,
            FangtasticPalettePlugin.MouthColor,
            FangtasticPalettePlugin.FaceColor,
            FangtasticPalettePlugin.EyeColor,
            FangtasticPalettePlugin.PupilColor,
            FangtasticPalettePlugin.EyeHighlightColor,
            FangtasticPalettePlugin.WingDustColor,
        };

        /// <summary>
        /// Whether the player owns Bat Form. Forms are ItemAssets whose ToolAddon is a form tool
        /// (BatToolAsset here), kept in GameInventory's Forms inventory; Contains routes the item to
        /// that inventory. Returns false if the inventory or item can't be reached, so an unknown
        /// state hides the tab rather than showing it wrongly.
        /// </summary>
        private static bool PlayerOwnsBatForm()
        {
            try
            {
                if (!MonoBehaviourSingleton<GameInventory>.Exists)
                {
                    return false;
                }

                var batItem = Asset.GetAll<ItemAsset>()
                    .FirstOrDefault(x => x != null && x.ToolAddon is BatToolAsset);
                if (batItem == null)
                {
                    return false;
                }

                return MonoBehaviourSingleton<GameInventory>.Instance.Contains(new ItemEntry(batItem));
            }
            catch (Exception e)
            {
                FangtasticPalettePlugin.Log.LogError($"[FangtasticPalette] Wardrobe: Bat Form ownership check failed: {e}");
                return false;
            }
        }

        [HarmonyPostfix]
        private static void Postfix(WardrobeCustomizationScreen __instance)
        {
            try
            {
                activeScreen = __instance;
                var bumperMenu = BumperMenuRef(__instance);
                if (bumperMenu == null)
                {
                    FangtasticPalettePlugin.Log.LogWarning("[FangtasticPalette] Wardrobe: no bumperMenuWidget found - cannot add the Bat Form tab.");
                    return;
                }

                // Only offer the tab once the player actually owns Bat Form - recolouring a form you
                // cannot turn into makes no sense, and the tab would otherwise show from the very
                // first mirror use. The widget has no "disabled" state, so gate by not adding it.
                if (!PlayerOwnsBatForm())
                {
                    FangtasticPalettePlugin.Log.LogInfo("[FangtasticPalette] Wardrobe: Bat Form not owned - tab hidden.");
                    return;
                }

                bumperMenu.AddItem(new BumperMenuWidget.WidgetData
                {
                    Text = "Bat Form",
                    Icon = TabIcon.Get(),
                    OnSelect = ShowBatInPreview,
                });

                // OnShow already called Show()/SelectDefaultMenu() before this postfix ran, so the
                // new item needs the widget rebuilt to appear. If the tab doesn't show up in-game,
                // this is the first thing to suspect.
                bumperMenu.Show();

                // Re-select the default tab AFTER adding ours. Show() reflows the strip and refreshes
                // the scroll selector but doesn't re-run the initial selection, so with enough tabs
                // to overflow the strip it stays centred with the first (selected) tab faded at the
                // edge. SelectDefaultMenu re-selects the first tab, and Show() just primed an instant
                // scroll, so the strip snaps to show the selected tab properly.
                bumperMenu.SelectDefaultMenu();

                // Snapshot the cat colours now (before any pick) so picks are a live preview that
                // reverts unless Confirm is pressed. OnCustomizationsConfirmed fires when the
                // screen's Confirm button is clicked; its Signal lives on this screen instance and
                // dies with it, so subscribing per-open needs no explicit unsubscribe.
                var colors = ManagedColors();
                colorSnapshot = colors.Select(c => c.Value).ToArray();
                customizationsConfirmed = false;
                __instance.OnCustomizationsConfirmed.AddListener(HandleCustomizationsConfirmed);

                FangtasticPalettePlugin.Log.LogInfo("[FangtasticPalette] Wardrobe: added the Bat Form tab.");
            }
            catch (Exception e)
            {
                FangtasticPalettePlugin.Log.LogError($"[FangtasticPalette] Wardrobe: failed to add the Bat Form tab: {e}");
            }
        }

        private static void ShowBatInPreview()
        {
            try
            {
                if (!MonoBehaviourSingleton<CharacterCustomizer>.Exists)
                {
                    FangtasticPalettePlugin.Log.LogWarning("[FangtasticPalette] Wardrobe: no CharacterCustomizer - cannot reach the preview.");
                    return;
                }

                var preview = MonoBehaviourSingleton<CharacterCustomizer>.Instance.CharacterPreview;
                if (preview == null)
                {
                    FangtasticPalettePlugin.Log.LogWarning("[FangtasticPalette] Wardrobe: CharacterPreview is null.");
                    return;
                }

                var humanBody = PreviewBodyRef(preview);
                if (humanBody == null)
                {
                    FangtasticPalettePlugin.Log.LogWarning("[FangtasticPalette] Wardrobe: the preview has no body view to replace.");
                    return;
                }

                if (batBodyInstance == null)
                {
                    var batBodyAsset = Asset.GetAll<BodyViewAsset>()
                        .FirstOrDefault(x => x != null && x.name.IndexOf(BatBodyViewNameFragment, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (batBodyAsset == null)
                    {
                        FangtasticPalettePlugin.Log.LogWarning(
                            $"[FangtasticPalette] Wardrobe: no BodyViewAsset matching '{BatBodyViewNameFragment}' found.");
                        return;
                    }

                    // Instantiate alongside the human body and copy its local transform, so the cat
                    // lands wherever the rig expects a body to be. Whether that framing suits a cat
                    // is exactly what this spike is testing.
                    batBodyInstance = UnityEngine.Object.Instantiate(batBodyAsset, humanBody.transform.parent);
                    batBodyInstance.transform.localPosition = humanBody.transform.localPosition;
                    batBodyInstance.transform.localRotation = humanBody.transform.localRotation;
                    batBodyInstance.transform.localScale = humanBody.transform.localScale;
                    FangtasticPalettePlugin.Log.LogInfo(
                        $"[FangtasticPalette] Wardrobe: instantiated '{batBodyAsset.name}' into the preview under " +
                        $"'{humanBody.transform.parent?.name}'.");
                }

                // Hide EVERY other body in the preview rig, not just the human - the sibling
                // Purrtastic Palette "Cat Form" tab instantiates its cat into the same parent and
                // doesn't get a teardown callback when our tab is picked, so switching Cat -> Bat
                // otherwise leaves the cat on screen alongside the bat. Deactivate all BodyViewAssets
                // under the parent, then show only ours.
                foreach (var body in humanBody.transform.parent.GetComponentsInChildren<BodyViewAsset>(true))
                {
                    if (body != null && body != batBodyInstance)
                    {
                        body.gameObject.SetActive(false);
                    }
                }

                batBodyInstance.gameObject.SetActive(true);
                HidePreviewVfx();
                ApplyFlapFreeze();
                PreviewBloomSuppressor.Suppress();

                // The preview cat is a separate instance of the body prefab with its own material
                // instances, so the colours applied to the live player don't carry over - without
                // this it renders vanilla black while the real cat outside is coloured.
                BatColorPatch.ApplyToBody(batBodyInstance);

                ClearCategoryPanel();
                BuildColorPanel();

                var renderers = batBodyInstance.GetComponentsInChildren<Renderer>(true).Length;
                FangtasticPalettePlugin.Log.LogInfo(
                    $"[FangtasticPalette] Wardrobe: showing the cat body in the preview " +
                    $"({renderers} renderer(s), scale {batBodyInstance.transform.lossyScale}).");
            }
            catch (Exception e)
            {
                FangtasticPalettePlugin.Log.LogError($"[FangtasticPalette] Wardrobe: failed to show the cat in the preview: {e}");
            }
        }

        /// <summary>
        /// Freezes or resumes the preview bat's flap animation per the Freeze Preview Flap setting,
        /// by scaling every Animator's speed. Preview-only (this instance is separate from the live
        /// player). Called on tab-select and on config change so the toggle updates live. A no-op
        /// when the preview isn't showing (batBodyInstance null).
        /// </summary>
        internal static void ApplyFlapFreeze()
        {
            if (batBodyInstance == null)
            {
                return;
            }

            var freeze = FangtasticPalettePlugin.FreezePreviewFlap.Value;
            foreach (var animator in batBodyInstance.GetComponentsInChildren<Animator>(true))
            {
                if (animator != null)
                {
                    animator.speed = freeze ? 0f : 1f;
                }
            }
        }

        /// <summary>
        /// Turns off the cat's glow effects in the wardrobe preview only. The preview holds the
        /// model still and close to the camera, which makes the aura sparkles and trail far more
        /// prominent than they are in play - enough to obscure the fur colour being picked. This
        /// touches only the preview's own instantiated copy, so the glow in the world is
        /// unaffected; AuraColor still controls that.
        /// </summary>
        private static void HidePreviewVfx()
        {
            if (batBodyInstance == null)
            {
                return;
            }

            var hidden = 0;
            foreach (var particles in batBodyInstance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.gameObject.SetActive(false);
                hidden++;
            }

            foreach (var trail in batBodyInstance.GetComponentsInChildren<TrailRenderer>(true))
            {
                trail.gameObject.SetActive(false);
                hidden++;
            }

            FangtasticPalettePlugin.Log.LogInfo($"[FangtasticPalette] Wardrobe: hid {hidden} VFX object(s) in the preview.");
        }

        /// <summary>
        /// Blanks the category rows on the right. Without this the panel keeps showing whatever
        /// the previously selected tab built (skin colour swatches, accessories, ...), which is
        /// both wrong and clickable - picking one would apply a human customization while the
        /// cat is on screen. The real colour panel replaces this; for now an empty list is the
        /// honest state.
        /// </summary>
        // The Content children this mod disabled to show its own panel, so exactly those can be
        // re-enabled again - not blindly re-enabling the inactive template or empty-state view.
        private static readonly System.Collections.Generic.List<GameObject> hiddenCategoryObjects =
            new System.Collections.Generic.List<GameObject>();

        private static void ClearCategoryPanel()
        {
            HideCategoryContent();
        }

        /// <summary>
        /// Hides the game's category rows so the Bat Form panel can take their place. The
        /// serialized categoryListWidget field points at an inactive TEMPLATE - the rows the game
        /// actually displays are runtime CLONES of it ("CategoryListWidget(Clone)") sitting beside
        /// it under Content (confirmed from a hierarchy dump). Disabling the template did nothing
        /// visible; every currently-active Content child has to be disabled instead. Doing it by
        /// "whatever is active right now" rather than by name also covers the empty-state view and
        /// anything else the screen parks there.
        /// </summary>
        private static void HideCategoryContent()
        {
            hiddenCategoryObjects.Clear();

            var parent = CategoryContentParent();
            if (parent == null)
            {
                return;
            }

            foreach (Transform child in parent)
            {
                if (child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(false);
                    hiddenCategoryObjects.Add(child.gameObject);
                }
            }
        }

        private static void RestoreCategoryContent()
        {
            foreach (var go in hiddenCategoryObjects)
            {
                if (go != null)
                {
                    go.SetActive(true);
                }
            }

            hiddenCategoryObjects.Clear();
        }

        private static Transform CategoryContentParent()
        {
            if (activeScreen == null)
            {
                return null;
            }

            var categoryList = CategoryListRef(activeScreen);
            return categoryList != null ? categoryList.transform.parent : null;
        }

        /// <summary>
        /// Builds the swatch panel in the space the category rows would occupy, parented to the
        /// category list's own parent so it inherits the screen's layout and scaling rather than
        /// floating in its own canvas.
        /// </summary>
        private static void BuildColorPanel()
        {
            if (activeScreen == null)
            {
                return;
            }

            var categoryList = CategoryListRef(activeScreen);
            var parent = categoryList != null ? categoryList.transform.parent : null;
            if (parent == null)
            {
                FangtasticPalettePlugin.Log.LogWarning("[FangtasticPalette] Wardrobe: no panel parent - cannot build the colour panel.");
                return;
            }

            // Recolour the preview as soon as a swatch is picked. The live player is handled by
            // the plugin's own SettingChanged hook; this covers the preview's separate body.
            BatFormColorPanel.OnColorChanged = () => BatColorPatch.ApplyToBody(batBodyInstance);
            BatFormColorPanel.Build(parent);
        }

        /// <summary>
        /// Puts the human body back whenever one of the game's own tabs is picked. Those tabs go
        /// through HandleTabSelected; the Bat Form tab does not, so this only ever fires for
        /// vanilla tabs. A PREFIX, not a postfix, so it runs before the original repopulates the
        /// category rows: it re-enables the category widget (which the cat tab disabled) and tears
        /// down our panel first, leaving the original free to lay out normally.
        /// </summary>
        [HarmonyPatch(typeof(WardrobeCustomizationScreen), "HandleTabSelected")]
        [HarmonyPrefix]
        private static void RestoreHumanPreview()
        {
            try
            {
                // Always re-show the category rows and drop our panel, even if the cat was never
                // shown - cheap, and it guarantees the game's rows are visible.
                RestoreCategoryContent();
                BatFormColorPanel.Destroy();

                if (batBodyInstance == null || !MonoBehaviourSingleton<CharacterCustomizer>.Exists)
                {
                    return; // cat was never shown - nothing further to undo
                }

                var preview = MonoBehaviourSingleton<CharacterCustomizer>.Instance.CharacterPreview;
                if (preview == null)
                {
                    return;
                }

                batBodyInstance.gameObject.SetActive(false);
                PreviewBloomSuppressor.Restore();

                var humanBody = PreviewBodyRef(preview);
                if (humanBody != null)
                {
                    humanBody.gameObject.SetActive(true);
                }

                FangtasticPalettePlugin.Log.LogInfo("[FangtasticPalette] Wardrobe: restored the human body for a vanilla tab.");
            }
            catch (Exception e)
            {
                FangtasticPalettePlugin.Log.LogError($"[FangtasticPalette] Wardrobe: failed to restore the human body: {e}");
            }
        }

        /// <summary>
        /// The preview rig is destroyed with the screen, so the cached instance must not outlive
        /// it - a stale reference would silently do nothing on the next visit.
        /// </summary>
        [HarmonyPatch(typeof(WardrobeCustomizationScreen), "OnHide")]
        [HarmonyPostfix]
        private static void ClearCachedBody()
        {
            RevertUnlessConfirmed();
            BatFormColorPanel.Destroy();
            PreviewBloomSuppressor.Restore();
            hiddenCategoryObjects.Clear(); // the screen and its Content are torn down with it
            batBodyInstance = null;
            activeScreen = null;
        }

        private static void HandleCustomizationsConfirmed()
        {
            customizationsConfirmed = true;
        }

        /// <summary>
        /// Restores the colour snapshot taken when the wardrobe opened, unless the player pressed
        /// Confirm. Writing a ConfigEntry.Value raises SettingChanged, which reapplies colours to the
        /// live player - so this reverts the world cat as well as discarding the preview picks. A
        /// no-op when nothing changed or when confirmed.
        /// </summary>
        private static void RevertUnlessConfirmed()
        {
            try
            {
                if (customizationsConfirmed || colorSnapshot == null)
                {
                    return;
                }

                var colors = ManagedColors();
                var reverted = 0;
                for (var i = 0; i < colors.Length && i < colorSnapshot.Length; i++)
                {
                    if (colors[i] != null && colors[i].Value != colorSnapshot[i])
                    {
                        colors[i].Value = colorSnapshot[i];
                        reverted++;
                    }
                }

                if (reverted > 0)
                {
                    FangtasticPalettePlugin.Log.LogInfo(
                        $"[FangtasticPalette] Wardrobe: reverted {reverted} colour(s) - closed without confirming.");
                }
            }
            catch (Exception e)
            {
                FangtasticPalettePlugin.Log.LogError($"[FangtasticPalette] Wardrobe: colour revert failed: {e}");
            }
            finally
            {
                colorSnapshot = null;
                customizationsConfirmed = false;
            }
        }
    }
}
