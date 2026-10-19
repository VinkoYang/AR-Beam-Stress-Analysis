using System;
using NUnit.Framework;
using static ARBeam.Mechanics.Tests.TestSupport;

namespace ARBeam.Mechanics.Tests
{
    /// <summary>
    /// Overhanging and fixed–fixed beams. Overhang references are built by superposition of Hibbeler
    /// Appendix C cases (simply supported span with an end moment + cantilever overhang); fixed–fixed
    /// references are the standard results in Gere &amp; Goodno, Mechanics of Materials, Appendix H
    /// (also Roark's Formulas, Table 8.1).
    /// </summary>
    public class OverhangAndFixedTests
    {
        [Test]
        public void Overhang_RightOverhangEndLoad()
        {
            const double span = 1.5, c = L - span;
            var m = Model(BeamSupports.Overhang(0, span));
            m.PointLoads.Add(new PointLoad(L, P));
            var s = BeamSolver.Solve(m);

            AssertRelative(-P * c / span, s.Reactions[0].Force, Exact, "R_A (pulls down)");
            AssertRelative(P * (span + c) / span, s.Reactions[1].Force, Exact, "R_B");
            AssertRelative(-P * c * c * (span + c) / (3 * EI), s.Deflection(L), Exact, "v at tip");
            AssertRelative(-P * c, s.Moment(span), Exact, "M at support B");
            AssertRelative(-P * c, s.PeakMoment.Value, Exact, "M_max");

            // The span bows upward; its largest rise is at x = span/√3.
            double rise = P * c * span * span / (9 * Math.Sqrt(3) * EI);
            AssertRelative(rise, s.Deflection(span / Math.Sqrt(3)), Exact, "upward rise in span");
            AssertNearZero(s.Deflection(0), rise, "v at A");
            AssertNearZero(s.Deflection(span), rise, "v at B");
        }

        [Test]
        public void Overhang_BothSides_EndLoad()
        {
            // Bench layout scaled: supports at L/4 and 3L/4, load at the right tip.
            double a = L / 4, b = 3 * L / 4, span = b - a, c = L - b;
            var m = Model(BeamSupports.Overhang(a, b));
            m.PointLoads.Add(new PointLoad(L, P));
            var s = BeamSolver.Solve(m);

            AssertRelative(-P * c * c * (span + c) / (3 * EI), s.Deflection(L), Exact, "v at loaded tip");
            // The unloaded left overhang stays straight and follows the support rotation θ_A = M·span/(6EI).
            double thetaA = P * c * span / (6 * EI);
            AssertRelative(thetaA, s.Slope(a), Exact, "θ at A");
            AssertRelative(-thetaA * a, s.Deflection(0), Exact, "v at left tip");
            AssertNearZero(s.Moment(a / 2), P * c, "M on unloaded overhang");
        }

        [Test]
        public void FixedFixed_CentralPointLoad()
        {
            var m = Model(BeamSupports.FixedFixed());
            m.PointLoads.Add(new PointLoad(L / 2, P));
            var s = BeamSolver.Solve(m);

            AssertRelative(P / 2, s.Reactions[0].Force, Exact, "R_A");
            AssertRelative(P / 2, s.Reactions[1].Force, Exact, "R_B");
            AssertRelative(-P * L / 8, s.Moment(0), Exact, "M at A");
            AssertRelative(-P * L / 8, s.Moment(L), Exact, "M at B");
            AssertRelative(P * L / 8, s.Moment(L / 2), Exact, "M at centre");
            AssertRelative(-P * L * L * L / (192 * EI), s.Deflection(L / 2), Exact, "v_max");
            AssertNearZero(s.Slope(0), P * L * L / EI, "θ at A");
            AssertNearZero(s.Slope(L), P * L * L / EI, "θ at B");
        }

        [Test]
        public void FixedFixed_OffCentrePointLoad()
        {
            const double a = 0.5, b = L - a;
            var m = Model(BeamSupports.FixedFixed());
            m.PointLoads.Add(new PointLoad(a, P));
            var s = BeamSolver.Solve(m);

            AssertRelative(P * b * b * (3 * a + b) / (L * L * L), s.Reactions[0].Force, Exact, "R_A");
            AssertRelative(P * a * a * (a + 3 * b) / (L * L * L), s.Reactions[1].Force, Exact, "R_B");
            AssertRelative(-P * a * b * b / (L * L), s.Moment(0), Exact, "M_A");
            AssertRelative(-P * a * a * b / (L * L), s.Moment(L), Exact, "M_B");
            AssertRelative(2 * P * a * a * b * b / (L * L * L), s.Moment(a), Exact, "M under load");
            AssertRelative(-P * a * a * a * b * b * b / (3 * EI * L * L * L), s.Deflection(a), Exact, "v under load");
        }

        [Test]
        public void FixedFixed_FullUniformLoad()
        {
            var m = Model(BeamSupports.FixedFixed());
            m.UniformLoads.Add(new UniformLoad(0, L, W));
            var s = BeamSolver.Solve(m);

            AssertRelative(-W * L * L / 12, s.Moment(0), Exact, "M at ends");
            AssertRelative(-W * L * L / 12, s.Moment(L), Exact, "M at ends");
            AssertRelative(W * L * L / 24, s.Moment(L / 2), Exact, "M at centre");
            AssertRelative(-W * Math.Pow(L, 4) / (384 * EI), s.Deflection(L / 2), Exact, "v_max");
            AssertRelative(-W * L * L / 12, s.PeakMoment.Value, Exact, "peak |M| at the clamps");
        }
    }
}
