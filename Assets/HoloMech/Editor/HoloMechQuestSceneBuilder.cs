using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using ARBeam;
using HoloMech;
using Oculus.Interaction.Editor.QuickActions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Creates Assets/HoloMech/Scenes/HoloMechQuest.unity for Meta Quest 3, from Meta's Building Blocks:
/// Camera Rig, Interactions Rig (hands and controllers, ray and poke) and Passthrough (mixed reality). The app sits in
/// a grabbable window (<see cref="XRWindowLayout"/>): a main canvas, a bar with Reset Layout, a handle under the window,
/// and one holder per panel with its own canvas and handle. Every canvas gets Meta's ray and poke canvas interaction,
/// every handle Meta's grab and ray grab, and the controls are Meta's UI Set prefabs (<see cref="MetaUiSkin"/>) on a
/// Quest-dark variant of the UI theme.
///
/// Installing blocks is asynchronous, so the CLI starts the build and polls:
///   unity command eval -- --code "return HoloMechQuestSceneBuilder.Build();"
///   unity command eval -- --code "return HoloMechQuestSceneBuilder.Status();"
/// </summary>
public static class HoloMechQuestSceneBuilder
{
    public const string ScenePath = "Assets/HoloMech/Scenes/HoloMechQuest.unity";
    const string QuestFolder = "Assets/HoloMech/Quest";
    public const string SkinPath = QuestFolder + "/MetaUiSkin.asset";
    public const string ThemePath = QuestFolder + "/UiTheme_Quest.asset";

    const string CameraRigBlock = "Packages/com.meta.xr.sdk.core/Editor/BuildingBlocks/BlockData/CameraRig/CameraRigBB.asset";
    const string PassthroughBlock = "Packages/com.meta.xr.sdk.core/Editor/BuildingBlocks/BlockData/Passthrough/Passthrough.asset";
    const string InteractionsRigBlock = "Packages/com.meta.xr.sdk.interaction.ovr/Editor/Blocks/Interactions/Interactions.asset";
    const string UiSet = "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Objects/UISet/Prefabs/";

    /// <summary>
    /// Window: 1.8 m wide, 1.6 m in front of the user, its centre 8 cm below eye height (at −25 cm the PI found it
    /// too low on the headset, 2026-10-21).
    /// </summary>
    const float BoardWidth = 1.8f;
    static readonly Vector3 BoardOffset = new Vector3(0f, -0.08f, 1.6f);

    static string status = "idle";

    public static string Status() => status;

    [MenuItem("HoloMech/Build Quest Scene")]
    static void BuildMenu()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Build();
    }

    public static string Build()
    {
        if (status.StartsWith("building")) return status;
        status = "building";
        Run();
        return status;
    }

    static async void Run()
    {
        try
        {
            Directory.CreateDirectory(QuestFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            UiThemeBuilder.LoadOrBuild();
            BuildSkin();
            BuildQuestTheme();
            HoloMechSceneBuilder.BuildRuntimeMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            var skin = AssetDatabase.LoadAssetAtPath<MetaUiSkin>(SkinPath);

            // Building Blocks (each installs its dependencies; the Interactions Rig brings the Camera Rig).
            status = "building: Interactions Rig";
            await InstallBlock(InteractionsRigBlock);
            if (UnityEngine.Object.FindAnyObjectByType<OVRCameraRig>() == null)
            {
                status = "building: Camera Rig";
                await InstallBlock(CameraRigBlock);
            }

            status = "building: Passthrough";
            await InstallBlock(PassthroughBlock);

            status = "building: board";
            var rig = UnityEngine.Object.FindAnyObjectByType<OVRCameraRig>();
            var eye = rig != null ? rig.centerEyeAnchor.GetComponent<Camera>() : Camera.main;

            // What Meta's Project Setup Tool requires for a passthrough underlay (it reports both as Required).
            if (rig != null)
            {
                var manager = rig.GetComponent<OVRManager>();
                if (manager != null) manager.isInsightPassthroughEnabled = true;
                foreach (var cam in rig.GetComponentsInChildren<Camera>(true))
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.clear;
                }
            }

            var lightGo = new GameObject("Directional Light", typeof(Light), typeof(UniversalAdditionalLightData));
            lightGo.transform.rotation = Quaternion.Euler(40, -30, 0);
            lightGo.GetComponent<Light>().type = LightType.Directional;

            // The window: main canvas (guided screens, banner, popups), window bar with Reset Layout, a grab handle
            // under it, and one grabbable holder per panel (XRWindowLayout moves the panels into them at runtime).
            status = "building: window";
            var window = new GameObject("HoloMech Window").transform;
            window.position = new Vector3(0, 1.6f, 0) + BoardOffset;
            const float px = BoardWidth / 1920f;
            var canvas = WorldCanvas("HoloMech UI", window, new Vector2(1920, 1080), px, Vector3.zero, eye);
            var bar = WorldCanvas("Window Bar", window, new Vector2(1920, 84), px, new Vector3(0, (540 + 24 + 42) * px, 0), eye);
            var windowGrab = Handle(window, window, new Vector2(0.32f, 0.022f), new Vector3(0, -(540 + 30) * px, -0.005f), eye);

            // Panels a little in front of the main canvas, so rays reach them first. The result menu keeps the 0.74
            // scale it has between the side panels on the board.
            var beamPanel = PanelHolder("Beam Panel Holder", window, new Vector2(560, 1032), px, new Vector3(-656 * px, 0, -0.01f), eye);
            var loadPanel = PanelHolder("Load Panel Holder", window, new Vector2(560, 1032), px, new Vector3(656 * px, 0, -0.01f), eye);
            var results = PanelHolder("Result Menu Holder", window, new Vector2(920, 560), px * 0.74f, new Vector3(0, -269 * px, -0.01f), eye);

            var beamGo = new GameObject("HoloMech Beam");
            var view = beamGo.AddComponent<HoloMechBeamView>();
            view.SetMaterials(AssetDatabase.LoadAssetAtPath<Material>("Assets/BeamStressMaterial.mat"), null, null, null, null);
            var vso = new SerializedObject(view);
            vso.FindProperty("buildOnStart").boolValue = false;
            vso.ApplyModifiedPropertiesWithoutUndo();

            var appGo = new GameObject("HoloMech App");
            var xr = appGo.AddComponent<XRPresentation>();
            xr.StartOffset = BoardOffset;
            xr.BeamLength = 0.5f;
            var windowLayout = window.gameObject.AddComponent<XRWindowLayout>();
            windowLayout.SetUp(beamPanel, loadPanel, results, bar, windowGrab);
            var app = appGo.AddComponent<HoloMechApp>();
            var so = new SerializedObject(app);
            so.FindProperty("theme").objectReferenceValue = theme;
            so.FindProperty("beamView").objectReferenceValue = view;
            so.FindProperty("viewCamera").objectReferenceValue = eye;
            so.FindProperty("worldSpace").boolValue = true;
            so.FindProperty("boardCanvas").objectReferenceValue = canvas;
            so.FindProperty("uiSkin").objectReferenceValue = skin;
            so.FindProperty("windowLayout").objectReferenceValue = windowLayout;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            HoloMechSceneBuilder.AddToBuild(ScenePath);
            status = $"Saved {ScenePath}";
        }
        catch (Exception e)
        {
            status = "error: " + (e.InnerException ?? e).Message;
            Debug.LogException(e);
        }
    }

    /// <summary>World-space canvas with Meta's ray and poke canvas interaction (Interaction SDK Quick Actions).</summary>
    static Canvas WorldCanvas(string name, Transform parent, Vector2 sizePx, float metresPerPx, Vector3 localPosition, Camera eye)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = sizePx;
        rt.localScale = Vector3.one * metresPerPx;
        rt.localPosition = localPosition;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = eye;
        QuickActionsAPI.AddRayCanvasInteraction(go);
        QuickActionsAPI.AddPokeCanvasInteraction(go);
        return canvas;
    }

    /// <summary>A panel's own canvas under a holder that a handle on its top edge moves.</summary>
    static Transform PanelHolder(string name, Transform window, Vector2 sizePx, float metresPerPx, Vector3 localPosition, Camera eye)
    {
        var holder = new GameObject(name).transform;
        holder.SetParent(window, false);
        holder.localPosition = localPosition;
        WorldCanvas("Canvas", holder, sizePx, metresPerPx, Vector3.zero, eye);
        float top = sizePx.y / 2f * metresPerPx;
        Handle(holder, holder, new Vector2(0.16f, 0.018f), new Vector3(0, top + 0.022f, -0.002f), eye);
        return holder;
    }

    /// <summary>
    /// Pill-shaped grab handle: a Grabbable (target = the window or a panel holder) with <see cref="FaceUserGrabTransformer"/>,
    /// Meta's hand / controller grab and ray grab interactions on a box collider a little larger than the pill.
    /// </summary>
    static FaceUserGrabTransformer Handle(Transform parent, Transform target, Vector2 sizeM, Vector3 localPosition, Camera eye)
    {
        var go = new GameObject("Grab Handle");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        var collider = go.AddComponent<BoxCollider>();
        collider.size = new Vector3(sizeM.x + 0.04f, sizeM.y + 0.03f, 0.02f);
        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        var transformer = go.AddComponent<FaceUserGrabTransformer>();
        var grabbable = go.AddComponent<Oculus.Interaction.Grabbable>();
        grabbable.InjectOptionalTargetTransform(target);
        grabbable.InjectOptionalOneGrabTransformer(transformer);
        grabbable.InjectOptionalTwoGrabTransformer(transformer);

        // Pill: 1 canvas pixel = 1 mm; display only (no raycaster), the collider takes the input.
        var pillCanvas = new GameObject("Pill", typeof(RectTransform), typeof(Canvas));
        var pr = (RectTransform)pillCanvas.transform;
        pr.SetParent(go.transform, false);
        pr.sizeDelta = sizeM * 1000f;
        pr.localScale = Vector3.one * 0.001f;
        var pc = pillCanvas.GetComponent<Canvas>();
        pc.renderMode = RenderMode.WorldSpace;
        pc.worldCamera = eye;
        var image = new GameObject("Image", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.rectTransform.SetParent(pr, false);
        image.rectTransform.anchorMin = Vector2.zero;
        image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
        image.sprite = theme != null ? theme.roundedRect : null;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 2f;
        image.raycastTarget = false;
        image.color = new Color(1f, 1f, 1f, 0.45f);

        QuickActionsAPI.AddGrabInteraction(go);
        QuickActionsAPI.AddRayGrabInteraction(go);
        go.AddComponent<GrabHandleVisual>().SetPill(image);
        return transformer;
    }

    /// <summary>Runs a Building Block's installer (internal in the Meta SDK, so through reflection).</summary>
    static async Task InstallBlock(string assetPath)
    {
        var block = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
        if (block == null) throw new FileNotFoundException("Building Block not found", assetPath);

        MethodInfo install = null;
        for (var type = block.GetType(); type != null && install == null; type = type.BaseType)
            install = type.GetMethod("InstallWithDependencies", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                null, new[] { typeof(GameObject) }, null);
        if (install == null) throw new MissingMethodException(block.GetType().Name, "InstallWithDependencies(GameObject)");

        var task = (Task)install.Invoke(block, new object[] { null });
        await task;
    }

    /// <summary>The UI Set prefabs the Quest scene builds its controls from.</summary>
    static void BuildSkin()
    {
        var skin = AssetDatabase.LoadAssetAtPath<MetaUiSkin>(SkinPath);
        if (skin == null)
        {
            skin = ScriptableObject.CreateInstance<MetaUiSkin>();
            AssetDatabase.CreateAsset(skin, SkinPath);
        }

        skin.primaryButton = Prefab("Button/UnityUIButtonBased/PrimaryButton_IconAndLabel_UnityUIButton.prefab");
        skin.secondaryButton = Prefab("Button/UnityUIButtonBased/SecondaryButton_IconAndLabel_UnityUIButton.prefab");
        skin.choiceButton = Prefab("Button/UnityUIToggleBased/TextTileButton_IconAndLabel_Toggle.prefab");
        skin.switchToggle = Prefab("Button/UnityUIToggleBased/ToggleButton_Switch.prefab");
        skin.checkbox = Prefab("Button/UnityUIToggleBased/ToggleButton_Checkbox.prefab");
        skin.slider = Prefab("Slider/MediumSlider.prefab");
        EditorUtility.SetDirty(skin);
        AssetDatabase.SaveAssets();
    }

    static GameObject Prefab(string path)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(UiSet + path);
        if (go == null) throw new FileNotFoundException("UI Set prefab not found", UiSet + path);
        return go;
    }

    /// <summary>The shared theme (fonts, sizes, diagram colours) with Quest system colours: neutral dark plates, Meta blue.</summary>
    static void BuildQuestTheme()
    {
        var baseTheme = AssetDatabase.LoadAssetAtPath<UiTheme>(UiThemeBuilder.ThemePath);
        var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<UiTheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }

        EditorUtility.CopySerialized(baseTheme, theme);
        theme.name = "UiTheme_Quest";
        theme.panel = new Color32(0x1C, 0x1E, 0x21, 0xEB);
        theme.panelRaised = new Color32(0x2B, 0x2D, 0x31, 0xFF);
        theme.panelHover = new Color32(0x3A, 0x3D, 0x42, 0xFF);
        theme.accent = new Color32(0x00, 0x64, 0xE0, 0xFF);
        theme.accentPressed = new Color32(0x00, 0x4F, 0xB3, 0xFF);
        theme.textPrimary = new Color(1f, 1f, 1f, 0.92f);
        theme.textSecondary = new Color(1f, 1f, 1f, 0.70f);
        theme.track = new Color(1f, 1f, 1f, 0.16f);
        theme.backdrop = new Color32(0x12, 0x13, 0x15, 0xFF);
        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
    }
}
