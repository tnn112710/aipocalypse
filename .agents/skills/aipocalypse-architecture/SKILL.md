---
name: aipocalypse-architecture
description: How aipocalypse wires its code — VContainer scopes and installers, entry points and central ticks, MessagePipe events, UniTask ownership and cancellation, TimeService pause / slow-mo, InputService-only input, Unity 6.6 project state. Use for any DI, registration, injection, gameplay event, subscriber or async / delay work, and any C# touching serialization, statics or build defines in this repo.
---

# Architecture rules in aipocalypse

Project layer on top of the library skills. Read those for the API: **unity-vcontainer** (VContainer 1.19), **unity-messagepipe** (MessagePipe 1.8.2), **unity-unitask** (UniTask 2.5.11), **unity-6-new-apis** (Unity 6.4–6.6). Where they and this file disagree, this file and `docs/technicals/` win. Enemy wiring: **aipocalypse-enemy-ai**.

Already documented. Read it, don't re-derive it:
- `AGENTS.md` → Key tech choices (input, DI, no singletons), Code style (base convention `oss-unity:csharp-unity` with the project's overrides: `_camelCase` fields, no `this.`/`private`, `[SerializeField] _field` + read-only property and never `[field: SerializeField]`; Dictionary serialization, no Odin serialization).
- `docs/technicals/ARCHITECTURE.md` → **Scopes and app flow** (what's built vs planned), **Gameplay events** (event list, `DamageService` / `AudioService` as single entry points), **Entities** (`EntityScope` parent lookup, defaults, execution order, strict mode), **Performance**, **Assemblies** (source generator off).
- `docs/technicals/CODE_STRUCTURE.md` → **Who depends on whom** (every registration per scope), **Frame order**, **Adding a script** (root vs scene installer, `DisableScriptModifier`).

The rest is what those docs don't spell out.

## 1. Scopes and installers

- **Root system:** `<System>Installer : IInstaller`, called from `ProjectLifetimeScope.Configure`. **Scene system:** a `SceneInstaller` component on the `GameLifetimeScope` GameObject. Never register a root service in a scene installer "to be safe": it shadows the root one.
- `SceneInstaller.Problem` returns why it can't install (unassigned reference) or `null`. Keep it a cheap, side-effect-free check: the scope skips the installer (warning; error when strict) and its services are simply absent.
- **Strict scope gotcha:** a strict `GameLifetimeScope` (World) errors for every non-abstract `SceneInstaller` type in the `MB13.Aipocalypse.Core` assembly it doesn't carry (found by reflection). Adding a new Core `SceneInstaller` makes every strict scene log an error until the component is added there.
- **Optional scene services** (`WorldClock`, `EnvironmentDirector`) can be missing in prototype scenes. Consumers inject `IObjectResolver` and call `TryResolve(out _x)` once in `Construct`, then handle null (`ClockHud`, `EnvironmentDirector`, `EnvironmentReactor`). Root services are always there: inject them directly. Exception: `EnvironmentDirector` also `TryResolve`s the root `GraphicsService`, because it runs in edit mode (`[ExecuteAlways]`) where nothing injects it.
- Root services never depend on scene services. Scene objects reach root services (the director requests `GraphicsService`; players register into `PlayerRegistry`).
- Cheat and console registrations are gated with `if (Cheats.Enabled)` inside the installer (`PlayerCheats`, `WorldClockCheats`, each system's `<System>Commands` console commands, the console itself in `ConsoleInstaller`): true in the editor and `ENABLE_CHEAT` builds (every development build, and distribution builds made with Cheat; [BUILD.md](../../../docs/technicals/BUILD.md)). `Debug.isDebugBuild` only for development tooling that isn't a cheat (the `GlobalMessagePipe` provider). Never `DEVELOPMENT_BUILD` (deprecated in 6.6). Something gameplay code may inject in every build but that only works with cheats is registered always and does nothing without them (`ScreenLog`). Console commands: [CONSOLE.md](../../../docs/technicals/CONSOLE.md#adding-a-command).
- `EntityScope.RegisterDefault` only throws when its *direct* parent is a strict `GameLifetimeScope`; in a scene with no `GameLifetimeScope` it falls back to the root and warns. Defaults go in `<System>/Defaults/` (`BaseStats`).
- `HudScope` is an `EntityScope` with no parts: the HUD runs in any scene on the parent's services.

## 2. Entry points and ticks

- `RegisterEntryPoint<T>().AsSelf()` whenever anything injects the concrete type (`TimeService`, `InputService`, `QuickTimeService`, `EffectPool`, `BulletSystem`, `WorldClock`, `AmbienceService`, `PlayerRespawnFlow`, `SpawnService`, `PrefabPool`, `PopulationDirector`, `FlockSystem`, `RaidDirector`, `AnimalManager`). Without `.AsSelf()` only its interfaces resolve (nothing injects `EnemyManager` or the cheats, so they're registered bare).
- Put the tick's logic in a public `Advance(deltaTime)` (or `AdvanceHitStop`) that `Tick()` calls with `Time.deltaTime`, so EditMode tests step it without a PlayerLoop (`EnemyManager`, `WorldClock`, `TimeService`, `RaidDirector`).
- A reader that must see input before `Player.Update` is an `ITickable` (VContainer ticks first). `QuickTimeService` uses that to `Consume` its button press.
- Bindings to registry state happen in `Start`, not `Construct`: scopes build in `Awake` order, so the player may not be registered when the HUD is injected (`PlayerHudView.Start → Rebind`).
- A base class names its `[Inject]` method uniquely (`PlayerHudView.ConstructPlayerHud`) so subclasses can still have their own `Construct`.
- Per-instance plain C# parts that need root services get them from a root factory service (`GunJamFactory` builds each gun's `GunJam`), not from the resolver.
- Third-party objects reach services through an injected bridge: `FeedbackContext` for Feel feedbacks (`MMF_HitStop`), `EnemyBrain` for Behavior Designer tasks.
- Crowd entities: injected once by their system's installer (scene-placed list collected on save / entering Play) or, when spawned, by the pool that creates them with the scene resolver (`SpawnService` → `PrefabPool`, `resolver.Instantiate`); no scope each, no per-instance subscriptions. Details in aipocalypse-enemy-ai.

## 3. Events (MessagePipe)

- Brokers: `RegisterMessagePipe()` once in `ProjectLifetimeScope` (stack traces in the editor). Nowhere else, including installers. Tests build `new ContainerBuilder()` + `RegisterMessagePipe()` (`CombatTests`, `WorldClockTests`).
- Event types: `readonly struct`, past tense, namespace `MB13.Aipocalypse.Core.Events` in `_Core/_Common/Events/`: mostly one file per area (`CombatEvents.cs`: `DamageDealt`, `PlayerDied`, `PlayerDeathSequenceEnded`; `PlayerEvents.cs`: active player and respawn flow; `QteEvents.cs`; `WorldTimeEvents.cs`; `WeatherEvents.cs`; `RaidEvents.cs`: raid start, waves and end with `RaidEndReason`; `DefenseEvents.cs`: a barrier broken / restored), plus one file per event for the gun jam pair (`GunJamBeat.cs`, `GunRepairCompleted.cs`). `_Common` events carry no weapon or feature types.
- "Something changed" events are empty or minimal; subscribers read the owner's current state (`ActivePlayerChanged` → `PlayerRegistry.Current`, `DayPhaseChanged` → `WorldClock.Phase`). No buffered brokers are used: late subscribers read the service on bind.
- Per-owner events carry an `Owner` and subscribers filter with the predicate overload (`GunJamFeedback` on `GunJamBeat` / `GunRepairCompleted`). `HitFeedbackHud` filters `DamageDealt` by `Info.Source`, `HurtFeedbackHud` by `Target` (the active player).
- Async events (`IAsyncPublisher`) are for steps the publisher must wait on: `PlayerRespawnFading`, `PlayerRespawning`. Handlers honour the token and check `this == null` after each await; a handler with nothing to await returns `UniTask.CompletedTask` (`DeathTimeRule`).

Who publishes → who subscribes today:

| Event | Publisher | Subscribers |
| --- | --- | --- |
| `DamageDealt` | `DamageService` | `HitFeedbackHud`, `HurtFeedbackHud`, `LandedHitFeedback` (its player's hits that stagger or headshot-kill) |
| `PlayerDied` | `PlayerLife` (publishers injected into `Player`) | `QuickTimeService` (cancels the QTE), `HudVisibilityHud` |
| `PlayerDeathSequenceEnded` | `PlayerLife` | `PlayerRespawnFlow` |
| `PlayerRevived` | `PlayerLife` | `PlayerRespawnFlow`, `HudVisibilityHud` |
| `ActivePlayerChanged` | `PlayerRegistry` | every `PlayerHudView`, `PlayerRespawnFlow`, `QuickTimeService` |
| `PlayerRespawnFading`, `PlayerRespawning` (async) | `PlayerRespawnFlow` | `DeathScreenHud`; `DeathTimeRule` (`PlayerRespawning`: ends a running raid, moves the clock) |
| `RespawnStageChanged` | `PlayerRespawnFlow` | `DeathScreenHud` |
| `QteStarted`, `QteAttempted`, `QteEnded` | `QuickTimeService` | `QteHud`; `GunJam` (`QteAttempted`) |
| `GunJamBeat`, `GunRepairCompleted` | `GunRepairFeedback` (per gun) | `GunJamFeedback` |
| `HourChanged`, `DayPhaseChanged`, `DayStarted`, `TimeJumped`, `TimeSkipEnded` | `WorldClock` | `EnemyBodyManager` (`DayPhaseChanged` at Dawn: bodies lying with loot sink); otherwise none yet (HUD, director, ambience and `RaidDirector` read `WorldClock` directly) |
| `BarrierBroken`, `BarrierRestored` | `BarrierSegment` | `BreachAlertHud` (`BarrierBroken`); the morning report later |
| `RaidStarted`, `RaidWaveStarted`, `RaidEnded` | `RaidDirector` | `RaidBannerHud` (`RaidStarted`, `RaidEnded` Cleared / Dawn); music and morning report later |
| `WeatherChanged` (empty: read `WeatherService`) | `WeatherService` | none yet (weather rules, HUD later) |
| `GameSaved` | `GameSession` (when a save's background write finishes, ok or not) | `SaveToastHud`, `PauseMenu` (→ `SaveSlotsPanel`) |

Update this table when you add an event or a subscriber.

## 4. Async, requests and pause

- Async exists in two places: `PlayerRespawnFlow` (one CTS per run, cancelled and disposed in `End` and `Dispose`; `async UniTaskVoid` + `.Forget()`) and `DeathScreenHud` (async handlers). Follow that shape for new flows. Everything per-entity or repeating is a central tick (`EnemyManager`, `EffectPool`'s timed returns on `Time.time`), never an awaited timer.
- **`UNITASK_DOTWEEN_SUPPORT` is off** (DOTween is the Asset Store copy in `Assets/Plugins/Demigiant`). Await a tween with `UniTask.WaitWhile(() => t.IsActive() && t.IsPlaying(), cancellationToken: ct)`. Turning the define on means adding it to the Standalone defines and updating PLUGINS.md in the same change.
- The root `SceneLoader` is the only place that loads scenes (through `GameSession` for loads, new games, Prototype and the main menu). A system that holds state across days saves it in its own `SaveSection` registered by its installer ([SAVE.md](../../../docs/technicals/SAVE.md#adding-a-system-to-the-save)); restore silently, and keep Unity object references out of saved data.
- **Owner-keyed requests:** `TimeService.Request` / `Release` (slow motion, slowest wins), `RequestPause` / `ReleasePause`, `RequestHitStop` (real time, ignored while paused); `InputService.RequestUI` / `ReleaseUI`, `RequestTextInput` / `ReleaseTextInput` (the dev console); `GraphicsService.RequestShadowDistance` / `ReleaseShadowDistance`; `WorldClock.RequestSpeed` / `Freeze` / `Release`. Pass `this` as owner and release on **every** exit path (end, cancel, `Dispose`, `OnDisable`). A missed release leaves the game slowed, paused or without gameplay input.
- **`TimeService` is the only writer of `Time.timeScale` and the fixed step.** Feel's time-scale feedbacks are banned; use `MMF_HitStop` (tools-unity-feel).
- **Pause = `timeScale` 0.** Scaled ticks freeze by themselves (`EnemyManager`, `WorldClock`, `BulletSystem`, effect lifetimes, scaled `UniTask.Delay`). Anything on unscaled time, frame waits or `WaitUntil` must check `TimeService.IsPaused` itself, as `QuickTimeService`, `QteHud`, `AmbienceService` and `Player` (breath sound, pose sequence) do. Nothing calls `RequestPause` outside tests yet (no pause menu).
- **Pooled objects never use `destroyCancellationToken`**: no pooled object awaits today; if one must, create a CTS on spawn and cancel + dispose it on return.

## 5. Unity 6.6 state of this project

- Editor: check `ProjectSettings/ProjectVersion.txt` (6000.6.7f1 when this was written).
- **Enter Play Mode Options:** enabled with flags `0`, so domain and scene still reload and statics reset. Mutable statics today: a type cache (`GameLifetimeScope._installerTypes`), editor-preview state (`EnvironmentDirector._editRevision`, `HandPreview`) and Feel's `MMF_HitStop.FeedbackTypeAuthorized`; the rest are readonly tables and `ProfilerMarker`s. Before anyone turns domain reload off, audit statics with `[AutoStaticsCleanup]` (unity-6-new-apis §2).
- Serialized key→value data uses the built-in `[SerializeField] Dictionary<,>` (in use: `ClockSettings.Phases`, `ClockStyle` phases, `TimeOfDayProfile.PhaseLooks`, `AmmoConfig` starting reserve, `EnemyDefinition` stats and targets, `EnemySystemSettings.AttackersPerTarget`, `EnemySpawnCard` variants). Enum-keyed tables get an EditMode test that every value has an entry (`PhaseDataTests`).
- Existing `[InitializeOnLoadMethod]` + `playModeStateChanged` hooks (`EnemyInstaller`, `EnvironmentDirector`, `HandPreview`) stay until that code is touched; new hooks use the 6.5 lifecycle attributes (`RaidInstaller`: `[OnCodeInitializing]` on a `partial` class, which the lifecycle source generator needs).
- Input is read only through `InputService` (no `OnMouseDown`, no `UnityEngine.Input`, no cursor or action-map changes elsewhere). The `Debug` map is enabled when `Cheats.Enabled`. The one sanctioned stand-in is `IPlayer.IntentSource`: a dev tool (`RaidAutopilot`, `cheat.autopilot`) implements `IPlayerIntentSource` and hands `Player` its `PlayerIntent` instead of the `InputService` read; it reads no devices itself, and `Player` still asks for intent only while it can act.
- URP custom passes use Render Graph (`FirstPersonRenderFeature.RecordRenderGraph` is the example).
- Editor log: `Logs/Editor.log` in the repo root (gitignored).
