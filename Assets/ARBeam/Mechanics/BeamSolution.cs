using System;
using System.Collections.Generic;

namespace ARBeam.Mechanics
{
    public enum BeamQuantity
    {
        /// <summary>Shear force V, N.</summary>
        Shear,

        /// <summary>Bending moment M, N·m (sagging positive).</summary>
        Moment,

        /// <summary>Slope θ = dv/dx, rad.</summary>
        Slope,

        /// <summary>Deflection v, m (upward positive).</summary>
        Deflection,

        /// <summary>Maximum bending stress in the section |M|·c/I, Pa.</summary>
        BendingStress,
    }

    /// <summary>A sampled point of a diagram. Discontinuities appear as two points with the same X.</summary>
    public readonly struct DiagramPoint
    {
        public double X { get; }
        public double Value { get; }

        public DiagramPoint(double x, double value)
        {
            X = x;
            Value = value;
        }

        public override string ToString() => $"({X:G6}, {Value:G6})";
    }

    /// <summary>Signed value of a quantity at the point where its magnitude is largest.</summary>
    public readonly struct Extremum
    {
        public double Position { get; }
        public double Value { get; }

        public Extremum(double position, double value)
        {
            Position = position;
            Value = value;
        }

        public override string ToString() => $"{Value:G6} @ x={Position:G6} m";
    }

    /// <summary>
    /// Result of <see cref="BeamSolver.Solve"/>. Every query is a closed-form evaluation, so it is cheap to
    /// call per vertex. Positions are clamped to [0, L].
    ///
    /// Shear and moment jump at point loads and clamps. <see cref="Shear"/> and <see cref="Moment"/> return the
    /// value just to the right of x, except at x = L where they return the value just to the left (the right
    /// limit would be outside the beam). Use <see cref="ShearLeft"/>/<see cref="ShearRight"/> when the side matters.
    /// </summary>
    public sealed class BeamSolution
    {
        const double PositionTolerance = 1e-9;

        readonly List<MacaulayTerm> terms;
        readonly double c1, c2, ei;
        readonly double[] keyPoints;

        public BeamModel Model { get; }
        public IReadOnlyList<Reaction> Reactions { get; }

        /// <summary>
        /// Sorted, distinct positions where a diagram can have a kink or a jump: ends, supports, point
        /// loads, and distributed-load limits.
        /// </summary>
        public IReadOnlyList<double> KeyPoints => keyPoints;

        public double Length => Model.Length;

        internal BeamSolution(BeamModel model, List<MacaulayTerm> terms, List<Reaction> reactions, double c1, double c2)
        {
            Model = model;
            this.terms = terms;
            Reactions = reactions.AsReadOnly();
            this.c1 = c1;
            this.c2 = c2;
            ei = model.FlexuralRigidity;
            keyPoints = BuildKeyPoints(model, terms);

            PeakShear = FindPeakShear();
            PeakMoment = FindPeakMoment();
            PeakDeflection = FindPeakDeflection();
        }

        /// <summary>Largest |V| (signed value).</summary>
        public Extremum PeakShear { get; }

        /// <summary>Largest |M| (signed value).</summary>
        public Extremum PeakMoment { get; }

        /// <summary>Largest |v| (signed value; negative means downward).</summary>
        public Extremum PeakDeflection { get; }

        /// <summary>Maximum bending stress anywhere in the beam, |M|max·c/I, Pa.</summary>
        public double MaxBendingStress => Math.Abs(PeakMoment.Value) * Model.Section.C / Model.Section.I;

        public double Shear(double x) => x >= Length ? ShearLeft(x) : ShearRight(x);
        public double ShearLeft(double x) => MacaulayTerm.SumShear(terms, Clamp(x), false);
        public double ShearRight(double x) => MacaulayTerm.SumShear(terms, Clamp(x), true);

        public double Moment(double x) => x >= Length ? MomentLeft(x) : MomentRight(x);
        public double MomentLeft(double x) => MacaulayTerm.SumMoment(terms, Clamp(x), false);
        public double MomentRight(double x) => MacaulayTerm.SumMoment(terms, Clamp(x), true);

        public double Slope(double x) => (MacaulayTerm.SumSlope(terms, Clamp(x)) + c1) / ei;

        public double Deflection(double x)
        {
            x = Clamp(x);
            return (MacaulayTerm.SumDeflection(terms, x) + c1 * x + c2) / ei;
        }

        /// <summary>Maximum bending stress in the section at x, |M|·c/I, Pa.</summary>
        public double BendingStress(double x) => Math.Abs(Moment(x)) * Model.Section.C / Model.Section.I;

        /// <summary>
        /// Normal stress σ = −M·y/I at height y above the neutral axis, Pa (tension positive). A sagging moment
        /// compresses the top fibre.
        /// </summary>
        public double FiberStress(double x, double y) => -Moment(x) * y / Model.Section.I;

        public double Evaluate(BeamQuantity quantity, double x)
        {
            switch (quantity)
            {
                case BeamQuantity.Shear: return Shear(x);
                case BeamQuantity.Moment: return Moment(x);
                case BeamQuantity.Slope: return Slope(x);
                case BeamQuantity.Deflection: return Deflection(x);
                case BeamQuantity.BendingStress: return BendingStress(x);
                default: throw new ArgumentOutOfRangeException(nameof(quantity));
            }
        }

        /// <summary>
        /// Samples a diagram segment by segment between <see cref="KeyPoints"/>. Each segment starts with the
        /// right-hand limit and ends with the left-hand limit, so jumps show up as vertical steps.
        /// </summary>
        public List<DiagramPoint> Diagram(BeamQuantity quantity, int samplesPerSegment = 16)
        {
            if (samplesPerSegment < 1) throw new ArgumentOutOfRangeException(nameof(samplesPerSegment));
            var points = new List<DiagramPoint>((keyPoints.Length - 1) * (samplesPerSegment + 1));
            for (int s = 0; s + 1 < keyPoints.Length; s++)
            {
                double x0 = keyPoints[s], x1 = keyPoints[s + 1];
                for (int j = 0; j <= samplesPerSegment; j++)
                {
                    double x = j == samplesPerSegment ? x1 : x0 + (x1 - x0) * j / samplesPerSegment;
                    points.Add(new DiagramPoint(x, EvaluateSided(quantity, x, j < samplesPerSegment)));
                }
            }

            return points;
        }

        double EvaluateSided(BeamQuantity quantity, double x, bool rightLimit)
        {
            switch (quantity)
            {
                case BeamQuantity.Shear: return rightLimit ? ShearRight(x) : ShearLeft(x);
                case BeamQuantity.Moment: return rightLimit ? MomentRight(x) : MomentLeft(x);
                case BeamQuantity.BendingStress:
                    return Math.Abs(rightLimit ? MomentRight(x) : MomentLeft(x)) * Model.Section.C / Model.Section.I;
                default: return Evaluate(quantity, x);
            }
        }

        double Clamp(double x)
        {
            if (x < -PositionTolerance || x > Length + PositionTolerance || double.IsNaN(x))
                throw new ArgumentOutOfRangeException(nameof(x), $"x={x} is outside the beam [0, {Length}].");
            return x < 0 ? 0 : x > Length ? Length : x;
        }

        static double[] BuildKeyPoints(BeamModel model, List<MacaulayTerm> terms)
        {
            var xs = new List<double> { 0, model.Length };
            foreach (var t in terms) xs.Add(t.A);
            var (a, b) = model.SupportPositions;
            xs.Add(a);
            xs.Add(b);
            xs.Sort();

            var unique = new List<double>(xs.Count);
            foreach (var x in xs)
            {
                if (x < 0 || x > model.Length) continue;
                if (unique.Count == 0 || x - unique[unique.Count - 1] > PositionTolerance) unique.Add(x);
            }

            return unique.ToArray();
        }

        Extremum FindPeakShear()
        {
            // Shear is piecewise linear, so its extremes sit at segment ends.
            var best = new Extremum(0, 0);
            for (int s = 0; s + 1 < keyPoints.Length; s++)
            {
                best = Larger(best, keyPoints[s], ShearRight(keyPoints[s]));
                best = Larger(best, keyPoints[s + 1], ShearLeft(keyPoints[s + 1]));
            }

            return best;
        }

        Extremum FindPeakMoment()
        {
            // M is at most quadratic between key points; interior extremes are where the (linear) shear crosses zero.
            var best = new Extremum(0, 0);
            for (int s = 0; s + 1 < keyPoints.Length; s++)
            {
                double x0 = keyPoints[s], x1 = keyPoints[s + 1];
                best = Larger(best, x0, MomentRight(x0));
                best = Larger(best, x1, MomentLeft(x1));

                double v0 = ShearRight(x0), v1 = ShearLeft(x1);
                if ((v0 > 0 && v1 < 0) || (v0 < 0 && v1 > 0))
                {
                    double xr = x0 + (x1 - x0) * v0 / (v0 - v1);
                    best = Larger(best, xr, MomentRight(xr));
                }
            }

            return best;
        }

        Extremum FindPeakDeflection()
        {
            // v is at most quartic between key points. Its interior extremes are roots of θ (at most cubic):
            // bracket sign changes on a fine grid, then bisect.
            const int Subdivisions = 32;
            var best = new Extremum(0, 0);
            for (int s = 0; s + 1 < keyPoints.Length; s++)
            {
                double x0 = keyPoints[s], x1 = keyPoints[s + 1];
                best = Larger(best, x0, Deflection(x0));
                best = Larger(best, x1, Deflection(x1));

                double prevX = x0, prevT = Slope(x0);
                for (int j = 1; j <= Subdivisions; j++)
                {
                    double x = x0 + (x1 - x0) * j / Subdivisions;
                    double t = Slope(x);
                    if (t == 0)
                        best = Larger(best, x, Deflection(x));
                    else if ((prevT > 0 && t < 0) || (prevT < 0 && t > 0))
                    {
                        double root = Bisect(prevX, x, prevT);
                        best = Larger(best, root, Deflection(root));
                    }

                    prevX = x;
                    prevT = t;
                }
            }

            return best;
        }

        double Bisect(double lo, double hi, double slopeAtLo)
        {
            for (int i = 0; i < 80 && hi - lo > 1e-15 * Length; i++)
            {
                double mid = 0.5 * (lo + hi);
                double t = Slope(mid);
                if ((t > 0) == (slopeAtLo > 0))
                {
                    lo = mid;
                    slopeAtLo = t;
                }
                else
                {
                    hi = mid;
                }
            }

            return 0.5 * (lo + hi);
        }

        static Extremum Larger(Extremum current, double x, double value)
            => Math.Abs(value) > Math.Abs(current.Value) ? new Extremum(x, value) : current;
    }
}
