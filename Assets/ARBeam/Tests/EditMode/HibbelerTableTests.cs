using System;
using NUnit.Framework;
using static ARBeam.Mechanics.Tests.TestSupport;

namespace ARBeam.Mechanics.Tests
{
    /// <summary>
    /// Reference values from R. C. Hibbeler, Mechanics of Materials (10th ed.), Appendix C,
    /// "Slopes and Deflections of Beams". Hibbeler's convention matches the solver: loads downward,
    /// v and θ positive upward/counter-clockwise, so the tabulated deflections are negative.
    /// </summary>
    public class HibbelerTableTests
    {
        // ---- Cantilever beams (fixed at x = 0) ----

        [Test]
        public void Cantilever_EndPointLoad()
        {
            var m = Model(BeamSupports.Cantilever());
            m.PointLoads.Add(new PointLoad(L, P));
            var s = BeamSolver.Solve(m);

            AssertRelative(-P * L * L * L / (3 * EI), s.Deflection(L), Exact, "v_max");
            AssertRelative(-P * L * L / (2 * EI), s.Slope(L), Exact, "θ_max");
            double x = 0.3 * L;
            AssertRelative(-P * x * x * (3 * L - x) / (6 * EI), s.Deflection(x), Exact, "elastic curve");
            AssertRelative(-P * L, s.Moment(0), Exact, "M at clamp");
            AssertRelative(P, s.Shear(0.5 * L), Exact, "V");
            AssertRelative(P, s.Reactions[0].Force, Exact, "R");
            AssertRelative(P * L, s.Reactions[0].Moment, Exact, "M_reaction");
        }

        [Test]
        public void Cantilever_PointLoadAtMidspan()
        {
            var m = Model(BeamSupports.Cantilever());
            m.PointLoads.Add(new PointLoad(L / 2, P));
            var s = BeamSolver.Solve(m);

            AssertRelative(-5 * P * L * L * L / (48 * EI), s.Deflection(L), Exact, "v_max");
            AssertRelative(-P * L * L / (8 * EI), s.Slope(L), Exact, "θ_max");
            AssertNearZero(s.Moment(0.75 * L), P * L, "M beyond load");
        }

        [Test]
        public void Cantilever_FullUniformLoad()
        {
            var m = Model(BeamSupports.Cantilever());
            m.UniformLoads.Add(new UniformLoad(0, L, W));
            var s = BeamSolver.Solve(m);

            AssertRelative(-W * Math.Pow(L, 4) / (8 * EI), s.Deflection(L), Exact, "v_max");
            AssertRelative(-W * L * L * L / (6 * EI), s.Slope(L), Exact, "θ_max");
            double x = 0.4 * L;
            AssertRelative(-W * x * x * (x * x - 4 * L * x + 6 * L * L) / (24 * EI), s.Deflection(x), Exact, "elastic curve");
            AssertRelative(-W * L * L / 2, s.Moment(0), Exact, "M at clamp");
        }

        [Test]
        public void Cantilever_UniformLoadOnHalfNextToClamp()
        {
            var m = Model(BeamSupports.Cantilever());
            m.UniformLoads.Add(new UniformLoad(0, L / 2, W));
            var s = BeamSolver.Solve(m);

            AssertRelative(-7 * W * Math.Pow(L, 4) / (384 * EI), s.Deflection(L), Exact, "v_max");
            AssertRelative(-W * L * L * L / (48 * EI), s.Slope(L), Exact, "θ_max");
        }

        // ---- Simply supported beams ----

        [Test]
        public void SimplySupported_CentralPointLoad()
        {
            var m = Model(BeamSupports.SimplySupported());
            m.PointLoads.Add(new PointLoad(L / 2, P));
            var s = BeamSolver.Solve(m);

            AssertRelative(-P * L * L * L / (48 * EI), s.Deflection(L / 2), Exact, "v_max");
            AssertRelative(-P * L * L / (16 * EI), s.Slope(0), Exact, "θ_1");
            AssertRelative(P * L * L / (16 * EI), s.Slope(L), Exact, "θ_2");
            double x = 0.2 * L;
            AssertRelative(-P * x * (3 * L * L - 4 * x * x) / (48 * EI), s.Deflection(x), Exact, "elastic curve");

            AssertRelative(P * L / 4, s.PeakMoment.Value, Exact, "M_max");
            AssertRelative(L / 2, s.PeakMoment.Position, Exact, "x at M_max");
            AssertRelative(-P * L * L * L / (48 * EI), s.PeakDeflection.Value, Exact, "peak v");
        }

        [Test]
        public void SimplySupported_OffCentrePointLoad()
        {
            const double a = 1.4, b = L - a; // Hibbeler draws a > b
            var m = Model(BeamSupports.SimplySupported());
            m.PointLoads.Add(new PointLoad(a, P));
            var s = BeamSolver.Solve(m);

            AssertRelative(-P * a * b * (L + b) / (6 * EI * L), s.Slope(0), Exact, "θ_1");
            AssertRelative(P * a * b * (L + a) / (6 * EI * L), s.Slope(L), Exact, "θ_2");
            AssertRelative(-P * b * a * (L * L - b * b - a * a) / (6 * EI * L), s.Deflection(a), Exact, "v at load");
            double x = 0.25;
            AssertRelative(-P * b * x * (L * L - b * b - x * x) / (6 * L * EI), s.Deflection(x), Exact, "elastic curve");

            double xMax = Math.Sqrt((L * L - b * b) / 3);
            double vMax = -P * b * Math.Pow(L * L - b * b, 1.5) / (9 * Math.Sqrt(3) * L * EI);
            AssertRelative(vMax, s.PeakDeflection.Value, Exact, "v_max");
            AssertRelative(xMax, s.PeakDeflection.Position, 1e-7, "x at v_max");

            AssertRelative(P * b / L, s.Reactions[0].Force, Exact, "R_A");
            AssertRelative(P * a / L, s.Reactions[1].Force, Exact, "R_B");
            AssertRelative(P * a * b / L, s.PeakMoment.Value, Exact, "M_max");
        }

        [Test]
        public void SimplySupported_FullUniformLoad()
        {
            var m = Model(BeamSupports.SimplySupported());
            m.UniformLoads.Add(new UniformLoad(0, L, W));
            var s = BeamSolver.Solve(m);

            AssertRelative(-5 * W * Math.Pow(L, 4) / (384 * EI), s.Deflection(L / 2), Exact, "v_max");
            AssertRelative(-W * L * L * L / (24 * EI), s.Slope(0), Exact, "θ_max");
            double x = 0.3 * L;
            AssertRelative(-W * x * (x * x * x - 2 * L * x * x + L * L * L) / (24 * EI), s.Deflection(x), Exact, "elastic curve");
            AssertRelative(W * L * L / 8, s.PeakMoment.Value, Exact, "M_max");
            AssertRelative(L / 2, s.PeakMoment.Position, Exact, "x at M_max");
            AssertRelative(-5 * W * Math.Pow(L, 4) / (384 * EI), s.PeakDeflection.Value, Exact, "peak v");
        }

        [Test]
        public void SimplySupported_UniformLoadOnLeftHalf()
        {
            var m = Model(BeamSupports.SimplySupported());
            m.UniformLoads.Add(new UniformLoad(0, L / 2, W));
            var s = BeamSolver.Solve(m);

            AssertRelative(-3 * W * L * L * L / (128 * EI), s.Slope(0), Exact, "θ_1");
            AssertRelative(7 * W * L * L * L / (384 * EI), s.Slope(L), Exact, "θ_2");
            AssertRelative(-5 * W * Math.Pow(L, 4) / (768 * EI), s.Deflection(L / 2), Exact, "v at midspan");
            double x = 0.3 * L;
            AssertRelative(-W * x * (16 * x * x * x - 24 * L * x * x + 9 * L * L * L) / (384 * EI), s.Deflection(x), Exact,
                "elastic curve");

            // Hibbeler tabulates these two with four significant figures.
            AssertRelative(-0.006563 * W * Math.Pow(L, 4) / EI, s.PeakDeflection.Value, OnePercent, "v_max");
            AssertRelative(0.4598 * L, s.PeakDeflection.Position, OnePercent, "x at v_max");
        }
    }
}
