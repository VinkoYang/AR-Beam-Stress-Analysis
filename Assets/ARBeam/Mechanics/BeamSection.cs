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

        /// <summary>
        /// Circular tube, outer diameter D and wall thickness t (t ≤ D/2; t = D/2 is a solid bar):
        /// A = π(D² − d²)/4, I = π(D⁴ − d⁴)/64, c = D/2, with d = D − 2t.
        /// </summary>
        public static BeamSection Pipe(double outerDiameter, double wall)
        {
            if (!(outerDiameter > 0)) throw new ArgumentOutOfRangeException(nameof(outerDiameter), "D must be positive.");
            if (!(wall > 0) || wall > outerDiameter / 2)
                throw new ArgumentOutOfRangeException(nameof(wall), "Wall thickness must satisfy 0 < t ≤ D/2.");
            double d = outerDiameter - 2 * wall;
            double D2 = outerDiameter * outerDiameter, d2 = d * d;
            return new BeamSection(Math.PI * (D2 - d2) / 4, Math.PI * (D2 * D2 - d2 * d2) / 64, outerDiameter / 2);
        }

        /// <summary>
        /// Rectangular tube, width b, height h, wall t (t = 0, or walls thick enough to meet, means solid):
        /// I = (b·h³ − bᵢ·hᵢ³)/12 with bᵢ = b − 2t, hᵢ = h − 2t; c = h/2.
        /// </summary>
        public static BeamSection Box(double width, double height, double wall)
        {
            if (!(width > 0)) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
            if (!(height > 0)) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
            if (!(wall >= 0)) throw new ArgumentOutOfRangeException(nameof(wall), "Wall thickness must be non-negative.");
            if (wall == 0 || 2 * wall >= Math.Min(width, height)) return Rectangular(width, height);
            double bi = width - 2 * wall, hi = height - 2 * wall;
            return new BeamSection(width * height - bi * hi,
                (width * height * height * height - bi * hi * hi * hi) / 12, height / 2);
        }

        /// <summary>
        /// Doubly symmetric I-section: overall height h, flange width bf, flange thickness tf, web thickness tw.
        /// A = 2·bf·tf + hw·tw, I = (bf·h³ − (bf − tw)·hw³)/12 with hw = h − 2tf; c = h/2.
        /// </summary>
        public static BeamSection IBeam(double height, double flangeWidth, double flangeThickness, double webThickness)
        {
            if (!(height > 0)) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
            if (!(flangeWidth > 0)) throw new ArgumentOutOfRangeException(nameof(flangeWidth), "Flange width must be positive.");
            if (!(flangeThickness > 0) || 2 * flangeThickness >= height)
                throw new ArgumentOutOfRangeException(nameof(flangeThickness), "Flange thickness must satisfy 0 < tf < h/2.");
            if (!(webThickness > 0) || webThickness > flangeWidth)
                throw new ArgumentOutOfRangeException(nameof(webThickness), "Web thickness must satisfy 0 < tw ≤ bf.");
            double hw = height - 2 * flangeThickness;
            double area = 2 * flangeWidth * flangeThickness + hw * webThickness;
            double i = (flangeWidth * height * height * height - (flangeWidth - webThickness) * hw * hw * hw) / 12;
            return new BeamSection(area, i, height / 2);
        }

        public override string ToString() => $"A={Area:G4} m², I={I:G4} m⁴, c={C:G4} m";
    }
}
