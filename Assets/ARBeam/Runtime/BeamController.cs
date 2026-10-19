using System;
using System.Collections.Generic;
using ARBeam.Mechanics;
using UnityEngine;

namespace ARBeam
{
    public enum BeamMaterialPreset
    {
        Aluminum6061T6,
        Brass260,
        Steel1018,
        Custom,
    }

    /// <summary>
    /// Holds the real beam's parameters, gathers loads from <see cref="BeamLoadSource"/>s, runs
    /// <see cref="BeamSolver"/> when something changed, and maps between beam coordinates (metres along the beam)
    /// and the scene. Views (<see cref="BeamMeshView"/>, <see cref="BeamDiagramGraphic"/>) listen to
    /// <see cref="Solved"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class BeamController : MonoBehaviour
    {
        [Header("Beam (real dimensions)")]
        [SerializeField, Min(1f)] protected float lengthMm = 400f;
        [SerializeField, Min(0.1f)] protected float widthMm = 25.4f;
        [SerializeField, Min(0.1f)] protected float thicknessMm = 3.175f;
        [SerializeField] protected BeamMaterialPreset material = BeamMaterialPreset.Aluminum6061T6;
        [SerializeField, Min(0.1f)] protected float customModulusGPa = 69f;
        [SerializeField, Min(0f)] protected float customDensityKgPerM3 = 2700f;

        [Header("Supports")]
        [SerializeField] protected SupportType supportType = SupportType.Cantilever;
        [Tooltip("Overhang only: pin position from the left end, mm.")]
        [SerializeField, Min(0f)] protected float supportAMm = 200f;
        [Tooltip("Overhang only: roller position from the left end, mm.")]
        [SerializeField, Min(0f)] protected float supportBMm = 600f;

        [Header("Loads")]
        [Tooltip("Adds ρAg to V, M, σ and the reactions. Deflection is always shown relative to the unloaded beam.")]
        [SerializeField] protected bool includeSelfWeight;
        [Tooltip("Load sources feeding this beam. Empty = every BeamLoadSource on this object and its children.")]
        [SerializeField] protected List<BeamLoadSource> loadSources = new List<BeamLoadSource>();

        [Header("Fixed colour and diagram scales")]
        [Tooltip("The worst placement of this mass defines full scale for σ, |V|, |M| and |δ|. Scales do not change with the current load.")]
        [SerializeField, Min(0.001f)] protected float designMassKg = 0.5f;
        [Tooltip("Stress at the top of the colour scale, MPa. 0 = derive from the design mass.")]
        [SerializeField, Min(0f)] protected float stressScaleOverrideMPa;

        [Header("Placement in the scene")]
        [Tooltip("Take the beam axis from the MeshFilter bounds on this object (local X, min to max).")]
        [SerializeField] protected bool axisFromMesh = true;
        [SerializeField] protected Vector3 axisStartLocal = Vector3.zero;
        [SerializeField] protected Vector3 axisEndLocal = Vector3.right;

        readonly List<PointLoad> collected = new List<PointLoad>();
        readonly List<BeamLoadSource> subscribed = new List<BeamLoadSource>();
        bool dirty = true;
        bool scalesDirty = true;
        bool axisResolved;
        BeamSolution selfWeightOnly;
        float stressScale, shearScale, momentScale, deflectionScale;

        /// <summary>Raised after every re-solve.</summary>
        public event Action<BeamController> Solved;

        /// <summary>Current solution (null until the first solve).</summary>
        public BeamSolution Solution { get; private set; }

        public double LengthM => lengthMm / 1000.0;
        public BeamSection Section => BeamSection.Rectangular(widthMm / 1000.0, thicknessMm / 1000.0);
        public BeamMaterial Material => ResolveMaterial();
        public BeamSupports Supports => ResolveSupports();

        public SupportType SupportType
        {
            get => supportType;
            set { supportType = value; MarkConfigChanged(); }
        }

        public bool IncludeSelfWeight
        {
            get => includeSelfWeight;
            set { includeSelfWeight = value; MarkConfigChanged(); }
        }

        /// <summary>Full-scale bending stress for colouring, Pa.</summary>
        public float StressScalePa { get { EnsureScales(); return stressScale; } }

        /// <summary>Full-scale |V| for diagrams, N.</summary>
        public float ShearScale { get { EnsureScales(); return shearScale; } }

        /// <summary>Full-scale |M| for diagrams, N·m.</summary>
        public float MomentScale { get { EnsureScales(); return momentScale; } }

        /// <summary>Full-scale |δ| for diagrams, m.</summary>
        public float DeflectionScale { get { EnsureScales(); return deflectionScale; } }

        /// <summary>Scene units per real metre along the beam.</summary>
        public float WorldUnitsPerMetre
        {
            get
            {
                EnsureAxis();
                return Vector3.Distance(transform.TransformPoint(axisStartLocal), transform.TransformPoint(axisEndLocal)) / (float)LengthM;
            }
        }

        /// <summary>
        /// Deflection caused by the applied loads only (self-weight removed), m, upward positive. This is what the
        /// overlay draws: the real beam already sags under its own weight.
        /// </summary>
        public double DisplayDeflection(double x)
        {
            if (Solution == null) return 0;
            double v = Solution.Deflection(x);
            return selfWeightOnly != null ? v - selfWeightOnly.Deflection(x) : v;
        }

        public Vector3 BeamXToWorld(double x)
        {
            EnsureAxis();
            float t = (float)(x / LengthM);
            return transform.TransformPoint(Vector3.LerpUnclamped(axisStartLocal, axisEndLocal, t));
        }

        /// <summary>Projects a world point onto the beam axis; returns metres from the left end, clamped to [0, L].</summary>
        public double WorldToBeamX(Vector3 world)
        {
            EnsureAxis();
            Vector3 a = transform.TransformPoint(axisStartLocal);
            Vector3 ab = transform.TransformPoint(axisEndLocal) - a;
            float t = Mathf.Clamp01(Vector3.Dot(world - a, ab) / ab.sqrMagnitude);
            return t * LengthM;
        }

        /// <summary>Sets the beam axis from two world points (e.g. tracked markers at the beam ends).</summary>
        public void SetAxisWorld(Vector3 leftEnd, Vector3 rightEnd)
        {
            axisFromMesh = false;
            axisStartLocal = transform.InverseTransformPoint(leftEnd);
            axisEndLocal = transform.InverseTransformPoint(rightEnd);
            MarkDirty();
        }

        public void AddLoadSource(BeamLoadSource source)
        {
            if (source == null || loadSources.Contains(source)) return;
            loadSources.Add(source);
            Subscribe(source);
            MarkDirty();
        }

        public void RemoveLoadSource(BeamLoadSource source)
        {
            if (!loadSources.Remove(source)) return;
            source.Changed -= OnSourceChanged;
            subscribed.Remove(source);
            MarkDirty();
        }

        /// <summary>Request a re-solve on the next Update.</summary>
        public void MarkDirty() => dirty = true;

        protected virtual void OnEnable()
        {
            if (loadSources.Count == 0) GetComponentsInChildren(true, loadSources);
            foreach (var s in loadSources) Subscribe(s);
            MarkConfigChanged();
        }

        protected virtual void OnDisable()
        {
            foreach (var s in subscribed)
                if (s != null) s.Changed -= OnSourceChanged;
            subscribed.Clear();
        }

        protected virtual void OnValidate()
        {
            supportAMm = Mathf.Clamp(supportAMm, 0f, lengthMm);
            supportBMm = Mathf.Clamp(supportBMm, 0f, lengthMm);
            MarkConfigChanged();
        }

        protected virtual void Update()
        {
            if (dirty) Recompute();
        }

        /// <summary>Solves immediately and raises <see cref="Solved"/>.</summary>
        public void Recompute()
        {
            dirty = false;
            var model = CreateModel();
            collected.Clear();
            foreach (var s in loadSources)
                if (s != null && s.isActiveAndEnabled) s.CollectLoads(this, collected);

            double L = LengthM;
            foreach (var p in collected)
                model.PointLoads.Add(new PointLoad(Math.Min(Math.Max(p.Position, 0), L), p.Force));

            try
            {
                Solution = BeamSolver.Solve(model);
                selfWeightOnly = includeSelfWeight ? BeamSolver.Solve(CreateModel()) : null;
            }
            catch (Exception e) when (e is InvalidOperationException || e is ArgumentException)
            {
                Debug.LogError($"[{name}] Beam model is invalid: {e.Message}", this);
                return;
            }

            Solved?.Invoke(this);
        }

        protected void MarkConfigChanged()
        {
            dirty = true;
            scalesDirty = true;
        }

        BeamModel CreateModel()
        {
            var m = new BeamModel(LengthM, Section, Material.ElasticModulus, Supports);
            if (includeSelfWeight) m.UniformLoads.Add(UniformLoad.SelfWeight(Material, Section, LengthM));
            return m;
        }

        void EnsureScales()
        {
            if (!scalesDirty) return;
            scalesDirty = false;

            // Sweep the design mass along the beam. The fixed scales come from the worst placement, so colours and
            // diagram heights mean the same thing no matter what is on the beam right now.
            double L = LengthM, force = designMassKg * Gravity.Standard;
            double sigma = 0, shear = 0, moment = 0, deflection = 0;
            const int Steps = 64;
            var (a, b) = Supports.Type == SupportType.Overhang ? (Supports.A, Supports.B) : (0.0, L);
            var positions = new List<double> { a, b };
            for (int i = 0; i <= Steps; i++) positions.Add(L * i / Steps);

            try
            {
                foreach (var x in positions)
                {
                    var m = CreateModel();
                    m.PointLoads.Add(new PointLoad(x, force));
                    var s = BeamSolver.Solve(m);
                    sigma = Math.Max(sigma, s.MaxBendingStress);
                    shear = Math.Max(shear, Math.Abs(s.PeakShear.Value));
                    moment = Math.Max(moment, Math.Abs(s.PeakMoment.Value));
                    deflection = Math.Max(deflection, Math.Abs(s.PeakDeflection.Value));
                }
            }
            catch (Exception e) when (e is InvalidOperationException || e is ArgumentException)
            {
                Debug.LogError($"[{name}] Beam model is invalid: {e.Message}", this);
            }

            stressScale = stressScaleOverrideMPa > 0 ? stressScaleOverrideMPa * 1e6f : (float)sigma;
            shearScale = (float)shear;
            momentScale = (float)moment;
            deflectionScale = (float)deflection;
        }

        void EnsureAxis()
        {
            // Read once: the deformed mesh's bounds move, the beam axis must not.
            if (!axisFromMesh || axisResolved) return;
            var mf = GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            var bounds = mf.sharedMesh.bounds;
            axisStartLocal = new Vector3(bounds.min.x, bounds.center.y, bounds.center.z);
            axisEndLocal = new Vector3(bounds.max.x, bounds.center.y, bounds.center.z);
            axisResolved = true;
        }

        BeamSupports ResolveSupports()
        {
            switch (supportType)
            {
                case SupportType.Cantilever: return BeamSupports.Cantilever();
                case SupportType.SimplySupported: return BeamSupports.SimplySupported();
                case SupportType.FixedFixed: return BeamSupports.FixedFixed();
                default:
                    double a = Math.Min(supportAMm, supportBMm) / 1000.0, b = Math.Max(supportAMm, supportBMm) / 1000.0;
                    return BeamSupports.Overhang(a, b);
            }
        }

        BeamMaterial ResolveMaterial()
        {
            switch (material)
            {
                case BeamMaterialPreset.Aluminum6061T6: return BeamMaterial.Aluminum6061T6;
                case BeamMaterialPreset.Brass260: return BeamMaterial.Brass260;
                case BeamMaterialPreset.Steel1018: return BeamMaterial.Steel1018;
                default: return new BeamMaterial("Custom", customModulusGPa * 1e9, customDensityKgPerM3);
            }
        }

        void Subscribe(BeamLoadSource source)
        {
            if (source == null || subscribed.Contains(source)) return;
            source.Changed += OnSourceChanged;
            subscribed.Add(source);
        }

        void OnSourceChanged(BeamLoadSource _) => dirty = true;
    }
}
