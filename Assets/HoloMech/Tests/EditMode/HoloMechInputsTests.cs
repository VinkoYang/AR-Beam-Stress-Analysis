using System;
using ARBeam.Mechanics;
using NUnit.Framework;

namespace HoloMech.Tests
{
    /// <summary>UI units (cm, N, N/m, GPa, "upward is positive") → SI solver model.</summary>
    public class HoloMechInputsTests
    {
        static void Near(double expected, double actual, string what, double rel = 1e-9)
            => Assert.That(actual, Is.EqualTo(expected).Within(Math.Abs(expected) * rel + 1e-300), what);

        [Test]
        public void DefaultsMatchThePaperAndSolve()
        {
            var inputs = new HoloMechInputs();
            Assert.That(inputs.lengthCm, Is.EqualTo(200f));
            Assert.That(inputs.elasticModulusGPa, Is.EqualTo(20f));
            Assert.That(inputs.section, Is.EqualTo(SectionType.Pipe));
            Assert.That(inputs.TrySolve(out var s, out var error), Is.True, error);
            Assert.That(s.Model.PointLoads.Count, Is.EqualTo(1));
        }

        [Test]
        public void UnitsAreConvertedToSI()
        {
            var inputs = new HoloMechInputs
            {
                section = SectionType.Box,
                box = new BoxDimensions { widthCm = 10, heightCm = 20, wallCm = 0 },
                lengthCm = 250,
                elasticModulusGPa = 200,
                leftEnd = EndCondition.Pinned,
                rightEnd = EndCondition.Pinned,
                pointLoad = new PointLoadInput { enabled = true, positionCm = 100, forceN = 2000, direction = LoadDirection.Down },
                uniformLoad = new UniformLoadInput { enabled = true, startCm = 50, endCm = 150, intensityNPerM = 300, direction = LoadDirection.Down },
            };
            var m = inputs.ToModel();

            Near(2.5, m.Length, "L in m");
            Near(200e9, m.ElasticModulus, "E in Pa");
            Near(0.1 * 0.2 * 0.2 * 0.2 / 12, m.Section.I, "I in m⁴");
            Near(0.1, m.Section.C, "c in m");
            Near(1.0, m.PointLoads[0].Position, "point load at 1 m");
            Near(2000, m.PointLoads[0].Force, "2000 N");
            Near(0.5, m.UniformLoads[0].Start, "UDL start");
            Near(1.5, m.UniformLoads[0].End, "UDL end");
            Near(300, m.UniformLoads[0].Intensity, "300 N/m");
        }

        [Test]
        public void UpwardIsPositiveOnScreen()
        {
            // The solver takes downward loads as positive, so Up must flip sign and lift the beam.
            var inputs = new HoloMechInputs
            {
                leftEnd = EndCondition.Pinned,
                rightEnd = EndCondition.Pinned,
                pointLoad = new PointLoadInput { enabled = true, positionCm = 100, forceN = 2000, direction = LoadDirection.Up },
            };
            var m = inputs.ToModel();
            Near(-2000, m.PointLoads[0].Force, "up load");
            var s = BeamSolver.Solve(m);
            Assert.That(s.Deflection(1.0), Is.GreaterThan(0), "beam moves up");
        }

        [Test]
        public void MaterialButtonsSetModulus()
        {
            var inputs = new HoloMechInputs();
            inputs.ApplyMaterial(MaterialPreset.Steel);
            Assert.That(inputs.elasticModulusGPa, Is.EqualTo(200f));
            inputs.ApplyMaterial(MaterialPreset.Concrete);
            Assert.That(inputs.elasticModulusGPa, Is.EqualTo(30f));
            inputs.ApplyMaterial(MaterialPreset.Wood);
            Assert.That(inputs.elasticModulusGPa, Is.EqualTo(11f));
            inputs.elasticModulusGPa = 73f;
            inputs.ApplyMaterial(MaterialPreset.Custom);
            Assert.That(inputs.elasticModulusGPa, Is.EqualTo(73f), "Custom keeps the slider value");
        }

        [Test]
        public void SectionDimensionsInCentimetres()
        {
            var inputs = new HoloMechInputs { section = SectionType.Pipe, pipe = new PipeDimensions { outerDiameterCm = 10, wallCm = 1 } };
            Near(Math.PI * (Math.Pow(0.1, 4) - Math.Pow(0.08, 4)) / 64, inputs.SectionProperties().I, "pipe I");
            inputs.section = SectionType.IBeam;
            inputs.iBeam = new IBeamDimensions { heightCm = 30, flangeWidthCm = 15, flangeThicknessCm = 1.5f, webThicknessCm = 1 };
            Near((15 * 27000.0 - 14 * 19683.0) / 12 * 1e-8, inputs.SectionProperties().I, "I-beam I", 1e-6);
        }

        [Test]
        public void UnstableSupportsAreReportedBeforeSolving()
        {
            var inputs = new HoloMechInputs { leftEnd = EndCondition.Free, rightEnd = EndCondition.Pinned };
            Assert.That(inputs.TrySolve(out var s, out var error), Is.False);
            Assert.That(s, Is.Null);
            Assert.That(error, Does.Contain("unstable"));
        }

        [Test]
        public void BadLoadInputsAreReported()
        {
            var inputs = new HoloMechInputs
            {
                uniformLoad = new UniformLoadInput { enabled = true, startCm = 150, endCm = 100 },
                pointLoad = new PointLoadInput { enabled = true, positionCm = 250 },
            };
            var problems = inputs.Validate();
            Assert.That(problems, Has.Some.Contains("end distance must be greater"));
            Assert.That(problems, Has.Some.Contains("Point load must be on the beam"));
        }

        [Test]
        public void CloneIsDeep()
        {
            var a = new HoloMechInputs();
            var b = a.Clone();
            b.pointLoad.forceN = 1;
            b.pipe.wallCm = 1;
            Assert.That(a.pointLoad.forceN, Is.EqualTo(2000f));
            Assert.That(a.pipe.wallCm, Is.EqualTo(2.6f));
        }

        [Test]
        public void SectionLimitsAreEnforced()
        {
            var i = new HoloMechInputs();
            i.pipe.outerDiameterCm = 4; i.pipe.wallCm = 3;
            i.iBeam.heightCm = 6; i.iBeam.flangeThicknessCm = 4; i.iBeam.flangeWidthCm = 3; i.iBeam.webThicknessCm = 3;
            Assert.That(i.ClampSectionDimensions(), Is.True);
            Assert.That(i.pipe.wallCm, Is.EqualTo(2f));
            Assert.That(i.iBeam.flangeThicknessCm, Is.LessThan(3f));
            Assert.That(i.iBeam.webThicknessCm, Is.LessThanOrEqualTo(3f));
            Assert.That(i.ClampSectionDimensions(), Is.False, "already valid");
            foreach (var type in new[] { SectionType.Pipe, SectionType.IBeam })
            {
                i.section = type;
                Assert.DoesNotThrow(() => i.SectionProperties(), type.ToString());
            }
        }
    }
}
