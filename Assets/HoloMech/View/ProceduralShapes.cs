using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoloMech
{
    /// <summary>Small procedural meshes (no asset files): arrow, triangular support, and runtime materials.</summary>
    public static class ProceduralShapes
    {
        static Mesh arrow, prism;

        /// <summary>Arrow of unit length along +Y, tail at the origin, tip at y = 1. Shaft and head are square in section.</summary>
        public static Mesh Arrow()
        {
            if (arrow != null) return arrow;
            var v = new List<Vector3>();
            var t = new List<int>();
            const float shaft = 0.045f, head = 0.14f, headStart = 0.68f;
            Box(v, t, -shaft, 0, -shaft, shaft, headStart, shaft);
            // Pyramid head.
            int b = v.Count;
            v.Add(new Vector3(-head, headStart, -head));
            v.Add(new Vector3(head, headStart, -head));
            v.Add(new Vector3(head, headStart, head));
            v.Add(new Vector3(-head, headStart, head));
            v.Add(new Vector3(0, 1, 0));
            t.AddRange(new[] { b, b + 1, b + 4, b + 1, b + 2, b + 4, b + 2, b + 3, b + 4, b + 3, b, b + 4, b, b + 2, b + 1, b, b + 3, b + 2 });
            arrow = Make("HoloMech Arrow", v, t);
            return arrow;
        }

        /// <summary>Triangular prism: apex at the origin, base 1 wide and 1 tall below it, 1 deep along Z.</summary>
        public static Mesh SupportTriangle()
        {
            if (prism != null) return prism;
            var v = new List<Vector3>();
            var t = new List<int>();
            const float h = 0.5f;
            Vector3[] front = { new Vector3(0, 0, -h), new Vector3(-0.5f, -1, -h), new Vector3(0.5f, -1, -h) };
            Vector3[] back = { new Vector3(0, 0, h), new Vector3(-0.5f, -1, h), new Vector3(0.5f, -1, h) };
            v.AddRange(front);
            v.AddRange(back);
            t.AddRange(new[] { 0, 2, 1, 3, 4, 5 });                           // end faces
            t.AddRange(new[] { 0, 1, 4, 0, 4, 3, 0, 3, 5, 0, 5, 2, 1, 2, 5, 1, 5, 4 }); // sides
            prism = Make("HoloMech Support", v, t);
            return prism;
        }

        static void Box(List<Vector3> v, List<int> t, float x0, float y0, float z0, float x1, float y1, float z1)
        {
            int b = v.Count;
            v.Add(new Vector3(x0, y0, z0)); v.Add(new Vector3(x1, y0, z0)); v.Add(new Vector3(x1, y0, z1)); v.Add(new Vector3(x0, y0, z1));
            v.Add(new Vector3(x0, y1, z0)); v.Add(new Vector3(x1, y1, z0)); v.Add(new Vector3(x1, y1, z1)); v.Add(new Vector3(x0, y1, z1));
            int[] q = { 0, 1, 5, 4, 1, 2, 6, 5, 2, 3, 7, 6, 3, 0, 4, 7, 4, 5, 6, 7, 3, 2, 1, 0 };
            for (int i = 0; i < q.Length; i += 4)
            {
                t.Add(b + q[i]); t.Add(b + q[i + 1]); t.Add(b + q[i + 2]);
                t.Add(b + q[i]); t.Add(b + q[i + 2]); t.Add(b + q[i + 3]);
            }
        }

        static Mesh Make(string name, List<Vector3> v, List<int> t)
        {
            // Duplicate per triangle so normals are flat; also fix winding to face away from the centroid.
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var centre = Vector3.zero;
            foreach (var p in v) centre += p / v.Count;
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - centre) < 0) (b, c) = (c, b);
                int k = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
            }

            var m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Folder under Resources with one material per kind (see HoloMechSceneBuilder.BuildRuntimeMaterials).</summary>
        public const string TemplateFolder = "HoloMechMaterials";

        /// <summary>
        /// Copies a template material from Resources when there is one: a player build keeps only the shaders and
        /// keyword variants that some asset uses, so materials made from <see cref="Shader.Find"/> alone would render
        /// pink on a device. Without templates (Editor, first run) the shader is looked up by name.
        /// </summary>
        static Material FromTemplate(string kind, Func<Shader> fallback)
        {
            var template = Resources.Load<Material>(TemplateFolder + "/" + kind);
            return template != null ? new Material(template) : new Material(fallback());
        }

        /// <summary>Lit URP material with a flat colour (falls back to Unlit, then Standard).</summary>
        public static Material Lit(Color color, float smoothness = 0.3f)
        {
            var m = FromTemplate("Lit", () => Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard"));
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        /// <summary>Lit URP material, alpha-blended (reference geometry that must not hide what is behind it).</summary>
        public static Material Transparent(Color color)
        {
            var m = FromTemplate("LitTransparent", () => Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
            if (!m.HasProperty("_Surface")) return m;
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
            return m;
        }

        /// <summary>Unlit colour (lines, labels' companions, diagram curves).</summary>
        public static Material Unlit(Color color)
        {
            var m = FromTemplate(color.a < 0.999f ? "UnlitTransparent" : "Unlit", () => Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            // Double-sided: a line that doubles back (a jump in a shear diagram) flips its triangle strip, and with
            // back-face culling everything after the turn disappears.
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            if (color.a < 0.999f && m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            return m;
        }

        /// <summary>A LineRenderer child in local space.</summary>
        public static LineRenderer Line(Transform parent, string name, Material material, float width, IList<Vector3> points, bool loop = false)
        {
            var go = new GameObject(name, typeof(LineRenderer));
            go.transform.SetParent(parent, false);
            var lr = go.GetComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.sharedMaterial = material;
            lr.widthMultiplier = width;
            lr.loop = loop;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            // Zero-length segments (repeated points, e.g. both sides of a diagram key point that has no jump) break
            // the line's triangle strip for every point after them, so drop exact repeats.
            var clean = new List<Vector3>(points.Count);
            foreach (var p in points)
                if (clean.Count == 0 || (p - clean[clean.Count - 1]).sqrMagnitude > 1e-12f) clean.Add(p);
            lr.positionCount = clean.Count;
            lr.SetPositions(clean.ToArray());
            return lr;
        }

        /// <summary>
        /// A polyline in the local XY plane built as one quad per segment plus a small square at every vertex, so
        /// joints stay filled whatever the angle (diagram jumps double back on themselves, which LineRenderer does
        /// not draw reliably).
        /// </summary>
        public static GameObject PlanarPolyline(Transform parent, string name, Material material, float width, IList<Vector3> points)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float h = width / 2;
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int k = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
                tris.Add(k); tris.Add(k + 2); tris.Add(k + 3);
            }

            for (int i = 0; i + 1 < points.Count; i++)
            {
                Vector3 a = points[i], b = points[i + 1];
                var dir = b - a;
                dir.z = 0;
                if (dir.sqrMagnitude < 1e-12f) continue;
                var n = new Vector3(-dir.y, dir.x, 0).normalized * h;
                Quad(a - n, a + n, b + n, b - n);
            }

            foreach (var p in points)
                Quad(p + new Vector3(-h, -h, 0), p + new Vector3(-h, h, 0), p + new Vector3(h, h, 0), p + new Vector3(h, -h, 0));

            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}