# README media

How the GIFs in `Docs/media/` are made. They show the desktop Editor scene `Assets/HoloMech/Scenes/HoloMech.unity`, driven by `DemoDriver` (real controls, scripted timeline) and captured by Unity Recorder through `DemoRecorder`.

| GIF | Demo | Length | What it shows |
|---|---|---|---|
| `holomech-workbench-live.gif` | `workbench` | 12 s | Simple preset; P1 moves along the beam and grows; P2 is added and moved. Colour = bending stress. |
| `holomech-inputs.gif` | `inputs` | 15 s | Support presets (Cantilever, Simple, Fixed-Fixed, 2 Spans); the clamp turned into a pin (unstable, banner); a uniform load added and shortened; an interior support added; Gantry preset. |
| `holomech-stress-cut.gif` | `stress` | 11 s | Stress view: the section cut moves along the beam showing shear force, then bending moment, then the normal-stress profile. |

## Steps

1. Open the project in Unity 6000.5.3f1 and open `Assets/HoloMech/Scenes/HoloMech.unity`.
2. Keep the Unity window in front and not minimised for the whole take; an occluded or minimised Editor can stop rendering the Game view.
3. Press Play. Wait until the Workbench is on screen.
4. Menu **HoloMech ▸ README Demos ▸ Record ...** (one demo per take). The Console prints `DemoRecorder: done` when the take ends. Look for `DemoDriver: no button ...` or `no slider/toggle` warnings: a warning means a control was not found and that step is missing from the take.
5. Stop Play between takes, so every take starts from the scene's default inputs.
6. Convert: `powershell -ExecutionPolicy Bypass -File Tools/ReadmeMedia/mp4_to_gif.ps1` (needs ffmpeg). It writes the three GIFs into `Docs/media/` and prints their sizes; keep each under about 3 MB (lower `-Fps` or `-Width` if needed).

If Play mode freezes on entering (the Editor stops responding), turn off **Project Settings ▸ XR Plug-in Management ▸ Windows/Mac/Linux ▸ Initialize XR on Startup** for the recording, and switch it back on afterwards (`git checkout -- Assets/XR/XRGeneralSettings.asset`). The desktop scene does not need XR.
