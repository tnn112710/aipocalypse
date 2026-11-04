---
name: "behavior-designer-pro-connect-gameplay-code"
description: "Use this skill whenever the user wants to 'set a blackboard variable', 'pass my target into the tree', 'send an event to my AI', or start, pause, stop, or manually tick Behavior Designer Pro from gameplay code. Covers Shared Variable scope, bindings, typed access, events, and lifecycle integration. Do NOT use for writing task logic (see behavior-designer-pro-create-gameobject-tasks) or subtree overrides (see behavior-designer-pro-reuse-subtrees). Confirm the installed product when a blackboard request does not name Behavior Designer Pro."
metadata:
  asset: "Behavior Designer Pro"
  publisher: "Opsive"
  asset-version: "3.3.4"
  skill-version: "1.0.1"
  unity: "2022.3.11+"
  render-pipelines: "Built-in, URP, HDRP (core behavior logic)"
  category: "tools/visual-scripting"
  asset-store-url: "https://assetstore.unity.com/packages/tools/visual-scripting/behavior-designer-pro-3-dots-powered-behavior-trees-368344"
  documentation-url: "https://opsive.com/support/documentation/behavior-designer-pro/"
  support-url: "https://opsive.com/support/"
  last-verified: "2026-10-14"
---

# Connect Trees to Gameplay Code

Connect an existing tree to project data and events using its public runtime APIs and explicit variable scope.

## When to use this skill

- "Set the enemy's target from my spawner."
- "Why are both enemies sharing the same health?"
- "Trigger a branch from my gameplay script."
- "Pause the tree during dialogue."

Not for: implementing the action performed by a task or changing branch priority.

## Prerequisites

Use Behavior Designer Pro 3.3.3 and Unity 2022.3.11 or newer. The package requires Graph Designer, Opsive Shared, Entities, Burst, and Mathematics; retain the versions supplied with the installed release (Entities 1.3.8 in the verified manifest). Core tree logic does not require a render-pipeline change; sample materials may require their own setup.

Run this read-only C# in an available Unity Editor execution tool before making changes:

```csharp
var treeType = System.Type.GetType("Opsive.BehaviorDesigner.Runtime.BehaviorTree, Opsive.BehaviorDesigner.Runtime");
UnityEngine.Debug.Log(treeType != null ? "Behavior Designer runtime found" : "Behavior Designer runtime unavailable");
```

If unavailable, check the first compiler error and **Tools > Opsive > Behavior Designer > Welcome**. If the product is absent, report that this skill requires [Behavior Designer Pro](https://assetstore.unity.com/packages/tools/visual-scripting/behavior-designer-pro-3-dots-powered-behavior-trees-368344); do not install another edition as a substitute. Confirm the installed release in the Welcome window. Preserve unsaved scenes and use the user's intended tree. Run Play Mode checks only when the scene can safely enter Play Mode.


Identify the exact tree component; an object can have multiple trees. Create variables in the tree's Shared Variables panel before looking them up. Match the name, type, and scope exactly. Use Graph scope for per-tree data unless sharing is intentional.

## Quick start

1. Create a Graph-scoped Boolean `Alert`, initially false, in the intended tree. Bind a Bool Comparison field to it rather than leaving the field as a literal value.
2. Save this component as `Assets/Scripts/TreeSignal.cs`, assign the intended tree, and let Unity compile. It exposes a context-menu operation so the request can be verified without custom input handling.

```csharp
/// <summary>Sets a known graph variable on an explicitly assigned tree.</summary>
public sealed class TreeSignal : UnityEngine.MonoBehaviour
{
    [UnityEngine.Tooltip("The behavior tree that owns the Alert graph variable.")]
    [UnityEngine.SerializeField] private Opsive.BehaviorDesigner.Runtime.BehaviorTree m_Tree;

    /// <summary>Sets Alert during Play Mode and reports whether the assignment succeeded.</summary>
    [UnityEngine.ContextMenu("Set Alert")]
    public void SetAlert()
    {
        if (!UnityEngine.Application.isPlaying || m_Tree == null) {
            return;
        }
        bool changed = m_Tree.SetVariableValue<bool>("Alert", true,
            Opsive.GraphDesigner.Runtime.Variables.SharedVariable.SharingScope.Graph);
        UnityEngine.Debug.Log(changed ? "Alert set" : "Alert name, type, or scope did not match", this);
    }
}
```

3. Enter Play Mode and invoke **Set Alert** on the TreeSignal component's context menu.
4. Expected result: `Alert set` appears and the runtime variable becomes true. A reacting branch still requires its own abort/event configuration.

## Workflows

### Workflow: Choose and verify scope

**Goal:** Share exactly the data the gameplay requires.

**Steps:** Use Graph for a single tree, GameObject for cooperating graphs on an object, Scene for scene-wide state, and Project for deliberately shared project state. A task field with a local literal is not a binding to a graph variable. Bind the field in its inspector and confirm it observes a runtime change. For property binding, select a compatible exposed member in the variable UI and test its read/write behavior; do not assume every field or read-only property is writable. Retrieve a known registered Boolean with `tree.GetVariable<bool>("Alert", scope).Value`. Validate the variable declaration first; do not add wrapper-null checks that hide a missing binding. Treat a false return from `SetVariableValue` as a failed assignment.

**Expected result:** Changing one tree's Graph variable does not change another tree; deliberately wider scopes share only as intended.

### Workflow: Deliver a named event

**Goal:** Start an event branch on one specific tree.

**Steps:** Add On Received Event and connect its branch. Set Event Name to `AlertSignal` and Global Event false; leave stored arguments unassigned for this no-argument example. After the tree is initialized, execute this C# with `tree` referencing the intended BehaviorTree:

```csharp
Opsive.Shared.Events.EventHandler.ExecuteEvent(tree, "AlertSignal");
```

The local target is the tree component, not its GameObject. Alternatively configure the built-in Send Event task's target tree. For argument-bearing events, match sender generic argument types and receiver stored-variable types/count. Use a global event only when broadcast is intended. Test a second tree to detect accidental broadcasts.

**Expected result:** Only the addressed tree's event branch runs and it receives the intended values.

### Workflow: Control execution

**Goal:** Connect tree execution to a game system.

**Steps:** For explicit startup, turn Start When Enabled off before enabling the object and call `StartBehavior()` when dependencies and variables are ready. It defaults to true for automatic startup. Pause with `StopBehavior(true)` and resume with `StartBehavior()`. Stop without pause with `StopBehavior()`. To control ticks, set `UpdateMode` to `Opsive.BehaviorDesigner.Runtime.Components.UpdateMode.Manual` and call `Tick()` from one owner after starting the tree; EveryFrame is the default. Check Boolean lifecycle return values. Avoid ticking twice through competing owners. A Manual tree waiting for a tick is not paused: `IsPaused()` becomes true only after an explicit pause completes. Resuming preserves Manual mode, so traversal and conditional reevaluation continue only after another `Tick()`. Repeated `StartBehavior()` calls on an already running tree do not resume it or enable automatic updates. Pausing a tree is distinct from pausing the whole Unity Editor.

**Expected result:** `IsRunning()`, `IsPaused()`, and observed task progress match the requested lifecycle, with no duplicate starts or ticks.

## Verification

- The setter returns true and a subsequent typed read yields the assigned value.
- Two trees retain independent Graph values; test intentional wider scopes separately.
- Event name, local target, global flag, and argument signatures match; only the intended recipient responds.
- A paused tree stops progressing and resumes; a Manual tree progresses only when ticked.
- Saved bindings survive leaving Play Mode and reopening the scene; runtime values need not persist.

## API quick reference

All tree methods below belong to `Opsive.BehaviorDesigner.Runtime.BehaviorTree`.

| Entry point | Type | What it does |
|---|---|---|
| `GetVariable<T>(UnityEngine.PropertyName, SharedVariable.SharingScope)` | Method | Retrieves a typed variable from the selected scope. |
| `SetVariableValue<T>(UnityEngine.PropertyName, T, SharedVariable.SharingScope)` | Method | Sets a declared variable and synchronizes managed values into ECS storage. |
| `StartBehavior()` / `StopBehavior(bool pause = false)` | Methods | Start/resume, pause, or stop. |
| `Tick()` | Method | Requests a manual evaluation on the next DOTS system-group update. |
| `Opsive.Shared.Events.EventHandler.ExecuteEvent(object, Opsive.Shared.Utility.StringHash)` | Static method | Sends a targeted no-argument event. |

Here `SharedVariable` is `Opsive.GraphDesigner.Runtime.Variables.SharedVariable`. For unmanaged variable storage, read the [Entity variable reference](../behavior-designer-pro-create-entity-tasks/references/variables-and-reevaluation.md).

## Common issues

- Setter returns false → wrong name, type, or scope → inspect the declared variable instead of creating a duplicate silently.
- Task ignores changes → inspector field is a literal → bind it to the shared variable.
- Event ignored → sent before initialization or targeted to GameObject → initialize the tree and use the tree component as target.
- Both agents change → Scene/Project scope chosen accidentally → use Graph scope for independent data.
- Manual tree is idle → no ticking owner → call Tick after startup at the intended cadence.

## Boundaries

These are Pro APIs; do not substitute original Behavior Designer `SharedBool` or manager APIs. Access `SharedVariable<T>.Value` directly in tasks. Check contained Unity object values when required, not the wrapper itself. This skill does not implement networking replication, persistence, or event buffering.
