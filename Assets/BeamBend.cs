using ARBeam;
using ARBeam.Mechanics;

/// <summary>
/// "Two fixed ends" scene (BeamScene): 800 mm beam clamped at both ends. Statically indeterminate, so it is
/// only offered in virtual mode (a real clamp is never perfectly rigid).
/// The mechanics are in ARBeam.BeamController / ARBeam.Mechanics.BeamSolver; this class keeps the original
/// name so the scene's slider events still find SetBend / SetLoadPosition.
/// </summary>
public class BeamBend : SliderDrivenBeam
{
    void Reset() => ApplyPreset(SupportType.FixedFixed, 800f);

    /// <summary>Original name of the mass slider callback in this scene.</summary>
    public void SetBend(float grams) => SetLoadMagnitude(grams);
}
