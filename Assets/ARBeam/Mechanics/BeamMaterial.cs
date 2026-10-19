using System;

namespace ARBeam.Mechanics
{
    /// <summary>Linear-elastic material. E in Pa, density in kg/m³.</summary>
    public readonly struct BeamMaterial
    {
        public string Name { get; }

        /// <summary>Young's modulus E, Pa.</summary>
        public double ElasticModulus { get; }

        /// <summary>Density ρ, kg/m³ (used only for self-weight).</summary>
        public double Density { get; }

        public BeamMaterial(string name, double elasticModulus, double density)
        {
            if (!(elasticModulus > 0)) throw new ArgumentOutOfRangeException(nameof(elasticModulus), "E must be positive.");
            if (!(density >= 0)) throw new ArgumentOutOfRangeException(nameof(density), "Density must be non-negative.");
            Name = name ?? string.Empty;
            ElasticModulus = elasticModulus;
            Density = density;
        }

        // Nominal handbook values (ASM / MatWeb). The bench rig should be checked against measured deflection.
        public static readonly BeamMaterial Aluminum6061T6 = new BeamMaterial("6061-T6 aluminum", 68.9e9, 2700.0);
        public static readonly BeamMaterial Brass260 = new BeamMaterial("C26000 brass (half-hard)", 110e9, 8530.0);
        public static readonly BeamMaterial Steel1018 = new BeamMaterial("1018 steel", 205e9, 7870.0);

        public override string ToString() => $"{Name} (E={ElasticModulus / 1e9:G4} GPa)";
    }
}
