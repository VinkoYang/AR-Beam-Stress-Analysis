# AR Beam Learning Module

Unity app for teaching shear force, bending moment, stress and deflection in beams. Students load a beam
(sliders today; tracked weights on a physical rig, or hand interaction in a headset, later) and see the
closed-form beam-theory result: the deflected shape, a stress colour map on a fixed scale, the shear-force and
bending-moment diagrams, and the support reactions.

The mechanics are exact Euler–Bernoulli closed-form solutions (Macaulay / singularity functions) for any set of
pin and clamp supports, checked against Hibbeler's beam tables and against an independent stiffness-method solver.
A reproduction of HoloMech (Amouzgar & Mousavi 2025) is being built in `Assets/HoloMech/` on the same solver.
Change history and design decisions are in [DEVLOG.md](DEVLOG.md).

## Requirements

| | |
|---|---|
| Unity | **6000.5.3f1** (Unity 6.5). The project was upgraded from 2022.3.62f3 on 2026-10-19. |
| Key packages | AR Foundation / ARCore / ARKit 6.5.0, XR Interaction Toolkit 3.5.1, URP 17.5.0, Input System 1.19.0, Test Framework 1.7.0, uGUI 2.5.0 (includes TextMesh Pro) |
| Optional | `com.unity.pipeline` (lets the [Unity CLI](https://unity.com) drive the open Editor); .NET 8 SDK to run the solver tests without Unity |
| Device builds | Android Build Support is **not** set up yet; add it in Unity Hub before building for a tablet. |

## Scenes

All in `Assets/_ARMarker/Scenes/`. Open `MainMenu.unity` and press Play.

| Scene | Menu button | Beam | Script on the beam |
|---|---|---|---|
| `BeamScene 1.unity` | One fixed end | Cantilever, L = 400 mm, clamped at the left | `CantileverBeam` |
| `BeamScene 2.unity` | Overhanging beam | L = 800 mm, pin at 200 mm, roller at 600 mm | `OverhangBeam` |
| `BeamScene.unity` | Two fixed ends | L = 800 mm, clamped at both ends (virtual only) | `BeamBend` |

All three use the bench-rig bar: 6061-T6 aluminium, 1" × 1/8" (25.4 × 3.175 mm), E = 68.9 GPa.
The mass slider is 0–500 g; the position slider runs from the left end (0) to the right end (1).
The **Shear Force & Bending Moment Diagram** button toggles a live diagram panel.

## How it fits together

```
 Load sources                 Model + solver                        Views
 ─────────────                ──────────────                        ─────
 SliderLoadSource  ─┐                                          ┌─▶ BeamMeshView        (deflected shape + stress colours)
 InspectorLoadSource├─▶ BeamController ──▶ BeamSolver.Solve ──▶├─▶ BeamDiagramGraphic  (V and M diagrams, uGUI)
 (marker / hand,    │    beam params,        (pure C#)         ├─▶ StressLegendLabel   (legend in MPa)
  to be written)   ─┘    fixed scales,                         └─▶ SliderLoadSource    (arrow, readouts)
                         scene ↔ beam mapping
```

| Folder | Assembly | What is in it |
|---|---|---|
| `Assets/ARBeam/Mechanics/` | `ARBeam.Mechanics` (`noEngineReferences: true`) | `BeamModel`, `BeamSection` (rectangle, pipe, box, I), `BeamMaterial`, `PointLoad`, `UniformLoad`, `BeamSupports` (named layouts, per-end Free/Pinned/Fixed, or any support list), `BeamSolver`, `BeamSolution`, `BeamResult`. No Unity dependency; the compiler enforces it. |
| `Assets/ARBeam/Runtime/` | `ARBeam.Runtime` | MonoBehaviours: `BeamController`, `BeamLoadSource` (+ `SliderLoadSource`, `InspectorLoadSource`), `BeamMeshView`, `BeamDiagramGraphic`, `StressLegendLabel`, `SliderDrivenBeam`. |
| `Assets/ARBeam/Editor/` | Assembly-CSharp-Editor | `BeamSceneSetup`: menu **ARBeam ▸ Set Up Beam Scenes** wires the three scenes. Idempotent. |
| `Assets/ARBeam/Tests/EditMode/` | `ARBeam.Mechanics.Tests` | 105 NUnit tests (see below). |
| `Assets/HoloMech/Model/` | `HoloMech.Model` (`noEngineReferences: true`) | `HoloMechInputs`: the HoloMech panels' values (cm, N, N/m, GPa, upward positive) and their conversion to an SI `BeamModel`. |
| `Assets/HoloMech/Tests/EditMode/` | `HoloMech.Tests` | 8 tests for the unit and sign conversion. |
| `Assets/*.cs` | Assembly-CSharp | The original scripts. `CantileverBeam`, `OverhangBeam`, `BeamBend` are now thin subclasses of `SliderDrivenBeam`, kept so the scenes' slider events still resolve. `DiagramPopup` toggles the live panel. |
| `Tools/SolverTests/` | — | .NET project that compiles the solver and its tests outside Unity. |

### Using the solver directly

```csharp
using ARBeam.Mechanics;

var bar = BeamSection.Rectangular(0.0254, 0.003175);                  // b, h in metres
var model = new BeamModel(0.8, bar, BeamMaterial.Aluminum6061T6.ElasticModulus,
                          BeamSupports.Overhang(0.2, 0.6));
model.PointLoads.Add(PointLoad.FromMass(0.8, 0.5));                   // 500 g at the right tip
var s = BeamSolver.Solve(model);

s.Reactions;              // [R_A = −2.45 N at 0.2 m, R_B = 7.35 N at 0.6 m]
s.Moment(0.6);            // −0.981 N·m
s.Deflection(0.8);        // −8.40e-3 m
s.MaxBendingStress;       // 2.30e7 Pa
s.Diagram(BeamQuantity.Shear);   // polyline with vertical steps at point loads
s.Sample(16);                    // BeamResult: arrays x, V, M, θ, w, σmax on the same grid

// Any end conditions; a mechanism is reported instead of solved:
var propped = new BeamModel(2.0, BeamSection.Pipe(0.16, 0.026), 200e9,
                            BeamSupports.Ends(EndCondition.Fixed, EndCondition.Pinned));
BeamSolver.TrySolve(propped, out var solution, out string error);   // error != null for Free–Pinned etc.
```

### Conventions

- **Units:** SI inside the solver (m, N, Pa). The Inspector uses mm, g and MPa for readability.
- **x** runs from the beam's left end (0) to the right end (L).
- **Signs (Hibbeler):** loads positive downward; V = sum of upward forces left of the section; M positive when
  sagging; θ and v positive upward. A beam loaded by gravity has v < 0. Diagrams draw positive values upward.
- **Reactions:** force positive upward, moment positive counter-clockwise on the beam.
- **Shear and moment jump** at point loads (and M at clamps). `Shear(x)`/`Moment(x)` return the right-hand limit
  (left-hand limit at x = L); `ShearLeft`/`ShearRight` when the side matters.
- **Self-weight** is off by default (`BeamController.includeSelfWeight`). When on, it is added to V, M, σ and the
  reactions; the drawn deflection is always relative to the unloaded beam, because the real bar already sags under
  its own weight.
- **Deflection amplification** (`BeamMeshView`): 1 means the drawn δ/L equals the real δ/L. The beam's scene length
  maps to the real length, so a 1:1 AR overlay uses amplification 1.
- **Fixed colour and diagram scales:** full scale is the design mass (500 g) at its worst position on that beam,
  computed once per configuration. Nothing is renormalised per frame, so colour and diagram height mean the same
  thing for every load. Override the stress scale with `stressScaleOverrideMPa`.

### Adding another input (marker-tracked weights, hand interaction)

Subclass `BeamLoadSource`, report loads in beam coordinates, and call `NotifyChanged()` when they change.
`BeamController.WorldToBeamX` projects a tracked world position onto the beam axis, and `SetAxisWorld` aligns the
axis with two tracked points (for example markers at the beam ends).

```csharp
public class MarkerLoadSource : ARBeam.BeamLoadSource
{
    public override void CollectLoads(ARBeam.BeamController beam, List<PointLoad> loads)
    {
        foreach (var hanger in stableHangers)   // e.g. only after 0.5 s without movement
            loads.Add(PointLoad.FromMass(beam.WorldToBeamX(hanger.position), hanger.massKg));
    }
}
```

Put it on the beam object (or a child) and it is picked up automatically, or add it with
`BeamController.AddLoadSource`.

## Tests

113 EditMode tests.

`Assets/ARBeam/Tests/EditMode/` (105):

- **HibbelerTableTests**: R. C. Hibbeler, *Mechanics of Materials* (10th ed.), Appendix C: cantilever (end load,
  midspan load, full and half UDL) and simply supported (central and off-centre load, full and half UDL),
  checking v_max, θ, the elastic curve, reactions and M_max. Exact formulas agree to 1e-9; the two tabulated
  four-figure coefficients (0.006563 wL⁴/EI at 0.4598 L) to the project's 1 % criterion.
- **OverhangAndFixedTests**: overhang by superposition of Appendix C cases; fixed–fixed from Gere & Goodno
  Appendix H / Roark Table 8.1.
- **GeneralSupportTests**: the HoloMech spec §3.5 acceptance cases (< 0.5 %) through the per-end API, including
  a propped cantilever and a cantilever clamped on the right; stability of all nine Free/Pinned/Fixed end pairs;
  a two-span continuous beam; and agreement with **StiffnessReferenceSolver** (direct stiffness method, Hermite
  elements, used only in tests) for every stable end pair, an overhang and a three-support beam, to 1e-6.
- **SectionTests**: A, I, c of rectangle, hollow box, pipe and I-section against hand calculation.
- **BenchRigTests**: the numbers in the rig plan (22.4 / 11.2 / 8.4 mm, 46 / 23 MPa for 500 g).
- **SolverPropertyTests**: equilibrium, dM/dx = V and dv/dx = θ, support conditions, superposition, peak
  finding against dense sampling, diagram steps, input validation.

`Assets/HoloMech/Tests/EditMode/` (8): cm/GPa → SI, "upward is positive" → solver sign, material buttons,
section dimensions, unstable supports and bad loads reported before solving.

Run them in **Window ▸ General ▸ Test Runner ▸ EditMode**, or without Unity:

```
dotnet test Tools/SolverTests
```

or against the open Editor with the Unity CLI:

```
unity command run_tests -- --mode EditMode --filter ARBeam.Mechanics.Tests --filter_type assembly
unity command run_tests -- --mode EditMode --filter HoloMech.Tests --filter_type assembly
```

## Re-running the scene setup

`BeamSceneSetup` sets the beam parameters, adds the components, sets the mass slider to 0–500 g, builds the live
diagram panel next to the old PNG popup (the PNG is kept, unused), and moves the overhang supports to 200/600 mm.
Run it from **ARBeam ▸ Set Up Beam Scenes**, or from the CLI:

```
unity command eval -- --code "return BeamSceneSetup.SetUpAllScenes();" --timeout 120000
```

## Not done yet

- HoloMech reproduction milestones M2–M5 (beam mesh and deflection view, input panels, stress view, device).
- Cross-check against the Python package IndeterminateBeam (Bonanno 2021).
- Load input from tracked weight markers (rig option B) or hand interaction (headset option A).
- A simply supported scene (the solver and tests already cover it).
- Android build setup; AR placement of the virtual beam.
