using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ARBeam.Mechanics;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

#if UNITY_EDITOR
namespace HoloMech.Tests
{
    /// <summary>
    /// Renders the beam of each solver-verification case (paper Table 2, C01-C12; definitions in
    /// 07_papers/NAMRC 55 2027/Table 2 solver verification/cases.json) with the application's own beam view:
    /// supports, load arrows, the deflected beam (auto scale, 10 % of the length) and the undeformed reference,
    /// coloured by bending stress, i.e. |M| normalised to the case's own maximum. No panels.
    /// A second HoloMechBeamView with the scene view's materials is placed far from the scene on its own layer, and an
    /// orthographic camera that sees only that layer writes one PNG per case to HOLOMECH_SHOTS (or Temp/Fig6).
    /// All tiles use the same frame, so the beam has the same on-image length in every tile.
    /// Run on its own:  -runTests -testPlatform PlayMode -testFilter HoloMech.Tests.Fig6BenchmarkShotPlayTest
    /// </summary>
    public class Fig6BenchmarkShotPlayTest
    {
        const int Layer = 31;

        static string ShotDir
        {
            get
            {
                var d = System.Environment.GetEnvironmentVariable("HOLOMECH_SHOTS");
                return string.IsNullOrEmpty(d) ? Path.GetFullPath("Temp/Fig6") : d;
            }
        }

        class Case
        {
            public string id;
            public float L;                                         // m
            public (float x, SupportKind k)[] supports;              // m
            public (float x, float P)[] points = new (float, float)[0];      // m, N (down)
            public (float a, float b, float w)[] udls = new (float, float, float)[0]; // m, m, N/m (down)
        }

        static readonly SupportKind Pin = SupportKind.Pin, Fix = SupportKind.Fixed;

        // Same values as cases.json (SI, loads down).
        static readonly Case[] Cases =
        {
            new Case { id = "C01", L = 2f, supports = new[] { (0f, Fix) }, points = new[] { (2f, 1000f) } },
            new Case { id = "C02", L = 4f, supports = new[] { (0f, Pin), (4f, Pin) }, points = new[] { (2f, 10000f) } },
            new Case { id = "C03", L = 4f, supports = new[] { (0f, Pin), (4f, Pin) }, points = new[] { (1.2f, 10000f) } },
            new Case { id = "C04", L = 4f, supports = new[] { (0f, Pin), (4f, Pin) }, udls = new[] { (0f, 4f, 2000f) } },
            new Case { id = "C05", L = 4f, supports = new[] { (0f, Fix), (4f, Fix) }, points = new[] { (2f, 10000f) } },
            new Case { id = "C06", L = 4f, supports = new[] { (0f, Fix), (4f, Pin) }, udls = new[] { (0f, 4f, 2000f) } },
            new Case { id = "C07", L = 4f, supports = new[] { (0f, Pin), (3f, Pin) }, points = new[] { (4f, 5000f) } },
            new Case { id = "C08", L = 8f, supports = new[] { (0f, Pin), (4f, Pin), (8f, Pin) }, udls = new[] { (0f, 8f, 2000f) } },
            new Case { id = "C09", L = 4f, supports = new[] { (1f, Fix), (4f, Pin) }, points = new[] { (0f, 3000f) }, udls = new[] { (1f, 4f, 2000f) } },
            new Case { id = "C10", L = 9f, supports = new[] { (0f, Pin), (3f, Pin), (6f, Pin), (9f, Pin) }, udls = new[] { (0f, 9f, 2000f) } },
            new Case { id = "C11", L = 6f, supports = new[] { (0f, Pin), (6f, Pin) }, points = new[] { (1.5f, 4000f), (4f, 6000f) },
                       udls = new[] { (0f, 2.5f, 1500f), (3.5f, 6f, 2500f) } },
            new Case { id = "C12", L = 2.4384f, supports = new[] { (0.1524f, Pin), (2.2860f, Pin) }, points = new[] { (1.2192f, 8900f) } },
        };

        static IEnumerator Frames(int n = 3)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static void SetInputs(HoloMechInputs inputs, Case c)
        {
            inputs.SetLength(c.L * 100f);
            if (c.id == "C12")
            {
                // Measured girder section (paper Sec. 4.4).
                inputs.section = SectionType.IBeam;
                inputs.iBeam.heightCm = 16f;
                inputs.iBeam.flangeWidthCm = 8.5f;
                inputs.iBeam.flangeThicknessCm = 1.4f;
                inputs.iBeam.webThicknessCm = 0.6f;
                inputs.elasticModulusGPa = 68.9f;
            }
            else
            {
                // Cases C01-C11 share one EI; the drawn section only sets the look, so it scales with L (depth L/25)
                // and every tile shows the same proportions. Colours are |M| / |M|max, independent of the section.
                inputs.section = SectionType.Box;
                inputs.box.heightCm = c.L * 100f / 25f;
                inputs.box.widthCm = inputs.box.heightCm * 0.8f;
                inputs.box.wallCm = inputs.box.heightCm * 0.08f;
                inputs.elasticModulusGPa = 200f;
            }

            inputs.supports.Clear();
            foreach (var (x, k) in c.supports)
                inputs.supports.Add(new SupportInput { kind = k, positionCm = x * 100f });
            inputs.pointLoads.Clear();
            foreach (var (x, P) in c.points)
                inputs.pointLoads.Add(new PointLoadInput { enabled = true, positionCm = x * 100f, forceN = P, direction = LoadDirection.Down });
            inputs.uniformLoads.Clear();
            foreach (var (a, b, w) in c.udls)
                inputs.uniformLoads.Add(new UniformLoadInput { enabled = true, startCm = a * 100f, endCm = b * 100f, intensityNPerM = w, direction = LoadDirection.Down });
            inputs.preset = SupportPreset.Custom;
        }

        static void SetLayer(Transform t)
        {
            t.gameObject.layer = Layer;
            foreach (Transform child in t) SetLayer(child);
        }

        static Bounds RendererBounds(Transform root)
        {
            var rs = root.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static Material Field(HoloMechBeamView v, string name)
            => (Material)typeof(HoloMechBeamView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(v);

        [UnityTest, Timeout(300000)]
        public IEnumerator RenderBenchmarkCases()
        {
            LogAssert.ignoreFailingMessages = true; // without a headset, the XR packages log errors of their own
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/HoloMech/Scenes/HoloMechQuest.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return Frames(15);

            var sceneView = Object.FindAnyObjectByType<HoloMechBeamView>();
            Assert.That(sceneView, Is.Not.Null);

            var mats = new[] { Field(sceneView, "stressMaterial"), Field(sceneView, "ghostMaterial"), Field(sceneView, "supportMaterial"),
                Field(sceneView, "pointLoadMaterial"), Field(sceneView, "uniformLoadMaterial") };
            GameObject go = null;
            HoloMechBeamView view = null;

            // A fresh view per case, far from the scene, so nothing else is in the frame. (Rebuilding one view leaves the
            // previous case's supports and arrows in place in batch mode, where its deferred Destroy does not run in time.)
            IEnumerator Build(Case c)
            {
                if (go != null) Object.DestroyImmediate(go);
                go = new GameObject("Fig6 beam " + c.id);
                go.transform.position = new Vector3(0f, 500f, 0f);
                view = go.AddComponent<HoloMechBeamView>();
                view.BuildOnStart = false;
                view.SetMaterials(mats[0], mats[1], mats[2], mats[3], mats[4]);
                SetInputs(view.Inputs, c);
                Assert.That(view.Rebuild(true), Is.True, c.id);
                view.ColorMode = BeamColorMode.BendingStress;
                SetLayer(go.transform);
                yield return Frames(2);
            }

            // Pass 1: solve and measure every case, to find one frame that holds all of them.
            var bounds = new List<Bounds>();
            foreach (var c in Cases)
            {
                yield return Build(c);
                var b = RendererBounds(go.transform);
                bounds.Add(b);
                Debug.Log($"[Fig6] {c.id} Mmax {System.Math.Abs(view.Solution.PeakMoment.Value):0.###} N m, " +
                          $"vmax {view.MaxDeflectionMetres * 1000:0.###} mm, scale {view.DeflectionScale:0.#}, bounds {b.min} - {b.max}");
            }

            float xMin = float.MaxValue, xMax = float.MinValue, yMin = float.MaxValue, yMax = float.MinValue;
            foreach (var b in bounds)
            {
                xMin = Mathf.Min(xMin, b.min.x); xMax = Mathf.Max(xMax, b.max.x);
                yMin = Mathf.Min(yMin, b.min.y); yMax = Mathf.Max(yMax, b.max.y);
            }

            float pad = 0.08f;
            float w = xMax - xMin + 2 * pad, h = yMax - yMin + 2 * pad;
            var centre = new Vector3((xMin + xMax) / 2f, (yMin + yMax) / 2f, 0f);
            int pxW = 1800, pxH = Mathf.RoundToInt(pxW * h / w);
            Debug.Log($"[Fig6] frame {w:0.000} x {h:0.000}, {pxW} x {pxH} px");

            var camGo = new GameObject("Fig6 Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            cam.orthographic = true;
            cam.orthographicSize = h / 2f;
            cam.aspect = w / h;
            cam.cullingMask = 1 << Layer;
            cam.transform.SetPositionAndRotation(centre - Vector3.forward * 20f, Quaternion.identity);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.30f, 0.32f, 0.35f, 1f);
            var rt = new RenderTexture(pxW, pxH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            Directory.CreateDirectory(ShotDir);

            // Pass 2: render.
            foreach (var c in Cases)
            {
                yield return Build(c);
                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(pxW, pxH, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, pxW, pxH), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                string file = Path.Combine(ShotDir, $"fig6_{c.id}.png");
                File.WriteAllBytes(file, tex.EncodeToPNG());
                Debug.Log($"[Fig6] wrote {file}");
                Object.Destroy(tex);
            }

            cam.targetTexture = null;
            rt.Release();
        }
    }
}
#endif
