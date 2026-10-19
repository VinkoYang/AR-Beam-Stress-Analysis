using System;
using NUnit.Framework;

namespace ARBeam.Mechanics.Tests
{
    static class TestSupport
    {
        // Generic textbook-style beam used by the table tests: L = 2 m, steel, 50 × 100 mm rectangle.
        public const double L = 2.0;
        public const double E = 200e9;
        public static readonly BeamSection Section = BeamSection.Rectangular(0.05, 0.10);
        public static double EI => E * Section.I;
        public const double P = 1000.0; // N
        public const double W = 500.0;  // N/m

        /// <summary>Tolerance for exact closed-form expressions.</summary>
        public const double Exact = 1e-9;

        /// <summary>Acceptance criterion from the project plan: within 1 % of the reference.</summary>
        public const double OnePercent = 0.01;

        public static BeamModel Model(BeamSupports supports, double length = L)
            => new BeamModel(length, Section, E, supports);

        public static void AssertRelative(double expected, double actual, double tolerance, string what)
        {
            double scale = Math.Max(Math.Abs(expected), 1e-300);
            double error = Math.Abs(actual - expected) / scale;
            Assert.That(error, Is.LessThanOrEqualTo(tolerance),
                $"{what}: expected {expected:G10}, got {actual:G10} (relative error {error:E2}).");
        }

        /// <summary>For values that should be zero: compare against a characteristic magnitude.</summary>
        public static void AssertNearZero(double actual, double magnitude, string what, double tolerance = 1e-9)
        {
            Assert.That(Math.Abs(actual), Is.LessThanOrEqualTo(tolerance * Math.Abs(magnitude)),
                $"{what}: expected ~0, got {actual:G10} (scale {magnitude:G4}).");
        }
    }
}
