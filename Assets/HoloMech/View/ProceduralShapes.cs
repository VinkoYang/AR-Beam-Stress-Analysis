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

        /// <summary>Lit URP material with a flat colour (falls back to Unlit, then Standard).</summary>
        public static Material Lit(Color color, float smoothness = 0.3f)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
            var m = new Material(shader) { color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        /// <summary>Lit URP material, alpha-blended (reference geometry that must not hide what is behind it).</summary>
        public static Material Transparent(Color color)
        {
            var m = Lit(color, 0.2f);
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
    }
}