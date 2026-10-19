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

        /// <summary>The supports the solver uses, sorted left to right.</summary>
        public List<Support> ResolvedSupports => Supports.Resolve(Length);

        /// <summary>Positions of the leftmost and rightmost support (both 0 for a single clamp).</summary>
        public (double a, double b) SupportPositions
        {
            get
            {
                var s = ResolvedSupports;
                return s.Count == 0 ? (0.0, Length) : (s[0].Position, s[s.Count - 1].Position);
            }
        }

        public void Validate()
        {
            if (Section.I <= 0) throw new InvalidOperationException("Section is not set.");
            if (Supports.Type == SupportType.Overhang && !(Supports.B > Supports.A))
                throw new InvalidOperationException($"Overhang supports must satisfy a < b (a={Supports.A}, b={Supports.B}).");

            var supports = ResolvedSupports;
            for (int i = 0; i < supports.Count; i++)
            {
                double p = supports[i].Position;
                if (p < 0 || p > Length || double.IsNaN(p))
                    throw new InvalidOperationException($"Support outside the beam: {supports[i]} (L={Length}).");
                if (i > 0 && p - supports[i - 1].Position < 1e-9 * Length)
                    throw new InvalidOperationException($"Two supports at the same position x={p}.");
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
