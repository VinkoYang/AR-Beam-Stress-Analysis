using System;
using System.Collections.Generic;
using ARBeam.Mechanics;

namespace HoloMech
{
    public enum SectionType
    {
        Pipe,
        Box,
        IBeam,
    }

    public enum MaterialPreset
    {
        Steel,
        Concrete,
        Wood,
        Custom,

        // After Custom because the scenes store this enum as a number (material: 3 = Custom). The buttons list it before Custom.
        Aluminum,
    }

    /// <summary>The Workbench preset last pressed; adding, deleting or retyping a support goes back to Custom.</summary>
    public enum SupportPreset
    {
        Custom,
        Cantilever,
        Simple,
        FixedFixed,
        TwoSpans,
        Gantry,
    }

    public enum LoadDirection
    {
        Up,
        Down,
    }

    /// <summary>Slider ranges and steps from the reproduction spec (§2.1–2.3). UI units: cm, N, N/m, GPa.</summary>
    public static class HoloMechRanges
    {
        public const float LengthMinCm = 50f, LengthMaxCm = 500f;

        /// <summary>Upper length limit in on-site mode only (a gantry can be longer than 5 m); the sliders stay at <see cref="LengthMaxCm"/>.</summary>
        public const float SiteLengthMaxCm = 1000f;

        public const float ModulusMinGPa = 1f, ModulusMaxGPa = 250f;
        public const float ForceMaxN = 10000f;

        /// <summary>Deflection scale slider range (log scale), spec §2.4.</summary>
        public const float ScaleMin = 1f, ScaleMax = 100000f;
        public const float UniformMaxNPerM = 10000f;

        public const float PipeDiameterMinCm = 2f, PipeDiameterMaxCm = 30f;
        public const float PipeWallMinCm = 0.2f, PipeWallMaxCm = 5f;

        public const float BoxWidthMinCm = 1f, BoxWidthMaxCm = 30f;
        public const float BoxHeightMinCm = 1f, BoxHeightMaxCm = 40f;
        public const float BoxWallMinCm = 0f, BoxWallMaxCm = 5f;

        public const float IHeightMinCm = 5f, IHeightMaxCm = 60f;
        public const float IFlangeWidthMinCm = 3f, IFlangeWidthMaxCm = 30f;
        public const float IFlangeThicknessMinCm = 0.3f, IFlangeThicknessMaxCm = 4f;
        public const float IWebThicknessMinCm = 0.2f, IWebThicknessMaxCm = 3f;

        /// <summary>Preset Young's moduli, GPa (spec §2.1).</summary>
        public static float PresetModulusGPa(MaterialPreset preset)
        {
            switch (preset)
            {
                case MaterialPreset.Steel: return 200f;
                case MaterialPreset.Concrete: return 30f;
                case MaterialPreset.Wood: return 11f;
                case MaterialPreset.Aluminum: return (float)(BeamMaterial.Aluminum6061T6.ElasticModulus / 1e9);
                default: return float.NaN;
            }
        }
    }

    [Serializable]
    public class PipeDimensions
    {
        public float outerDiameterCm = 16f;
        public float wallCm = 2.6f;
    }

    [Serializable]
    public class BoxDimensions
    {
        public float widthCm = 15.5f;
        public float heightCm = 20.5f;
        /// <summary>Wall thickness; 0 = solid.</summary>
        public float wallCm = 2.5f;
    }

    [Serializable]
    public class IBeamDimensions
    {
        public float heightCm = 32.5f;
        public float flangeWidthCm = 16.5f;
        public float flangeThicknessCm = 2.15f;
        public float webThicknessCm = 1.6f;
    }

    [Serializable]
    public class PointLoadInput
    {
        public bool enabled = true;
        public float positionCm = 100f;
        public float forceN = 2000f;
        public LoadDirection direction = LoadDirection.Down;
    }

    [Serializable]
    public class UniformLoadInput
    {
        public bool enabled;
        public float startCm = 100f;
        public float endCm = 150f;
        public float intensityNPerM = 2000f;
        public LoadDirection direction = LoadDirection.Down;
    }

    /// <summary>
    /// One support along the beam: a pin (or roller) or a clamp. <see cref="atRightEnd"/> keeps a support on the right
    /// end when the length changes; otherwise <see cref="positionCm"/> is measured from the left end.
    /// </summary>
    [Serializable]
    public class SupportInput
    {
        public SupportKind kind = SupportKind.Pin;
        public float positionCm;
        public bool atRightEnd;

        /// <summary>Position on a beam of length <paramref name="lengthCm"/>.</summary>
        public float PositionOn(float lengthCm) => atRightEnd ? lengthCm : Math.Min(Math.Max(positionCm, 0f), lengthCm);

        public SupportInput Clone() => new SupportInput { kind = kind, positionCm = positionCm, atRightEnd = atRightEnd };
    }

    /// <summary>
    /// Everything the HoloMech panels collect, in the units the panels show (cm, N, N/m, GPa; upward load
    /// positive on screen). <see cref="ToModel"/> converts to an SI <see cref="BeamModel"/> for the solver.
    /// Defaults follow the paper's screenshots (Fig. 3–5).
    /// </summary>
    [Serializable]
    public class HoloMechInputs
    {
        public SectionType section = SectionType.Pipe;
        public PipeDimensions pipe = new PipeDimensions();
        public BoxDimensions box = new BoxDimensions();
        public IBeamDimensions iBeam = new IBeamDimensions();

        /// <summary>Supports in any number and at any position (default: clamped at both ends, as in the paper).</summary>
        public List<SupportInput> supports = new List<SupportInput>
        {
            new SupportInput { kind = SupportKind.Fixed, positionCm = 0f },
            new SupportInput { kind = SupportKind.Fixed, atRightEnd = true },
        };

        public SupportPreset preset = SupportPreset.Custom;

        public float lengthCm = 200f;
        public MaterialPreset material = MaterialPreset.Custom;
        public float elasticModulusGPa = 20f;

        /// <summary>Point loads in any number (the Workbench adds and deletes them).</summary>
        public List<PointLoadInput> pointLoads = new List<PointLoadInput> { new PointLoadInput() };

        /// <summary>Uniform (partial) loads in any number.</summary>
        public List<UniformLoadInput> uniformLoads = new List<UniformLoadInput> { new UniformLoadInput() };

        /// <summary>Upper limits of the Workbench lists, so the panels stay readable on a headset.</summary>
        public const int MaxPointLoads = 8, MaxUniformLoads = 4, MaxSupports = 6;

        /// <summary>Supports closer than this are reported as being at the same place.</summary>
        public const float MinSupportGapCm = 1f;

        /// <summary>The first point load (the paper's single Point-Load column in the Guided flow).</summary>
        public PointLoadInput pointLoad
        {
            get
            {
                if (pointLoads.Count == 0) pointLoads.Add(new PointLoadInput { enabled = false });
                return pointLoads[0];
            }
            set
            {
                if (pointLoads.Count == 0) pointLoads.Add(value); else pointLoads[0] = value;
            }
        }

        /// <summary>The first uniform load (the paper's single Uniform-Load column in the Guided flow).</summary>
        public UniformLoadInput uniformLoad
        {
            get
            {
                if (uniformLoads.Count == 0) uniformLoads.Add(new UniformLoadInput { enabled = false });
                return uniformLoads[0];
            }
            set
            {
                if (uniformLoads.Count == 0) uniformLoads.Add(value); else uniformLoads[0] = value;
            }
        }

        /// <summary>
        /// Condition at the left end (the paper's boundary panel): Free when no support sits at x = 0. Setting it
        /// replaces whatever support is at that end.
        /// </summary>
        public EndCondition leftEnd
        {
            get => EndAt(false);
            set => SetEnd(false, value);
        }

        /// <summary>Condition at the right end, as <see cref="leftEnd"/>.</summary>
        public EndCondition rightEnd
        {
            get => EndAt(true);
            set => SetEnd(true, value);
        }

        const float EndToleranceCm = 0.5f;

        bool IsAtEnd(SupportInput s, bool right)
            => right ? s.atRightEnd || s.PositionOn(lengthCm) >= lengthCm - EndToleranceCm
                     : !s.atRightEnd && s.positionCm <= EndToleranceCm;

        EndCondition EndAt(bool right)
        {
            foreach (var s in supports)
                if (IsAtEnd(s, right)) return s.kind == SupportKind.Fixed ? EndCondition.Fixed : EndCondition.Pinned;
            return EndCondition.Free;
        }

        void SetEnd(bool right, EndCondition condition)
        {
            preset = SupportPreset.Custom;
            supports.RemoveAll(s => IsAtEnd(s, right));
            if (condition == EndCondition.Free) return;
            var kind = condition == EndCondition.Fixed ? SupportKind.Fixed : SupportKind.Pin;
            var support = right ? new SupportInput { kind = kind, atRightEnd = true, positionCm = lengthCm } : new SupportInput { kind = kind };
            if (right) supports.Add(support); else supports.Insert(0, support);
        }

        /// <summary>
        /// Changes the length. Supports on the right end stay on it; everything else keeps its distance from the left
        /// end and is pulled back onto the beam if it would fall off.
        /// </summary>
        public void SetLength(float newLengthCm)
        {
            lengthCm = newLengthCm;
            foreach (var s in supports)
                if (!s.atRightEnd) s.positionCm = Math.Min(Math.Max(s.positionCm, 0f), lengthCm);
            ClampLoadsToLength();
        }

        public double LengthM => lengthCm / 100.0;

        /// <summary>Selects a material button: presets set E; Custom keeps the current slider value.</summary>
        public void ApplyMaterial(MaterialPreset preset)
        {
            material = preset;
            if (preset != MaterialPreset.Custom) elasticModulusGPa = HoloMechRanges.PresetModulusGPa(preset);
        }

        /// <summary>
        /// The lab gantry: aluminium I-beam on two pins at the ends, one point load (the trolley) at midspan. Length and
        /// section keep their current values and the trolley gets the usual new-load force: all of them are placeholders
        /// until the gantry is measured (T257), so nothing here may be read as a measured value.
        /// </summary>
        public void ApplyGantryPreset()
        {
            section = SectionType.IBeam;
            ApplyMaterial(MaterialPreset.Aluminum);
            supports.Clear();
            supports.Add(new SupportInput { kind = SupportKind.Pin });
            supports.Add(new SupportInput { kind = SupportKind.Pin, atRightEnd = true });
            pointLoads.Clear();
            pointLoads.Add(new PointLoadInput { enabled = true, positionCm = lengthCm / 2f, forceN = 1000f, direction = LoadDirection.Down });
            uniformLoads.Clear();
            preset = SupportPreset.Gantry;
        }

        /// <summary>True while the two supports and the trolley load of the Gantry preset are there to adjust.</summary>
        public bool IsGantry => preset == SupportPreset.Gantry && supports.Count == 2 && pointLoads.Count >= 1;

        /// <summary>A, I, c of the selected section in SI units.</summary>
        public BeamSection SectionProperties()
        {
            const double cm = 0.01;
            switch (section)
            {
                case SectionType.Pipe: return BeamSection.Pipe(pipe.outerDiameterCm * cm, pipe.wallCm * cm);
                case SectionType.Box: return BeamSection.Box(box.widthCm * cm, box.heightCm * cm, box.wallCm * cm);
                default:
                    return BeamSection.IBeam(iBeam.heightCm * cm, iBeam.flangeWidthCm * cm, iBeam.flangeThicknessCm * cm,
                        iBeam.webThicknessCm * cm);
            }
        }

        /// <summary>The supports in SI units for the solver.</summary>
        public BeamSupports Supports
        {
            get
            {
                var list = new Support[supports.Count];
                for (int i = 0; i < list.Length; i++)
                    list[i] = new Support(supports[i].PositionOn(lengthCm) / 100.0, supports[i].kind);
                return BeamSupports.Custom(list);
            }
        }

        /// <summary>Pulls load positions back onto the beam after the length was shortened.</summary>
        public void ClampLoadsToLength()
        {
            foreach (var p in pointLoads) p.positionCm = Math.Min(Math.Max(p.positionCm, 0f), lengthCm);
            foreach (var w in uniformLoads)
            {
                w.endCm = Math.Min(Math.Max(w.endCm, 0f), lengthCm);
                w.startCm = Math.Min(Math.Max(w.startCm, 0f), lengthCm);
                if (w.endCm <= w.startCm) w.startCm = Math.Max(0f, w.endCm - 1f);
            }
        }

        /// <summary>
        /// Enforces the dependent section limits after a slider moved: pipe wall ≤ D/2, flange thickness &lt; h/2,
        /// web ≤ flange width. Returns true if anything was changed.
        /// </summary>
        public bool ClampSectionDimensions()
        {
            bool changed = false;
            if (pipe.wallCm > pipe.outerDiameterCm / 2f) { pipe.wallCm = pipe.outerDiameterCm / 2f; changed = true; }
            float tfMax = iBeam.heightCm / 2f - 0.1f;
            if (iBeam.flangeThicknessCm > tfMax) { iBeam.flangeThicknessCm = tfMax; changed = true; }
            if (iBeam.webThicknessCm > iBeam.flangeWidthCm) { iBeam.webThicknessCm = iBeam.flangeWidthCm; changed = true; }
            return changed;
        }

        /// <summary>Input problems a panel should show before the solver runs. Empty list = fine.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            if (!(lengthCm > 0)) problems.Add("Beam length must be positive.");
            if (!(elasticModulusGPa > 0)) problems.Add("Elastic modulus must be positive.");

            switch (section)
            {
                case SectionType.Pipe:
                    if (!(pipe.wallCm > 0) || pipe.wallCm > pipe.outerDiameterCm / 2)
                        problems.Add("Pipe wall thickness must be greater than 0 and at most half the outer diameter.");
                    break;
                case SectionType.IBeam:
                    if (2 * iBeam.flangeThicknessCm >= iBeam.heightCm)
                        problems.Add("I-beam flanges are thicker than half the height.");
                    if (iBeam.webThicknessCm > iBeam.flangeWidthCm)
                        problems.Add("I-beam web is wider than the flange.");
                    break;
            }

            for (int i = 0; i < pointLoads.Count; i++)
            {
                var p = pointLoads[i];
                if (p.enabled && (p.positionCm < 0 || p.positionCm > lengthCm))
                    problems.Add($"Point load must be on the beam (0 to beam length): P{i + 1}.");
            }

            for (int i = 0; i < uniformLoads.Count; i++)
            {
                var w = uniformLoads[i];
                if (!w.enabled) continue;
                if (!(w.endCm > w.startCm))
                    problems.Add($"Uniform load: end distance must be greater than start distance (w{i + 1}).");
                if (w.startCm < 0 || w.endCm > lengthCm)
                    problems.Add($"Uniform load must be on the beam (0 to beam length): w{i + 1}.");
            }

            if (supports.Count == 0)
            {
                problems.Add("The beam has no supports, so it is unstable. Add a fixed support, or two pinned supports.");
                return problems;
            }

            var positions = new List<float>();
            foreach (var s in supports) positions.Add(s.PositionOn(lengthCm));
            positions.Sort();
            for (int i = 1; i < positions.Count; i++)
            {
                if (positions[i] - positions[i - 1] < MinSupportGapCm)
                {
                    problems.Add($"Two supports are at the same place (x = {positions[i]:0} cm). Move one of them.");
                    return problems;
                }
            }

            if (!BeamSolver.IsStable(Supports, LengthM))
                problems.Add(supports.Count == 1 && supports[0].kind == SupportKind.Pin
                    ? "The beam is unstable with a single pinned support: it can rotate freely. Add a second support, or make it fixed."
                    : $"The beam is unstable with a {leftEnd} left end and a {rightEnd} right end: " +
                      "it can move or rotate freely. Fix at least one end, or pin both ends.");
            return problems;
        }

        /// <summary>
        /// Converts to the solver's SI model. The solver takes loads positive downward; the panels show upward as
        /// positive, so a Down load becomes a positive solver load.
        /// </summary>
        public BeamModel ToModel()
        {
            var model = new BeamModel(LengthM, SectionProperties(), elasticModulusGPa * 1e9, Supports);
            foreach (var p in pointLoads)
                if (p.enabled && p.forceN != 0)
                    model.PointLoads.Add(new PointLoad(p.positionCm / 100.0, Signed(p.forceN, p.direction)));
            foreach (var w in uniformLoads)
                if (w.enabled && w.intensityNPerM != 0 && w.endCm > w.startCm)
                    model.UniformLoads.Add(new UniformLoad(w.startCm / 100.0, w.endCm / 100.0, Signed(w.intensityNPerM, w.direction)));
            return model;
        }

        /// <summary>Validates and solves. On failure <paramref name="error"/> holds the message for the user.</summary>
        public bool TrySolve(out BeamSolution solution, out string error)
        {
            var problems = Validate();
            if (problems.Count > 0)
            {
                solution = null;
                error = string.Join("\n", problems);
                return false;
            }

            return BeamSolver.TrySolve(ToModel(), out solution, out error);
        }

        internal static double Signed(float magnitude, LoadDirection direction)
            => direction == LoadDirection.Down ? Math.Abs(magnitude) : -Math.Abs(magnitude);

        public HoloMechInputs Clone()
        {
            var c = (HoloMechInputs)MemberwiseClone();
            c.pipe = new PipeDimensions { outerDiameterCm = pipe.outerDiameterCm, wallCm = pipe.wallCm };
            c.box = new BoxDimensions { widthCm = box.widthCm, heightCm = box.heightCm, wallCm = box.wallCm };
            c.iBeam = new IBeamDimensions
            {
                heightCm = iBeam.heightCm, flangeWidthCm = iBeam.flangeWidthCm,
                flangeThicknessCm = iBeam.flangeThicknessCm, webThicknessCm = iBeam.webThicknessCm,
            };
            c.pointLoads = pointLoads.ConvertAll(p => new PointLoadInput
            {
                enabled = p.enabled, positionCm = p.positionCm, forceN = p.forceN, direction = p.direction,
            });
            c.uniformLoads = uniformLoads.ConvertAll(w => new UniformLoadInput
            {
                enabled = w.enabled, startCm = w.startCm, endCm = w.endCm, intensityNPerM = w.intensityNPerM, direction = w.direction,
            });
            c.supports = supports.ConvertAll(x => x.Clone());
            return c;
        }
    }
}
