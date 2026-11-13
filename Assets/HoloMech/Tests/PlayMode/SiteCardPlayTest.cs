using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

#if UNITY_EDITOR
namespace HoloMech.Tests
{
    /// <summary>
    /// Opens the Quest scene and presses the on-site card's buttons: the Gantry preset, the Dimensions rows and Save snapshot.
    /// There is no headset here, so the spatial anchor buttons must be grey. The anchors themselves can only be tested on a headset.
    /// </summary>
    public class SiteCardPlayTest
    {
        const string Card = "On-site Overlay (MR) Card/Content/";

        readonly List<string> ownExceptions = new List<string>();

        /// <summary>The failing messages are ignored below, so exceptions from this project's code are collected here.</summary>
        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception && stack.Contains("HoloMech.")) ownExceptions.Add(message + "\n" + stack);
        }

        [SetUp]
        public void WatchExceptions()
        {
            ownExceptions.Clear();
            Application.logMessageReceived += OnLog;
        }

        [TearDown]
        public void StopWatchingExceptions()
        {
            Application.logMessageReceived -= OnLog;
        }

        static string GetPath(Transform t) => t.parent == null ? t.name : GetPath(t.parent) + "/" + t.name;

        static Button Find(string path)
            => Object.FindObjectsByType<Button>().FirstOrDefault(b => GetPath(b.transform).EndsWith(path));

        static TMP_Text Text(string path)
            => Object.FindObjectsByType<TMP_Text>().FirstOrDefault(l => GetPath(l.transform).EndsWith(path));

        static Button Row(string caption, string button)
            => Find(Card + "Dimension Rows/" + caption + "/" + button);

        static string Value(string caption) => Text(Card + "Dimension Rows/" + caption + "/Value").text;

        static IEnumerator Frames(int count = 3)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        static void AssertGrey(Button button, string what)
        {
            Assert.That(button.interactable, Is.False, what);
            Assert.That(button.GetComponent<CanvasGroup>().alpha, Is.LessThan(1f), what + " looks disabled");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator GantryRowsAndSnapshot()
        {
            LogAssert.ignoreFailingMessages = true; // without a headset, the XR packages log errors of their own
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/HoloMech/Scenes/HoloMechQuest.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return Frames(10);
            Assert.That(ownExceptions, Is.Empty, "the app starts without exceptions");

            var app = Object.FindAnyObjectByType<HoloMechApp>();
            var site = Object.FindAnyObjectByType<SitePlacement>();
            var view = Object.FindAnyObjectByType<HoloMechBeamView>();
            Assert.That(site, Is.Not.Null, "the Quest window creates the on-site card");
            var inputs = app.Inputs;

            // Before the preset there are no gantry rows, and the note is hidden.
            Assert.That(Row("Beam length", "Plus"), Is.Not.Null);
            Assert.That(Row("Left support", "Plus"), Is.Null);
            Assert.That(Text("Gantry Note"), Is.Null, "inactive");
            AssertGrey(Find(Card + "Save snapshot"), "snapshot outside on-site mode");

            Find("Presets/Gantry").onClick.Invoke();
            yield return Frames();
            Assert.That(inputs.IsGantry, Is.True);
            Assert.That(inputs.elasticModulusGPa, Is.EqualTo(68.9f).Within(1e-4f));
            Assert.That(Text("Elastic Module (GPa)/Value").text, Is.EqualTo("68.9"));
            Assert.That(Text("Gantry Note").text, Does.Contain("Lab gantry 2026-11-11"));
            Assert.That(Text(Card + "Status").text, Does.Contain("Lab gantry 2026-11-11"));
            Assert.That(inputs.lengthCm, Is.EqualTo(HoloMechInputs.LabGantryLengthCm), "measured girder length");
            Assert.That(inputs.iBeam.heightCm, Is.EqualTo(HoloMechInputs.LabGantryHeightCm));
            Assert.That(inputs.iBeam.flangeWidthCm, Is.EqualTo(HoloMechInputs.LabGantryFlangeWidthCm));
            Assert.That(Row("Height h", "Plus"), Is.Not.Null, "the rows followed the section to the I-beam");
            Assert.That(Row("Web thick. tw", "Plus"), Is.Not.Null);
            Assert.That(Row("Outer diam. D", "Plus"), Is.Null);
            Assert.That(Row("Left support", "Plus"), Is.Not.Null);
            Assert.That(Row("Right support", "Plus"), Is.Not.Null);
            Assert.That(Row("Trolley", "Plus"), Is.Not.Null);

            // Outside on-site mode the rows do nothing.
            Row("Beam length", "Plus").onClick.Invoke();
            Assert.That(inputs.lengthCm, Is.EqualTo(HoloMechInputs.LabGantryLengthCm));

            site.SetSiteMode(true);
            yield return Frames();
            Assert.That(Find(Card + "Save snapshot").interactable, Is.True);
            AssertGrey(Find(Card + "Save placement"), "no anchors without a headset");
            AssertGrey(Find(Card + "Forget placement"), "no anchors without a headset");
            Assert.That(Text(Card + "Anchor Note").text, Does.Contain("headset build"));

            // The length changes by the step, the left end stays, and the beam keeps its true size.
            var leftEnd = view.transform.position;
            Row("Beam length", "Plus").onClick.Invoke();
            Row("Beam length", "Plus").onClick.Invoke();
            yield return Frames();
            Assert.That(inputs.lengthCm, Is.EqualTo(244.04f).Within(1e-3f));
            Assert.That(Value("Beam length"), Is.EqualTo("2440.4 mm"));
            Assert.That(view.transform.position, Is.EqualTo(leftEnd), "left end");
            Assert.That(view.Solution.Model.Length, Is.EqualTo(2.4404).Within(1e-5));
            Assert.That(view.transform.localScale.x, Is.EqualTo(view.TrueScale).Within(1e-5f));

            // 1 mm steps move the pins (82 in apart, 177.8 mm from each end) and the trolley (at midspan, 1219.2 mm).
            Row("Right support", "Minus").onClick.Invoke();
            Row("Left support", "Plus").onClick.Invoke();
            Row("Trolley", "Plus").onClick.Invoke();
            yield return Frames();
            Assert.That(Value("Right support"), Is.EqualTo("2259.6 mm"));
            Assert.That(Value("Left support"), Is.EqualTo("178.8 mm"));
            Assert.That(Value("Trolley"), Is.EqualTo("1220.2 mm"));
            Assert.That(view.Solution.Reactions[1].Position, Is.EqualTo(2.2596).Within(1e-4));

            // Past the slider's limit: it shows its end, the inputs and the row keep the value.
            inputs.SetLength(499.95f);
            Row("Beam length", "Plus").onClick.Invoke();
            yield return Frames();
            Assert.That(inputs.lengthCm, Is.EqualTo(500.05f).Within(1e-3f));
            var slider = Object.FindObjectsByType<Slider>().First(s => GetPath(s.transform).Contains("Beam Length (cm)"));
            Assert.That(slider.value, Is.EqualTo(HoloMechRanges.LengthMaxCm));
            Assert.That(Value("Beam length"), Is.EqualTo("5000.5 mm"));

            // Save snapshot: one more row and one more JSON file. The Editor's data folder is put back afterwards.
            string csv = Path.Combine(Application.persistentDataPath, "site_snapshots.csv");
            string folder = Path.Combine(Application.persistentDataPath, "snapshots");
            string before = File.Exists(csv) ? File.ReadAllText(csv) : null;
            var filesBefore = Directory.Exists(folder) ? Directory.GetFiles(folder) : new string[0];
            try
            {
                Find(Card + "Save snapshot").onClick.Invoke();
                string[] lines = File.ReadAllLines(csv);
                Assert.That(lines[0], Is.EqualTo(SiteSnapshot.CsvHeader));
                string[] row = lines[lines.Length - 1].Split(',');
                Assert.That(row[1], Is.EqualTo("manual"));
                Assert.That(row[2], Is.EqualTo("Gantry"));
                Assert.That(row[3], Is.EqualTo("5000.5"));

                string[] added = Directory.GetFiles(folder).Except(filesBefore).ToArray();
                Assert.That(added.Length, Is.EqualTo(1));
                var loaded = JsonUtility.FromJson<SiteSnapshot>(File.ReadAllText(added[0]));
                Assert.That(loaded.inputs.lengthCm, Is.EqualTo(inputs.lengthCm));
                Assert.That(loaded.posX, Is.EqualTo(view.transform.position.x).Within(1e-3f));
            }
            finally
            {
                foreach (var f in Directory.Exists(folder) ? Directory.GetFiles(folder).Except(filesBefore) : new string[0]) File.Delete(f);
                if (before != null) File.WriteAllText(csv, before);
                else if (File.Exists(csv)) File.Delete(csv);
            }

            // Another preset leaves the gantry: its rows and note go away.
            site.SetSiteMode(false);
            Find("Presets/Simple").onClick.Invoke();
            yield return Frames();
            Assert.That(inputs.preset, Is.EqualTo(SupportPreset.Simple));
            Assert.That(Row("Left support", "Plus"), Is.Null);
            Assert.That(Text("Gantry Note"), Is.Null, "inactive");
            Assert.That(ownExceptions, Is.Empty);
        }
    }
}
#endif
