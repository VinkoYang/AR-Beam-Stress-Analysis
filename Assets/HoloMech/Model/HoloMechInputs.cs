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

        public EndCondition leftEnd = EndCondition.Fixed;
        public EndCondition rightEnd = EndCondition.Fixed;

        public float lengthCm = 200f;
        public MaterialPreset material = MaterialPreset.Custom;
        public float elasticModulusGPa = 20f;

        public PointLoadInput pointLoad = new PointLoadInput();
        public UniformLoadInput uniformLoad = new UniformLoadInput();

        public double LengthM => lengthCm / 100.0;

        /// <summary>Selects a material button: presets set E; Custom keeps the current slider value.</summary>
        public void ApplyMaterial(MaterialPreset preset)
        {
            material = preset;
            if (preset != MaterialPreset.Custom) elasticModulusGPa = HoloMechRanges.PresetModulusGPa(preset);
        }

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

        public BeamSupports Supports => BeamSupports.Ends(leftEnd, rightEnd);

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

            if (pointLoad.enabled && (pointLoad.positionCm < 0 || pointLoad.positionCm > lengthCm))
                problems.Add("Point load must be on the beam (0 to beam length).");
            if (uniformLoad.enabled)
            {
                if (!(uniformLoad.endCm > uniformLoad.startCm))
                    problems.Add("Uniform load: end distance must be greater than start distance.");
                if (uniformLoad.startCm < 0 || uniformLoad.endCm > lengthCm)
                    problems.Add("Uniform load must be on the beam (0 to beam length).");
            }

            if (!BeamSolver.IsStable(Supports))
                problems.Add($"The beam is unstable with a {leftEnd} left end and a {rightEnd} right end: " +
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
            if (pointLoad.enabled && pointLoad.forceN != 0)
                model.PointLoads.Add(new PointLoad(pointLoad.positionCm / 100.0, Signed(pointLoad.forceN, pointLoad.direction)));
            if (uniformLoad.enabled && uniformLoad.intensityNPerM != 0 && uniformLoad.endCm > uniformLoad.startCm)
                model.UniformLoads.Add(new UniformLoad(uniformLoad.startCm / 100.0, uniformLoad.endCm / 100.0,
                    Signed(uniformLoad.intensityNPerM, uniformLoad.direction)));
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

        static double Signed(float magnitude, LoadDirection direction)
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
            c.pointLoad = new PointLoadInput
            {
                enabled = pointLoad.enabled, positionCm = pointLoad.positionCm, forceN = pointLoad.forceN,
                direction = pointLoad.direction,
            };
            c.uniformLoad = new UniformLoadInput
            {
                enabled = uniformLoad.enabled, startCm = uniformLoad.startCm, endCm = uniformLoad.endCm,
                intensityNPerM = uniformLoad.intensityNPerM, direction = uniformLoad.direction,
            };
            return c;
        }
    }
}
