using System;
using System.Collections.Generic;
using System.Globalization;
using ARBeam;
using ARBeam.Mechanics;
using TMPro;
using UnityEngine;

namespace HoloMech
{
    /// <summary>What the section cut shows (one at a time, paper Fig. 7).</summary>
    public enum CutQuantity
    {
        ShearForce,
        BendingMoment,
        NormalStress,
    }

    /// <summary>
    /// Scene 5 (paper Fig. 7–8): the undeformed beam cut at a movable section, the part beyond the cut drawn
    /// transparent, the internal action at the cut (shear arrow, moment arc, or the linear normal-stress profile), and
    /// shear-force and bending-moment diagrams as 3D line plots under the beam: shared x axis in metres, shear (cyan) on
    /// a left axis and moment (yellow) on a right axis, nine values each.
    ///
    /// Sign convention as the solver (Hibbeler): the cut face shown is the right-hand face of the left part; positive V
    /// acts downward on it, positive (sagging) M counter-clockwise, and σ = −M·y/I is tension-positive.
    /// </summary>
    public class StressProfileView : MonoBehaviour
    {
        [SerializeField] HoloMechBeamView beam;
        [SerializeField] UiTheme theme;

        float cutMetres = -1f;
        CutQuantity quantity = CutQuantity.ShearForce;
        bool showShearDiagram = true, showMomentDiagram = true;

        Transform root, diagramRoot, shearRoot, momentRoot, cutRoot, cutMarker;
        MeshFilter leftPiece, rightPiece;
        Material solid, lineShear, lineMoment, axis, grid, white, tension, compression, shearArrow, momentArrow;
        double vMin, vMax, mMin, mMax, sigmaMax;

        /// <summary>Raised when the cut, the quantity or the solution changes (the colour bar listens).</summary>
        public event Action Changed;

        public float CutMetres => Mathf.Max(cutMetres, 0f);
        public CutQuantity Quantity => quantity;

        public void Initialise(HoloMechBeamView beamView, UiTheme uiTheme)
        {
            beam = beamView;
            theme = uiTheme;
        }

        // ------------------------------------------------------------------ public controls

        public void Show(bool on)
        {
            EnsureObjects();
            beam.StressMode = on;
            root.gameObject.SetActive(on);
            if (on) Rebuild();
        }

        /// <summary>Recomputes ranges and redraws everything for the current solution.</summary>
        public void Rebuild()
        {
            var s = beam.Solution;
            if (s == null) return;
            EnsureObjects();
            double L = s.Length;
            if (cutMetres < 0 || cutMetres > L) cutMetres = (float)(L * 0.5);

            vMin = vMax = mMin = mMax = 0;
            foreach (var p in s.Diagram(BeamQuantity.Shear, 24)) { vMin = Math.Min(vMin, p.Value); vMax = Math.Max(vMax, p.Value); }
            foreach (var p in s.Diagram(BeamQuantity.Moment, 24)) { mMin = Math.Min(mMin, p.Value); mMax = Math.Max(mMax, p.Value); }
            sigmaMax = s.MaxBendingStress;

            BuildDiagrams();
            ApplyCut();
        }

        public void SetCut(float metres)
        {
            if (beam.Solution == null) return;
            cutMetres = Mathf.Clamp(metres, 0f, (float)beam.Solution.Length);
            ApplyCut();
        }

        public void SetQuantity(CutQuantity q)
        {
            quantity = q;
            ApplyCut();
        }

        public void SetShowDiagrams(bool shear, bool moment)
        {
            showShearDiagram = shear;
            showMomentDiagram = moment;
            if (shearRoot != null) shearRoot.gameObject.SetActive(shear);
            if (momentRoot != null) momentRoot.gameObject.SetActive(moment);
        }

        /// <summary>Value of the selected quantity at the cut, in display units (N, N·m, MPa).</summary>
        public double ValueAtCut()
        {
            var s = beam.Solution;
            if (s == null) return 0;
            switch (quantity)
            {
                case CutQuantity.ShearForce: return s.Shear(CutMetres);
                case CutQuantity.BendingMoment: return s.Moment(CutMetres);
                default: return s.BendingStress(CutMetres) / 1e6;
            }
        }

        /// <summary>Range of the selected quantity along the beam, display units (for the colour bar).</summary>
        public (double min, double max) Range()
        {
            switch (quantity)
            {
                case CutQuantity.ShearForce: return (vMin, vMax);
                case CutQuantity.BendingMoment: return (mMin, mMax);
                default: return (0, sigmaMax / 1e6);
            }
        }

        public string QuantityLabel()
        {
            switch (quantity)
            {
                case CutQuantity.ShearForce: return "Shear Force (N)";
                case CutQuantity.BendingMoment: return "Bending Moment (N·m)";
                default: return "Normal Stress σmax (MPa)";
            }
        }

        // ------------------------------------------------------------------ construction

        void EnsureObjects()
        {
            if (root != null) return;
            root = new GameObject("Stress Profile").transform;
            root.SetParent(beam.transform, false);
            diagramRoot = Child(root, "Diagrams");
            shearRoot = Child(diagramRoot, "Shear Force Diagram");
            momentRoot = Child(diagramRoot, "Bending Moment Diagram");
            cutRoot = Child(root, "Section Cut");

            leftPiece = MeshChild("Beam left of cut");
            rightPiece = MeshChild("Beam right of cut");

            solid = ProceduralShapes.Lit(new Color(0.78f, 0.8f, 0.84f), 0.35f);
            lineShear = ProceduralShapes.Unlit(theme != null ? theme.shearLine : Color.cyan);
            lineMoment = ProceduralShapes.Unlit(theme != null ? theme.momentLine : Color.yellow);
            axis = ProceduralShapes.Unlit(new Color(1f, 1f, 1f, 0.75f));
            grid = ProceduralShapes.Unlit(new Color(1f, 1f, 1f, 0.12f));
            white = ProceduralShapes.Unlit(new Color(0.95f, 0.97f, 1f));
            tension = ProceduralShapes.Unlit(new Color(0.96f, 0.33f, 0.27f));
            compression = ProceduralShapes.Unlit(new Color(0.33f, 0.58f, 1f));
            shearArrow = ProceduralShapes.Lit(new Color(1f, 0.78f, 0.1f), 0.2f);
            momentArrow = ProceduralShapes.Unlit(new Color(1f, 0.6f, 0.15f));
            leftPiece.GetComponent<MeshRenderer>().sharedMaterial = solid;
            rightPiece.GetComponent<MeshRenderer>().sharedMaterial = beam.GhostMaterial != null
                ? beam.GhostMaterial
                : ProceduralShapes.Transparent(new Color(0.78f, 0.8f, 0.85f, 0.3f));
        }

        static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        MeshFilter MeshChild(string name)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            return go.GetComponent<MeshFilter>();
        }

        float Units => beam.DisplayUnitsPerMetre;
        float HalfHeight => (float)beam.Profile.Height / 2 * Units;
        float D => beam.DisplayLength;

        /// <summary>Diagram band under the beam: top edge and height in local units.</summary>
        (float top, float height) Band() => (-HalfHeight - Mathf.Max(0.55f, D * 0.16f), D * 0.36f);

        void BuildDiagrams()
        {
            Clear(shearRoot);
            Clear(momentRoot);
            for (int i = diagramRoot.childCount - 1; i >= 0; i--)
            {
                var c = diagramRoot.GetChild(i);
                if (c != shearRoot && c != momentRoot) Destroy(c.gameObject);
            }

            var s = beam.Solution;
            var (top, height) = Band();
            float bottom = top - height;
            float lw = D * 0.0065f;

            // Frame: x axis with a tick and label every metre, faint vertical grid lines.
            double L = s.Length;
            ProceduralShapes.Line(diagramRoot, "x axis", axis, lw * 0.6f, new[] { new Vector3(0, bottom, 0), new Vector3(D, bottom, 0) });
            int metres = (int)Math.Floor(L + 1e-9);
            for (int m = 0; m <= metres; m++)
            {
                float x = (float)(m / L) * D;
                ProceduralShapes.Line(diagramRoot, $"grid {m}", grid, lw * 0.4f, new[] { new Vector3(x, bottom, 0), new Vector3(x, top, 0) });
                Label(diagramRoot, m.ToString(CultureInfo.InvariantCulture), new Vector3(x, bottom - D * 0.04f, 0), D * 0.04f, TextAlignmentOptions.Center);
            }

            Label(diagramRoot, "x (m)", new Vector3(D * 0.5f, bottom - D * 0.09f, 0), D * 0.036f, TextAlignmentOptions.Center);

            // Nine evenly spaced levels; each axis labels its own values at those heights.
            for (int k = 0; k < 9; k++)
            {
                float y = bottom + height * k / 8f;
                ProceduralShapes.Line(diagramRoot, $"level {k}", grid, lw * 0.3f, new[] { new Vector3(0, y, 0), new Vector3(D, y, 0) });
                Label(shearRoot, Tick(Lerp(vMin, vMax, k / 8.0)), new Vector3(-D * 0.015f, y, 0), D * 0.034f, TextAlignmentOptions.Right);
                Label(momentRoot, Tick(Lerp(mMin, mMax, k / 8.0)), new Vector3(D * 1.015f, y, 0), D * 0.034f, TextAlignmentOptions.Left);
            }

            var titleV = Label(shearRoot, "Shear Force Diagram (N)", new Vector3(-D * 0.17f, bottom + height / 2, 0), D * 0.04f, TextAlignmentOptions.Center);
            titleV.transform.localRotation = Quaternion.Euler(0, 0, 90);
            titleV.color = theme != null ? theme.shearLine : Color.cyan;
            var titleM = Label(momentRoot, "Bending Moment Diagram (N·m)", new Vector3(D * 1.17f, bottom + height / 2, 0), D * 0.04f, TextAlignmentOptions.Center);
            titleM.transform.localRotation = Quaternion.Euler(0, 0, 90);
            titleM.color = theme != null ? theme.momentLine : Color.yellow;

            Curve(shearRoot, s.Diagram(BeamQuantity.Shear, 24), vMin, vMax, lineShear, lw * 1.6f, bottom, height, L);
            Curve(momentRoot, s.Diagram(BeamQuantity.Moment, 24), mMin, mMax, lineMoment, lw * 1.6f, bottom, height, L);

            cutMarker = ProceduralShapes.Line(diagramRoot, "cut marker", white, lw * 0.5f, new[] { Vector3.zero, Vector3.up }).transform;
            SetShowDiagrams(showShearDiagram, showMomentDiagram);
        }

        void Curve(Transform parent, List<DiagramPoint> points, double min, double max, Material mat, float width, float bottom, float height, double L)
        {
            double span = Math.Max(max - min, 1e-12);
            var pts = new List<Vector3>(points.Count);
            foreach (var p in points)
                pts.Add(new Vector3((float)(p.X / L) * D, bottom + (float)((p.Value - min) / span) * height, -0.01f));
            ProceduralShapes.PlanarPolyline(parent, "curve", mat, width, pts);
        }

        void ApplyCut()
        {
            var s = beam.Solution;
            if (s == null || root == null) return;
            double L = s.Length, x = CutMetres;
            var profile = beam.Profile;

            // Beam split at the cut: opaque on the left, transparent beyond.
            ReplaceMesh(leftPiece, x > L * 1e-4 ? BeamMeshBuilder.Build(profile, x, Units, 96) : null);
            ReplaceMesh(rightPiece, L - x > L * 1e-4 ? BeamMeshBuilder.Build(profile, L - x, Units, 96, x) : null);

            // Cut marker across the diagram band.
            var (top, height) = Band();
            float xu = (float)(x / L) * D;
            var lr = cutMarker.GetComponent<LineRenderer>();
            lr.SetPosition(0, new Vector3(xu, top - height, -0.02f));
            lr.SetPosition(1, new Vector3(xu, top + D * 0.02f, -0.02f));

            Clear(cutRoot);
            DrawOutline(profile, xu);
            switch (quantity)
            {
                case CutQuantity.ShearForce: DrawShear(s.Shear(x), xu); break;
                case CutQuantity.BendingMoment: DrawMoment(s.Moment(x), xu); break;
                default: DrawStress(s.Moment(x), xu); break;
            }

            Changed?.Invoke();
        }

        static void ReplaceMesh(MeshFilter f, BeamMeshBuilder b)
        {
            if (f.sharedMesh != null) Destroy(f.sharedMesh);
            if (b != null) b.Apply(null, null);
            f.sharedMesh = b?.Mesh;
        }

        void DrawOutline(SectionProfile profile, float xu)
        {
            foreach (var loop in profile.Loops)
            {
                var pts = new Vector3[loop.Count];
                for (int i = 0; i < loop.Count; i++)
                    pts[i] = new Vector3(xu + 0.002f, (float)loop.V[i] * Units, (float)loop.U[i] * Units);
                ProceduralShapes.Line(cutRoot, "outline", white, D * 0.004f, pts, true);
            }
        }

        /// <summary>Positive V acts downward on the right face of the left part.</summary>
        void DrawShear(double v, float xu)
        {
            double vScale = Math.Max(Math.Abs(vMin), Math.Abs(vMax));
            if (vScale < 1e-12 || Math.Abs(v) < 1e-9 * vScale) return;
            float length = Mathf.Max(D * 0.05f, (float)(Math.Abs(v) / vScale) * D * 0.2f);
            bool down = v > 0;
            var go = new GameObject("Shear", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(cutRoot, false);
            go.GetComponent<MeshFilter>().sharedMesh = ProceduralShapes.Arrow();
            go.GetComponent<MeshRenderer>().sharedMaterial = shearArrow;
            float w = length * 0.35f;
            go.transform.localScale = new Vector3(w, length, w);
            go.transform.localRotation = down ? Quaternion.Euler(0, 0, 180) : Quaternion.identity;
            // In front of the beam (towards the viewer) so the opaque part does not hide it.
            float front = -((float)beam.Profile.Width / 2 * Units + D * 0.03f);
            go.transform.localPosition = new Vector3(xu, down ? length / 2 : -length / 2, front);
        }

        /// <summary>Arc arrow about the beam's transverse axis; sagging (positive) is counter-clockwise on this face.</summary>
        void DrawMoment(double m, float xu)
        {
            double mScale = Math.Max(Math.Abs(mMin), Math.Abs(mMax));
            if (mScale < 1e-12 || Math.Abs(m) < 1e-9 * mScale) return;
            float r = Mathf.Max(HalfHeight * 2.4f, D * 0.06f);
            bool ccw = m > 0;
            const int n = 36;
            float a0 = -110f, a1 = 110f; // arc on the +x side of the face
            var pts = new List<Vector3>();
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad;
                pts.Add(new Vector3(xu + r * Mathf.Cos(a) * 0.55f, r * Mathf.Sin(a), 0));
            }

            if (!ccw) pts.Reverse();
            ProceduralShapes.Line(cutRoot, "Moment", momentArrow, D * 0.008f, pts);

            // Head at the end of the arc, pointing along the tangent.
            Vector3 end = pts[pts.Count - 1], before = pts[pts.Count - 4];
            var head = new GameObject("Moment head", typeof(MeshFilter), typeof(MeshRenderer));
            head.transform.SetParent(cutRoot, false);
            head.GetComponent<MeshFilter>().sharedMesh = ProceduralShapes.Arrow();
            head.GetComponent<MeshRenderer>().sharedMaterial = momentArrow;
            float hl = D * 0.05f;
            head.transform.localScale = new Vector3(hl * 0.9f, hl, hl * 0.9f);
            var dir = (end - before).normalized;
            head.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
            head.transform.localPosition = end - dir * hl * 0.7f;
        }

        /// <summary>σ(y) = −M·y/I across the depth: bars normal to the face, tension red (pulling out), compression blue.</summary>
        void DrawStress(double m, float xu)
        {
            if (sigmaMax < 1e-9) return;
            var sec = beam.Inputs.SectionProperties();
            double c = beam.Profile.Height / 2;
            const int levels = 13;
            var tips = new List<Vector3>();
            for (int k = 0; k < levels; k++)
            {
                double y = -c + 2 * c * k / (levels - 1);
                double sigma = -m * y / sec.I;
                float yu = (float)y * Units;
                float len = (float)(sigma / sigmaMax) * D * 0.14f;
                var from = new Vector3(xu + 0.004f, yu, 0);
                var to = new Vector3(xu + 0.004f + len, yu, 0);
                tips.Add(to);
                if (Mathf.Abs(len) > 1e-4f)
                    ProceduralShapes.Line(cutRoot, "σ", sigma > 0 ? tension : compression, D * 0.004f, new[] { from, to });
            }

            ProceduralShapes.Line(cutRoot, "σ envelope", white, D * 0.003f, tips);
        }

        // ------------------------------------------------------------------ helpers

        TextMeshPro Label(Transform parent, string text, Vector3 position, float height, TextAlignmentOptions align)
        {
            var go = new GameObject("Label", typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshPro>();
            if (theme != null) tmp.font = theme.medium;
            tmp.text = text;
            tmp.fontSize = height * 10f; // TMP 3D: font size 10 ≈ 1 unit line height
            tmp.color = new Color(0.9f, 0.93f, 1f);
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            var rt = tmp.rectTransform;
            rt.sizeDelta = new Vector2(D * 0.6f, height * 1.6f);
            rt.pivot = align == TextAlignmentOptions.Right ? new Vector2(1, 0.5f) : align == TextAlignmentOptions.Left ? new Vector2(0, 0.5f) : new Vector2(0.5f, 0.5f);
            rt.localPosition = position;
            return tmp;
        }

        static string Tick(double v)
        {
            double a = Math.Abs(v);
            string f = a >= 100 ? "0" : a >= 1 ? "0.#" : "0.###";
            return v.ToString(f, CultureInfo.InvariantCulture);
        }

        static double Lerp(double a, double b, double t) => a + (b - a) * t;

        static void Clear(Transform t)
        {
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject);
        }
    }
}
