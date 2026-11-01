# Map performance

What moved the frame time on the WorldMap and how it was measured. This is the reference for environment perf; for general profiling and optimisation use the user-scope `unity-optimization-pc` skill and ARCHITECTURE.md → Performance. Machine: RTX 5070 Ti Laptop / i7-13700K, 1080p, editor play mode. Target 80–100 fps.

## 1. Where it stands

- 2026-10-20 pass: nine test views at 107–131 fps (from 83–113). Main thread 5.0–6.5 ms, of which the editor loop ~3 ms; GPU 1.5–2.8 ms; draws at the spawn 4.1k → 2.5k. The editor is CPU-bound (GPU at ~13% load).
- 2026-10-25 pass (Core Ultra 7 255HX + RTX 5070 Ti Laptop): the recorded views (`PerfSuite.DefaultViews`, also in ENVIRONMENT.md) at 09:00 run at 101–116 fps. Game PlayerLoop is 6.3–7.5 ms (render submission 3.5–4.6 ms of it); GPU is 1.3–2.7 ms. Still main-thread bound. Scenario results (actions, day/night, first use, A/B) are in ENVIRONMENT.md → Performance.
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
| Mip streaming off | −0.4 to −0.6 ms main thread at every view (no texture streams; `TextureStreamingManager.UpdateRenderers` walked 13k renderers) | `QualitySettings` PC; `PerfSettings.Apply` |
| GPU occlusion culling (GRD) | +1.1 ms GPU, +0.9 ms CPU at the spawn | Keep off |
| 2 shadow cascades / soft shadows Medium / render scale 0.8 | −0.28 / −0.07 / −0.15 ms GPU at the spawn | Quality-tier knobs, not needed at the target |
| SW3 height pre-pass off | No measurable change | Left on |
| Atmosphere particles off | −0.34 ms main, −0.16 ms render thread | Art; reference cost only |

Rules of thumb for map content: details ≤ ~200 triangles for dense cover; detail distance ≤ ~120 m on Unity's renderer; shadows only on things near the player and tall enough to show one; no colliders beyond the walls; Pause culling on ambient particles; price every "beauty" feature (SSAO, longer shadows, reflections) in ms at the fixed views before proposing it. SSAO, 150 m/4096 shadows, SSR and a lake probe were all added in a beauty pass and reverted for cost.

## 3. Measuring

Scripts in `AgentScripts/Perf/` (run with `unity command script run` in play mode; last run 2026-10-20, recompile-check before relying on them) and `AgentScripts/DayNight/Prof.cs`:

| Script | Does |
| --- | --- |
| `PerfSuite.Views(views, seconds, profile, hour)` | Per-view sweep at a frozen hour: frame time, game cost (PlayerLoop minus the present wait), render submission, FrameTiming GPU, draws / SetPass / triangles / shadow casters, cameras per frame; `profile` adds top markers outside the editor loop. |
| `PerfSuite.Actions` / `FirstUse` / `DayNight` | Idle, look, walk, sprint, jump, fire (hip, aim, turning), switch, reload, melee through a virtual keyboard and mouse; first-use hitches in a fresh play session; hour transitions, holds and night with the lantern. |
| `PerfSuite.Ab(only, frames, hour, at)` | Runtime A/B of ~30 toggles (post components, shadows, GRD occlusion, render scale, features, terrain, LOD, far clip, atmosphere, FPS counter, HUD, mip streaming, SW3 height pre-pass), four headings each, everything restored. |
| `PerfSuite.Crowd(counts, seconds, hour, profile, keep, ab)` | Enemy count sweep: hides the scene's enemies, spawns goblins on a disc in front of the player, measures each count facing the crowd and away, plus enemy / Animator / skinning markers and a profiled breakdown at the largest count; `ab` measures runtime variants (culling, layers, shadows, Mesh LOD force / threshold / bias). Results in the aipocalypse-characters skill §5. |
| `PerfSuite.Fight(count, seconds, hour, distance, dumps, dumpMs, gc, aimHeight, standOut)` | A night raid fight: stands `standOut` m out from the home base along the raid `SpawnZone`, spawns `count` goblins through `EnemySpawner` (pool prewarmed like a raid, `AliveCap` + `CorpseHeadroom`) `distance` m further, keeps them topped up, and holds AK fire at the nearest (god mode, infinite ammo, no jams; stops a running raid and confirms a pending respawn first). Approach without fire, then the profiled fight: hits / kills / staggers, hit-stop count and frames, hit-path markers (`BulletSystem.Advance`, `DamageService.Deal` / `Publish`, `EnemySpawner.Spawn`, `EnemyPool.Prewarm`, enemy ticks), the worst frames' main-thread call trees, `gc` adds GC.Alloc stacks. Run once right after entering play (cold: first-use costs, mostly editor JIT) and again (warm). Results in ARCHITECTURE.md → Performance. |
| `PerfBuild.Build()` + `PerfSuite.Capture(raw, skip, depth, dumps)` | Profiling a development player instead of the editor (no EditorLoop, no editor JIT, IL2CPP): `PerfBuild` builds WorldMap to `Builds/Perf/` (scene list passed in, build profiles untouched); run it with Unity's profiler arguments (`-profiler-enable -profiler-log-file <x>.raw -profiler-capture-frame-count N -profiler-maxusedmemory 268435456`) and `-console "..."` commands to set up the scene (CONSOLE.md); `Capture` loads the `.raw` into the Profiler (edit mode) and reports frame time, main-thread game time (PlayerLoop minus the frame-cap / vsync / present waits), the per-item breakdown, top markers, render thread and the worst frames. |
| `PerfSuite.Gpu`, `SpikeDump`, `PickupDump`, `GcStacks`, `Movers`, `OnGuiUsers`, `Bare` | GPU Usage module per pass; the call tree of the worst or median frame; GC call stacks by mode; renderers that move each frame plus texture streaming state; live `OnGUI` users; bare frame time. |
| `PerfProbe.Run("name:x,z,yaw;…", frames)` | Teleports the player to each view (CharacterController off/on), settles 2 s, then median/p95 frame time, CPU/GPU from `FrameTimingManager`, draw calls. |
| `PerfAB.Run(outName, frames, from, to)` | A/B of runtime toggles (shadows, depth priming, LOD bias, terrain distances/density/shadows, render features, atmosphere) averaged over 4 yaws; "baseline" first and last to catch drift; snapshot/restore in `finally` (URP asset edits made in play mode persist to disk otherwise). Writes `%TEMP%/claude_perf_<outName>.txt`. |
| `PerfCapture.Run(outName, seconds, look)` | Idle / look / fire / look+fire phases through a virtual mouse on the real input path; `ProfilerDriver` capture without the editor; Batches, SetPass, draws, triangles, shadow casters, GC per frame. |
| `SceneScan.Run()` | Static dump: URP asset, renderer features, quality, cameras, lights, renderer and shadow-caster counts, top shaders, terrain draw settings, volumes. Good first step. |
| `Prof.Window/Compare/Spikes/Report` | Median CPU/GPU in a frame window, markers that grew most between two windows, spike frames. |

Traps:
- Play mode doesn't tick while Unity is in the background: `Application.runInBackground = true` **right after** `editor.playmode start` (in edit mode it writes ProjectSettings).
- `script run` budget: the CLI's `--timeout` (one timeout per request); async entries keep running after the CLI gives up, so also write results to a file.
- Modal dialogs (scene modified externally, save prompt before tests) block every command including `editor status`.
- Error Pause freezes play mode when firing logs an error. The player spawns empty-handed: give it a weapon by script to profile firing.
- Noise: a long-running editor session ran at 14–19 ms PlayerLoop vs 7.5–9 ms after a restart (broken audio device, memory eviction); baselines drift ~2 ms while warming up; the Profiler window itself causes 25–30 ms frames; an open Scene view is an extra camera; a selected Light slowed frames. Average several yaws (one spot drew 1.6k vs 3k calls by heading).
- Judge final numbers in a development build; in the editor close the Scene view and collapse the Hierarchy.
- While Unity is not the foreground app (`InternalEditorUtility.isApplicationActive` false), the main thread stalls about 500 ms every 0.5–1.5 s in `UpdateScene > WaitForLastPresentation`; `editor focus` does not fix it. `PerfSuite` subtracts that wait from PlayerLoop and counts stalls. Medians survive it, p95 / max do not.
- The GPU Usage profiler area inflates GPU time (5 ms vs FrameTiming's 2 ms) and the render thread, and it stays on after a capture: restore it (`PerfSuite.GpuArea(false)`). FrameTiming GPU reads ~0 for a while after a keyword or shader-variant change (shadows off, cookie off, terrain basemap); ignore those rows.
- Any memory-category `ProfilerRecorder` (GC Allocated In Frame) or a profiler capture adds `Profiler.FlushMemoryCounters` (~2.4 ms per frame, outside PlayerLoop). While the profiler records, Addressables' profiler emitter (`MonoBehaviourCallbackHooks.LateUpdate`) can spike 45–60 ms. Neither is game cost.
- `await` continuations run inside PlayerLoop (`ScriptRunDelayedTasks`); `ConfigureAwait(false)` moves them off the main thread. Sample and drive input from an `EditorApplication.update` callback, as `PerfSuite.Record` does.
