---
name: "behavior-designer-pro-create-entity-tasks"
description: "Use this skill whenever the user wants to 'write a DOTS behavior task', 'make a Burst-compatible action', 'create an ECS conditional', or repair Behavior Designer Pro task-system execution. Covers authoring classes, buffers, activation flags, systems, runtime indices, variables, and reevaluation. Do NOT use for managed component actions (see behavior-designer-pro-create-gameobject-tasks) or general performance diagnosis (see behavior-designer-pro-debug-behavior-trees). Confirm Entities and the installed product in Prerequisites."
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

# Write Custom Entity Tasks

Create Behavior Designer Pro Entity tasks using the installed templates and preserve the traversal, data, and activation contracts required by the runtime.

## When to use this skill

- "Create a Burst-compatible behavior action."
- "Make this ECS conditional participate in aborts."
- "My task system runs but the node never finishes."

Not for: converting an entire project to DOTS, or calling managed UnityEngine APIs from a job.

## Prerequisites

Use Behavior Designer Pro 3.3.3 and Unity 2022.3.11 or newer. The package requires Graph Designer, Opsive Shared, Entities, Burst, and Mathematics; retain the versions supplied with the installed release (Entities 1.3.8 in the verified manifest). Core tree logic does not require a render-pipeline change; sample materials may require their own setup.

Run this read-only C# in an available Unity Editor execution tool before making changes:

```csharp
var treeType = System.Type.GetType("Opsive.BehaviorDesigner.Runtime.BehaviorTree, Opsive.BehaviorDesigner.Runtime");
UnityEngine.Debug.Log(treeType != null ? "Behavior Designer runtime found" : "Behavior Designer runtime unavailable");
```

If unavailable, check the first compiler error and **Tools > Opsive > Behavior Designer > Welcome**. If the product is absent, report that this skill requires [Behavior Designer Pro](https://assetstore.unity.com/packages/tools/visual-scripting/behavior-designer-pro-3-dots-powered-behavior-trees-368344); do not install another edition as a substitute. Confirm the installed release in the Welcome window. Preserve unsaved scenes and use the user's intended tree. Run Play Mode checks only when the scene can safely enter Play Mode.


Confirm `Unity.Entities.ISystem`, `Opsive.BehaviorDesigner.Runtime.Templates.ECSActionNode`, and the generic ECS task bases are available. Use the project's runtime assembly and compatible Entities source generators; a plain external C# compiler is not sufficient to validate generated Entities jobs. Identify whether execution uses runtime GameObjects or baked entities before promising support for both.

## Quick start

1. Generate an action from the installed template so its system boilerplate matches the installed runtime. Run this C# through an available Unity Editor execution tool:

```csharp
var directory = "Assets/Scripts/BehaviorTasks";
var path = directory + "/SkillEntitySuccess.cs";
if (System.IO.File.Exists(path)) {
    throw new System.InvalidOperationException("Choose a new class and path; the target already exists.");
}
System.IO.Directory.CreateDirectory(directory);
var source = new Opsive.BehaviorDesigner.Runtime.Templates.ECSActionNode().GetScript("SkillEntitySuccess");
System.IO.File.WriteAllText(path, source, new System.Text.UTF8Encoding(false));
UnityEditor.AssetDatabase.ImportAsset(path);
```

2. Wait for Unity compilation. Open **Tools > Opsive > Behavior Designer > Editor**, choose **Create Task**, search for SkillEntitySuccess, and connect it under Start.
3. Run the tree. The template sets an executable Queued node to Success. Expected result: the task completes without managed job calls, compile errors, or Console exceptions.
4. Add two instances of the same task type to a Sequence and rerun. Both must finish independently; a task type's data buffer may contain more than one node.

## Workflows

### Workflow: Add runtime logic

**Goal:** Replace the template's immediate success with the required unmanaged operation.

**Steps:** Preserve these contracts while editing the generated source:

- The authoring class derives from `ECSActionTask<TSystem, TBufferElement, TComponentFlag>` and creates data in `GetBufferElement()`.
- Set the node's buffer Index from `RuntimeIndex`, not the authoring Index. Subtree injection can change runtime positions.
- Store unmanaged data in an `IBufferElementData` struct and define a task-specific `IComponentData, IEnableableComponent` flag.
- Keep the task system `[DisableAutoCreation]`; Behavior Designer installs and schedules its task systems. Do not also register it in an unrelated Unity update group.
- Query the task's enabled flag and `EvaluateFlag`, with correct read/write access to branch, task, and custom buffers.
- Check `BranchComponent.CanExecute` and the node's actual status before executing. One enabled type flag does not make every buffered instance active.
- Initialize work on Queued, continue multi-frame work on Running, and write updated task/custom structs back to their buffers. The untouched action template handles Queued only.
- Carry job dependencies forward through `state.Dependency`. Burst jobs must not reference GameObjects, managed task instances, or managed SharedVariable wrappers.

**Expected result:** Only active instances execute; the correct TaskComponent receives Running, Success, or Failure and traversal advances.

### Workflow: Add a reevaluating conditional

**Goal:** Support changing predicates during a running branch.

**Steps:** Start with `new Opsive.BehaviorDesigner.Runtime.Templates.ECSConditionalNode().GetScript("SkillEntityCondition")` and save it using the same guarded workflow as Quick start. Implement the predicate in normal execution and reevaluation using consistent data. The template implements `IReevaluateResponder`, exposes a reevaluation flag/system, and checks the task's reevaluation state. Preserve those paths; inheriting ECSConditionalTask alone does not supply them. Read [variables and reevaluation](references/variables-and-reevaluation.md) before adding a live Shared Variable predicate.

**Expected result:** The conditional responds during initial traversal and changes status during an enabled conditional abort.

### Workflow: Verify lifecycle and baking requirements

**Goal:** Support the lifecycle actually used by the project.

**Steps:** Test two instances per tree and two trees. Test completion, interruption, and restart. If custom runtime data owns resources, define and test its cleanup rather than relying on a managed OnEnd callback that Entity tasks do not inherit. If baking is required, use a separate test scene with the project's SubScene workflow and confirm the baked path creates the data and flag types. Do not label a runtime-only test as baking verification. Add pause/save behavior only if the requested task needs it and verify the installed interfaces first.

**Expected result:** The task works in the specified execution path and does not reuse stale per-run state.

## Verification

- Unity's full assembly/source-generation pipeline compiles the authoring class, buffer, flag, and system.
- The node is discoverable, runs, and advances its parent.
- Two nodes of the same type do not overwrite each other's runtime data or statuses.
- Inactive/finished entries do not execute just because another entry enables the type flag.
- A conditional test toggles a bound predicate while a lower-priority task is Running.
- Record runtime and baking checks separately; inspect Console exceptions and job safety errors.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `Opsive.BehaviorDesigner.Runtime.Templates.ECSActionNode.GetScript(string)` | Method | Generates installed action-system boilerplate. |
| `Opsive.BehaviorDesigner.Runtime.Templates.ECSConditionalNode.GetScript(string)` | Method | Generates conditional and reevaluation boilerplate. |
| `Opsive.BehaviorDesigner.Runtime.Tasks.ECSActionTask<TSystem,TBufferElement,TComponentFlag>` | Generic base | Bridges managed authoring to ECS execution. |
| `Opsive.BehaviorDesigner.Runtime.Components.TaskComponent` | Buffer element | Stores node runtime status and topology. |
| `Opsive.GraphDesigner.Runtime.Variables.ECSVariableRegistry.Register<T>(SharedVariable<T>)` | Method | Registers supported unmanaged Shared Variable data. |

In the last row, `SharedVariable<T>` is `Opsive.GraphDesigner.Runtime.Variables.SharedVariable<T>`.

## Common issues

- System never executes → wrong/missing enabled flag or query → compare with the installed template and inspect the active node.
- Parent stays Running → status changed only in a local copy → write TaskComponent back at the correct runtime index.
- One node affects another → using authoring Index or treating the type flag as per-node activation → use RuntimeIndex and per-entry status checks.
- Conditional works once only → no reevaluation responder/system → use the conditional template and test aborts.
- Burst errors → managed data captured in a job → move authoring-only objects out of runtime buffers and job code.

## Boundaries

This skill documents the installed per-type ECS task lane. It does not assume a future compact runtime, arbitrary unmanaged variable layouts, or automatic speedups. Do not change package-owned traversal systems to compensate for incorrect custom-task wiring. Use GameObject tasks when managed gameplay access is required.
