using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FangtasticPalette
{
    /// <summary>
    /// The colour-swatch subsystem for one Bat Form colour row: the preset grid, the cloned-or-drawn
    /// swatches, the "+" custom tile that opens the RGB picker, and the selection state that keeps the
    /// ring/checkmark in sync with the config value. Prefers a clone of the game's own swatch widget
    /// (<see cref="BatFormSwatch"/> - real selection ring, checkmark, hover sound) and falls back to a
    /// drawn three-circle swatch when the template can't be found.
    ///
    /// State (<see cref="Swatches"/> / <see cref="ClonedSwatches"/>) accumulates as rows are built and
    /// is wiped by <see cref="Reset"/> at the start of each panel build. Two callbacks are injected by
    /// <see cref="BatFormColorPanel"/> so this doesn't depend back on the orchestrator:
    /// <see cref="OnColorChanged"/> (refresh the preview) and <see cref="RequestRebuild"/> (rebuild the
    /// whole panel after a custom colour is picked, so the custom tile shows it).
    /// </summary>
    internal static class SwatchGrid
    {
        private const float SwatchSize = 90f;

        private static readonly (string Label, string Hex)[] Presets =
        {
            ("Black", "#141018"),
            ("White", "#F2ECFF"),
            ("Red", "#FF4339"),
            ("Orange", "#FF8A3D"),
            ("Gold", "#FCD34D"),
            ("Green", "#4ADE80"),
            ("Teal", "#2DD4BF"),
            ("Blue", "#5ACEF9"),
            ("Indigo", "#7F54D3"),
            ("Violet", "#E452FC"),
            ("Pink", "#FF7AC6"),
        };

        private static readonly List<SwatchEntry> Swatches = new List<SwatchEntry>();
        private static readonly List<BatFormSwatch> ClonedSwatches = new List<BatFormSwatch>();

        // Injected by BatFormColorPanel.Build so this component never references the orchestrator.
        internal static Action OnColorChanged;
        internal static Action RequestRebuild;

        internal static int Count => Swatches.Count;

        /// <summary>Wipe the per-build selection state. Call at the start of each panel build.</summary>
        internal static void Reset()
        {
            Swatches.Clear();
            ClonedSwatches.Clear();
        }

        /// <summary>
        /// One colour row: a decorated header label plus the swatch grid (default + presets + custom
        /// tile). Returns the row height so the panel can size its scroll content deterministically.
        /// </summary>
        internal static float AddColorRow(Transform parent, string label, ConfigEntry<string> setting, Color defaultColor)
        {
            const int columns = 5;
            const float spacing = 20f;
            const float labelHeight = 40f;
            // Extra height reserved at the bottom of each row so the selection frame (the bat
            // wings) and the checkmark of the last swatch row - which extend past the swatch cell -
            // don't collide with the next row's header.
            const float frameOverflow = 24f;

            var swatchCount = 1 + Presets.Length + 1; // default + presets + custom
            var gridRows = Mathf.CeilToInt(swatchCount / (float)columns);
            var gridHeight = gridRows * SwatchSize + (gridRows - 1) * spacing;
            var rowHeight = labelHeight + 56f + gridHeight + frameOverflow; // 56f = header-to-grid gap

            var row = new GameObject($"Row_{label}", typeof(RectTransform));
            row.transform.SetParent(parent, false);

            var rowLayout = row.AddComponent<VerticalLayoutGroup>();
            rowLayout.spacing = 56f;
            // Same reason as the root group: true so the label and grid stack by their own
            // heights instead of overlapping.
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandHeight = false;

            var rowElement = row.AddComponent<LayoutElement>();
            rowElement.preferredHeight = rowHeight;
            rowElement.minHeight = rowHeight;

            PanelControls.AddLabel(row.transform, label, labelHeight);

            var grid = new GameObject("Swatches", typeof(RectTransform));
            grid.transform.SetParent(row.transform, false);
            var gridLayout = grid.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(SwatchSize, SwatchSize);
            gridLayout.spacing = new Vector2(spacing, spacing);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = columns;
            // Centre the block of swatches in the (full-width) row rather than left-packing it.
            gridLayout.childAlignment = TextAnchor.UpperCenter;

            var gridElement = grid.AddComponent<LayoutElement>();
            gridElement.preferredHeight = gridHeight;
            gridElement.minHeight = gridHeight;

            // Default first, painted in the part's real vanilla colour.
            AddSwatch(grid.transform, "Default", string.Empty, defaultColor, setting);

            foreach (var (presetLabel, hex) in Presets)
            {
                AddSwatch(grid.transform, presetLabel, hex, ParseOr(hex, Color.magenta), setting);
            }

            AddCustomTile(grid.transform, setting);

            return rowHeight;
        }

        /// <summary>Re-sync every swatch's selection ring/checkmark with the current config values.</summary>
        internal static void RefreshSelection()
        {
            foreach (var entry in Swatches)
            {
                var selected = entry.IsCustomTile ? IsCustomValue(entry.Setting) : IsCurrent(entry.Setting, entry.Hex);

                if (entry.Ring != null)
                {
                    entry.Ring.enabled = selected;
                }

                if (entry.Check != null)
                {
                    entry.Check.enabled = selected;
                }
            }

            foreach (var cloned in ClonedSwatches)
            {
                if (cloned != null)
                {
                    cloned.Refresh();
                }
            }
        }

        private static void AddSwatch(Transform parent, string name, string hex, Color fill, ConfigEntry<string> setting)
        {
            // Prefer the game's own widget (bat wings, checkmark, hover sound); fall back to a
            // drawn swatch only if the template cannot be found.
            if (BatFormSwatch.IsAvailable)
            {
                var cloned = BatFormSwatch.Create(
                    parent, $"Swatch_{name}", fill, () => IsCurrent(setting, hex),
                    () => { Apply(setting, hex); RefreshSelection(); });

                if (cloned != null)
                {
                    ClonedSwatches.Add(cloned);
                    return;
                }
            }

            var swatch = BuildSwatchShell(parent, $"Swatch_{name}", fill, out var ring, out var check);

            var trigger = swatch.AddComponent<EventTrigger>();
            PanelControls.AddTrigger(trigger, EventTriggerType.PointerEnter, () => ring.enabled = true);
            PanelControls.AddTrigger(trigger, EventTriggerType.PointerExit, () => ring.enabled = IsCurrent(setting, hex));
            PanelControls.AddTrigger(trigger, EventTriggerType.PointerClick, () =>
            {
                Apply(setting, hex);
                RefreshSelection();
            });
            swatch.AddComponent<ScrollForwarder>(); // EventTrigger eats the wheel; keep the panel scrollable

            Swatches.Add(new SwatchEntry(setting, hex, ring, check, isCustomTile: false));
        }

        private static void AddCustomTile(Transform parent, ConfigEntry<string> setting)
        {
            var fillColor = IsCustomValue(setting) ? ParseOr(setting.Value, Color.white) : (Color?)null;

            if (BatFormSwatch.IsAvailable)
            {
                var cloned = BatFormSwatch.Create(
                    parent, "Swatch_Custom", fillColor, () => IsCustomValue(setting),
                    () => OpenPicker(parent, setting));

                if (cloned != null)
                {
                    ClonedSwatches.Add(cloned);
                    // Centre the "+" on the colour plate (the visible circle), not the widget root -
                    // the root reserves extra height, so centring on it puts the "+" off-centre.
                    AddCaption(cloned.PlateTransform != null ? cloned.PlateTransform : cloned.transform, "+");
                    return;
                }
            }

            var fill = fillColor ?? new Color(0.22f, 0.18f, 0.30f, 1f);
            var swatch = BuildSwatchShell(parent, "Swatch_Custom", fill, out var ring, out var check);

            AddCaption(swatch.transform, "+");

            var trigger = swatch.AddComponent<EventTrigger>();
            PanelControls.AddTrigger(trigger, EventTriggerType.PointerEnter, () => ring.enabled = true);
            PanelControls.AddTrigger(trigger, EventTriggerType.PointerExit, () => ring.enabled = IsCustomValue(setting));
            PanelControls.AddTrigger(trigger, EventTriggerType.PointerClick, () => OpenPicker(parent, setting));
            swatch.AddComponent<ScrollForwarder>(); // EventTrigger eats the wheel; keep the panel scrollable

            Swatches.Add(new SwatchEntry(setting, null, ring, check, isCustomTile: true));
        }

        /// <summary>
        /// A swatch is three stacked circles: an accent ring, the colour face inset inside it, and
        /// a checkmark on top. The ring is a full-size circle behind a smaller face rather than an
        /// outline sprite, which keeps it to one generated texture for everything.
        /// </summary>
        private static GameObject BuildSwatchShell(Transform parent, string name, Color fill, out Image ring, out TextMeshProUGUI check)
        {
            var swatch = new GameObject(name, typeof(RectTransform));
            swatch.transform.SetParent(parent, false);

            var ringHost = new GameObject("Ring", typeof(RectTransform));
            ringHost.transform.SetParent(swatch.transform, false);
            PanelControls.Stretch((RectTransform)ringHost.transform, 0f);
            ring = ringHost.AddComponent<Image>();
            ring.sprite = CircleSprite.Get();
            ring.color = Palette.Accent;
            ring.raycastTarget = false;
            ring.enabled = false;

            var faceHost = new GameObject("Face", typeof(RectTransform));
            faceHost.transform.SetParent(swatch.transform, false);
            PanelControls.Stretch((RectTransform)faceHost.transform, 7f);
            var face = faceHost.AddComponent<Image>();
            face.sprite = CircleSprite.Get();
            face.color = fill;
            // The face is the click target; the swatch root has no graphic of its own.
            face.raycastTarget = true;

            var checkHost = new GameObject("Check", typeof(RectTransform));
            checkHost.transform.SetParent(swatch.transform, false);
            PanelControls.Stretch((RectTransform)checkHost.transform, 0f);
            check = checkHost.AddComponent<TextMeshProUGUI>();
            check.text = "<b>✓</b>";
            check.fontSize = 46f;
            check.color = Palette.Label;
            check.alignment = TextAlignmentOptions.Center;
            check.raycastTarget = false;
            check.enabled = false;
            GameFonts.Apply(check, preferOutline: true);

            return swatch;
        }

        private static void AddCaption(Transform parent, string text)
        {
            var host = new GameObject("Caption", typeof(RectTransform));
            host.transform.SetParent(parent, false);
            host.transform.SetAsLastSibling();
            PanelControls.Stretch((RectTransform)host.transform, 0f);

            // ignoreLayout so a layout group on the cloned swatch cannot reposition it - the "+"
            // was landing at the bottom because the clone's own layout was placing this host.
            var element = host.AddComponent<LayoutElement>();
            element.ignoreLayout = true;

            var label = host.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 44f;
            label.color = Palette.Label;
            label.alignment = TextAlignmentOptions.Center;   // horizontal centre
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            label.raycastTarget = false;
            GameFonts.Apply(label, preferOutline: false);
        }

        private static void OpenPicker(Transform context, ConfigEntry<string> setting)
        {
            var initial = ParseOr(setting.Value, Color.white);
            var canvas = context.GetComponentInParent<Canvas>();
            var pickerParent = canvas != null ? (RectTransform)canvas.transform : (RectTransform)context.root;
            ColorPickerPopup.Open(pickerParent, initial, chosen =>
            {
                Apply(setting, "#" + ColorUtility.ToHtmlStringRGB(chosen));
                // Rebuild so the custom tile shows the colour that was picked - swatch fill is set
                // when it is created, not bound to the setting.
                RequestRebuild?.Invoke();
            });
        }

        private static bool IsCurrent(ConfigEntry<string> setting, string hex)
        {
            var current = (setting.Value ?? string.Empty).Trim();
            return string.Equals(current, (hex ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCustomValue(ConfigEntry<string> setting)
        {
            var current = (setting.Value ?? string.Empty).Trim();
            if (current.Length == 0)
            {
                return false;
            }

            foreach (var (_, hex) in Presets)
            {
                if (string.Equals(current, hex, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static void Apply(ConfigEntry<string> setting, string hex)
        {
            setting.Value = hex ?? string.Empty;
            OnColorChanged?.Invoke();
        }

        private static Color ParseOr(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return fallback;
            }

            return ColorUtility.TryParseHtmlString(hex, out var parsed) ? parsed : fallback;
        }

        private readonly struct SwatchEntry
        {
            internal SwatchEntry(ConfigEntry<string> setting, string hex, Image ring, TextMeshProUGUI check, bool isCustomTile)
            {
                Setting = setting;
                Hex = hex;
                Ring = ring;
                Check = check;
                IsCustomTile = isCustomTile;
            }

            internal ConfigEntry<string> Setting { get; }
            internal string Hex { get; }
            internal Image Ring { get; }
            internal TextMeshProUGUI Check { get; }
            internal bool IsCustomTile { get; }
        }
    }
}
