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
/// Camera Rig, Interactions Rig (hands and controllers, ray and poke) and Passthrough (mixed reality). The HoloMech
/// panels sit on a world-space board whose canvas gets Meta's ray and poke canvas interaction, and the controls are
/// Meta's UI Set prefabs (<see cref="MetaUiSkin"/>) on a Quest-dark variant of the UI theme.
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

    /// <summary>Board: 1.8 m wide, 1.6 m in front of the user and a little below eye height.</summary>
    const float BoardWidth = 1.8f;
    static readonly Vector3 BoardOffset = new Vector3(0f, -0.25f, 1.6f);

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

            var board = new GameObject("HoloMech Board").transform;
            board.position = new Vector3(0, 1.6f, 0) + BoardOffset;
            var canvasGo = new GameObject("HoloMech UI", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var rt = (RectTransform)canvasGo.transform;
            rt.SetParent(board, false);
            rt.sizeDelta = new Vector2(1920, 1080);
            rt.localScale = Vector3.one * (BoardWidth / 1920f);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = eye;

            status = "building: canvas interaction";
            QuickActionsAPI.AddRayCanvasInteraction(canvasGo);
            QuickActionsAPI.AddPokeCanvasInteraction(canvasGo);

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
            var app = appGo.AddComponent<HoloMechApp>();
            var so = new SerializedObject(app);
            so.FindProperty("theme").objectReferenceValue = theme;
            so.FindProperty("beamView").objectReferenceValue = view;
            so.FindProperty("viewCamera").objectReferenceValue = eye;
            so.FindProperty("worldSpace").boolValue = true;
            so.FindProperty("boardCanvas").objectReferenceValue = canvas;
            so.FindProperty("uiSkin").objectReferenceValue = skin;
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
