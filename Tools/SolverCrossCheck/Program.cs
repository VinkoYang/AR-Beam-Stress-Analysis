// Runs ARBeam.Mechanics.BeamSolver (the solver the headset app uses) and the independent stiffness-method
// reference on every case in cases.json and writes csharp_results.json for crosscheck.py.
//
//   dotnet run -c Release -- cases.json csharp_results.json

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using ARBeam.Mechanics;
using ARBeam.Mechanics.Tests;

static class Program
{
    static int Main(string[] args)
    {
        string casesPath = args.Length > 0 ? args[0] : "cases.json";
        string outPath = args.Length > 1 ? args[1] : "csharp_results.json";

        var root = JsonNode.Parse(File.ReadAllText(casesPath));
        double e = root["E"].GetValue<double>();
        double i = root["I"].GetValue<double>();
        // Area and extreme-fibre distance do not enter V, M, θ, v; any positive values will do.
        var section = new BeamSection(1e-3, i, 0.1);

        var results = new JsonArray();
        foreach (var c in root["cases"].AsArray())
        {
            string id = c["id"].GetValue<string>();
            double length = c["L"].GetValue<double>();
            var supports = c["supports"].AsArray()
                .Select(s => new Support(s["x"].GetValue<double>(),
                    s["kind"].GetValue<string>() == "fixed" ? SupportKind.Fixed : SupportKind.Pin))
                .ToArray();

            var model = new BeamModel(length, section, e, BeamSupports.Custom(supports));
            foreach (var p in c["point_loads"].AsArray())
                model.PointLoads.Add(new PointLoad(p["x"].GetValue<double>(), p["P"].GetValue<double>()));
            foreach (var w in c["udls"].AsArray())
                model.UniformLoads.Add(new UniformLoad(w["a"].GetValue<double>(), w["b"].GetValue<double>(),
                    w["w"].GetValue<double>()));

            var sol = BeamSolver.Solve(model);
            var stiff = StiffnessReferenceSolver.Solve(model, 400);

            // Nodal comparison: the stiffness method is exact at its nodes for these loads.
            double maxW = 0, maxDiff = 0;
            for (int n = 0; n < stiff.NodeX.Length; n++)
            {
                double ours = sol.Deflection(stiff.NodeX[n]);
                maxW = Math.Max(maxW, Math.Abs(ours));
                maxDiff = Math.Max(maxDiff, Math.Abs(ours - stiff.W[n]));
            }

            // Solve time on this PC (not the headset): median of 2000 solves after warm-up.
            for (int k = 0; k < 200; k++) BeamSolver.Solve(model);
            var times = new double[2000];
            var sw = new System.Diagnostics.Stopwatch();
            for (int k = 0; k < times.Length; k++)
            {
                sw.Restart();
                BeamSolver.Solve(model);
                sw.Stop();
                times[k] = sw.Elapsed.TotalMilliseconds * 1000.0;
            }
            Array.Sort(times);

            results.Add(new JsonObject
            {
                ["id"] = id,
                ["reactions"] = Reactions(sol.Reactions),
                ["peak_moment"] = new JsonObject { ["x"] = sol.PeakMoment.Position, ["value"] = sol.PeakMoment.Value },
                ["peak_deflection"] = new JsonObject
                    { ["x"] = sol.PeakDeflection.Position, ["value"] = sol.PeakDeflection.Value },
                ["stiffness"] = new JsonObject
                {
                    ["reactions"] = Reactions(stiff.Reactions),
                    ["nodes"] = stiff.NodeX.Length,
                    ["max_abs_nodal_deflection"] = maxW,
                    ["max_nodal_deflection_diff"] = maxDiff,
                },
                ["solve_us_median_pc"] = times[times.Length / 2],
            });
            Console.WriteLine($"{id}: M_peak={sol.PeakMoment}  v_peak={sol.PeakDeflection}  " +
                              $"stiffness nodal diff={maxDiff:E2} m  solve={times[times.Length / 2]:F1} us");
        }

        var output = new JsonObject
        {
            ["solver"] = "ARBeam.Mechanics.BeamSolver (generalised Macaulay, closed form)",
            ["reference"] = "StiffnessReferenceSolver (Hermite cubic elements, >= 400 elements)",
            ["results"] = results,
        };
        File.WriteAllText(outPath, output.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Wrote {outPath}");
        return 0;
    }

    static JsonArray Reactions(IEnumerable<Reaction> reactions)
    {
        var a = new JsonArray();
        foreach (var r in reactions)
            a.Add(new JsonObject { ["x"] = r.Position, ["force"] = r.Force, ["moment"] = r.Moment });
        return a;
    }
}
