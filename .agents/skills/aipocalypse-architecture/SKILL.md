---
name: aipocalypse-architecture
description: How aipocalypse wires its code — VContainer scopes and installers (ProjectLifetimeScope root installers, GameLifetimeScope + SceneInstaller components, strict mode, EntityScope / RegisterDefault, optional services via TryResolve), entry points and central ticks, MessagePipe event conventions and who publishes / subscribes what, UniTask ownership (PlayerRespawnFlow, async events, DOTween awaits), owner-keyed requests (TimeService pause / slow-mo / hit stop, InputService UI, GraphicsService), pause behaviour, pooled-object cancellation, and the Unity 6.6 project state (Enter Play Mode setting, statics, Dictionary serialization, Debug.isDebugBuild gating, InputService-only input, Logs/Editor.log). Use for any DI / registration / scope / installer / injection work, any gameplay event or subscriber, any async / delay work, and any C# touching serialization, statics, build defines or Unity 6.6 APIs in this repo, with the library skills unity-vcontainer, unity-messagepipe, unity-unitask and unity-6-new-apis.
---

# Architecture rules in aipocalypse

Project layer on top of the library skills. Read those for the API: **unity-vcontainer** (VContainer 1.19), **unity-messagepipe** (MessagePipe 1.8.2), **unity-unitask** (UniTask 2.5.11), **unity-6-new-apis** (Unity 6.4–6.6). Where they and this file disagree, this file and `docs/technicals/` win. Enemy wiring: **aipocalypse-enemy-ai**.

Already documented. Read it, don't re-derive it:
- `AGENTS.md` → Key tech choices (input, DI, no singletons), Code style (Dictionary serialization, no Odin serialization).
- `docs/technicals/ARCHITECTURE.md` → **Scopes and app flow** (what's built vs planned), **Gameplay events** (event list, `DamageService` / `AudioService` as single entry points), **Entities** (`EntityScope` parent lookup, defaults, execution order, strict mode), **Performance**, **Assemblies** (source generator off).
- `docs/technicals/CODE_STRUCTURE.md` → **Who depends on whom** (every registration per scope), **Frame order**, **Adding a script** (root vs scene installer, `DisableScriptModifier`).

The rest is what those docs don't spell out.

## 1. Scopes and installers

- **Root system:** `<System>Installer : IInstaller`, called from `ProjectLifetimeScope.Configure`. **Scene system:** a `SceneInstaller` component on the `GameLifetimeScope` GameObject. Never register a root service in a scene installer "to be safe": it shadows the root one.
- `SceneInstaller.Problem` returns why it can't install (unassigned reference) or `null`. Keep it a cheap, side-effect-free check: the scope skips the installer (warning; error when strict) and its services are simply absent.
- **Strict scope gotcha:** a strict `GameLifetimeScope` (World) errors for every non-abstract `SceneInstaller` type in the `Aipocalypse.Core` assembly it doesn't carry (found by reflection). Adding a new Core `SceneInstaller` makes every strict scene log an error until the component is added there.
- **Optional scene services** (`WorldClock`, `EnvironmentDirector`) can be missing in prototype scenes. Consumers inject `IObjectResolver` and call `TryResolve(out _x)` once in `Construct`, then handle null (`ClockHud`, `EnvironmentDirector`, `EnvironmentReactor`). Root services are always there: inject them directly.
- Root services never depend on scene services. Scene objects reach root services (the director requests `GraphicsService`; players register into `PlayerRegistry`).
- Dev-only registrations are gated with `if (Debug.isDebugBuild)` inside the installer (`PlayerCheats`, `WorldClockCheats`, the `GlobalMessagePipe` provider). Never `DEVELOPMENT_BUILD` (deprecated in 6.6).
- `EntityScope.RegisterDefault` only throws when its *direct* parent is a strict `GameLifetimeScope`; in a scene with no `GameLifetimeScope` it falls back to the root and warns. Defaults go in `<System>/Defaults/` (`BaseStats`).
- `HudScope` is an `EntityScope` with no parts: the HUD runs in any scene on the parent's services.

## 2. Entry points and ticks

- `RegisterEntryPoint<T>().AsSelf()` whenever anything injects the concrete type (`TimeService`, `InputService`, `QuickTimeService`, `EffectPool`, `BulletSystem`, `WorldClock`, `AmbienceService`, `PlayerRespawnFlow`). Without `.AsSelf()` only its interfaces resolve (nothing injects `EnemyManager` or the cheats, so they're registered bare).
- Put the tick's logic in a public `Advance(deltaTime)` (or `AdvanceHitStop`) that `Tick()` calls with `Time.deltaTime`, so EditMode tests step it without a PlayerLoop (`EnemyManager`, `WorldClock`, `TimeService`).
- A reader that must see input before `Player.Update` is an `ITickable` (VContainer ticks first). `QuickTimeService` uses that to `Consume` its button press.
- Bindings to registry state happen in `Start`, not `Construct`: scopes build in `Awake` order, so the player may not be registered when the HUD is injected (`PlayerHudView.Start → Rebind`).
- A base class names its `[Inject]` method uniquely (`PlayerHudView.ConstructPlayerHud`) so subclasses can still have their own `Construct`.
- Per-instance plain C# parts that need root services get them from a root factory service (`GunJamFactory` builds each gun's `GunJam`), not from the resolver.
- Third-party objects reach services through an injected bridge: `FeedbackContext` for Feel feedbacks (`MMF_HitStop`), `EnemyBrain` for Behavior Designer tasks.
- Crowd entities: injected once by their system's installer (scene-placed list collected on save / entering Play) or spawner (`resolver.Instantiate`); no scope each, no per-instance subscriptions. Details in aipocalypse-enemy-ai.

## 3. Events (MessagePipe)

- Brokers: `RegisterMessagePipe()` once in `ProjectLifetimeScope` (stack traces in the editor). Nowhere else, including installers. Tests build `new ContainerBuilder()` + `RegisterMessagePipe()` (`CombatTests`, `WorldClockTests`).
- Event types: `readonly struct`, past tense, namespace `Aipocalypse.Core.Events` in `_Core/_Common/Events/` (one file per area, e.g. `PlayerEvents.cs`). `_Common` events carry no weapon or feature types.
- "Something changed" events are empty or minimal; subscribers read the owner's current state (`ActivePlayerChanged` → `PlayerRegistry.Current`, `DayPhaseChanged` → `WorldClock.Phase`). No buffered brokers are used: late subscribers read the service on bind.
- Per-owner events carry an `Owner` and subscribers filter with the predicate overload (`GunJamFeedback` on `GunJamBeat` / `GunRepairCompleted`). `HitFeedbackHud` filters `DamageDealt` by `Info.Source`.
- Async events (`IAsyncPublisher`) are for steps the publisher must wait on: `PlayerRespawnFading`, `PlayerRespawning`. Handlers honour the token and check `this == null` after each await.

Who publishes → who subscribes today:

| Event | Publisher | Subscribers |
| --- | --- | --- |
| `DamageDealt` | `DamageService` | `HitFeedbackHud` |
| `PlayerDied` | `PlayerLife` (publishers injected into `Player`) | `QuickTimeService` (cancels the QTE), `HudVisibilityHud` |
| `PlayerDeathSequenceEnded` | `PlayerLife` | `PlayerRespawnFlow` |
| `PlayerRevived` | `PlayerLife` | `PlayerRespawnFlow`, `HudVisibilityHud` |
| `ActivePlayerChanged` | `PlayerRegistry` | every `PlayerHudView`, `PlayerRespawnFlow`, `QuickTimeService` |
| `PlayerRespawnFading`, `PlayerRespawning` (async) | `PlayerRespawnFlow` | `DeathScreenHud` |
| `RespawnStageChanged` | `PlayerRespawnFlow` | `DeathScreenHud` |
| `QteStarted`, `QteAttempted`, `QteEnded` | `QuickTimeService` | `QteHud`; `GunJam` (`QteAttempted`) |
| `GunJamBeat`, `GunRepairCompleted` | `GunRepairFeedback` (per gun) | `GunJamFeedback` |
| `HourChanged`, `DayPhaseChanged`, `DayStarted`, `TimeJumped`, `TimeSkipEnded` | `WorldClock` | none yet (HUD, director and ambience read `WorldClock` directly) |

Update this table when you add an event or a subscriber.

## 4. Async, requests and pause

- Async exists in two places: `PlayerRespawnFlow` (one CTS per run, cancelled and disposed in `End` and `Dispose`; `async UniTaskVoid` + `.Forget()`) and `DeathScreenHud` (async handlers). Follow that shape for new flows. Everything per-entity or repeating is a central tick (`EnemyManager`, `EffectPool`'s timed returns on `Time.time`), never an awaited timer.
- **`UNITASK_DOTWEEN_SUPPORT` is off** (DOTween is the Asset Store copy in `Assets/Plugins/Demigiant`). Await a tween with `UniTask.WaitWhile(() => t.IsActive() && t.IsPlaying(), cancellationToken: ct)`. Turning the define on means adding it to the Standalone defines and updating PLUGINS.md in the same change.
- No `SceneLoader` exists yet: when it lands, it's the only place that loads scenes.
- **Owner-keyed requests:** `TimeService.Request` / `Release` (slow motion, slowest wins), `RequestPause` / `ReleasePause`, `RequestHitStop` (real time, ignored while paused); `InputService.RequestUI` / `ReleaseUI`; `GraphicsService.RequestShadowDistance` / `ReleaseShadowDistance`; `WorldClock.RequestSpeed` / `Freeze` / `Release`. Pass `this` as owner and release on **every** exit path (end, cancel, `Dispose`, `OnDisable`). A missed release leaves the game slowed, paused or without gameplay input.
- **`TimeService` is the only writer of `Time.timeScale` and the fixed step.** Feel's time-scale feedbacks are banned; use `MMF_HitStop` (tools-unity-feel).
- **Pause = `timeScale` 0.** Scaled ticks freeze by themselves (`EnemyManager`, `WorldClock`, `BulletSystem`, effect lifetimes, scaled `UniTask.Delay`). Anything on unscaled time, frame waits or `WaitUntil` must check `TimeService.IsPaused` itself, as `QuickTimeService`, `QteHud`, `AmbienceService` and `Player` (breath sound, pose sequence) do. Nothing calls `RequestPause` outside tests yet (no pause menu).
- **Pooled objects never use `destroyCancellationToken`**: no pooled object awaits today; if one must, create a CTS on spawn and cancel + dispose it on return.

## 5. Unity 6.6 state of this project

- Editor: check `ProjectSettings/ProjectVersion.txt` (6000.6.7f1 when this was written).
- **Enter Play Mode Options:** enabled with flags `0`, so domain and scene still reload and statics reset. Mutable statics today: a type cache (`GameLifetimeScope._installerTypes`), editor-preview state (`EnvironmentDirector._editRevision`, `HandPreview`) and Feel's `MMF_HitStop.FeedbackTypeAuthorized`; the rest are readonly tables and `ProfilerMarker`s. Before anyone turns domain reload off, audit statics with `[AutoStaticsCleanup]` (unity-6-new-apis §2).
- Serialized key→value data uses the built-in `[SerializeField] Dictionary<,>` (in use: `ClockSettings.Phases`, `ClockStyle` phases, `AmmoConfig` starting reserve). Enum-keyed tables get an EditMode test that every value has an entry (`PhaseDataTests`).
- Existing `[InitializeOnLoadMethod]` + `playModeStateChanged` hooks (`EnemyInstaller`, `EnvironmentDirector`, `HandPreview`) stay until that code is touched; new hooks use the 6.5 lifecycle attributes.
- Input is read only through `InputService` (no `OnMouseDown`, no `UnityEngine.Input`, no cursor or action-map changes elsewhere). The `Debug` map is enabled when `Debug.isDebugBuild`.
- URP custom passes use Render Graph (`FirstPersonRenderFeature.RecordRenderGraph` is the example).
- Editor log: `Logs/Editor.log` in the repo root (gitignored).
