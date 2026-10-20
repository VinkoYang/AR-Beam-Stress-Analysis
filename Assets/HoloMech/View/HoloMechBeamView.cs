using System;
using System.Collections.Generic;
using ARBeam.Mechanics;
using UnityEngine;

namespace HoloMech
{
    /// <summary>
    /// The holographic beam of HoloMech Scene 4: a grey undeformed beam, the coloured deflected beam (deflection
    /// times an adjustable scale, coloured by |w|), supports as grey blocks, and load arrows (yellow point load,
    /// blue row for a uniform load). Rebuilds itself from <see cref="HoloMechInputs"/>; the beam lies along local +X
    /// with its left end at the local origin, so the view can be moved or scaled as a whole.
    /// </summary>
    public class HoloMechBeamView : MonoBehaviour
    {
        [SerializeField] HoloMechInputs inputs = new HoloMechInputs();

        [Tooltip("Scene units for the whole beam length, whatever its real length.")]
        [SerializeField, Min(0.5f)] float displayLength = 4f;

        [Tooltip("Colour by |w| using a fixed reference instead of this beam's own maximum, so loads can be compared.")]
        [SerializeField] bool lockColorScale;
        [SerializeField, Min(1e-6f)] float lockedDeflectionMetres = 0.01f;

        [Header("Materials")]
        [Tooltip("Needs vertex colours (the student's BeamStressMaterial / BeamStressShader does).")]
        [SerializeField] Material stressMaterial;
        [SerializeField] Material ghostMaterial;
        [SerializeField] Material supportMaterial;
        [SerializeField] Material pointLoadMaterial;
        [SerializeField] Material uniformLoadMaterial;

        BeamMeshBuilder deformed, ghost;
        MeshFilter deformedFilter, ghostFilter;
        Transform supportsRoot, loadsRoot;
        GameObject pointArrow;
        readonly List<GameObject> uniformArrows = new List<GameObject>();

        float deflectionScale = 1f;
        bool showDeformation = true, showLoading = true;
        string builtSignature;

        /// <summary>Raised after a successful solve.</summary>
        public event Action<HoloMechBeamView> Solved;

        /// <summary>Raised when the inputs cannot be solved (message for the user).</summary>
        public event Action<string> SolveFailed;

        public HoloMechInputs Inputs => inputs;
        public BeamSolution Solution { get; private set; }
        public float DisplayUnitsPerMetre { get; private set; } = 1f;
        public float DeflectionScale => deflectionScale;
        public float DisplayLength => displayLength;

        /// <summary>Largest |w| in metres under the current loads, the figure the panel shows.</summary>
        public double MaxDeflectionMetres => Solution != null ? Math.Abs(Solution.PeakDeflection.Value) : 0;

        public bool ShowDeformation
        {
            get => showDeformation;
            set { showDeformation = value; ApplyVisibility(); }
        }

        public bool ShowLoading
        {
            get => showLoading;
            set { showLoading = value; ApplyVisibility(); }
        }

        public bool LockColorScale
        {
            get => lockColorScale;
            set { lockColorScale = value; Refresh(); }
        }

        /// <summary>A scale that makes the largest deflection about 10 % of the beam length (the spec's default).</summary>
        public float DefaultDeflectionScale()
        {
            double w = MaxDeflectionMetres * DisplayUnitsPerMetre;
            if (w < 1e-12) return 1f;
            return Mathf.Clamp((float)(0.1 * displayLength / w), HoloMechRanges.ScaleMin, HoloMechRanges.ScaleMax);
        }

        public void SetDeflectionScale(float scale)
        {
            deflectionScale = Mathf.Clamp(scale, HoloMechRanges.ScaleMin, HoloMechRanges.ScaleMax);
            Refresh();
        }

        public void SetMaterials(Material stress, Material ghostMat, Material support, Material pointLoad, Material uniformLoad)
        {
            stressMaterial = stress;
            ghostMaterial = ghostMat;
            supportMaterial = support;
            pointLoadMaterial = pointLoad;
            uniformLoadMaterial = uniformLoad;
        }

        void Awake()
        {
            EnsureChildren();
        }

        void Start()
        {
            Rebuild(true);
        }

        /// <summary>Solves with new inputs and redraws. Returns false (and raises SolveFailed) if the beam cannot be solved.</summary>
        public bool Apply(HoloMechInputs newInputs, bool resetScale = true)
        {
            inputs = newInputs.Clone();
            return Rebuild(resetScale);
        }

        /// <summary>Re-solves the current inputs (after editing <see cref="Inputs"/> in place).</summary>
        public bool Rebuild(bool resetScale)
        {
            EnsureChildren();
            if (!inputs.TrySolve(out var solution, out var error))
            {
                SolveFailed?.Invoke(error);
                return false;
            }

            Solution = solution;
            DisplayUnitsPerMetre = displayLength / (float)inputs.LengthM;

            string signature = Signature();
            if (signature != builtSignature)
            {
                BuildMeshes();
                builtSignature = signature;
            }

            BuildSupports();
            BuildLoads();
            if (resetScale) deflectionScale = DefaultDeflectionScale();
            Refresh();
            ApplyVisibility();
            Solved?.Invoke(this);
            return true;
        }

        /// <summary>Redraws the deflected beam for the current scale and colour mode.</summary>
        public void Refresh()
        {
            if (deformed == null || Solution == null) return;
            double wMax = lockColorScale ? lockedDeflectionMetres : Math.Max(MaxDeflectionMetres, 1e-15);
            double length = inputs.LengthM;
            float units = DisplayUnitsPerMetre;
            deformed.Apply(
                f => (float)Solution.Deflection(f * length) * units * deflectionScale,
                f => (float)(Math.Abs(Solution.Deflection(f * length)) / wMax));
        }

        // ------------------------------------------------------------------ construction

        string Signature()
        {
            return $"{inputs.section}|{inputs.pipe.outerDiameterCm}|{inputs.pipe.wallCm}|{inputs.box.widthCm}|{inputs.box.heightCm}|" +
                   $"{inputs.box.wallCm}|{inputs.iBeam.heightCm}|{inputs.iBeam.flangeWidthCm}|{inputs.iBeam.flangeThicknessCm}|" +
                   $"{inputs.iBeam.webThicknessCm}|{inputs.lengthCm}";
        }

        void EnsureChildren()
        {
            if (deformedFilter != null) return;
            deformedFilter = NewMeshChild("Deformed beam");
            ghostFilter = NewMeshChild("Undeformed beam");
            supportsRoot = new GameObject("Supports").transform;
            supportsRoot.SetParent(transform, false);
            loadsRoot = new GameObject("Loads").transform;
            loadsRoot.SetParent(transform, false);
        }

        MeshFilter NewMeshChild(string name)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            return go.GetComponent<MeshFilter>();
        }

        void BuildMeshes()
        {
            var profile = SectionProfile.For(inputs);
            if (deformed != null) Destroy(deformed.Mesh);
            if (ghost != null) Destroy(ghost.Mesh);
            deformed = BeamMeshBuilder.Build(profile, inputs.LengthM, DisplayUnitsPerMetre);
            ghost = BeamMeshBuilder.Build(profile, inputs.LengthM, DisplayUnitsPerMetre, 2);
            ghost.Apply(null, null);

            deformedFilter.sharedMesh = deformed.Mesh;
            ghostFilter.sharedMesh = ghost.Mesh;
            // Slightly larger than the real section so its faces never coincide with the deformed beam's (z-fighting).
            ghostFilter.transform.localScale = new Vector3(1f, 1.012f, 1.012f);
            deformedFilter.GetComponent<MeshRenderer>().sharedMaterial = stressMaterial != null ? stressMaterial : (stressMaterial = ProceduralShapes.Lit(Color.white));
            ghostFilter.GetComponent<MeshRenderer>().sharedMaterial = ghostMaterial != null ? ghostMaterial : (ghostMaterial = ProceduralShapes.Transparent(new Color(0.78f, 0.8f, 0.85f, 0.38f)));
        }

        void BuildSupports()
        {
            Clear(supportsRoot);
            if (supportMaterial == null) supportMaterial = ProceduralShapes.Lit(new Color(0.78f, 0.8f, 0.84f), 0.1f);
            float u = DisplayUnitsPerMetre;
            float halfH = (float)SectionProfile.For(inputs, 16).Height / 2 * u;
            float width = (float)SectionProfile.For(inputs, 16).Width * u;
            float block = Mathf.Max(halfH * 3f, 0.4f);

            foreach (var s in Solution.Model.ResolvedSupports)
            {
                float x = (float)s.Position * u;
                if (s.Kind == SupportKind.Fixed)
                {
                    bool left = s.Position < inputs.LengthM / 2;
                    var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(wall.GetComponent<Collider>());
                    wall.name = left ? "Fixed support (left)" : "Fixed support (right)";
                    wall.transform.SetParent(supportsRoot, false);
                    float thick = 0.18f;
                    wall.transform.localScale = new Vector3(thick, block * 1.4f, Mathf.Max(block * 1.4f, width * 2.2f));
                    wall.transform.localPosition = new Vector3(x + (left ? -thick / 2 : thick / 2), 0, 0);
                    wall.GetComponent<MeshRenderer>().sharedMaterial = supportMaterial;
                }
                else
                {
                    var tri = new GameObject("Pinned support", typeof(MeshFilter), typeof(MeshRenderer));
                    tri.transform.SetParent(supportsRoot, false);
                    tri.GetComponent<MeshFilter>().sharedMesh = ProceduralShapes.SupportTriangle();
                    tri.GetComponent<MeshRenderer>().sharedMaterial = supportMaterial;
                    float size = Mathf.Max(halfH * 2.6f, 0.28f);
                    tri.transform.localScale = new Vector3(size, size, Mathf.Max(width * 1.4f, size));
                    tri.transform.localPosition = new Vector3(x, -halfH, 0);
                }
            }
        }

        void BuildLoads()
        {
            Clear(loadsRoot);
            pointArrow = null;
            uniformArrows.Clear();
            if (pointLoadMaterial == null) pointLoadMaterial = ProceduralShapes.Lit(new Color(1f, 0.78f, 0.1f), 0.2f);
            if (uniformLoadMaterial == null) uniformLoadMaterial = ProceduralShapes.Lit(new Color(0.2f, 0.55f, 0.95f), 0.2f);

            float u = DisplayUnitsPerMetre;
            float halfH = (float)SectionProfile.For(inputs, 16).Height / 2 * u;
            float arrowLength = Mathf.Max(displayLength * 0.16f, halfH * 3f);

            if (inputs.pointLoad.enabled && inputs.pointLoad.forceN != 0)
            {
                bool down = inputs.pointLoad.direction == LoadDirection.Down;
                pointArrow = NewArrow("Point load", pointLoadMaterial, inputs.pointLoad.positionCm / 100f * u, down, halfH, arrowLength, 1f);
            }

            if (inputs.uniformLoad.enabled && inputs.uniformLoad.intensityNPerM != 0 && inputs.uniformLoad.endCm > inputs.uniformLoad.startCm)
            {
                bool down = inputs.uniformLoad.direction == LoadDirection.Down;
                float x0 = inputs.uniformLoad.startCm / 100f * u, x1 = inputs.uniformLoad.endCm / 100f * u;
                int count = Mathf.Clamp(Mathf.RoundToInt((x1 - x0) / (arrowLength * 0.28f)) + 1, 2, 24);
                for (int i = 0; i < count; i++)
                    uniformArrows.Add(NewArrow("Uniform load", uniformLoadMaterial, Mathf.Lerp(x0, x1, i / (count - 1f)), down, halfH, arrowLength * 0.8f, 0.8f));
            }
        }

        /// <summary>An arrow whose tip touches the beam surface: above the beam pointing down, or below pointing up.</summary>
        GameObject NewArrow(string name, Material material, float x, bool down, float halfHeight, float length, float thickness)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(loadsRoot, false);
            go.GetComponent<MeshFilter>().sharedMesh = ProceduralShapes.Arrow();
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            float w = length * 0.55f * thickness;
            go.transform.localScale = new Vector3(w, length, w);
            // Mesh points +Y. Down: tail above, flipped; up: tail below.
            go.transform.localRotation = down ? Quaternion.Euler(0, 0, 180) : Quaternion.identity;
            go.transform.localPosition = down ? new Vector3(x, halfHeight + length, 0) : new Vector3(x, -halfHeight - length, 0);
            return go;
        }

        void ApplyVisibility()
        {
            if (deformedFilter != null) deformedFilter.gameObject.SetActive(showDeformation);
            if (loadsRoot != null) loadsRoot.gameObject.SetActive(showLoading);
        }

        static void Clear(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
        }
    }
}
