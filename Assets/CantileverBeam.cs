using ARBeam;
using ARBeam.Mechanics;

/// <summary>
/// "One fixed end" scene (BeamScene 1): 400 mm cantilever clamped at the left end.
/// The mechanics are in ARBeam.BeamController / ARBeam.Mechanics.BeamSolver; this class keeps the original
/// name so the scene's slider events still find SetLoadMagnitude / SetLoadPosition.
/// </summary>
public class CantileverBeam : SliderDrivenBeam
{
    void Reset() => ApplyPreset(SupportType.Cantilever, 400f);
}
