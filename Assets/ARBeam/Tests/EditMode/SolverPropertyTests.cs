using System;
using System.Collections.Generic;
using NUnit.Framework;
using static ARBeam.Mechanics.Tests.TestSupport;

namespace ARBeam.Mechanics.Tests
{
    /// <summary>
    /// Checks that hold for any load case: equilibrium, the differential relations between V, M, θ and v,
    /// support conditions, superposition, and the diagram sampler.
    /// </summary>
    public class SolverPropertyTests
    {
        static IEnumerable<BeamSupports> AllSupports()
        {
            yield return BeamSupports.Cantilever();
            yield return BeamSupports.SimplySupported();
            yield return BeamSupports.Overhang(0.5, 1.5);
            yield return BeamSupports.Overhang(0.3, 2.0);
            yield return BeamSupports.FixedFixed();
        }

        static BeamModel RandomCase(BeamSupports supports, int seed)
        {
            var rng = new Random(seed);
            var m = Model(supports);
            int n = 1 + rng.Next(4);
            for (int i = 0; i < n; i++) m.PointLoads.Add(new PointLoad(rng.NextDouble() * L, 200 + 1800 * rng.NextDouble()));
            double s0 = rng.NextDouble() * L * 0.6;
            m.UniformLoads.Add(new UniformLoad(s0, s0 + 0.2 + rng.NextDouble() * (L - s0 - 0.2), 100 + 900 * rng.NextDouble()));
            return m;
        }

        [Test, Combinatorial]
        public void ReactionsBalanceLoads(
            [ValueSource(nameof(AllSupports))] BeamSupports supports,
            [Values(1, 2, 3, 4, 5)] int seed)
        {
            var m = RandomCase(supports, seed);
            var s = BeamSolver.Solve(m);

            double force = 0, moment = 0; // upward forces and counter-clockwise moments about x = 0
            foreach (var p in m.PointLoads)
            {
                force -= p.Force;
                moment -= p.Force * p.Position;
            }

            foreach (var w in m.UniformLoads)
            {
                force -= w.Resultant;
                moment -= w.Resultant * w.Centroid;
            }

            double scale = Math.Abs(force);
            foreach (var r in s.Reactions)
            {
                force += r.Force;
                moment += r.Force * r.Position + r.Moment;
            }

            AssertNearZero(force, scale, "ΣF");
            AssertNearZero(moment, scale * L, "ΣM");
        }

        [Test, Combinatorial]
        public void DifferentialRelationsHold(
            [ValueSource(nameof(AllSupports))] BeamSupports supports,
            [Values(1, 2, 3)] int seed)
        {
            var s = BeamSolver.Solve(RandomCase(supports, seed));
            const double h = 1e-5;
            double vScale = Math.Abs(s.PeakShear.Value);
            double thetaScale = 0;
            for (int i = 0; i <= 50; i++) thetaScale = Math.Max(thetaScale, Math.Abs(s.Slope(L * i / 50)));

            for (int i = 1; i < 200; i++)
            {
                double x = L * (i + 0.37) / 200;
                if (x + h >= L || NearKeyPoint(s, x, 2 * h)) continue;

                double dM = (s.Moment(x + h) - s.Moment(x - h)) / (2 * h);
                AssertNearZero(dM - s.Shear(x), vScale, $"dM/dx − V at x={x:F4}", 1e-5);

                double dv = (s.Deflection(x + h) - s.Deflection(x - h)) / (2 * h);
                AssertNearZero(dv - s.Slope(x), thetaScale, $"dv/dx − θ at x={x:F4}", 1e-5);
            }
        }

        [Test, Combinatorial]
        public void SupportConditionsHold(
            [ValueSource(nameof(AllSupports))] BeamSupports supports,
            [Values(1, 2, 3)] int seed)
        {
            var m = RandomCase(supports, seed);
            var s = BeamSolver.Solve(m);
            double vScale = Math.Abs(s.PeakDeflection.Value);
            double mScale = Math.Abs(s.PeakMoment.Value);
            double thetaScale = vScale / L;

            switch (supports.Type)
            {
                case SupportType.Cantilever:
                    AssertNearZero(s.Deflection(0), vScale, "v(0)");
                    AssertNearZero(s.Slope(0), thetaScale, "θ(0)");
                    AssertNearZero(s.MomentLeft(L), mScale, "M at free end");
                    break;
                case SupportType.FixedFixed:
                    AssertNearZero(s.Deflection(0), vScale, "v(0)");
                    AssertNearZero(s.Slope(0), thetaScale, "θ(0)");
                    AssertNearZero(s.Deflection(L), vScale, "v(L)");
                    AssertNearZero(s.Slope(L), thetaScale, "θ(L)");
                    break;
                default:
                    var (a, b) = m.SupportPositions;
                    AssertNearZero(s.Deflection(a), vScale, "v(a)");
                    AssertNearZero(s.Deflection(b), vScale, "v(b)");
                    AssertNearZero(s.MomentRight(0), mScale, "M at left end");
                    AssertNearZero(s.MomentLeft(L), mScale, "M at right end");
                    break;
            }
        }

        [Test]
        public void Superposition()
        {
            foreach (var supports in AllSupports())
            {
                var both = Model(supports);
                var first = Model(supports);
                var second = Model(supports);
                both.PointLoads.Add(new PointLoad(0.7, 800));
                first.PointLoads.Add(new PointLoad(0.7, 800));
                both.UniformLoads.Add(new UniformLoad(0.2, 1.9, 300));
                second.UniformLoads.Add(new UniformLoad(0.2, 1.9, 300));

                var sb = BeamSolver.Solve(both);
                var s1 = BeamSolver.Solve(first);
                var s2 = BeamSolver.Solve(second);
                double vScale = Math.Abs(sb.PeakDeflection.Value);
                double mScale = Math.Abs(sb.PeakMoment.Value);

                for (int i = 0; i <= 20; i++)
                {
                    double x = L * i / 20;
                    AssertNearZero(sb.Deflection(x) - s1.Deflection(x) - s2.Deflection(x), vScale, $"{supports} v({x})");
                    AssertNearZero(sb.Moment(x) - s1.Moment(x) - s2.Moment(x), mScale, $"{supports} M({x})");
                }
            }
        }

        [Test]
        public void PeaksMatchDenseSampling()
        {
            foreach (var supports in AllSupports())
            {
                for (int seed = 1; seed <= 5; seed++)
                {
                    var s = BeamSolver.Solve(RandomCase(supports, seed));
                    double maxM = 0, maxV = 0;
                    const int n = 20000;
                    for (int i = 0; i <= n; i++)
                    {
                        double x = L * i / n;
                        maxM = Math.Max(maxM, Math.Abs(s.Moment(x)));
                        maxV = Math.Max(maxV, Math.Abs(s.Deflection(x)));
                    }

                    // Moment peaks under point loads are kinks; a uniform grid would miss them by O(V·Δx).
                    foreach (var k in s.KeyPoints)
                    {
                        maxM = Math.Max(maxM, Math.Max(Math.Abs(s.MomentLeft(k)), Math.Abs(s.MomentRight(k))));
                        maxV = Math.Max(maxV, Math.Abs(s.Deflection(k)));
                    }

                    Assert.That(Math.Abs(s.PeakMoment.Value), Is.GreaterThanOrEqualTo(maxM * (1 - 1e-12)), $"{supports} seed {seed} |M|max");
                    AssertRelative(maxM, Math.Abs(s.PeakMoment.Value), 1e-6, $"{supports} seed {seed} |M|max");
                    Assert.That(Math.Abs(s.PeakDeflection.Value), Is.GreaterThanOrEqualTo(maxV * (1 - 1e-12)), $"{supports} seed {seed} |v|max");
                    AssertRelative(maxV, Math.Abs(s.PeakDeflection.Value), 1e-6, $"{supports} seed {seed} |v|max");
                }
            }
        }

        [Test]
        public void ShearDiagramStepsByLoadAtPointLoad()
        {
            var m = Model(BeamSupports.SimplySupported());
            m.PointLoads.Add(new PointLoad(0.5, 300));
            m.PointLoads.Add(new PointLoad(1.2, 700));
            var s = BeamSolver.Solve(m);

            AssertRelative(-300, s.ShearRight(0.5) - s.ShearLeft(0.5), Exact, "jump at 0.5");
            AssertRelative(-700, s.ShearRight(1.2) - s.ShearLeft(1.2), Exact, "jump at 1.2");

            var d = s.Diagram(BeamQuantity.Shear, 4);
            Assert.That(d[0].X, Is.EqualTo(0));
            Assert.That(d[d.Count - 1].X, Is.EqualTo(L));
            int steps = 0;
            for (int i = 1; i < d.Count; i++)
            {
                Assert.That(d[i].X, Is.GreaterThanOrEqualTo(d[i - 1].X), "x must be non-decreasing");
                if (d[i].X == d[i - 1].X && Math.Abs(d[i].Value - d[i - 1].Value) > 1) steps++;
            }

            Assert.That(steps, Is.EqualTo(2), "one vertical step per interior point load");
            AssertRelative(s.Reactions[0].Force, d[0].Value, Exact, "V starts at R_A");
            AssertRelative(-s.Reactions[1].Force, d[d.Count - 1].Value, Exact, "V ends at −R_B");
        }

        [Test]
        public void SelfWeightOfBenchBar()
        {
            var bar = BeamSection.Rectangular(0.025, 0.003);
            var w = UniformLoad.SelfWeight(BeamMaterial.Aluminum6061T6, bar, 0.8);
            AssertRelative(2700 * 0.025 * 0.003 * Gravity.Standard, w.Intensity, Exact, "ρAg");
        }

        [Test]
        public void InvalidInputIsRejected()
        {
            var outside = Model(BeamSupports.SimplySupported());
            outside.PointLoads.Add(new PointLoad(L + 0.1, 100));
            Assert.Throws<InvalidOperationException>(() => BeamSolver.Solve(outside));

            var badSupports = Model(BeamSupports.Overhang(1.5, 0.5));
            Assert.Throws<InvalidOperationException>(() => BeamSolver.Solve(badSupports));

            var s = BeamSolver.Solve(Model(BeamSupports.Cantilever()));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Deflection(-0.1));
            Assert.That(s.PeakDeflection.Value, Is.EqualTo(0), "unloaded beam");
        }

        static bool NearKeyPoint(BeamSolution s, double x, double distance)
        {
            foreach (var k in s.KeyPoints)
                if (Math.Abs(k - x) < distance) return true;
            return false;
        }
    }
}
