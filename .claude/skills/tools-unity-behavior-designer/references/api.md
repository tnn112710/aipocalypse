# Behavior Designer Pro 3.3.3: verified API

Taken by reflection (`scripts/BdApi.cs`) from the installed DLLs. Re-run it after a package update. Members not listed here have not been checked, so verify them before use.

## Namespaces / using

```csharp
using Opsive.BehaviorDesigner.Runtime;                 // BehaviorTree, Subtree
using Opsive.BehaviorDesigner.Runtime.Tasks;           // TaskStatus, ConditionalAbortType, IPausableTask, ISavableTask, ECS*Task
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;   // Action, ActionNode
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals; // Conditional, ConditionalNode
using Opsive.BehaviorDesigner.Runtime.Components;      // UpdateMode, EvaluationType, ECS buffers (BranchComponent, TaskComponent…)
using Opsive.GraphDesigner.Runtime.Variables;          // SharedVariable, SharedVariable<T>, ECSSharedVariableIndex<T>, RequireShared
using Opsive.Shared.Utility;                           // [Category], [Description], MemberVisibility
using Opsive.Shared.Events;                            // EventHandler (clashes with System.EventHandler, so alias or fully qualify it)
```

`Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Action` clashes with `System.Action`. Inside task files, avoid `using System;` or alias it.

## BehaviorTree (MonoBehaviour)

Lifecycle and control:
- `bool StartBehavior()`, `bool StopBehavior(bool pause)`, `bool RestartBehavior()`
- `bool IsActive()`, `bool IsRunning()`, `bool IsPaused()`, `TaskStatus Status`
- `bool StartBranch(Type eventNodeType)` / `StartBranch(IEventNode)` starts a specific event branch
- `void Tick()` works only when `UpdateMode == UpdateMode.Manual`
- Properties: `UpdateMode UpdateMode` (`EveryFrame`, `Manual`), `EvaluationType EvaluationType` (`EntireTree`, `Count`), `int MaxEvaluationCount` (1–65535), `bool StartWhenEnabled`, `bool PauseWhenDisabled`, `string Name`, `Entity Entity`, `World World`
- Callbacks (public fields, `System.Action…`): `OnBehaviorTreeStarted`, `OnBehaviorTreeStopped(bool)`, `OnBehaviorTreeDestroyed`, plus collision/trigger forwarding and save/load hooks (`OnWillSave`, `OnDidSave`, `OnWillLoad`, `OnDidLoad`)

Variables:
- `SharedVariable<T> GetVariable<T>(PropertyName name)` and `GetVariable<T>(name, SharingScope scope)`
- `bool SetVariableValue<T>(PropertyName name, T value)` and `(name, value, SharingScope scope)`. Strings convert implicitly to `PropertyName`
- `SharedVariable[] SharedVariables`, `SharingScope VariableScope`
- `SharingScope`: `Self, Empty, Graph, GameObject, Scene, Project, Dynamic, Subgraph`

Tasks and introspection:
- `T FindTask<T>()`, `T[] FindTasks<T>()`, `int FindTasks<T>(T[] buffer)` (the last one doesn't allocate)
- `Task GetTaskObject(int index)`, `TaskStatus GetRuntimeNodeStatus(int index)`, `bool IsNodeActive(bool, int)`, `bool IsNodeEnabled(bool, int)`

Subtrees: `void ReevaluateSubtreeReferences()` re-picks the subtrees of `SubtreeReferenceSelector`.

Save/load: `SaveManager.SaveData? Save(VariableSaveScope)`, `bool Save(string path, VariableSaveScope)`, `bool Load(SaveData, Action<BehaviorTree> beforeRestore)`, `bool Load(string path, Action<BehaviorTree>)`. `VariableSaveScope` is `GameObjectVariables, SceneVariables, ProjectVariables`. Scene object references can't be saved, so restore them in the `Load` callback (see `Samples~/Scripts/SceneManagers/SaveLoadSceneManager.cs`). Saving is postponed in this project (ARCHITECTURE.md). When it's added, route this through the Persistence feature and Easy Save 3.

Advanced (pure ECS): `StartBehavior(World, Entity)`, `StopBehavior(World, Entity, bool)`, `Tick(World, Entity)`, `StartBakedBehaviorTree(World, Entity)`, `GetBehaviorTree(Entity)`.

## Task (base of Action / Conditional / CompositeNode / DecoratorNode)

Overridable: `OnAwake()`, `OnStart()`, `TaskStatus OnUpdate()`, `OnEnd()`, `Reset()`, `OnBehaviorTreeStarted()`, `OnBehaviorTreeStopped(bool paused)`, `OnDestroy()`, `OnDrawGizmos()`, `OnDrawGizmosSelected()`, plus physics callbacks `OnTriggerEnter/Exit(Collider)`, `OnCollisionEnter/Exit(Collision)`, 2D variants and `OnControllerColliderHit`. Each physics callback is gated by its `Receive…Callback` bool property, which you override to return `true`.

Conditional only: `TaskStatus OnReevaluateUpdate()`, used by conditional aborts. It defaults to `OnUpdate`.

Protected fields: `m_GameObject`, `m_Transform`, `m_BehaviorTree`, `m_Enabled`, `m_RuntimeIndex`. Properties: `gameObject`, `transform`, `Status`, `Enabled`.

Helpers: `GetComponent<T>()`, `GetComponent(Type)`, `TryGetComponent<T>(out T)`, `StartCoroutine(IEnumerator)` / `StopCoroutine` / `StopAllCoroutines`. Prefer not using coroutines. Keep tasks as `OnUpdate` state machines.

Pause/save hooks: `IPausableTask` (`Pause(World, Entity)`, `Resume(World, Entity)`) and `ISavableTask` (`GetSaveReflectionType(int)` returns `MemberVisibility.All`, `Public` or `None`, plus `Save(World, Entity)` and `Load(object, World, Entity)`).

`TaskStatus`: `Inactive, Queued, Running, Success, Failure`. Return only `Running`, `Success` or `Failure` from `OnUpdate`.

## Other base classes

- `Action` / `Conditional` can be stacked (several in one node). `ActionNode` / `ConditionalNode` can't.
- `TargetGameObjectAction` / `TargetGameObjectConditional` have an `m_TargetGameObject` shared variable. The task then works on that object (`m_ResolvedGameObject`, `m_ResolvedTransform`) instead of the tree's own object.
- `TargetBehaviorTreeAction` / `TargetBehaviorTreeConditional` work the same way, but target another BehaviorTree.
- `CompositeNode` / `DecoratorNode` have `MaxChildCount` (checked when editing) and `NextChildIndex` (read at runtime while Running). Prefer the built-ins.
- Entity tasks: `ECSActionTask<TSystem, TBufferElement, TFlag>`, `ECSConditionalTask<…>`, `ECSCompositeTask<…>`, `ECSDecoratorTask<…>`. See entity-tasks.md.
- Interfaces: `IReevaluateResponder` (ECS conditional aborts: `ReevaluateFlag`, `ReevaluateSystemType`) and `IInterruptResponder` (`InterruptSystemType`).

## SharedVariable<T>

`T Value { get; set; }`, `object GetValue()`, `void SetValue(object)`, `object Clone()`, `Type ElementType`, and the `OnValueChange` event (used in the samples). Literals convert implicitly, as in `SharedVariable<float> m_Speed = 3f;`. `[RequireShared]` forces the field to be bound to a blackboard variable.

## Events (Opsive.Shared.Events.EventHandler, static)

```csharp
EventHandler.ExecuteEvent(tree, "Alert");                     // no args
EventHandler.ExecuteEvent<int>(tree, "Damaged", 5);           // typed, up to 3 args
EventHandler.RegisterEvent<int>(tree, "Damaged", OnDamaged);  // a MonoBehaviour can listen to tree events
EventHandler.UnregisterEvent<int>(tree, "Damaged", OnDamaged);
```

Sender and receiver must match on target, name, argument count **and argument types**. The global overloads omit the target object.

Tree side:
- `HasReceivedEvent` (conditional): `EventName`, `GlobalEvent`, `ReceiveMode`, `MaxQueueSize`, and typed stored-value variables. Put it under a conditional abort.
- `OnReceivedEvent` (event node): starts its own branch when the event arrives. Stores up to 3 arguments in shared variables.
- `SendEvent` (action): sends an event to this tree or a target tree.

## Built-in tasks: when to use them in this project

Composites: `Sequence`, `Selector`, `Parallel`, `ParallelSelector`, `PrioritySelector`, `RandomSelector`, `RandomSequence`, `SelectorEvaluator`, `UtilitySelector`. `Sequence` and `Selector` have `AbortType` (`ConditionalAbortType`).

Decorators:
- `Cooldown` waits its duration *after* the child completes, then returns the child's status. It holds the branch Running, which makes it an attack recovery rather than a gate.
- `ConditionalEvaluator` wraps a conditional with re-evaluation.
- `Inverter`, `Repeater`, `UntilSuccess`, `UntilFailure`, `ReturnSuccess`, `ReturnFailure`, `Iterator`.
- `PriorityEvaluator`, `PriorityVariableEvaluator`, `UtilityEvaluator`, `UtilityCurveEvaluator`, `UtilityVariableEvaluator`.

Use freely: Math, Variables, Conversions, IList, `Utility/*` (`RateLimiter`, `Counter`, `Idle`, `ReturnStatus`, `PerformInterruption`), `Time/Wait`, `Debug/*`, `BehaviorTree/*` (Start, Stop, Restart, `SetSubtree`), `Events/SendEvent`, `Conditionals/Math/*`, `RandomProbability`, `HasValue`.

Use with care, preferring our own components or the bridge:
- `Animator/*`: fine for simple bosses. Crowd-style animation goes through our view components.
- `Transform/*` and `Rigidbody/*`: these move objects directly and fight A*. Move only through `IAstarAI`.
- `Physics/Raycast`, `OverlapShape`, `CastShape`: senses should check the EntityRegistry spatial grid first.

Don't use in this project:
- `NavMesh/*`: we use A*.
- `GameObject/Instantiate`, `Destroy`, `CloneGameObject`, `SpawnAtPosition`, `PoolManager`, `Particles/Pool`: use our pools instead.
- `GameObject/FindGameObject*`: no Find calls.
- `SaveLoad/*`: owned by Easy Save 3 / Persistence.
- `Audio/*`: owned by BroAudio.
- `SceneManagement/*`: owned by SceneLoader.
- `Time/SetTimeScale`, `SlowMotionEffect`, `TimeScaleCurve`: `TimeService` owns timeScale.
- `Camera/*`: Cinemachine owns the camera.
- `Resources/*`: no Resources loading.
- `Utility/ApplicationQuit`.
