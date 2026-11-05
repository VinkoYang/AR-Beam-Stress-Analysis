using System;
using System.Globalization;
using System.Threading;
using ARBeam.Mechanics;
using NUnit.Framework;
using UnityEngine;

namespace HoloMech.Tests
{
    /// <summary>site_snapshots.csv rows and the JSON round trip.</summary>
    public class SiteSnapshotTests
    {
        static readonly DateTime Time = new DateTime(2026, 11, 5, 18, 30, 15, 250, DateTimeKind.Utc);

        static readonly string[] Columns = SiteSnapshot.CsvHeader.Split(',');

        static string Cell(string[] row, string column) => row[Array.IndexOf(Columns, column)];

        static SiteSnapshot Snapshot(HoloMechInputs i, string trigger = SiteSnapshot.TriggerManual, string uuid = "")
            => SiteSnapshot.Create(i, trigger, Time, 1.25f, 0.9f, -2.5f, 37.5f, uuid);

        static HoloMechInputs Gantry()
        {
            var i = new HoloMechInputs { lengthCm = 300f };
            i.ApplyGantryPreset();
            SiteAdjust.StepGantrySupport(i, false, 200f);
            SiteAdjust.StepGantrySupport(i, true, -200f);
            return i;
        }

        [Test]
        public void HeaderHasTheAgreedColumns()
        {
            Assert.That(Columns, Is.EqualTo(new[]
            {
                "time_utc", "trigger", "preset", "length_mm", "section_type", "h_mm", "bf_mm", "tf_mm", "tw_mm", "box_w_mm", "box_h_mm",
                "box_wall_mm", "pipe_od_mm", "pipe_wall_mm", "E_GPa", "I_mm4", "supports", "point_loads", "udls", "pos_x", "pos_y", "pos_z",
                "yaw_deg", "anchor_uuid", "sigma_max_MPa", "sigma_max_x_m", "v_max_mm", "v_max_x_m",
            }));
        }

        [Test]
        public void RowHoldsTheGantryValues()
        {
            var i = Gantry();
            string[] row = Snapshot(i, SiteSnapshot.TriggerAnchorSave, "abc-123").ToCsvRow().Split(',');
            Assert.That(row.Length, Is.EqualTo(Columns.Length));

            Assert.That(Cell(row, "time_utc"), Does.StartWith("2026-11-05T18:30:15.25"));
            Assert.That(Cell(row, "trigger"), Is.EqualTo("anchor_save"));
            Assert.That(Cell(row, "preset"), Is.EqualTo("Gantry"));
            Assert.That(Cell(row, "length_mm"), Is.EqualTo("3000.0"));
            Assert.That(Cell(row, "section_type"), Is.EqualTo("IBeam"));
            Assert.That(Cell(row, "h_mm"), Is.EqualTo("325.0"));
            Assert.That(Cell(row, "E_GPa"), Is.EqualTo("68.90"));
            Assert.That(Cell(row, "supports"), Is.EqualTo("Pin@0.2000;Pin@2.8000"));
            Assert.That(Cell(row, "point_loads"), Is.EqualTo("1000.0@1.5000"));
            Assert.That(Cell(row, "udls"), Is.Empty);
            Assert.That(Cell(row, "pos_x"), Is.EqualTo("1.2500"));
            Assert.That(Cell(row, "yaw_deg"), Is.EqualTo("37.50"));
            Assert.That(Cell(row, "anchor_uuid"), Is.EqualTo("abc-123"));
        }

        [Test]
        public void ResultsComeFromTheSolver()
        {
            var i = Gantry();
            Assert.That(i.TrySolve(out var s, out var error), Is.True, error);
            string[] row = Snapshot(i).ToCsvRow().Split(',');
            var c = CultureInfo.InvariantCulture;
            Assert.That(double.Parse(Cell(row, "sigma_max_MPa"), c), Is.EqualTo(s.MaxBendingStress / 1e6).Within(1e-3));
            Assert.That(double.Parse(Cell(row, "sigma_max_x_m"), c), Is.EqualTo(s.PeakMoment.Position).Within(1e-3));
            Assert.That(double.Parse(Cell(row, "v_max_mm"), c), Is.EqualTo(s.PeakDeflection.Value * 1e3).Within(1e-4));
            Assert.That(double.Parse(Cell(row, "v_max_x_m"), c), Is.EqualTo(s.PeakDeflection.Position).Within(1e-3));
            Assert.That(double.Parse(Cell(row, "I_mm4"), c), Is.EqualTo(i.SectionProperties().I * 1e12).Within(0.1));
        }

        [Test]
        public void ColumnsOfOtherSectionsStayEmpty()
        {
            var box = new HoloMechInputs { section = SectionType.Box };
            string[] row = Snapshot(box).ToCsvRow().Split(',');
            Assert.That(Cell(row, "box_w_mm"), Is.EqualTo("155.0"));
            Assert.That(Cell(row, "box_h_mm"), Is.EqualTo("205.0"));
            Assert.That(Cell(row, "box_wall_mm"), Is.EqualTo("25.0"));
            foreach (var column in new[] { "h_mm", "bf_mm", "tf_mm", "tw_mm", "pipe_od_mm", "pipe_wall_mm" })
                Assert.That(Cell(row, column), Is.Empty, column);

            var pipe = new HoloMechInputs { section = SectionType.Pipe };
            row = Snapshot(pipe).ToCsvRow().Split(',');
            Assert.That(Cell(row, "pipe_od_mm"), Is.EqualTo("160.0"));
            Assert.That(Cell(row, "pipe_wall_mm"), Is.EqualTo("26.0"));
            Assert.That(Cell(row, "box_w_mm"), Is.Empty);
        }

        [Test]
        public void LoadColumnsUseTheSolverSignAndSkipLoadsThatAreOff()
        {
            var i = new HoloMechInputs();
            i.pointLoads.Clear();
            i.pointLoads.Add(new PointLoadInput { positionCm = 50, forceN = 2000, direction = LoadDirection.Down });
            i.pointLoads.Add(new PointLoadInput { positionCm = 100, forceN = 500, direction = LoadDirection.Up });
            i.pointLoads.Add(new PointLoadInput { enabled = false, positionCm = 150, forceN = 700 });
            i.uniformLoads.Clear();
            i.uniformLoads.Add(new UniformLoadInput { enabled = true, startCm = 10, endCm = 60, intensityNPerM = 300 });
            i.uniformLoads.Add(new UniformLoadInput { enabled = false });
            string[] row = Snapshot(i).ToCsvRow().Split(',');
            Assert.That(Cell(row, "point_loads"), Is.EqualTo("2000.0@0.5000;-500.0@1.0000"));
            Assert.That(Cell(row, "udls"), Is.EqualTo("300.0@0.1000-0.6000"));
        }

        [Test]
        public void UnsolvableBeamKeepsItsInputsAndLeavesResultsEmpty()
        {
            var i = new HoloMechInputs();
            i.supports.Clear();
            string[] row = Snapshot(i).ToCsvRow().Split(',');
            Assert.That(row.Length, Is.EqualTo(Columns.Length));
            Assert.That(Cell(row, "length_mm"), Is.EqualTo("2000.0"));
            foreach (var column in new[] { "sigma_max_MPa", "sigma_max_x_m", "v_max_mm", "v_max_x_m" })
                Assert.That(Cell(row, column), Is.Empty, column);
        }

        [Test]
        public void NumbersIgnoreTheSystemCulture()
        {
            var culture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                string[] row = Snapshot(Gantry()).ToCsvRow().Split(',');
                Assert.That(row.Length, Is.EqualTo(Columns.Length), "a decimal comma would add columns");
                Assert.That(Cell(row, "supports"), Is.EqualTo("Pin@0.2000;Pin@2.8000"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = culture;
            }
        }

        [Test]
        public void SnapshotDoesNotFollowLaterChanges()
        {
            var i = Gantry();
            var snapshot = Snapshot(i);
            i.SetLength(400f);
            Assert.That(snapshot.inputs.lengthCm, Is.EqualTo(300f));
        }

        [TestCase(SectionType.IBeam)]
        [TestCase(SectionType.Box)]
        [TestCase(SectionType.Pipe)]
        public void JsonRoundTripSolvesToTheSameResult(SectionType section)
        {
            var i = Gantry();
            i.section = section;
            i.uniformLoads.Add(new UniformLoadInput { enabled = true, startCm = 40, endCm = 220, intensityNPerM = 750, direction = LoadDirection.Down });
            i.pointLoads.Add(new PointLoadInput { positionCm = 77.7f, forceN = 321.5f, direction = LoadDirection.Up });
            i.iBeam.heightCm = 12.3f;
            i.iBeam.flangeThicknessCm = 0.7f;
            i.iBeam.flangeWidthCm = 8.1f;
            i.iBeam.webThicknessCm = 0.4f;
            i.elasticModulusGPa = 68.9f;
            Assert.That(i.TrySolve(out var before, out var error), Is.True, error);

            string json = JsonUtility.ToJson(Snapshot(i, SiteSnapshot.TriggerAnchorSave, "uuid-1"), true);
            var loaded = JsonUtility.FromJson<SiteSnapshot>(json);

            Assert.That(loaded.trigger, Is.EqualTo(SiteSnapshot.TriggerAnchorSave));
            Assert.That(loaded.anchorUuid, Is.EqualTo("uuid-1"));
            Assert.That(loaded.yawDeg, Is.EqualTo(37.5f));
            Assert.That(loaded.inputs.preset, Is.EqualTo(SupportPreset.Gantry));
            Assert.That(loaded.inputs.IsGantry, Is.True);
            Assert.That(loaded.inputs.material, Is.EqualTo(MaterialPreset.Aluminum));

            Assert.That(loaded.inputs.TrySolve(out var after, out error), Is.True, error);
            Assert.That(after.MaxBendingStress, Is.EqualTo(before.MaxBendingStress), "σmax");
            Assert.That(after.PeakDeflection.Value, Is.EqualTo(before.PeakDeflection.Value), "vmax");
            Assert.That(after.PeakDeflection.Position, Is.EqualTo(before.PeakDeflection.Position), "x of vmax");
            Assert.That(loaded.ToCsvRow(), Is.EqualTo(Snapshot(i, SiteSnapshot.TriggerAnchorSave, "uuid-1").ToCsvRow()), "same CSV row");
        }

        [Test]
        public void JsonKeepsARightEndSupportOnTheEnd()
        {
            var i = new HoloMechInputs { lengthCm = 300f };
            i.ApplyGantryPreset();
            var loaded = JsonUtility.FromJson<SiteSnapshot>(JsonUtility.ToJson(Snapshot(i)));
            loaded.inputs.SetLength(350f);
            Assert.That(loaded.inputs.supports[1].PositionOn(350f), Is.EqualTo(350f));
        }

        [Test]
        public void SavedPlacementRestoresIntoTheInputsThePanelsHold()
        {
            // anchors.json is a snapshot; a restore copies its inputs into the object the Workbench already holds.
            var saved = Gantry();
            string json = JsonUtility.ToJson(Snapshot(saved, SiteSnapshot.TriggerAnchorSave, "uuid-2"));

            var live = new HoloMechInputs();
            var pointLoads = live.pointLoads;
            live.CopyFrom(JsonUtility.FromJson<SiteSnapshot>(json).inputs);

            Assert.That(live.IsGantry, Is.True);
            Assert.That(live.lengthCm, Is.EqualTo(300f));
            Assert.That(live.section, Is.EqualTo(SectionType.IBeam));
            Assert.That(live.pointLoads, Is.Not.SameAs(pointLoads));
            Assert.That(SiteAdjust.GantrySupportMm(live, true), Is.EqualTo(2800f).Within(1e-3f));
            Assert.That(live.TrySolve(out var a, out var error), Is.True, error);
            Assert.That(saved.TrySolve(out var b, out error), Is.True, error);
            Assert.That(a.MaxBendingStress, Is.EqualTo(b.MaxBendingStress));
        }

        [Test]
        public void CopyFromDoesNotShareLists()
        {
            var source = Gantry();
            var live = new HoloMechInputs();
            live.CopyFrom(source);
            source.supports.Clear();
            source.pointLoads[0].forceN = 1f;
            source.iBeam.heightCm = 7f;
            Assert.That(live.supports.Count, Is.EqualTo(2));
            Assert.That(live.pointLoads[0].forceN, Is.EqualTo(1000f));
            Assert.That(live.iBeam.heightCm, Is.EqualTo(32.5f));
        }

        [Test]
        public void JsonKeepsEmptyLoadLists()
        {
            var i = new HoloMechInputs();
            i.pointLoads.Clear();
            i.uniformLoads.Clear();
            var loaded = JsonUtility.FromJson<SiteSnapshot>(JsonUtility.ToJson(Snapshot(i)));
            Assert.That(loaded.inputs.pointLoads, Is.Empty, "not the default load");
            Assert.That(loaded.inputs.uniformLoads, Is.Empty);
        }
    }
}
