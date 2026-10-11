---
name: tools-unity-object-pooling
description: Object pooling for aipocalypse with Unity's built-in UnityEngine.Pool — who owns which pool (system services, never a static PoolManager), PrefabPool with VContainer injection at creation, spawn/despawn contract and reset checklist (A* FollowerEntity, Behavior Designer, Feel, physics, VFX), central-tick despawn timers, async prewarm, scene lifetime, collection pools (ListPool/HashSetPool) and which plugins pool for themselves (BroAudio, Damage Numbers Pro, GPUI). Use when anything spawns repeatedly — enemies, projectiles, VFX, decals, pickups — or when a hot path needs a temporary collection.
---

# Object pooling (aipocalypse)

Use **Unity's built-in `UnityEngine.Pool`** (CoreModule, Unity 6000.6). Don't write a custom pool class, a static `PoolManager`, or extension methods like `prefab.SpawnPooled()`. ARCHITECTURE.md: *pool everything that spawns repeatedly, prewarm during loading or at dusk, spawning = take from pool + reset, no `Instantiate`, no allocation.*

## 1. Who owns which pool

Pools are owned by the system that spawns the thing, inside a plain C# service registered by that system's installer. No static access.

| Spawns | Pool owner | Notes |
| --- | --- | --- |
| Enemies | `EnemyFactory` (EnemySystem) | Injected once at creation (§3). `EnemyManager` ticks them and returns them on death |
| Projectiles | WeaponSystem (projectile service) | Ticked and expired by the service's loop, not per-projectile `Update` |
| Hit VFX, decals, shell casings | A VFX/decal service in `_Common` (or the owning system) | Plain objects, no injection |
| Loot pickups | The system that drops them (inventory / loot feature) | Crowd entity: injected once at creation |
| Damage numbers | **Damage Numbers Pro**, built-in pooling | Enable pooling on the DNP prefab. Don't wrap it in our pool |
| Audio sources | **BroAudio** | Never pool `AudioSource`s ourselves |
| Foliage / props | **GPU Instancer Pro** | Instanced, not pooled |
| Unique entities (player, dog, cat, drone, bosses) | Not pooled | Few instances. Spawn/destroy normally, or keep in scene and toggle |

Off-limits plugin pools: Feel's `MMObjectPooler` / `MMMiniObjectPooler` / `MMF_ParticlesInstantiation`, Behavior Designer's `PoolManager` / `Instantiate` / `Destroy` tasks.

## 2. `UnityEngine.Pool` API (checked against the 6000.6 docs)

- `new ObjectPool<T>(Func<T> createFunc, Action<T> actionOnGet = null, Action<T> actionOnRelease = null, Action<T> actionOnDestroy = null, bool collectionCheck = true, int defaultCapacity = 10, int maxSize = 10000)`
  - **`defaultCapacity` creates nothing.** It only sizes the internal storage. Instances are created lazily on `Get`, so prewarm yourself (§6).
  - `collectionCheck` throws on double release, **in the Editor only** (no cost in player builds). Keep it `true`.
  - When the pool already holds `maxSize` inactive items, a released item goes to `actionOnDestroy` instead. Size `maxSize` to the real peak from data (`WaveTable`), and destroy the GameObject in `actionOnDestroy`.
- `T Get()`, `PooledObject<T> Get(out T v)` (dispose to return; for short-lived scoped use), `void Release(T)`, `void Clear()`, `void Dispose()` (destroys all inactive items via `actionOnDestroy`).
- `CountAll`, `CountActive`, `CountInactive`.
- `LinkedPool<T>`: same idea, linked list, no capacity arg. Only useful when pool sizes swing wildly. Default to `ObjectPool<T>`.
- Static collection pools: `ListPool<T>`, `HashSetPool<T>`, `DictionaryPool<TKey, TValue>`, `CollectionPool<TCollection, TItem>` (cleared on release), `GenericPool<T>`, `UnsafeGenericPool<T>` (no collection check). Main thread only.

## 3. `PrefabPool<T>`: the one wrapper we keep

Put it in `Assets/_Game/_Core/_Common/Pooling/`. It's a plain class owned by a service, so it isn't registered as a global singleton.

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Aipocalypse.Core.Common.Pooling
{
    // One prefab's pool. Instances are created inactive and injected once; the owner activates them on spawn.
    public sealed class PrefabPool<T> : IDisposable where T : Component
    {
        readonly T _prefab;
        readonly Transform _root;            // active parent for pooled instances (scene object, see §7)
        readonly Transform _incubator;       // inactive parent: Instantiate under it so Awake/OnEnable don't run before injection
        readonly IObjectResolver _resolver;  // null for plain objects (VFX, decals)
        readonly ObjectPool<T> _pool;

        public PrefabPool(T prefab, Transform root, IObjectResolver resolver, int maxSize)
        {
            _prefab = prefab;
            _root = root;
            _resolver = resolver;
            var incubator = new GameObject($"{prefab.name}_Incubator");
            incubator.SetActive(false);
            incubator.transform.SetParent(root, false);
            _incubator = incubator.transform;
            _pool = new ObjectPool<T>(Create, null, OnRelease, OnDestroy, collectionCheck: true, maxSize: maxSize);
        }

        public int CountActive => _pool.CountActive;
        public int CountInactive => _pool.CountInactive;

        // Returns an INACTIVE instance. Caller positions and initialises it, then calls SetActive(true).
        public T Get() => _pool.Get();
        public void Release(T item) => _pool.Release(item);

        public async UniTask PrewarmAsync(int total, int perFrame, CancellationToken ct)
        {
            var batch = ListPool<T>.Get();
            try
            {
                while (_pool.CountAll < total)
                {
                    for (int i = 0; i < perFrame && _pool.CountAll < total; i++) batch.Add(_pool.Get());
                    foreach (var item in batch) _pool.Release(item);
                    batch.Clear();
                    await UniTask.Yield(ct);
                }
            }
            finally { ListPool<T>.Release(batch); }
        }

        T Create()
        {
            var item = Object.Instantiate(_prefab, _incubator);
            item.gameObject.SetActive(false);
            _resolver?.InjectGameObject(item.gameObject);
            item.transform.SetParent(_root, false);
            return item;
        }

        static void OnRelease(T item) => item.gameObject.SetActive(false);
        static void OnDestroy(T item) { if (item) Object.Destroy(item.gameObject); }

        public void Dispose()
        {
            _pool.Dispose();
            if (_incubator) Object.Destroy(_incubator.gameObject);
        }
    }
}
```

Why it's shaped this way:
- **Injection happens once, at creation, before `Awake`** (ARCHITECTURE.md → crowd entities). Instantiating under an inactive parent keeps `Awake`/`OnEnable` from running before `[Inject]`. `InjectGameObject` walks inactive children too.
- **`Get()` doesn't activate.** The owner sets position and data *first*, then activates. Activating at the old position and then moving breaks things. In particular, `FollowerEntity` creates its ECS entity in `OnEnable` at the current transform position.
- The resolver passed in is the owning scope's (`GameLifetimeScope` for World), so the injected services are the scene's.

## 4. Spawn / despawn contract

Each pooled component exposes an explicit spawn method that takes its data. Don't name it `Reset` (that's a Unity editor message, and it errors when it has parameters). Don't use `OnEnable` for spawn logic.

```csharp
// EnemyFactory (owner)
public Enemy Spawn(EnemyArchetypeDef def, Vector3 position, Quaternion rotation)
{
    var enemy = _pools[def].Get();                      // inactive
    enemy.transform.SetPositionAndRotation(position, rotation);
    enemy.gameObject.SetActive(true);                   // OnEnable: FollowerEntity creates its entity here
    enemy.Spawn(def, position);                         // data, Teleport, start tree, register in EntityRegistry
    return enemy;
}

public void Despawn(Enemy enemy)
{
    enemy.Despawn();                                    // undo everything Spawn did (checklist below)
    _pools[enemy.Def].Release(enemy);                   // deactivates
}
```

**Reset checklist.** `Despawn()` undoes what `Spawn()` did, and `Spawn()` sets every field it relies on:
- Gameplay state: health, timers, target, status effects, cached references to other entities.
- `EntityRegistry` unregister (or rely on `OnDisable` unregistering consistently, but pick one).
- **A\* `FollowerEntity`**: `Teleport(pos)` *after* activation in `Spawn`. Re-apply speed, stop distance and RVO settings. Clear `AIDestinationSetter.target` on despawn (`tools-unity-astar` §5).
- **Behavior Designer**: `StopBehavior()` on despawn. Set variables then `StartBehavior()` / `RestartBehavior()` on spawn (`tools-unity-behavior-designer` §3).
- **Feel** (unique or pooled view models only): `StopFeedbacksOnDisable` + `RestoreInitialValuesOnDisable` on their players.
- **MessagePipe**: crowd entities don't subscribe (the manager routes). If a pooled object must subscribe, subscribe in `Spawn`, dispose in `Despawn`.
- Physics: `Rigidbody.linearVelocity = angularVelocity = Vector3.zero`, re-enable colliders disabled on death, ragdoll back to kinematic.
- Visuals: Animator `Rebind()` + `Update(0f)` if state must restart. Shader properties (hit flash) back to neutral. `TrailRenderer.Clear()`, `ParticleSystem.Clear()` on reuse.
- UniTask work started by the instance: cancel its `CancellationTokenSource` on despawn (don't use `destroyCancellationToken`, since pooled objects are never destroyed).

**Releasing twice** or releasing an object that came from another pool corrupts state. `collectionCheck` catches the first in the Editor. Keep a `Def`/`Pool` back-reference on the instance so the owner knows which pool it came from.

## 5. Despawn timing: central, not per object

- No per-projectile or per-VFX `Update`, no `Invoke`, no coroutine `WaitForSeconds` timers (they allocate and scatter control).
- The owner keeps a dense list of active items with an expiry time and checks it in its single tick (`ITickable`). Swap-remove on expiry.
- Particle VFX: set `Stop Action = Callback` and have one small component forward `OnParticleSystemStopped` to the owner, or expire by the known duration in the owner's list.

## 6. Prewarming

- Prewarm counts come from data (wave/night tables), never magic numbers in code.
- When: World load (loading screen) for baseline counts, and **at dusk** for the night's wave size (ARCHITECTURE.md), spread across frames with `PrewarmAsync(total, perFrame, ct)`. Never at the moment a wave arrives.
- Each item goes through `Create` → injection, plus `Awake` on first activation. Watch the first-spawn frame in the profiler. If `Awake` work is heavy, move it to injection time or do a one-time warm activation during loading.

## 7. Scene lifetime

- Pool roots are scene objects (e.g. a `Pools` GameObject in World, or created by the owning service under the scene). They are never `DontDestroyOnLoad`.
- Pool-owning services live in `GameLifetimeScope` and implement `IDisposable` → `pool.Dispose()`. On "quit to menu" the scope disposes the services and the scene unload destroys the objects. Nothing survives into the next run.
- Prototype scenes: the owning system's installer (or the entity's default) creates its pools, so same code, no special prototype pool.

## 8. Collection pools for hot paths

For temporary lists in queries (spatial grid results, overlap results, targets in range):

```csharp
using (ListPool<IDamageable>.Get(out var hits))
{
    _registry.QueryRadius(point, radius, hits);
    for (int i = 0; i < hits.Count; i++) hits[i].TakeDamage(info);
}
```

- For code that runs **every frame on one owner** (`EnemyManager` tick), a reused member buffer (`readonly List<T> _buffer = new(256)`, cleared each use) is simpler and just as fast. Use `ListPool` for occasional, re-entrant or scattered uses.
- Never keep a pooled list after releasing it, and never release a list you still return to a caller.
- Physics: `Physics.OverlapSphereNonAlloc` / `RaycastNonAlloc` into a reused array. Batched queries (`RaycastCommand`) when counts grow (ARCHITECTURE.md).
- Strings: don't build strings per frame at all (debug UI excepted). No StringBuilder pool needed.

## 9. Debugging and verification

- Show `CountActive` / `CountInactive` per pool in the debug panel (`IDebugCommands`). A steadily climbing `CountAll` during a night means items aren't being released.
- `ProfilerMarker` around spawn/despawn in the owners. In `Proto_Stress`, the GC Alloc column for spawn frames must be 0 after prewarm.
- Common bugs: object appears for one frame at its old position (activated before positioning); enemy keeps the previous life's target or tree state (missing reset); `MissingReferenceException` after a scene change (a pool kept across scenes); "Trying to release an object that has already been released" (double despawn, e.g. death and timeout both releasing).

---
Rewritten for this project from `tools-unity-object-pooling` in [tjboudreaux/cc-plugin-unity-gamedev](https://github.com/tjboudreaux/cc-plugin-unity-gamedev) (MIT, see `../THIRD_PARTY_LICENSE-unity-gamedev.txt`). `UnityEngine.Pool` API checked against the Unity 6000.6.5 `UnityEngine.CoreModule.xml` docs.
