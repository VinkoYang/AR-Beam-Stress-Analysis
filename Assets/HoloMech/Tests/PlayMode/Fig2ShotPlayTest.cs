using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
    /// Renders the paper's Fig. 2 (Workbench panel) from the Quest scene without a headset: a continuous beam on three pins
    /// and one clamped support with two point loads and one uniform load, S4, P2 and w1 opened, coloured by deflection.
    /// A separate camera looks straight at the window and renders it to a PNG in HOLOMECH_SHOTS (or Temp/Fig2).
    /// Run on its own:  -runTests -testPlatform PlayMode -testFilter HoloMech.Tests.Fig2ShotPlayTest
    /// </summary>
    public class Fig2ShotPlayTest
    {
        static string ShotDir
        {
            get
            {
                var d = System.Environment.GetEnvironmentVariable("HOLOMECH_SHOTS");
                return string.IsNullOrEmpty(d) ? Path.GetFullPath("Temp/Fig2") : d;
            }
        }

        static string GetPath(Transform t) => t.parent == null ? t.name : GetPath(t.parent) + "/" + t.name;

        static Button Find(string path)
            => Object.FindObjectsByType<Button>().FirstOrDefault(b => GetPath(b.transform).EndsWith(path));

        static IEnumerator Frames(int n = 3)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static void Click(string path)
        {
            var b = Find(path);
            Assert.That(b, Is.Not.Null, path);
            b.onClick.Invoke();
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator RenderWorkbench()
        {
            LogAssert.ignoreFailingMessages = true; // without a headset, the XR packages log errors of their own
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/HoloMech/Scenes/HoloMechQuest.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return Frames(15);

            var app = Object.FindAnyObjectByType<HoloMechApp>();
            Assert.That(app, Is.Not.Null);
            var inputs = app.Inputs;
            inputs.section = SectionType.Box;
            inputs.box.widthCm = 10f;
            inputs.box.heightCm = 8f;
            inputs.box.wallCm = 0.6f;
            inputs.SetLength(266);
            inputs.ApplyMaterial(MaterialPreset.Aluminum);
            inputs.supports.Clear();
            inputs.supports.Add(new SupportInput { kind = SupportKind.Pin, positionCm = 19 });
            inputs.supports.Add(new SupportInput { kind = SupportKind.Pin, positionCm = 124 });
            inputs.supports.Add(new SupportInput { kind = SupportKind.Pin, positionCm = 230 });
            inputs.supports.Add(new SupportInput { kind = SupportKind.Fixed, positionCm = 177 });
            inputs.pointLoads.Clear();
            inputs.pointLoads.Add(new PointLoadInput { enabled = true, positionCm = 65, forceN = 1000, direction = LoadDirection.Down });
            inputs.pointLoads.Add(new PointLoadInput { enabled = true, positionCm = 148, forceN = 1000, direction = LoadDirection.Up });
            inputs.uniformLoads.Clear();
            inputs.uniformLoads.Add(new UniformLoadInput { enabled = true, startCm = 209, endCm = 266, intensityNPerM = 1520, direction = LoadDirection.Down });
            app.Workbench.Sync();
            typeof(HoloMechApp).GetField("dirty", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(app, true);
            yield return Frames(5);

            Click("Support List/S4/Row/Summary");
            yield return Frames();
            Click("Point Load List/P2/Row/Summary");
            yield return Frames();
            Click("Uniform Load List/w1/Row/Summary");
            yield return Frames(10);

            var view = Object.FindAnyObjectByType<HoloMechBeamView>();
            Assert.That(view.Solution, Is.Not.Null, "solved");
            Assert.That(view.Solution.Reactions.Count, Is.EqualTo(4));
            view.ColorMode = BeamColorMode.Deflection;
            yield return Frames(5);

            // Frame every active world-space canvas.
            var canvases = Object.FindObjectsByType<Canvas>()
                .Where(c => c.isActiveAndEnabled && c.isRootCanvas && c.renderMode == RenderMode.WorldSpace).ToArray();
            foreach (var c in canvases)
                Debug.Log($"[Fig2] canvas {GetPath(c.transform)} pos {c.transform.position} fwd {c.transform.forward} scale {c.transform.lossyScale}");
            var main = canvases.OrderByDescending(c => ((RectTransform)c.transform).rect.width * c.transform.lossyScale.x).First();
            var rot = main.transform.rotation;
            var inv = Quaternion.Inverse(rot);
            var corners = new Vector3[4];
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var c in canvases)
            {
                ((RectTransform)c.transform).GetWorldCorners(corners);
                foreach (var p in corners)
                {
                    var q = inv * p;
                    min = Vector3.Min(min, q);
                    max = Vector3.Max(max, q);
                }
            }

            var centre = rot * ((min + max) / 2f);
            float w = max.x - min.x, h = max.y - min.y;
            float margin = 1.02f;
            int pxW = 4200;
            int pxH = Mathf.RoundToInt(pxW * h / w);
            Debug.Log($"[Fig2] main {GetPath(main.transform)} frame {w:0.000} x {h:0.000} m, {pxW} x {pxH} px");

            var go = new GameObject("Fig2 Camera");
            var cam = go.AddComponent<Camera>();
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            cam.orthographic = true;
            cam.orthographicSize = h / 2f * margin;
            cam.aspect = w / h;
            cam.transform.SetPositionAndRotation(centre - main.transform.forward * 2f, rot);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 10f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.30f, 0.32f, 0.35f, 1f);
            var rt = new RenderTexture(pxW, pxH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            foreach (var c in canvases) c.worldCamera = cam;
            yield return Frames(3);
            yield return new WaitForEndOfFrame();
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(pxW, pxH, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, pxW, pxH), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Directory.CreateDirectory(ShotDir);
            string file = Path.Combine(ShotDir, "fig2_workbench_unity.png");
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Debug.Log($"[Fig2] wrote {file}");
            Object.Destroy(tex);
            cam.targetTexture = null;
            rt.Release();
        }
    }
}
#endif
