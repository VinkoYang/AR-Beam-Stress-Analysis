using System;

namespace ARBeam.Mechanics
{
    /// <summary>
    /// Cross-section properties for Euler–Bernoulli bending about the horizontal axis. SI units.
    /// </summary>
    public readonly struct BeamSection
    {
        /// <summary>Cross-sectional area A, m².</summary>
        public double Area { get; }

        /// <summary>Second moment of area I about the neutral axis, m⁴.</summary>
        public double I { get; }

        /// <summary>Distance c from the neutral axis to the extreme fibre, m.</summary>
        public double C { get; }

        public BeamSection(double area, double i, double c)
        {
            if (!(area > 0)) throw new ArgumentOutOfRangeException(nameof(area), "Area must be positive.");
            if (!(i > 0)) throw new ArgumentOutOfRangeException(nameof(i), "I must be positive.");
            if (!(c > 0)) throw new ArgumentOutOfRangeException(nameof(c), "c must be positive.");
            Area = area;
            I = i;
            C = c;
        }

        /// <summary>Elastic section modulus S = I / c, m³.</summary>
        public double SectionModulus => I / C;

        /// <summary>
        /// Solid rectangle of width b and depth h, bending about the axis parallel to b:
        /// I = b·h³/12, c = h/2.
        /// </summary>
        public static BeamSection Rectangular(double width, double height)
        {
            if (!(width > 0)) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
            if (!(height > 0)) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
            return new BeamSection(width * height, width * height * height * height / 12.0, height / 2.0);
        }

        public override string ToString() => $"A={Area:G4} m², I={I:G4} m⁴, c={C:G4} m";
    }
}
