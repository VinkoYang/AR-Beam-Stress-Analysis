using System.Collections.Generic;
using ARBeam.Mechanics;
using UnityEngine;

namespace ARBeam
{
    /// <summary>
    /// One hanging mass driven by UI: a position slider (0–1 along the beam) and a mass slider (grams).
    /// Optionally moves a load arrow and writes readouts.
    /// </summary>
    public class SliderLoadSource : BeamLoadSource
    {
        [SerializeField, Range(0f, 1f)] float position01 = 1f;
        [SerializeField, Min(0f)] float massGrams;

        [Tooltip("Round the mass to the nearest hanger the rig actually has.")]
        [SerializeField] bool snapToHangerMasses;
        [SerializeField] float[] hangerMassesGrams = { 0f, 100f, 200f, 500f };

        [Header("Optional UI")]
        [Tooltip("If set, the starting values are read from these sliders so the UI and the load agree.")]
        [SerializeField] UnityEngine.UI.Slider massSlider;
        [SerializeField] UnityEngine.UI.Slider positionSlider;
        [SerializeField] TMPro.TMP_Text massLabel;
        [SerializeField] TMPro.TMP_Text positionLabel;

        [Header("Optional visuals")]
        [SerializeField] BeamController beam;
        [SerializeField] Transform loadArrow;
        [Tooltip("Arrow offset above the beam axis, scene units.")]
        [SerializeField] float arrowHeight = 0.2f;
        [Tooltip("Move the arrow with the drawn (amplified) deflection of this mesh.")]
        [SerializeField] BeamMeshView followMesh;

        public float Position01 => position01;
        public float MassGrams => massGrams;

        public Transform LoadArrow
        {
            get => loadArrow;
            set => loadArrow = value;
        }

        /// <summary>UI hook: 0 = left end, 1 = right end.</summary>
        public void SetPosition01(float value)
        {
            position01 = Mathf.Clamp01(value);
            OnValuesChanged();
        }

        /// <summary>UI hook: hanging mass in grams.</summary>
        public void SetMassGrams(float value)
        {
            massGrams = snapToHangerMasses ? Snap(value) : Mathf.Max(0f, value);
            OnValuesChanged();
        }

        public override void CollectLoads(BeamController target, List<PointLoad> loads)
        {
            if (massGrams <= 0f) return;
            loads.Add(PointLoad.FromMass(position01 * target.LengthM, massGrams / 1000.0));
        }

        void Awake()
        {
            if (beam == null) beam = GetComponentInParent<BeamController>();
        }

        void Start()
        {
            if (massSlider != null) massGrams = snapToHangerMasses ? Snap(massSlider.value) : Mathf.Max(0f, massSlider.value);
            if (positionSlider != null) position01 = Mathf.Clamp01(positionSlider.normalizedValue);
            OnValuesChanged();
        }

        void LateUpdate()
        {
            // Every frame, because in AR the beam itself can move.
            if (loadArrow == null || beam == null) return;
            double x = position01 * beam.LengthM;
            float drop = followMesh != null
                ? (float)(beam.DisplayDeflection(x) * followMesh.DeflectionAmplification) * beam.WorldUnitsPerMetre
                : 0f;
            loadArrow.position = beam.BeamXToWorld(x) + Vector3.up * (arrowHeight + drop);
        }

        void OnValuesChanged()
        {
            if (massLabel != null) massLabel.text = $"Mass: {massGrams:0} g";
            if (positionLabel != null && beam != null) positionLabel.text = $"x = {position01 * beam.LengthM * 1000:0} mm";
            NotifyChanged();
        }

        float Snap(float grams)
        {
            if (hangerMassesGrams == null || hangerMassesGrams.Length == 0) return Mathf.Max(0f, grams);
            float best = hangerMassesGrams[0];
            foreach (var m in hangerMassesGrams)
                if (Mathf.Abs(m - grams) < Mathf.Abs(best - grams)) best = m;
            return best;
        }
    }
}
