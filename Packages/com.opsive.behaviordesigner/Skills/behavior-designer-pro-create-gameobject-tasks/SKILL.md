---
name: "behavior-designer-pro-create-gameobject-tasks"
description: "Use this skill whenever the user wants to 'write a custom behavior task', 'make a door action', 'check my component from a condition', or fix a managed Behavior Designer Pro task lifecycle. Covers GameObject actions and conditionals, Shared Variables, status returns, interruption cleanup, and task discovery. Do NOT use for Burst/Entity task systems (see behavior-designer-pro-create-entity-tasks) or operating Atlas (see behavior-designer-pro-use-atlas). Confirm the product before using a behavior-tree task API."
metadata:
  asset: "Behavior Designer Pro"
  publisher: "Opsive"
  asset-version: "3.3.4"
  skill-version: "1.0.0"
  unity: "2022.3.11+"
  render-pipelines: "Built-in, URP, HDRP (core behavior logic)"
  category: "tools/visual-scripting"
  asset-store-url: "https://assetstore.unity.com/packages/tools/visual-scripting/behavior-designer-pro-3-dots-powered-behavior-trees-368344"
  documentation-url: "https://opsive.com/support/documentation/behavior-designer-pro/"
  support-url: "https://opsive.com/support/"
  last-verified: "2026-10-09"
---

# Write Custom GameObject Tasks

Implement project-specific managed task logic that the Behavior Designer Pro editor can discover and the runtime can start, interrupt, and re-enter correctly.

## When to use this skill

- "Create a task that calls my door component."
- "Check whether my character is ready to attack."
- "My action never finishes or leaves movement active after an abort."

Not for: authoring unmanaged ECS systems, generic component scripts, or generating duplicate versions of existing tasks.

## Prerequisites

Use Behavior Designer Pro 3.3.3 and Unity 2022.3.11 or newer. The package requires Graph Designer, Opsive Shared, Entities, Burst, and Mathematics; retain the versions supplied with the installed release (Entities 1.3.8 in the verified manifest). Core tree logic does not require a render-pipeline change; sample materials may require their own setup.

Run this read-only C# in an available Unity Editor execution tool before making changes:

```csharp
var treeType = System.Type.GetType("Opsive.BehaviorDesigner.Runtime.BehaviorTree, Opsive.BehaviorDesigner.Runtime");
UnityEngine.Debug.Log(treeType != null ? "Behavior Designer runtime found" : "Behavior Designer runtime unavailable");
```

If unavailable, check the first compiler error and **Tools > Opsive > Behavior Designer > Welcome**. If the product is absent, report that this skill requires [Behavior Designer Pro](https://assetstore.unity.com/packages/tools/visual-scripting/behavior-designer-pro-3-dots-powered-behavior-trees-368344); do not install another edition as a substitute. Confirm the installed release in the Welcome window. Preserve unsaved scenes and use the user's intended tree. Run Play Mode checks only when the scene can safely enter Play Mode.


Search existing tasks first. Create customer code in the project's normal runtime scripts folder, not inside the purchased package or an Editor-only folder. If using an asmdef, reference the assemblies defining the required Behavior Designer and Graph Designer types. A task is not a MonoBehaviour and is added in the task selector, not Add Component.

## Quick start

1. Save the following as `Assets/Scripts/SetTargetActiveTask.cs`. This small example demonstrates a typed Shared Variable, contained-object validation, and explicit status. Use an existing equivalent task for production unless different behavior is required.

```csharp
/// <summary>Sets a target object's active state and reports the result.</summary>
public sealed class SetTargetActiveTask : Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Action
{
    [UnityEngine.Tooltip("The object whose active state should change.")]
    [UnityEngine.SerializeField] private Opsive.GraphDesigner.Runtime.Variables.SharedVariable<UnityEngine.GameObject> m_Target;
    [UnityEngine.Tooltip("The desired active state.")]
    [UnityEngine.SerializeField] private Opsive.GraphDesigner.Runtime.Variables.SharedVariable<bool> m_Active = true;

    /// <summary>Applies the requested state to the target.</summary>
    /// <returns>Failure for a missing target; otherwise Success.</returns>
    public override Opsive.BehaviorDesigner.Runtime.Tasks.TaskStatus OnUpdate()
    {
        var target = m_Target.Value;
        if (target == null) {
            return Opsive.BehaviorDesigner.Runtime.Tasks.TaskStatus.Failure;
        }
        target.SetActive(m_Active.Value);
        return Opsive.BehaviorDesigner.Runtime.Tasks.TaskStatus.Success;
    }
}
```

2. Wait for compilation. Open **Tools > Opsive > Behavior Designer > Editor**, use **Create Task**, and search `SetTargetActiveTask` (the display label may insert spaces).
3. Add it under Start. Assign a separate test object to Target and set Active false; avoid disabling the object that owns the test tree.
4. Run the tree. Expected result: the target becomes inactive and the task succeeds. Clear Target and rerun to verify Failure without an exception.

## Workflows

### Workflow: Implement a multi-frame action

**Goal:** Keep a task Running until its operation is complete.

**Steps:** Cache stable components in `OnAwake()`. Initialize per-execution progress in `OnStart()`. Return Running while work is pending, Success once the goal is reached, and Failure when an actual prerequisite or operation fails. Release subscriptions, coroutines, or gameplay ownership in `OnEnd()` for both completion and interruption. Keep cleanup repeat-safe. Do not confuse authoring `Reset()` with runtime re-entry. Use [the complete timed-action reference](references/timed-action.md) for a runnable lifecycle example.

**Expected result:** The task completes at the expected time, cleans up on abort, and starts fresh on its next entry.

### Workflow: Implement a condition

**Goal:** Observe gameplay state without starting an operation.

**Steps:** Derive from `Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals.Conditional`. Return Success when the predicate holds and Failure otherwise. Use typed Shared Variables for exposed inputs. Keep reevaluation side-effect-free: the default `OnReevaluateUpdate()` calls `OnUpdate()`. Put ongoing work in an action, not a conditional. If using events, maintain observed state through a separately managed subscription rather than consuming a one-time signal on every predicate evaluation.

**Expected result:** Repeated evaluation gives the current truth value without duplicate gameplay effects.

### Workflow: Expose and bind task inputs

**Goal:** Make the task configurable and reusable.

**Steps:** Add Tooltip attributes to public/serialized fields and XML comments to classes and methods. Use `Opsive.GraphDesigner.Runtime.Variables.SharedVariable<T>` and `.Value`. A missing contained GameObject may justify a check; a wrapper-null guard conceals an authoring/initialization problem and should not be added. Initialize meaningful literal defaults, bind shared fields in the task inspector, and test a change to the bound value at runtime. For custom component calls, verify the component's actual API before writing the call.

**Expected result:** Fields appear in the task inspector and their literal or bound values drive execution.

## Verification

- The class compiles and appears in Create Task; it is not attached as a component.
- Exercise the intended Success, Failure, and Running paths; instant tasks need no artificial Running path.
- Abort a Running action, verify cleanup, and re-enter it twice to detect stale state or subscriptions.
- Confirm serialized defaults and Shared Variable assignments survive a scene save/reload.
- Run a second tree instance to detect accidental static/shared execution state.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Action` | Base class | Managed action task. |
| `Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals.Conditional` | Base class | Managed predicate with reevaluation support. |
| `Opsive.BehaviorDesigner.Runtime.Tasks.Task.OnAwake/OnStart/OnUpdate/OnEnd` | Callbacks | Initialization, per-run start, evaluation, and termination. |
| `Opsive.BehaviorDesigner.Runtime.Tasks.TaskStatus` | Enum | Includes Running, Success, and Failure. |
| `Opsive.GraphDesigner.Runtime.Variables.SharedVariable<T>.Value` | Property | Reads or writes the configured task value. |

## Common issues

- Task missing → compile error, abstract class, or wrong assembly → fix compilation and confirm it derives from a supported task base.
- Task never advances → always returns Running → implement an explicit completion condition.
- Aborted task still acts → cleanup missing → release the owned effect in OnEnd.
- Re-entry skips work → per-run state initialized only in OnAwake → reset it in OnStart.
- Wrong Action type → `System.Action` ambiguity → qualify the Behavior Designer base class.

## Boundaries

Managed tasks run on the main thread and may use UnityEngine objects. Do not add Burst attributes to make them Entity tasks. Preserve unrelated user code when editing an existing task. Migration from the original Behavior Designer requires separate API review.
