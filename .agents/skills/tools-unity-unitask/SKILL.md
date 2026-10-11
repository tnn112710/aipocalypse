---
name: tools-unity-unitask
description: UniTask 2.5 async/await for aipocalypse — where async belongs (loading, scene flow, prewarm, one-off sequences) and where it doesn't (per-entity gameplay timers → central ticks), cancellation tokens by owner (VContainer IAsyncStartable/Dispose, destroyCancellationToken, pooled objects), time scale vs TimeService pause, fire-and-forget safety, exceptions, MessagePipe/DOTween/Addressables interop, tracker window. Use for any async/await, delay, wait or coroutine-replacement work.
---

# UniTask (aipocalypse)

Installed: **UniTask 2.5.11** (UPM git, tracks HEAD; source in `Library/PackageCache/com.cysharp.unitask@*/Runtime`). Namespace `Cysharp.Threading.Tasks` (`.Linq` for async enumerables). The project uses UniTask instead of coroutines and `System.Threading.Tasks.Task` (ARCHITECTURE.md).

## 1. When to use async here, and when not

| Use UniTask | Don't. Use a central tick instead |
| --- | --- |
| Scene loading (`SceneLoader`), startup (`IAsyncStartable`), loading screens | Per-enemy / per-projectile timers, cooldowns, AI think delays (they belong in `EnemyManager` / service ticks) |
| Pool prewarm spread over frames | Anything that runs for hundreds of entities at once |
| One-off sequences: dusk → night transition, tutorial steps, death → respawn flow | Per-frame polling loops (`while (true) { …; await Yield(); }`) for gameplay. Make it an `ITickable` |
| UI flows: wait for a button, fade, then continue | Repeating gameplay timers (use a timestamp checked in a tick) |

Why: each running async method is a state machine plus a PlayerLoop registration, which is fine for a few and wasteful for hundreds. Central ticks also keep time-slicing and the frame budget in one place (ARCHITECTURE.md → Performance).

## 2. Cancellation: every await gets a token from its owner

| Owner | Token |
| --- | --- |
| VContainer entry point `IAsyncStartable.StartAsync(CancellationToken ct)` | `ct` (cancelled when the scope is disposed) |
| Plain C# service doing async later | Own `CancellationTokenSource _cts = new();`. `_cts.Cancel(); _cts.Dispose();` in `IDisposable.Dispose` |
| MonoBehaviour that is destroyed normally | Unity's `destroyCancellationToken` (or UniTask's `this.GetCancellationTokenOnDestroy()`) |
| **Pooled object** (enemy, projectile, VFX) | **Not** `destroyCancellationToken`, because pooled objects are never destroyed. Use a CTS created in `Spawn`, cancelled and disposed in `Despawn` (`tools-unity-object-pooling` §4) |
| Work bound to two lifetimes | `CancellationTokenSource.CreateLinkedTokenSource(a, b)`, disposed in `finally` |

```csharp
public sealed class NightTransition : IAsyncStartable
{
    readonly ISubscriber<TimePhaseChanged> _phase;
    public NightTransition(ISubscriber<TimePhaseChanged> phase) => _phase = phase;

    public async UniTask StartAsync(CancellationToken ct)
    {
        while (true)
        {
            var e = await _phase.FirstAsync(ct, p => p.Phase == DayPhase.Dusk);   // MessagePipe → UniTask
            await PrewarmNightAsync(e, ct);
        }
    }
}
```

- **Pass the token into the awaited call** (`UniTask.Delay(…, cancellationToken: ct)`, `FirstAsync(ct)`, `ToUniTask(cancellationToken: ct)`), not just a check after it. Otherwise the await keeps running after its owner is gone.
- Cancellation throws `OperationCanceledException`. That's normal flow, so don't log it as an error. At the top of a fire-and-forget method, catch it and return, or use `.SuppressCancellationThrow()` to get a `(bool IsCanceled, T Result)` tuple.
- Scope disposal doesn't wait for running awaits. The code after an await must assume the world may have changed (object despawned, scene unloading) unless the token says otherwise.

## 3. Time scale and pause (TimeService owns `Time.timeScale`)

- `UniTask.Delay(ms)` / `UniTask.WaitForSeconds(sec)` default to **scaled** time (`DelayType.DeltaTime`), so they **freeze while paused** and slow during hit stop. That's right for gameplay sequences.
- UI, menus and the pause screen: `UniTask.Delay(ms, ignoreTimeScale: true)` or `DelayType.UnscaledDeltaTime`. Otherwise a menu fade waits forever while paused.
- `DelayType.Realtime` (wall clock) only for things that must ignore frame time completely (network-style timeouts, none currently).
- Frame waits: `UniTask.Yield(ct)` (next PlayerLoop Update), `UniTask.NextFrame(ct)`, `UniTask.DelayFrame(n, cancellationToken: ct)`, `UniTask.WaitForFixedUpdate(ct)`, `UniTask.WaitForEndOfFrame(ct)` (or the `MonoBehaviour` overload). Frame waits keep running while paused.
- Conditions: `UniTask.WaitUntil(() => …, cancellationToken: ct)` / `WaitWhile`. The lambda runs every frame. Fine for one-offs, not as a substitute for events (prefer `ISubscriber<T>.FirstAsync`).

## 4. Fire-and-forget

```csharp
// Caller doesn't need the result: UniTaskVoid + .Forget(), with errors handled inside.
public void OnDeath() => RespawnFlowAsync(_cts.Token).Forget();

async UniTaskVoid RespawnFlowAsync(CancellationToken ct)
{
    try
    {
        await _fade.OutAsync(ct);
        await _sceneFlow.RespawnAsync(ct);
        await _fade.InAsync(ct);
    }
    catch (OperationCanceledException) { }
}
```

- Unhandled exceptions in forgotten tasks are not lost. They go to `UniTaskScheduler.UnobservedTaskException` and are logged by default (cancellations aren't, unless `PropagateOperationCanceledException`). Still catch them locally when the flow needs cleanup (fade back in, re-enable input).
- `async void` is never used. `async UniTaskVoid` is only for fire-and-forget entry points. Everything else returns `UniTask` / `UniTask<T>`.
- **Never await the same `UniTask` twice** (it's a pooled, single-use struct). To share a result, `.Preserve()` it or use `UniTaskCompletionSource`.
- **Never block** with `.GetAwaiter().GetResult()` on the main thread.

## 5. Combining

- `await UniTask.WhenAll(a, b, c)`. Tuple results for different types: `var (x, y) = await UniTask.WhenAll(ta, tb);`. **Rethrows the first exception** (not an `AggregateException`).
- `await UniTask.WhenAny(a, b)` returns the winner's index (plus results). Cancel the losers with a linked CTS.
- Timeouts: `.Timeout(TimeSpan)` throws `TimeoutException`. `.TimeoutWithoutException(...)` returns `(bool IsTimeout, T Result)`.
- Limit concurrency with a plain counter or a `Channel`. Don't use `SemaphoreSlim` + LINQ `Select(async …)` (allocations, thread-pool semantics).

## 6. Interop

- **Scenes:** `await SceneManager.LoadSceneAsync(name).ToUniTask(progress, cancellationToken: ct)`, only inside `SceneLoader` (no scene loads elsewhere).
- **MessagePipe:** `subscriber.FirstAsync(ct, predicate)` (await the next event), `IAsyncPublisher<T>.PublishAsync(msg, ct)` (see `tools-unity-messagepipe` §5).
- **DOTween:** UniTask's DOTween extensions (`await tween.ToUniTask(TweenCancelBehaviour.Kill, ct)`, `await tween.WithCancellation(ct)`) compile only with `UNITASK_DOTWEEN_SUPPORT`. That define is set automatically only for the UPM package `com.demigiant.dotween`. Our DOTween is the Asset Store copy in `Assets/Plugins/Demigiant`, so **it's off**. To use them, add `UNITASK_DOTWEEN_SUPPORT` to Standalone Scripting Define Symbols (the `DOTween.Modules` asmdef already exists) and note it in PLUGINS.md in the same change. Until then: `await UniTask.WaitWhile(() => tween.IsActive() && tween.IsPlaying(), cancellationToken: ct)`.
- **Addressables:** not used for now (ARCHITECTURE.md). If adopted: `await handle.ToUniTask(cancellationToken: ct)`, and release the handle on cancel.
- **uGUI:** `await button.OnClickAsync(ct)`. TMP input extensions exist (`External/TextMeshPro`).
- **Coroutines:** convert third-party coroutines with `IEnumerator.ToUniTask(this MonoBehaviour)`. Don't write new coroutines.
- **Threads:** `await UniTask.SwitchToThreadPool()` for pure C# work (no Unity API), then `await UniTask.SwitchToMainThread(ct)`. Burst jobs are preferred for heavy math (ARCHITECTURE.md escape hatch).

## 7. Allocation notes

- Awaiting `UniTask` directly allocates nothing in steady state (pooled promises). Lambdas capturing locals (`WaitUntil(() => _x > y)`) allocate a closure per call. Fine at startup, not in a per-frame path.
- `.AsTask()` and `Task`-returning APIs allocate. Keep them at boundaries.
- `UniTask.Delay(0)` isn't a frame wait. Use `UniTask.Yield()`.

## 8. Debugging

- **Window → UniTask Tracker**: live list of pending UniTasks with stack traces (enable tracking + stack trace in the window). Tasks that stay pending after leaving World are leaks, usually a missing token.
- Exception logged from "UniTaskScheduler": a forgotten task threw. Its stack trace shows the async method.
- Sequence stalls only while paused: scaled delay in a UI flow (§3).

---
Rewritten for this project from `tools-unity-unitask` in [tjboudreaux/cc-plugin-unity-gamedev](https://github.com/tjboudreaux/cc-plugin-unity-gamedev) (MIT, see `../THIRD_PARTY_LICENSE-unity-gamedev.txt`). API checked against the UniTask 2.5.11 source.
