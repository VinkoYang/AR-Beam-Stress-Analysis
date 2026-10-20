using System.IO;
using HoloMech;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

/// <summary>
/// Records the README demos with Unity Recorder: in Play mode, starts a <see cref="DemoDriver"/> script and an MP4
/// capture of the Game view at a constant 30 fps (game time is locked to the capture rate, so the result is smooth
/// even on a slow or unfocused Editor), and stops when the script's duration has passed. Converting to GIF is done
/// outside Unity with ffmpeg.
///
/// CLI, in Play mode:  unity command eval -- --code "return DemoRecorder.Begin(\"workbench\", \"holomech-workbench-live\");"
///                     unity command eval -- --code "return DemoRecorder.Status();"
/// </summary>
public static class DemoRecorder
{
    public const string OutputFolder = "Temp/Recordings";

    static RecorderController controller;
    static float stopAt;
    static string status = "idle";

    public static string Begin(string demo, string fileName, int width = 1280, int height = 720)
    {
        if (!Application.isPlaying) return "error: enter Play mode first";
        if (controller != null && controller.IsRecording()) return "error: already recording";

        Directory.CreateDirectory(OutputFolder);
        var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movie.name = fileName;
        movie.Enabled = true;
        movie.EncoderSettings = new CoreEncoderSettings
        {
            Codec = CoreEncoderSettings.OutputCodec.MP4,
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
        };
        movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = width, OutputHeight = height };
        movie.AudioInputSettings.PreserveAudio = false;
        movie.OutputFile = Path.GetFullPath(Path.Combine(OutputFolder, fileName));

        settings.AddRecorderSettings(movie);
        settings.SetRecordModeToManual();
        settings.FrameRate = 30;
        settings.FrameRatePlayback = FrameRatePlayback.Constant;
        settings.CapFrameRate = true;

        controller = new RecorderController(settings);
        controller.PrepareRecording();
        if (!controller.StartRecording()) return "error: recorder did not start";

        var driver = new GameObject("Demo Driver").AddComponent<DemoDriver>();
        driver.Run(demo);
        stopAt = Time.time + DemoDriver.Duration(demo);
        status = "recording " + fileName;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        return status;
    }

    public static string Status() => status;

    static void Tick()
    {
        if (controller == null) { EditorApplication.update -= Tick; return; }
        if (Application.isPlaying && Time.time < stopAt) return;
        controller.StopRecording();
        controller = null;
        EditorApplication.update -= Tick;
        status = "done";
    }
}
