using System.Collections.Generic;

namespace ARBeam.Mechanics
{
    /// <summary>
    /// Sampled solution, convenient for drawing and for exporting to other tools. SI units, same sign convention
    /// as <see cref="BeamSolver"/>. Samples follow the diagram grid, so X is non-decreasing and a jump in V or M
    /// shows up as two samples with the same X.
    /// </summary>
    public sealed class BeamResult
    {
        /// <summary>Position from the left end, m.</summary>
        public double[] X { get; }

        /// <summary>Shear force, N.</summary>
        public double[] Shear { get; }

        /// <summary>Bending moment, N·m (sagging positive).</summary>
        public double[] Moment { get; }

        /// <summary>Slope, rad.</summary>
        public double[] Slope { get; }

        /// <summary>Deflection, m (upward positive).</summary>
        public double[] Deflection { get; }

        /// <summary>Extreme-fibre bending stress |M|·c/I, Pa.</summary>
        public double[] MaxStress { get; }

        public IReadOnlyList<Reaction> Reactions { get; }
        public Extremum PeakShear { get; }
        public Extremum PeakMoment { get; }
        public Extremum PeakDeflection { get; }

        /// <summary>Largest extreme-fibre bending stress anywhere, Pa.</summary>
        public double MaxBendingStress { get; }

        internal BeamResult(int count, BeamSolution solution)
        {
            X = new double[count];
            Shear = new double[count];
            Moment = new double[count];
            Slope = new double[count];
            Deflection = new double[count];
            MaxStress = new double[count];
            Reactions = solution.Reactions;
            PeakShear = solution.PeakShear;
            PeakMoment = solution.PeakMoment;
            PeakDeflection = solution.PeakDeflection;
            MaxBendingStress = solution.MaxBendingStress;
        }

        public int Count => X.Length;
    }
}
