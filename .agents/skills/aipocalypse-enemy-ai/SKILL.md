---
name: aipocalypse-enemy-ai
description: How AI is built in aipocalypse — the EnemySystem (EnemyInstaller, EnemyRegistry, EnemyManager time-sliced ticking of Manual Behavior Designer Pro trees, EnemyBrain bridge, EnemyMover over A* FollowerEntity, EnemyDefinition data), the shared Wander subtree and its tasks, writing new enemy tasks and subtrees, adding an enemy kind, getting enemies into WorldMap and prototype scenes, the Navigation prefab and WorldMap's cached recast graph, VContainer/MessagePipe/A* wiring rules here, spawning and pooling, enemy performance gates, and what is not built yet (combat, senses, pooling, companions, drone, bosses). Use for any enemy, crowd, companion, NPC or boss movement, pathfinding, navmesh, behaviour tree or AI decision work in this repo, together with the library skills unity-behavior-designer-pro and unity-astar.
---

# Enemy AI in aipocalypse

Project layer on top of the library skills. Read those for the API: **unity-behavior-designer-pro** (BD Pro 3.3.3: tasks, trees, aborts, events, Manual mode) and **unity-astar** (A\* 5.4.7: FollowerEntity, graphs, queries, pooling). Where they and this file disagree, this file and `docs/technicals/` win.

What exists, file by file, is already documented. Read it, don't re-derive it:
- `docs/technicals/ARCHITECTURE.md` → **AI** (the wander slice, how a scene gets enemies, the tick budget, the Navigation prefab), **Entities** (crowd vs unique entities, `IPlayerTarget` / `PlayerRegistry`), **Performance** (allocation and central-tick rules), **Prototypes → main game**.
- `docs/technicals/CODE_STRUCTURE.md` → `_Core/EnemySystem/` rows, `Content/Enemies/`, the scope/installer and frame-order diagrams.
- `docs/technicals/PLUGINS.md` → A\* and Behavior Designer Pro rows, the plugin rules (per-instance plugin components on crowd entities: only `BehaviorTree` and `FollowerEntity`), the wander-demo / navmesh note.
- `docs/technicals/ENVIRONMENT.md` → **Navigation** (WorldMap's graph, water volume, `Navigation.Bake`, when to re-bake).
- `docs/ROADMAP.md` → `Proto_Enemies` (next enemy work and its gate).

The rest of this file is what those docs don't spell out: the contracts the code relies on, and the rules for extending it.

## 1. Enemy prefab contract

Checked against `Content/Enemies/WanderCapsule/WanderCapsule.prefab` and `_Core/EnemySystem/`:

- `BehaviorTree`: **Update Mode `Manual`**, **Start When Enabled off**, **Pause When Disabled off**, Evaluation Type `EntireTree`, and the kind's subtree assigned on the component (the capsule uses `Content/Enemies/_Shared/Wander.asset`). Nothing assigns `Subgraph` from code.
- `FollowerEntity` (radius 0.5, height 2 — the WorldMap graph is baked for this shape), local avoidance on, gravity on, `groundMask` = Default only.
- `EnemyMover` and `EnemyBrain` (`[RequireComponent]` enforces `BehaviorTree` + `EnemyMover`; `EnemyMover` requires `FollowerEntity`). `EnemyBrain._definition` set to the kind's `EnemyDefinition`, or it logs an error and never starts.
- Layer **`Enemy`**, which keeps the enemies out of the recast scan and out of grounding queries.
- No `EntityScope`, no `Seeker`/`RVOController`, no other per-instance plugin components, no `Update()`.

## 2. Lifecycle and ticking (what tasks can rely on)

- **Injection first, then the tree.** `EnemyBrain.Construct` gets `EnemyRegistry` and the scene's `AstarPath`. Activation (`EnemyMover.Initialize` → `Tree.StartBehavior()` → `EnemyRegistry.Add`) waits for both injection and the brain's first `Start`, whichever comes last. So a task's `OnAwake`/`OnStart` always sees an initialized mover.
- **Enable/disable is the spawn/despawn switch.** `OnDisable` leaves the registry, `StopBehavior(false)` and halts the mover; `OnEnable` (after the first start) activates again. Re-activation re-runs `EnemyMover.Initialize`, which re-reads speed/stop distance from the definition and sets `Home = transform.position`. A future pool must therefore position the enemy **before** `SetActive(true)`, then `Teleport` the `FollowerEntity` after enabling (unity-astar → Pooling).
- **Only `EnemyManager` ticks enemy trees.** Don't add an `Update`, a coroutine or a second ticker. Budget: `Data/Enemies/EnemySystemSettings.asset` (`ThinkInterval` 1/15 s, `MaxThinksPerFrame` 200). Consequences for tasks:
  - A `Running` task is re-entered about 15 times a second, not every frame, and less often past the per-frame cap. Check state (`Arrived`, `TimedOut`) and timestamps (`Time.time >= _until`); never accumulate per tick.
  - Scaled time: while `TimeService` pauses the game, trees don't think and `FollowerEntity` freezes too. Use `Time.time`/`Time.deltaTime`; never the built-in time-scale tasks.
  - A tree that disables its own enemy mid-tick is safe (swap-remove; the swapped-in enemy waits a turn).
  - `EnemyManager.Advance(deltaTime)` exists for tests; the profiler marker is `Aipocalypse.EnemyManager.Think`.

## 3. Writing tasks and subtrees here

Follow `_Core/EnemySystem/WanderTasks.cs`:
- Tasks are `ActionNode` / `ConditionalNode` (or `Action`/`Conditional` when stacking is wanted) in the namespace of the system that owns them (`Aipocalypse.Core.EnemySystem` today), with `[Category("Aipocalypse/<Subtree>")]`. Project code style applies (AGENTS.md → Code style), not Opsive's `m_` style.
- Cache `EnemyBrain` and `EnemyMover` in `OnAwake`. Tuning comes from `_brain.Definition` (and the settings asset it links, e.g. `Definition.Wander`), never literals in the task or the graph.
- Hand-offs between tasks go through brain properties (`EnemyBrain.Destination`), not blackboard variables; the trees have no shared variables today. Add a variable only when designers need to bind it.
- Movement only through `EnemyMover`; add a method there rather than touching `FollowerEntity` from a task. Undo in `OnEnd` (`Halt()`), which also runs on aborts.
- Navmesh queries go through `EnemyMover` with the injected `AstarPath` (see `TryFindDestination`: `GetNearest` + `PathUtilities.IsPathPossible`, capped attempts). Never `AstarPath.active`.
- Subtree assets live in `Content/Enemies/_Shared/` when shared, else in the enemy's folder. Propose the tree as text first (unity-behavior-designer-pro §4); the user builds or edits it in the Behavior Designer window. The serialized task data is binary-encoded, so never hand-edit `.asset` YAML.
- Trees store each task by its full type name (`Aipocalypse.Core.EnemySystem.ChooseWanderDestination` in `Wander.asset`). Renaming a task class or moving its namespace breaks every tree that uses it: rename only with the user, then re-open and fix the affected subtrees in the Behavior Designer window.

**New enemy kind:** `Content/Enemies/<Name>/` with the prefab (variant of an existing one where possible) and its `EnemyDefinition` asset; reuse a subtree, or add one plus its settings ScriptableObject (`Data/Enemies/` or shared) referenced from `EnemyDefinition`. Prefer new data over a new tree, a new tree over a new task. The GDD lists behaviour archetypes (CORE.md → Combat & enemies: rusher, tank, ranged, flyer, swarm, sneaker); their behaviour and numbers must be proposed and confirmed before building (AGENTS.md → Prompt first, GDD second). Update CODE_STRUCTURE.md and ARCHITECTURE.md → AI in the same change.

## 4. Wiring in this codebase

- **VContainer** (unity-vcontainer): `EnemyInstaller` is the only registration point (a `SceneInstaller` on the scene's `GameLifetimeScope`). Enemies are crowd entities: injected once, no scope of their own. Services reach tasks only through `EnemyBrain`: add the dependency to `EnemyBrain.Construct` (setup time), expose what tasks need. BD tasks are never injected.
- **Spawning:** placed enemies are collected by `EnemyInstaller` on scene save / entering Play (or its "Collect scene enemies" button); code spawning isn't built: when it is, spawn through the scene resolver (`resolver.Instantiate`, injected before `Awake`; an unused `EnemySpawner` doing that was removed, see git history). A plain `Instantiate` gives an un-injected enemy that logs an error and never thinks.
- **A\***: the installer registers the `AstarPath` from the scene's `Navigation` prefab instance; `EnemyBrain` passes it to `EnemyMover.Initialize`. PLUGINS.md's A\* row mentions an `INavigation` service; it doesn't exist yet. Until it does, keep A\* queries inside `EnemyMover` (or a new EnemySystem service) using the injected instance.
- **MessagePipe** (unity-messagepipe): no enemy events exist yet; shared event structs live in `_Core/_Common/Events/` (`DamageDealt`, `PlayerDied`, `DayPhaseChanged`…). When enemies need them: one subscriber in the enemy system routes to the affected `EnemyBrain`, which forwards to its tree (`EventHandler.ExecuteEvent` / `SetVariableValue`); no per-enemy subscriptions. Outbound: tasks call the brain or a service, which publishes.
- **Targeting the player:** use `IPlayerTarget` from `PlayerRegistry` (ARCHITECTURE.md → Entities), injected into the brain. Not wired into enemies yet.

## 5. Scenes and navigation

- Every scene with agents needs `_Core/EnemySystem/Prefabs/Navigation.prefab` (AstarPath + RVOSimulator) and an `EnemyInstaller` on its `GameLifetimeScope` with that `AstarPath` and the settings asset assigned (the installer reports a `Problem` otherwise).
- The prefab's default graph (small terrain-following grid, scanned on startup) is for small prototype scenes. WorldMap's instance holds the cached recast graph produced by the map generator: re-bake with the generator's `Navigation` steps after collider, terrain or water changes, never a hand scan without the water mesh (ENVIRONMENT.md → Navigation). Changing the enemy `FollowerEntity` radius/height means re-baking at the new size, or a second graph per size.
- Scenes today: only `Scenes/Prototypes/WorldMap.unity` (two capsules outside the home gate). `Proto_Enemies` / `Proto_Stress` are planned (ROADMAP), not created. `Tests/PlayMode/WorldMapSmokeTests` asserts at least one registered enemy in WorldMap, so keep one placed there or update the test.

## 6. Performance

- No per-system enemy budget is written in ARCHITECTURE.md yet. The ROADMAP gate stands: 300 tree-driven enemies within budget in `Proto_Stress` before adding more archetypes; record the measured budget in ARCHITECTURE.md → Performance when it exists.
- First levers are the settings asset (`ThinkInterval`, `MaxThinksPerFrame`); then the escalation order in unity-behavior-designer-pro §5. Entity (ECS) tasks or baking agents to subscenes break "no ECS in our code" and need the user's sign-off.
- Profile with the Behavior Designer window closed and A\* gizmos off; watch `Aipocalypse.EnemyManager.Think`, `AIMovementSystemGroup`, `RVOSystem`.
- Known deviation: `WanderCapsule.prefab`'s `FollowerEntity.debugFlags` is `Path` (64); set it to none before stress tests.

## 7. Not built yet (don't assume these exist)

Pooling, spawn rules and waves, enemy attack/health/combat, senses and the `EntityRegistry` spatial grid (ROADMAP → Foundation), enemy events, player targeting, distance LOD, the `INavigation` service. Companions (drone, dog, cat) and bosses exist only in `docs/gdd/CORE.md` (bosses are an open question there): any behaviour taken from it is proposed and confirmed first. When they land, unique agents follow unity-behavior-designer-pro §1 (own `EntityScope`, `EveryFrame` or Manual from their own system), and their notes belong in ARCHITECTURE.md → AI and here.
