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
    /// supports, length and material. Right panel: point loads and uniform loads. Each card can be collapsed.
    /// A banner at the top explains why the beam cannot be solved (e.g. unstable supports) instead of a blocking popup.
    ///
    /// Supports, point loads and uniform loads are lists (PI's request, 2026-10-28): "+ Add" appends one, each entry
    /// is a one-line summary that expands to its controls, and its × asks for confirmation before deleting. Supports
    /// can sit anywhere along the beam, so continuous and propped beams can be built. The panels scroll when the lists
    /// grow taller than the panel.
    /// </summary>
    public class WorkbenchLayout : MonoBehaviour
    {
        UiTheme t;
        HoloMechInputs inputs;
        Action changed;

        RectTransform dimsHost, supportsHost, pointsHost, uniformsHost;
        Button addSupport, addPoint, addUniform;
        Action<int> setSection, setMaterial;
        Slider length, modulus;
        TMP_Text lengthL, modulusL;
        readonly List<(Slider s, TMP_Text l, Func<float> get)> dims = new List<(Slider, TMP_Text, Func<float>)>();

        /// <summary>Refreshes the open list entry's sliders and every entry's summary line (after the length changed).</summary>
        readonly List<Action> itemSyncs = new List<Action>();

        int openSupport = -1, openPoint = 0, openUniform = -1;
        GameObject banner;
        TMP_Text bannerText;
        bool syncing;
        RectTransform leftPanel, rightPanel, leftContent, rightContent;

        /// <summary>Raised when a side panel is collapsed or expanded (the camera re-frames).</summary>
        public event Action LayoutChanged;

        /// <summary>Raised when the Gantry preset was pressed (the on-site card repeats the note below).</summary>
        public event Action GantryApplied;

        /// <summary>The Gantry preset uses the girder measured on 2026-11-11 (see <see cref="HoloMechInputs.ApplyLabGantryDimensions"/>).</summary>
        public const string GantryDefaultsNote = "Lab gantry 2026-11-11: 2438 mm, I 160 × 85 mm, pins 2083 mm apart, trolley 4450 N at midspan";

        TMP_Text gantryNote;

        public bool LeftCollapsed { get; private set; }
        public bool RightCollapsed { get; private set; }

        /// <summary>The side panels (the Quest window moves them onto their own grabbable canvases).</summary>
        public RectTransform LeftPanel => leftPanel;
        public RectTransform RightPanel => rightPanel;

        /// <summary>Quest window: the panels are moved by hand, so the collapse tabs are not shown.</summary>
        public void HideCollapseButtons()
        {
            foreach (var name in new[] { "Collapse Beam Panel", "Collapse Load Panel" })
            {
                var b = transform.Find(name);
                if (b != null) b.gameObject.SetActive(false);
            }
        }

        /// <summary>Free area for the 3D view, as fractions of the screen width.</summary>
        public float FreeLeft => LeftCollapsed ? 0.04f : CentreLeft;
        public float FreeRight => RightCollapsed ? 0.96f : CentreRight;

        static readonly MaterialPreset[] Presets = { MaterialPreset.Steel, MaterialPreset.Concrete, MaterialPreset.Wood, MaterialPreset.Aluminum, MaterialPreset.Custom };

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

            w.leftPanel = w.SidePanel(root, "Beam Panel", true, out w.leftContent);
            w.BuildSection(w.Card(w.leftContent, "Section"));
            w.BuildSupports(w.Card(w.leftContent, "Supports"));
            w.BuildBeam(w.Card(w.leftContent, "Length and Material"));

            w.rightPanel = w.SidePanel(root, "Load Panel", false, out w.rightContent);
            w.CollapseButton(root, true);
            w.CollapseButton(root, false);
            var hint = UiKit.Label(t, w.rightContent, "Hint", "Upward load is positive", t.regular, t.captionSize, t.textSecondary, TextAlignmentOptions.Left);
            Height(hint.gameObject, 26);
            w.BuildPoints(w.Card(w.rightContent, "Point Loads"));
            w.BuildUniforms(w.Card(w.rightContent, "Uniform Loads"));

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

        /// <summary>
        /// Adds a collapsible card at the bottom of a side panel and returns its content column, so other components
        /// (the MR on-site placement) can put their controls next to the inputs.
        /// </summary>
        public RectTransform AddCard(bool left, string title) => Card(left ? leftContent : rightContent, title);

        // ------------------------------------------------------------------ layout scaffolding

        /// <summary>Side panel with a vertical scroll view; returns the panel, <paramref name="content"/> takes the cards.</summary>
        RectTransform SidePanel(Transform root, string name, bool left, out RectTransform content)
        {
            var p = UiKit.Panel(t, root, name, t.panel);
            var r = p.rectTransform;
            r.anchorMin = new Vector2(left ? 0 : 1, 0);
            r.anchorMax = new Vector2(left ? 0 : 1, 1);
            r.pivot = new Vector2(left ? 0 : 1, 0.5f);
            r.offsetMin = new Vector2(left ? 24 : -584, 24);
            r.offsetMax = new Vector2(left ? 584 : -24, -24);

            var viewport = UiKit.Rect("Viewport", r);
            viewport.Stretch(new Vector2(0, 8), new Vector2(0, -8));
            viewport.gameObject.AddComponent<RectMask2D>();
            // A transparent graphic so a drag on empty space between cards scrolls the panel.
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            content = UiKit.Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(20, 20, 10, 18);
            v.spacing = 10;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = p.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
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

            var content = Column(card, "Content", 6);

            void Title(bool open) => label.text = title + (open ? "   <color=#8FA3CC>−</color>" : "   <color=#8FA3CC>+</color>");
            Title(true);
            header.onClick.AddListener(() =>
            {
                content.gameObject.SetActive(!content.gameObject.activeSelf);
                Title(content.gameObject.activeSelf);
            });
            return content;
        }

        static RectTransform Column(Transform parent, string name, float spacing)
        {
            var col = UiKit.Rect(name, parent);
            var cv = col.gameObject.AddComponent<VerticalLayoutGroup>();
            cv.spacing = spacing;
            cv.childControlHeight = true;
            cv.childControlWidth = true;
            cv.childForceExpandHeight = false;
            cv.childForceExpandWidth = true;
            return col;
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

        /// <summary>A full-width button row.</summary>
        Button RowButton(RectTransform parent, string name, string text, Action onClick)
        {
            var b = UiKit.Button(t, parent, name, text, t.captionSize + 2);
            Height(b.gameObject, 48);
            b.onClick.AddListener(() => onClick());
            return b;
        }

        /// <summary>Buttons side by side in one row (presets).</summary>
        void ButtonRow(RectTransform parent, string name, string[] captions, Action<int> onClick)
        {
            var row = UiKit.Rect(name, parent);
            Height(row.gameObject, 48);
            for (int i = 0; i < captions.Length; i++)
            {
                int k = i;
                var b = UiKit.Button(t, row, captions[i], captions[i], t.captionSize - 2);
                var rt = b.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2((float)i / captions.Length, 0);
                rt.anchorMax = new Vector2((float)(i + 1) / captions.Length, 1);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(i == 0 ? 0 : 3, 0);
                rt.offsetMax = new Vector2(i == captions.Length - 1 ? 0 : -3, 0);
                b.onClick.AddListener(() => onClick(k));
            }
        }

        // ------------------------------------------------------------------ list entries

        /// <summary>
        /// One list entry: a summary line that opens or closes the entry, a × that turns the line into
        /// "Delete …? [Delete] [Cancel]", and (when open) the entry's controls built by <paramref name="body"/>.
        /// </summary>
        void Entry(RectTransform host, string id, Func<string> summary, bool open, Action toggle, Action<RectTransform> body, Action delete)
        {
            var item = Column(host, id, 4);

            var row = UiKit.Rect("Row", item);
            Height(row.gameObject, 48);
            var head = UiKit.Button(t, row, "Summary", "", t.captionSize + 2);
            var hr = head.GetComponent<RectTransform>();
            hr.anchorMin = Vector2.zero;
            hr.anchorMax = Vector2.one;
            hr.offsetMin = Vector2.zero;
            hr.offsetMax = new Vector2(-56, 0);
            var headLabel = head.GetComponentInChildren<TMP_Text>();
            headLabel.alignment = TextAlignmentOptions.MidlineLeft;
            headLabel.rectTransform.Stretch(new Vector2(14, 0), new Vector2(-8, 0));
            void Refresh() => headLabel.text = (open ? "−  " : "+  ") + summary();
            Refresh();
            head.onClick.AddListener(() => toggle());

            var del = UiKit.Button(t, row, "Delete", "×", t.captionSize + 6);
            del.GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(48, 48));

            // Confirmation, in place of the summary line.
            var confirm = UiKit.Panel(t, row, "Confirm", new Color(0.45f, 0.08f, 0.1f, 0.95f));
            confirm.rectTransform.Stretch();
            var question = UiKit.Label(t, confirm.transform, "Question", $"Delete {id}?", t.medium, t.captionSize + 2, Color.white, TextAlignmentOptions.MidlineLeft);
            question.rectTransform.Stretch(new Vector2(14, 0), new Vector2(-260, 0));
            var yes = UiKit.Button(t, confirm.transform, "Confirm Delete", "Delete", t.captionSize + 2);
            yes.GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-130, 0), new Vector2(122, 44));
            var no = UiKit.Button(t, confirm.transform, "Cancel Delete", "Cancel", t.captionSize + 2);
            no.GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(122, 44));
            confirm.gameObject.SetActive(false);
            del.onClick.AddListener(() => confirm.gameObject.SetActive(true));
            no.onClick.AddListener(() => confirm.gameObject.SetActive(false));
            yes.onClick.AddListener(() => delete());

            itemSyncs.Add(Refresh);
            if (open)
            {
                var b = Column(item, "Body", 4);
                var pad = b.GetComponent<VerticalLayoutGroup>();
                pad.padding = new RectOffset(18, 0, 0, 6);
                body(b);
            }
        }

        static void Clear(RectTransform host)
        {
            for (int i = host.childCount - 1; i >= 0; i--)
            {
                var child = host.GetChild(i);
                child.SetParent(null);
                Destroy(child.gameObject);
            }
        }

        static string Arrow(LoadDirection d) => d == LoadDirection.Up ? "↑" : "↓";

        /// <summary>Rebuilds all three lists (after add, delete, open/close, presets or a sync from the guided flow).</summary>
        void RebuildLists()
        {
            bool was = syncing;
            syncing = true;
            itemSyncs.Clear();
            gantryNote.gameObject.SetActive(inputs.IsGantry);
            BuildSupportList();
            BuildPointList();
            BuildUniformList();
            syncing = was;
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }

        void ListChanged()
        {
            RebuildLists();
            changed?.Invoke();
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
            Clear(dimsHost);
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

        // ---- supports

        void BuildSupports(RectTransform c)
        {
            ButtonRow(c, "Presets", new[] { "Cantilever", "Simple", "Fixed-Fixed", "2 Spans", "Gantry", "Max" }, ApplySupportPreset);
            gantryNote = UiKit.Label(t, c, "Gantry Note", GantryDefaultsNote, t.regular, t.captionSize, t.textSecondary, TextAlignmentOptions.TopLeft);
            gantryNote.textWrappingMode = TextWrappingModes.Normal;
            Height(gantryNote.gameObject, 50);
            supportsHost = Column(c, "Support List", 4);
            addSupport = RowButton(c, "Add Support", "+  Add support", () =>
            {
                if (inputs.supports.Count >= HoloMechInputs.MaxSupports) return;
                inputs.preset = SupportPreset.Custom;
                inputs.supports.Add(new SupportInput { kind = SupportKind.Pin, positionCm = FreeSpot() });
                openSupport = inputs.supports.Count - 1;
                ListChanged();
            });
        }

        /// <summary>A position for a new support: midway in the widest gap between existing supports and the ends.</summary>
        float FreeSpot()
        {
            var xs = new List<float> { 0f, inputs.lengthCm };
            foreach (var s in inputs.supports) xs.Add(s.PositionOn(inputs.lengthCm));
            xs.Sort();
            float best = inputs.lengthCm / 2f, gap = -1f;
            for (int i = 1; i < xs.Count; i++)
            {
                if (xs[i] - xs[i - 1] > gap)
                {
                    gap = xs[i] - xs[i - 1];
                    best = (xs[i] + xs[i - 1]) / 2f;
                }
            }
            return Mathf.Round(best);
        }

        void ApplySupportPreset(int preset)
        {
            if (preset == 5) // "Max": every list at its limit, for the performance measurement
            {
                inputs.ApplyMaxLoadPreset();
                openSupport = -1;
                openPoint = -1;
                openUniform = -1;
                Sync();
                changed?.Invoke();
                return;
            }

            if (preset == 4)
            {
                inputs.ApplyLabGantryDimensions();
                openSupport = -1;
                openPoint = 0;
                openUniform = -1;
                Sync(); // the preset also sets the section, material and loads
                changed?.Invoke();
                GantryApplied?.Invoke();
                return;
            }

            float L = inputs.lengthCm;
            inputs.preset = (SupportPreset)(preset + 1);
            inputs.supports.Clear();
            switch (preset)
            {
                case 0:
                    inputs.supports.Add(new SupportInput { kind = SupportKind.Fixed });
                    break;
                case 1:
                    inputs.supports.Add(new SupportInput { kind = SupportKind.Pin });
                    inputs.supports.Add(new SupportInput { kind = SupportKind.Pin, atRightEnd = true });
                    break;
                case 2:
                    inputs.supports.Add(new SupportInput { kind = SupportKind.Fixed });
                    inputs.supports.Add(new SupportInput { kind = SupportKind.Fixed, atRightEnd = true });
                    break;
                default:
                    inputs.supports.Add(new SupportInput { kind = SupportKind.Pin });
                    inputs.supports.Add(new SupportInput { kind = SupportKind.Pin, positionCm = Mathf.Round(L / 2f) });
                    inputs.supports.Add(new SupportInput { kind = SupportKind.Pin, atRightEnd = true });
                    break;
            }

            openSupport = -1;
            ListChanged();
        }

        string SupportSummary(int i)
        {
            var s = inputs.supports[i];
            float x = s.PositionOn(inputs.lengthCm);
            string where = x <= 0.5f ? " (left end)" : x >= inputs.lengthCm - 0.5f ? " (right end)" : "";
            return $"S{i + 1}   {(s.kind == SupportKind.Fixed ? "Fixed" : "Pinned")}   x = {x:0} cm{where}";
        }

        void BuildSupportList()
        {
            Clear(supportsHost);
            for (int i = 0; i < inputs.supports.Count; i++)
            {
                int k = i;
                var s = inputs.supports[i];
                Entry(supportsHost, $"S{i + 1}", () => SupportSummary(k), openSupport == k,
                    () => { openSupport = openSupport == k ? -1 : k; RebuildLists(); },
                    body =>
                    {
                        Choice(body, "Type", new[] { "Pinned", "Fixed" }, s.kind == SupportKind.Fixed ? 1 : 0, j =>
                        {
                            s.kind = j == 1 ? SupportKind.Fixed : SupportKind.Pin;
                            inputs.preset = SupportPreset.Custom;
                            gantryNote.gameObject.SetActive(false);
                            RefreshSummaries();
                        });
                        TMP_Text label;
                        var pos = Slider(body, "Distance from left end (cm)", 0, inputs.lengthCm, s.PositionOn(inputs.lengthCm), 1f, "{0:0}", out label, v =>
                        {
                            s.atRightEnd = v >= inputs.lengthCm - 0.5f;
                            s.positionCm = v;
                            RefreshSummaries();
                        });
                        itemSyncs.Add(() =>
                        {
                            pos.maxValue = inputs.lengthCm;
                            UiKit.SetSlider(pos, label, "{0:0}", s.PositionOn(inputs.lengthCm));
                        });
                    },
                    () =>
                    {
                        inputs.supports.RemoveAt(k);
                        inputs.preset = SupportPreset.Custom;
                        openSupport = -1;
                        ListChanged();
                    });
            }

            addSupport.interactable = inputs.supports.Count < HoloMechInputs.MaxSupports;
        }

        // ---- point loads

        void BuildPoints(RectTransform c)
        {
            pointsHost = Column(c, "Point Load List", 4);
            addPoint = RowButton(c, "Add Point Load", "+  Add point load", () =>
            {
                if (inputs.pointLoads.Count >= HoloMechInputs.MaxPointLoads) return;
                inputs.pointLoads.Add(new PointLoadInput { enabled = true, positionCm = Mathf.Round(inputs.lengthCm / 2f), forceN = 1000f });
                openPoint = inputs.pointLoads.Count - 1;
                ListChanged();
            });
        }

        void BuildPointList()
        {
            Clear(pointsHost);
            for (int i = 0; i < inputs.pointLoads.Count; i++)
            {
                int k = i;
                var p = inputs.pointLoads[i];
                Entry(pointsHost, $"P{i + 1}",
                    () => $"P{k + 1}   {p.forceN:0} N {Arrow(p.direction)}   x = {p.positionCm:0} cm{(p.enabled ? "" : "   (off)")}",
                    openPoint == k,
                    () => { openPoint = openPoint == k ? -1 : k; RebuildLists(); },
                    body =>
                    {
                        var on = UiKit.Checkbox(t, body, "Enabled", "Apply this load", p.enabled, null, t.captionSize + 2);
                        Height(on.gameObject, 48);
                        on.onValueChanged.AddListener(v => { if (syncing) return; p.enabled = v; RefreshSummaries(); changed?.Invoke(); });
                        TMP_Text posL;
                        var pos = Slider(body, "Distance from left end (cm)", 0, inputs.lengthCm, p.positionCm, 1f, "{0:0}", out posL, v => { p.positionCm = v; RefreshSummaries(); });
                        Slider(body, "Force Magnitude (N)", 0, HoloMechRanges.ForceMaxN, p.forceN, 10f, "{0:0}", out _, v => { p.forceN = v; RefreshSummaries(); });
                        Choice(body, "Direction", new[] { "Up  ↑", "Down  ↓" }, p.direction == LoadDirection.Up ? 0 : 1,
                            j => { p.direction = j == 0 ? LoadDirection.Up : LoadDirection.Down; RefreshSummaries(); });
                        itemSyncs.Add(() =>
                        {
                            pos.maxValue = inputs.lengthCm;
                            UiKit.SetSlider(pos, posL, "{0:0}", p.positionCm);
                        });
                    },
                    () =>
                    {
                        inputs.pointLoads.RemoveAt(k);
                        openPoint = -1;
                        ListChanged();
                    });
            }

            addPoint.interactable = inputs.pointLoads.Count < HoloMechInputs.MaxPointLoads;
        }

        // ---- uniform loads

        void BuildUniforms(RectTransform c)
        {
            uniformsHost = Column(c, "Uniform Load List", 4);
            addUniform = RowButton(c, "Add Uniform Load", "+  Add uniform load", () =>
            {
                if (inputs.uniformLoads.Count >= HoloMechInputs.MaxUniformLoads) return;
                inputs.uniformLoads.Add(new UniformLoadInput { enabled = true, startCm = 0f, endCm = inputs.lengthCm, intensityNPerM = 1000f });
                openUniform = inputs.uniformLoads.Count - 1;
                ListChanged();
            });
        }

        void BuildUniformList()
        {
            Clear(uniformsHost);
            for (int i = 0; i < inputs.uniformLoads.Count; i++)
            {
                int k = i;
                var w = inputs.uniformLoads[i];
                Entry(uniformsHost, $"w{i + 1}",
                    () => $"w{k + 1}   {w.intensityNPerM:0} N/m {Arrow(w.direction)}   {w.startCm:0}–{w.endCm:0} cm{(w.enabled ? "" : "   (off)")}",
                    openUniform == k,
                    () => { openUniform = openUniform == k ? -1 : k; RebuildLists(); },
                    body =>
                    {
                        var on = UiKit.Checkbox(t, body, "Enabled", "Apply this load", w.enabled, null, t.captionSize + 2);
                        Height(on.gameObject, 48);
                        on.onValueChanged.AddListener(v => { if (syncing) return; w.enabled = v; RefreshSummaries(); changed?.Invoke(); });
                        TMP_Text startL = null, endL = null;
                        Slider start = null, end = null;
                        void SyncRange()
                        {
                            bool was = syncing;
                            syncing = true;
                            start.maxValue = end.maxValue = inputs.lengthCm;
                            UiKit.SetSlider(start, startL, "{0:0}", w.startCm);
                            UiKit.SetSlider(end, endL, "{0:0}", w.endCm);
                            syncing = was;
                        }

                        start = Slider(body, "Start from left end (cm)", 0, inputs.lengthCm, w.startCm, 1f, "{0:0}", out startL, v =>
                        {
                            w.startCm = v;
                            if (w.endCm <= v) w.endCm = Mathf.Min(v + 1f, inputs.lengthCm);
                            SyncRange();
                            RefreshSummaries();
                        });
                        end = Slider(body, "End from left end (cm)", 0, inputs.lengthCm, w.endCm, 1f, "{0:0}", out endL, v =>
                        {
                            w.endCm = v;
                            if (w.startCm >= v) w.startCm = Mathf.Max(v - 1f, 0f);
                            SyncRange();
                            RefreshSummaries();
                        });
                        Slider(body, "Intensity (N/m)", 0, HoloMechRanges.UniformMaxNPerM, w.intensityNPerM, 10f, "{0:0}", out _, v => { w.intensityNPerM = v; RefreshSummaries(); });
                        Choice(body, "Direction", new[] { "Up  ↑", "Down  ↓" }, w.direction == LoadDirection.Up ? 0 : 1,
                            j => { w.direction = j == 0 ? LoadDirection.Up : LoadDirection.Down; RefreshSummaries(); });
                        itemSyncs.Add(SyncRange);
                    },
                    () =>
                    {
                        inputs.uniformLoads.RemoveAt(k);
                        openUniform = -1;
                        ListChanged();
                    });
            }

            addUniform.interactable = inputs.uniformLoads.Count < HoloMechInputs.MaxUniformLoads;
        }

        /// <summary>Updates every summary line and the open entries' sliders (without rebuilding the lists).</summary>
        void RefreshSummaries()
        {
            bool was = syncing;
            syncing = true;
            foreach (var sync in itemSyncs) sync();
            syncing = was;
        }

        // ---- length and material

        void BuildBeam(RectTransform c)
        {
            length = Slider(c, "Beam Length (cm)", HoloMechRanges.LengthMinCm, HoloMechRanges.LengthMaxCm, inputs.lengthCm, 1f, "{0:0}", out lengthL, v =>
            {
                inputs.SetLength(v);
                RefreshSummaries();
            });
            modulus = Slider(c, "Elastic Module (GPa)", HoloMechRanges.ModulusMinGPa, HoloMechRanges.ModulusMaxGPa, inputs.elasticModulusGPa, 1f, "{0:0.#}", out modulusL, v =>
            {
                inputs.elasticModulusGPa = v;
                inputs.material = MaterialPreset.Custom;
                setMaterial(Array.IndexOf(Presets, MaterialPreset.Custom));
            });
            setMaterial = Choice(c, "", new[] { "Steel", "Concrete", "Wood", "Aluminum", "Custom" }, Array.IndexOf(Presets, inputs.material), i =>
            {
                inputs.ApplyMaterial(Presets[i]);
                bool was = syncing;
                syncing = true;
                UiKit.SetSlider(modulus, modulusL, "{0:0.#}", inputs.elasticModulusGPa);
                syncing = was;
            });
        }

        /// <summary>Refreshes every control from the inputs (after the guided flow or the on-site alignment changed them).</summary>
        public void Sync()
        {
            syncing = true;
            setSection((int)inputs.section);
            BuildDimensions();
            setMaterial(Array.IndexOf(Presets, inputs.material));
            UiKit.SetSlider(length, lengthL, "{0:0}", inputs.lengthCm);
            UiKit.SetSlider(modulus, modulusL, "{0:0.#}", inputs.elasticModulusGPa);
            syncing = false;
            RebuildLists();
        }
    }
}
