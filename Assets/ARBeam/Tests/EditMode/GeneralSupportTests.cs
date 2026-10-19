using System;
using System.Collections.Generic;
using NUnit.Framework;
using static ARBeam.Mechanics.Tests.TestSupport;

namespace ARBeam.Mechanics.Tests
{
    /// <summary>
    /// Any combination of Free / Pinned / Fixed ends (HoloMech's boundary panel), the acceptance cases of the
    /// HoloMech reproduction spec §3.5 (tolerance 0.5 %), stability checks, and a cross-check against an
    /// independent stiffness-method solver.
    /// </summary>
    public class GeneralSupportTests
    {
        const double Spec = 0.005;

        static readonly EndCondition[] Conditions = { EndCondition.Free, EndCondition.Pinned, EndCondition.Fixed };

        static BeamModel Ends(EndCondition left, EndCondition right) => Model(BeamSupports.Ends(left, right));

        // ---- Spec §3.5 acceptance cases, built through the per-end API ----

        [Test]
        public void Spec_Cantilever_TipLoad()
        {
            var m = Ends(EndCondition.Fixed, EndCondition.Free);
            m.PointLoads.Add(new PointLoad(L, P));
            var s = BeamSolver.Solve(m);
            AssertRelative(-P * L * L * L / (3 * EI), s.Deflection(L), Spec, "tip deflection PL³/3EI");
            AssertRelative(P * L, Math.Abs(s.Moment(0)), Spec, "fixed-end moment PL");
        }

        [Test]
        public void Spec_Cantilever_FixedOnTheRight()
        {
            var m = Ends(EndCondition.Free, EndCondition.Fixed);
            m.PointLoads.Add(new PointLoad(0, P));
            var s = BeamSolver.Solve(m);
            AssertRelative(-P * L * L * L / (3 * EI), s.Deflection(0), Spec, "tip deflection PL³/3EI");
            AssertRelative(P * L, Math.Abs(s.Moment(L)), Spec, "fixed-end moment PL");
            AssertRelative(P, s.Reactions[0].Force, Exact, "R");
        }

        [Test]
        public void Spec_SimplySupported_CentralLoad()
        {
            var m = Ends(EndCondition.Pinned, EndCondition.Pinned);
            m.PointLoads.Add(new PointLoad(L / 2, P));
            var s = BeamSolver.Solve(m);
            AssertRelative(-P * L * L * L / (48 * EI), s.Deflection(L / 2), Spec, "PL³/48EI");
            AssertRelative(P * L / 4, s.Moment(L / 2), Spec, "PL/4");
        }

        [Test]
        public void Spec_SimplySupported_UniformLoad()
        {
            var m = Ends(EndCondition.Pinned, EndCondition.Pinned);
            m.UniformLoads.Add(new UniformLoad(0, L, W));
            var s = BeamSolver.Solve(m);
            AssertRelative(-5 * W * Math.Pow(L, 4) / (384 * EI), s.Deflection(L / 2), Spec, "5wL⁴/384EI");
            AssertRelative(W * L * L / 8, s.Moment(L / 2), Spec, "wL²/8");
        }

        [Test]
        public void Spec_FixedFixed_CentralLoad()
        {
            var m = Ends(EndCondition.Fixed, EndCondition.Fixed);
            m.PointLoads.Add(new PointLoad(L / 2, P));
            var s = BeamSolver.Solve(m);
            AssertRelative(-P * L * L * L / (192 * EI), s.Deflection(L / 2), Spec, "PL³/192EI");
            AssertRelative(P * L / 8, Math.Abs(s.Moment(0)), Spec, "end moment PL/8");
            AssertRelative(P * L / 8, Math.Abs(s.Moment(L)), Spec, "end moment PL/8");
        }

        [Test]
        public void Spec_Propped_UniformLoad([Values] bool clampOnLeft)
        {
            var m = clampOnLeft ? Ends(EndCondition.Fixed, EndCondition.Pinned) : Ends(EndCondition.Pinned, EndCondition.Fixed);
            m.UniformLoads.Add(new UniformLoad(0, L, W));
            var s = BeamSolver.Solve(m);
            var pin = clampOnLeft ? s.Reactions[1] : s.Reactions[0];
            var clamp = clampOnLeft ? s.Reactions[0] : s.Reactions[1];
            AssertRelative(3 * W * L / 8, pin.Force, Spec, "pinned-end reaction 3wL/8");
            AssertRelative(5 * W * L / 8, clamp.Force, Spec, "fixed-end reaction 5wL/8");
            AssertRelative(W * L * L / 8, Math.Abs(clamp.Moment), Spec, "fixed-end moment wL²/8");
        }

        [Test]
        public void Spec_SimplySupported_ReactionsForAnyLoadPosition([Values(0.0, 0.3, 0.77, 1.4, 2.0)] double a)
        {
            var m = Ends(EndCondition.Pinned, EndCondition.Pinned);
            m.PointLoads.Add(new PointLoad(a, P));
            var s = BeamSolver.Solve(m);
            // Compared against P rather than the (possibly zero) expected value.
            AssertNearZero(s.Reactions[0].Force - P * (L - a) / L, P, "P(L−a)/L", Spec);
            AssertNearZero(s.Reactions[1].Force - P * a / L, P, "Pa/L", Spec);
        }

        // ---- Stability ----

        [Test]
        public void Stability_AllNineEndCombinations()
        {
            foreach (var left in Conditions)
            {
                foreach (var right in Conditions)
                {
                    bool mechanism = left == EndCondition.Free && right != EndCondition.Fixed
                                     || right == EndCondition.Free && left != EndCondition.Fixed;
                    Assert.That(BeamSolver.IsStable(BeamSupports.Ends(left, right)), Is.EqualTo(!mechanism), $"{left}–{right}");

                    var m = Ends(left, right);
                    m.PointLoads.Add(new PointLoad(0.7, P));
                    bool solved = BeamSolver.TrySolve(m, out var s, out var error);
                    Assert.That(solved, Is.EqualTo(!mechanism), $"{left}–{right}: {error}");
                    if (mechanism)
                    {
                        Assert.That(s, Is.Null);
                        Assert.That(error, Does.Contain("Unstable"));
                        Assert.Throws<UnstableBeamException>(() => BeamSolver.Solve(m));
                    }
                }
            }
        }

        [Test]
        public void Stability_MechanismIsReportedEvenWithoutLoad()
        {
            Assert.Throws<UnstableBeamException>(() => BeamSolver.Solve(Ends(EndCondition.Free, EndCondition.Free)));
            Assert.Throws<UnstableBeamException>(() => BeamSolver.Solve(Ends(EndCondition.Pinned, EndCondition.Free)));
            Assert.Throws<UnstableBeamException>(() => BeamSolver.Solve(Ends(EndCondition.Free, EndCondition.Pinned)));
        }

        // ---- Beyond two ends ----

        [Test]
        public void TwoSpanContinuousBeam_UniformLoad()
        {
            // Supports at 0, L/2, L; each span l = L/2. Classic result: R_end = 3wl/8, R_mid = 10wl/8, M_mid = −wl²/8.
            double l = L / 2;
            var m = Model(BeamSupports.Custom(new Support(0, SupportKind.Pin), new Support(l, SupportKind.Pin),
                new Support(L, SupportKind.Pin)));
            m.UniformLoads.Add(new UniformLoad(0, L, W));
            var s = BeamSolver.Solve(m);
            AssertRelative(3 * W * l / 8, s.Reactions[0].Force, Exact, "R_end");
            AssertRelative(10 * W * l / 8, s.Reactions[1].Force, Exact, "R_mid");
            AssertRelative(3 * W * l / 8, s.Reactions[2].Force, Exact, "R_end");
            AssertRelative(-W * l * l / 8, s.Moment(l), Exact, "M over the middle support");
        }

        // ---- Independent cross-check ----

        [Test]
        public void MatchesStiffnessMethod_AllStableCombinations([Values(1, 2, 3, 4)] int seed)
        {
            foreach (var left in Conditions)
            {
                foreach (var right in Conditions)
                {
                    if (!BeamSolver.IsStable(BeamSupports.Ends(left, right))) continue;
                    var m = RandomLoads(Ends(left, right), seed);
                    CompareWithStiffness(m, $"{left}–{right} seed {seed}");
                }
            }
        }

        [Test]
        public void MatchesStiffnessMethod_OverhangAndContinuous()
        {
            CompareWithStiffness(RandomLoads(Model(BeamSupports.Overhang(0.4, 1.5)), 7), "overhang");
            CompareWithStiffness(RandomLoads(Model(BeamSupports.Custom(new Support(0, SupportKind.Fixed),
                new Support(1.1, SupportKind.Pin), new Support(L, SupportKind.Pin))), 8), "fixed + 2 pins");
        }

        [Test]
        public void SampleMatchesPointQueries()
        {
            var m = RandomLoads(Ends(EndCondition.Fixed, EndCondition.Pinned), 3);
            var s = BeamSolver.Solve(m);
            var r = s.Sample(8);
            Assert.That(r.Count, Is.EqualTo((s.KeyPoints.Count - 1) * 9));
            for (int i = 0; i < r.Count; i++)
            {
                AssertNearZero(r.Deflection[i] - s.Deflection(r.X[i]), Math.Abs(s.PeakDeflection.Value), $"w at {r.X[i]}");
                Assert.That(r.MaxStress[i], Is.EqualTo(Math.Abs(r.Moment[i]) * m.Section.C / m.Section.I).Within(1e-6));
                if (i > 0) Assert.That(r.X[i], Is.GreaterThanOrEqualTo(r.X[i - 1]));
            }

            Assert.That(r.PeakMoment.Value, Is.EqualTo(s.PeakMoment.Value));
            Assert.That(r.Reactions.Count, Is.EqualTo(2));
        }

        static BeamModel RandomLoads(BeamModel m, int seed)
        {
            var rng = new Random(seed);
            m.PointLoads.Add(new PointLoad(rng.NextDouble() * L, (rng.NextDouble() - 0.3) * 2 * P));
            m.PointLoads.Add(new PointLoad(rng.NextDouble() * L, (rng.NextDouble() - 0.3) * 2 * P));
            double s0 = rng.NextDouble() * 0.5 * L;
            m.UniformLoads.Add(new UniformLoad(s0, s0 + 0.3 + rng.NextDouble() * (L - s0 - 0.3), (rng.NextDouble() - 0.3) * 2 * W));
            return m;
        }

        static void CompareWithStiffness(BeamModel m, string label)
        {
            var exact = BeamSolver.Solve(m);
            var fe = StiffnessReferenceSolver.Solve(m);

            double wScale = Math.Abs(exact.PeakDeflection.Value), tScale = 0, fScale = 0;
            foreach (var p in m.PointLoads) fScale += Math.Abs(p.Force);
            foreach (var u in m.UniformLoads) fScale += Math.Abs(u.Resultant);
            for (int i = 0; i < fe.NodeX.Length; i++) tScale = Math.Max(tScale, Math.Abs(fe.Theta[i]));

            // The stiffness matrix of ~200 elements has a condition number of order (L/l)^4 ~ 1e9, so the
            // reference itself carries ~1e-7 relative round-off. 1e-6 still catches any real disagreement.
            const double Agree = 1e-6;
            for (int i = 0; i < fe.NodeX.Length; i++)
            {
                double x = fe.NodeX[i];
                AssertNearZero(fe.W[i] - exact.Deflection(x), wScale, $"{label}: w at x={x:F4}", Agree);
                AssertNearZero(fe.Theta[i] - exact.Slope(x), tScale, $"{label}: θ at x={x:F4}", Agree);
            }

            Assert.That(fe.Reactions.Count, Is.EqualTo(exact.Reactions.Count), label);
            for (int i = 0; i < fe.Reactions.Count; i++)
            {
                AssertNearZero(fe.Reactions[i].Force - exact.Reactions[i].Force, fScale, $"{label}: R{i}", Agree);
                AssertNearZero(fe.Reactions[i].Moment - exact.Reactions[i].Moment, fScale * L, $"{label}: M{i}", Agree);
            }
        }
    }
}
