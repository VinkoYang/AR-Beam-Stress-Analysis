using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HoloMech
{
    /// <summary>
    /// Scripted demos for the README recordings: drives the real UI controls (sliders, buttons, toggles) along a timeline
    /// in game time, so a Recorder capture at a fixed frame rate shows smooth, genuine interaction. Not used by the app.
    /// </summary>
    public class DemoDriver : MonoBehaviour
    {
        public const string Workbench = "workbench", Inputs = "inputs", Stress = "stress", Rig = "rig";

        /// <summary>Length of each demo in seconds of game time.</summary>
        public static float Duration(string demo)
        {
            switch (demo)
            {
                case Workbench: return 9f;
                case Inputs: return 13f;
                case Stress: return 11f;
                default: return 9f;
            }
        }

        public void Run(string demo) => StartCoroutine(Script(demo));

        IEnumerator Script(string demo)
        {
            switch (demo)
            {
                case Workbench: return WorkbenchDemo();
                case Inputs: return InputsDemo();
                case Stress: return StressDemo();
                default: return RigDemo();
            }
        }

        // ------------------------------------------------------------------ demos

        IEnumerator WorkbenchDemo()
        {
            var pos = SliderAt("Point Load Card/Content/Distance from left end (cm)/Slider");
            var force = SliderAt("Point Load Card/Content/Force Magnitude (N)/Slider");
            yield return Hold(0.6f);
            yield return Glide(pos, 100, 25, 1.6f);
            yield return Glide(pos, 25, 175, 3.0f);
            yield return Glide(pos, 175, 100, 1.4f);
            yield return Glide(force, 2000, 6000, 1.4f);
            yield return Hold(1f);
        }

        IEnumerator InputsDemo()
        {
            yield return Hold(0.8f);
            Click("Section Card/Content/Choice/Options/Box");
            yield return Hold(1.1f);
            Click("Section Card/Content/Choice/Options/I-Beam");
            yield return Hold(1.1f);
            Click("Section Card/Content/Choice/Options/Pipe");
            yield return Hold(0.9f);
            Click("Right end/Options/Free");          // cantilever
            yield return Hold(1.4f);
            Click("Left end/Options/Pinned");         // Pinned–Free: unstable, banner
            yield return Hold(1.8f);
            Click("Right end/Options/Pinned");        // simply supported
            yield return Hold(1.3f);
            Toggle("Uniform Load Card/Content/Enabled", true);
            yield return Hold(1.0f);
            Click("Uniform Load Card/Content/Direction/Options/Up  ↑");
            yield return Hold(1.3f);
            Click("Left end/Options/Fixed");
            Click("Right end/Options/Fixed");
            yield return Hold(1.5f);
        }

        IEnumerator StressDemo()
        {
            var app = FindAnyObjectByType<HoloMechApp>();
            app.Inputs.uniformLoad.enabled = true;
            app.Inputs.uniformLoad.direction = LoadDirection.Up;
            app.Workbench.Sync();
            app.SetLayout(AppLayout.Workbench);
            app.Go(AppState.Stress);
            app.Workbench.SetCollapsed(true, true);
            var cut = SliderAt("Cut/Slider");
            yield return Hold(0.6f);
            yield return Glide(cut, 10, 70, 2.2f);
            Toggle("Bending Moment", true);
            yield return Glide(cut, 70, 130, 2.6f);
            Toggle("Normal Stress", true);
            yield return Glide(cut, 130, 190, 2.6f);
            Toggle("Shear Force", true);
            yield return Glide(cut, 190, 100, 1.6f);
        }

        IEnumerator RigDemo()
        {
            var mass = SliderAt("Load Slider");
            var position = SliderAt("Position Slider");
            yield return Hold(0.5f);
            yield return Glide(mass, 0, 500, 2.6f);
            yield return Hold(0.6f);
            yield return Glide(position, 1, 0.2f, 2.6f);
            yield return Glide(position, 0.2f, 1, 2.0f);
            yield return Hold(0.7f);
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
            float t0 = Time.time;
            while (Time.time - t0 < seconds)
            {
                float u = Mathf.SmoothStep(0, 1, (Time.time - t0) / seconds);
                s.value = Mathf.Lerp(from, to, u);
                yield return null;
            }

            s.value = to;
        }

        static Slider SliderAt(string path)
        {
            var go = GameObject.Find(path);
            if (go == null) Debug.LogWarning($"DemoDriver: no slider at '{path}'");
            return go != null ? go.GetComponent<Slider>() : null;
        }

        static void Click(string path)
        {
            var go = GameObject.Find(path);
            if (go == null) { Debug.LogWarning($"DemoDriver: no button at '{path}'"); return; }
            go.GetComponent<Button>().onClick.Invoke();
        }

        static void Toggle(string path, bool on)
        {
            var go = GameObject.Find(path);
            if (go == null) { Debug.LogWarning($"DemoDriver: no toggle at '{path}'"); return; }
            go.GetComponent<UnityEngine.UI.Toggle>().isOn = on;
        }
    }
}
