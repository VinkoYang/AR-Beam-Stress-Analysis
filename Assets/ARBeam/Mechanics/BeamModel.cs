using System;
using System.Collections.Generic;

namespace ARBeam.Mechanics
{
    /// <summary>
    /// Everything the solver needs: geometry, stiffness, supports and loads. SI units throughout.
    /// x runs from the left end (0) to the right end (L).
    /// </summary>
    public sealed class BeamModel
    {
        public double Length { get; }
        public BeamSection Section { get; }

        /// <summary>Young's modulus E, Pa.</summary>
        public double ElasticModulus { get; }

        public BeamSupports Supports { get; }

        public List<PointLoad> PointLoads { get; } = new List<PointLoad>();
        public List<UniformLoad> UniformLoads { get; } = new List<UniformLoad>();

        public BeamModel(double length, BeamSection section, double elasticModulus, BeamSupports supports)
        {
            if (!(length > 0)) throw new ArgumentOutOfRangeException(nameof(length), "Length must be positive.");
            if (!(elasticModulus > 0)) throw new ArgumentOutOfRangeException(nameof(elasticModulus), "E must be positive.");
            Length = length;
            Section = section;
            ElasticModulus = elasticModulus;
            Supports = supports;
        }

        /// <summary>Flexural rigidity EI, N·m².</summary>
        public double FlexuralRigidity => ElasticModulus * Section.I;

        /// <summary>Support positions actually used by the solver (a, b). Cantilever/FixedFixed return (0, L).</summary>
        public (double a, double b) SupportPositions
            => Supports.Type == SupportType.Overhang ? (Supports.A, Supports.B) : (0.0, Length);

        public void Validate()
        {
            if (Section.I <= 0) throw new InvalidOperationException("Section is not set.");
            if (Supports.Type == SupportType.Overhang)
            {
                var (a, b) = SupportPositions;
                if (a < 0 || b > Length || !(b > a))
                    throw new InvalidOperationException($"Overhang supports must satisfy 0 <= a < b <= L (a={a}, b={b}, L={Length}).");
            }

            foreach (var p in PointLoads)
            {
                if (p.Position < 0 || p.Position > Length || double.IsNaN(p.Force))
                    throw new InvalidOperationException($"Point load outside the beam: {p}.");
            }

            foreach (var w in UniformLoads)
            {
                if (w.Start < 0 || w.End > Length || double.IsNaN(w.Intensity))
                    throw new InvalidOperationException($"Uniform load outside the beam: {w}.");
            }
        }
    }
}
