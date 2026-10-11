---
name: tools-unity-vcontainer
description: VContainer 1.19 dependency injection for aipocalypse — the scope tree (ProjectLifetimeScope root via VContainerSettings, GameLifetimeScope per scene, EntityScope per unique entity, EntityInjector for crowds), per-system installers, lifetimes across child scopes, entry points (ITickable central ticks, IAsyncStartable), injecting MonoBehaviours and pooled prefabs, defaults-with-warning seams, strict mode, testing and diagnostics. Use for any registration, injection, scope, installer or "VContainerException" work.
---

# VContainer (aipocalypse)

Installed: **VContainer 1.19.0** (UPM git, pinned tag; source in `Library/PackageCache/jp.hadashikick.vcontainer@*/Runtime`). Namespaces `VContainer`, `VContainer.Unity`. The architecture is in **ARCHITECTURE.md → Scopes and app flow / Entities**. This skill is the how-to for it. If `_Core/_Bootstrap` and `_Core/_Common` already contain the scope classes, follow the existing code over the sketches here.

House rules (AGENTS.md, ARCHITECTURE.md):
- No `X.Instance` singletons, no `FindObjectOfType`, no static service access in game code. Services are plain C#, registered in scopes.
- **Each system registers through its own installer.** Cutting a feature = removing its installer.
- DI is setup-time only: resolve and inject at creation, never per frame.
- **The VContainer source generator stays off** (multi-assembly injector issue hadashiA/VContainer#822). Reflection injection is fine on PC.

## 1. The scope tree

```text
ProjectLifetimeScope (root)  ← VContainerSettings.RootLifetimeScope prefab, auto-created before any scene
  SettingsService · InputService · SceneLoader · SessionRequest · EntityRegistry · MessagePipe · LoadingScreen
  ├─ MainMenuLifetimeScope (MainMenu scene)
  └─ GameLifetimeScope (World / Proto_* scene) [DefaultExecutionOrder(-5000)]
       installers: Core systems + enabled Features; GameStarter entry point
       ├─ PlayerScope : EntityScope   (unique entity, child container)
       ├─ DogScope    : EntityScope
       └─ crowd entities (enemies, pickups): no scope; injected once by their pool / EntityInjector
```

- **Root:** `VContainerSettings` asset (in Preloaded Assets) → `RootLifetimeScope` = the `ProjectLifetimeScope` prefab. Every scene scope gets it as its parent automatically, so Play works from any scene.
- **Scene scopes** find the root on their own. No `parentReference` needed.
- **`EntityScope`** overrides `FindParent()` to return the scene's `GameLifetimeScope`, or `null` to fall back to the root. `GameLifetimeScope` needs `[DefaultExecutionOrder(-5000)]` so it has built its container before any entity scope's `Awake`. VContainer only defers building for `parentReference`, not `FindParent`.
- **Scene unload** disposes the scene scope: every `IDisposable` it created is disposed, then the GameObjects are destroyed. Root services survive.

## 2. Lifetimes, as they actually behave across scopes

From `Container.cs` (`ScopedContainer.ResolveCore`):

| Lifetime | Resolved from a child scope |
| --- | --- |
| `Singleton` registered in the **root** | The root's one instance. Truly shared |
| `Singleton` registered in a **child** | One instance in that child (shared by its descendants) |
| `Scoped` | **A new instance per scope that resolves it**, even if registered only in the parent. A `Scoped` root service resolved by 10 entity scopes is 10 instances |
| `Transient` | New every resolve |

So use `Singleton` by default. Use `Scoped` only when you *want* one per scope (e.g. a per-entity helper registered in `EntityScope.Configure`). Never register a service again in a child scope "to be safe": it shadows the parent's and splits state. MessagePipe has the same trap (`tools-unity-messagepipe` §1).

A root singleton must not depend on a scene-scope service (the root can't see children). Invert it: the scene service registers itself with the root one at `Start` and unregisters on `Dispose` (e.g. `EntityRegistry` in root, entities register into it).

## 3. Installers: one per system

```csharp
namespace Aipocalypse.Core.EnemySystem
{
    public sealed class EnemySystemInstaller : IInstaller
    {
        readonly EnemySystemSettings _settings;   // ScriptableObject from the scope's inspector
        public EnemySystemInstaller(EnemySystemSettings settings) => _settings = settings;

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_settings);
            builder.Register<EnemyFactory>(Lifetime.Singleton);
            builder.Register<INavigation, AstarNavigation>(Lifetime.Singleton);
            builder.RegisterEntryPoint<EnemyManager>().AsSelf();      // ITickable central tick
        }
    }
}
```

```csharp
[DefaultExecutionOrder(-5000)]
public sealed class GameLifetimeScope : LifetimeScope
{
    [SerializeField] EnemySystemSettings _enemySettings;
    [SerializeField] bool _strict;      // on in World (§6)
    // …

    protected override void Configure(IContainerBuilder builder)
    {
        new EnemySystemInstaller(_enemySettings).Install(builder);
        // other Core installers; Feature installers only if enabled (DevStartConfig / session)
        builder.RegisterEntryPoint<GameStarter>();
    }
}
```

- Registration API (checked in source): `Register<T>(Lifetime)`, `Register<TInterface, TImpl>(Lifetime)`, `.As<T>()`, `.AsImplementedInterfaces()`, `.AsSelf()`, `.WithParameter(...)`, `RegisterInstance(obj)` (plus `<TInterface…>` overloads), `RegisterEntryPoint<T>(Lifetime = Singleton)`, `RegisterComponent(component)`, `RegisterComponentInNewPrefab(prefab, Lifetime)`, `RegisterComponentOnNewGameObject<T>(Lifetime, name)`, `RegisterBuildCallback(Action<IObjectResolver>)`, `RegisterDisposeCallback(Action<IObjectResolver>)`, `RegisterEntryPointExceptionHandler(Action<Exception>)`, `UseEntryPoints(...)`, `UseComponents(...)`.
- Factories: `RegisterFactory<T>(Func<T>)` (**no lifetime parameter**, the func is the singleton), or `RegisterFactory<TParam, T>(Func<IObjectResolver, Func<TParam, T>> factoryFactory, Lifetime)` when the factory needs the container. For anything non-trivial (pools, spawning prefabs) write a factory **class** that takes `IObjectResolver` instead.
- Data: register ScriptableObjects with `RegisterInstance(asset)` from a serialized field. **No `Resources.Load`** (AGENTS.md / PLUGINS.md).
- **`RegisterComponentInHierarchy` searches the whole scene.** It's fine for one-per-scene objects in a scene scope (e.g. the HUD). **Never use it for entity parts.** It grabs another instance's component (ARCHITECTURE.md).

## 4. Entry points (no MonoBehaviour Update for services)

`IInitializable`, `IPostInitializable`, `IStartable`, `IPostStartable`, `IAsyncStartable` (`UniTask StartAsync(CancellationToken)`), `ITickable`, `IPostTickable`, `ILateTickable`, `IPostLateTickable`, `IFixedTickable`, `IPostFixedTickable`. They're registered with `RegisterEntryPoint<T>()`. VContainer drives them from the PlayerLoop (one loop for all, not per-object `Update`).

- Central ticks (`EnemyManager`, projectile service, pools' expiry lists) are `ITickable`. That is ARCHITECTURE.md's "no `Update()` of their own".
- Exceptions thrown in entry points are logged with `Debug.LogException` by default (`RegisterEntryPointExceptionHandler` to route elsewhere). A throwing `Tick` keeps ticking next frame.
- `IAsyncStartable.StartAsync` gets a token cancelled when the scope is disposed. Pass it to every await (`tools-unity-unitask`).
- Entry points are only created if registered with `RegisterEntryPoint`. A plain `Register<T>` for an `ITickable` never ticks (common bug). To expose it too, chain `.AsSelf()` / `.As<IFoo>()`.

## 5. Injecting MonoBehaviours

| Kind (ARCHITECTURE.md → Entities) | How it gets injected |
| --- | --- |
| Scene objects of a scene scope (HUD, `EnemyManager`'s scene view) | Scope's **Auto Inject Game Objects** list, or `RegisterComponent(…)` from a serialized ref |
| Unique entity (player, dog, drone) | Its own `EntityScope`: `RegisterBuildCallback(r => r.InjectGameObject(gameObject))` injects its hierarchy |
| Crowd entity, spawned | Pool injects **once at creation**: `resolver.InjectGameObject(instance)` (`tools-unity-object-pooling` §3) |
| Crowd entity, dropped in a scene by hand | `EntityInjector` on `Awake`: finds the nearest scope, injects once, hands itself to the manager |
| Plain objects (projectiles, VFX, props) | No injection. Data via `[SerializeField]` or from the spawner |

- MonoBehaviours use **method injection**: `[Inject] void Construct(IFoo foo, IBar bar)`. Constructor injection is impossible for them. Avoid `[Inject]` fields and properties. They hide dependencies and are easy to forget in tests.
- Plain C# services use **constructor injection** only.
- Injection happens before the object is used *only if* you arrange it. For prefabs: `resolver.Instantiate(prefab, parent)` (VContainer temporarily deactivates the prefab so `Awake`/`OnEnable` run after injection), or instantiate under an inactive parent then `InjectGameObject` (pools). Don't rely on `Start` running after injection by luck.
- Third-party objects that can't be injected (BD tasks, Feel feedbacks): use a bridge MonoBehaviour that *is* injected (`AgentBrain`, `FeedbackContext`).

## 6. EntityScope defaults and strict mode

```csharp
public abstract class EntityScope : LifetimeScope
{
    protected override LifetimeScope FindParent() =>
        Find<GameLifetimeScope>(gameObject.scene);           // null → VContainer falls back to the root

    protected override void Configure(IContainerBuilder builder)
    {
        RegisterParts(builder);
        builder.RegisterBuildCallback(r => r.InjectGameObject(gameObject));
    }

    protected abstract void RegisterParts(IContainerBuilder builder);

    // Register a default only if no ancestor provides the real service.
    protected void RegisterDefault<TService, TDefault>(IContainerBuilder builder) where TDefault : TService
    {
        if (Parent != null && Parent.Container.TryGetRegistration(typeof(TService), out _)) return;
        if (GameLifetimeScopeIsStrict) throw new VContainerException(typeof(TService), $"{name}: missing {typeof(TService).Name}");
        Debug.LogWarning($"{name}: using default {typeof(TDefault).Name} for {typeof(TService).Name}", this);
        builder.Register<TService, TDefault>(Lifetime.Singleton);
    }
}
```

- `LifetimeScope.Find<T>(scene)` exists in 1.19. `Parent` is set before `Configure` runs. `TryGetRegistration` on the parent container searches the whole parent chain.
- Strict mode (`GameLifetimeScope` in World): a default being used is an **error**, so a missing installer can't hide behind `InfiniteAmmo`/`BaseStats`. Prototype scenes are non-strict.
- Use defaults only at the entity's real seams (`IAmmoSource`, `IStatProvider`, `IPlayerLocator`…), never for core services like `EntityRegistry`.

## 7. Async work, scope disposal and lifetimes

- Services that start async work implement `IDisposable` and cancel their own `CancellationTokenSource` in `Dispose`, or take the token from `IAsyncStartable.StartAsync`. Scope disposal does **not** wait for pending awaits. A continuation that runs after disposal must see the cancelled token and stop.
- `Lazy<T>` injection is **not supported** by VContainer, so don't "fix" a circular dependency with it. Circular dependencies mean a missing seam. Split the shared part into a third service, or turn one direction into an event (MessagePipe).
- Don't hold `IObjectResolver` in services to resolve things later (service locator). The exceptions are factories and pools that instantiate or inject prefabs.
- `resolver.TryResolve<T>(out var x)` is built in (`IObjectResolverExtensions`). Don't add your own extension with the same name.

## 8. Testing

EditMode tests build a container directly. No scene and no `LifetimeScope` are needed:

```csharp
[Test]
public void Inventory_ConsumesAmmo()
{
    var builder = new ContainerBuilder();
    builder.Register<InventoryService>(Lifetime.Singleton);
    builder.RegisterInstance<IStatProvider>(new FakeStats());   // hand-written fake; no mocking library is installed
    using var resolver = builder.Build();

    var inventory = resolver.Resolve<InventoryService>();
    // act / assert
}
```

- Installer smoke test: `new ContainerBuilder()`, run the system's `Install(builder)` with test settings, `Build()`, and resolve each public service. This catches missing registrations without a scene.
- Child-scope behaviour: `resolver.CreateScope(b => …)` returns an `IScopedObjectResolver`. Use it to test shadowing and default rules.
- PlayMode smoke test (ARCHITECTURE.md → Prototypes): load World and each `Proto_*`, wait a frame, assert entities are injected and nothing was logged as an error.

## 9. Diagnostics and common errors

- **Window → VContainer Diagnostics** (enable `EnableDiagnostics` on the `VContainerSettings` asset, editor only): every scope, registration, resolve and instance.
- `VContainerException: No such registration of type: X`: no installer registers X in this scope or its ancestors; the scope found the wrong parent (check `FindParent` and the execution order); or the type lives in an assembly the installer doesn't reference.
- Service exists but is "a different instance" than expected: `Scoped` lifetime (§2), or a duplicate registration in a child scope.
- `NullReferenceException` in `Awake`/`OnEnable` of an injected component: it ran before injection (§5). Move the logic into `Construct` / a spawn method.
- An `ITickable` that never ticks: registered with `Register` instead of `RegisterEntryPoint`.
- `MissingReferenceException` after quitting to menu: a root service kept a reference to a scene object. Unregister in `OnDisable`/`Dispose`.

---
Rewritten for this project from `tools-unity-vcontainer` in [tjboudreaux/cc-plugin-unity-gamedev](https://github.com/tjboudreaux/cc-plugin-unity-gamedev) (MIT, see `../THIRD_PARTY_LICENSE-unity-gamedev.txt`). API checked against the VContainer 1.19.0 source.
