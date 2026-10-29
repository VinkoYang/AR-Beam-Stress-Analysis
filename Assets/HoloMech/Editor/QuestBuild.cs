using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;

/// <summary>
/// Builds the Quest APK from the command line with the "Android Quest 3" build profile:
///   Unity -batchmode -projectPath . -executeMethod QuestBuild.Build -quit -logFile build.log
/// Output: Builds/HoloMechQuest.apk (git-ignored). Install with: adb install -r Builds/HoloMechQuest.apk
/// </summary>
public static class QuestBuild
{
    const string ProfilePath = "Assets/Settings/Build Profiles/Android Quest 3.asset";
    const string Output = "Builds/HoloMechQuest.apk";

    public static void Build()
    {
        var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(ProfilePath);
        if (profile == null) { Debug.LogError("QuestBuild: build profile not found: " + ProfilePath); EditorApplication.Exit(1); return; }
        var report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
        {
            buildProfile = profile,
            locationPathName = Output,
            options = BuildOptions.None,
        });
        Debug.Log($"QuestBuild: {report.summary.result}, {report.summary.totalSize / 1e6:0.0} MB, {report.summary.totalErrors} errors -> {Output}");
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
