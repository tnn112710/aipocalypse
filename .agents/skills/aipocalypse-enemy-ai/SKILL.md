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

- `BehaviorTree`: **Update Mode `Manual`**, **Start When Enabled off**, **Pause When Disabled off**, Evaluation Type `EntireTree`, and the kind's subtree assigned on the component (the capsule uses `Content/Enemies/_Shared/Wander.asset`, the goblins `Melee.asset`). Nothing assigns `Subgraph` at runtime; setup scripts set the serialized `m_Subtree` (`BuildGoblin.Upgrade`).
- `FollowerEntity` (radius 0.5, height 2 — the WorldMap graph is baked for this shape), local avoidance on, gravity on, `groundMask` = Default only.
- `EnemyMover` and `EnemyBrain` (`[RequireComponent]` enforces `BehaviorTree` + `EnemyMover`; `EnemyMover` requires `FollowerEntity`). `EnemyBrain._definition` set to the kind's `EnemyDefinition` (base stats for every `EnemyStat`, `Reaction`, `Wander`, and `Melee` when its tree fights), or it logs an error and never starts.
- `EnemyBody` with `_visual` (the child that sinks after death) and at least one non-trigger collider on the `Enemy` layer: that is what bullets and swings hit (`DamageService.DealHit` finds the `IDamageable` in the collider's parents). `EnemyCombatTests` checks both.
- Weak spots: a `Hitbox` (`_Common/Combat`) on a collider scales hits there and can make them critical. Animated kinds put a ×2 critical sphere on the head bone. No other collider may wrap it: the goblins' root capsule ends at the hips and a sphere on the upper chest covers the torso, all riding the bones so they follow the pose (`BuildGoblin.AddHeadHitbox`). Check with `ProbeCombat.HeadRays`; `EnemyCombatTests` checks the bind pose.
- Layer **`Enemy`**, which keeps the enemies out of the recast scan and out of grounding queries.
- No `EntityScope`, no `Seeker`/`RVOController`, no other per-instance plugin components, no `Update()`.
- Animated kinds (the Goblins) add `EnemyAnimator` and a nested `Model` child with the Animator, and their definition links `Animation` settings. Everything about the model and animation is in **aipocalypse-characters**.

## 2. Lifecycle and ticking (what tasks can rely on)

- **Injection first, then the tree.** `EnemyBrain.Construct` gets `EnemyRegistry` and the scene's `AstarPath`. Activation (`EnemyMover.Initialize` → `Tree.StartBehavior()` → `EnemyRegistry.Add`) waits for both injection and the brain's first `Start`, whichever comes last. So a task's `OnAwake`/`OnStart` always sees an initialized mover.
- **Stats per life.** Each activation builds a fresh `EnemyStats` from the definition (`EnemyBrain.Stats`). Read tunable numbers through it, never from the definition at runtime, and apply a temporary change as a modifier with a source (`AddModifier` / `RemoveModifiers(source)`); the brain re-applies `MoveSpeed` and `MaxHealth` on `Changed`. A new stat: append to `EnemyStat`, give every definition a base value, and apply it in `EnemyBrain.ApplyStat` (or where it's read).
- **Death.** `EnemyBody` at 0 HP: colliders off, `EnemyBrain.Die()` (leaves the registry, stops the tree and the mover, like a disable), death animation, then after `EnemyReactionSettings.CorpseTime` the visual sinks and the body disables the GameObject. Tasks never see a dead enemy (it's no longer ticked); systems that hold enemies check `EnemyBrain.IsDead`.
- **Enable/disable is the spawn/despawn switch.** `OnDisable` leaves the registry, `StopBehavior(false)` and halts the mover; `OnEnable` (after the first start) activates again. Re-activation re-runs `EnemyMover.Initialize`, which re-reads speed (the new `EnemyStats`) and stop distance and sets `Home = transform.position`. A future pool must therefore position the enemy **before** `SetActive(true)`, then `Teleport` the `FollowerEntity` after enabling (unity-astar → Pooling).
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
- Subtree assets live in `Content/Enemies/_Shared/` when shared, else in the enemy's folder. Propose the tree as text first (unity-behavior-designer-pro §4); the user builds or edits it in the Behavior Designer window, or a script generates it. The serialized task data is binary-encoded, so never hand-edit `.asset` YAML.
- **Generating a subtree** (`AgentScripts/Enemies/EnemyTrees.cs`, `Melee`): set `Subtree.EventNodes` (`Start { ConnectedIndex = 0 }`), `TreeLogicNodes` in depth-first order with `Index`, `ParentIndex` and `SiblingIndex` (the next sibling; `ushort.MaxValue` = none), one `LogicNodeProperties` per node (display name, position) and the event's `NodeProperties`, then `Serialize()` and save; rebuild the same asset in place to keep its GUID. Read one back with `Deserialize(true)` and `LogicNodes` to check it. A generated tree is overwritten on the next run, so window edits to it must go into the script.
- **Conditional aborts here:** a conditional re-checked by an abort runs `OnUpdate` again, so it may set brain state (`PlayerInReach` sets `Target`). An abort fires only when the conditional's result *changes*: a branch that fails while its conditional still succeeds falls to the next branch and stays there (the conditional didn't change). So fight tasks return Success to restart the fight (`MeleeAttack` when the player steps back), and Failure only when the target is gone.
- Trees store each task by its full type name (`Aipocalypse.Core.EnemySystem.ChooseWanderDestination` in `Wander.asset`). Renaming a task class or moving its namespace breaks every tree that uses it: rename only with the user, then re-open and fix the affected subtrees in the Behavior Designer window.

**New enemy kind:** `Content/Enemies/<Name>/` with the prefab (variant of an existing one where possible) and its `EnemyDefinition` asset; reuse a subtree, or add one plus its settings ScriptableObject (`Data/Enemies/` or shared) referenced from `EnemyDefinition`. Prefer new data over a new tree, a new tree over a new task. The GDD lists behaviour archetypes (CORE.md → Combat & enemies: rusher, tank, ranged, flyer, swarm, sneaker); their behaviour and numbers must be proposed and confirmed before building (AGENTS.md → Prompt first, GDD second). Update CODE_STRUCTURE.md and ARCHITECTURE.md → AI in the same change.

## 4. Wiring in this codebase

- **VContainer** (unity-vcontainer): `EnemyInstaller` is the only registration point (a `SceneInstaller` on the scene's `GameLifetimeScope`). Enemies are crowd entities: injected once, no scope of their own. Services reach tasks only through `EnemyBrain`: add the dependency to `EnemyBrain.Construct` (setup time), expose what tasks need. BD tasks are never injected.
- **Spawning:** placed enemies are collected by `EnemyInstaller` on scene save / entering Play (or its "Collect scene enemies" button); code spawning isn't built: when it is, spawn through the scene resolver (`resolver.Instantiate`, injected before `Awake`; an unused `EnemySpawner` doing that was removed, see git history). A plain `Instantiate` gives an un-injected enemy that logs an error and never thinks.
- **A\***: the installer registers the `AstarPath` from the scene's `Navigation` prefab instance; `EnemyBrain` passes it to `EnemyMover.Initialize`. PLUGINS.md's A\* row mentions an `INavigation` service; it doesn't exist yet. Until it does, keep A\* queries inside `EnemyMover` (or a new EnemySystem service) using the injected instance.
- **MessagePipe** (unity-messagepipe): no enemy events exist yet (a kill is `DamageDealt` with `Result.Killed`); shared event structs live in `_Core/_Common/Events/` (`DamageDealt`, `PlayerDied`, `DayPhaseChanged`…). When enemies need them: one subscriber in the enemy system routes to the affected `EnemyBrain`, which forwards to its tree (`EventHandler.ExecuteEvent` / `SetVariableValue`); no per-enemy subscriptions. Outbound: tasks call the brain or a service, which publishes.
- **Grass bending:** `EnemyBrain` `TryResolve`s the scene's `GrassBendField` (EnvironmentSystem, registered by `EnvironmentInstaller`) and joins it while active with `EnemyDefinition.GrassBendRadius`. Animals and other crowd movers do the same (`Add(transform, radius)` / `Remove`); never a Stylized Grass `GrassBender` per crowd entity.
- **Targeting the player:** `EnemyBrain.Player` is `PlayerRegistry.Current` (root scope, `TryResolve`d, so null in scenes without a player) as an `IPlayerTarget`; the engaged one is `EnemyBrain.Target` (set by `PlayerInReach`, cleared on death and disable). Check `IsDead` before acting on it.

## 5. Scenes and navigation

- Every scene with agents needs `_Core/EnemySystem/Prefabs/Navigation.prefab` (AstarPath + RVOSimulator) and an `EnemyInstaller` on its `GameLifetimeScope` with that `AstarPath` and the settings asset assigned (the installer reports a `Problem` otherwise).
- The prefab's default graph (small terrain-following grid, scanned on startup) is for small prototype scenes. WorldMap's instance holds the cached recast graph produced by the map generator: re-bake with the generator's `Navigation` steps after collider, terrain or water changes, never a hand scan without the water mesh (ENVIRONMENT.md → Navigation). Changing the enemy `FollowerEntity` radius/height means re-baking at the new size, or a second graph per size.
- Scenes today: only `Scenes/Prototypes/WorldMap.unity` (ten animated goblins outside the home gate; the capsules were removed, `WanderCapsule.prefab` remains as the bare contract). `Proto_Enemies` / `Proto_Stress` are planned (ROADMAP), not created. `Tests/PlayMode/WorldMapSmokeTests` asserts at least one registered enemy in WorldMap, so keep one placed there or update the test.

## 6. Performance

- No per-system enemy budget is written in ARCHITECTURE.md yet. The ROADMAP gate stands: 300 tree-driven enemies within budget in `Proto_Stress` before adding more archetypes; record the measured budget in ARCHITECTURE.md → Performance when it exists.
- First levers are the settings asset (`ThinkInterval`, `MaxThinksPerFrame`); then the escalation order in unity-behavior-designer-pro §5. Entity (ECS) tasks or baking agents to subscenes break "no ECS in our code" and need the user's sign-off.
- Profile with the Behavior Designer window closed and A\* gizmos off; watch `Aipocalypse.EnemyManager.Think`, `AIMovementSystemGroup`, `RVOSystem`.
- Known deviation: `WanderCapsule.prefab`'s `FollowerEntity.debugFlags` is `Path` (64); set it to none on enemies built from it (the Goblin prefabs have it off).

## 7. Not built yet (don't assume these exist)

Pooling, spawn rules and waves, enemy damage to the player (goblins notice, chase and swing at the player through the Melee subtree, but the swing hurts nothing; health, knockback, hit reactions and death exist: ARCHITECTURE.md → AI), line of sight and hearing (`PlayerInReach` is distance only), enemy damage resistances, the `EntityRegistry` spatial grid (ROADMAP → Foundation), enemy events, distance LOD, the `INavigation` service. Companions (drone, dog, cat) and bosses exist only in `docs/gdd/CORE.md` (bosses are an open question there): any behaviour taken from it is proposed and confirmed first. When they land, unique agents follow unity-behavior-designer-pro §1 (own `EntityScope`, `EveryFrame` or Manual from their own system), and their notes belong in ARCHITECTURE.md → AI and here.
