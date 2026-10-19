using System.Collections.Generic;
using ARBeam;
using ARBeam.Mechanics;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Wires the solver into the three original beam scenes. Idempotent: running it again re-applies the same
/// settings and reuses objects it created before. Nothing from the original scenes is deleted; the static
/// SF&amp;BM image stays in place (unused) next to the live diagram panel.
///
/// Menu: ARBeam ▸ Set Up Beam Scenes. From the Unity CLI:
///   unity command eval --code "return BeamSceneSetup.SetUpAllScenes();"
/// </summary>
public static class BeamSceneSetup
{
    const string LiveDiagramName = "LiveDiagram";

    static readonly string[] ScenePaths =
    {
        "Assets/_ARMarker/Scenes/BeamScene 1.unity", // One fixed end
        "Assets/_ARMarker/Scenes/BeamScene 2.unity", // Overhanging beam
        "Assets/_ARMarker/Scenes/BeamScene.unity",   // Two fixed ends
    };

    [MenuItem("ARBeam/Set Up Beam Scenes")]
    static void SetUpAllScenesMenu()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) SetUpAllScenes(true);
    }

    /// <summary>
    /// Opens, sets up and saves each beam scene. Refuses (no dialog, safe for the CLI) when an open scene has
    /// unsaved changes, unless <paramref name="discardOpenChanges"/> is true.
    /// </summary>
    public static string SetUpAllScenes(bool discardOpenChanges = false)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var open = SceneManager.GetSceneAt(i);
            if (open.isDirty && !discardOpenChanges)
                return $"Not run: '{open.path}' has unsaved changes. Save it, or pass discardOpenChanges: true.";
        }

        var log = new List<string>();
        foreach (var path in ScenePaths)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            log.Add(SetUpScene(scene));
            EditorSceneManager.SaveScene(scene);
        }

        string summary = string.Join("\n", log);
        Debug.Log("[BeamSceneSetup]\n" + summary);
        return summary;
    }

    [MenuItem("ARBeam/Set Up Open Beam Scene")]
    public static void SetUpActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        Debug.Log("[BeamSceneSetup] " + SetUpScene(scene));
        EditorSceneManager.MarkSceneDirty(scene);
    }

    public static string SetUpScene(Scene scene)
    {
        var beam = Object.FindAnyObjectByType<SliderDrivenBeam>();
        if (beam == null) return $"{scene.name}: no beam script found, skipped.";

        // ---- Beam parameters: the bench rig (6061-T6, 1" × 1/8"). ----
        var so = new SerializedObject(beam);
        float startPosition01;
        switch (beam)
        {
            case CantileverBeam _:
                SetBeam(so, SupportType.Cantilever, 400f, 0f, 0f);
                startPosition01 = 1f;
                break;
            case OverhangBeam _:
                SetBeam(so, SupportType.Overhang, 800f, 200f, 600f);
                startPosition01 = 1f;
                break;
            default:
                SetBeam(so, SupportType.FixedFixed, 800f, 0f, 0f);
                startPosition01 = 0.5f;
                break;
        }

        so.FindProperty("widthMm").floatValue = 25.4f;
        so.FindProperty("thicknessMm").floatValue = 3.175f;
        so.FindProperty("material").enumValueIndex = (int)BeamMaterialPreset.Aluminum6061T6;
        so.FindProperty("includeSelfWeight").boolValue = false;
        so.FindProperty("designMassKg").floatValue = 0.5f;
        so.FindProperty("stressScaleOverrideMPa").floatValue = 0f;
        so.FindProperty("axisFromMesh").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        var meshView = GetOrAdd<BeamMeshView>(beam.gameObject);
        var mv = new SerializedObject(meshView);
        mv.FindProperty("beam").objectReferenceValue = beam;
        mv.FindProperty("deflectionAmplification").floatValue = 1f;
        mv.ApplyModifiedPropertiesWithoutUndo();

        // ---- Sliders → SliderLoadSource. Mass slider becomes 0–500 g. ----
        var canvas = FindCanvasWith("Load Slider");
        var massSlider = canvas != null ? Find<UnityEngine.UI.Slider>(canvas, "Load Slider") : null;
        var positionSlider = canvas != null ? Find<UnityEngine.UI.Slider>(canvas, "Position Slider") : null;
        if (massSlider != null)
        {
            massSlider.minValue = 0f;
            massSlider.maxValue = 500f;
            massSlider.wholeNumbers = true;
            massSlider.SetValueWithoutNotify(0f);
            EditorUtility.SetDirty(massSlider);
        }

        if (positionSlider != null)
        {
            positionSlider.SetValueWithoutNotify(startPosition01);
            EditorUtility.SetDirty(positionSlider);
        }

        var source = GetOrAdd<SliderLoadSource>(beam.gameObject);
        var ss = new SerializedObject(source);
        ss.FindProperty("beam").objectReferenceValue = beam;
        ss.FindProperty("loadArrow").objectReferenceValue = so.FindProperty("loadArrow").objectReferenceValue;
        ss.FindProperty("followMesh").objectReferenceValue = meshView;
        ss.FindProperty("massSlider").objectReferenceValue = massSlider;
        ss.FindProperty("positionSlider").objectReferenceValue = positionSlider;
        ss.FindProperty("massLabel").objectReferenceValue = SliderLabel(massSlider, "Magnitude");
        ss.FindProperty("positionLabel").objectReferenceValue = SliderLabel(positionSlider, "Position");
        ss.FindProperty("position01").floatValue = startPosition01;
        ss.FindProperty("massGrams").floatValue = 0f;
        ss.ApplyModifiedPropertiesWithoutUndo();

        // ---- Stress legend shows the fixed full scale in MPa. ----
        var legend = canvas != null ? canvas.Find("StressLegend") : null;
        if (legend != null)
        {
            var label = GetOrAdd<StressLegendLabel>(legend.gameObject);
            var ls = new SerializedObject(label);
            ls.FindProperty("beam").objectReferenceValue = beam;
            ls.FindProperty("maxLabel").objectReferenceValue = Find<TMP_Text>(legend, "Maximum");
            ls.FindProperty("minLabel").objectReferenceValue = Find<TMP_Text>(legend, "0");
            ls.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- Live V / M diagrams replace the static PNG popup. ----
        string diagramNote = "no DiagramPopup";
        var popup = Object.FindAnyObjectByType<DiagramPopup>();
        if (popup != null && popup.diagramImage != null)
        {
            var panel = BuildDiagramPanel((RectTransform)popup.diagramImage.transform, beam);
            popup.panel = panel;
            popup.autoHide = false;
            EditorUtility.SetDirty(popup);
            diagramNote = "live diagram panel wired";
        }

        // ---- Overhang: put the support models where the solver's supports are. ----
        string supportNote = "";
        if (beam is OverhangBeam)
        {
            var a = GameObject.Find("Support");
            var b = GameObject.Find("Support (1)");
            if (a != null && b != null)
            {
                var (left, right) = a.transform.position.x <= b.transform.position.x ? (a, b) : (b, a);
                MoveAlongX(left.transform, beam.BeamXToWorld(0.2).x);
                MoveAlongX(right.transform, beam.BeamXToWorld(0.6).x);
                supportNote = ", supports moved to 200/600 mm";
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        return $"{scene.name}: {beam.GetType().Name} → {beam.SupportType}, L = {beam.LengthM * 1000:0} mm; {diagramNote}{supportNote}.";
    }

    static void SetBeam(SerializedObject so, SupportType type, float lengthMm, float aMm, float bMm)
    {
        so.FindProperty("supportType").enumValueIndex = (int)type;
        so.FindProperty("lengthMm").floatValue = lengthMm;
        so.FindProperty("supportAMm").floatValue = aMm;
        so.FindProperty("supportBMm").floatValue = bMm;
    }

    /// <summary>
    /// Panel with the same rect as the old PNG: white card, shear diagram on top, moment diagram below.
    /// Hidden through a CanvasGroup until the SF&amp;BM button is pressed.
    /// </summary>
    static CanvasGroup BuildDiagramPanel(RectTransform pngImage, BeamController beam)
    {
        var parent = pngImage.parent;
        var existing = parent.Find(LiveDiagramName);
        var root = existing != null ? (RectTransform)existing : NewRect(LiveDiagramName, parent);
        root.anchorMin = pngImage.anchorMin;
        root.anchorMax = pngImage.anchorMax;
        root.pivot = pngImage.pivot;
        root.anchoredPosition = pngImage.anchoredPosition;
        root.sizeDelta = pngImage.sizeDelta;
        root.SetSiblingIndex(pngImage.GetSiblingIndex() + 1);

        var bg = GetOrAdd<UnityEngine.UI.Image>(root.gameObject);
        bg.color = new Color(1f, 1f, 1f, 0.92f);
        bg.raycastTarget = false;
        var group = GetOrAdd<CanvasGroup>(root.gameObject);
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        BuildDiagram(root, "ShearDiagram", BeamQuantity.Shear, beam, new Vector2(0f, 0.5f), new Vector2(1f, 1f),
            new Color(0.2f, 0.55f, 0.35f, 0.45f), new Color(0.2f, 0.55f, 0.35f, 0.45f));
        BuildDiagram(root, "MomentDiagram", BeamQuantity.Moment, beam, new Vector2(0f, 0f), new Vector2(1f, 0.5f),
            new Color(0.95f, 0.45f, 0.2f, 0.45f), new Color(0.2f, 0.45f, 0.95f, 0.45f));

        var footer = Label(root, "Footer", 18, TextAlignmentOptions.Bottom);
        Stretch(footer.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 4f), new Vector2(0f, 30f));
        footer.text = $"x = 0 at left end   ·   L = {beam.LengthM * 1000:0} mm   ·   fixed scale: 500 g at worst position";
        footer.color = new Color(0.25f, 0.25f, 0.25f);
        return group;
    }

    static void BuildDiagram(RectTransform root, string name, BeamQuantity quantity, BeamController beam,
        Vector2 anchorMin, Vector2 anchorMax, Color positive, Color negative)
    {
        var existing = root.Find(name);
        var area = existing != null ? (RectTransform)existing : NewRect(name, root);
        Stretch(area, anchorMin, anchorMax, new Vector2(30f, 36f), new Vector2(-30f, -44f));

        GetOrAdd<CanvasRenderer>(area.gameObject);
        var graphic = GetOrAdd<BeamDiagramGraphic>(area.gameObject);
        var title = Label(root, name + "Title", 24, TextAlignmentOptions.TopLeft);
        Stretch(title.rectTransform, new Vector2(anchorMin.x, anchorMax.y), anchorMax, new Vector2(30f, -40f), new Vector2(-30f, -8f));
        var peak = Label(root, name + "Peak", 22, TextAlignmentOptions.TopRight);
        Stretch(peak.rectTransform, new Vector2(anchorMin.x, anchorMax.y), anchorMax, new Vector2(30f, -40f), new Vector2(-30f, -10f));

        var gs = new SerializedObject(graphic);
        gs.FindProperty("beam").objectReferenceValue = beam;
        gs.FindProperty("quantity").enumValueIndex = (int)quantity;
        gs.FindProperty("fixedScale").boolValue = true;
        gs.FindProperty("positiveFill").colorValue = positive;
        gs.FindProperty("negativeFill").colorValue = negative;
        gs.FindProperty("titleLabel").objectReferenceValue = title;
        gs.FindProperty("peakLabel").objectReferenceValue = peak;
        gs.FindProperty("m_RaycastTarget").boolValue = false;
        gs.ApplyModifiedPropertiesWithoutUndo();
        title.text = quantity == BeamQuantity.Shear ? "Shear force V (N)" : "Bending moment M (N·m)";
    }

    static TextMeshProUGUI Label(RectTransform parent, string name, float size, TextAlignmentOptions align)
    {
        var t = parent.Find(name);
        var rt = t != null ? (RectTransform)t : NewRect(name, parent);
        var tmp = GetOrAdd<TextMeshProUGUI>(rt.gameObject);
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = new Color(0.1f, 0.1f, 0.1f);
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    static void MoveAlongX(Transform t, float worldX)
    {
        var p = t.position;
        t.position = new Vector3(worldX, p.y, p.z);
        EditorUtility.SetDirty(t);
    }

    static Transform FindCanvasWith(string childName)
    {
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            if (c.transform.Find(childName) != null) return c.transform;
        return null;
    }

    /// <summary>The slider's caption: the child with the given name, else its first direct TMP child (scenes differ).</summary>
    static TMP_Text SliderLabel(UnityEngine.UI.Slider slider, string preferredName)
    {
        if (slider == null) return null;
        var named = Find<TMP_Text>(slider.transform, preferredName);
        if (named != null) return named;
        foreach (Transform child in slider.transform)
        {
            var t = child.GetComponent<TMP_Text>();
            if (t != null && child.name != "L" && child.name != "R") return t;
        }

        return null;
    }

    static T Find<T>(Transform parent, string childName) where T : Component
    {
        var t = parent.Find(childName);
        return t != null ? t.GetComponent<T>() : null;
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }
}
