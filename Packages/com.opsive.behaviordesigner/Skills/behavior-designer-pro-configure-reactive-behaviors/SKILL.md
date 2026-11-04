---
name: "behavior-designer-pro-configure-reactive-behaviors"
description: "Use this skill whenever the user wants Behavior Designer Pro to 'interrupt patrol', 'switch to fleeing when health drops', 'react when a condition changes', or fix observer/conditional abort behavior. Covers Self, Lower Priority, Both, branch ordering, and interruption cleanup. Do NOT use for creating custom conditionals (see behavior-designer-pro-create-gameobject-tasks) or delivering gameplay events (see behavior-designer-pro-connect-gameplay-code). Confirm the product in Prerequisites when the request does not name it."
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

# Configure Reactive Behaviors

Configure conditional reevaluation so changing input can interrupt the correct running branch without rebuilding the tree each frame.

## When to use this skill

- "My enemy keeps idling after it sees the player."
- "Stop this action when its condition becomes false."
- "Make fleeing take priority over attacking."

Not for: implementing perception or changing how a custom task computes its condition.

## Prerequisites

Use Behavior Designer Pro 3.3.3 and Unity 2022.3.11 or newer. The package requires Graph Designer, Opsive Shared, Entities, Burst, and Mathematics; retain the versions supplied with the installed release (Entities 1.3.8 in the verified manifest). Core tree logic does not require a render-pipeline change; sample materials may require their own setup.

Run this read-only C# in an available Unity Editor execution tool before making changes:

```csharp
var treeType = System.Type.GetType("Opsive.BehaviorDesigner.Runtime.BehaviorTree, Opsive.BehaviorDesigner.Runtime");
UnityEngine.Debug.Log(treeType != null ? "Behavior Designer runtime found" : "Behavior Designer runtime unavailable");
```

If unavailable, check the first compiler error and **Tools > Opsive > Behavior Designer > Welcome**. If the product is absent, report that this skill requires [Behavior Designer Pro](https://assetstore.unity.com/packages/tools/visual-scripting/behavior-designer-pro-3-dots-powered-behavior-trees-368344); do not install another edition as a substitute. Confirm the installed release in the Welcome window. Preserve unsaved scenes and use the user's intended tree. Run Play Mode checks only when the scene can safely enter Play Mode.


Use a tree with a known Running task and controllable condition. For a dependency-free demonstration, create a Graph-scoped Boolean named `Alert`, initially false, and compare it with a literal true using Bool Comparison.

## Quick start

1. Create Start -> Selector. Give the Selector two children ordered left to right: an alert Sequence and an idle Wait with Duration `30`.
2. Give the alert Sequence two children: Bool Comparison (`Bool1` bound to `Alert`, `Bool2` literal true), then an alert Wait with Duration `30`.
3. Select the alert Sequence and set Abort Type to **Both**. The default None does not enable conditional aborts.
4. Run the tree with Alert false. Confirm the idle Wait is Running.
5. Set Alert true in the runtime Shared Variables view or with `BehaviorTree.SetVariableValue<bool>`. Confirm the idle branch is interrupted and the alert Wait starts on a subsequent evaluation.
6. Set Alert false. Confirm the alert branch is interrupted and the idle branch resumes. Do not rely on a one-shot Log as the only active branch: keep the tree running while testing the reverse transition.

## Workflows

### Workflow: Select the abort scope

**Goal:** Reevaluate only the branch scope needed by the behavior.

**Steps:** Configure Abort Type on the composite owning the conditional branch:

| Value | Use |
|---|---|
| None | No conditional-abort reevaluation. |
| Self | A condition change should abort work within that branch. |
| Lower Priority | A condition change should interrupt a lower-priority branch to its right. |
| Both | The branch can preempt lower-priority work and stop its own work when the condition changes. |

Keep higher-priority branches left of lower-priority branches. Trace nested composites to identify which composite owns the condition and running work; placing an abort on an unrelated parent is not equivalent.

**Expected result:** Test input changes interrupt only the intended work, in both directions where requested.

### Workflow: Verify interruption cleanup

**Goal:** End gameplay side effects when the tree aborts a task.

**Steps:** Observe a managed action while Running, then change the condition to abort it. Check its `OnEnd()` cleanup and the gameplay component it controls. Re-enter the branch and check that `OnStart()` resets per-run state. A conditional should be a repeatable observation: avoid consuming events or starting gameplay effects in the predicate. Managed Conditional defaults `OnReevaluateUpdate()` to `OnUpdate()`; use a side-effect-free override when reevaluation needs different logic. For custom Entity conditionals, use [Entity tasks](../behavior-designer-pro-create-entity-tasks/SKILL.md) and its reevaluation reference.

**Expected result:** Aborted actions release their effects once and can start again without stale state.

### Workflow: Choose a different reaction mechanism

**Goal:** Match the trigger to the appropriate mechanism.

**Steps:** Use conditional aborts for changing predicates and branch priority. For a named gameplay message, use [events](../behavior-designer-pro-connect-gameplay-code/SKILL.md). For explicit interruption or an evaluator decorator, inspect the installed task's fields and scope before substituting it; neither is a universal replacement for abort configuration. Reproduce the existing failure first and change one mechanism at a time.

**Expected result:** The tree responds to the actual trigger without redundant polling or restart loops.

## Verification

- Save and reopen: the correct composite retains its Abort Type.
- With Alert false, only the idle Wait is active; with Alert true, only the alert Wait is active.
- Repeat false -> true -> false at least twice before the 30-second waits complete.
- Confirm branch cleanup and re-entry; Console and Error Window remain clear.
- Record input changes and task transitions, not just a screenshot of a successful end state.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `Opsive.BehaviorDesigner.Runtime.Tasks.ConditionalAbortType` | Enum | None, LowerPriority, Self, Both. |
| `Opsive.BehaviorDesigner.Runtime.Tasks.Composites.Sequence.AbortType` | Property | Configures Sequence reevaluation scope. |
| `Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals.Math.BoolComparison` | Conditional | Returns Success when two Boolean values match. |
| `Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals.Conditional.OnReevaluateUpdate()` | Virtual method | Managed conditional reevaluation callback. |
| `Opsive.BehaviorDesigner.Runtime.Tasks.Task.OnEnd()` | Virtual method | Managed task termination cleanup. |

## Common issues

- No reaction → Abort Type None or incorrect variable binding → inspect the owning composite and runtime variable value.
- Preemption works but return does not → Lower Priority alone → use Both if the active branch must also abort itself.
- Constant switching → noisy input → stabilize the gameplay predicate with explicit thresholds or hysteresis appropriate to the request.
- Behavior continues after the branch ends → custom action does not clean up → repair its lifecycle using the custom-task skill.

## Boundaries

Abort configuration does not implement sensors or fix an incorrect health value. Do not restart the whole tree every frame to conceal missing reevaluation. Respect intentional simultaneous work in Parallel branches rather than applying exclusive-branch assumptions to every tree.
