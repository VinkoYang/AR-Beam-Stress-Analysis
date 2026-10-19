using System.Collections.Generic;
using ARBeam.Mechanics;
using UnityEngine;
using UnityEngine.UI;

namespace ARBeam
{
    /// <summary>
    /// Shear-force or bending-moment diagram drawn from the live solution. Works on a screen-space canvas (side
    /// panel) and on a world-space canvas laid along the beam (overlay). The beam's left end maps to the left edge
    /// of the rect; positive values are drawn upward (Hibbeler convention, sagging moment up).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class BeamDiagramGraphic : MaskableGraphic
    {
        [SerializeField] BeamController beam;
        [SerializeField] BeamQuantity quantity = BeamQuantity.Moment;
        [SerializeField, Min(1)] int samplesPerSegment = 24;

        [Tooltip("Use the beam's fixed full scale (design mass at its worst position). Off = fit the current curve.")]
        [SerializeField] bool fixedScale = true;

        [SerializeField, Min(0.5f)] float lineWidth = 3f;
        [SerializeField] Color lineColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        [SerializeField] Color positiveFill = new Color(0.95f, 0.45f, 0.2f, 0.45f);
        [SerializeField] Color negativeFill = new Color(0.2f, 0.45f, 0.95f, 0.45f);
        [SerializeField] Color axisColor = new Color(0f, 0f, 0f, 0.6f);

        [Header("Optional labels")]
        [SerializeField] TMPro.TMP_Text titleLabel;
        [SerializeField] TMPro.TMP_Text peakLabel;

        List<DiagramPoint> points = new List<DiagramPoint>();

        public BeamController Beam
        {
            get => beam;
            set
            {
                if (beam == value) return;
                if (isActiveAndEnabled && beam != null) beam.Solved -= OnSolved;
                beam = value;
                if (isActiveAndEnabled && beam != null) beam.Solved += OnSolved;
                Refresh();
            }
        }

        public BeamQuantity Quantity
        {
            get => quantity;
            set { quantity = value; Refresh(); }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            if (beam != null) beam.Solved += OnSolved;
            Refresh();
        }

        protected override void OnDisable()
        {
            if (beam != null) beam.Solved -= OnSolved;
            base.OnDisable();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (Application.isPlaying) Refresh();
        }
#endif

        void OnSolved(BeamController _) => Refresh();

        void Refresh()
        {
            points.Clear();
            if (beam != null && beam.Solution != null)
                points = beam.Solution.Diagram(quantity, samplesPerSegment);
            UpdateLabels();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float midY = r.center.y;
            AddLine(vh, new Vector2(r.xMin, midY), new Vector2(r.xMax, midY), 1f, axisColor);
            if (points.Count < 2 || beam == null) return;

            float scale = fixedScale ? FixedScale() : 0f;
            if (scale <= 0f)
                foreach (var p in points) scale = Mathf.Max(scale, Mathf.Abs((float)p.Value));
            if (scale <= 0f) return;

            float length = (float)beam.LengthM;
            float halfHeight = 0.45f * r.height;
            Vector2 Map(DiagramPoint p) => new Vector2(
                r.xMin + r.width * (float)p.X / length,
                midY + Mathf.Clamp((float)p.Value / scale, -1.1f, 1.1f) * halfHeight);

            for (int i = 1; i < points.Count; i++)
            {
                Vector2 a = Map(points[i - 1]), b = Map(points[i]);
                if (b.x > a.x) AddFill(vh, a, b, midY);
            }

            for (int i = 1; i < points.Count; i++)
                AddLine(vh, Map(points[i - 1]), Map(points[i]), lineWidth, lineColor);
        }

        float FixedScale()
        {
            switch (quantity)
            {
                case BeamQuantity.Shear: return beam.ShearScale;
                case BeamQuantity.Moment: return beam.MomentScale;
                case BeamQuantity.Deflection: return beam.DeflectionScale;
                case BeamQuantity.BendingStress: return beam.StressScalePa;
                default: return 0f;
            }
        }

        /// <summary>Fills between the curve and the axis, split where the curve crosses zero.</summary>
        void AddFill(VertexHelper vh, Vector2 a, Vector2 b, float axisY)
        {
            float da = a.y - axisY, db = b.y - axisY;
            if (da * db < 0f)
            {
                float t = da / (da - db);
                var cross = new Vector2(Mathf.Lerp(a.x, b.x, t), axisY);
                AddFill(vh, a, cross, axisY);
                AddFill(vh, cross, b, axisY);
                return;
            }

            Color c = da + db >= 0f ? positiveFill : negativeFill;
            AddQuad(vh, new Vector2(a.x, axisY), a, b, new Vector2(b.x, axisY), c);
        }

        static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-8f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized * (0.5f * width);
            AddQuad(vh, a - n, a + n, b + n, b - n, c);
        }

        static void AddQuad(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color c)
        {
            int i = vh.currentVertCount;
            var v = UIVertex.simpleVert;
            v.color = c;
            v.position = p0; vh.AddVert(v);
            v.position = p1; vh.AddVert(v);
            v.position = p2; vh.AddVert(v);
            v.position = p3; vh.AddVert(v);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        void UpdateLabels()
        {
            if (titleLabel != null) titleLabel.text = Title(quantity);
            if (peakLabel == null) return;
            if (beam == null || beam.Solution == null)
            {
                peakLabel.text = string.Empty;
                return;
            }

            var s = beam.Solution;
            Extremum peak = quantity == BeamQuantity.Shear ? s.PeakShear
                : quantity == BeamQuantity.Deflection ? s.PeakDeflection
                : s.PeakMoment;
            peakLabel.text = quantity switch
            {
                BeamQuantity.Shear => $"peak V = {peak.Value:0.00} N at x = {peak.Position * 1000:0} mm",
                BeamQuantity.Moment => $"peak M = {peak.Value:0.000} N·m at x = {peak.Position * 1000:0} mm",
                BeamQuantity.Deflection => $"peak δ = {peak.Value * 1000:0.0} mm at x = {peak.Position * 1000:0} mm",
                _ => $"σmax = {s.MaxBendingStress / 1e6:0.0} MPa at x = {peak.Position * 1000:0} mm",
            };
        }

        static string Title(BeamQuantity q) => q switch
        {
            BeamQuantity.Shear => "Shear force V (N)",
            BeamQuantity.Moment => "Bending moment M (N·m)",
            BeamQuantity.Deflection => "Deflection δ (mm)",
            BeamQuantity.Slope => "Slope θ (rad)",
            _ => "Bending stress σ (MPa)",
        };
    }
}
