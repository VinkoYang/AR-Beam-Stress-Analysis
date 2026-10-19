using System;
using System.Collections.Generic;

namespace ARBeam.Mechanics
{
    /// <summary>
    /// Closed-form Euler–Bernoulli solver using Macaulay (singularity) functions.
    ///
    /// Sign convention (Hibbeler): loads positive downward; shear V is the sum of upward forces to the
    /// left of the section; bending moment M is positive when sagging; slope θ and deflection v are
    /// positive upward/counter-clockwise, so a beam loaded by gravity has v &lt; 0.
    ///
    /// The bending moment is written as M(x) = Σ kᵢ⟨x − aᵢ⟩^nᵢ including the support reactions. Integrating
    /// EI·v″ = M twice gives θ and v in closed form; the two integration constants come from the support
    /// conditions. The fixed–fixed beam has two extra unknowns (left reaction force and moment) that
    /// follow from v(L) = θ(L) = 0, a 2×2 linear system solved in closed form below.
    /// </summary>
    public static class BeamSolver
    {
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

            var terms = new List<MacaulayTerm>(loadTerms);
            var reactions = new List<Reaction>(2);
            double c1 = 0, c2 = 0; // EI·v = Σ … + c1·x + c2

            switch (model.Supports.Type)
            {
                case SupportType.Cantilever:
                {
                    // Clamp at x = 0 carries all the load. Its moment (counter-clockwise on the beam) is the
                    // total load moment; the internal moment just right of the clamp is minus that.
                    double r = totalForce;
                    double m = totalMoment;
                    reactions.Add(new Reaction(0, r, m));
                    terms.Add(new MacaulayTerm(0, r, 1));
                    terms.Add(new MacaulayTerm(0, -m, 0));
                    // v(0) = θ(0) = 0 and every bracket vanishes at x = 0, so c1 = c2 = 0.
                    break;
                }

                case SupportType.SimplySupported:
                case SupportType.Overhang:
                {
                    var (a, b) = model.SupportPositions;
                    double rb = (totalMoment - totalForce * a) / (b - a);
                    double ra = totalForce - rb;
                    reactions.Add(new Reaction(a, ra, 0));
                    reactions.Add(new Reaction(b, rb, 0));
                    terms.Add(new MacaulayTerm(a, ra, 1));
                    terms.Add(new MacaulayTerm(b, rb, 1));

                    // v(a) = v(b) = 0.
                    double fa = MacaulayTerm.SumDeflection(terms, a);
                    double fb = MacaulayTerm.SumDeflection(terms, b);
                    c1 = -(fb - fa) / (b - a);
                    c2 = -fa - c1 * a;
                    break;
                }

                case SupportType.FixedFixed:
                {
                    // Unknowns at x = 0: reaction R0 (up) and support moment M0 (counter-clockwise).
                    // With clamps at both ends c1 = c2 = 0, and v(L) = θ(L) = 0 give
                    //   R0·L²/2 − M0·L   = −Gθ
                    //   R0·L³/6 − M0·L²/2 = −Gv
                    // where Gθ, Gv are the load-only parts of EI·θ(L) and EI·v(L).
                    double gTheta = MacaulayTerm.SumSlope(loadTerms, L);
                    double gV = MacaulayTerm.SumDeflection(loadTerms, L);
                    double r0 = (12.0 * gV - 6.0 * gTheta * L) / (L * L * L);
                    double m0 = (6.0 * gV - 2.0 * gTheta * L) / (L * L);

                    double rL = totalForce - r0;
                    double mL = totalMoment - rL * L - m0; // Σ counter-clockwise moments about x = 0 vanish
                    reactions.Add(new Reaction(0, r0, m0));
                    reactions.Add(new Reaction(L, rL, mL));
                    terms.Add(new MacaulayTerm(0, r0, 1));
                    terms.Add(new MacaulayTerm(0, -m0, 0));
                    // The right-end reactions sit at x = L and never act on a section inside the beam.
                    break;
                }

                default:
                    throw new NotSupportedException(model.Supports.Type.ToString());
            }

            return new BeamSolution(model, terms, reactions, c1, c2);
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
