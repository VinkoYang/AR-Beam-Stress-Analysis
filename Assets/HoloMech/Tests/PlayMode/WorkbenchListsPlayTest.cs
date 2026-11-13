using System.Collections;
using System.IO;
using System.Linq;
using ARBeam.Mechanics;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

#if UNITY_EDITOR
namespace HoloMech.Tests
{
    /// <summary>
    /// Opens the desktop HoloMech scene, builds a two-span beam with several loads through the input model, and checks
    /// that the Workbench lists, the delete confirmation and the stress colouring run without errors. Screenshots go to
    /// the folder in the HOLOMECH_SHOTS environment variable, if it is set.
    /// </summary>
    public class WorkbenchListsPlayTest
    {
        static string ShotDir => System.Environment.GetEnvironmentVariable("HOLOMECH_SHOTS");

        static IEnumerator Shot(string name)
        {
            // Without a shot folder, skip the end-of-frame wait: in batchmode it never comes on this scene and the test hangs.
            if (string.IsNullOrEmpty(ShotDir)) yield break;
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(ShotDir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        static Button Find(string path)
            => Object.FindObjectsByType<Button>().FirstOrDefault(b => GetPath(b.transform).EndsWith(path));

        static string GetPath(Transform t) => t.parent == null ? t.name : GetPath(t.parent) + "/" + t.name;

        [UnityTest, Timeout(180000)]
        public IEnumerator TwoSpanBeamWithSeveralLoads()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/HoloMech/Scenes/HoloMech.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 5; i++) yield return null;

            var app = Object.FindAnyObjectByType<HoloMechApp>();
            Assert.That(app, Is.Not.Null);
            var inputs = app.Inputs;
            inputs.section = SectionType.IBeam;
            inputs.SetLength(400);
            inputs.ApplyMaterial(MaterialPreset.Steel);
            inputs.supports.Clear();
            inputs.supports.Add(new SupportInput { kind = SupportKind.Pin });
            inputs.supports.Add(new SupportInput { kind = SupportKind.Pin, positionCm = 200 });
            inputs.supports.Add(new SupportInput { kind = SupportKind.Fixed, atRightEnd = true });
            inputs.pointLoads.Clear();
            inputs.pointLoads.Add(new PointLoadInput { positionCm = 80, forceN = 6000 });
            inputs.pointLoads.Add(new PointLoadInput { positionCm = 300, forceN = 3000 });
            inputs.uniformLoads.Clear();
            inputs.uniformLoads.Add(new UniformLoadInput { enabled = true, startCm = 220, endCm = 380, intensityNPerM = 4000 });
            app.SetLayout(AppLayout.Workbench);
            for (int i = 0; i < 5; i++) yield return null;

            var view = Object.FindAnyObjectByType<HoloMechBeamView>();
            Assert.That(view.Solution, Is.Not.Null);
            Assert.That(view.Solution.Reactions.Count, Is.EqualTo(3));
            Assert.That(view.Solution.Reactions.Sum(r => r.Force), Is.EqualTo(6000 + 3000 + 4000 * 1.6).Within(1e-6));
            yield return Shot("01 workbench two-span");

            view.ColorMode = BeamColorMode.BendingStress;
            yield return null;
            yield return Shot("02 stress colours");

            // Delete P2 with confirmation.
            var del = Find("Point Load List/P2/Row/Delete");
            Assert.That(del, Is.Not.Null, "P2 delete button");
            del.onClick.Invoke();
            yield return null;
            yield return Shot("03 delete confirmation");
            Assert.That(inputs.pointLoads.Count, Is.EqualTo(2), "nothing deleted before confirming");
            Find("Point Load List/P2/Row/Confirm/Confirm Delete").onClick.Invoke();
            for (int i = 0; i < 3; i++) yield return null;
            Assert.That(inputs.pointLoads.Count, Is.EqualTo(1));

            // Add a point load and a support from the buttons.
            Find("Add Point Load").onClick.Invoke();
            Find("Add Support").onClick.Invoke();
            for (int i = 0; i < 3; i++) yield return null;
            Assert.That(inputs.pointLoads.Count, Is.EqualTo(2));
            Assert.That(inputs.supports.Count, Is.EqualTo(4));
            Assert.That(view.Solution.Reactions.Count, Is.EqualTo(4));
            yield return Shot("04 after add");
        }
    }
}
#endif
