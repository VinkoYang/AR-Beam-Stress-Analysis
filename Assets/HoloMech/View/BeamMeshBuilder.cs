using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoloMech
{
    /// <summary>
    /// Procedural beam mesh: the section outline extruded along local +X from 0 to the beam length, with a number of
    /// stations. Each station is rigidly translated in local Y by the deflection there (sections do not distort),
    /// and vertex colours follow any per-station value.
    /// </summary>
    public sealed class BeamMeshBuilder
    {
        public Mesh Mesh { get; }
        public int Segments { get; }
        public float Length { get; }

        readonly Vector3[] rest;
        readonly Vector3[] shown;
        readonly Color[] colors;
        readonly float[] station; // x fraction 0..1 of every vertex

        BeamMeshBuilder(Mesh mesh, Vector3[] rest, float[] station, int segments, float length)
        {
            Mesh = mesh;
            this.rest = rest;
            this.station = station;
            Segments = segments;
            Length = length;
            shown = new Vector3[rest.Length];
            colors = new Color[rest.Length];
        }

        /// <param name="unitsPerMetre">Scene units per real metre; the section is scaled like the length.</param>
        /// <param name="startMetres">Where this piece starts along the beam (pieces either side of a section cut).</param>
        public static BeamMeshBuilder Build(SectionProfile profile, double lengthMetres, float unitsPerMetre, int segments = 240,
            double startMetres = 0)
        {
            float length = (float)lengthMetres * unitsPerMetre;
            float x0 = (float)startMetres * unitsPerMetre;
            var verts = new List<Vector3>();
            var stations = new List<float>();
            var tris = new List<int>();

            foreach (var loop in profile.Loops)
            {
                int n = loop.Count;
                // Sharp outlines get their own vertex pair per edge (flat shading); smooth ones share vertices.
                int columns = loop.Smooth ? n : 2 * n;
                int first = verts.Count;
                int trisFrom = tris.Count;
                for (int s = 0; s <= segments; s++)
                {
                    float f = (float)s / segments;
                    for (int c = 0; c < columns; c++)
                    {
                        int k = loop.Smooth ? c : (c / 2 + (c % 2)) % n;
                        verts.Add(new Vector3(x0 + f * length, (float)loop.V[k] * unitsPerMetre, (float)loop.U[k] * unitsPerMetre));
                        stations.Add(f);
                    }
                }

                for (int s = 0; s < segments; s++)
                {
                    int rowA = first + s * columns, rowB = rowA + columns;
                    if (loop.Smooth)
                    {
                        for (int c = 0; c < columns; c++)
                            AddQuad(tris, rowA + c, rowA + (c + 1) % columns, rowB + (c + 1) % columns, rowB + c);
                    }
                    else
                    {
                        for (int e = 0; e < n; e++)
                            AddQuad(tris, rowA + 2 * e, rowA + 2 * e + 1, rowB + 2 * e + 1, rowB + 2 * e);
                    }
                }

                FixWinding(verts, tris, trisFrom, loop.IsHole);
            }

            // End caps, flat shaded, facing -X at the left end and +X at the right end.
            for (int end = 0; end < 2; end++)
            {
                float f = end;
                int start = tris.Count;
                for (int i = 0; i < profile.CapU.Count; i++)
                {
                    verts.Add(new Vector3(x0 + f * length, (float)profile.CapV[i] * unitsPerMetre, (float)profile.CapU[i] * unitsPerMetre));
                    stations.Add(f);
                    tris.Add(verts.Count - 1);
                }

                float wantX = end == 0 ? -1f : 1f;
                for (int t = start; t < tris.Count; t += 3)
                {
                    var n = Vector3.Cross(verts[tris[t + 1]] - verts[tris[t]], verts[tris[t + 2]] - verts[tris[t]]);
                    if (n.x * wantX < 0) (tris[t + 1], tris[t + 2]) = (tris[t + 2], tris[t + 1]);
                }
            }

            var mesh = new Mesh { name = "HoloMech Beam", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.MarkDynamic();
            return new BeamMeshBuilder(mesh, verts.ToArray(), stations.ToArray(), segments, length);
        }

        static void AddQuad(List<int> t, int a, int b, int c, int d)
        {
            t.Add(a); t.Add(b); t.Add(c);
            t.Add(a); t.Add(c); t.Add(d);
        }

        /// <summary>Flips every triangle from <paramref name="from"/> if the first one faces the wrong way.</summary>
        static void FixWinding(List<Vector3> v, List<int> t, int from, bool towardAxis)
        {
            var p0 = v[t[from]];
            var n = Vector3.Cross(v[t[from + 1]] - p0, v[t[from + 2]] - p0);
            var radial = new Vector3(0, p0.y, p0.z); // every section outline is centred on the axis
            bool outward = Vector3.Dot(n, radial) > 0;
            if (outward == towardAxis)
                for (int k = from; k < t.Count; k += 3) (t[k + 1], t[k + 2]) = (t[k + 2], t[k + 1]);
        }

        /// <summary>
        /// Deflects and colours the mesh. <paramref name="deflectionUnits"/> gives the vertical shift (scene units,
        /// up positive) at a station fraction 0..1; <paramref name="colorT"/> gives the colour parameter 0..1.
        /// </summary>
        public void Apply(Func<float, float> deflectionUnits, Func<float, float> colorT)
        {
            var dy = new float[Segments + 1];
            var ct = new Color[Segments + 1];
            for (int s = 0; s <= Segments; s++)
            {
                float f = (float)s / Segments;
                dy[s] = deflectionUnits != null ? deflectionUnits(f) : 0f;
                ct[s] = colorT != null ? ColorScale.Evaluate(colorT(f)) : Color.gray;
            }

            for (int i = 0; i < rest.Length; i++)
            {
                int s = Mathf.Clamp(Mathf.RoundToInt(station[i] * Segments), 0, Segments);
                shown[i] = rest[i] + new Vector3(0, dy[s], 0);
                colors[i] = ct[s];
            }

            Mesh.vertices = shown;
            Mesh.colors = colors;
            Mesh.RecalculateNormals();
            Mesh.RecalculateBounds();
        }
    }
}
