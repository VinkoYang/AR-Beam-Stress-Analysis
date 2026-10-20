using System;
using System.Collections.Generic;
using ARBeam;
using ARBeam.Mechanics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HoloMech
{
    /// <summary>
    /// All inputs on screen at once, with the beam re-solved as soon as anything changes (PI's request, 2026-10-20;
    /// the paper's step-by-step flow stays available as the Guided layout). Left panel: section and its dimensions,
    /// boundary conditions, length and material. Right panel: point and uniform loads. Each card can be collapsed.
    /// A banner at the top explains why the beam cannot be solved (e.g. unstable supports) instead of a blocking popup.
    /// </summary>
    public class WorkbenchLayout : MonoBehaviour
    {
        UiTheme t;
        HoloMechInputs inputs;
        Action changed;

        RectTransform dimsHost;
        Action<int> setSection, setLeft, setRight, setMaterial, setPointDir, setUniformDir;
        Slider length, modulus, pointPos, pointForce, uniStart, uniEnd, uniForce;
        TMP_Text lengthL, modulusL, pointPosL, pointForceL, uniStartL, uniEndL, uniForceL;
        Toggle pointOn, uniformOn;
        readonly List<(Slider s, TMP_Text l, Func<float> get)> dims = new List<(Slider, TMP_Text, Func<float>)>();
        GameObject banner;
        TMP_Text bannerText;
        bool syncing;
        RectTransform leftPanel, rightPanel;

        /// <summary>Raised when a side panel is collapsed or expanded (the camera re-frames).</summary>
        public event Action LayoutChanged;

        public bool LeftCollapsed { get; private set; }
        public bool RightCollapsed { get; private set; }

        /// <summary>Free area for the 3D view, as fractions of the screen width.</summary>
        public float FreeLeft => LeftCollapsed ? 0.04f : CentreLeft;
        public float FreeRight => RightCollapsed ? 0.96f : CentreRight;

        static readonly EndCondition[] Ends = { EndCondition.Free, EndCondition.Pinned, EndCondition.Fixed };
        static readonly MaterialPreset[] Presets = { MaterialPreset.Steel, MaterialPreset.Concrete, MaterialPreset.Wood, MaterialPreset.Custom };

        /// <summary>Left edge of the free centre area and right edge, as fractions of the 1920-wide reference.</summary>
        public const float CentreLeft = 600f / 1920f, CentreRight = 1320f / 1920f;

        public static WorkbenchLayout Create(UiTheme t, Transform canvas, HoloMechInputs inputs, Action changed)
        {
            var root = UiKit.Rect("Workbench", canvas);
            root.Stretch();
            var w = root.gameObject.AddComponent<WorkbenchLayout>();
            w.t = t;
            w.inputs = inputs;
            w.changed = changed;

            var left = w.SidePanel(root, "Beam Panel", true);
            w.leftPanel = left;
            w.BuildSection(w.Card(left, "Section"));
            w.BuildBoundary(w.Card(left, "Boundary Conditions"));
            w.BuildBeam(w.Card(left, "Length and Material"));

            var right = w.SidePanel(root, "Load Panel", false);
            w.rightPanel = right;
            w.CollapseButton(root, true);
            w.CollapseButton(root, false);
            var hint = UiKit.Label(t, right, "Hint", "Upward load is positive", t.regular, t.captionSize, t.textSecondary, TextAlignmentOptions.Left);
            Height(hint.gameObject, 26);
            w.BuildPoint(w.Card(right, "Point Load"));
            w.BuildUniform(w.Card(right, "Uniform Load"));

            var b = UiKit.Panel(t, root, "Error Banner", new Color(0.45f, 0.08f, 0.1f, 0.92f));
            b.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(700, 110));
            w.bannerText = UiKit.Label(t, b.transform, "Text", "", t.medium, t.captionSize + 2, Color.white, TextAlignmentOptions.MidlineLeft);
            w.bannerText.rectTransform.Stretch(new Vector2(20, 8), new Vector2(-20, -8));
            w.bannerText.textWrappingMode = TextWrappingModes.Normal;
            w.banner = b.gameObject;
            w.banner.SetActive(false);

            w.Sync();
            return w;
        }

        /// <summary>Shows or hides the problem banner.</summary>
        public void ShowProblems(IList<string> problems)
        {
            banner.SetActive(problems != null && problems.Count > 0);
            if (banner.activeSelf) bannerText.text = string.Join("\n", problems);
        }

        // ------------------------------------------------------------------ layout scaffolding

        RectTransform SidePanel(Transform root, string name, bool left)
        {
            var p = UiKit.Panel(t, root, name, t.panel);
            var r = p.rectTransform;
            r.anchorMin = new Vector2(left ? 0 : 1, 0);
            r.anchorMax = new Vector2(left ? 0 : 1, 1);
            r.pivot = new Vector2(left ? 0 : 1, 0.5f);
            r.offsetMin = new Vector2(left ? 24 : -584, 24);
            r.offsetMax = new Vector2(left ? 584 : -24, -24);
            var v = p.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(20, 20, 18, 18);
            v.spacing = 10;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            return r;
        }

        /// <summary>Small tab on the inner edge of a side panel that slides it away (or back).</summary>
        void CollapseButton(RectTransform root, bool left)
        {
            var b = UiKit.Button(t, root, left ? "Collapse Beam Panel" : "Collapse Load Panel", left ? "‹" : "›", 34);
            var r = b.GetComponent<RectTransform>();
            var label = b.GetComponentInChildren<TMP_Text>();
            void Place()
            {
                bool collapsed = left ? LeftCollapsed : RightCollapsed;
                float inner = collapsed ? 24 : 584;
                r.Place(new Vector2(left ? 0 : 1, 0.5f), new Vector2(left ? 0 : 1, 0.5f), new Vector2(left ? inner + 6 : -inner - 6, 0), new Vector2(40, 120));
                label.text = left ? (collapsed ? "›" : "‹") : (collapsed ? "‹" : "›");
            }

            Place();
            b.onClick.AddListener(() =>
            {
                if (left) LeftCollapsed = !LeftCollapsed; else RightCollapsed = !RightCollapsed;
                (left ? leftPanel : rightPanel).gameObject.SetActive(!(left ? LeftCollapsed : RightCollapsed));
                Place();
                LayoutChanged?.Invoke();
            });
        }

        /// <summary>Collapses or expands the side panels from code (screenshots, presenter mode).</summary>
        public void SetCollapsed(bool left, bool right)
        {
            LeftCollapsed = left;
            RightCollapsed = right;
            leftPanel.gameObject.SetActive(!left);
            rightPanel.gameObject.SetActive(!right);
            foreach (var b in GetComponentsInChildren<Button>(true))
            {
                if (b.name != "Collapse Beam Panel" && b.name != "Collapse Load Panel") continue;
                bool isLeft = b.name == "Collapse Beam Panel";
                bool collapsed = isLeft ? left : right;
                float inner = collapsed ? 24 : 584;
                b.GetComponent<RectTransform>().anchoredPosition = new Vector2(isLeft ? inner + 6 : -inner - 6, 0);
                b.GetComponentInChildren<TMP_Text>().text = isLeft ? (collapsed ? "›" : "‹") : (collapsed ? "‹" : "›");
            }

            LayoutChanged?.Invoke();
        }

        /// <summary>Collapsible card: a header button and a content column.</summary>
        RectTransform Card(RectTransform panel, string title)
        {
            var card = UiKit.Rect(title + " Card", panel);
            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;

            var header = UiKit.Button(t, card, "Header", "", t.labelSize);
            Height(header.gameObject, 46);
            var label = header.GetComponentInChildren<TMP_Text>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.rectTransform.Stretch(new Vector2(16, 0), new Vector2(-16, 0));
            var c = header.colors;
            c.normalColor = Color.Lerp(t.panel, t.panelRaised, 0.35f);
            header.colors = c;

            var content = UiKit.Rect("Content", card);
            var cv = content.gameObject.AddComponent<VerticalLayoutGroup>();
            cv.spacing = 6;
            cv.childControlHeight = true;
            cv.childControlWidth = true;
            cv.childForceExpandHeight = false;
            cv.childForceExpandWidth = true;

            void Title(bool open) => label.text = title + (open ? "   <color=#8FA3CC>−</color>" : "   <color=#8FA3CC>+</color>");
            Title(true);
            header.onClick.AddListener(() =>
            {
                content.gameObject.SetActive(!content.gameObject.activeSelf);
                Title(content.gameObject.activeSelf);
            });
            return content;
        }

        static void Height(GameObject go, float h)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            le.minHeight = h;
        }

        Slider Slider(RectTransform parent, string caption, float min, float max, float value, float step, string format, out TMP_Text label, Action<float> set)
        {
            var s = UiKit.LabeledSlider(t, parent, caption, caption, min, max, value, step, format, out label, v =>
            {
                if (syncing) return;
                set(v);
                changed?.Invoke();
            });
            Height(s.transform.parent.gameObject, 70);
            return s;
        }

        /// <summary>Caption on the left, segmented buttons on the right.</summary>
        Action<int> Choice(RectTransform parent, string caption, string[] options, int selected, Action<int> set)
        {
            var row = UiKit.Rect(string.IsNullOrEmpty(caption) ? "Choice" : caption, parent);
            Height(row.gameObject, 48);
            if (!string.IsNullOrEmpty(caption))
            {
                var l = UiKit.Label(t, row, "Caption", caption, t.medium, t.captionSize + 2, t.textPrimary, TextAlignmentOptions.MidlineLeft);
                l.rectTransform.anchorMin = new Vector2(0, 0);
                l.rectTransform.anchorMax = new Vector2(0, 1);
                l.rectTransform.pivot = new Vector2(0, 0.5f);
                l.rectTransform.sizeDelta = new Vector2(120, 0);
                l.rectTransform.anchoredPosition = Vector2.zero;
            }

            var setter = UiKit.Segmented(t, row, "Options", options, selected, i =>
            {
                if (syncing) return;
                set(i);
                changed?.Invoke();
            });
            var seg = (RectTransform)row.Find("Options");
            seg.anchorMin = Vector2.zero;
            seg.anchorMax = Vector2.one;
            seg.offsetMin = new Vector2(string.IsNullOrEmpty(caption) ? 0 : 128, 0);
            seg.offsetMax = Vector2.zero;
            return setter;
        }

        // ------------------------------------------------------------------ cards

        void BuildSection(RectTransform c)
        {
            setSection = Choice(c, "", new[] { "Pipe", "Box", "I-Beam" }, (int)inputs.section, i =>
            {
                inputs.section = (SectionType)i;
                BuildDimensions();
            });
            dimsHost = UiKit.Rect("Dimensions", c);
            var v = dimsHost.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 4;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            BuildDimensions();
        }

        void BuildDimensions()
        {
            for (int i = dimsHost.childCount - 1; i >= 0; i--)
            {
                var child = dimsHost.GetChild(i);
                child.SetParent(null);
                Destroy(child.gameObject);
            }

            dims.Clear();
            switch (inputs.section)
            {
                case SectionType.Pipe:
                    Dim("Outer diameter D (cm)", HoloMechRanges.PipeDiameterMinCm, HoloMechRanges.PipeDiameterMaxCm, () => inputs.pipe.outerDiameterCm, v => inputs.pipe.outerDiameterCm = v);
                    Dim("Wall thickness t (cm)", HoloMechRanges.PipeWallMinCm, HoloMechRanges.PipeWallMaxCm, () => inputs.pipe.wallCm, v => inputs.pipe.wallCm = v);
                    break;
                case SectionType.Box:
                    Dim("Width b (cm)", HoloMechRanges.BoxWidthMinCm, HoloMechRanges.BoxWidthMaxCm, () => inputs.box.widthCm, v => inputs.box.widthCm = v);
                    Dim("Height h (cm)", HoloMechRanges.BoxHeightMinCm, HoloMechRanges.BoxHeightMaxCm, () => inputs.box.heightCm, v => inputs.box.heightCm = v);
                    Dim("Wall t (cm), 0 = solid", HoloMechRanges.BoxWallMinCm, HoloMechRanges.BoxWallMaxCm, () => inputs.box.wallCm, v => inputs.box.wallCm = v);
                    break;
                default:
                    Dim("Height h (cm)", HoloMechRanges.IHeightMinCm, HoloMechRanges.IHeightMaxCm, () => inputs.iBeam.heightCm, v => inputs.iBeam.heightCm = v);
                    Dim("Flange width bf (cm)", HoloMechRanges.IFlangeWidthMinCm, HoloMechRanges.IFlangeWidthMaxCm, () => inputs.iBeam.flangeWidthCm, v => inputs.iBeam.flangeWidthCm = v);
                    Dim("Flange thickness tf (cm)", HoloMechRanges.IFlangeThicknessMinCm, HoloMechRanges.IFlangeThicknessMaxCm, () => inputs.iBeam.flangeThicknessCm, v => inputs.iBeam.flangeThicknessCm = v);
                    Dim("Web thickness tw (cm)", HoloMechRanges.IWebThicknessMinCm, HoloMechRanges.IWebThicknessMaxCm, () => inputs.iBeam.webThicknessCm, v => inputs.iBeam.webThicknessCm = v);
                    break;
            }

            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }

        void Dim(string caption, float min, float max, Func<float> get, Action<float> set)
        {
            TMP_Text label = null;
            Slider s = null;
            s = Slider(dimsHost, caption, min, max, get(), 0.1f, "{0:0.0}", out label, v =>
            {
                set(v);
                if (inputs.ClampSectionDimensions()) SyncDimensions();
            });
            dims.Add((s, label, get));
        }

        void SyncDimensions()
        {
            bool was = syncing;
            syncing = true;
            foreach (var (s, l, get) in dims) UiKit.SetSlider(s, l, "{0:0.0}", get());
            syncing = was;
        }

        void BuildBoundary(RectTransform c)
        {
            var names = new[] { "Free", "Pinned", "Fixed" };
            setLeft = Choice(c, "Left end", names, Array.IndexOf(Ends, inputs.leftEnd), i => inputs.leftEnd = Ends[i]);
            setRight = Choice(c, "Right end", names, Array.IndexOf(Ends, inputs.rightEnd), i => inputs.rightEnd = Ends[i]);
        }

        void BuildBeam(RectTransform c)
        {
            length = Slider(c, "Beam Length (cm)", HoloMechRanges.LengthMinCm, HoloMechRanges.LengthMaxCm, inputs.lengthCm, 1f, "{0:0}", out lengthL, v =>
            {
                inputs.lengthCm = v;
                inputs.ClampLoadsToLength();
                SyncLoads();
            });
            modulus = Slider(c, "Elastic Module (GPa)", HoloMechRanges.ModulusMinGPa, HoloMechRanges.ModulusMaxGPa, inputs.elasticModulusGPa, 1f, "{0:0}", out modulusL, v =>
            {
                inputs.elasticModulusGPa = v;
                inputs.material = MaterialPreset.Custom;
                setMaterial(3);
            });
            setMaterial = Choice(c, "", new[] { "Steel", "Concrete", "Wood", "Custom" }, Array.IndexOf(Presets, inputs.material), i =>
            {
                inputs.ApplyMaterial(Presets[i]);
                bool was = syncing;
                syncing = true;
                UiKit.SetSlider(modulus, modulusL, "{0:0}", inputs.elasticModulusGPa);
                syncing = was;
            });
        }

        void BuildPoint(RectTransform c)
        {
            pointOn = UiKit.Checkbox(t, c, "Enabled", "Apply a point load", inputs.pointLoad.enabled, null, t.captionSize + 2);
            Height(pointOn.gameObject, 48);
            pointOn.onValueChanged.AddListener(on => { if (syncing) return; inputs.pointLoad.enabled = on; Interactable(); changed?.Invoke(); });
            pointPos = Slider(c, "Distance from left end (cm)", 0, inputs.lengthCm, inputs.pointLoad.positionCm, 1f, "{0:0}", out pointPosL, v => inputs.pointLoad.positionCm = v);
            pointForce = Slider(c, "Force Magnitude (N)", 0, HoloMechRanges.ForceMaxN, inputs.pointLoad.forceN, 10f, "{0:0}", out pointForceL, v => inputs.pointLoad.forceN = v);
            setPointDir = Choice(c, "Direction", new[] { "Up  ↑", "Down  ↓" }, inputs.pointLoad.direction == LoadDirection.Up ? 0 : 1,
                i => inputs.pointLoad.direction = i == 0 ? LoadDirection.Up : LoadDirection.Down);
        }

        void BuildUniform(RectTransform c)
        {
            uniformOn = UiKit.Checkbox(t, c, "Enabled", "Apply a uniform load", inputs.uniformLoad.enabled, null, t.captionSize + 2);
            Height(uniformOn.gameObject, 48);
            uniformOn.onValueChanged.AddListener(on => { if (syncing) return; inputs.uniformLoad.enabled = on; Interactable(); changed?.Invoke(); });
            uniStart = Slider(c, "Start from left end (cm)", 0, inputs.lengthCm, inputs.uniformLoad.startCm, 1f, "{0:0}", out uniStartL, v =>
            {
                inputs.uniformLoad.startCm = v;
                if (inputs.uniformLoad.endCm <= v) inputs.uniformLoad.endCm = Mathf.Min(v + 1f, inputs.lengthCm);
                SyncLoads();
            });
            uniEnd = Slider(c, "End from left end (cm)", 0, inputs.lengthCm, inputs.uniformLoad.endCm, 1f, "{0:0}", out uniEndL, v =>
            {
                inputs.uniformLoad.endCm = v;
                if (inputs.uniformLoad.startCm >= v) inputs.uniformLoad.startCm = Mathf.Max(v - 1f, 0f);
                SyncLoads();
            });
            uniForce = Slider(c, "Intensity (N/m)", 0, HoloMechRanges.UniformMaxNPerM, inputs.uniformLoad.intensityNPerM, 10f, "{0:0}", out uniForceL, v => inputs.uniformLoad.intensityNPerM = v);
            setUniformDir = Choice(c, "Direction", new[] { "Up  ↑", "Down  ↓" }, inputs.uniformLoad.direction == LoadDirection.Up ? 0 : 1,
                i => inputs.uniformLoad.direction = i == 0 ? LoadDirection.Up : LoadDirection.Down);
        }

        void Interactable()
        {
            pointPos.interactable = pointForce.interactable = inputs.pointLoad.enabled;
            uniStart.interactable = uniEnd.interactable = uniForce.interactable = inputs.uniformLoad.enabled;
        }

        void SyncLoads()
        {
            bool was = syncing;
            syncing = true;
            pointPos.maxValue = uniStart.maxValue = uniEnd.maxValue = inputs.lengthCm;
            UiKit.SetSlider(pointPos, pointPosL, "{0:0}", inputs.pointLoad.positionCm);
            UiKit.SetSlider(uniStart, uniStartL, "{0:0}", inputs.uniformLoad.startCm);
            UiKit.SetSlider(uniEnd, uniEndL, "{0:0}", inputs.uniformLoad.endCm);
            syncing = was;
        }

        /// <summary>Refreshes every control from the inputs (after the guided flow changed them).</summary>
        public void Sync()
        {
            syncing = true;
            setSection((int)inputs.section);
            BuildDimensions();
            setLeft(Array.IndexOf(Ends, inputs.leftEnd));
            setRight(Array.IndexOf(Ends, inputs.rightEnd));
            setMaterial(Array.IndexOf(Presets, inputs.material));
            UiKit.SetSlider(length, lengthL, "{0:0}", inputs.lengthCm);
            UiKit.SetSlider(modulus, modulusL, "{0:0}", inputs.elasticModulusGPa);
            pointOn.isOn = inputs.pointLoad.enabled;
            uniformOn.isOn = inputs.uniformLoad.enabled;
            UiKit.SetSlider(pointForce, pointForceL, "{0:0}", inputs.pointLoad.forceN);
            UiKit.SetSlider(uniForce, uniForceL, "{0:0}", inputs.uniformLoad.intensityNPerM);
            setPointDir(inputs.pointLoad.direction == LoadDirection.Up ? 0 : 1);
            setUniformDir(inputs.uniformLoad.direction == LoadDirection.Up ? 0 : 1);
            syncing = false;
            SyncLoads();
            Interactable();
        }
    }
}
