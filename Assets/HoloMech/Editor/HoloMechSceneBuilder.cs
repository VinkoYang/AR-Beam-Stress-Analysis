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
