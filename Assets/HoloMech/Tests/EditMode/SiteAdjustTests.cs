using System;
using ARBeam.Mechanics;
using NUnit.Framework;

namespace HoloMech.Tests
{
    /// <summary>On-site "Dimensions" rows: millimetre steps on the cm inputs, ranges and section limits.</summary>
    public class SiteAdjustTests
    {
        static void Near(double expected, double actual, string what, double tolerance = 1e-3)
            => Assert.That(actual, Is.EqualTo(expected).Within(tolerance), what);

        [Test]
        public void RowsFollowTheSectionType()
        {
            Assert.That(SiteAdjust.For(SectionType.Pipe), Is.EqualTo(new[]
                { SiteDimension.Length, SiteDimension.PipeOuterDiameter, SiteDimension.PipeWall }));
            Assert.That(SiteAdjust.For(SectionType.Box), Is.EqualTo(new[]
                { SiteDimension.Length, SiteDimension.BoxWidth, SiteDimension.BoxHeight, SiteDimension.BoxWall }));
            Assert.That(SiteAdjust.For(SectionType.IBeam), Is.EqualTo(new[]
            {
                SiteDimension.Length, SiteDimension.IHeight, SiteDimension.IFlangeWidth, SiteDimension.IFlangeThickness, SiteDimension.IWebThickness,
            }));
        }

        [Test]
        public void EveryRowStepsItsOwnDimensionInMillimetres()
        {
            foreach (SiteDimension d in Enum.GetValues(typeof(SiteDimension)))
            {
                var i = new HoloMechInputs();
                float before = SiteAdjust.ValueMm(i, d);
                Assert.That(SiteAdjust.Step(i, d, -1f), Is.EqualTo(SiteStepResult.Changed), d.ToString());
                Near(before - 1f, SiteAdjust.ValueMm(i, d), d.ToString());
            }
        }

        [Test]
        public void StepsChangeTheSectionProperties()
        {
            var i = new HoloMechInputs
            {
                section = SectionType.IBeam,
                iBeam = new IBeamDimensions { heightCm = 30, flangeWidthCm = 15, flangeThicknessCm = 1.5f, webThicknessCm = 1 },
            };
            SiteAdjust.Step(i, SiteDimension.IHeight, 10f);
            SiteAdjust.Step(i, SiteDimension.IWebThickness, 5f);
            Near(31.0, i.iBeam.heightCm, "h");
            Near(1.5, i.iBeam.webThicknessCm, "tw");
            double expected = BeamSection.IBeam(0.31, 0.15, 0.015, 0.015).I;
            Near(expected, i.SectionProperties().I, "I follows the stepped dimensions", expected * 1e-5);
        }

        [Test]
        public void ManySmallStepsDoNotDrift()
        {
            var i = new HoloMechInputs { lengthCm = 200f };
            for (int k = 0; k < 500; k++) SiteAdjust.Step(i, SiteDimension.Length, 1f);
            Near(2500.0, SiteAdjust.ValueMm(i, SiteDimension.Length), "500 × 1 mm");
        }

        [Test]
        public void SiteLengthLimitDoesNotChangeTheSliderLimit()
        {
            Assert.That(HoloMechRanges.LengthMaxCm, Is.EqualTo(500f), "Guided and the Workbench slider");
            Assert.That(HoloMechRanges.SiteLengthMaxCm, Is.EqualTo(1000f));

            var i = new HoloMechInputs { lengthCm = 499.95f };
            Assert.That(SiteAdjust.Step(i, SiteDimension.Length, 10f), Is.EqualTo(SiteStepResult.Changed), "past the slider limit");
            Assert.That(i.lengthCm, Is.GreaterThan(500f));
            Assert.That(i.TrySolve(out _, out var error), Is.True, error);

            i.SetLength(999.95f);
            Assert.That(SiteAdjust.Step(i, SiteDimension.Length, 10f), Is.EqualTo(SiteStepResult.AtLimit));
            Assert.That(i.lengthCm, Is.EqualTo(1000f));
        }

        [Test]
        public void LengthStepKeepsTheExistingSupportRule()
        {
            var i = new HoloMechInputs();
            i.supports.Clear();
            i.supports.Add(new SupportInput { kind = SupportKind.Pin, positionCm = 40f });
            i.supports.Add(new SupportInput { kind = SupportKind.Pin, atRightEnd = true });
            SiteAdjust.Step(i, SiteDimension.Length, 100f);
            Near(210.0, i.lengthCm, "L");
            Near(40.0, i.supports[0].PositionOn(i.lengthCm), "interior support keeps its distance from the left end");
            Near(210.0, i.supports[1].PositionOn(i.lengthCm), "right-end support follows");
        }

        [Test]
        public void RangeLimitsAreReported()
        {
            var i = new HoloMechInputs();
            i.iBeam.heightCm = HoloMechRanges.IHeightMinCm;
            i.iBeam.flangeThicknessCm = 1f;
            Assert.That(SiteAdjust.Step(i, SiteDimension.IHeight, -1f), Is.EqualTo(SiteStepResult.AtLimit));
            Assert.That(i.iBeam.heightCm, Is.EqualTo(HoloMechRanges.IHeightMinCm));
        }

        [Test]
        public void SectionLimitsAreReported()
        {
            var i = new HoloMechInputs { section = SectionType.IBeam };
            i.iBeam = new IBeamDimensions { heightCm = 6f, flangeWidthCm = 5f, flangeThicknessCm = 1f, webThicknessCm = 1f };
            Assert.That(SiteAdjust.Step(i, SiteDimension.IFlangeThickness, 50f), Is.EqualTo(SiteStepResult.SectionLimited), "growing tf past h/2");
            Assert.That(2 * i.iBeam.flangeThicknessCm, Is.LessThan(i.iBeam.heightCm));
            Assert.That(i.TrySolve(out _, out var error), Is.True, error);

            i.iBeam = new IBeamDimensions { heightCm = 6f, flangeWidthCm = 5f, flangeThicknessCm = 2.9f, webThicknessCm = 1f };
            Assert.That(SiteAdjust.Step(i, SiteDimension.IHeight, -10f), Is.EqualTo(SiteStepResult.SectionLimited), "shrinking h pulls tf back");
            Assert.That(2 * i.iBeam.flangeThicknessCm, Is.LessThan(i.iBeam.heightCm));

            i.section = SectionType.Pipe;
            i.pipe = new PipeDimensions { outerDiameterCm = 4f, wallCm = 2f };
            Assert.That(SiteAdjust.Step(i, SiteDimension.PipeOuterDiameter, -10f), Is.EqualTo(SiteStepResult.SectionLimited), "shrinking D pulls the wall back");
            Near(1.5, i.pipe.wallCm, "wall = D/2");
        }
    }
}
