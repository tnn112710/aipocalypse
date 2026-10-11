---
name: tools-unity-messagepipe
description: MessagePipe (Cysharp) with VContainer for aipocalypse — registering brokers once in the root scope, defining gameplay event structs, publishing, subscribing and disposing from services, MonoBehaviours, unique entities and crowd entities, keyed/buffered/async variants, filters, testing and diagnostics. Use whenever code crosses a system boundary with an event (ShotFired, DamageDealt, EntityDied, TimePhaseChanged…) or adds a subscriber. NOT UniRx/R3 MessageBroker, NOT static event buses (GlobalMessagePipe.Get*, MMEventManager, C# static events).
---

# MessagePipe (aipocalypse)

Installed: **MessagePipe 1.8.2** + **MessagePipe.VContainer 1.8.2** (UPM git, tracks HEAD; source in `Library/PackageCache/com.cysharp.messagepipe@*/Runtime`), VContainer 1.19.0, UniTask 2.5.11. Namespace `MessagePipe`.

Project rules (ARCHITECTURE.md → Gameplay events): Core **announces** what happens, features **react**. Events are small structs, published through injected `IPublisher<T>` / `ISubscriber<T>`. Brokers live in the root scope. Events are for things that *happen*, never per-frame data. Inside one system, plain C# events are fine. MessagePipe is for crossing systems.

## 1. Setup: once, in the root scope only

```csharp
// _Core/_Bootstrap/ProjectLifetimeScope.cs (root, created by VContainerSettings)
protected override void Configure(IContainerBuilder builder)
{
    builder.RegisterMessagePipe(options =>
    {
#if UNITY_EDITOR
        options.EnableCaptureStackTrace = true;   // diagnostics window shows who subscribed; costs allocations on Subscribe
#endif
    });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Only so Window → MessagePipe Diagnostics works. Game code never calls GlobalMessagePipe.
    builder.RegisterBuildCallback(r => GlobalMessagePipe.SetProvider(r.AsServiceProvider()));
#endif
    // … other root services
}
```

Facts from the installed source:
- **Open generics are on.** `MessagePipe.asmdef` defines `MESSAGEPIPE_OPENGENERICS_SUPPORT` when VContainer ≥ 1.14 is installed, so `RegisterMessagePipe()` registers `IPublisher<>`, `ISubscriber<>` and the rest as open generics. Any event struct resolves with **no per-type `RegisterMessageBroker<T>` call**. Many online VContainer examples show per-type registration because they predate this, and it isn't needed here.
- Default `InstanceLifetime` is **Singleton**. Resolved from a child scope (`GameLifetimeScope`, an `EntityScope`), a root singleton comes from the root, so every scope shares one broker per event type. That is what "brokers live in the root" means.
- **Never call `RegisterMessagePipe()` again in a child scope.** Never register a broker in a child scope either (`RegisterMessageBroker<T>` in `GameLifetimeScope`, an installer or an `EntityScope`). A child registration makes VContainer build a *separate* broker for that scope, so publishers and subscribers in different scopes silently stop hearing each other.
- Don't switch `InstanceLifetime` to `Scoped`. Scoped resolves a new broker per scope, with the same split.
- `HandlingSubscribeDisposedPolicy` defaults to `Ignore`: subscribing to a broker whose scope was disposed returns a no-op. In practice this only happens if something outlives its scope, so treat it as a bug.

The `GameLifetimeScope` unload disposes game services, which dispose their subscriptions (§4). The root brokers stay alive across scene loads.

## 2. Defining events

```csharp
namespace Aipocalypse.Core.Events
{
    public readonly struct DamageDealt
    {
        public readonly IDamageable Target;
        public readonly DamageInfo Info;      // amount, source, hit point, type…
        public DamageDealt(IDamageable target, in DamageInfo info) { Target = target; Info = info; }
    }
}
```

- `readonly struct`, past-tense name (`ShotFired`, `EntityDied`, `ItemPicked`, `TimePhaseChanged`). Commands ("DoX") are method calls on an injected interface, not events.
- Core events in `_Core/_Common/Events/`. A feature's own events go in its folder (`Features/DayNight/Events/`). Core never subscribes to a feature's event.
- Payload = what subscribers need to react without querying back. Entity references go in as interfaces (`IDamageable`) or entity ids, not as concrete MonoBehaviours, so the Services layer stays clean. No strings built per publish, no collections allocated per publish.
- Publishing a struct allocates nothing (`MessageBrokerCore.Publish` loops over a handler array). Keep it that way: no boxing (don't publish as `object`), no closures per publish.

## 3. Publishing

```csharp
public sealed class WeaponService
{
    readonly IPublisher<ShotFired> _shotFired;
    public WeaponService(IPublisher<ShotFired> shotFired) => _shotFired = shotFired;

    void Fire(in ShotData shot)
    {
        // … rules …
        _shotFired.Publish(new ShotFired(shot.Weapon, shot.Origin, shot.Direction));
    }
}
```

- Handlers run **synchronously, in subscription order, inside `Publish`**. A slow subscriber slows the publisher.
- **A handler that throws stops the remaining handlers and the exception reaches the publisher** (no try/catch in `Publish`). Subscribers must not throw for expected cases. Guard and return.
- Publishing from inside a handler (event chains) works. Disposing a subscription during a publish is safe (removed slots are null-checked). A subscription added during a publish may or may not receive that same message.
- Avoid ping-pong chains across systems (A publishes → B handles and publishes → A handles…). If a feature needs to change the outcome of a Core action, use a seam interface (ARCHITECTURE.md → seams), not an event round-trip.

## 4. Subscribing and disposing

Every `Subscribe` returns an `IDisposable`. A subscription that's never disposed leaks its handler (and whatever it captures) into the root broker, which outlives the scene.

**Plain C# service** (registered in an installer). VContainer disposes `IDisposable` services when their scope ends:

```csharp
public sealed class LootDropper : IStartable, IDisposable
{
    readonly ISubscriber<EntityDied> _died;
    IDisposable _subscriptions;

    public LootDropper(ISubscriber<EntityDied> died) => _died = died;

    public void Start()
    {
        var bag = DisposableBag.CreateBuilder();
        _died.Subscribe(OnDied).AddTo(bag);
        // more subscriptions… .AddTo(bag)
        _subscriptions = bag.Build();
    }

    void OnDied(EntityDied e) { /* … */ }

    public void Dispose() => _subscriptions?.Dispose();
}
```

Subscribe in `IStartable.Start` (or the constructor), not lazily per frame. `Subscribe` allocates (handler wrapper, subscription, delegate). Pass a method group, and subscribe once.

**MonoBehaviour view / unique entity part** (injected by its `EntityScope`):

```csharp
IDisposable _subscription;

[Inject]
void Construct(ISubscriber<DamageDealt> damage) =>
    _subscription = damage.Subscribe(OnDamage, e => ReferenceEquals(e.Target, _health));   // predicate filter

void OnDestroy() => _subscription?.Dispose();
```

Dispose in `OnDestroy` (or `OnDisable`, if you subscribe in `OnEnable`). The predicate overload adds a filter object once at subscribe time. Filtering per publish is just a delegate call.

**Crowd entities (enemies, pickups, critters): don't subscribe per instance.** Hundreds of handlers on one broker make every publish loop over all of them, and pooled instances would need to resubscribe. Instead:
- The **manager subscribes once** (`EnemyManager`, `PickupManager`) and routes to the right instance via `EntityRegistry` or the event's target reference.
- Or, when many listeners each care about *their own* key, use a **keyed broker**: `ISubscriber<int, EnemyAlerted>` with `Subscribe(entityId, handler)`. Lookup is by key, not a loop over all handlers. Use only if the manager-routing approach gets awkward.

**Behavior Designer trees** don't subscribe. A unique agent's bridge (`AgentBrain`) subscribes and forwards into the tree; for crowd enemies `EnemyManager` subscribes once and calls the hit enemy's `EnemyBrain.Notify` (`tools-unity-behavior-designer` §3). **Feel players** don't subscribe either. A view subscribes and plays them (`tools-unity-feel`).

## 5. Variants: when to use which

| Interface | Use for |
| --- | --- |
| `IPublisher<T>` / `ISubscriber<T>` | Default. Something happened |
| `ISubscriber<TKey, T>` | Per-entity or per-channel events with many independent listeners (§4) |
| `IBufferedPublisher<T>` / `IBufferedSubscriber<T>` | **State-like** facts a late subscriber needs on subscribe: current `TimePhaseChanged`, `SessionStarted`. Holds the last value and replays it to new subscribers |
| `IAsyncPublisher<T>` / `IAsyncSubscriber<T>` | Rare. Subscribers that must finish async work before the publisher continues (e.g. "about to save", "scene unloading"). `await publisher.PublishAsync(msg, ct)` (default strategy Parallel; pass `AsyncPublishStrategy.Sequential` for ordered work). Always pass a `CancellationToken` |
| `subscriber.FirstAsync(ct, predicate)` | Await the next matching event once (tutorial steps, "wait until the boss dies"). Returns `UniTask<T>` |
| `IRequestHandler<TReq, TRes>` | Don't use. Inject an interface instead (clearer, testable, no hidden routing) |
| `GlobalMessagePipe.GetPublisher/GetSubscriber` | Never in game code (static service locator). Diagnostics setup only (§1) |

## 6. Filters

`MessageHandlerFilter<T>` (order + `Handle(message, next)`) can wrap handlers: logging, dropping events while paused. Prefer explicit `if` checks in the handler. Use a filter only for cross-cutting behaviour applied to many subscribers, and register it with `builder.RegisterMessageHandlerFilter<TFilter>()` in the root scope. Global filters (`options.AddGlobalMessageHandlerFilter<T>()`) run for every message of every type. Keep them out of builds.

## 7. Testing (EditMode)

Services that publish or subscribe are tested with a real container, with no mocks for MessagePipe:

```csharp
[Test]
public void LootDropper_DropsOnEnemyDeath()
{
    var builder = new ContainerBuilder();
    builder.RegisterMessagePipe();
    builder.Register<LootDropper>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
    // register fakes for LootDropper's other dependencies
    using var resolver = builder.Build();

    var dropper = resolver.Resolve<LootDropper>();
    dropper.Start();
    resolver.Resolve<IPublisher<EntityDied>>().Publish(new EntityDied(/* … */));

    // assert on the fake inventory / spawner
}
```

Test once when the bootstrap exists (guards the §1 scope rules): resolve `ISubscriber<T>` from a child scope (`resolver.CreateScope(_ => {})`), publish from the root, and assert the child's handler fired.

## 8. Diagnostics and debugging

- **Window → MessagePipe Diagnostics** (needs the `SetProvider` line from §1): live subscription counts per message type and, with `EnableCaptureStackTrace`, where each subscription was made. A count that grows each time you enter Play mode or reload World means a leaked subscription.
- Handler not called: subscriber resolved from a scope with its own broker registration (§1); subscription disposed early (stored in a local, or the bag rebuilt); predicate filter wrong; event published before the subscriber's `Start` (use a buffered broker if late subscribers need the last value).
- `NullReferenceException` inside a handler after a scene change: an undisposed subscription on a destroyed object.

---
Patterns written for this project from the MessagePipe 1.8.2 and VContainer 1.19.0 sources (`Library/PackageCache`). MessagePipe docs: https://github.com/Cysharp/MessagePipe.
