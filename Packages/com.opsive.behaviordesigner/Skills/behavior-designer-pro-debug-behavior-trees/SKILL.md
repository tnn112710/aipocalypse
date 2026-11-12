---
name: "behavior-designer-pro-debug-behavior-trees"
description: "Use this skill whenever the user says 'my behavior tree does nothing', 'this task is stuck running', 'the wrong branch executes', 'my variable is ignored', or reports Behavior Designer Pro performance problems. Covers editor validation, lifecycle, runtime status, variable scope, subtree failures, and targeted profiling. Do NOT use for new tree construction (see behavior-designer-pro-build-behavior-trees) or authoring ECS tasks from scratch (see behavior-designer-pro-create-entity-tasks). Confirm the installed product before diagnosing a generic AI symptom."
metadata:
  asset: "Behavior Designer Pro"
  publisher: "Opsive"
  asset-version: "3.3.4"
  skill-version: "1.0.8"
  unity: "2022.3.11+"
  render-pipelines: "Built-in, URP, HDRP (core behavior logic)"
  category: "tools/visual-scripting"
  asset-store-url: "https://assetstore.unity.com/packages/tools/visual-scripting/behavior-designer-pro-3-dots-powered-behavior-trees-368344"
  documentation-url: "https://opsive.com/support/documentation/behavior-designer-pro/"
  support-url: "https://opsive.com/support/"
  last-verified: "2026-11-09"
---

# Diagnose Behavior Tree Problems

Reproduce a specific Behavior Designer Pro failure, identify the owning configuration or task, and verify a focused repair against the original symptom.

## When to use this skill

- "The tree never starts."
- "This action stays Running forever."
- "It picks the wrong branch after my variable changes."
- "Adding more enemies makes the tree expensive."

Not for: rewriting a working tree, speculative optimization, or repairing unrelated Unity systems.

## Prerequisites

Use Behavior Designer Pro 3.3.4 and Unity 2022.3.11 or newer. The package requires Graph Designer, Opsive Shared, Entities, Burst, and Mathematics; retain the versions supplied with the installed release (Entities 1.3.8 in the verified manifest). Core tree logic does not require a render-pipeline change; sample materials may require their own setup.

Run this read-only C# in an available Unity Editor execution tool before making changes:

```csharp
var treeType = System.Type.GetType("Opsive.BehaviorDesigner.Runtime.BehaviorTree, Opsive.BehaviorDesigner.Runtime");
UnityEngine.Debug.Log(treeType != null ? "Behavior Designer runtime found" : "Behavior Designer runtime unavailable");
```

If unavailable, check the first compiler error and **Tools > Opsive > Behavior Designer > Welcome**. If the product is absent, report that this skill requires [Behavior Designer Pro](https://assetstore.unity.com/packages/tools/visual-scripting/behavior-designer-pro-3-dots-powered-behavior-trees-368344); do not install another edition as a substitute. Confirm the installed release in the Welcome window. Preserve unsaved scenes and use the user's intended tree. Run Play Mode checks only when the scene can safely enter Play Mode.


Record the selected object and tree, expected behavior, actual behavior, and a minimal reproduction. Preserve current authoring work before entering Play Mode. Distinguish a Console compile error from an Error Window graph error and from a valid graph with incorrect gameplay behavior.

## Quick start

1. Open **Tools > Opsive > Behavior Designer > Error Window** and the Unity Console. Resolve the earliest relevant compile/initialization error before interpreting downstream failures.
2. Select the intended tree in **Tools > Opsive > Behavior Designer > Editor**. Confirm the component is enabled and Start is connected to the intended root.
3. Inspect Start When Enabled and Update Mode. If automatic startup is disabled, identify the code that calls StartBehavior. If Manual mode is selected, identify the owner that calls Tick.
4. Reproduce in Play Mode. Inspect runtime variable values and task status on the actual instance, not only the Subtree asset defaults.
5. Locate the earliest point where observed status differs from the expected trace. Change the owning configuration or custom task, rerun the same reproduction, and verify an unaffected path.
6. Expected result: the original failure is absent for the reason identified, with evidence from task transitions or gameplay state.

## Workflows

### Workflow: Diagnose stalled execution

**Goal:** Explain why a tree or branch does not advance.

**Steps:** Check `IsRunning()` and `IsPaused()`. A Manual tree waiting for `Tick()` is not paused. After `StopBehavior(true)` and `StartBehavior()`, Manual mode still requires a new tick for traversal and conditional reevaluation. For a running branch, inspect its active leaf and the input that should complete it. Running is a valid ongoing state; do not change it to Success merely to advance the graph. A Sequence waits for its Running child; a Selector will not fall through while its child runs. For custom managed tasks, inspect OnStart/OnUpdate/OnEnd. For Entity tasks, inspect the task-specific enabled flag, query, RuntimeIndex, branch execution gate, and writeback to TaskComponent using the [Entity skill](../behavior-designer-pro-create-entity-tasks/SKILL.md).

**Expected result:** The blocking condition, missing tick, pause, or task implementation defect is identified and corrected at its owner.

### Workflow: Diagnose unexpected branches and data

**Goal:** Separate input/binding errors from control-flow errors.

**Steps:** Inspect the actual runtime variable's name, type, scope, and value. Verify setter return values. Confirm the task field is bound rather than literal. Trace left-to-right sibling order and conditional statuses, then inspect the abort-owning composite. For subtree references, check assigned assets, overrides, and injection errors; use the [subtree skill](../behavior-designer-pro-reuse-subtrees/SKILL.md). Reduce the test to a Boolean predicate and long-running Wait when perception or movement obscures the result.

**Expected result:** The same input sequence produces the expected branch transitions on repeated runs.

### Workflow: Diagnose stale prefab variant snapshots

**Goal:** Verify a prefab variant starts after its base tree changes, including variants spawned only at runtime.

**Steps:** A "compiled from a stale parent" error means the variant's compiled snapshot predates its parent. Reproduce with a scene containing only a spawner that references the variant prefab; opening the variant tree first can refresh the snapshot and hide the failure. Save a base change and let automatic refresh finish over subsequent editor updates. Changed graph assets and imports of prefabs containing graph or supported SharedVariable components automatically refresh dependent compiled snapshots, including unopened variant chains, while retaining semantic overrides. Prefab dependency discovery runs once per Editor session. Initialization and automatic refresh use a 5 ms soft budget per editor update, yielding between prefab operations. Unity asset discovery and individual imports/saves are indivisible and can exceed that budget. Subsequent prefab imports update an incremental reverse dependency index, so saving a graph prefab follows cached links instead of scanning every project prefab. Script reloads retain the index; moves, deletions, and nested source replacements update the cached relationships. Verify the saved variants are current while still in Edit Mode, then enter Play Mode without opening or repairing them. Play and build preparation synchronously finish any partial index and pending dependency refresh before proceeding; unresolved updates stop the transition. Active refreshes survive script reload by retaining their source edits, and a new edit between frames restarts the affected batch. Pre-play preparation does not scan and repair every project prefab. Legacy prefab data still uses **Tools > Opsive > Behavior Designer > Upgrade Project Serialization...**; normal edits after migration must propagate automatically. Repeat with another base edit and with a chain of variants. Saving, reimporting, or moving a prefab with neither graph nor supported SharedVariable components must not schedule graph dependency discovery or display graph validation progress. Confirm inherited tasks update, local values remain overridden, and the spawned tree runs. The runtime stale-parent check must continue rejecting invalid snapshots.

**Expected result:** The unopened variant starts after each base revision, retains its overrides, and executes the latest inherited tasks.

### Workflow: Investigate measured performance

**Goal:** Reduce the cost actually observed in the user's scene.

**Steps:** Capture a baseline with a fixed scene, agent count, update cadence, and build/editor context. Use Unity's CPU Profiler to identify managed task work, allocations, and traversal/system overhead. Compare one controlled change with the same workload. Cache repeated component lookups when a custom task causes measured cost; reduce unnecessarily frequent gameplay queries; consider an Entity implementation only for suitable unmanaged work. If changing tick cadence or Evaluation Type/Max Evaluation Count, test the resulting reaction latency and completion behavior as well as frame cost. Do not claim gains from editor visual smoothness alone.

For Utility Selectors, distinguish an external `Tick()` request from the internal traversal passes it enables. The runtime samples each evaluator's `GetUtilityValue()` once per tree tick and reuses that score during traversal, including failure fallback and Repeater re-entry. Score inputs changed by tasks take effect on the next tick. The cache follows tree updates rather than Unity frames: separately processed ticks in one frame refresh scores, while repeated requests before one update coalesce. `BlockDuringExecution` is applied to the sampled score on every pass, so a running child remains protected and blocking clears when the child finishes. Editor utility labels display the runtime value without calling the getter. Keep scoring free of side effects; independently cache or schedule sensing when its cost remains high.

**Expected result:** Recorded timings or allocations improve without violating the requested behavior or responsiveness.

## Verification

- Preserve a before/after reproduction with fixed inputs and expected transitions.
- Error Window and Console are clear of new errors after the repair.
- Test the repaired path and one neighboring path, including interruption or restart when relevant.
- If authoring data changed, save and reopen to check persistence.
- For performance work, record workload, capture conditions, observed metric, and behavior tradeoffs.

## API quick reference

Methods below belong to `Opsive.BehaviorDesigner.Runtime.BehaviorTree` unless qualified.

| Entry point | Type | What it does |
|---|---|---|
| `IsRunning()` / `IsPaused()` | Methods | Report lifecycle state. |
| `GetRuntimeNodeStatus(int runtimeIndex)` | Method | Reads a runtime node status; use a verified runtime index. |
| `GetVariable<T>(UnityEngine.PropertyName)` | Method | Reads a Graph-scoped variable through the typed wrapper. |
| `Opsive.BehaviorDesigner.Editor.BehaviorDesignerWindow.ShowErrorWindow()` | Static method | Shows editor graph errors. |
| `UpdateMode`, `EvaluationType`, `MaxEvaluationCount` | Properties | Configure cadence and evaluation budgeting. |

## Common issues

- Nothing runs → disconnected Start, disabled component, paused tree, or missing manual tick → verify those states before replacing tasks.
- Wrong instance inspected → multiple trees or shared subtree asset → select the runtime owner and graph explicitly.
- Variables reset after leaving Play Mode → runtime edits are not authoring defaults → change and save defaults outside Play Mode if persistence is intended.
- Runtime index mismatch after subtree injection → authoring indices used as runtime indices → obtain runtime mapping rather than assuming equality.
- Lower-priority work never interrupts → abort configuration or predicate issue → reproduce using the reactive-behavior skill.

## Boundaries

A clear Error Window proves only the validations it performs, not gameplay correctness. Do not silence errors, force statuses, add broad null guards, or restart every frame to conceal a defect. Diagnose project code and configuration first; modifying purchased runtime internals is a separate engineering task.
