using ARBeam.Mechanics;
using NUnit.Framework;

namespace HoloMech.Tests
{
    /// <summary>The Gantry preset and its on-site rows for the two pins and the trolley.</summary>
    public class GantryPresetTests
    {
        static void Near(double expected, double actual, string what, double tolerance = 1e-3)
            => Assert.That(actual, Is.EqualTo(expected).Within(tolerance), what);

        static HoloMechInputs Gantry(float lengthCm = 300f)
        {
            var i = new HoloMechInputs { lengthCm = lengthCm };
            i.ApplyGantryPreset();
            return i;
        }

        [Test]
        public void PresetSetsSectionMaterialSupportsAndTrolley()
        {
            var i = new HoloMechInputs { lengthCm = 300f, section = SectionType.Box };
            i.uniformLoad = new UniformLoadInput { enabled = true };
            i.pointLoads.Add(new PointLoadInput());
            i.ApplyGantryPreset();

            Assert.That(i.IsGantry, Is.True);
            Assert.That(i.section, Is.EqualTo(SectionType.IBeam));
            Assert.That(i.material, Is.EqualTo(MaterialPreset.Aluminum));
            Near(68.9, i.elasticModulusGPa, "E as BeamMaterial.Aluminum6061T6");

            Assert.That(i.supports.Count, Is.EqualTo(2));
            Assert.That(i.supports[0].kind, Is.EqualTo(SupportKind.Pin));
            Assert.That(i.supports[1].kind, Is.EqualTo(SupportKind.Pin));
            Near(0, i.supports[0].PositionOn(300f), "left pin at x = 0");
            Near(300, i.supports[1].PositionOn(300f), "right pin at x = L");

            Assert.That(i.pointLoads.Count, Is.EqualTo(1));
            Assert.That(i.pointLoads[0].enabled, Is.True);
            Near(150, i.pointLoads[0].positionCm, "trolley at midspan");
            Assert.That(i.uniformLoads, Is.Empty);
        }

        [Test]
        public void PresetSolvesAsASimplySupportedBeamWithACentralLoad()
        {
            var i = Gantry();
            Assert.That(i.TrySolve(out var s, out var error), Is.True, error);
            var section = i.SectionProperties();
            double m = i.pointLoads[0].forceN * 3.0 / 4;
            Assert.That(s.MaxBendingStress, Is.EqualTo(m * section.C / section.I).Within(1e-3 * m * section.C / section.I), "PL/4 · c/I");
        }

        [Test]
        public void AluminumDoesNotRenumberTheStoredMaterialValues()
        {
            // HoloMechXR and HoloMechQuest store material: 3.
            Assert.That((int)MaterialPreset.Custom, Is.EqualTo(3));
            Assert.That((int)MaterialPreset.Steel, Is.EqualTo(0));
            var i = new HoloMechInputs();
            i.ApplyMaterial(MaterialPreset.Aluminum);
            Near(68.9, i.elasticModulusGPa, "button sets E");
        }

        [Test]
        public void OneMillimetreLeavesTheRightEnd()
        {
            // The Workbench slider snaps to the end within 5 mm; a 1 mm row step must not.
            var i = Gantry();
            Assert.That(SiteAdjust.StepGantrySupport(i, true, -1f), Is.EqualTo(SiteStepResult.Changed));
            Near(2999.0, SiteAdjust.GantrySupportMm(i, true), "right pin moved in by 1 mm");
            Assert.That(i.supports[1].atRightEnd, Is.False);
            SiteAdjust.StepGantrySupport(i, true, -1f);
            Near(2998.0, SiteAdjust.GantrySupportMm(i, true), "and on by another");
        }

        [Test]
        public void SupportBackOnTheEndFollowsTheLengthAgain()
        {
            var i = Gantry();
            SiteAdjust.StepGantrySupport(i, true, -5f);
            SiteAdjust.Step(i, SiteDimension.Length, 100f);
            Near(2995.0, SiteAdjust.GantrySupportMm(i, true), "moved in: keeps its distance from the left end");

            Assert.That(SiteAdjust.StepGantrySupport(i, true, 1000f), Is.EqualTo(SiteStepResult.AtLimit));
            Assert.That(i.supports[1].atRightEnd, Is.True);
            SiteAdjust.Step(i, SiteDimension.Length, 100f);
            Near(3200.0, SiteAdjust.GantrySupportMm(i, true), "on the end: follows");
        }

        [Test]
        public void SupportsPositionIndependentlyOfTheLengthAndKeepOverhangs()
        {
            var i = Gantry();
            SiteAdjust.StepGantrySupport(i, false, 200f);
            SiteAdjust.StepGantrySupport(i, true, -200f);
            Near(200.0, SiteAdjust.GantrySupportMm(i, false), "left pin");
            Near(2800.0, SiteAdjust.GantrySupportMm(i, true), "right pin");
            Near(300.0, i.lengthCm, "length unchanged");

            Assert.That(i.TrySolve(out var s, out var error), Is.True, error);
            Assert.That(s.Reactions.Count, Is.EqualTo(2));
            Near(0.2, s.Reactions[0].Position, "reaction at the left pin");
            Near(2.8, s.Reactions[1].Position, "reaction at the right pin");
            double m = i.pointLoads[0].forceN * 2.6 / 4;
            Near(m, s.PeakMoment.Value, "PL/4 over the 2.6 m between the pins", 1e-3 * m);
        }

        [Test]
        public void SupportsCannotCross()
        {
            var i = Gantry(100f);
            Assert.That(SiteAdjust.StepGantrySupport(i, false, 5000f), Is.EqualTo(SiteStepResult.AtLimit));
            Near(990.0, SiteAdjust.GantrySupportMm(i, false), "left stops 1 cm short of the right");
            Assert.That(SiteAdjust.StepGantrySupport(i, true, -5000f), Is.EqualTo(SiteStepResult.AtLimit));
            Assert.That(SiteAdjust.GantrySupportMm(i, true), Is.GreaterThan(SiteAdjust.GantrySupportMm(i, false)));
            Assert.That(i.Validate(), Is.Empty);

            Assert.That(SiteAdjust.StepGantrySupport(i, false, -9000f), Is.EqualTo(SiteStepResult.AtLimit));
            Near(0, SiteAdjust.GantrySupportMm(i, false), "left stops at the left end");
        }

        [Test]
        public void RowsFollowTheSupportsByPosition()
        {
            var i = Gantry();
            i.supports[0].positionCm = 250f;
            i.supports[1].atRightEnd = false;
            i.supports[1].positionCm = 50f;
            Near(500.0, SiteAdjust.GantrySupportMm(i, false), "left row = the lower x");
            Near(2500.0, SiteAdjust.GantrySupportMm(i, true), "right row = the higher x");
        }

        [Test]
        public void TrolleyMovesAlongTheBeamOnly()
        {
            var i = Gantry();
            Assert.That(SiteAdjust.StepTrolley(i, -1f), Is.EqualTo(SiteStepResult.Changed));
            Near(1499.0, SiteAdjust.TrolleyMm(i), "1 mm left");
            Assert.That(SiteAdjust.StepTrolley(i, 9000f), Is.EqualTo(SiteStepResult.AtLimit));
            Near(3000.0, SiteAdjust.TrolleyMm(i), "stops at the right end");
        }

        [Test]
        public void GantryStateNeedsItsSupportsAndTrolley()
        {
            var i = Gantry();
            Assert.That(i.Clone().IsGantry, Is.True, "clones keep the preset");

            var noSupport = Gantry();
            noSupport.supports.RemoveAt(1);
            Assert.That(noSupport.IsGantry, Is.False);

            var noTrolley = Gantry();
            noTrolley.pointLoads.Clear();
            Assert.That(noTrolley.IsGantry, Is.False);

            var changedEnd = Gantry();
            changedEnd.leftEnd = EndCondition.Fixed;
            Assert.That(changedEnd.IsGantry, Is.False, "the Guided boundary panel leaves the preset");
        }
    }
}
