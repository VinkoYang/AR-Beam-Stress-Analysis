using System;
using System.Collections.Generic;

namespace HoloMech
{
    /// <summary>A closed outline in the section plane: u lateral, v vertical (up), metres.</summary>
    public sealed class ProfileLoop
    {
        public readonly double[] U;
        public readonly double[] V;

        /// <summary>True for curved outlines (pipe): side normals are smoothed across vertices.</summary>
        public readonly bool Smooth;

        /// <summary>Holes run clockwise; outer outlines counter-clockwise.</summary>
        public readonly bool IsHole;

        public ProfileLoop(double[] u, double[] v, bool smooth, bool isHole)
        {
            U = u;
            V = v;
            Smooth = smooth;
            IsHole = isHole;
        }

        public int Count => U.Length;
    }

    /// <summary>
    /// Outline and end-cap triangles of the HoloMech sections, used to extrude the beam mesh. Pure geometry, so the
    /// drawn section can be checked against the section properties the solver uses (area and I agree exactly for
    /// box and I; the pipe is a polygon, so they agree to the polygon approximation).
    /// </summary>
    public sealed class SectionProfile
    {
        public readonly List<ProfileLoop> Loops = new List<ProfileLoop>();

        /// <summary>End-cap triangles as (u, v) triples, counter-clockwise seen from +x.</summary>
        public readonly List<double> CapU = new List<double>();
        public readonly List<double> CapV = new List<double>();

        /// <summary>Overall height and width, m.</summary>
        public double Height { get; private set; }
        public double Width { get; private set; }

        public static SectionProfile For(HoloMechInputs inputs, int pipeSegments = 48)
        {
            const double cm = 0.01;
            switch (inputs.section)
            {
                case SectionType.Pipe: return Pipe(inputs.pipe.outerDiameterCm * cm, inputs.pipe.wallCm * cm, pipeSegments);
                case SectionType.Box: return Box(inputs.box.widthCm * cm, inputs.box.heightCm * cm, inputs.box.wallCm * cm);
                default:
                    return IBeam(inputs.iBeam.heightCm * cm, inputs.iBeam.flangeWidthCm * cm,
                        inputs.iBeam.flangeThicknessCm * cm, inputs.iBeam.webThicknessCm * cm);
            }
        }

        public static SectionProfile Pipe(double outerDiameter, double wall, int segments = 48)
        {
            var p = new SectionProfile { Height = outerDiameter, Width = outerDiameter };
            double ro = outerDiameter / 2, ri = Math.Max(0, ro - wall);
            p.Loops.Add(Circle(ro, segments, false));
            if (ri > 1e-9)
            {
                p.Loops.Add(Circle(ri, segments, true));
                for (int i = 0; i < segments; i++)
                {
                    int j = (i + 1) % segments;
                    double a0 = 2 * Math.PI * i / segments, a1 = 2 * Math.PI * j / segments;
                    p.Quad(ro * Math.Cos(a0), ro * Math.Sin(a0), ro * Math.Cos(a1), ro * Math.Sin(a1),
                        ri * Math.Cos(a1), ri * Math.Sin(a1), ri * Math.Cos(a0), ri * Math.Sin(a0));
                }
            }
            else
            {
                for (int i = 0; i < segments; i++)
                {
                    double a0 = 2 * Math.PI * i / segments, a1 = 2 * Math.PI * (i + 1) / segments;
                    p.Tri(0, 0, ro * Math.Cos(a0), ro * Math.Sin(a0), ro * Math.Cos(a1), ro * Math.Sin(a1));
                }
            }

            return p;
        }

        /// <summary>Rectangle b × h, hollow with wall t (t = 0, or walls that meet, = solid).</summary>
        public static SectionProfile Box(double width, double height, double wall)
        {
            var p = new SectionProfile { Height = height, Width = width };
            double bo = width / 2, ho = height / 2;
            p.Loops.Add(Rect(bo, ho, false));
            bool hollow = wall > 0 && 2 * wall < Math.Min(width, height);
            if (!hollow)
            {
                p.Quad(-bo, -ho, bo, -ho, bo, ho, -bo, ho);
                return p;
            }

            double bi = bo - wall, hi = ho - wall;
            p.Loops.Add(Rect(bi, hi, true));
            p.Quad(-bo, -ho, bo, -ho, bi, -hi, -bi, -hi); // bottom wall
            p.Quad(bo, -ho, bo, ho, bi, hi, bi, -hi);     // right wall
            p.Quad(bo, ho, -bo, ho, -bi, hi, bi, hi);     // top wall
            p.Quad(-bo, ho, -bo, -ho, -bi, -hi, -bi, hi); // left wall
            return p;
        }

        /// <summary>Doubly symmetric I: height h, flange width bf, flange thickness tf, web thickness tw.</summary>
        public static SectionProfile IBeam(double height, double flangeWidth, double flangeThickness, double webThickness)
        {
            var p = new SectionProfile { Height = height, Width = flangeWidth };
            double h = height / 2, b = flangeWidth / 2, w = webThickness / 2, f = h - flangeThickness;
            // Counter-clockwise from the bottom-left corner.
            double[] u = { -b, b, b, w, w, b, b, -b, -b, -w, -w, -b };
            double[] v = { -h, -h, -f, -f, f, f, h, h, f, f, -f, -f };
            p.Loops.Add(new ProfileLoop(u, v, false, false));
            p.Quad(-b, -h, b, -h, b, -f, -b, -f); // bottom flange
            p.Quad(-w, -f, w, -f, w, f, -w, f);   // web
            p.Quad(-b, f, b, f, b, h, -b, h);     // top flange
            return p;
        }

        /// <summary>Area from the outlines (shoelace; holes subtract), m².</summary>
        public double Area()
        {
            double a = 0;
            foreach (var loop in Loops)
                for (int i = 0; i < loop.Count; i++)
                {
                    int j = (i + 1) % loop.Count;
                    a += loop.U[i] * loop.V[j] - loop.U[j] * loop.V[i];
                }

            return a / 2;
        }

        /// <summary>Second moment of area about the horizontal axis v = 0, m⁴ (polygon formula).</summary>
        public double SecondMoment()
        {
            double s = 0;
            foreach (var loop in Loops)
                for (int i = 0; i < loop.Count; i++)
                {
                    int j = (i + 1) % loop.Count;
                    double cross = loop.U[i] * loop.V[j] - loop.U[j] * loop.V[i];
                    s += cross * (loop.V[i] * loop.V[i] + loop.V[i] * loop.V[j] + loop.V[j] * loop.V[j]);
                }

            return s / 12;
        }

        static ProfileLoop Circle(double r, int n, bool hole)
        {
            var u = new double[n];
            var v = new double[n];
            for (int i = 0; i < n; i++)
            {
                // Holes run clockwise so the shoelace sum subtracts them.
                double a = 2 * Math.PI * (hole ? n - i : i) / n;
                u[i] = r * Math.Cos(a);
                v[i] = r * Math.Sin(a);
            }

            return new ProfileLoop(u, v, true, hole);
        }

        static ProfileLoop Rect(double halfWidth, double halfHeight, bool hole)
        {
            double[] u = { -halfWidth, halfWidth, halfWidth, -halfWidth };
            double[] v = { -halfHeight, -halfHeight, halfHeight, halfHeight };
            if (hole)
            {
                Array.Reverse(u);
                Array.Reverse(v);
            }

            return new ProfileLoop(u, v, false, hole);
        }

        void Tri(double u0, double v0, double u1, double v1, double u2, double v2)
        {
            CapU.Add(u0); CapV.Add(v0);
            CapU.Add(u1); CapV.Add(v1);
            CapU.Add(u2); CapV.Add(v2);
        }

        /// <summary>Quad p0 p1 p2 p3 (counter-clockwise) as two triangles.</summary>
        void Quad(double u0, double v0, double u1, double v1, double u2, double v2, double u3, double v3)
        {
            Tri(u0, v0, u1, v1, u2, v2);
            Tri(u0, v0, u2, v2, u3, v3);
        }
    }
}
