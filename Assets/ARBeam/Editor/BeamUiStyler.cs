using ARBeam;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Lays out and styles the main menu and the three beam scenes with the shared <see cref="UiTheme"/>.
///
/// Layout rules: 1920×1080 reference canvas, match 0.5, and every element anchored to the edge or corner it
/// belongs to (no centre-anchored offsets), so nothing falls off screen at any aspect ratio. Existing objects are
/// re-anchored and restyled, not replaced; new objects (backdrop, control panel, title, value readouts) are added
/// once and reused on re-runs.
///
/// Menu: ARBeam ▸ UI ▸ Restyle All Scenes. CLI:
///   unity command eval -- --code "return BeamUiStyler.StyleAllScenes();" --timeout 180000
/// </summary>
public static class BeamUiStyler
{
    public const string MainMenuPath = "Assets/_ARMarker/Scenes/MainMenu.unity";
    static readonly Vector2 Reference = new Vector2(1920, 1080);

    static UiTheme t;

    [MenuItem("ARBeam/UI/Restyle All Scenes")]
    static void StyleAllScenesMenu()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) StyleAllScenes(true);
    }

    public static string StyleAllScenes(bool discardOpenChanges = false)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty && !discardOpenChanges)
                return $"Not run: '{SceneManager.GetSceneAt(i).path}' has unsaved changes.";

        t = UiThemeBuilder.LoadOrBuild();
        var log = new System.Text.StringBuilder();

        var menu = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
        log.AppendLine(StyleMainMenu());
        EditorSceneManager.SaveScene(menu);

        foreach (var path in BeamSceneSetup.ScenePaths)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            log.AppendLine(StyleBeamScene(scene));
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log("[BeamUiStyler]\n" + log);
        return log.ToString();
    }

    // ------------------------------------------------------------------ main menu

    static string StyleMainMenu()
    {
        var canvas = Object.FindAnyObjectByType<Canvas>();
        var root = canvas.transform;
        SetUpCanvas(canvas);

        var backdrop = Child(root, "Backdrop", 0);
        Stretch(backdrop);
        Panel(backdrop, t.backdrop, rounded: false);

        var card = Child(root, "Card", 1);
        Place(card, Center, Center, Vector2.zero, new Vector2(760, 800));
        Panel(card, t.panel);

        var title = root.Find("Text (TMP)");
        if (title != null)
        {
            var r = (RectTransform)title;
            Place(r, Center, Center, new Vector2(0, 300), new Vector2(680, 72));
            Text(title, "Beam Learning Module", t.bold, t.displaySize, t.textPrimary, TextAlignmentOptions.Center);
        }

        var subtitle = root.Find("Text (TMP) (1)");
        if (subtitle != null)
        {
            Place((RectTransform)subtitle, Center, Center, new Vector2(0, 220), new Vector2(640, 70));
            Text(subtitle, "Choose a beam, load it, and see shear, bending moment, stress and deflection.",
                t.regular, 22, t.textSecondary, TextAlignmentOptions.Center, wrap: true);
        }

        MenuButton(root, "One Fixed End", 100, "One fixed end", "Cantilever, 400 mm");
        MenuButton(root, "Overhanging Beam", -12, "Overhanging beam", "800 mm on supports at 200 and 600 mm");
        MenuButton(root, "Two Fixed Ends", -124, "Two fixed ends", "Clamped–clamped, 800 mm (virtual only)");
        EnsureHoloMechButton(root);
        MenuButton(root, "HoloMech", -236, "HoloMech", "Any section, supports and loads; stress view");

        var footer = Child(root, "Footer");
        Place(footer, Center, Center, new Vector2(0, -350), new Vector2(680, 30));
        Text(footer, "6061-T6 aluminium bar, 25.4 × 3.2 mm  ·  closed-form Euler–Bernoulli beam theory",
            t.regular, 16, t.textSecondary, TextAlignmentOptions.Center);

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        return "MainMenu: restyled";
    }

    /// <summary>Fourth menu button, a copy of an existing one re-wired to <see cref="LoadSceneButton"/>.</summary>
    static void EnsureHoloMechButton(Transform root)
    {
        if (root.Find("HoloMech") != null) return;
        var template = root.Find("Two Fixed Ends");
        if (template == null) return;
        var copy = Object.Instantiate(template.gameObject, root);
        copy.name = "HoloMech";
        foreach (var mb in copy.GetComponents<MonoBehaviour>())
            if (!(mb is Button) && !(mb is Image)) Object.DestroyImmediate(mb);
        var button = copy.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        var loader = copy.AddComponent<LoadSceneButton>();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, loader.Load);
    }

    static void MenuButton(Transform root, string name, float y, string label, string caption)
    {
        var button = root.Find(name);
        if (button == null) return;
        Place((RectTransform)button, Center, Center, new Vector2(0, y), new Vector2(600, 96));
        Button(button.GetComponent<Button>());
        var text = button.Find("Text (TMP)");
        if (text != null)
        {
            Stretch((RectTransform)text, new Vector2(28, 0), new Vector2(-28, 0));
            Text(text, $"<font-weight=600>{label}</font-weight>\n<size=18><color=#{Hex(t.textSecondary)}>{caption}</color></size>",
                t.regular, 26, t.textPrimary, TextAlignmentOptions.Left);
        }
    }

    // ------------------------------------------------------------------ beam scenes

    static string StyleBeamScene(Scene scene)
    {
        var canvas = FindCanvasWith("Load Slider");
        if (canvas == null) return $"{scene.name}: no beam UI found";
        var root = canvas.transform;
        SetUpCanvas(canvas.GetComponent<Canvas>());
        var beam = Object.FindAnyObjectByType<BeamController>();

        // Top bar: back (left), title (centre), diagram button (right).
        var back = root.Find("Back Button");
        if (back != null)
        {
            Place((RectTransform)back, TopLeft, TopLeft, new Vector2(32, -32), new Vector2(230, 60));
            Button(back.GetComponent<Button>());
            ButtonLabel(back, "‹  Main menu");
        }

        var diagrams = root.Find("SF&BM");
        if (diagrams != null)
        {
            Place((RectTransform)diagrams, TopRight, TopRight, new Vector2(-32, -32), new Vector2(300, 60));
            Button(diagrams.GetComponent<Button>());
            ButtonLabel(diagrams, "V & M diagrams");
        }

        var title = Child(root, "BeamTitle");
        Place(title, TopCenter, TopCenter, new Vector2(0, -30), new Vector2(900, 44));
        var titleText = Text(title, "Beam", t.semiBold, t.titleSize, t.textPrimary, TextAlignmentOptions.Center);
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 18;
        titleText.fontSizeMax = t.titleSize;
        var details = Child(root, "BeamDetails");
        Place(details, TopCenter, TopCenter, new Vector2(0, -76), new Vector2(900, 30));
        var detailsText = Text(details, "", t.regular, 19, t.textSecondary, TextAlignmentOptions.Center);
        detailsText.enableAutoSizing = true;
        detailsText.fontSizeMin = 14;
        detailsText.fontSizeMax = 19;
        var info = GetOrAdd<BeamInfoLabel>(title.gameObject);
        Set(info, "beam", beam);
        Set(info, "title", titleText);
        Set(info, "details", detailsText);
        info.Refresh();

        // Bottom control panel with the two sliders.
        var panel = Child(root, "ControlPanel");
        Place(panel, BottomCenter, BottomCenter, new Vector2(0, 28), new Vector2(1080, 176));
        Panel(panel, t.panel);

        var mass = root.Find("Load Slider") ?? panel.Find("Load Slider");
        var position = root.Find("Position Slider") ?? panel.Find("Position Slider");
        if (mass != null) StyleSliderRow(mass, panel, -52, "Mass");
        if (position != null)
        {
            StyleSliderRow(position, panel, -116, "Position");
            EndLabel(position.Find("L"), true, "left end");
            EndLabel(position.Find("R"), false, "right end");
        }

        // Stress legend on the right edge.
        var legend = root.Find("StressLegend");
        if (legend != null)
        {
            var legendPanel = Child(root, "LegendPanel");
            Place(legendPanel, RightMiddle, RightMiddle, new Vector2(-24, 40), new Vector2(196, 460));
            Panel(legendPanel, t.panel);
            // Panel directly behind the legend, whatever order earlier runs left them in.
            legendPanel.SetSiblingIndex(legend.GetSiblingIndex());
            if (legendPanel.GetSiblingIndex() > legend.GetSiblingIndex()) legend.SetSiblingIndex(legendPanel.GetSiblingIndex());

            var caption = Child(legendPanel, "Caption");
            Place(caption, TopCenter, TopCenter, new Vector2(0, -12), new Vector2(172, 48));
            Text(caption, "Max bending\nstress σ", t.medium, 17, t.textSecondary, TextAlignmentOptions.Center, wrap: true);
            var oldCaption = legend.Find("Caption");
            if (oldCaption != null) Object.DestroyImmediate(oldCaption.gameObject); // from an earlier layout of ours

            Place((RectTransform)legend, RightMiddle, RightMiddle, new Vector2(-48, 26), new Vector2(24, 340));
            var bar = legend.GetComponent<Image>();
            bar.raycastTarget = false;
            var max = legend.Find("Maximum");
            if (max != null)
            {
                Place((RectTransform)max, new Vector2(0, 1), new Vector2(1, 0.5f), new Vector2(-14, -6), new Vector2(130, 30));
                Text(max, max.GetComponent<TMP_Text>().text, t.semiBold, 20, t.textPrimary, TextAlignmentOptions.Right);
            }

            var zero = legend.Find("0");
            if (zero != null)
            {
                Place((RectTransform)zero, new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(-14, 6), new Vector2(130, 30));
                Text(zero, zero.GetComponent<TMP_Text>().text, t.semiBold, 20, t.textPrimary, TextAlignmentOptions.Right);
            }
        }

        // Solver wiring (components, slider ranges, value labels, live diagram panel), then style that panel.
        string setup = BeamSceneSetup.SetUpScene(scene);
        var live = root.Find("LiveDiagram");
        if (live != null) StyleDiagramPanel((RectTransform)live);

        // 3D labels on the support models.
        foreach (var tmp in Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include))
        {
            tmp.font = t.semiBold;
            EditorUtility.SetDirty(tmp);
        }

        AddDesktopFraming(beam);
        EditorSceneManager.MarkSceneDirty(scene);
        return $"{scene.name}: restyled; {setup}";
    }

    static void StyleSliderRow(Transform sliderT, RectTransform panel, float y, string caption)
    {
        sliderT.SetParent(panel, false);
        var slider = sliderT.GetComponent<Slider>();
        var rt = (RectTransform)sliderT;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(196, y - 16);
        rt.offsetMax = new Vector2(-176, y + 16);

        var bg = sliderT.Find("Background") as RectTransform;
        if (bg != null)
        {
            bg.anchorMin = new Vector2(0, 0.5f);
            bg.anchorMax = new Vector2(1, 0.5f);
            bg.sizeDelta = new Vector2(0, 10);
            bg.anchoredPosition = Vector2.zero;
            Panel(bg, t.track);
        }

        // Fill and handle areas share a 14 px inset so the fill ends exactly at the handle centre; the fill
        // itself reaches back to the track start.
        var fillArea = sliderT.Find("Fill Area") as RectTransform;
        if (fillArea != null)
        {
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.offsetMin = new Vector2(14, -5);
            fillArea.offsetMax = new Vector2(-14, 5);
            var fill = fillArea.Find("Fill") as RectTransform;
            if (fill != null)
            {
                fill.offsetMin = new Vector2(-14, 0);
                fill.offsetMax = new Vector2(0, 0);
                Panel(fill, t.accent);
            }
        }

        var handleArea = sliderT.Find("Handle Slide Area") as RectTransform;
        if (handleArea != null)
        {
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(14, 0);
            handleArea.offsetMax = new Vector2(-14, 0);
            var handle = handleArea.Find("Handle") as RectTransform;
            if (handle != null)
            {
                handle.sizeDelta = new Vector2(30, 0);
                var img = handle.GetComponent<Image>();
                img.sprite = t.circle;
                img.type = Image.Type.Simple;
                img.color = Color.white;
                EditorUtility.SetDirty(img);
            }
        }

        var colors = slider.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.9f, 0.94f, 1f);
        colors.pressedColor = new Color(0.8f, 0.87f, 1f);
        colors.selectedColor = Color.white;
        slider.colors = colors;
        EditorUtility.SetDirty(slider);

        // Caption on the left (the scene's existing label), live value on the right.
        TMP_Text captionText = null;
        foreach (Transform c in sliderT)
        {
            var tmp = c.GetComponent<TMP_Text>();
            if (tmp != null && c.name != "L" && c.name != "R" && c.name != "Value")
            {
                captionText = tmp;
                break;
            }
        }

        if (captionText != null)
        {
            Place((RectTransform)captionText.transform, LeftMiddle, RightMiddle, new Vector2(-22, 0), new Vector2(170, 40));
            Text(captionText.transform, caption, t.medium, t.labelSize, t.textPrimary, TextAlignmentOptions.Right);
        }

        var value = Child(sliderT, "Value");
        Place(value, RightMiddle, LeftMiddle, new Vector2(22, 0), new Vector2(150, 40));
        Text(value, "", t.semiBold, t.valueSize, t.textPrimary, TextAlignmentOptions.Left);
    }

    static void EndLabel(Transform label, bool left, string text)
    {
        if (label == null) return;
        var r = (RectTransform)label;
        Place(r, left ? new Vector2(0, 0) : new Vector2(1, 0), left ? new Vector2(0, 1) : new Vector2(1, 1),
            new Vector2(0, -4), new Vector2(160, 24));
        Text(label, text, t.regular, 16, t.textSecondary, left ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.TopRight);
    }

    static void StyleDiagramPanel(RectTransform live)
    {
        live.anchorMin = new Vector2(0.16f, 0.25f);
        live.anchorMax = new Vector2(0.84f, 0.86f);
        live.pivot = new Vector2(0.5f, 0.5f);
        live.offsetMin = Vector2.zero;
        live.offsetMax = Vector2.zero;
        Panel(live, t.panel);

        foreach (var g in live.GetComponentsInChildren<BeamDiagramGraphic>(true))
        {
            var so = new SerializedObject(g);
            bool shear = g.Quantity == ARBeam.Mechanics.BeamQuantity.Shear;
            so.FindProperty("lineColor").colorValue = shear ? t.shearLine : t.momentLine;
            so.FindProperty("positiveFill").colorValue = shear ? t.shearFill : t.momentPositiveFill;
            so.FindProperty("negativeFill").colorValue = shear ? t.shearFill : t.momentNegativeFill;
            so.FindProperty("axisColor").colorValue = t.diagramAxis;
            so.FindProperty("lineWidth").floatValue = 3f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        foreach (var tmp in live.GetComponentsInChildren<TMP_Text>(true))
        {
            bool isTitle = tmp.name.EndsWith("Title");
            bool isPeak = tmp.name.EndsWith("Peak");
            tmp.font = isTitle ? t.semiBold : isPeak ? t.medium : t.regular;
            tmp.fontSize = isTitle ? 24 : isPeak ? 20 : 16;
            tmp.color = isTitle ? t.textPrimary : t.textSecondary;
            EditorUtility.SetDirty(tmp);
        }
    }

    static void AddDesktopFraming(BeamController beam)
    {
        if (beam == null) return;
        var framing = GetOrAdd<DesktopPreviewFraming>(beam.gameObject);
        Set(framing, "beam", beam);
        Set(framing, "origin", Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>());
        var so = new SerializedObject(framing);
        so.FindProperty("margin").floatValue = 1.5f; // room for the legend panel and the support models
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ helpers

    static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    static readonly Vector2 TopLeft = new Vector2(0, 1);
    static readonly Vector2 TopRight = new Vector2(1, 1);
    static readonly Vector2 TopCenter = new Vector2(0.5f, 1);
    static readonly Vector2 BottomCenter = new Vector2(0.5f, 0);
    static readonly Vector2 LeftMiddle = new Vector2(0, 0.5f);
    static readonly Vector2 RightMiddle = new Vector2(1, 0.5f);

    static void SetUpCanvas(Canvas canvas)
    {
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Reference;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        EditorUtility.SetDirty(scaler);
    }

    /// <summary>Anchor and pivot at the same point; position relative to it.</summary>
    static void Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        r.anchorMin = anchor;
        r.anchorMax = anchor;
        r.pivot = pivot;
        r.anchoredPosition = position;
        r.sizeDelta = size;
        EditorUtility.SetDirty(r);
    }

    static void Stretch(RectTransform r) => Stretch(r, Vector2.zero, Vector2.zero);

    static void Stretch(RectTransform r, Vector2 offsetMin, Vector2 offsetMax)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.pivot = Center;
        r.offsetMin = offsetMin;
        r.offsetMax = offsetMax;
        EditorUtility.SetDirty(r);
    }

    static Image Panel(RectTransform r, Color color, bool rounded = true)
    {
        var img = GetOrAdd<Image>(r.gameObject);
        img.sprite = rounded ? t.roundedRect : null;
        img.type = rounded ? Image.Type.Sliced : Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
        EditorUtility.SetDirty(img);
        return img;
    }

    static void Button(Button button)
    {
        if (button == null) return;
        var img = button.GetComponent<Image>();
        img.sprite = t.roundedRect;
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        img.raycastTarget = true;
        var c = button.colors;
        c.normalColor = t.panelRaised;
        c.highlightedColor = t.panelHover;
        c.pressedColor = t.accent;
        c.selectedColor = t.panelRaised;
        c.disabledColor = new Color(0.4f, 0.4f, 0.45f, 0.5f);
        c.colorMultiplier = 1;
        c.fadeDuration = 0.08f;
        button.colors = c;
        EditorUtility.SetDirty(img);
        EditorUtility.SetDirty(button);
    }

    static void ButtonLabel(Transform button, string label)
    {
        var text = button.Find("Text (TMP)");
        if (text == null) return;
        Stretch((RectTransform)text, new Vector2(16, 0), new Vector2(-16, 0));
        Text(text, label, t.semiBold, t.buttonSize, t.textPrimary, TextAlignmentOptions.Center);
    }

    static TMP_Text Text(Transform tr, string text, TMP_FontAsset font, float size, Color color,
        TextAlignmentOptions align, bool wrap = false)
    {
        var tmp = tr.GetComponent<TMP_Text>();
        if (tmp == null) tmp = tr.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.font = font;
        tmp.fontSize = size;
        tmp.enableAutoSizing = false;
        tmp.color = color;
        tmp.alignment = align;
        tmp.fontStyle = FontStyles.Normal;
        tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.characterSpacing = 0;
        tmp.wordSpacing = 0;
        tmp.margin = Vector4.zero;
        EditorUtility.SetDirty(tmp);
        return tmp;
    }

    /// <summary>Finds or creates a RectTransform child, optionally at a sibling index.</summary>
    static RectTransform Child(Transform parent, string name, int siblingIndex = -1)
    {
        var existing = parent.Find(name);
        RectTransform r;
        if (existing != null) r = (RectTransform)existing;
        else
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            r = (RectTransform)go.transform;
            r.SetParent(parent, false);
        }

        if (siblingIndex >= 0) r.SetSiblingIndex(siblingIndex);
        return r;
    }

    static Transform FindCanvasWith(string childName)
    {
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (c.transform.Find(childName) != null) return c.transform;
            var panel = c.transform.Find("ControlPanel");
            if (panel != null && panel.Find(childName) != null) return c.transform;
        }

        return null;
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static void Set(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
}
