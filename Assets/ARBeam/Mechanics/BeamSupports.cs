using System;
using System.Collections.Generic;

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

        /// <summary>Clamped at both ends (statically indeterminate).</summary>
        FixedFixed,

        /// <summary>Each end chosen independently: Free, Pinned or Fixed (HoloMech's boundary panel).</summary>
        Ends,

        /// <summary>Any list of supports at any positions.</summary>
        Custom,
    }

    /// <summary>Condition at one end of the beam.</summary>
    public enum EndCondition
    {
        Free,
        Pinned,
        Fixed,
    }

    /// <summary>What a support restrains: a pin or roller stops deflection; a clamp also stops rotation.</summary>
    public enum SupportKind
    {
        Pin,
        Fixed,
    }

    /// <summary>One support at a position along the beam, m.</summary>
    public readonly struct Support
    {
        public double Position { get; }
        public SupportKind Kind { get; }

        public Support(double position, SupportKind kind)
        {
            Position = position;
            Kind = kind;
        }

        public override string ToString() => $"{Kind} @ x={Position:G4} m";
    }

    /// <summary>
    /// Support layout. The named layouts (cantilever, simply supported, …) are shorthands; every layout resolves to
    /// a list of <see cref="Support"/>s for a given length. Positions are measured from the left end, m.
    /// </summary>
    public readonly struct BeamSupports
    {
        readonly Support[] custom;

        public SupportType Type { get; }

        /// <summary>Left support position a (Overhang only).</summary>
        public double A { get; }

        /// <summary>Right support position b (Overhang only).</summary>
        public double B { get; }

        /// <summary>Left-end condition (Ends only).</summary>
        public EndCondition Left { get; }

        /// <summary>Right-end condition (Ends only).</summary>
        public EndCondition Right { get; }

        BeamSupports(SupportType type, double a, double b, EndCondition left, EndCondition right, Support[] custom)
        {
            Type = type;
            A = a;
            B = b;
            Left = left;
            Right = right;
            this.custom = custom;
        }

        public static BeamSupports Cantilever() => new BeamSupports(SupportType.Cantilever, 0, 0, default, default, null);
        public static BeamSupports SimplySupported() => new BeamSupports(SupportType.SimplySupported, 0, 0, default, default, null);
        public static BeamSupports Overhang(double a, double b) => new BeamSupports(SupportType.Overhang, a, b, default, default, null);
        public static BeamSupports FixedFixed() => new BeamSupports(SupportType.FixedFixed, 0, 0, default, default, null);

        public static BeamSupports Ends(EndCondition left, EndCondition right)
            => new BeamSupports(SupportType.Ends, 0, 0, left, right, null);

        public static BeamSupports Custom(params Support[] supports)
            => new BeamSupports(SupportType.Custom, 0, 0, default, default, (Support[])supports.Clone());

        /// <summary>The supports for a beam of length <paramref name="length"/>, sorted left to right.</summary>
        public List<Support> Resolve(double length)
        {
            var list = new List<Support>(2);
            switch (Type)
            {
                case SupportType.Cantilever:
                    list.Add(new Support(0, SupportKind.Fixed));
                    break;
                case SupportType.SimplySupported:
                    list.Add(new Support(0, SupportKind.Pin));
                    list.Add(new Support(length, SupportKind.Pin));
                    break;
                case SupportType.Overhang:
                    list.Add(new Support(A, SupportKind.Pin));
                    list.Add(new Support(B, SupportKind.Pin));
                    break;
                case SupportType.FixedFixed:
                    list.Add(new Support(0, SupportKind.Fixed));
                    list.Add(new Support(length, SupportKind.Fixed));
                    break;
                case SupportType.Ends:
                    if (Left != EndCondition.Free) list.Add(new Support(0, ToKind(Left)));
                    if (Right != EndCondition.Free) list.Add(new Support(length, ToKind(Right)));
                    break;
                case SupportType.Custom:
                    if (custom != null) list.AddRange(custom);
                    break;
                default:
                    throw new NotSupportedException(Type.ToString());
            }

            list.Sort((p, q) => p.Position.CompareTo(q.Position));
            return list;
        }

        static SupportKind ToKind(EndCondition c) => c == EndCondition.Fixed ? SupportKind.Fixed : SupportKind.Pin;

        public override string ToString()
        {
            switch (Type)
            {
                case SupportType.Overhang: return $"Overhang (a={A:G4} m, b={B:G4} m)";
                case SupportType.Ends: return $"{Left}–{Right}";
                case SupportType.Custom: return "Custom (" + string.Join(", ", custom ?? Array.Empty<Support>()) + ")";
                default: return Type.ToString();
            }
        }
    }
}
