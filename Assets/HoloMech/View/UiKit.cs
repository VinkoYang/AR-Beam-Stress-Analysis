using System;
using ARBeam;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HoloMech
{
    /// <summary>
    /// Builds HoloMech's controls from code with the shared <see cref="UiTheme"/> (Inter, navy panels, blue accent),
    /// so panels do not depend on prefab files and can be created for screen space or world space alike.
    /// Layout is always by anchors.
    /// </summary>
    public static class UiKit
    {
        /// <summary>
        /// When set (Quest build), controls are instantiated from Meta's UI Set prefabs instead of drawn by UiKit.
        /// Layout and the returned Unity UI components (Button, Toggle, Slider) are the same either way.
        /// </summary>
        public static MetaUiSkin Skin;

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            return r;
        }

        /// <summary>Anchor and pivot at one point.</summary>
        public static RectTransform Place(this RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            r.anchorMin = anchor;
            r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = position;
            r.sizeDelta = size;
            return r;
        }

        public static RectTransform Stretch(this RectTransform r, Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = offsetMin;
            r.offsetMax = offsetMax;
            return r;
        }

        public static Canvas ScreenCanvas(string name, Transform parent = null)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            // Expand: the whole 1920x1080 reference area is always visible, at any aspect ratio.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            return canvas;
        }

        /// <summary>Makes sure the scene can receive pointer input (mouse in the Editor, XR UI input module on devices).</summary>
        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        public static Image Panel(UiTheme t, Transform parent, string name, Color color)
        {
            var r = Rect(name, parent);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = t.roundedRect;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI Label(UiTheme t, Transform parent, string name, string text, TMP_FontAsset font, float size,
            Color color, TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var r = Rect(name, parent);
            var tmp = r.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.font = font;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static Button Button(UiTheme t, Transform parent, string name, string text, float fontSize = 0)
        {
            if (Skin != null && (Skin.secondaryButton != null || Skin.primaryButton != null)) return MetaButton(t, parent, name, text, fontSize);
            var img = Panel(t, parent, name, Color.white);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var c = b.colors;
            c.normalColor = t.panelRaised;
            c.highlightedColor = t.panelHover;
            c.pressedColor = t.accent;
            c.selectedColor = t.panelRaised;
            c.disabledColor = new Color(0.4f, 0.4f, 0.45f, 0.5f);
            b.colors = c;
            var label = Label(t, img.transform, "Text", text, t.semiBold, fontSize > 0 ? fontSize : t.buttonSize, t.textPrimary, TextAlignmentOptions.Center);
            label.rectTransform.Stretch();
            return b;
        }

        /// <summary>A row-sized switch: caption on the left, pill switch on the right. Returns the Toggle.</summary>
        public static Toggle Switch(UiTheme t, Transform parent, string name, string caption, bool on, Color? rowColor = null)
        {
            if (Skin != null && Skin.switchToggle != null)
                return MetaToggleRow(t, parent, name, caption, on, Skin.switchToggle, null, 0, new Vector2(40, 24), 1.6f);
            var row = Panel(t, parent, name, rowColor ?? t.panelRaised);
            row.raycastTarget = true;
            var label = Label(t, row.transform, "Caption", caption, t.medium, t.labelSize, t.textPrimary, TextAlignmentOptions.MidlineLeft);
            label.rectTransform.Stretch(new Vector2(20, 0), new Vector2(-96, 0));
            label.textWrappingMode = TextWrappingModes.Normal;
            label.enableAutoSizing = true;
            label.fontSizeMax = t.labelSize;
            label.fontSizeMin = 14;

            var track = Panel(t, row.transform, "Track", on ? t.accent : t.track);
            track.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(64, 34));
            var knob = Panel(t, track.transform, "Knob", Color.white);
            knob.sprite = t.circle;
            knob.type = Image.Type.Simple;
            knob.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(on ? 47 : 17, 0), new Vector2(26, 26));

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = row;
            toggle.transition = Selectable.Transition.None;
            toggle.isOn = on;
            toggle.onValueChanged.AddListener(v => Style(v));
            Style(on);
            return toggle;

            void Style(bool value)
            {
                track.color = value ? t.accent : t.track;
                knob.rectTransform.anchoredPosition = new Vector2(value ? 47 : 17, 0);
                row.color = value ? Color.Lerp(t.panelRaised, t.panelHover, 0.6f) : (rowColor ?? t.panelRaised);
            }
        }

        /// <summary>Horizontal slider with a track, accent fill and round handle. Linear or logarithmic.</summary>
        public static Slider Slider(UiTheme t, Transform parent, string name, float min, float max, float value)
        {
            if (Skin != null && Skin.slider != null) return MetaSlider(parent, name, min, max, value);
            var root = Rect(name, parent);
            root.gameObject.AddComponent<RectTransform>();

            var bg = Rect("Background", root);
            bg.anchorMin = new Vector2(0, 0.5f);
            bg.anchorMax = new Vector2(1, 0.5f);
            bg.sizeDelta = new Vector2(0, 10);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.sprite = t.roundedRect;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = t.track;

            var fillArea = Rect("Fill Area", root);
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.offsetMin = new Vector2(14, -5);
            fillArea.offsetMax = new Vector2(-14, 5);
            var fill = Rect("Fill", fillArea);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(-14, 0);
            fill.offsetMax = Vector2.zero;
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.sprite = t.roundedRect;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = t.accent;

            var handleArea = Rect("Handle Slide Area", root).Stretch(new Vector2(14, 0), new Vector2(-14, 0));
            var handle = Rect("Handle", handleArea);
            handle.sizeDelta = new Vector2(30, 0);
            handle.anchorMin = new Vector2(0, 0);
            handle.anchorMax = new Vector2(0, 1);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.sprite = t.circle;
            handleImg.color = Color.white;

            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill;
            s.handleRect = handle;
            s.targetGraphic = handleImg;
            s.minValue = min;
            s.maxValue = max;
            s.value = value;
            return s;
        }

        /// <summary>
        /// Row with a check box on the right. Rows sharing a <see cref="ToggleGroup"/> are mutually exclusive
        /// (the paper's section, boundary and force-direction choices).
        /// </summary>
        public static Toggle Checkbox(UiTheme t, Transform parent, string name, string caption, bool on, ToggleGroup group = null, float fontSize = 0)
        {
            if (Skin != null && Skin.checkbox != null)
                return MetaToggleRow(t, parent, name, caption, on, Skin.checkbox, group, fontSize, new Vector2(16, 16), 2.2f);
            var row = Panel(t, parent, name, t.panelRaised);
            row.raycastTarget = true;
            var label = Label(t, row.transform, "Caption", caption, t.medium, fontSize > 0 ? fontSize : t.labelSize, t.textPrimary, TextAlignmentOptions.MidlineLeft);
            label.rectTransform.Stretch(new Vector2(20, 0), new Vector2(-70, 0));

            var box = Panel(t, row.transform, "Box", t.track);
            box.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(34, 34));
            var tick = Panel(t, box.transform, "Tick", t.accent);
            tick.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20));

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = row;
            toggle.transition = Selectable.Transition.None;
            toggle.group = group;
            toggle.isOn = on;
            toggle.onValueChanged.AddListener(v => Style(v));
            Style(on);
            return toggle;

            void Style(bool value)
            {
                tick.enabled = value;
                row.color = value ? Color.Lerp(t.panelRaised, t.panelHover, 0.7f) : t.panelRaised;
            }
        }

        /// <summary>
        /// Caption (left) and live value (right) above a slider. <paramref name="step"/> rounds the value
        /// (0 = continuous). Returns the slider; <paramref name="valueLabel"/> shows the formatted value.
        /// </summary>
        public static Slider LabeledSlider(UiTheme t, Transform parent, string name, string caption, float min, float max, float value,
            float step, string format, out TMP_Text valueLabel, Action<float> onChanged)
        {
            var root = Rect(name, parent);
            var cap = Label(t, root, "Caption", caption, t.medium, t.labelSize - 2, t.textPrimary, TextAlignmentOptions.TopLeft);
            cap.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(520, 34));
            var val = Label(t, root, "Value", "", t.semiBold, t.valueSize - 2, t.textPrimary, TextAlignmentOptions.TopRight);
            val.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(220, 34));
            valueLabel = val;

            var slider = Slider(t, root, "Slider", min, max, value);
            var sr = (RectTransform)slider.transform;
            sr.anchorMin = new Vector2(0, 0);
            sr.anchorMax = new Vector2(1, 0);
            sr.pivot = new Vector2(0.5f, 0);
            sr.offsetMin = new Vector2(0, 0);
            sr.offsetMax = new Vector2(0, 32);

            void Show(float v) => val.text = string.Format(System.Globalization.CultureInfo.InvariantCulture, format, v);
            slider.onValueChanged.AddListener(v =>
            {
                if (step > 0)
                {
                    float snapped = Mathf.Round(v / step) * step;
                    if (!Mathf.Approximately(snapped, v))
                    {
                        slider.SetValueWithoutNotify(snapped);
                        v = snapped;
                    }
                }

                Show(v);
                onChanged?.Invoke(v);
            });
            Show(value);
            return slider;
        }

        /// <summary>Sets a slider without firing events and refreshes its value label (for programmatic changes).</summary>
        public static void SetSlider(Slider s, TMP_Text label, string format, float value)
        {
            s.SetValueWithoutNotify(value);
            label.text = string.Format(System.Globalization.CultureInfo.InvariantCulture, format, value);
        }

        /// <summary>
        /// Row of mutually exclusive buttons (segmented control). Returns a setter that highlights an index without
        /// raising <paramref name="onChanged"/>.
        /// </summary>
        public static Action<int> Segmented(UiTheme t, Transform parent, string name, string[] options, int selected, Action<int> onChanged)
        {
            if (Skin != null && Skin.choiceButton != null) return MetaSegmented(t, parent, name, options, selected, onChanged);
            var row = Rect(name, parent);
            var buttons = new Button[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                int k = i;
                var b = Button(t, row, options[i], options[i], t.captionSize + 2);
                var r = b.GetComponent<RectTransform>();
                r.anchorMin = new Vector2((float)i / options.Length, 0);
                r.anchorMax = new Vector2((float)(i + 1) / options.Length, 1);
                r.offsetMin = new Vector2(i == 0 ? 0 : 4, 0);
                r.offsetMax = new Vector2(i == options.Length - 1 ? 0 : -4, 0);
                b.onClick.AddListener(() => { Highlight(k); onChanged?.Invoke(k); });
                buttons[i] = b;
            }

            Highlight(selected);
            return Highlight;

            void Highlight(int index)
            {
                for (int i = 0; i < buttons.Length; i++)
                {
                    var c = buttons[i].colors;
                    c.normalColor = i == index ? t.accent : t.panelRaised;
                    c.selectedColor = c.normalColor;
                    c.highlightedColor = i == index ? t.accent : t.panelHover;
                    buttons[i].colors = c;
                }
            }
        }

        // ------------------------------------------------------------------ Meta UI Set (Quest)

        static GameObject Spawn(GameObject prefab, Transform parent, string name)
        {
            var go = UnityEngine.Object.Instantiate(prefab, parent, false);
            go.name = name;
            foreach (var tr in go.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = parent.gameObject.layer;
            return go;
        }

        /// <summary>
        /// UI Set buttons are laid out for a 133x48 button with a 14-px label beside an icon. Our panels size buttons
        /// by anchors (46-px rows, tall side tabs), so the inner layout groups are switched off, the plate stretches
        /// to the button and the label fills it, centred, at our font size. The placeholder icon is hidden.
        /// </summary>
        static void FitButton(GameObject go, string text, float fontSize)
        {
            foreach (var group in go.GetComponentsInChildren<LayoutGroup>(true)) group.enabled = false;
            foreach (var element in go.GetComponentsInChildren<LayoutElement>(true))
                if (element.gameObject != go) element.ignoreLayout = true;

            TMP_Text label = null;
            foreach (var tr in go.GetComponentsInChildren<RectTransform>(true))
            {
                if (tr.gameObject == go) continue;
                switch (tr.name)
                {
                    case "Content":
                    case "Background":
                    case "Text":
                        tr.Stretch();
                        break;
                    case "Elements":
                        tr.Stretch(new Vector2(8, 0), new Vector2(-8, 0));
                        break;
                    case "Icon":
                    case "Gap":
                    case "Space":
                    case "Subtitle":
                    case "Label (1)":
                        tr.gameObject.SetActive(false);
                        break;
                    case "Label":
                        label = tr.GetComponent<TMP_Text>();
                        break;
                }
            }

            if (label == null) label = go.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;
            label.rectTransform.Stretch();
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMax = fontSize;
            label.fontSizeMin = 12;
            label.fontSize = fontSize;
        }

        static Button MetaButton(UiTheme t, Transform parent, string name, string text, float fontSize)
        {
            // Grey (secondary) plates for the many panel buttons; the white primary plate is Quest's single call to action.
            var go = Spawn(Skin.secondaryButton != null ? Skin.secondaryButton : Skin.primaryButton, parent, name);
            FitButton(go, text, fontSize > 0 ? fontSize : t.buttonSize);
            return go.GetComponent<Button>();
        }

        /// <summary>Caption row (our panel and label) with a UI Set switch or check box on the right.</summary>
        static Toggle MetaToggleRow(UiTheme t, Transform parent, string name, string caption, bool on, GameObject prefab, ToggleGroup group,
            float fontSize, Vector2 nativeSize, float scale)
        {
            var row = Panel(t, parent, name, t.panelRaised);
            var label = Label(t, row.transform, "Caption", caption, t.medium, fontSize > 0 ? fontSize : t.labelSize, t.textPrimary, TextAlignmentOptions.MidlineLeft);
            label.rectTransform.Stretch(new Vector2(20, 0), new Vector2(-96, 0));
            label.textWrappingMode = TextWrappingModes.Normal;
            label.enableAutoSizing = true;
            label.fontSizeMax = fontSize > 0 ? fontSize : t.labelSize;
            label.fontSizeMin = 14;

            var go = Spawn(prefab, row.transform, "Control");
            var r = (RectTransform)go.transform;
            r.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), nativeSize);
            r.localScale = Vector3.one * scale * Skin.controlScale;
            var inner = go.GetComponent<Toggle>();
            inner.isOn = on;

            // The returned Toggle sits on the row (callers lay out and group the row); the UI Set control mirrors it,
            // and a click on either changes both.
            row.raycastTarget = true;
            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = row;
            toggle.transition = Selectable.Transition.None;
            toggle.group = group;
            toggle.isOn = on;
            bool syncing = false;
            toggle.onValueChanged.AddListener(v =>
            {
                if (syncing) return;
                syncing = true;
                inner.isOn = v;
                syncing = false;
            });
            inner.onValueChanged.AddListener(v =>
            {
                if (syncing) return;
                syncing = true;
                toggle.isOn = v;
                if (toggle.isOn != v) inner.isOn = toggle.isOn; // a group refused to switch the row off
                syncing = false;
            });
            return toggle;
        }

        /// <summary>Marks one button of a set as chosen (accent colour; with the Meta skin, the others are dimmed).</summary>
        public static void SetHighlighted(UiTheme t, Button b, bool on)
        {
            if (Skin != null && (Skin.secondaryButton != null || Skin.primaryButton != null))
            {
                var group = b.GetComponent<CanvasGroup>();
                if (group == null) group = b.gameObject.AddComponent<CanvasGroup>();
                group.alpha = on ? 1f : 0.45f;
                return;
            }

            var c = b.colors;
            c.normalColor = on ? t.accent : t.panelRaised;
            c.selectedColor = c.normalColor;
            b.colors = c;
            var img = b.GetComponent<Image>();
            if (img != null) img.color = Color.white;
        }

        static Slider MetaSlider(Transform parent, string name, float min, float max, float value)
        {
            var go = Spawn(Skin.slider, parent, name);
            var s = go.GetComponent<Slider>();
            s.minValue = min;
            s.maxValue = max;
            s.SetValueWithoutNotify(value);
            return s;
        }

        static Action<int> MetaSegmented(UiTheme t, Transform parent, string name, string[] options, int selected, Action<int> onChanged)
        {
            var row = Rect(name, parent);
            var group = row.gameObject.AddComponent<ToggleGroup>();
            group.allowSwitchOff = false;
            var toggles = new Toggle[options.Length];
            bool quiet = false;
            for (int i = 0; i < options.Length; i++)
            {
                int k = i;
                var go = Spawn(Skin.choiceButton, row, options[i]);
                FitButton(go, options[i], t.captionSize + 2);
                var r = (RectTransform)go.transform;
                r.anchorMin = new Vector2((float)i / options.Length, 0);
                r.anchorMax = new Vector2((float)(i + 1) / options.Length, 1);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.offsetMin = new Vector2(i == 0 ? 0 : 3, 0);
                r.offsetMax = new Vector2(i == options.Length - 1 ? 0 : -3, 0);
                var tg = go.GetComponent<Toggle>();
                tg.group = group;
                tg.isOn = i == selected;
                tg.onValueChanged.AddListener(on => { if (on && !quiet) onChanged?.Invoke(k); });
                toggles[i] = tg;
            }

            return index =>
            {
                quiet = true;
                for (int i = 0; i < toggles.Length; i++) toggles[i].isOn = i == index;
                quiet = false;
            };
        }

        /// <summary>Slider position 0..1 ↔ value on a log scale (for the deflection scale).</summary>
        public static float LogToValue(float t01, float min, float max) => Mathf.Exp(Mathf.Lerp(Mathf.Log(min), Mathf.Log(max), t01));

        public static float ValueToLog(float value, float min, float max)
            => Mathf.InverseLerp(Mathf.Log(min), Mathf.Log(max), Mathf.Log(Mathf.Clamp(value, min, max)));
    }
}
