---
name: tools-unity-profiling
description: Profiling for aipocalypse's target (hundreds of enemies at 60 fps on PC) — Proto_Stress measurement protocol, ProfilerMarker conventions for central ticks, zero-GC-per-frame verification, reading A*/Burst job threads, in-game stats with ProfilerRecorder, memory leak checks with the Memory Profiler package, GPU/rendering tools (Frame Debugger, Rendering Debugger, GPUI), hitch hunting, and the Unity CLI get_performance_stats command. Use when measuring or optimising performance, adding markers, or judging whether a change is fast enough.
---

# Profiling (aipocalypse)

Target (ARCHITECTURE.md): **hundreds of active enemies at 60 fps** (16.6 ms/frame) on the target PC, **zero GC allocations per frame** in gameplay, measured in `Proto_Stress`, not assumed.

Tools available here: Unity Profiler (CPU, GPU, Memory, Rendering, Physics modules), **Memory Profiler package 1.1.12** (`com.unity.memoryprofiler`), Frame Debugger, URP **Rendering Debugger**, Render Graph Viewer, **Advanced FPS Counter** (CodeStage, dev-build overlay), and Unity CLI `unity command get_performance_stats` (render, memory, frame-timing snapshot). Not installed: Profile Analyzer, `com.unity.profiling.core` (so **no `ProfilerCounter<T>` / `ProfilerCounterValue<T>`**; those types aren't in the engine).

## 1. Measurement protocol (Proto_Stress)

Run it whenever enemy, navigation, spawning, weapon or rendering code changes (ARCHITECTURE.md → Measure).

1. **Development build** with *Autoconnect Profiler* (Build Profiles). Deep Profile **off**. Editor numbers are for quick looks only: the editor adds overhead and different GC and job-safety behaviour.
2. Before measuring: Burst compilation on. Jobs → Burst → **Safety Checks off** and Leak Detection off for the measuring session (turn them back on for development). A* agent debug rendering off. Scene view, Behavior Designer window and Frame Debugger closed.
3. `Proto_Stress` spawns **100 / 300 / 500** enemies attacking the player. For each tier, wait ~5 s for spawn/prewarm to settle, then capture ~300 frames.
4. Record per tier: median and 95th-percentile frame time, main thread ms, `Aipocalypse.EnemyManager.Tick` ms, A* movement (`AIMovementSystemGroup`, RVO) on worker threads, render thread ms, GPU ms, **GC Alloc per frame (must be 0)**, batches/SetPass calls.
5. Write the numbers into `docs/technicals/PERF.md` (create it on first run: date, commit, hardware, tier table). A change that regresses a tier needs an explanation in that file.

Starting budget at 300 enemies (refine with real data): main thread scripts ≤ 5 ms (of which `EnemyManager.Tick` ≤ 2 ms), physics ≤ 1.5 ms, animation ≤ 2 ms, render thread ≤ 5 ms, GPU ≤ 12 ms. Over budget means profile *before* optimising, and fix the biggest item first.

## 2. ProfilerMarkers: naming and placement

```csharp
using Unity.Profiling;

public sealed class EnemyManager : ITickable
{
    static readonly ProfilerMarker s_Tick   = new("Aipocalypse.EnemyManager.Tick");
    static readonly ProfilerMarker s_Think  = new("Aipocalypse.EnemyManager.Think");
    static readonly ProfilerMarker s_Senses = new("Aipocalypse.EnemyManager.Senses");

    public void Tick()
    {
        using var _ = s_Tick.Auto();
        using (s_Think.Auto()) { /* time-sliced tree.Tick() on enemy BD trees */ }
        using (s_Senses.Auto()) { /* grid queries + few raycasts */ }
    }
}
```

- Prefix **`Aipocalypse.`** so a Profiler hierarchy search finds all our markers at once.
- `static readonly`, created once. `Auto()` returns a struct, so there's no allocation. Markers are near-free when the profiler isn't recording, so leave them in.
- Put markers at **system granularity**: every central tick (`EnemyManager`, projectile service, pools' expiry), senses/targeting, spawning and despawning, `PrefabPool.Get/Release`, `WaveDirector`, `EntityRegistry` grid queries, scene-load steps. Not on tiny per-entity methods: thousands of samples per frame distort the capture.
- Markers on code inside Burst jobs: `ProfilerMarker` works in Burst too, and shows on worker threads.
- Don't use `Profiler.BeginSample(string)` (legacy). Use `ProfilerMarker` instead.

## 3. Zero allocations per frame

- Profiler CPU module → Hierarchy → sort by **GC Alloc**. Turn on *Call Stacks* for `GC.Alloc` to see the allocating line.
- After prewarm, a steady combat frame must show **0 B** under our markers. Allowed exceptions: editor-only code, UI text that changes (keep it rare), one-time spawn of a new pool item (shouldn't happen after prewarm).
- Usual culprits: LINQ, `foreach` over interfaces (boxing enumerators), closures/lambdas in hot paths, `string` concatenation or formatting, `params` arrays, boxing structs (event structs as `object`), `GetComponent` returning arrays, `Physics.*` non-`NonAlloc` calls, `new List` per call, `UniTask` lambdas per frame, coroutines' `WaitForSeconds`.
- Incremental GC is **on** (`gcIncremental: 1`). That spreads collection cost but doesn't excuse allocations.
- Quick check without the profiler: a `ProfilerRecorder` on `"GC Allocated In Frame"` (§5) in the debug overlay.

## 4. Reading A*, Burst and the job threads

- `FollowerEntity` movement runs in ECS systems (`AIMovementSystemGroup`, `RVOSystem`) on **worker threads**, so expand the Timeline view, not just the main-thread Hierarchy. Main-thread cost shows as **sync points** (`JobHandle.Complete`, `CompleteDependency`) inside our code when we read or write agent properties. Many small waits under `EnemyManager.Tick` means too much `FollowerEntity` property access (`tools-unity-astar` §4).
- Pathfinding runs on A*'s own pathfinding threads. A long `Pathfinding` thread with idle agents means too many repaths (check repath intervals and destination churn).
- Burst-compiled jobs show as `(Burst)`. If a job shows without it, Burst is off or the method failed to compile (see Jobs → Burst → Open Inspector).

## 5. In-game stats (dev builds, debug panel)

Use `ProfilerRecorder` (CoreModule) to read built-in counters with no package needed:

```csharp
ProfilerRecorder _mainThread, _gcAlloc, _batches;

void OnEnable()
{
    _mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
    _gcAlloc    = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
    _batches    = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
}

void OnDisable() { _mainThread.Dispose(); _gcAlloc.Dispose(); _batches.Dispose(); }
// Read _gcAlloc.LastValue, _batches.LastValue; average _mainThread samples (nanoseconds) for ms.
```

- Our own counts (active enemies, pool sizes, active agents) go in the debug panel directly from the services (`IDebugCommands`). No custom profiler counters (package not installed). If you need them in Profiler captures, add `com.unity.profiling.core` and note it in PLUGINS.md.
- **Advanced FPS Counter** (CodeStage) gives an FPS/memory overlay in dev builds. Keep it out of release builds.
- `FrameTimingManager` (CPU/GPU frame times in builds) returns nothing unless **Player Settings → Frame Timing Stats** is enabled (`enableFrameTimingStats` is currently **0**). Enable it only for profiling builds if needed.
- From the CLI (editor in Play mode): `unity command get_performance_stats` gives a structured snapshot of render, memory and frame timing. It's handy for quick before/after checks while iterating, not for budget numbers (editor).

## 6. Memory and leaks

- **Memory Profiler** (Window → Analysis → Memory Profiler): take a snapshot in World at the start of night 1 and after night 3. Compare. Growth in managed objects (`List`, closures, our services), GameObjects or textures between snapshots is a leak.
- Typical leaks here: pools growing every night (items not released, `tools-unity-object-pooling` §9), MessagePipe subscriptions not disposed (Diagnostics window counts, `tools-unity-messagepipe` §8), UniTasks pending forever (UniTask Tracker), root services holding scene objects after "quit to menu".
- Asset memory: textures (Synty/BK packs are large, so check import sizes and compression), audio import settings (BroAudio library entries), meshes with Read/Write enabled unnecessarily.

## 7. Rendering and GPU

- **GPU bound or CPU bound?** Compare GPU ms vs main/render thread ms in the Profiler (GPU module needs a supported graphics API in the build). If GPU ms is highest, optimise rendering, not scripts.
- **Frame Debugger**: draw call list, SRP Batcher breaks, overdraw sources. **Rendering Debugger** (URP): overdraw, lighting, material validation. **Render Graph Viewer**: URP passes and resource usage. Custom renderer features must validate (see the `unity:validate-urp-render-graph-renderer-feature` skill).
- Many enemies on screen: skinned mesh cost (bones, `Animator` culling mode, LOD groups), shadow casters (turn off shadows on far or small enemies), SRP Batcher-compatible materials.
- Foliage and props go through **GPU Instancer Pro**. Check its debugger/stats window for instance counts and culling.
- Post-processing (URP volumes, Kronnect, COZY clouds/fog) can dominate GPU time. Toggle volumes in the Rendering Debugger to measure each.

## 8. Hitches (single long frames)

Capture with the Profiler recording, then select the spike frame:
- `GC.Collect`: allocations (§3).
- `Instantiate` / `Awake`: pool not prewarmed (`tools-unity-object-pooling` §6), or heavy `Awake` work.
- `Shader.CreateGPUProgram` / PSO creation: first use of a shader variant. Prewarm via a shader variant collection / `GraphicsStateCollection` during loading.
- A* graph updates or scans: large `UpdateGraphs` bounds, or rescanning instead of `NavmeshCut`.
- `Physics.SyncTransforms`: many transforms moved, then queried in the same frame.
- Scene / asset loads on the main thread: should be async in `SceneLoader` behind a loading screen.

## 9. Writing performance-sensitive code: quick rules

- Measure first. Optimise the top item in the capture, re-measure, record in PERF.md.
- One loop over dense data beats many `Update()`s. Time-slice decisions, keep movement per-frame (ARCHITECTURE.md).
- Cache component and service references at setup. No `GetComponent`, `Find*` or DI resolve per frame.
- Escape hatch: when `EnemyManager.Tick` itself is the bottleneck, move its hot data into arrays and the loop into a Burst job (ARCHITECTURE.md), and profile again before and after.

---
Rewritten for this project from `tools-unity-profiling` in [tjboudreaux/cc-plugin-unity-gamedev](https://github.com/tjboudreaux/cc-plugin-unity-gamedev) (MIT, see `../THIRD_PARTY_LICENSE-unity-gamedev.txt`). Profiling types checked against the Unity 6000.6.5 `UnityEngine.CoreModule` docs and `ProjectSettings.asset`.
