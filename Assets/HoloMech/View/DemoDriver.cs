using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HoloMech
{
    /// <summary>
    /// Scripted demos for the README recordings: drives the real Workbench controls (preset buttons, list entries,
    /// sliders, add buttons, switches) along a timeline in game time, so a Recorder capture at a fixed frame rate shows
    /// smooth, genuine interaction. Not used by the app.
    ///
    /// Controls are found by name under a named ancestor (for example the slider "Distance from left end (cm)" inside
    /// list entry "P1"), not by full hierarchy path, so the demos survive layout changes. A control that cannot be found
    /// is reported with a warning and skipped; check the Console after each take.
    /// </summary>
    public class DemoDriver : MonoBehaviour
    {
        public const string Workbench = "workbench", Inputs = "inputs", Stress = "stress";

        /// <summary>Length of each demo in seconds of game time.</summary>
        public static float Duration(string demo)
        {
            switch (demo)
            {
                case Workbench: return 12f;
                case Inputs: return 15f;
                case Stress: return 11f;
                default: return 10f;
            }
        }

        public void Run(string demo) => StartCoroutine(Script(demo));

        IEnumerator Script(string demo)
        {
            switch (demo)
            {
                case Inputs: return InputsDemo();
                case Stress: return StressDemo();
                default: return WorkbenchDemo();
            }
        }

        static HoloMechApp App => FindAnyObjectByType<HoloMechApp>();

        static void ColourByStress()
        {
            var view = FindAnyObjectByType<HoloMechBeamView>();
            if (view != null) view.ColorMode = BeamColorMode.BendingStress;
        }

        // ------------------------------------------------------------------ demos

        /// <summary>Simply supported beam: P1 travels along the beam and grows, then a second load is added and moved.</summary>
        IEnumerator WorkbenchDemo()
        {
            Click("Presets", "Simple");
            yield return Hold(0.2f);
            var app = App;
            float L = app != null ? app.Inputs.lengthCm : 200f;
            if (app != null && app.Inputs.pointLoads.Count == 0) Click(null, "Add Point Load");
            yield return Hold(0.1f);
            if (FindSlider("P1", "Distance from left end (cm)") == null) Click("P1", "Summary"); // open the entry
            ColourByStress();
            yield return Hold(0.8f);

            var pos = FindSlider("P1", "Distance from left end (cm)");
            var force = FindSlider("P1", "Force Magnitude (N)");
            yield return Glide(pos, L * 0.5f, L * 0.15f, 1.6f);
            yield return Glide(pos, L * 0.15f, L * 0.85f, 2.6f);
            yield return Glide(pos, L * 0.85f, L * 0.4f, 1.2f);
            yield return Glide(force, force != null ? force.value : 2000f, 6000f, 1.2f);
            yield return Hold(0.5f);

            Click(null, "Add Point Load"); // P2 at midspan, 1000 N, opened
            yield return Hold(0.6f);
            var pos2 = FindSlider("P2", "Distance from left end (cm)");
            var force2 = FindSlider("P2", "Force Magnitude (N)");
            yield return Glide(force2, 1000f, 5000f, 1.2f);
            yield return Glide(pos2, L * 0.5f, L * 0.8f, 1.4f);
            yield return Hold(0.9f);
        }

        /// <summary>Support presets, an unstable layout and its banner, a uniform load, an interior support, the Gantry preset.</summary>
        IEnumerator InputsDemo()
        {
            ColourByStress();
            yield return Hold(0.6f);
            Click("Presets", "Cantilever");
            yield return Hold(1.2f);
            Click("Presets", "Simple");
            yield return Hold(1.0f);
            Click("Presets", "Fixed-Fixed");
            yield return Hold(1.0f);
            Click("Presets", "2 Spans");
            yield return Hold(1.3f);

            Click("Presets", "Cantilever");
            yield return Hold(0.5f);
            Click("S1", "Summary");                   // open the clamp
            yield return Hold(0.5f);
            Click("S1", "Pinned");                    // a single pin: mechanism, the banner explains
            yield return Hold(1.8f);
            Click("S1", "Fixed");
            yield return Hold(0.6f);

            Click("Presets", "Simple");
            yield return Hold(0.6f);
            Click(null, "Add Uniform Load");           // full span, opened
            yield return Hold(1.0f);
            var w = FindSlider("w1", "Start from left end (cm)");
            float L = App != null ? App.Inputs.lengthCm : 200f;
            yield return Glide(w, 0f, L * 0.5f, 1.2f);
            Click(null, "Add Support");                // pin in the widest gap
            yield return Hold(1.4f);

            Click("Presets", "Gantry");
            yield return Hold(1.6f);
        }

        /// <summary>Stress view: the section cut moves along the beam showing V, then M, then the normal-stress profile.</summary>
        IEnumerator StressDemo()
        {
            var app = App;
            if (app != null)
            {
                Click("Presets", "Simple");
                var u = app.Inputs.uniformLoads;
                u.Clear();
                u.Add(new UniformLoadInput { enabled = true, startCm = 0f, endCm = app.Inputs.lengthCm, intensityNPerM = 2000f, direction = LoadDirection.Up });
                app.Workbench.Sync();
                app.SetLayout(AppLayout.Workbench);
                app.Go(AppState.Stress);
                app.Workbench.SetCollapsed(true, true);
            }

            var cut = FindSlider("Cut", null);
            float L = app != null ? app.Inputs.lengthCm : 200f;
            yield return Hold(0.6f);
            yield return Glide(cut, L * 0.05f, L * 0.35f, 2.2f);
            Toggle("Bending Moment", true);
            yield return Glide(cut, L * 0.35f, L * 0.65f, 2.6f);
            Toggle("Normal Stress", true);
            yield return Glide(cut, L * 0.65f, L * 0.95f, 2.6f);
            Toggle("Shear Force", true);
            yield return Glide(cut, L * 0.95f, L * 0.5f, 1.6f);
        }

        // ------------------------------------------------------------------ helpers

        static IEnumerator Hold(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        /// <summary>Moves a slider smoothly (ease in-out) so its callbacks fire every frame, as when dragged.</summary>
        static IEnumerator Glide(Slider s, float from, float to, float seconds)
        {
            if (s == null) yield break;
            from = Mathf.Clamp(from, s.minValue, s.maxValue);
            to = Mathf.Clamp(to, s.minValue, s.maxValue);
            float t0 = Time.time;
            while (Time.time - t0 < seconds)
            {
                float u = Mathf.SmoothStep(0, 1, (Time.time - t0) / seconds);
                s.value = Mathf.Lerp(from, to, u);
                yield return null;
            }

            s.value = to;
        }

        static bool HasAncestor(Transform t, string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            for (var p = t.parent; p != null; p = p.parent)
                if (p.name == name) return true;
            return false;
        }

        /// <summary>
        /// The active slider under an object called <paramref name="ancestor"/> whose own name or whose parent's name is
        /// <paramref name="caption"/> (labelled sliders are a "caption" object with the slider inside). A null caption
        /// takes the slider called <paramref name="ancestor"/>, or the first slider under it.
        /// </summary>
        static Slider FindSlider(string ancestor, string caption)
        {
            foreach (var s in FindObjectsByType<Slider>())
            {
                var t = s.transform;
                bool captionOk = caption == null || t.name == caption || (t.parent != null && t.parent.name == caption);
                bool ancestorOk = caption == null ? (t.name == ancestor || HasAncestor(t, ancestor)) : HasAncestor(t, ancestor);
                if (captionOk && ancestorOk) return s;
            }

            return null;
        }

        /// <summary>Presses the active button (or selects the active toggle) called <paramref name="name"/> under <paramref name="ancestor"/>.</summary>
        static void Click(string ancestor, string name)
        {
            foreach (var b in FindObjectsByType<Button>())
                if (b.name == name && HasAncestor(b.transform, ancestor)) { b.onClick.Invoke(); return; }
            foreach (var tg in FindObjectsByType<UnityEngine.UI.Toggle>())
                if (tg.name == name && HasAncestor(tg.transform, ancestor)) { tg.isOn = true; return; } // segmented option (Meta UI Set skin)
            Debug.LogWarning($"DemoDriver: no button '{name}' under '{ancestor ?? "(any)"}'");
        }

        static void Toggle(string name, bool on)
        {
            foreach (var tg in FindObjectsByType<UnityEngine.UI.Toggle>())
                if (tg.name == name || (tg.transform.parent != null && tg.transform.parent.name == name)) { tg.isOn = on; return; }
            Debug.LogWarning($"DemoDriver: no toggle '{name}'");
        }
    }
}
