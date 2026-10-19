using ARBeam.Mechanics;
using UnityEngine;

namespace ARBeam
{
    /// <summary>
    /// Base for the three original scene scripts (CantileverBeam, OverhangBeam, BeamBend). Keeps their public
    /// slider methods so the scenes' UI events still resolve, and forwards them to a <see cref="SliderLoadSource"/>.
    /// Nothing here is mechanics; the solver only sees the load source.
    /// </summary>
    [RequireComponent(typeof(SliderLoadSource))]
    public abstract class SliderDrivenBeam : BeamController
    {
        [Tooltip("Arrow from the original scene. Handed to the SliderLoadSource if that has none.")]
        [SerializeField] protected Transform loadArrow;

        SliderLoadSource sliders;

        protected SliderLoadSource Sliders
        {
            get
            {
                if (sliders == null) sliders = GetComponent<SliderLoadSource>();
                if (sliders == null) sliders = gameObject.AddComponent<SliderLoadSource>();
                return sliders;
            }
        }

        protected virtual void Awake()
        {
            if (Sliders.LoadArrow == null) Sliders.LoadArrow = loadArrow;
        }

        /// <summary>Mass slider, grams.</summary>
        public void SetLoadMagnitude(float grams) => Sliders.SetMassGrams(grams);

        /// <summary>Position slider, 0 = left end, 1 = right end.</summary>
        public void SetLoadPosition(float value) => Sliders.SetPosition01(value);

        protected void ApplyPreset(SupportType type, float lengthMillimetres, float aMm = 0f, float bMm = 0f)
        {
            supportType = type;
            lengthMm = lengthMillimetres;
            supportAMm = aMm;
            supportBMm = bMm;
            MarkConfigChanged();
        }
    }
}
