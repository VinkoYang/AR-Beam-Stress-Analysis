using System;

namespace ARBeam.Mechanics
{
    public static class Gravity
    {
        /// <summary>Standard gravity, m/s².</summary>
        public const double Standard = 9.80665;
    }

    /// <summary>
    /// Concentrated transverse force. <see cref="Force"/> is in N and positive downward (the direction
    /// gravity pulls a hanging mass); a negative value pushes the beam up.
    /// </summary>
    public readonly struct PointLoad
    {
        /// <summary>Distance from the left end x = 0, m.</summary>
        public double Position { get; }

        /// <summary>Force, N, positive downward.</summary>
        public double Force { get; }

        public PointLoad(double position, double force)
        {
            Position = position;
            Force = force;
        }

        /// <summary>Weight of a hanging mass (kg) at <paramref name="position"/>.</summary>
        public static PointLoad FromMass(double position, double mass, double g = Gravity.Standard)
            => new PointLoad(position, mass * g);

        public override string ToString() => $"P={Force:G4} N @ x={Position:G4} m";
    }

    /// <summary>
    /// Uniformly distributed transverse load over [Start, End]. <see cref="Intensity"/> is in N/m and
    /// positive downward.
    /// </summary>
    public readonly struct UniformLoad
    {
        public double Start { get; }
        public double End { get; }

        /// <summary>Load per unit length w, N/m, positive downward.</summary>
        public double Intensity { get; }

        public UniformLoad(double start, double end, double intensity)
        {
            if (!(end > start)) throw new ArgumentException("UniformLoad needs End > Start.");
            Start = start;
            End = end;
            Intensity = intensity;
        }

        public double Resultant => Intensity * (End - Start);
        public double Centroid => 0.5 * (Start + End);

        /// <summary>Self-weight ρ·A·g over the full length.</summary>
        public static UniformLoad SelfWeight(BeamMaterial material, BeamSection section, double length,
            double g = Gravity.Standard)
            => new UniformLoad(0.0, length, material.Density * section.Area * g);

        public override string ToString() => $"w={Intensity:G4} N/m on [{Start:G4}, {End:G4}] m";
    }
}
