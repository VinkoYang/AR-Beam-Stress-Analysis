using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HoloMech
{
    /// <summary>
    /// Fine adjustment of a slider with the thumbstick. Press or drag a slider with the ray to select it (its value turns
    /// the accent colour), then push either thumbstick left or right: one flick = one step, holding repeats after
    /// <see cref="StickRepeater.RepeatDelay"/>, faster near full deflection. B or Y lets go of it; picking another slider moves
    /// the selection there. While something is grabbed the stick belongs to the grab (<see cref="GrabStick"/>), not here.
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public class SliderStick : MonoBehaviour, IPointerDownHandler, IInitializePotentialDragHandler, IBeginDragHandler
    {
        /// <summary>The slider the thumbstick currently adjusts, or null.</summary>
        public static SliderStick Selected { get; private set; }

        /// <summary>Fraction of the range per step when the slider has no step of its own (continuous slider).</summary>
        public const float ContinuousFraction = 1f / 200f;

        Slider slider;
        float step;
        TMP_Text label;
        Color accent, labelColor;
        readonly StickRepeater repeater = new StickRepeater();

        // EventSystem selection when this slider was picked. The Meta UI Set sliders have Navigation = None, so pressing
        // them does not select them in the EventSystem; only a later change of that selection means another control was used.
        GameObject selectionWhenPicked;

        /// <summary>Called by <see cref="UiKit.LabeledSlider"/>. <paramref name="step"/> 0 = continuous.</summary>
        public void Init(float step, TMP_Text valueLabel, Color accentColor)
        {
            this.step = step;
            label = valueLabel;
            accent = accentColor;
            if (label != null) labelColor = label.color;
            if (slider == null) slider = GetComponent<Slider>();
            // The Meta UI Set prefab's child visuals can take the pointer-down, so a value change by the ray also selects.
            slider.onValueChanged.AddListener(_ => { if (!nudging) Select(this); });
        }

        bool nudging;

        void Awake() => slider = GetComponent<Slider>();

        public void OnPointerDown(PointerEventData eventData) => Select(this);
        public void OnInitializePotentialDrag(PointerEventData eventData) => Select(this);
        public void OnBeginDrag(PointerEventData eventData) => Select(this);

        void OnDisable()
        {
            if (Selected == this) Select(null);
        }

        static void Select(SliderStick s)
        {
            if (Selected == s) return;
            if (Selected != null) Selected.Highlight(false);
            Selected = s;
            if (s != null)
            {
                s.repeater.Reset();
                s.selectionWhenPicked = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                s.Highlight(true);
                Debug.Log($"[SliderStick] selected {(s.transform.parent != null ? s.transform.parent.name : "")}/{s.name}");
            }
        }

        void Highlight(bool on)
        {
            if (label != null) label.color = on ? accent : labelColor;
        }

        void Update()
        {
            if (Selected != this) return;

            // Another control was pressed since this slider was picked: the EventSystem selection changed.
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != selectionWhenPicked && es.currentSelectedGameObject != gameObject)
            {
                Select(null);
                return;
            }

            // B or Y lets go of the slider.
            if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch) ||
                OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch))
            {
                Select(null);
                return;
            }

            if (slider == null || !slider.IsInteractable() || GrabStick.GrabbedRecently)
            {
                repeater.Reset();
                return;
            }

            int n = repeater.Tick(GrabStick.Read().x, Time.unscaledDeltaTime);
            if (n == 0) return;
            float next = Nudge(slider.value, StepSize(step, slider.minValue, slider.maxValue), slider.minValue, slider.maxValue, n);
            if (Mathf.Approximately(next, slider.value)) return;
            nudging = true;
            try { slider.value = next; } // fires onValueChanged: snapping + solve
            finally { nudging = false; }
        }

        /// <summary>The step used by the stick: the slider's own step, or a fixed fraction of its range.</summary>
        public static float StepSize(float step, float min, float max) =>
            step > 0f ? step : Mathf.Max(max - min, 0f) * ContinuousFraction;

        /// <summary><paramref name="value"/> moved by <paramref name="steps"/> steps, kept inside [min, max].</summary>
        public static float Nudge(float value, float stepSize, float min, float max, int steps) =>
            Mathf.Clamp(value + steps * stepSize, min, max);
    }

    /// <summary>
    /// Turns one thumbstick axis into discrete steps: a flick past <see cref="PressThreshold"/> gives one step, holding
    /// gives further steps after <see cref="RepeatDelay"/> at <see cref="SlowRate"/>, or <see cref="FastRate"/> beyond
    /// <see cref="FastThreshold"/>. Returning inside <see cref="ReleaseThreshold"/> re-arms it. Plain class for EditMode tests.
    /// </summary>
    public class StickRepeater
    {
        public const float PressThreshold = 0.5f, ReleaseThreshold = 0.3f, FastThreshold = 0.9f;
        public const float RepeatDelay = 0.4f, SlowRate = 8f, FastRate = 30f;

        int dir;
        float held, carry;

        public void Reset()
        {
            dir = 0;
            held = 0f;
            carry = 0f;
        }

        /// <summary>Signed number of steps for this frame.</summary>
        public int Tick(float x, float dt)
        {
            float a = Mathf.Abs(x);
            if (dir == 0)
            {
                if (a < PressThreshold) return 0;
                dir = x > 0 ? 1 : -1;
                held = 0f;
                carry = 0f;
                return dir;
            }

            // Released, or flipped to the other side: re-arm (a flip counts as a new press on the next frame).
            if (a < ReleaseThreshold || Mathf.Sign(x) != dir)
            {
                Reset();
                return 0;
            }

            held += dt;
            if (held < RepeatDelay) return 0;
            carry += dt * (a >= FastThreshold ? FastRate : SlowRate);
            int n = Mathf.FloorToInt(carry);
            carry -= n;
            return n * dir;
        }
    }
}
