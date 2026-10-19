using System;
using System.Collections.Generic;

namespace ARBeam.Mechanics
{
    /// <summary>
    /// Closed-form Euler–Bernoulli solver using Macaulay (singularity) functions, for any set of pin and clamp
    /// supports, statically determinate or not.
    ///
    /// Sign convention (Hibbeler): loads positive downward; shear V is the sum of upward forces to the
    /// left of the section; bending moment M is positive when sagging; slope θ and deflection v are
    /// positive upward/counter-clockwise, so a beam loaded by gravity has v &lt; 0.
    ///
    /// Method. The bending moment is M(x) = Σ kᵢ⟨x − aᵢ⟩^nᵢ over the loads and the unknown support reactions
    /// (a force Rᵢ at every support, a moment Mⱼ at every clamp). Integrating EI·v″ = M twice gives EI·θ and
    /// EI·v in closed form plus two constants C1, C2. The unknowns {Rᵢ, Mⱼ, C1, C2} satisfy a square linear
    /// system: ΣF = 0, ΣM = 0, v = 0 at every support and θ = 0 at every clamp. A singular system means the
    /// supports allow rigid-body motion (a mechanism, e.g. Free–Pinned), which is reported instead of a result.
    /// Once the system is solved, V, M, θ and v are exact everywhere; nothing is discretised.
    /// </summary>
    public static class BeamSolver
    {
        /// <summary>Solves the beam. Throws <see cref="UnstableBeamException"/> for a mechanism.</summary>
        public static BeamSolution Solve(BeamModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            model.Validate();

            double L = model.Length;
            var loadTerms = new List<MacaulayTerm>();
            double totalForce = 0;   // Σ downward load, N
            double totalMoment = 0;  // Σ downward load × lever arm about x = 0, N·m

            foreach (var p in model.PointLoads)
            {
                loadTerms.Add(new MacaulayTerm(p.Position, -p.Force, 1));
                totalForce += p.Force;
                totalMoment += p.Force * p.Position;
            }

            foreach (var w in model.UniformLoads)
            {
                loadTerms.Add(new MacaulayTerm(w.Start, -0.5 * w.Intensity, 2));
                loadTerms.Add(new MacaulayTerm(w.End, 0.5 * w.Intensity, 2));
                totalForce += w.Resultant;
                totalMoment += w.Resultant * w.Centroid;
            }

            var supports = model.ResolvedSupports;
            var clamps = new List<int>();
            for (int i = 0; i < supports.Count; i++)
                if (supports[i].Kind == SupportKind.Fixed) clamps.Add(i);

            // Unknowns: R0..R(nR−1), M0..M(nM−1), C1, C2. Rows and columns are scaled by powers of L so every
            // matrix entry is O(1) and the singularity test does not depend on units.
            int nR = supports.Count, nM = clamps.Count, n = nR + nM + 2;
            int iC1 = nR + nM, iC2 = iC1 + 1;
            var a = new double[n, n];
            var rhs = new double[n];
            var colScale = new double[n];
            for (int j = 0; j < nR; j++) colScale[j] = 1;
            for (int j = 0; j < nM; j++) colScale[nR + j] = L;
            colScale[iC1] = L * L;
            colScale[iC2] = L * L * L;

            int row = 0;

            // ΣF = 0: Σ Rᵢ = total load.
            for (int j = 0; j < nR; j++) a[row, j] = 1;
            rhs[row++] = totalForce;

            // ΣM about x = 0 (counter-clockwise positive): Σ Rᵢ·aᵢ + Σ Mⱼ = load moment. Row scaled by 1/L.
            for (int j = 0; j < nR; j++) a[row, j] = supports[j].Position / L;
            for (int j = 0; j < nM; j++) a[row, nR + j] = 1 / L;
            rhs[row++] = totalMoment / L;

            // v = 0 at every support. Row scaled by 1/L³.
            foreach (var s in supports)
            {
                double x = s.Position, inv = 1 / (L * L * L);
                for (int j = 0; j < nR; j++) a[row, j] = Cube(x - supports[j].Position) / 6 * inv;
                for (int j = 0; j < nM; j++) a[row, nR + j] = -Square(x - supports[clamps[j]].Position) / 2 * inv;
                a[row, iC1] = x * inv;
                a[row, iC2] = inv;
                rhs[row++] = -MacaulayTerm.SumDeflection(loadTerms, x) * inv;
            }

            // θ = 0 at every clamp. Row scaled by 1/L².
            foreach (int c in clamps)
            {
                double x = supports[c].Position, inv = 1 / (L * L);
                for (int j = 0; j < nR; j++) a[row, j] = Square(x - supports[j].Position) / 2 * inv;
                for (int j = 0; j < nM; j++) a[row, nR + j] = -Positive(x - supports[clamps[j]].Position) * inv;
                a[row, iC1] = inv;
                rhs[row++] = -MacaulayTerm.SumSlope(loadTerms, x) * inv;
            }

            for (int r = 0; r < n; r++)
                for (int j = 0; j < n; j++)
                    a[r, j] *= colScale[j];

            if (!LinearSolve(a, rhs, out var scaled))
                throw new UnstableBeamException(model.Supports);

            var terms = new List<MacaulayTerm>(loadTerms);
            var reactions = new List<Reaction>(nR);
            for (int i = 0; i < nR; i++)
            {
                double force = scaled[i] * colScale[i];
                int k = clamps.IndexOf(i);
                double moment = k >= 0 ? scaled[nR + k] * colScale[nR + k] : 0;
                reactions.Add(new Reaction(supports[i].Position, force, moment));
                terms.Add(new MacaulayTerm(supports[i].Position, force, 1));
                // A counter-clockwise clamp moment is hogging just to its right. A clamp at x = L never acts on a
                // section inside the beam.
                if (k >= 0 && supports[i].Position < L) terms.Add(new MacaulayTerm(supports[i].Position, -moment, 0));
            }

            double c1 = scaled[iC1] * colScale[iC1], c2 = scaled[iC2] * colScale[iC2];
            return new BeamSolution(model, terms, reactions, c1, c2);
        }

        /// <summary>Like <see cref="Solve"/>, but reports a mechanism or invalid input as a message.</summary>
        public static bool TrySolve(BeamModel model, out BeamSolution solution, out string error)
        {
            try
            {
                solution = Solve(model);
                error = null;
                return true;
            }
            catch (InvalidOperationException e)
            {
                solution = null;
                error = e.Message;
                return false;
            }
        }

        /// <summary>True if the supports restrain rigid-body motion (independent of the loads).</summary>
        public static bool IsStable(BeamSupports supports, double length = 1.0)
        {
            var probe = new BeamModel(length, BeamSection.Rectangular(1, 1), 1, supports);
            return TrySolve(probe, out _, out _);
        }

        /// <summary>Gaussian elimination with partial pivoting. False if the matrix is numerically singular.</summary>
        static bool LinearSolve(double[,] a, double[] b, out double[] x)
        {
            int n = b.Length;
            x = new double[n];
            const double SingularPivot = 1e-10;

            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                for (int r = col + 1; r < n; r++)
                    if (Math.Abs(a[r, col]) > Math.Abs(a[pivot, col])) pivot = r;
                if (Math.Abs(a[pivot, col]) < SingularPivot) return false;

                if (pivot != col)
                {
                    for (int j = 0; j < n; j++) (a[col, j], a[pivot, j]) = (a[pivot, j], a[col, j]);
                    (b[col], b[pivot]) = (b[pivot], b[col]);
                }

                for (int r = col + 1; r < n; r++)
                {
                    double f = a[r, col] / a[col, col];
                    if (f == 0) continue;
                    for (int j = col; j < n; j++) a[r, j] -= f * a[col, j];
                    b[r] -= f * b[col];
                }
            }

            for (int r = n - 1; r >= 0; r--)
            {
                double s = b[r];
                for (int j = r + 1; j < n; j++) s -= a[r, j] * x[j];
                x[r] = s / a[r, r];
            }

            return true;
        }

        static double Positive(double d) => d > 0 ? d : 0;
        static double Square(double d) => d > 0 ? d * d : 0;
        static double Cube(double d) => d > 0 ? d * d * d : 0;
    }

    /// <summary>The supports let the beam move as a rigid body, so there is no static solution.</summary>
    public sealed class UnstableBeamException : InvalidOperationException
    {
        public BeamSupports Supports { get; }

        public UnstableBeamException(BeamSupports supports)
            : base($"Unstable beam: the supports ({supports}) allow rigid-body motion. Restrain the beam against " +
                   "both vertical movement and rotation, e.g. one fixed end, or two pinned supports.")
        {
            Supports = supports;
        }
    }

    /// <summary>Support reaction acting on the beam. Force positive upward (N), moment positive counter-clockwise (N·m).</summary>
    public readonly struct Reaction
    {
        public double Position { get; }
        public double Force { get; }
        public double Moment { get; }

        public Reaction(double position, double force, double moment)
        {
            Position = position;
            Force = force;
            Moment = moment;
        }

        public override string ToString() => $"R={Force:G6} N, M={Moment:G6} N·m @ x={Position:G4} m";
    }

    /// <summary>One term k·⟨x − a⟩ⁿ of the bending moment M(x).</summary>
    readonly struct MacaulayTerm
    {
        public readonly double A;
        public readonly double K;
        public readonly int N;

        public MacaulayTerm(double a, double k, int n)
        {
            A = a;
            K = k;
            N = n;
        }

        /// <summary>
        /// ⟨x − a⟩ⁿ. When x == a, <paramref name="includeAtX"/> chooses the right-hand limit (true) or the
        /// left-hand limit (false); this only matters for n = 0, i.e. jumps.
        /// </summary>
        public static double Bracket(double x, double a, int n, bool includeAtX)
        {
            double d = x - a;
            if (d < 0 || (d == 0 && !includeAtX)) return 0;
            if (n == 0) return 1;
            double r = d;
            for (int i = 1; i < n; i++) r *= d;
            return r;
        }

        public static double SumMoment(List<MacaulayTerm> terms, double x, bool rightLimit)
        {
            double s = 0;
            foreach (var t in terms) s += t.K * Bracket(x, t.A, t.N, rightLimit);
            return s;
        }

        public static double SumShear(List<MacaulayTerm> terms, double x, bool rightLimit)
        {
            double s = 0;
            foreach (var t in terms)
            {
                if (t.N == 0) continue; // concentrated moments do not change shear
                s += t.K * t.N * Bracket(x, t.A, t.N - 1, rightLimit);
            }
            return s;
        }

        /// <summary>Σ k/(n+1)·⟨x − a⟩ⁿ⁺¹ (EI·θ without the integration constant).</summary>
        public static double SumSlope(List<MacaulayTerm> terms, double x)
        {
            double s = 0;
            foreach (var t in terms) s += t.K / (t.N + 1) * Bracket(x, t.A, t.N + 1, true);
            return s;
        }

        /// <summary>Σ k/((n+1)(n+2))·⟨x − a⟩ⁿ⁺² (EI·v without the integration constants).</summary>
        public static double SumDeflection(List<MacaulayTerm> terms, double x)
        {
            double s = 0;
            foreach (var t in terms) s += t.K / ((t.N + 1) * (t.N + 2)) * Bracket(x, t.A, t.N + 2, true);
            return s;
        }
    }
}
