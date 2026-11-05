using System;

namespace HoloMech
{
    /// <summary>A length the on-site card can step with its − / + buttons.</summary>
    public enum SiteDimension
    {
        Length,
        PipeOuterDiameter,
        PipeWall,
        BoxWidth,
        BoxHeight,
        BoxWall,
        IHeight,
        IFlangeWidth,
        IFlangeThickness,
        IWebThickness,
    }

    public enum SiteStepResult
    {
        Changed,

        /// <summary>The value stopped at the end of its own range.</summary>
        AtLimit,

        /// <summary><see cref="HoloMechInputs.ClampSectionDimensions"/> pulled a dimension back.</summary>
        SectionLimited,
    }

    /// <summary>
    /// Millimetre steps for the on-site "Dimensions" rows (PI's request, 2026-11-05): the real beam is measured with a
    /// tape, so the card works in mm while <see cref="HoloMechInputs"/> stays in cm. Values are kept to 0.1 mm.
    /// </summary>
    public static class SiteAdjust
    {
        static readonly SiteDimension[] PipeRows = { SiteDimension.Length, SiteDimension.PipeOuterDiameter, SiteDimension.PipeWall };
        static readonly SiteDimension[] BoxRows = { SiteDimension.Length, SiteDimension.BoxWidth, SiteDimension.BoxHeight, SiteDimension.BoxWall };
        static readonly SiteDimension[] IBeamRows =
        {
            SiteDimension.Length, SiteDimension.IHeight, SiteDimension.IFlangeWidth, SiteDimension.IFlangeThickness, SiteDimension.IWebThickness,
        };

        /// <summary>The rows for a section type: beam length, then that section's dimensions.</summary>
        public static SiteDimension[] For(SectionType section)
        {
            switch (section)
            {
                case SectionType.Pipe: return PipeRows;
                case SectionType.Box: return BoxRows;
                default: return IBeamRows;
            }
        }

        public static string Caption(SiteDimension d)
        {
            switch (d)
            {
                case SiteDimension.Length: return "Beam length";
                case SiteDimension.PipeOuterDiameter: return "Outer diam. D";
                case SiteDimension.PipeWall: return "Wall t";
                case SiteDimension.BoxWidth: return "Width b";
                case SiteDimension.BoxHeight: return "Height h";
                case SiteDimension.BoxWall: return "Wall t";
                case SiteDimension.IHeight: return "Height h";
                case SiteDimension.IFlangeWidth: return "Flange width bf";
                case SiteDimension.IFlangeThickness: return "Flange thick. tf";
                default: return "Web thick. tw";
            }
        }

        public static float ValueMm(HoloMechInputs i, SiteDimension d) => GetCm(i, d) * 10f;

        /// <summary>
        /// Changes a dimension by <paramref name="deltaMm"/>. The length goes through <see cref="HoloMechInputs.SetLength"/>,
        /// so right-end supports follow it, and may reach <see cref="HoloMechRanges.SiteLengthMaxCm"/>.
        /// </summary>
        public static SiteStepResult Step(HoloMechInputs i, SiteDimension d, float deltaMm)
        {
            Range(d, out float min, out float max);
            float wanted = RoundCm(GetCm(i, d) + deltaMm / 10f);
            float value = Math.Min(Math.Max(wanted, min), max);
            bool limited = value != wanted;

            if (d == SiteDimension.Length)
            {
                i.SetLength(value);
                return limited ? SiteStepResult.AtLimit : SiteStepResult.Changed;
            }

            SetCm(i, d, value);
            if (i.ClampSectionDimensions()) return SiteStepResult.SectionLimited;
            return limited ? SiteStepResult.AtLimit : SiteStepResult.Changed;
        }

        static float RoundCm(float cm) => (float)Math.Round(cm, 2);

        static void Range(SiteDimension d, out float min, out float max)
        {
            switch (d)
            {
                case SiteDimension.Length: min = HoloMechRanges.LengthMinCm; max = HoloMechRanges.SiteLengthMaxCm; break;
                case SiteDimension.PipeOuterDiameter: min = HoloMechRanges.PipeDiameterMinCm; max = HoloMechRanges.PipeDiameterMaxCm; break;
                case SiteDimension.PipeWall: min = HoloMechRanges.PipeWallMinCm; max = HoloMechRanges.PipeWallMaxCm; break;
                case SiteDimension.BoxWidth: min = HoloMechRanges.BoxWidthMinCm; max = HoloMechRanges.BoxWidthMaxCm; break;
                case SiteDimension.BoxHeight: min = HoloMechRanges.BoxHeightMinCm; max = HoloMechRanges.BoxHeightMaxCm; break;
                case SiteDimension.BoxWall: min = HoloMechRanges.BoxWallMinCm; max = HoloMechRanges.BoxWallMaxCm; break;
                case SiteDimension.IHeight: min = HoloMechRanges.IHeightMinCm; max = HoloMechRanges.IHeightMaxCm; break;
                case SiteDimension.IFlangeWidth: min = HoloMechRanges.IFlangeWidthMinCm; max = HoloMechRanges.IFlangeWidthMaxCm; break;
                case SiteDimension.IFlangeThickness: min = HoloMechRanges.IFlangeThicknessMinCm; max = HoloMechRanges.IFlangeThicknessMaxCm; break;
                default: min = HoloMechRanges.IWebThicknessMinCm; max = HoloMechRanges.IWebThicknessMaxCm; break;
            }
        }

        static float GetCm(HoloMechInputs i, SiteDimension d)
        {
            switch (d)
            {
                case SiteDimension.Length: return i.lengthCm;
                case SiteDimension.PipeOuterDiameter: return i.pipe.outerDiameterCm;
                case SiteDimension.PipeWall: return i.pipe.wallCm;
                case SiteDimension.BoxWidth: return i.box.widthCm;
                case SiteDimension.BoxHeight: return i.box.heightCm;
                case SiteDimension.BoxWall: return i.box.wallCm;
                case SiteDimension.IHeight: return i.iBeam.heightCm;
                case SiteDimension.IFlangeWidth: return i.iBeam.flangeWidthCm;
                case SiteDimension.IFlangeThickness: return i.iBeam.flangeThicknessCm;
                default: return i.iBeam.webThicknessCm;
            }
        }

        static void SetCm(HoloMechInputs i, SiteDimension d, float cm)
        {
            switch (d)
            {
                case SiteDimension.PipeOuterDiameter: i.pipe.outerDiameterCm = cm; break;
                case SiteDimension.PipeWall: i.pipe.wallCm = cm; break;
                case SiteDimension.BoxWidth: i.box.widthCm = cm; break;
                case SiteDimension.BoxHeight: i.box.heightCm = cm; break;
                case SiteDimension.BoxWall: i.box.wallCm = cm; break;
                case SiteDimension.IHeight: i.iBeam.heightCm = cm; break;
                case SiteDimension.IFlangeWidth: i.iBeam.flangeWidthCm = cm; break;
                case SiteDimension.IFlangeThickness: i.iBeam.flangeThicknessCm = cm; break;
                case SiteDimension.IWebThickness: i.iBeam.webThicknessCm = cm; break;
            }
        }
    }
}
