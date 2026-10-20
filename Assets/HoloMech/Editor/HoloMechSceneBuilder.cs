using System.IO;
using ARBeam;
using ARBeam.Mechanics;
using HoloMech;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Creates Assets/HoloMech/Scenes/HoloMech.unity: camera, light, the HoloMech beam view (set to the paper's Fig. 5
/// case) and the app object. Re-runnable; the scene is rebuilt from scratch each time.
/// Menu: HoloMech ▸ Build Scene. CLI: unity command eval -- --code "return HoloMechSceneBuilder.Build();"
/// </summary>
public static class HoloMechSceneBuilder
{
    public const string ScenePath = "Assets/HoloMech/Scenes/HoloMech.unity";
    public const string XRScenePath = "Assets/HoloMech/Scenes/HoloMechXR.unity";
    const string ArRigPrefab = "Assets/Samples/XR Interaction Toolkit/3.1.2/AR Starter Assets/Prefabs/XR Origin (AR Rig).prefab";

    [MenuItem("HoloMech/Build XR Scene")]
    static void BuildXRMenu()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) BuildXR();
    }

    /// <summary>
    /// Mixed-reality scene: the project's AR rig (phone AR; swap for an XR rig on a headset), AR Session, XRI UI input,
    /// and the app in world-space mode. In the Editor it runs under XR Simulation.
    /// </summary>
    public static string BuildXR()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(XRScenePath));
        UiThemeBuilder.LoadOrBuild();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(UiThemeBuilder.ThemePath);

        var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ArRigPrefab));
        rig.transform.position = Vector3.zero;
        new GameObject("AR Session", typeof(UnityEngine.XR.ARFoundation.ARSession), typeof(UnityEngine.XR.ARFoundation.ARInputManager));
        new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule));

        var lightGo = new GameObject("Directional Light", typeof(Light), typeof(UniversalAdditionalLightData));
        lightGo.transform.rotation = Quaternion.Euler(40, -30, 0);
        lightGo.GetComponent<Light>().type = LightType.Directional;

        var beamGo = new GameObject("HoloMech Beam");
        var view = beamGo.AddComponent<HoloMechBeamView>();
        view.SetMaterials(AssetDatabase.LoadAssetAtPath<Material>("Assets/BeamStressMaterial.mat"), null, null, null, null);
        var vso = new SerializedObject(view);
        vso.FindProperty("buildOnStart").boolValue = false;
        vso.ApplyModifiedPropertiesWithoutUndo();

        var app = new GameObject("HoloMech App").AddComponent<HoloMechApp>();
        var so = new SerializedObject(app);
        so.FindProperty("theme").objectReferenceValue = theme;
        so.FindProperty("beamView").objectReferenceValue = view;
        so.FindProperty("viewCamera").objectReferenceValue = rig.GetComponentInChildren<Camera>();
        so.FindProperty("worldSpace").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, XRScenePath);
        AddToBuild(ScenePath);
        AddToBuild(XRScenePath);
        return $"Saved {XRScenePath}";
    }

    const string RuntimeMaterialFolder = "Assets/HoloMech/Resources/" + ProceduralShapes.TemplateFolder;

    /// <summary>
    /// Saves one template per runtime material kind under Resources, so device builds keep the URP Lit / Unlit
    /// shaders with the opaque, transparent and double-sided variants that <see cref="ProceduralShapes"/> uses.
    /// </summary>
    [MenuItem("HoloMech/Build Runtime Materials")]
    public static string BuildRuntimeMaterials()
    {
        Directory.CreateDirectory(RuntimeMaterialFolder);
        Save("Lit", ProceduralShapes.Lit(Color.white));
        Save("LitTransparent", ProceduralShapes.Transparent(new Color(1, 1, 1, 0.5f)));
        Save("Unlit", ProceduralShapes.Unlit(Color.white));
        Save("UnlitTransparent", ProceduralShapes.Unlit(new Color(1, 1, 1, 0.5f)));
        AssetDatabase.SaveAssets();
        return $"Saved runtime materials in {RuntimeMaterialFolder}";

        static void Save(string kind, Material m)
        {
            string path = $"{RuntimeMaterialFolder}/{kind}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) { EditorUtility.CopySerialized(m, existing); EditorUtility.SetDirty(existing); }
            else AssetDatabase.CreateAsset(m, path);
        }
    }

    internal static void AddToBuild(string path)
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (list.Exists(s => s.path == path)) return;
        list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    [MenuItem("HoloMech/Build Scene")]
    static void BuildMenu()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Build();
    }

    public static string Build()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        UiThemeBuilder.LoadOrBuild();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // Loaded after the scene switch: changing scenes can unload assets that were only held by a managed reference.
        var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(UiThemeBuilder.ThemePath);

        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(UniversalAdditionalCameraData));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(0x1A, 0x20, 0x2C, 0xFF);
        cam.fieldOfView = 50;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 100;

        var lightGo = new GameObject("Directional Light", typeof(Light), typeof(UniversalAdditionalLightData));
        lightGo.transform.rotation = Quaternion.Euler(40, -30, 0);
        var light = lightGo.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.color = new Color(1f, 0.98f, 0.94f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.66f);

        var beamGo = new GameObject("HoloMech Beam");
        beamGo.transform.position = new Vector3(-2f, 0, 0);
        var view = beamGo.AddComponent<HoloMechBeamView>();
        ApplyPaperFigure5(view.Inputs);
        var stress = AssetDatabase.LoadAssetAtPath<Material>("Assets/BeamStressMaterial.mat");
        view.SetMaterials(stress, null, null, null, null);
        var vso = new SerializedObject(view);
        vso.FindProperty("buildOnStart").boolValue = false; // HoloMechApp builds the beam when the user presses Next
        vso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(view);

        var appGo = new GameObject("HoloMech App");
        var app = appGo.AddComponent<HoloMechApp>();
        var so = new SerializedObject(app);
        so.FindProperty("theme").objectReferenceValue = theme;
        so.FindProperty("beamView").objectReferenceValue = view;
        so.FindProperty("viewCamera").objectReferenceValue = cam;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        return $"Saved {ScenePath}";
    }

    /// <summary>Paper Fig. 5: a pipe fixed at both ends, a downward point load near the left end and an upward uniform load.</summary>
    static void ApplyPaperFigure5(HoloMechInputs i)
    {
        i.section = SectionType.Pipe;
        i.pipe.outerDiameterCm = 16f;
        i.pipe.wallCm = 2.6f;
        i.leftEnd = EndCondition.Fixed;
        i.rightEnd = EndCondition.Fixed;
        i.lengthCm = 200f;
        i.ApplyMaterial(MaterialPreset.Steel);
        i.pointLoad.enabled = true;
        i.pointLoad.positionCm = 20f;
        i.pointLoad.forceN = 2000f;
        i.pointLoad.direction = LoadDirection.Down;
        i.uniformLoad.enabled = true;
        i.uniformLoad.startCm = 60f;
        i.uniformLoad.endCm = 150f;
        i.uniformLoad.intensityNPerM = 2000f;
        i.uniformLoad.direction = LoadDirection.Up;
    }
}
