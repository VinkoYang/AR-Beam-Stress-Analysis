using System;
using System.Globalization;
using System.IO;
using ARBeam;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HoloMech
{
    /// <summary>
    /// On-site overlay (PI's request, 2026-10-28): in mixed reality the beam leaves the window and is shown at true scale
    /// (1 scene metre = 1 real metre) so it can be laid onto a real member, e.g. the beam of a lab gantry crane, and its
    /// stress colour map read in place. Loads stay simulated; a label over the beam says so.
    ///
    /// Placement, coarse to fine:
    /// 1. Grab the handle above the beam (hand, controller or ray). The beam follows and turns about the vertical only.
    /// 2. "Align by 2 points": touch the left end of the real beam with a controller and press A (right) or X (left),
    ///    then the right end. The beam's left end goes to the first point, its axis to the line between the points,
    ///    and (optionally) its length to the distance between them.
    /// 3. Nudge buttons move the beam along its axis, up/down and sideways, and turn it, by a chosen step.
    ///
    /// The controls live on a card in the Workbench beam panel; <see cref="HoloMechApp"/> creates this component when
    /// it runs in the Quest window.
    /// </summary>
    public class SitePlacement : MonoBehaviour
    {
        UiTheme t;
        HoloMechBeamView beam;
        HoloMechInputs inputs;
        WorkbenchLayout workbench;
        Action changed;
        XRWindowLayout window;

        Transform handle;
        LevelGrabTransformer grab;
        TextMeshPro label;
        TMP_Text status;
        Toggle siteToggle, lengthToggle;
        Action<int> setTouch;
        Button alignButton;

        bool siteMode;
        Transform homeParent;
        Vector3 homePosition, homeScale;
        Quaternion homeRotation;
        BeamColorMode homeColor;
        bool placedOnce;

        enum Capture { None, LeftEnd, RightEnd }
        Capture capture;
        Vector3 leftPoint;

        float stepMetres = 0.01f;
        const float StepDegrees = 0.5f;

        /// <summary>Where the controller touched the real beam: on its top surface, or (by eye) on its axis.</summary>
        bool touchTop = true;
        bool spanSetsLength = true;

        public bool SiteMode => siteMode;

        public void Setup(UiTheme theme, HoloMechBeamView view, HoloMechInputs appInputs, WorkbenchLayout panels, Action onInputsChanged,
                          XRWindowLayout windowLayout)
        {
            t = theme;
            beam = view;
            inputs = appInputs;
            workbench = panels;
            changed = onInputsChanged;
            window = windowLayout;

            grab = beam.GetComponentInChildren<LevelGrabTransformer>(true);
            if (grab != null)
            {
                handle = grab.transform;
                handle.gameObject.SetActive(false);
                grab.Ended += () => SetStatus("Beam moved. Fine-tune with the buttons below.");
            }

            if (window != null) window.Resetting += () => SetSiteMode(false);
            beam.Solved += _ => { if (siteMode) ApplyTrueScale(); };
            BuildCard(workbench.AddCard(true, "On-site Overlay (MR)"));
            BuildLabel();
        }

        // ------------------------------------------------------------------ UI

        void BuildCard(RectTransform c)
        {
            siteToggle = UiKit.Switch(t, c, "On-site mode", "On-site mode (true scale)", false);
            Height(siteToggle.gameObject, 56);
            siteToggle.onValueChanged.AddListener(SetSiteMode);

            status = UiKit.Label(t, c, "Status", "", t.regular, t.captionSize, t.textSecondary, TextAlignmentOptions.TopLeft);
            status.textWrappingMode = TextWrappingModes.Normal;
            Height(status.gameObject, 64);
            SetStatus("Turn on to lay the beam onto a real member at true size. Loads stay simulated.");

            alignButton = UiKit.Button(t, c, "Align by 2 points", "Align by 2 points", t.captionSize + 2);
            Height(alignButton.gameObject, 52);
            alignButton.onClick.AddListener(StartAlignment);

            setTouch = Choice(c, "Touched", new[] { "Top surface", "Axis" }, 0, i => touchTop = i == 0);
            lengthToggle = UiKit.Switch(t, c, "Span sets length", "Touched span sets beam length", true);
            Height(lengthToggle.gameObject, 52);
            lengthToggle.onValueChanged.AddListener(on => spanSetsLength = on);

            Choice(c, "Step", new[] { "5 mm", "1 cm", "5 cm" }, 1, i => stepMetres = i == 0 ? 0.005f : i == 1 ? 0.01f : 0.05f);
            Nudge(c, "Along beam", v => Move(beam.transform.right * v));
            Nudge(c, "Up / down", v => Move(Vector3.up * v));
            Nudge(c, "Sideways", v => Move(beam.transform.forward * v));
            Nudge(c, "Turn (0.5°)", v => Turn(Mathf.Sign(v) * StepDegrees));
            Interactable();
        }

        static void Height(GameObject go, float h)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            le.minHeight = h;
        }

        Action<int> Choice(RectTransform parent, string caption, string[] options, int selected, Action<int> set)
        {
            var row = UiKit.Rect(caption, parent);
            Height(row.gameObject, 48);
            var l = UiKit.Label(t, row, "Caption", caption, t.medium, t.captionSize + 2, t.textPrimary, TextAlignmentOptions.MidlineLeft);
            l.rectTransform.anchorMin = new Vector2(0, 0);
            l.rectTransform.anchorMax = new Vector2(0, 1);
            l.rectTransform.pivot = new Vector2(0, 0.5f);
            l.rectTransform.sizeDelta = new Vector2(120, 0);
            l.rectTransform.anchoredPosition = Vector2.zero;
            var setter = UiKit.Segmented(t, row, "Options", options, selected, set);
            var seg = (RectTransform)row.Find("Options");
            seg.anchorMin = Vector2.zero;
            seg.anchorMax = Vector2.one;
            seg.offsetMin = new Vector2(128, 0);
            seg.offsetMax = Vector2.zero;
            return setter;
        }

        /// <summary>Caption and a − / + pair; the action gets ± the current step.</summary>
        void Nudge(RectTransform parent, string caption, Action<float> act, string minus = "−", string plus = "+")
        {
            var row = UiKit.Rect(caption, parent);
            Height(row.gameObject, 48);
            var l = UiKit.Label(t, row, "Caption", caption, t.medium, t.captionSize + 2, t.textPrimary, TextAlignmentOptions.MidlineLeft);
            l.rectTransform.Stretch(Vector2.zero, new Vector2(-240, 0));
            var down = UiKit.Button(t, row, "Minus", minus, t.labelSize);
            down.GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-122, 0), new Vector2(114, 46));
            var up = UiKit.Button(t, row, "Plus", plus, t.labelSize);
            up.GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(114, 46));
            down.onClick.AddListener(() => { if (siteMode) act(-stepMetres); });
            up.onClick.AddListener(() => { if (siteMode) act(stepMetres); });
        }

        void Interactable()
        {
            if (alignButton != null) alignButton.interactable = siteMode;
        }

        void SetStatus(string text)
        {
            if (status != null) status.text = text;
        }

        void BuildLabel()
        {
            var go = new GameObject("On-site Label");
            label = go.AddComponent<TextMeshPro>();
            label.font = t.semiBold;
            label.fontSize = 0.6f;
            label.alignment = TextAlignmentOptions.Bottom;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(3f, 0.4f);
            go.SetActive(false);
        }

        // ------------------------------------------------------------------ site mode

        public void SetSiteMode(bool on)
        {
            if (on == siteMode) return;
            siteMode = on;
            capture = Capture.None;
            siteToggle.isOn = on; // re-enters with siteMode already set, so it returns at once
            var bt = beam.transform;
            if (on)
            {
                homeParent = bt.parent;
                homePosition = bt.localPosition;
                homeRotation = bt.localRotation;
                homeScale = bt.localScale;
                homeColor = beam.ColorMode;

                bt.SetParent(null, true);
                if (!placedOnce) PlaceInFrontOfUser();
                beam.AutoScale = false;
                beam.SetDeflectionScale(1f); // true deflection: on a real member it is too small to see; the colours carry the result
                beam.ColorMode = BeamColorMode.BendingStress;
                ApplyTrueScale();
                SetStatus("Grab the handle above the beam to move it, or use Align by 2 points.");
            }
            else
            {
                bt.SetParent(homeParent, false);
                bt.localPosition = homePosition;
                bt.localRotation = homeRotation;
                bt.localScale = homeScale;
                beam.ColorMode = homeColor;
                beam.AutoScale = true;
                beam.SetDeflectionScale(beam.DefaultDeflectionScale());
                SetStatus("Turn on to lay the beam onto a real member at true size. Loads stay simulated.");
            }

            if (handle != null) handle.gameObject.SetActive(on);
            label.gameObject.SetActive(on);
            Interactable();
        }

        void ApplyTrueScale()
        {
            beam.transform.localScale = Vector3.one * beam.TrueScale;
        }

        /// <summary>First use: the beam 1.5 m ahead of the user, 30 cm below eye height, square to the view.</summary>
        void PlaceInFrontOfUser()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            float span = (float)inputs.LengthM;
            beam.transform.position = cam.transform.position + forward * 1.5f - Vector3.up * 0.3f - right * span / 2f;
            beam.transform.rotation = Quaternion.LookRotation(Vector3.Cross(right, Vector3.up), Vector3.up);
            placedOnce = true;
        }

        void Move(Vector3 delta)
        {
            beam.transform.position += delta;
            placedOnce = true;
        }

        void Turn(float degrees)
        {
            beam.transform.RotateAround(Midspan(), Vector3.up, degrees);
        }

        Vector3 Midspan() => beam.transform.position + beam.transform.right * (float)inputs.LengthM / 2f;

        // ------------------------------------------------------------------ two-point alignment

        void StartAlignment()
        {
            if (!siteMode) return;
            capture = Capture.LeftEnd;
            SetStatus("Touch the LEFT end of the real beam with a controller, then press A (right) or X (left).");
        }

        void Update()
        {
            if (!siteMode || capture == Capture.None) return;
            Vector3? point = null;
            var rig = FindRig();
            if (rig == null) return;
            if (OVRInput.GetDown(OVRInput.Button.One)) point = rig.rightControllerAnchor.position;
            else if (OVRInput.GetDown(OVRInput.Button.Three)) point = rig.leftControllerAnchor.position;
            if (point == null) return;

            if (capture == Capture.LeftEnd)
            {
                leftPoint = point.Value;
                capture = Capture.RightEnd;
                SetStatus("Now touch the RIGHT end and press A or X.");
            }
            else
            {
                capture = Capture.None;
                Align(leftPoint, point.Value);
            }
        }

        OVRCameraRig rig;
        OVRCameraRig FindRig() => rig != null ? rig : rig = FindAnyObjectByType<OVRCameraRig>();

        /// <summary>Lays the beam between two touched points: left end, level axis through both, length = their span.</summary>
        void Align(Vector3 left, Vector3 right)
        {
            var axis = Vector3.ProjectOnPlane(right - left, Vector3.up);
            float span = axis.magnitude;
            if (span < 0.2f)
            {
                SetStatus("The two points are less than 20 cm apart. Try again.");
                return;
            }

            axis /= span;
            float drop = touchTop ? (float)SectionProfile.For(inputs, 16).Height / 2f : 0f;
            beam.transform.rotation = Quaternion.LookRotation(Vector3.Cross(axis, Vector3.up), Vector3.up);
            beam.transform.position = left - Vector3.up * drop;
            placedOnce = true;

            string lengthNote = "";
            if (spanSetsLength)
            {
                float cm = Mathf.Clamp(Mathf.Round(span * 100f), HoloMechRanges.LengthMinCm, HoloMechRanges.LengthMaxCm);
                inputs.SetLength(cm);
                workbench.Sync();
                changed?.Invoke();
                lengthNote = $" Beam length set to {cm:0} cm.";
            }

            SetStatus($"Aligned. Touched span {span * 100f:0.0} cm, height difference {(right.y - left.y) * 100f:0.0} cm.{lengthNote}");
            LogAlignment(left, right, span);
        }

        /// <summary>
        /// Appends one row per two-point alignment to alignment_log.csv in the app's persistent data folder (on the Quest:
        /// /sdcard/Android/data/&lt;package&gt;/files/), for the registration-accuracy trials.
        /// </summary>
        void LogAlignment(Vector3 left, Vector3 right, float span)
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, "alignment_log.csv");
                bool header = !File.Exists(path);
                var c = CultureInfo.InvariantCulture;
                using (var w = new StreamWriter(path, true))
                {
                    if (header) w.WriteLine("time_utc,left_x,left_y,left_z,right_x,right_y,right_z,span_m,touched_top,length_cm");
                    w.WriteLine(string.Join(",", DateTime.UtcNow.ToString("o", c),
                        left.x.ToString("F4", c), left.y.ToString("F4", c), left.z.ToString("F4", c),
                        right.x.ToString("F4", c), right.y.ToString("F4", c), right.z.ToString("F4", c),
                        span.ToString("F4", c), touchTop ? "1" : "0", inputs.lengthCm.ToString("F1", c)));
                }
                Debug.Log("[SitePlacement] alignment logged to " + path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SitePlacement] could not write the alignment log: " + e.Message);
            }
        }

        // ------------------------------------------------------------------ handle and label

        void LateUpdate()
        {
            if (!siteMode) return;
            var bt = beam.transform;
            float halfH = (float)SectionProfile.For(inputs, 16).Height / 2f;
            var mid = Midspan();

            if (handle != null)
            {
                handle.localScale = Vector3.one / Mathf.Max(bt.localScale.x, 1e-6f);
                handle.position = mid + Vector3.up * (halfH + 0.12f);
                handle.rotation = bt.rotation;
            }

            if (label != null && beam.Solution != null)
            {
                var s = beam.Solution;
                label.text = "<color=#FFC34D>SIMULATED LOADS</color>\n" +
                             $"σmax = {s.MaxBendingStress / 1e6:0.##} MPa at x = {s.PeakMoment.Position:0.00} m";
                label.transform.position = mid + Vector3.up * (halfH + 0.32f);
                var cam = Camera.main;
                if (cam != null)
                {
                    var away = label.transform.position - cam.transform.position;
                    if (away.sqrMagnitude > 1e-4f) label.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
                }
            }
        }
    }
}
