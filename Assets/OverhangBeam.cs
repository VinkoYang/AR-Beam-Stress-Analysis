using ARBeam;
using ARBeam.Mechanics;

/// <summary>
/// "Overhanging Beam" scene (BeamScene 2): 800 mm beam on supports at 200 mm and 600 mm, as on the bench rig.
/// The mechanics are in ARBeam.BeamController / ARBeam.Mechanics.BeamSolver; this class keeps the original
/// name so the scene's slider events still find SetLoadMagnitude / SetLoadPosition.
/// </summary>
public class OverhangBeam : SliderDrivenBeam
{
    void Reset() => ApplyPreset(SupportType.Overhang, 800f, 200f, 600f);
}
