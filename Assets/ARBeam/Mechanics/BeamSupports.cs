namespace ARBeam.Mechanics
{
    public enum SupportType
    {
        /// <summary>Clamped at x = 0, free at x = L.</summary>
        Cantilever,

        /// <summary>Pin at x = 0, roller at x = L.</summary>
        SimplySupported,

        /// <summary>Pin and roller at arbitrary positions a &lt; b; the beam may overhang on either side.</summary>
        Overhang,

        /// <summary>Clamped at both ends (statically indeterminate; virtual mode only).</summary>
        FixedFixed,
    }

    /// <summary>Support layout. Positions are measured from the left end, m.</summary>
    public readonly struct BeamSupports
    {
        public SupportType Type { get; }

        /// <summary>Left support position a (Overhang only).</summary>
        public double A { get; }

        /// <summary>Right support position b (Overhang only).</summary>
        public double B { get; }

        BeamSupports(SupportType type, double a, double b)
        {
            Type = type;
            A = a;
            B = b;
        }

        public static BeamSupports Cantilever() => new BeamSupports(SupportType.Cantilever, 0, 0);
        public static BeamSupports SimplySupported() => new BeamSupports(SupportType.SimplySupported, 0, 0);
        public static BeamSupports Overhang(double a, double b) => new BeamSupports(SupportType.Overhang, a, b);
        public static BeamSupports FixedFixed() => new BeamSupports(SupportType.FixedFixed, 0, 0);

        public override string ToString()
            => Type == SupportType.Overhang ? $"Overhang (a={A:G4} m, b={B:G4} m)" : Type.ToString();
    }
}
