using System;
using System.Collections.Generic;

namespace ARBeam.Mechanics.Tests
{
    /// <summary>
    /// Independent reference: direct stiffness method with Hermite cubic beam elements (two DOFs per node, w and
    /// θ, upward / counter-clockwise positive). Point loads sit on nodes; uniform loads use consistent nodal loads
    /// (qL/2, qL²/12). For these loads Hermite elements reproduce the exact nodal displacements, so the nodal
    /// values and reactions must match <see cref="BeamSolver"/> to round-off.
    ///
    /// Used only by tests, to check the Macaulay solver against a method that shares none of its algebra.
    /// </summary>
    static class StiffnessReferenceSolver
    {
        public sealed class Result
        {
            public double[] NodeX;
            public double[] W;
            public double[] Theta;
            public List<Reaction> Reactions = new List<Reaction>();
        }

        public static Result Solve(BeamModel model, int minElements = 200)
        {
            double L = model.Length, ei = model.FlexuralRigidity;
            var supports = model.ResolvedSupports;

            // Nodes at every load and support position, then refined to at least minElements elements.
            var key = new List<double> { 0, L };
            foreach (var s in supports) key.Add(s.Position);
            foreach (var p in model.PointLoads) key.Add(p.Position);
            foreach (var udl in model.UniformLoads)
            {
                key.Add(udl.Start);
                key.Add(udl.End);
            }

            key.Sort();
            var nodes = new List<double>();
            foreach (var x in key)
                if (nodes.Count == 0 || x - nodes[nodes.Count - 1] > 1e-12 * L) nodes.Add(x);
            var refined = new List<double> { nodes[0] };
            for (int i = 1; i < nodes.Count; i++)
            {
                double x0 = nodes[i - 1], x1 = nodes[i];
                int pieces = Math.Max(1, (int)Math.Ceiling((x1 - x0) / L * minElements));
                for (int j = 1; j <= pieces; j++) refined.Add(j == pieces ? x1 : x0 + (x1 - x0) * j / pieces);
            }

            int nn = refined.Count, nd = 2 * nn;
            var k = new double[nd, nd];
            var f = new double[nd];

            for (int e = 0; e + 1 < nn; e++)
            {
                double l = refined[e + 1] - refined[e], c = ei / (l * l * l);
                double[,] ke =
                {
                    { 12 * c, 6 * l * c, -12 * c, 6 * l * c },
                    { 6 * l * c, 4 * l * l * c, -6 * l * c, 2 * l * l * c },
                    { -12 * c, -6 * l * c, 12 * c, -6 * l * c },
                    { 6 * l * c, 2 * l * l * c, -6 * l * c, 4 * l * l * c },
                };
                int d0 = 2 * e;
                for (int r = 0; r < 4; r++)
                    for (int s = 0; s < 4; s++)
                        k[d0 + r, d0 + s] += ke[r, s];

                double mid = 0.5 * (refined[e] + refined[e + 1]);
                foreach (var udl in model.UniformLoads)
                {
                    if (mid <= udl.Start || mid >= udl.End) continue;
                    double q = -udl.Intensity; // FE takes upward as positive
                    f[d0] += q * l / 2;
                    f[d0 + 1] += q * l * l / 12;
                    f[d0 + 2] += q * l / 2;
                    f[d0 + 3] -= q * l * l / 12;
                }
            }

            foreach (var p in model.PointLoads) f[2 * NodeAt(refined, p.Position)] -= p.Force;

            var fixedDof = new bool[nd];
            foreach (var s in supports)
            {
                int node = NodeAt(refined, s.Position);
                fixedDof[2 * node] = true;
                if (s.Kind == SupportKind.Fixed) fixedDof[2 * node + 1] = true;
            }

            var free = new List<int>();
            for (int d = 0; d < nd; d++)
                if (!fixedDof[d]) free.Add(d);

            int nf = free.Count;
            var kff = new double[nf, nf];
            var ff = new double[nf];
            for (int r = 0; r < nf; r++)
            {
                ff[r] = f[free[r]];
                for (int s = 0; s < nf; s++) kff[r, s] = k[free[r], free[s]];
            }

            var uf = Gauss(kff, ff);
            var u = new double[nd];
            for (int r = 0; r < nf; r++) u[free[r]] = uf[r];

            var result = new Result { NodeX = refined.ToArray(), W = new double[nn], Theta = new double[nn] };
            for (int i = 0; i < nn; i++)
            {
                result.W[i] = u[2 * i];
                result.Theta[i] = u[2 * i + 1];
            }

            // Reaction = K·u − f at restrained DOFs (force upward, moment counter-clockwise on the beam).
            foreach (var s in supports)
            {
                int node = NodeAt(refined, s.Position);
                double force = Row(k, u, 2 * node) - f[2 * node];
                double moment = s.Kind == SupportKind.Fixed ? Row(k, u, 2 * node + 1) - f[2 * node + 1] : 0;
                result.Reactions.Add(new Reaction(s.Position, force, moment));
            }

            return result;
        }

        static double Row(double[,] k, double[] u, int r)
        {
            double s = 0;
            for (int j = 0; j < u.Length; j++) s += k[r, j] * u[j];
            return s;
        }

        static int NodeAt(List<double> nodes, double x)
        {
            int best = 0;
            for (int i = 1; i < nodes.Count; i++)
                if (Math.Abs(nodes[i] - x) < Math.Abs(nodes[best] - x)) best = i;
            return best;
        }

        static double[] Gauss(double[,] a, double[] b)
        {
            int n = b.Length;
            double scale = 0;
            for (int i = 0; i < n; i++) scale = Math.Max(scale, Math.Abs(a[i, i]));
            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                for (int r = col + 1; r < n; r++)
                    if (Math.Abs(a[r, col]) > Math.Abs(a[pivot, col])) pivot = r;
                if (Math.Abs(a[pivot, col]) < 1e-12 * scale)
                    throw new InvalidOperationException("Singular stiffness matrix: the supports form a mechanism.");
                if (pivot != col)
                {
                    for (int j = 0; j < n; j++) (a[col, j], a[pivot, j]) = (a[pivot, j], a[col, j]);
                    (b[col], b[pivot]) = (b[pivot], b[col]);
                }

                for (int r = col + 1; r < n; r++)
                {
                    double m = a[r, col] / a[col, col];
                    if (m == 0) continue;
                    for (int j = col; j < n; j++) a[r, j] -= m * a[col, j];
                    b[r] -= m * b[col];
                }
            }

            var x = new double[n];
            for (int r = n - 1; r >= 0; r--)
            {
                double s = b[r];
                for (int j = r + 1; j < n; j++) s -= a[r, j] * x[j];
                x[r] = s / a[r, r];
            }

            return x;
        }
    }
}
