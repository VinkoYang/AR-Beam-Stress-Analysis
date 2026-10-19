using System.Collections.Generic;
using UnityEngine;

namespace ARBeam
{
    /// <summary>Box mesh subdivided along local X, so it can bend. Flat-shaded faces, UVs run along the length.</summary>
    public static class BoxMeshBuilder
    {
        public static Mesh Build(Bounds bounds, int segments)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            Vector3 min = bounds.min, max = bounds.max;

            // Four long faces: each is a strip of quads along X. Corners listed counter-clockwise seen from outside.
            AddStrip(new Vector3(0, max.y, min.z), new Vector3(0, max.y, max.z)); // top
            AddStrip(new Vector3(0, min.y, max.z), new Vector3(0, min.y, min.z)); // bottom
            AddStrip(new Vector3(0, min.y, min.z), new Vector3(0, max.y, min.z)); // front (−Z)
            AddStrip(new Vector3(0, max.y, max.z), new Vector3(0, min.y, max.z)); // back (+Z)

            AddCap(min.x, false);
            AddCap(max.x, true);

            var mesh = new Mesh();
            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;

            void AddStrip(Vector3 edgeA, Vector3 edgeB)
            {
                int start = vertices.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float t = (float)i / segments;
                    float x = Mathf.Lerp(min.x, max.x, t);
                    vertices.Add(new Vector3(x, edgeA.y, edgeA.z));
                    vertices.Add(new Vector3(x, edgeB.y, edgeB.z));
                    uvs.Add(new Vector2(t, 0));
                    uvs.Add(new Vector2(t, 1));
                }

                for (int i = 0; i < segments; i++)
                {
                    int a = start + 2 * i, b = a + 1, c = a + 2, d = a + 3;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(c); triangles.Add(b); triangles.Add(d);
                }
            }

            void AddCap(float x, bool facingPositiveX)
            {
                int start = vertices.Count;
                vertices.Add(new Vector3(x, min.y, min.z));
                vertices.Add(new Vector3(x, max.y, min.z));
                vertices.Add(new Vector3(x, max.y, max.z));
                vertices.Add(new Vector3(x, min.y, max.z));
                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(0, 1));
                uvs.Add(new Vector2(1, 1));
                uvs.Add(new Vector2(1, 0));
                if (facingPositiveX)
                {
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
                }
                else
                {
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                    triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
                }
            }
        }
    }
}
