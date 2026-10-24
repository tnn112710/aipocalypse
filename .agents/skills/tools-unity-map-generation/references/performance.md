# Map performance

What moved the frame time on the WorldMap and how it was measured. This is the reference for environment perf; for general profiling and optimisation use the user-scope `unity-optimization-pc` skill and ARCHITECTURE.md → Performance. Machine: RTX 5070 Ti Laptop / i7-13700K, 1080p, editor play mode. Target 80–100 fps.

## 1. Where it stands

- 2026-10-20 pass: nine test views at 107–131 fps (from 83–113). Main thread 5.0–6.5 ms, of which the editor loop ~3 ms; GPU 1.5–2.8 ms; draws at the spawn 4.1k → 2.5k. The editor is CPU-bound (GPU at ~13% load).
- The coordinates of those nine views were never recorded. Write view lists into ENVIRONMENT.md as `name:x,z,yaw` so runs are comparable.
- The 1500 m map (v23: ~11k terrain trees, ~29k scripted renderers, 228 particle systems, far clip 3000 m) was never measured.

## 2. What moved it

| Change | Effect | Notes |
| --- | --- | --- |
| SSAO off; decals Screen Space (not DBuffer) | Each forced a depth-normals prepass that re-drew every grass blade: ~4k draws, 8M triangles, 2–4 ms GPU at the spawn | Project-wide (PC_RPAsset/PC_Renderer) |
| Depth priming **Forced** | Forest GPU 7.1 → 3.9 ms, meadow 5.5 → 3.7 ms, for ~1k extra draws; disabling it cost +0.6 ms | A review agent wrongly guessed "draws everything twice, disable it" |
| Static batching off | GRD can't instance static-batched combined meshes; batching also duplicated ~4.6M vertices (~263 MB); −320 draws at equal cost | Player Settings |
| GRD small-mesh culling | Skips pebbles and far props covering a few pixels (asset holds 0.3%; docs say 0.5%); the shadow-pass variant gave nothing | |
| LOD bias | Bias 2 doubled every LOD/cull distance (~+4k draws); 0.7–0.75 picked from A/B | |
| First-person overlay camera → renderer feature | ~1.1 ms CPU | |
| Detail distance | 250 m = 20M triangles at the spawn, 120 m ≈ 9.5M; 120 → 80 m: −1.6 to −1.9 ms CPU, −1.0 ms GPU; density 1 → 0.6 about the same as the distance cut | Details dominate triangles |
| Detail prototypes | Salvia group (960 tris) → single stem (199) + merged layers (38 → 34): detail triangles −27% at the spawn | Check triangles before adding |
| Terrain shadow casting off | −1.0 to −1.6 ms CPU, −0.3 to −0.8 ms GPU | User applied with tree distance 1000, detail 100 |
| Shadows off on low/far rocks | ~8.4k renderer overrides in the scene (bank, water, island stones, near range, peaks) | Must be in the generator's render-policy pass |
| Particle culling Pause | Noise-module systems simulated off-screen under Automatic; all atmosphere particles off = −0.4 ms CPU, −0.5 ms GPU | Record as prefab overrides |
| Shadow distance 100 m / 4 cascades vs 150 m / 4 | Same cost; the URP template's 50 m left every tree past it unshadowed | Now 150 m / 4 / 2048 |
| Water reflections | Environment reflections off on every water material; no planar reflections (a second full scene render) | tools-unity-stylized-water |
| Unity terrain trees | ~6k of 9.6k draws at the spawn before the pass; 4.5–7.8 ms GPU in forest and highlands | Forest thins beyond ~350 m (LOD cull) |
| GPUI **Detail** Manager | Faster: ~28 indirect draws for hundreds of thousands of instances | Needs the GPUI setup node in every Shader Graph (Raygeas graphs lack it) |
| GPUI **Tree** Manager | Slower: CPU issues `RenderMeshIndirect` per prototype × LOD × material × camera every frame even when culled to 0; 43 prototypes ≈ 750 calls per camera (estimate, not profiled); many prototypes had 1–10 instances | Consolidate prototypes, shadow LOD map, billboards before retrying |
| Firing in the editor | 25–50 ms spikes: the Hierarchy window rebuilds its 16k-object tree when pooled FX re-parent | Collapse the Hierarchy; builds unaffected |

Rules of thumb for map content: details ≤ ~200 triangles for dense cover; detail distance ≤ ~120 m on Unity's renderer; shadows only on things near the player and tall enough to show one; no colliders beyond the walls; Pause culling on ambient particles; price every "beauty" feature (SSAO, longer shadows, reflections) in ms at the fixed views before proposing it. SSAO, 150 m/4096 shadows, SSR and a lake probe were all added in a beauty pass and reverted for cost.

## 3. Measuring

Scripts in `AgentScripts/Perf/` (run with `unity command run_script` in play mode; last run 2026-10-20, recompile-check before relying on them) and `AgentScripts/DayNight/Prof.cs`:

| Script | Does |
| --- | --- |
| `PerfProbe.Run("name:x,z,yaw;…", frames)` | Teleports the player to each view (CharacterController off/on), settles 2 s, then median/p95 frame time, CPU/GPU from `FrameTimingManager`, draw calls. |
| `PerfAB.Run(outName, frames, from, to)` | A/B of runtime toggles (shadows, depth priming, LOD bias, terrain distances/density/shadows, render features, atmosphere) averaged over 4 yaws; "baseline" first and last to catch drift; snapshot/restore in `finally` (URP asset edits made in play mode persist to disk otherwise). Writes `%TEMP%/claude_perf_<outName>.txt`. |
| `PerfCapture.Run(outName, seconds, look)` | Idle / look / fire / look+fire phases through a virtual mouse on the real input path; `ProfilerDriver` capture without the editor; Batches, SetPass, draws, triangles, shadow casters, GC per frame. |
| `SceneScan.Run()` | Static dump: URP asset, renderer features, quality, cameras, lights, renderer and shadow-caster counts, top shaders, terrain draw settings, volumes. Good first step. |
| `Prof.Window/Compare/Spikes/Report` | Median CPU/GPU in a frame window, markers that grew most between two windows, spike frames. |

Traps:
- Play mode doesn't tick while Unity is in the background: `Application.runInBackground = true` **right after** `editor_play` (in edit mode it writes ProjectSettings).
- `run_script` budget: `--timeout_ms` + CLI `--timeout`; async entries keep running after the CLI gives up, so also write results to a file.
- Modal dialogs (scene modified externally, save prompt before tests) block every command including `editor_status`.
- Error Pause freezes play mode when firing logs an error. The player spawns empty-handed: give it a weapon by script to profile firing.
- Noise: a long-running editor session ran at 14–19 ms PlayerLoop vs 7.5–9 ms after a restart (broken audio device, memory eviction); baselines drift ~2 ms while warming up; the Profiler window itself causes 25–30 ms frames; an open Scene view is an extra camera; a selected Light slowed frames. Average several yaws (one spot drew 1.6k vs 3k calls by heading).
- Judge final numbers in a development build; in the editor close the Scene view and collapse the Hierarchy.
