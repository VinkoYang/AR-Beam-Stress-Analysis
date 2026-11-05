using System;
using System.Collections.Generic;
using System.Globalization;
using ARBeam.Mechanics;

namespace HoloMech
{
    /// <summary>
    /// One record of the on-site beam: the complete inputs, its pose and the result of solving them. Written as a row of
    /// site_snapshots.csv and as a JSON file that can be loaded and solved again. The loads are simulated, the pose is the
    /// beam's left end in world coordinates, and world coordinates are only comparable within one session (the origin
    /// moves with each start); the spatial anchor is what links sessions.
    /// </summary>
    [Serializable]
    public class SiteSnapshot
    {
        public const string TriggerManual = "manual", TriggerAlign = "align", TriggerAnchorSave = "anchor_save";

        public const string CsvHeader =
            "time_utc,trigger,preset,length_mm,section_type,h_mm,bf_mm,tf_mm,tw_mm,box_w_mm,box_h_mm,box_wall_mm,pipe_od_mm,pipe_wall_mm," +
            "E_GPa,I_mm4,supports,point_loads,udls,pos_x,pos_y,pos_z,yaw_deg,anchor_uuid," +
            "sigma_max_MPa,sigma_max_x_m,v_max_mm,v_max_x_m";

        public string timeUtc;
        public string trigger;
        public string anchorUuid;
        public float posX, posY, posZ, yawDeg;
        public HoloMechInputs inputs;

        public static SiteSnapshot Create(HoloMechInputs inputs, string trigger, DateTime timeUtc, float posX, float posY, float posZ, float yawDeg, string anchorUuid)
        {
            return new SiteSnapshot
            {
                timeUtc = timeUtc.ToString("o", CultureInfo.InvariantCulture),
                trigger = trigger,
                anchorUuid = anchorUuid ?? "",
                posX = posX,
                posY = posY,
                posZ = posZ,
                yawDeg = yawDeg,
                inputs = inputs.Clone(),
            };
        }

        /// <summary>A row for <see cref="CsvHeader"/>: sections that do not apply and results of an unsolvable beam stay empty.</summary>
        public string ToCsvRow()
        {
            var i = inputs;
            bool iBeam = i.section == SectionType.IBeam, box = i.section == SectionType.Box, pipe = i.section == SectionType.Pipe;
            BeamSolution solution = null;
            double momentOfInertia = 0;
            try
            {
                momentOfInertia = i.SectionProperties().I;
                if (!i.TrySolve(out solution, out _)) solution = null;
            }
            catch (ArgumentException)
            {
                solution = null; // the file still records the inputs that could not be solved
            }

            return string.Join(",",
                timeUtc, trigger, i.preset.ToString(), Mm(i.lengthCm), i.section.ToString(),
                iBeam ? Mm(i.iBeam.heightCm) : "", iBeam ? Mm(i.iBeam.flangeWidthCm) : "", iBeam ? Mm(i.iBeam.flangeThicknessCm) : "", iBeam ? Mm(i.iBeam.webThicknessCm) : "",
                box ? Mm(i.box.widthCm) : "", box ? Mm(i.box.heightCm) : "", box ? Mm(i.box.wallCm) : "",
                pipe ? Mm(i.pipe.outerDiameterCm) : "", pipe ? Mm(i.pipe.wallCm) : "",
                F(i.elasticModulusGPa, "F2"), momentOfInertia > 0 ? F(momentOfInertia * 1e12, "F1") : "",
                SupportsColumn(i), PointLoadsColumn(i), UniformLoadsColumn(i),
                F(posX, "F4"), F(posY, "F4"), F(posZ, "F4"), F(yawDeg, "F2"), anchorUuid,
                solution != null ? F(solution.MaxBendingStress / 1e6, "F3") : "",
                solution != null ? F(solution.PeakMoment.Position, "F3") : "",
                solution != null ? F(solution.PeakDeflection.Value * 1e3, "F4") : "",
                solution != null ? F(solution.PeakDeflection.Position, "F3") : "");
        }

        /// <summary>kind@x_m; ...</summary>
        static string SupportsColumn(HoloMechInputs i)
        {
            var parts = new List<string>();
            foreach (var s in i.supports) parts.Add($"{s.kind}@{F(s.PositionOn(i.lengthCm) / 100.0, "F4")}");
            return string.Join(";", parts);
        }

        /// <summary>P_N@x_m; ... with the solver's sign (downward positive); loads switched off are left out.</summary>
        static string PointLoadsColumn(HoloMechInputs i)
        {
            var parts = new List<string>();
            foreach (var p in i.pointLoads)
                if (p.enabled && p.forceN != 0)
                    parts.Add($"{F(HoloMechInputs.Signed(p.forceN, p.direction), "F1")}@{F(p.positionCm / 100.0, "F4")}");
            return string.Join(";", parts);
        }

        /// <summary>w_Npm@a_m-b_m; ... with the solver's sign (downward positive); loads switched off are left out.</summary>
        static string UniformLoadsColumn(HoloMechInputs i)
        {
            var parts = new List<string>();
            foreach (var w in i.uniformLoads)
                if (w.enabled && w.intensityNPerM != 0)
                    parts.Add($"{F(HoloMechInputs.Signed(w.intensityNPerM, w.direction), "F1")}@{F(w.startCm / 100.0, "F4")}-{F(w.endCm / 100.0, "F4")}");
            return string.Join(";", parts);
        }

        static string Mm(float cm) => F(cm * 10.0, "F1");

        static string F(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);
    }
}
