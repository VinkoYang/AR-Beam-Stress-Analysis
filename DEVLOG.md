# Dev log

Newest first. Each entry records what changed, why, how it was checked, and what is still open.

## 2026-10-19 — Real beam mechanics (branch `wenhao/solver`, Wenhao Yang with Claude Code)

### Starting point

`main` at `69a2186` (Initial commit, 2026-04-10). What the original scripts did:

- `CantileverBeam`, `OverhangBeam`, `BeamBend` bent and coloured the mesh with
  `load × boundary shape function × exp(−8·|x − x_load|)`. That is not beam theory: the colour was a deflection
  proxy, the overhang "supports" were hard-coded at 1/3 and 2/3 of the mesh, and the "two fixed ends" scene only
  coloured (no deflection).
- Deformation used `InverseTransformDirection`, which ignores the beam's non-uniform scale (564 × 23 × 24), so
  the offset direction and size were wrong in local space.
- `CantileverBeam.Update` logged every frame; `BeamBend` allocated an array every frame; all three recomputed
  every frame whether or not anything changed.
- The beam mesh (`CubeMesh1.fbx`) has only 21 slices along its length, too few to bend smoothly.
- The mass slider ran 0–0.1 in arbitrary units.
- Shear-force / bending-moment "diagrams" were three static PNGs (`SF&BM*.png`) flashed by `DiagramPopup`.

### Unity 6.5 upgrade (commit "Upgrade project to Unity 6000.5.3f1")

Opened in 6000.5.3f1 and let the editor upgrade; no hand edits in that commit. AR Foundation 5.2 → 6.5,
XRI 3.1.2 → 3.5.1, URP 14 → 17.5, Test Framework 1.1.33 → 1.7.0, TMP folded into uGUI 2.5.

- The upgrade also imported TMP "Examples & Extras" (≈290 files). Left out of the commit; still on disk on the
  machine that did the upgrade. Delete or keep as you like; it can be re-imported from the TMP package.
- Remaining warnings come from the AR template (`ARTemplateMenuManager` uses `planesChanged`, deprecated in AR
  Foundation 6) and Visual Scripting node options. Not touched.

### Solver (`Assets/ARBeam/Mechanics`)

- Pure C#; the asmdef sets `noEngineReferences`, so it cannot depend on Unity. Inputs: length, rectangular
  section (b, h → A, I, c), E, supports, point loads, uniform (partial) loads. Outputs: V, M, θ, v, |M|c/I and
  fibre stress at any x, reactions, signed peaks of V, M, v with their positions, diagram polylines with
  vertical steps.
- Method: Macaulay. M(x) = Σ kᵢ⟨x − aᵢ⟩ⁿ including reactions, integrated twice in closed form.
  Cantilever and overhang/simply supported reactions come from statics; the two integration constants from
  v(0) = θ(0) = 0 or v(a) = v(b) = 0. Fixed–fixed: the left reaction and moment follow from v(L) = θ(L) = 0,
  a 2×2 system solved in closed form.
- Simply supported is the overhang case with supports at 0 and L (same code path).
- Peak M: segment ends plus the zero of the (linear) shear inside each segment. Peak v: roots of θ by bracketing
  and bisection. Both checked against 20 000-point sampling.

### Verification

- 77 EditMode tests, all passing in Unity 6.5 (Test Runner and `unity command run_tests`) and in .NET 8
  (`dotnet test Tools/SolverTests`). References: Hibbeler 10th ed. Appendix C; Gere & Goodno Appendix H; the rig
  plan's numbers. Exact formulas agree to 1e-9 relative; four-figure table coefficients to < 1 %.
- Mutation check: breaking one integration constant (`c2 = −fa − c1·a` → `c2 = −fa`) made 8 tests fail.
- Play-mode checks through the Unity CLI, 500 g unless noted:

  | Scene | Load | Solver result |
  |---|---|---|
  | Cantilever 400 mm | tip | R = 4.903 N, M_A = 1.961 N·m, tip v = −22.41 mm, σ = 45.96 MPa |
  | Overhang 800 mm (200/600) | right tip | R_A = −2.452 N, R_B = 7.355 N, M_B = −0.981 N·m, tip v = −8.404 mm, σ = 22.98 MPa |
  | Overhang | 200 g at 400 mm | R = 0.981 N each, M = 0.196 N·m, both tips rise 0.84 mm |
  | Fixed–fixed 800 mm | midspan | R = 2.452 N each, M_ends = −0.490 N·m, M_mid = 0.490 N·m, v = −2.801 mm, σ = 11.49 MPa |

  Shapes of the V and M diagrams were checked on screenshots.

### Unity side (`Assets/ARBeam/Runtime`, scenes)

- `BeamController` holds the real parameters, gathers loads from `BeamLoadSource`s, re-solves only when a source
  reports a change, and maps between metres along the beam and world space. The solver never sees a slider.
- `BeamMeshView` replaces the 21-slice mesh by a 64-segment box with the same bounds, converts the deflection to
  local space with `InverseTransformVector` (handles the non-uniform scale), and colours by |M|c/I on a fixed scale.
- `BeamDiagramGraphic` draws V and M from the solution on a uGUI `MaskableGraphic`. It works on a screen-space
  canvas (control condition: side panel) and on a world-space canvas laid along the beam (overlay condition),
  so both study conditions use the same code.
- The three original scripts keep their names, GUIDs, public slider methods and the `loadArrow` reference; they
  are now thin subclasses of `SliderDrivenBeam`.
- `BeamSceneSetup` (editor) configured the scenes: bench-rig bar, 0–500 g mass slider, live diagram panel in place
  of the PNG popup (PNG object kept), stress legend in MPa, overhang support models moved to 200/600 mm.

### Decisions

| Question | Decision | Why |
|---|---|---|
| Self-weight | Off by default, switchable | Matches textbook diagrams, tared load cells, and "deflection relative to the unloaded beam". 25 × 3 mm aluminium adds ≈ 16 % to the simply supported midspan moment, so turn it on when comparing absolute stresses. |
| Overhang supports | 200 / 600 mm on an 800 mm beam | Same as the rig plan; the scene's support models were moved to match. |
| Diagram popup | Toggle, stays up, updates live | The old 10 s flash made sense for a static picture, not for one that follows the slider. `DiagramPopup.autoHide` restores the old behaviour. |
| Colour quantity | Section maximum \|M\|c/I, existing blue→red legend | Keeps the students' legend. Fibre stress σ(x, y) is in the API (`FiberStress`) if a tension/compression view is wanted. |
| Colour and diagram scale | Fixed: 500 g at its worst position for that beam | Colours and diagram heights stay comparable between loads. Cantilever 46 MPa, overhang 23 MPa, fixed–fixed 14 MPa. |
| Amplification | 1 = true δ/L | In these scenes 500 g on the cantilever already sags about 1.3 beam depths. |
| 1018 steel E | 205 GPa (MatWeb) | The rig plan used 200 GPa; only matters for the material comparison. |

### Housekeeping

- `com.unity.pipeline` 0.8.0-exp.1 added so the Unity CLI can drive the Editor (scene setup and verification
  were done that way). Remove it if nobody uses the CLI.
- The TMP dynamic atlas of `AlfaSlabOne-Regular SDF.asset` changes whenever new characters are rendered in Play
  mode; that churn was reverted, not committed.
- Unity CLI notes: an unfocused Editor freezes Play mode unless `Application.runInBackground` is on (set it for
  the session, then restore; the project value is `false`); `capture_game_view --save_path` writes inside
  `Assets/`, so capture inline instead.

### Open

- Cross-validate against IndeterminateBeam (Python). Plan: export a shared set of cases and compare V, M, v at
  sample points, mapping sign conventions. Not started; the test cases are currently in C# only.
- Load source for tracked weights (rig option B) and for hand interaction (headset option A).
- Simply supported scene, if the experiment uses one.
- Android build module and on-device test.
