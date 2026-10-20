using System;
using ARBeam.Mechanics;
using NUnit.Framework;

namespace HoloMech.Tests
{
    /// <summary>The outline that gets extruded into the beam mesh must describe the same section the solver uses.</summary>
    public class SectionProfileTests
    {
        static void Near(double expected, double actual, double rel, string what)
            => Assert.That(actual, Is.EqualTo(expected).Within(Math.Abs(expected) * rel), what);

        double CapArea(SectionProfile p)
        {
            double a = 0;
            for (int i = 0; i < p.CapU.Count; i += 3)
                a += 0.5 * ((p.CapU[i + 1] - p.CapU[i]) * (p.CapV[i + 2] - p.CapV[i]) - (p.CapU[i + 2] - p.CapU[i]) * (p.CapV[i + 1] - p.CapV[i]));
            return a;
        }

        [Test]
        public void BoxMatchesSection([Values(0.0, 0.01, 0.03)] double wall)
        {
            var p = SectionProfile.Box(0.10, 0.20, wall);
            var s = BeamSection.Box(0.10, 0.20, wall);
            Near(s.Area, p.Area(), 1e-12, "area");
            Near(s.I, p.SecondMoment(), 1e-12, "I");
            Near(s.Area, CapArea(p), 1e-12, "cap triangles cover the section");
        }

        [Test]
        public void IBeamMatchesSection()
        {
            var p = SectionProfile.IBeam(0.30, 0.15, 0.015, 0.01);
            var s = BeamSection.IBeam(0.30, 0.15, 0.015, 0.01);
            Near(s.Area, p.Area(), 1e-12, "area");
            Near(s.I, p.SecondMoment(), 1e-12, "I");
            Near(s.Area, CapArea(p), 1e-12, "cap triangles cover the section");
        }

        [Test]
        public void PipeMatchesSectionToPolygonAccuracy()
        {
            var p = SectionProfile.Pipe(0.10, 0.01, 96);
            var s = BeamSection.Pipe(0.10, 0.01);
            Near(s.Area, p.Area(), 2e-3, "area");
            Near(s.I, p.SecondMoment(), 4e-3, "I");
            Near(p.Area(), CapArea(p), 1e-12, "cap triangles cover the outline");
        }

        [Test]
        public void SolidPipeHasNoHole()
        {
            var p = SectionProfile.Pipe(0.10, 0.05, 64);
            Assert.That(p.Loops.Count, Is.EqualTo(1));
            Near(Math.PI * 0.05 * 0.05, p.Area(), 3e-3, "area");
        }

        [Test]
        public void DefaultInputsProduceAProfile()
        {
            var inputs = new HoloMechInputs();
            foreach (var type in new[] { SectionType.Pipe, SectionType.Box, SectionType.IBeam })
            {
                inputs.section = type;
                var p = SectionProfile.For(inputs);
                Assert.That(p.Area(), Is.GreaterThan(0), type.ToString());
                Near(inputs.SectionProperties().Area, p.Area(), 3e-3, type + " area");
            }
        }
    }
}
