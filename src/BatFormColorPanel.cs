using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;

namespace FangtasticPalette
{
    /// <summary>
    /// The Bat Form tab's panel: a scrollable column of a Freeze-Flap toggle + one colour-swatch row
    /// per part (each with its intensity/strength slider tucked underneath). This class is the
    /// orchestrator - it composes the panel and does the deterministic height arithmetic that makes it
    /// scroll (see the note in <see cref="Build"/>). The pieces live in their own components: the
    /// swatch grid + selection in <see cref="SwatchGrid"/>, the reusable control-row builders in
    /// <see cref="PanelControls"/>. Ported from Purrtastic Palette; see repo root 17-wardrobe-ui.md.
    /// </summary>
    internal static class BatFormColorPanel
    {
        // Declarative colour -> intensity/strength slider map (replaces a label-string switch): a
        // colour row gets the matching slider tucked directly beneath it; colours absent from the map
        // (Fangs, Eyes, Pupil, Eye Highlight) have none. Keyed by BatPalette.Colors' label.
        private static readonly Dictionary<string, (string Label, Func<ConfigEntry<float>> Setting, float Min, float Max)>
            IntensitySliders =
                new Dictionary<string, (string, Func<ConfigEntry<float>>, float, float)>
                {
                    ["Body"] = ("Body Intensity", () => FangtasticPalettePlugin.BodyIntensity, 0f, 1f),
                    ["Ears"] = ("Ear Intensity", () => FangtasticPalettePlugin.EarIntensity, 0f, 1f),
                    ["Mouth"] = ("Mouth Intensity", () => FangtasticPalettePlugin.MouthIntensity, 0f, 1f),
                    ["Face"] = ("Face Intensity", () => FangtasticPalettePlugin.FaceIntensity, 0f, 1f),
                    ["Wing Dust"] = ("Wing Dust Strength", () => FangtasticPalettePlugin.WingDustStrength, 1f, 6f),
                };

        private static GameObject root;
        private static Transform lastParent;

        internal static Action OnColorChanged;

        internal static bool IsBuilt => root != null;

        internal static void Build(Transform parent)
        {
            lastParent = parent;
            Destroy();

            try
            {
                SwatchGrid.Reset();
                SwatchGrid.OnColorChanged = () => OnColorChanged?.Invoke();
                SwatchGrid.RequestRebuild = () => { if (lastParent != null) { Build(lastParent); } };

                root = new GameObject("FangtasticPalette_ColorPanel", typeof(RectTransform));
                root.transform.SetParent(parent, false);

                var layout = root.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 28f;
                // childControlHeight true so each row's explicit LayoutElement height is honoured;
                // false makes them collapse onto each other.
                layout.childControlHeight = true;
                layout.childControlWidth = true;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = true;
                layout.padding = new RectOffset(8, 8, 40, 8); // extra top padding above the first header

                // Sum the row heights so the panel can be given a DEFINITE height. This is the fix
                // for "the panel doesn't scroll": the parent "Content" is the screen's own scroll
                // content (a ScrollRect + Mask viewport above it, a VerticalLayoutGroup +
                // ContentSizeFitter on it). The game's category rows scroll because each is a
                // top-anchored child with a real height, so the fitter grows Content past the
                // viewport. The old panel instead stretch-filled Content (anchorMax y = 1), pinning
                // its height to Content rather than driving it - so Content never overflowed and the
                // Elastic ScrollRect just rubber-banded back. Give the panel its own height and
                // top-anchor it exactly like a game row, and the existing ScrollRect scrolls it - no
                // new mask, which is what blanked the panel twice before (doc §6/§7).
                var totalHeight = (float)(layout.padding.top + layout.padding.bottom);
                var rowCount = 0;

                // Preview control first, so it's the first thing seen when the tab opens.
                totalHeight += PanelControls.AddToggleRow(root.transform, "Freeze Flap", FangtasticPalettePlugin.FreezePreviewFlap);
                rowCount++;

                // Each colour row, with its own intensity/strength slider tucked directly underneath
                // (Body/Ears/Mouth/Face/Wing Dust have one; the rest don't). Grouping the slider with
                // its colour reads better than a block of loose sliders at the bottom.
                foreach (var (label, setting, defaultColor) in BatPalette.Colors)
                {
                    totalHeight += SwatchGrid.AddColorRow(root.transform, label, setting(), defaultColor);
                    rowCount++;

                    if (IntensitySliders.TryGetValue(label, out var slider))
                    {
                        totalHeight += PanelControls.AddSliderRow(
                            root.transform, slider.Label, slider.Setting(), slider.Min, slider.Max,
                            () => OnColorChanged?.Invoke());
                        rowCount++;
                    }
                }

                totalHeight += layout.spacing * Mathf.Max(0, rowCount - 1);

                var rootRect = (RectTransform)root.transform;
                // Width stretches to Content (minus a 16px inset each side); height is fixed and the
                // panel hangs from the top edge, so the ContentSizeFitter above sizes Content to it.
                rootRect.anchorMin = new Vector2(0f, 1f);
                rootRect.anchorMax = new Vector2(1f, 1f);
                rootRect.pivot = new Vector2(0.5f, 1f);
                rootRect.sizeDelta = new Vector2(-32f, totalHeight);
                rootRect.anchoredPosition = Vector2.zero;

                // Belt and suspenders: if Content's VerticalLayoutGroup turns out to control child
                // height, honour our height through a LayoutElement too, not only the RectTransform.
                var rootElement = root.AddComponent<LayoutElement>();
                rootElement.preferredHeight = totalHeight;
                rootElement.minHeight = totalHeight;
                rootElement.flexibleHeight = 0f;

                SwatchGrid.RefreshSelection();

                // Recompute the parent Content's ContentSizeFitter and the ScrollRect now, so the
                // panel is the right height and scrollable on the very first frame rather than after
                // a layout pass.
                if (parent is RectTransform parentRect)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
                }

                var rect = rootRect.rect;
                FangtasticPalettePlugin.Log.LogInfo(
                    $"[FangtasticPalette] Wardrobe: colour panel built - parent '{parent.name}', " +
                    $"panel rect {rect.width:F0}x{rect.height:F0}, target height {totalHeight:F0}, " +
                    $"{root.transform.childCount} row(s), {SwatchGrid.Count} swatch(es), " +
                    $"activeInHierarchy={root.activeInHierarchy}.");
            }
            catch (Exception e)
            {
                FangtasticPalettePlugin.Log.LogError($"[FangtasticPalette] Wardrobe: failed to build the colour panel: {e}");
            }
        }

        internal static void Destroy()
        {
            ColorPickerPopup.CloseAny();
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }

            root = null;
        }
    }
}
