---
name: tools-unity-behavior-designer
description: Behavior Designer Pro 3.x (Opsive, DOTS-based) for aipocalypse — writing GameObject/Entity tasks, designing trees (aborts, events, subtrees, utility), wiring trees to VContainer/MessagePipe/A* FollowerEntity, pooling, Manual Tick, performance. Use for any behavior tree / enemy or companion AI design work with Behavior Designer. NOT classic Behavior Designer 1.x — its API (BehaviorDesigner.Runtime, SharedFloat, TaskCategory) does not exist here.
---

# Behavior Designer Pro (aipocalypse)

Installed: **Behavior Designer Pro 3.3.3** (`Packages/com.opsive.behaviordesigner`) + Graph Designer 3.3.4 + Opsive Shared. Unity 6000.6, Entities 6.6, A* 5.4.7. `GRAPH_DESIGNER` is defined for Standalone (built-in tasks compile only with it).

Before writing code, read:
- [references/api.md](references/api.md) — API **verified by reflection against 3.3.3**. Trust it over memory; most online examples are BD 1.x and will not compile.
- [references/patterns.md](references/patterns.md) — tree recipes for this game's archetypes.
- [references/entity-tasks.md](references/entity-tasks.md) — only if writing Burst/ECS tasks.

After a package update, re-verify the API: `unity command run_script --file .agents/skills/tools-unity-behavior-designer/scripts/BdApi.cs --entry BdApi.Dump --args '["Opsive.BehaviorDesigner.Runtime.BehaviorTree"]'` (or `BdApi.Find '["Sequence"]'`). Sources for anything else: built-in tasks `Packages/com.opsive.behaviordesigner/Runtime/Tasks/**` (full C#), samples `Samples~/Scripts/**`, docs https://opsive.com/support/documentation/behavior-designer-pro/.

## 1. Decide first: should this be a behavior tree at all?

| Agent | Tool | Why |
| --- | --- | --- |
| Crowd enemies (rusher, tank, ranged, flyer, swarm, sneaker) — hundreds | **Code** in `EnemyManager` (state per archetype, data in `EnemyArchetypeDef`) | ARCHITECTURE.md: no per-enemy `Update`, time-sliced thinking, zero alloc. AGENTS.md: no per-instance plugin components on crowd entities |
| Unique agents — drone, dog, cat, bosses, elites, set-piece NPCs | **Behavior tree** (GameObject tasks) | Few instances, rich branching, designer iteration |
| Crowd via BD (only if a `Proto_Stress` run proves it) | `UpdateMode.Manual` + `EnemyManager` calls `Tick()` staggered, or Entity tasks | Must keep the central tick and budget |

If the user asks for a tree on a crowd enemy, point this out and confirm before proceeding. Unity Behavior (`com.unity.behavior`) is also installed — the project should standardise on one tree tool; check `docs/technicals/PLUGINS.md` (create/update it when the choice is made).

## 2. Task basics (GameObject tasks)

```csharp
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime.Variables;
using Opsive.Shared.Utility;
using UnityEngine;

namespace Aipocalypse.AI.Tasks
{
    [Category("Aipocalypse/Combat")]
    [Description("Moves to the target with the A* agent. Success on arrival, Failure if the target is lost.")]
    public class MoveToTarget : Action
    {
        [Tooltip("Target to chase.")]
        [SerializeField] SharedVariable<GameObject> _target;
        [Tooltip("Stop when closer than this.")]
        [SerializeField] SharedVariable<float> _stopDistance = 2f;
        [SerializeField] float _repathInterval = 0.5f;

        Pathfinding.IAstarAI _agent;
        float _nextRepath;

        public override void OnAwake() => _agent = GetComponent<Pathfinding.IAstarAI>();

        public override void OnStart()
        {
            _agent.isStopped = false;
            _nextRepath = 0f;
        }

        public override TaskStatus OnUpdate()
        {
            var target = _target.Value;
            if (target == null) return TaskStatus.Failure;

            var toTarget = target.transform.position - m_Transform.position;
            if (toTarget.sqrMagnitude <= _stopDistance.Value * _stopDistance.Value) return TaskStatus.Success;

            if (Time.time >= _nextRepath)
            {
                _agent.destination = target.transform.position;
                _nextRepath = Time.time + _repathInterval;
            }
            return TaskStatus.Running;
        }

        public override void OnEnd() => _agent.isStopped = true;

        public override void Reset()
        {
            _target = null;
            _stopDistance = 2f;
            _repathInterval = 0.5f;
        }
    }
}
```

Rules:
- Base classes: `Action` / `Conditional` (stackable), `ActionNode` / `ConditionalNode` (not stackable), `CompositeNode` / `DecoratorNode` for custom flow (rare — prefer built-ins).
- Inputs designers tune or wire are `[SerializeField] SharedVariable<T>` (assign a literal or bind to a blackboard variable in the inspector). Constants that never vary per tree can be plain `[SerializeField]` fields.
- Lifecycle: `OnAwake` (tree init — cache components) → `OnStart` (task entered) → `OnUpdate` returns `Running/Success/Failure` → `OnEnd` (task left, **also on abort** — always undo side effects here: stop agent, release locks). `OnBehaviorTreeStarted/Stopped(bool paused)` for tree-level state. `Reset()` = inspector defaults.
- Inherited fields: `m_GameObject`, `m_Transform`, `m_BehaviorTree`; `GetComponent<T>()` / `TryGetComponent`. Cache in `OnAwake`, never `GetComponent` in `OnUpdate`.
- Physics callbacks are opt-in: override `ReceiveTriggerEnterCallback => true` then `OnTriggerEnter`. Prefer gameplay events instead (§4).
- Conditionals used under aborts are re-run every tick: keep them cheap; override `OnReevaluateUpdate` only if the abort check should differ.
- Zero allocations in `OnUpdate`/`OnReevaluateUpdate`: no LINQ, no `new` collections, `NonAlloc` physics, no string building.
- Code style: project style (no XML docs, `_camelCase` privates, namespace `Aipocalypse.*`), not Opsive's `m_` style. Exception: inherited `m_Transform` etc.
- Put attributes `[Category("Aipocalypse/...")]` so our tasks group together in the task list.

## 3. Wiring a tree into this architecture

Tasks are plain objects created by BD's deserializer — **VContainer cannot inject them**. Use one bridge component per agent:

```csharp
namespace Aipocalypse.AI
{
    // Lives on the agent root next to BehaviorTree. Injected by the entity's EntityScope; owns all service access for its tree.
    public class AgentBrain : MonoBehaviour
    {
        [SerializeField] BehaviorTree _tree;           // Opsive.BehaviorDesigner.Runtime.BehaviorTree; Start When Enabled = OFF

        public IPlayerLocator Player { get; private set; }
        public EntityRegistry Registry { get; private set; }
        IPublisher<EntityDied> _diedPublisher;
        System.IDisposable _subscriptions;

        [Inject]
        void Construct(IPlayerLocator player, EntityRegistry registry,
                       IPublisher<EntityDied> diedPublisher, ISubscriber<DamageDealt> damageSubscriber)
        {
            Player = player;
            Registry = registry;
            _diedPublisher = diedPublisher;
            _subscriptions = damageSubscriber.Subscribe(OnDamage);    // filter to self inside
            _tree.SetVariableValue("Player", player.Transform.gameObject);
            _tree.StartBehavior();                                    // start only after injection
        }

        // Called by tasks — tasks never publish directly.
        public void ReportDeath() => _diedPublisher.Publish(new EntityDied(/* ... */));

        void OnDamage(DamageDealt e)
        {
            if (e.Target != gameObject) return;
            Opsive.Shared.Events.EventHandler.ExecuteEvent(_tree, "Damaged", e.Amount);
        }

        void OnDestroy() => _subscriptions?.Dispose();
    }
}
```

(Event/interface names above are placeholders — use the real ones from `_Core/_Common`.)

- **Start order:** turn **Start When Enabled off** and call `StartBehavior()` from the bridge after injection. Otherwise `OnAwake` of tasks can run before VContainer has injected the bridge.
- Tasks reach services via `GetComponent<AgentBrain>()` in `OnAwake`, never via statics, `FindObjectOfType`, or BD's `FindGameObject*` tasks.
- **Inbound gameplay → tree:** MessagePipe subscriber in the bridge → `EventHandler.ExecuteEvent(tree, "Name", args)` (reacting) or `tree.SetVariableValue("Name", value)` (state). Tree side: `HasReceivedEvent` under a conditional abort, or an `OnReceivedEvent` event branch.
- **Tree → gameplay:** a task calls a method on the bridge, which publishes the MessagePipe struct event. Tasks never hold `IPublisher` directly unless they get it from the bridge.
- **Blackboard scope:** use **Graph/GameObject** variables for per-agent state. Avoid Scene/Project-scope shared variables for game state (hidden global state; the sample's `SceneSharedVariables` lookup uses `FindAnyObjectByType`). Game state lives in services/State classes.
- **Data:** tuning numbers come from the agent's ScriptableObject def (copied into variables by the bridge at start), not hardcoded in the graph, so the "every number lives in data" rule holds.
- **Navigation:** `FollowerEntity` implements `Pathfinding.IAstarAI` — write movement tasks against `IAstarAI` (`destination`, `isStopped`, `reachedDestination`, `remainingDistance`, `pathPending`, `SearchPath()`, `Teleport`). Never the built-in `NavMesh/*` tasks. Opsive also ships an A* integration (separate download from Opsive Downloads, adds a pathfinder adapter for its Movement/Tactical add-ons) — only relevant if those packs are bought.
- **Time:** use `Time.deltaTime`/`Time.time`; `TimeService` owns `Time.timeScale` — never use the built-in `Time/SetTimeScale`, `SlowMotionEffect`, `TimeScaleCurve` tasks.
- **Pooling:** built-in `Instantiate`, `Destroy`, `CloneGameObject`, `SpawnAtPosition`, `PoolManager` tasks are off-limits — spawn through our pools via the bridge. Despawn: `StopBehavior()`; respawn: set variables then `StartBehavior()` (or `RestartBehavior()`).
- **Other off-limits built-ins:** `SaveLoad/*` PlayerPrefs tasks (Easy Save 3 / Persistence feature owns saving), `Audio/*` (BroAudio), `SceneManagement/*` (SceneLoader). Feel has an Opsive bridge (separate download) — otherwise trigger feedbacks through the bridge/events like the rest of the game.
- **Assemblies:** Opsive's runtime DLLs are auto-referenced. An asmdef only needs explicit refs for Opsive *source* asmdefs (e.g. `Opsive.BehaviorDesigner.Runtime.Tasks.Actions` to subclass a built-in) and, for Entity tasks, `Unity.Entities`, `Unity.Burst`, `Unity.Collections`, `Unity.Mathematics`. Generic AI tasks: `Assets/_Game/_Core/EnemySystem/AI/`; feature-specific (drone, pets) in that feature's folder — Features → Core only.

## 4. Designing trees

Design the tree in text first, get agreement, then the user builds it in the Behavior Designer window (trees serialize inside the `BehaviorTree` component on the prefab; don't hand-edit the YAML). Write specs like:

```text
Root: Selector (abort: none)
├─ Sequence [abort: LowerPriority]   "Flee when hurt"
│   ├─ FloatComparison  Health/Max < 0.25
│   └─ Flee (to SafePoint)
├─ Sequence [abort: Both]            "Fight"
│   ├─ CanSeeTarget (Player)
│   ├─ Cooldown 1.2s → AttackTarget
│   └─ MoveToTarget
└─ Patrol (waypoints)                 fallback
```

- Priority runs left → right; fallback last. Every branch must have a terminating fallback so the root never idles with Failure.
- **Conditional aborts** (`None`, `Self`, `LowerPriority`, `Both`) are set on the composite. Use the narrowest one: `LowerPriority` to preempt patrol when the player appears; `Self` to drop out of a branch when its condition fails; `None` for branches that must finish (attack wind-ups). Put expensive conditionals (raycasts) after cheap ones (distance, flags).
- **Events over polling:** damage, alerts, "fire lit", day/night phase → events into the tree, not conditionals polling services each tick.
- **Utility choices** (pick the best need/attack): `UtilitySelector` with `UtilityEvaluator`/`UtilityCurveEvaluator` children; `PrioritySelector` + `PriorityEvaluator` for scored priorities.
- **Subtrees** for reusable chunks (Flee, Search, Patrol) and for phase swaps (boss phases, drone moods): `SubtreeReference` / `SubtreeReferenceSelector` driven by an int variable + `tree.ReevaluateSubtreeReferences()`.
- Keep trees small: one tree's state fits ~191 tasks (127 in Count mode). Split into subtrees well before that.
- Ready-made recipes: [references/patterns.md](references/patterns.md).

## 5. Performance settings

- **Update Mode:** `EveryFrame` (default) or `Manual` (`tree.Tick()` from our code; calling `Tick()` in EveryFrame only logs a warning). Manual is how a central manager can time-slice trees.
- **Evaluation Type:** `EntireTree` (resolve fully this frame) or `Count` + `MaxEvaluationCount` (spread big trees across frames, adds decision latency).
- Traversal is Burst-compiled for all trees; GameObject task bodies run managed on the main thread. Move to Entity tasks only if a **player build** profile shows task execution (not traversal) is the bottleneck.
- Profile with the Behavior Designer window **closed**, Burst compilation **on**, in a development build. Add a `ProfilerMarker` around our manual `Tick()` loop.

## 6. Debugging

- Select the agent with the Behavior Designer window open: live task status, variable values. Breakpoints via the `Debug/Break` task; `Debug/Log`, `LogValue`, `DrawRay` tasks for quick traces (remove before commit).
- From code: `tree.Status`, `tree.IsActive()`, `tree.GetRuntimeNodeStatus(index)`, `tree.FindTask<T>()`; hooks `OnBehaviorTreeStarted`, `OnBehaviorTreeStopped`.
- In the editor via CLI: `unity command console` for errors; `run_script` to inspect a tree's variables at runtime.
- Common failures: task `OnAwake` hit a null service (tree started before injection — §3); branch never preempts (missing/narrow abort, or conditional placed after a Running action); event not received (sender/receiver name, argument count and **types** must match exactly; `int` vs `float` silently mismatches).

---
Adapted from `tools-unity-behavior-designer` in [tjboudreaux/cc-plugin-unity-gamedev](https://github.com/tjboudreaux/cc-plugin-unity-gamedev) (MIT, see `../THIRD_PARTY_LICENSE-unity-gamedev.txt`); rewritten for Behavior Designer Pro 3.3.3 and this project.
