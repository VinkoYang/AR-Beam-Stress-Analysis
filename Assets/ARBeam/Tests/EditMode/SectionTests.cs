using System;
using NUnit.Framework;
using static ARBeam.Mechanics.Tests.TestSupport;

namespace ARBeam.Mechanics.Tests
{
    /// <summary>Section properties against hand calculation (values in the comments, worked in cm).</summary>
    public class SectionTests
    {
        const double Cm = 0.01;

        [Test]
        public void SolidRectangle()
        {
            // b = 10, h = 20 cm: A = 200 cm², I = 10·20³/12 = 6666.67 cm⁴, c = 10 cm.
            var s = BeamSection.Box(10 * Cm, 20 * Cm, 0);
            AssertRelative(200e-4, s.Area, Exact, "A");
            AssertRelative(10 * 20.0 * 20 * 20 / 12 * 1e-8, s.I, Exact, "I");
            AssertRelative(0.10, s.C, Exact, "c");
            AssertRelative(BeamSection.Rectangular(0.1, 0.2).I, s.I, Exact, "same as Rectangular");
        }

        [Test]
        public void HollowBox()
        {
            // b = 10, h = 20, t = 1 cm: inner 8 × 18. A = 200 − 144 = 56 cm²; I = (10·8000 − 8·5832)/12 = 2778.67 cm⁴.
            var s = BeamSection.Box(10 * Cm, 20 * Cm, 1 * Cm);
            AssertRelative(56e-4, s.Area, Exact, "A");
            AssertRelative((10 * 8000.0 - 8 * 5832.0) / 12 * 1e-8, s.I, Exact, "I");
            AssertRelative(0.10, s.C, Exact, "c");
        }

        [Test]
        public void Pipe()
        {
            // D = 10, t = 1 cm, d = 8: A = π(100 − 64)/4 = 28.274 cm²; I = π(10⁴ − 8⁴)/64 = 289.81 cm⁴.
            var s = BeamSection.Pipe(10 * Cm, 1 * Cm);
            AssertRelative(Math.PI * 36 / 4 * 1e-4, s.Area, Exact, "A");
            AssertRelative(Math.PI * (10000 - 4096) / 64 * 1e-8, s.I, Exact, "I");
            AssertRelative(0.05, s.C, Exact, "c");
        }

        [Test]
        public void SolidPipeIsARoundBar()
        {
            var s = BeamSection.Pipe(10 * Cm, 5 * Cm);
            AssertRelative(Math.PI * Math.Pow(0.1, 4) / 64, s.I, Exact, "πD⁴/64");
        }

        [Test]
        public void IBeam()
        {
            // h = 30, bf = 15, tf = 1.5, tw = 1 cm, hw = 27: A = 2·15·1.5 + 27·1 = 72 cm²;
            // I = (15·27000 − 14·19683)/12 = 10786.5 cm⁴.
            var s = BeamSection.IBeam(30 * Cm, 15 * Cm, 1.5 * Cm, 1 * Cm);
            AssertRelative(72e-4, s.Area, Exact, "A");
            AssertRelative((15 * 27000.0 - 14 * 19683.0) / 12 * 1e-8, s.I, Exact, "I");
            AssertRelative(0.15, s.C, Exact, "c");
        }

        [Test]
        public void IBeamWithWebAsWideAsFlangeIsARectangle()
        {
            AssertRelative(BeamSection.Rectangular(0.15, 0.30).I, BeamSection.IBeam(0.30, 0.15, 0.015, 0.15).I, Exact, "I");
        }

        [Test]
        public void InvalidDimensionsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BeamSection.Pipe(0.1, 0.06));
            Assert.Throws<ArgumentOutOfRangeException>(() => BeamSection.IBeam(0.1, 0.1, 0.05, 0.01));
            Assert.Throws<ArgumentOutOfRangeException>(() => BeamSection.IBeam(0.1, 0.05, 0.01, 0.06));
            Assert.Throws<ArgumentOutOfRangeException>(() => BeamSection.Box(0.1, 0.1, -0.01));
        }
    }
}
