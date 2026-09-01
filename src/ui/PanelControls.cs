using System;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FangtasticPalette
{
    /// <summary>
    /// Reusable control-row builders + low-level RectTransform primitives for the Bat Form colour
    /// panel. These are the layout building blocks (a decorated-header label, an On/Off toggle pill, a
    /// value slider) that <see cref="BatFormColorPanel"/> composes into the panel and that
    /// <see cref="SwatchGrid"/> reuses (label + `Stretch`/`AddTrigger`). Each `Add*Row` returns the
    /// row's height so the panel can size its scroll content deterministically.
    /// </summary>
    internal static class PanelControls
    {
        /// <summary>
        /// A decorated header label (the game's swirl-flourish header when the template is available,
        /// else a plain left-aligned label), sized to <paramref name="height"/>.
        /// </summary>
        internal static void AddLabel(Transform parent, string text, float height)
        {
            // Prefer the game's decorated header (swirl flourishes + rule); fall back to a plain
            // left-aligned label if the template cannot be found.
            var decorated = HeaderDecoration.Create(parent, text);
            if (decorated != null)
            {
                var headerElement = decorated.GetComponent<LayoutElement>() ?? decorated.AddComponent<LayoutElement>();
                headerElement.preferredHeight = height;
                headerElement.minHeight = height;
                return;
            }

            var host = new GameObject("Label", typeof(RectTransform));
            host.transform.SetParent(parent, false);

            var label = host.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 32f;
            label.color = Palette.Label;
            label.alignment = TextAlignmentOptions.Left;
            label.raycastTarget = false;
            GameFonts.Apply(label, preferOutline: false);

            var element = host.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
        }

        /// <summary>
        /// A labelled On/Off toggle bound to a bool config entry - a pill that shows accent/"On" when
        /// set and dim/"Off" when clear. Writing the config raises SettingChanged, which the plugin
        /// routes to BatFormWardrobe.ApplyFlapFreeze, so the preview updates live. Returns row height.
        /// </summary>
        internal static float AddToggleRow(Transform parent, string label, ConfigEntry<bool> setting)
        {
            const float labelHeight = 40f;
            const float gap = 30f;
            const float toggleHeight = 56f;
            const float bottomPad = 16f;
            var rowHeight = labelHeight + gap + toggleHeight + bottomPad;

            var row = new GameObject($"Row_{label}", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rowLayout = row.AddComponent<VerticalLayoutGroup>();
            rowLayout.spacing = gap;
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childForceExpandWidth = true;
            var rowElement = row.AddComponent<LayoutElement>();
            rowElement.preferredHeight = rowHeight;
            rowElement.minHeight = rowHeight;

            AddLabel(row.transform, label, labelHeight);

            var host = new GameObject("ToggleHost", typeof(RectTransform));
            host.transform.SetParent(row.transform, false);
            var hostLayout = host.AddComponent<HorizontalLayoutGroup>();
            hostLayout.childAlignment = TextAnchor.MiddleCenter;
            hostLayout.childControlWidth = false;
            hostLayout.childControlHeight = false;
            var hostElement = host.AddComponent<LayoutElement>();
            hostElement.preferredHeight = toggleHeight;
            hostElement.minHeight = toggleHeight;

            var button = new GameObject("Toggle", typeof(RectTransform));
            button.transform.SetParent(host.transform, false);
            ((RectTransform)button.transform).sizeDelta = new Vector2(200f, toggleHeight);
            var bg = button.AddComponent<Image>();
            bg.sprite = PanelSprite.Get();
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;

            var textGo = new GameObject("State", typeof(RectTransform));
            textGo.transform.SetParent(button.transform, false);
            Stretch((RectTransform)textGo.transform, 0f);
            var stateText = textGo.AddComponent<TextMeshProUGUI>();
            stateText.fontSize = 30f;
            stateText.alignment = TextAlignmentOptions.Center;
            stateText.raycastTarget = false;
            GameFonts.Apply(stateText, preferOutline: true);

            void Paint()
            {
                var on = setting.Value;
                bg.color = on ? Palette.Accent : new Color(1f, 1f, 1f, 0.14f);
                stateText.text = on ? "<b>On</b>" : "Off";
                stateText.color = Palette.Label;
            }

            Paint();

            var trigger = button.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerClick, () =>
            {
                setting.Value = !setting.Value; // raises SettingChanged -> ApplyFlapFreeze
                Paint();
            });
            button.AddComponent<ScrollForwarder>();

            return rowHeight;
        }

        /// <summary>
        /// A labelled horizontal slider bound to a float config entry, with a live "x" readout. Writing
        /// the config raises SettingChanged (live apply) and <paramref name="onChanged"/> refreshes the
        /// preview. Returns row height.
        /// </summary>
        internal static float AddSliderRow(Transform parent, string label, ConfigEntry<float> setting,
            float min, float max, Action onChanged, Color? accent = null)
        {
            return AddSliderRow(parent, label, () => setting.Value, v => setting.Value = v, min, max, onChanged, accent);
        }

        /// <summary>
        /// Slider bound to a plain getter/setter rather than a ConfigEntry - for any control that is
        /// intentionally not persisted. Writing the setter does NOT raise Config.SettingChanged, so
        /// only the preview refreshes (via <paramref name="onChanged"/>), not the live player.
        /// Built from plain Images (solid track + accent fill) and a circular handle rather than a
        /// cloned game widget - the game has no slider in this screen to clone.
        /// </summary>
        internal static float AddSliderRow(Transform parent, string label, Func<float> get, Action<float> set,
            float min, float max, Action onChanged, Color? accent = null)
        {
            const float labelHeight = 40f;
            const float gap = 44f;
            const float sliderHeight = 40f;
            const float bottomPad = 20f;
            var rowHeight = labelHeight + gap + sliderHeight + bottomPad;

            var row = new GameObject($"Row_{label}", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rowLayout = row.AddComponent<VerticalLayoutGroup>();
            rowLayout.spacing = gap;
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childForceExpandWidth = true;
            var rowElement = row.AddComponent<LayoutElement>();
            rowElement.preferredHeight = rowHeight;
            rowElement.minHeight = rowHeight;

            AddLabel(row.transform, label, labelHeight);

            var host = new GameObject("SliderHost", typeof(RectTransform));
            host.transform.SetParent(row.transform, false);
            var hostLayout = host.AddComponent<HorizontalLayoutGroup>();
            hostLayout.childAlignment = TextAnchor.MiddleCenter;
            hostLayout.childControlWidth = true;
            hostLayout.childControlHeight = true;
            hostLayout.childForceExpandWidth = false;
            hostLayout.spacing = 24f;
            hostLayout.padding = new RectOffset(40, 40, 0, 0);
            var hostElement = host.AddComponent<LayoutElement>();
            hostElement.preferredHeight = sliderHeight;
            hostElement.minHeight = sliderHeight;

            var sliderGo = new GameObject("Slider", typeof(RectTransform));
            sliderGo.transform.SetParent(host.transform, false);
            var sliderLE = sliderGo.AddComponent<LayoutElement>();
            sliderLE.preferredHeight = 22f;
            sliderLE.minHeight = 22f;
            sliderLE.flexibleWidth = 1f;

            var track = new GameObject("Track", typeof(RectTransform));
            track.transform.SetParent(sliderGo.transform, false);
            ThinCenteredBar((RectTransform)track.transform);
            var trackImg = track.AddComponent<Image>();
            trackImg.color = new Color(1f, 1f, 1f, 0.16f);
            trackImg.raycastTarget = true;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            ThinCenteredBar((RectTransform)fillArea.transform);
            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.sizeDelta = new Vector2(0f, 0f);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = accent ?? Palette.Accent;
            fillImg.raycastTarget = false;

            // The Slider drives the handle's anchors to full vertical stretch every frame, so the
            // handle's HEIGHT comes from the slide area, not from the handle's own size. Make the
            // slide area a short centred bar (26px, inset by the handle radius each side) so the
            // handle stretches to a 26px circle instead of a tall oval.
            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            var handleAreaRect = (RectTransform)handleArea.transform;
            handleAreaRect.anchorMin = new Vector2(0f, 0.5f);
            handleAreaRect.anchorMax = new Vector2(1f, 0.5f);
            handleAreaRect.pivot = new Vector2(0.5f, 0.5f);
            handleAreaRect.sizeDelta = new Vector2(-26f, 26f);
            handleAreaRect.anchoredPosition = Vector2.zero;

            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(handleArea.transform, false);
            var handleRect = (RectTransform)handle.transform;
            handleRect.sizeDelta = new Vector2(26f, 0f); // width 26; height comes from the 26px slide area
            var handleImg = handle.AddComponent<Image>();
            handleImg.sprite = CircleSprite.Get();
            handleImg.color = accent ?? Palette.Label;

            var slider = sliderGo.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = false;
            slider.value = Mathf.Clamp(get(), min, max);

            var valueGo = new GameObject("Value", typeof(RectTransform));
            valueGo.transform.SetParent(host.transform, false);
            var valueLE = valueGo.AddComponent<LayoutElement>();
            valueLE.preferredWidth = 96f;
            valueLE.minWidth = 96f;
            var valueText = valueGo.AddComponent<TextMeshProUGUI>();
            valueText.text = $"{get():0.0}x";
            valueText.fontSize = 30f;
            valueText.color = Palette.Label;
            valueText.alignment = TextAlignmentOptions.Left;
            valueText.raycastTarget = false;
            GameFonts.Apply(valueText, preferOutline: false);

            slider.onValueChanged.AddListener(v =>
            {
                set(v);
                valueText.text = $"{v:0.0}x";
                onChanged?.Invoke();
            });

            return rowHeight;
        }

        /// <summary>Anchor-stretch a rect to fill its parent, inset by <paramref name="inset"/> on all sides.</summary>
        internal static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        // A full-width, vertically-centred thin bar - the slider track/fill, so the slider reads as
        // a slim line with a round handle rather than a fat block filling the whole row height.
        internal static void ThinCenteredBar(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0f, 12f);
            rect.anchoredPosition = Vector2.zero;
        }

        internal static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }
    }
}
