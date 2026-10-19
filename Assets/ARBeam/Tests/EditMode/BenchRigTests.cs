using NUnit.Framework;
using static ARBeam.Mechanics.Tests.TestSupport;

namespace ARBeam.Mechanics.Tests
{
    /// <summary>
    /// The physical rig planned in "实验装置 DIY 方案_v01" (2026-10-18): 6061-T6 flat bar 1" × 1/8",
    /// E = 69 GPa, 500 g hanger at the worst position. The plan quotes deflections to 0.1 mm and stresses to
    /// 1 MPa, so these use the 1 % acceptance tolerance.
    /// </summary>
    public class BenchRigTests
    {
        static readonly BeamSection Bar = BeamSection.Rectangular(0.0254, 0.003175);
        const double E69 = 69e9;
        const double Mass = 0.5;
        const double G = 9.81; // the plan used 9.81

        [Test]
        public void SecondMomentOfArea_Is67Point7mm4()
        {
            AssertRelative(67.7e-12, Bar.I, OnePercent, "I");
        }

        [Test]
        public void Cantilever400_TipLoad()
        {
            var m = new BeamModel(0.4, Bar, E69, BeamSupports.Cantilever());
            m.PointLoads.Add(PointLoad.FromMass(0.4, Mass, G));
            var s = BeamSolver.Solve(m);

            AssertRelative(-22.4e-3, s.PeakDeflection.Value, OnePercent, "tip deflection");
            AssertRelative(46e6, s.MaxBendingStress, OnePercent, "σ_max");
        }

        [Test]
        public void SimplySupported800_MidspanLoad()
        {
            var m = new BeamModel(0.8, Bar, E69, BeamSupports.SimplySupported());
            m.PointLoads.Add(PointLoad.FromMass(0.4, Mass, G));
            var s = BeamSolver.Solve(m);

            AssertRelative(-11.2e-3, s.PeakDeflection.Value, OnePercent, "midspan deflection");
            AssertRelative(23e6, s.MaxBendingStress, OnePercent, "σ_max");
        }

        [Test]
        public void Overhang800_Supports200And600_TipLoad()
        {
            var m = new BeamModel(0.8, Bar, E69, BeamSupports.Overhang(0.2, 0.6));
            m.PointLoads.Add(PointLoad.FromMass(0.8, Mass, G));
            var s = BeamSolver.Solve(m);

            AssertRelative(-8.4e-3, s.Deflection(0.8), OnePercent, "tip deflection");
            AssertRelative(23e6, s.MaxBendingStress, OnePercent, "σ_max");
        }
    }
}
