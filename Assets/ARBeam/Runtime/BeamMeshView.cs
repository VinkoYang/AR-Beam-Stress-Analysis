using UnityEngine;

namespace ARBeam
{
    /// <summary>
    /// Bends the beam mesh by the solver's deflection (relative to the unloaded beam, times an amplification
    /// factor) and colours each vertex by the section's maximum bending stress on a fixed scale.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public class BeamMeshView : MonoBehaviour
    {
        [SerializeField] BeamController beam;

        [Tooltip("1 = true δ/L. Larger values exaggerate the deflected shape.")]
        [SerializeField, Min(0f)] float deflectionAmplification = 1f;

        [SerializeField] bool colorByStress = true;

        [Tooltip("If the source mesh has fewer slices along the beam than this, it is replaced by a subdivided box with the same bounds so it can bend smoothly.")]
        [SerializeField, Min(2)] int minSegments = 64;

        Mesh mesh;
        Vector3[] rest;
        Vector3[] deformed;
        Color[] colors;
        double[] vertexX; // metres from the beam's left end, per vertex

        public float DeflectionAmplification
        {
            get => deflectionAmplification;
            set
            {
                deflectionAmplification = Mathf.Max(0f, value);
                Apply();
            }
        }

        /// <summary>UI hook for a slider.</summary>
        public void SetDeflectionAmplification(float value) => DeflectionAmplification = value;

        void Awake()
        {
            if (beam == null) beam = GetComponentInParent<BeamController>();
            BuildMesh();
        }

        void OnEnable()
        {
            if (beam == null) return;
            beam.Solved += OnSolved;
            Apply();
        }

        void OnDisable()
        {
            if (beam != null) beam.Solved -= OnSolved;
        }

        void OnValidate()
        {
            if (Application.isPlaying && mesh != null) Apply();
        }

        void OnSolved(BeamController _) => Apply();

        void BuildMesh()
        {
            var filter = GetComponent<MeshFilter>();
            var source = filter.sharedMesh;
            if (source == null || beam == null) return;

            // Map from the rest pose before anything is deformed.
            double length = beam.LengthM;
            mesh = CountSlices(source, length) < minSegments + 1
                ? BoxMeshBuilder.Build(source.bounds, minSegments)
                : Instantiate(source);
            mesh.name = source.name + " (deformable)";
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;

            rest = mesh.vertices;
            deformed = new Vector3[rest.Length];
            colors = new Color[rest.Length];
            vertexX = new double[rest.Length];
            for (int i = 0; i < rest.Length; i++)
                vertexX[i] = beam.WorldToBeamX(transform.TransformPoint(rest[i]));
        }

        int CountSlices(Mesh source, double length)
        {
            var seen = new System.Collections.Generic.HashSet<long>();
            foreach (var v in source.vertices)
                seen.Add((long)System.Math.Round(beam.WorldToBeamX(transform.TransformPoint(v)) / length * 10000));
            return seen.Count;
        }

        void Apply()
        {
            if (mesh == null || beam == null || beam.Solution == null) return;

            // Gravity deflection is along world up. Converting one scaled vector to local space handles the
            // rotated, non-uniformly scaled transforms the scenes use.
            Vector3 localUpPerMetre = transform.InverseTransformVector(Vector3.up * beam.WorldUnitsPerMetre);
            float stressScale = beam.StressScalePa;
            var solution = beam.Solution;

            for (int i = 0; i < rest.Length; i++)
            {
                double x = vertexX[i];
                float v = (float)(beam.DisplayDeflection(x) * deflectionAmplification);
                deformed[i] = rest[i] + localUpPerMetre * v;
                if (colorByStress)
                    colors[i] = StressColors.Evaluate(stressScale > 0 ? (float)(solution.BendingStress(x) / stressScale) : 0f);
            }

            mesh.vertices = deformed;
            if (colorByStress) mesh.colors = colors;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}
