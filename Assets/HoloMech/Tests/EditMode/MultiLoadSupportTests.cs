using System;
using System.Linq;
using ARBeam.Mechanics;
using NUnit.Framework;

namespace HoloMech.Tests
{
    /// <summary>
    /// Workbench inputs with several loads and supports at any position (PI's request, 2026-10-28): the input model
    /// passes them all to the solver, keeps end supports on the ends when the length changes, and reports layouts that
    /// cannot be solved.
    /// </summary>
    public class MultiLoadSupportTests
    {
        static void Near(double expected, double actual, string what, double rel = 1e-9)
            => Assert.That(actual, Is.EqualTo(expected).Within(Math.Abs(expected) * rel + 1e-9), what);

        static HoloMechInputs Bare(float lengthCm)
        {
            var i = new HoloMechInputs();
            i.SetLength(lengthCm);
            i.pointLoads.Clear();
            i.uniformLoads.Clear();
            i.supports.Clear();
            return i;
        }

        static double ReactionAt(BeamSolution s, double x) => s.Reactions.Single(r => Math.Abs(r.Position - x) < 1e-9).Force;

        [Test]
        public void SeveralPointLoadsSuperpose()
        {
            // Simply supported 4 m, 1000 N at 1 m and 3000 N at 3 m: R_A = (1000·3 + 3000·1)/4 = 1500 N, R_B = 2500 N.
            var i = Bare(400);
            i.leftEnd = EndCondition.Pinned;
            i.rightEnd = EndCondition.Pinned;
            i.pointLoads.Add(new PointLoadInput { positionCm = 100, forceN = 1000 });
            i.pointLoads.Add(new PointLoadInput { positionCm = 300, forceN = 3000 });
            Assert.That(i.TrySolve(out var s, out var error), Is.True, error);
            Assert.That(s.Model.PointLoads.Count, Is.EqualTo(2));
            Near(1500, ReactionAt(s, 0), "R_A");
            Near(2500, ReactionAt(s, 4), "R_B");
        }

        [Test]
        public void DisabledLoadsAreIgnored()
        {
            var i = Bare(400);
            i.leftEnd = EndCondition.Fixed;
            i.pointLoads.Add(new PointLoadInput { positionCm = 400, forceN = 1000 });
            i.pointLoads.Add(new PointLoadInput { enabled = false, positionCm = 200, forceN = 9000 });
            i.uniformLoads.Add(new UniformLoadInput { enabled = false, startCm = 0, endCm = 400, intensityNPerM = 500 });
            Assert.That(i.TrySolve(out var s, out var error), Is.True, error);
            Near(1000, ReactionAt(s, 0), "cantilever reaction");
        }

        [Test]
        public void TwoSpanContinuousBeamUnderFullUniformLoad()
        {
            // Pins at 0, L and 2L, w over the whole beam: end reactions 3wL/8, middle 10wL/8, M over the middle = −wL²/8.
            const double w = 2000, L = 3;
            var i = Bare(600);
            i.supports.Add(new SupportInput { kind = SupportKind.Pin, positionCm = 0 });
            i.supports.Add(new SupportInput { kind = SupportKind.Pin, positionCm = 300 });
            i.supports.Add(new SupportInput { kind = SupportKind.Pin, atRightEnd = true });
            i.uniformLoads.Add(new UniformLoadInput { enabled = true, startCm = 0, endCm = 600, intensityNPerM = (float)w });
            Assert.That(i.TrySolve(out var s, out var error), Is.True, error);
            Near(3 * w * L / 8, ReactionAt(s, 0), "end reaction", 1e-6);
            Near(10 * w * L / 8, ReactionAt(s, 3), "middle reaction", 1e-6);
            Near(-w * L * L / 8, s.Moment(3), "moment over the middle support", 1e-6);
        }

        [Test]
        public void SeveralUniformLoadsAddUp()
        {
            // Two halves of a full-span load equal the full-span load: R = wL/2 at each pin of a simply supported beam.
            var i = Bare(400);
            i.leftEnd = EndCondition.Pinned;
            i.rightEnd = EndCondition.Pinned;
            i.uniformLoads.Add(new UniformLoadInput { enabled = true, startCm = 0, endCm = 200, intensityNPerM = 1000 });
            i.uniformLoads.Add(new UniformLoadInput { enabled = true, startCm = 200, endCm = 400, intensityNPerM = 1000 });
            Assert.That(i.TrySolve(out var s, out var error), Is.True, error);
            Near(2000, ReactionAt(s, 0), "R_A");
            Near(1000 * 4 * 4 / 8.0, s.Moment(2), "midspan moment wL²/8");
        }

        [Test]
        public void InteriorClampAndProppedOverhangSolve()
        {
            // Clamp in the middle of the beam and a pin at the right end; both overhangs carry loads.
            var i = Bare(400);
            i.supports.Add(new SupportInput { kind = SupportKind.Fixed, positionCm = 150 });
            i.supports.Add(new SupportInput { kind = SupportKind.Pin, atRightEnd = true });
            i.pointLoads.Add(new PointLoadInput { positionCm = 0, forceN = 500 });
            i.pointLoads.Add(new PointLoadInput { positionCm = 300, forceN = 800 });
            Assert.That(i.TrySolve(out var s, out var error), Is.True, error);
            Near(1300, s.Reactions.Sum(r => r.Force), "vertical equilibrium");
            Near(0, s.Deflection(1.5), "no deflection at the clamp");
            Near(0, s.Slope(1.5), "no rotation at the clamp");
            Near(0, s.Deflection(4), "no deflection at the pin");
        }

        [Test]
        public void RightEndSupportFollowsTheLength()
        {
            var i = new HoloMechInputs();
            i.rightEnd = EndCondition.Pinned;
            i.supports.Add(new SupportInput { kind = SupportKind.Pin, positionCm = 120 });
            i.SetLength(300);
            Assert.That(i.rightEnd, Is.EqualTo(EndCondition.Pinned));
            Assert.That(i.Supports.Resolve(3.0).Select(s => s.Position), Is.EqualTo(new[] { 0.0, 1.2, 3.0 }).Within(1e-6));
            i.SetLength(100);
            Assert.That(i.supports.Single(s => !s.atRightEnd && s.kind == SupportKind.Pin).positionCm, Is.EqualTo(100f));
            Assert.That(i.Validate(), Has.Some.Contains("same place"));
        }

        [Test]
        public void LayoutsThatCannotBeSolvedAreReported()
        {
            var none = Bare(200);
            Assert.That(none.Validate(), Has.Some.Contains("no supports"));

            var onePin = Bare(200);
            onePin.supports.Add(new SupportInput { kind = SupportKind.Pin, positionCm = 100 });
            Assert.That(onePin.Validate(), Has.Some.Contains("single pinned support"));

            var twoPins = Bare(200);
            twoPins.supports.Add(new SupportInput { kind = SupportKind.Pin, positionCm = 50 });
            twoPins.supports.Add(new SupportInput { kind = SupportKind.Pin, positionCm = 150 });
            Assert.That(twoPins.Validate(), Is.Empty);
        }

        [Test]
        public void CloneCopiesTheLists()
        {
            var a = new HoloMechInputs();
            a.pointLoads.Add(new PointLoadInput { forceN = 10 });
            var b = a.Clone();
            b.pointLoads[1].forceN = 99;
            b.supports[0].positionCm = 50;
            b.uniformLoads.Clear();
            Assert.That(a.pointLoads[1].forceN, Is.EqualTo(10f));
            Assert.That(a.supports[0].positionCm, Is.EqualTo(0f));
            Assert.That(a.uniformLoads.Count, Is.EqualTo(1));
        }
    }
}
