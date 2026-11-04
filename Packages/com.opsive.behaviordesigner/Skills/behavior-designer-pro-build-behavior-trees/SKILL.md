---
name: "behavior-designer-pro-build-behavior-trees"
description: "Use this skill whenever the user wants to 'build an enemy behavior tree', 'add a fallback', 'repeat this action', or edit Behavior Designer Pro tasks and connections. Covers graph construction, execution order, bounded repetition, saving, and verification. Do NOT use for custom task implementation (see behavior-designer-pro-create-gameobject-tasks) or conditional-abort configuration (see behavior-designer-pro-configure-reactive-behaviors). When the request mentions only NPC AI, confirm Behavior Designer Pro is installed before using this workflow."
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

# Build and Edit Behavior Trees

Translate a gameplay requirement into a saved Behavior Designer Pro graph with explicit execution and completion behavior.

## When to use this skill

- "Wait, then run this action."
- "Try this first and fall back if it fails."
- "Repeat this behavior three times."

Not for: writing missing task implementations, navigation setup, or configuring reactive interruption. Use the appropriate sibling skill or installed add-on.

## Prerequisites

Use Behavior Designer Pro 3.3.3 and Unity 2022.3.11 or newer. The package requires Graph Designer, Opsive Shared, Entities, Burst, and Mathematics; retain the versions supplied with the installed release (Entities 1.3.8 in the verified manifest). Core tree logic does not require a render-pipeline change; sample materials may require their own setup.

Run this read-only C# in an available Unity Editor execution tool before making changes:

```csharp
var treeType = System.Type.GetType("Opsive.BehaviorDesigner.Runtime.BehaviorTree, Opsive.BehaviorDesigner.Runtime");
UnityEngine.Debug.Log(treeType != null ? "Behavior Designer runtime found" : "Behavior Designer runtime unavailable");
```

If unavailable, check the first compiler error and **Tools > Opsive > Behavior Designer > Welcome**. If the product is absent, report that this skill requires [Behavior Designer Pro](https://assetstore.unity.com/packages/tools/visual-scripting/behavior-designer-pro-3-dots-powered-behavior-trees-368344); do not install another edition as a substitute. Confirm the installed release in the Welcome window. Preserve unsaved scenes and use the user's intended tree. Run Play Mode checks only when the scene can safely enter Play Mode.


Inventory available tasks with the editor's Create Task search. Confirm optional movement/sensing tasks exist before designing around them. Establish what each leaf considers Success, Failure, and Running.

## Quick start

1. Select the target object and open **Tools > Opsive > Behavior Designer > Editor**. Use **Add Behavior Tree** in the background menu only if a new tree is needed.
2. Use **Create Task** to add Sequence. Confirm Start connects to it.
3. Add Wait and Log; connect Sequence to both, with Wait to the left. Set Wait Duration to `1`, Random Duration false, and Log Text to `Finished waiting`.
4. Use **Tools > Opsive > Behavior Designer > Error Window** to check structure and required fields. Save the scene or prefab using the normal Unity save workflow.
5. Run the tree. Expected result: Wait is Running before completion, then Log executes and succeeds. Reopen the saved graph after exiting Play Mode.

## Workflows

### Workflow: Build a fallback

**Goal:** Attempt the preferred branch before a backup.

**Steps:** Add a Selector with the preferred branch on the left and fallback on the right. Put a condition and its dependent action inside a Sequence when both are required. A Sequence stops on the first Failure; a Selector stops on the first Success. Running prevents advancing to the next sibling. Test both condition outcomes. If an already-running fallback must be preempted, use [reactive behaviors](../behavior-designer-pro-configure-reactive-behaviors/SKILL.md).

**Expected result:** A failed preferred branch reaches the fallback; a successful preferred branch does not.

### Workflow: Repeat with a known bound

**Goal:** Repeat a child without creating an accidental endless loop.

**Steps:** Place the child under Repeater. Its defaults are Repeat Forever true, Repeat Count `0`, and End On Failure false. For exactly three successful iterations, set Repeat Forever false and Repeat Count `3`. Use a Sequence containing Wait then the action to insert time between attempts. Decide whether failure should end repetition and configure End On Failure accordingly. For retry-until-success, Until Success repeats its child but does not provide an attempt limit; do not describe it as a bounded retry.

**Expected result:** The recorded action count matches the requested bound and delays occur between attempts.

### Workflow: Edit an existing graph

**Goal:** Change only the behavior requested and preserve authoring data.

**Steps:** Identify the owning scene component, prefab, or subtree before editing. Record the existing connections and values affected by the request. Use editor graph operations to add, connect, or remove tasks; use Undo if the resulting topology is wrong. Reposition siblings to establish execution order. Save, reopen, and test the changed path plus an unaffected sibling path. Do not hand-edit Unity YAML, serialized graph payloads, authoring layers, or runtime topology indices.

**Expected result:** The intended behavior changes and persists without changing unrelated branches or prefab instances.

## Verification

- Start has a connected root; actions have no unintended children; decorators have one child.
- The Error Window reports no errors.
- Exercise each branch with controlled input; record the sequence of task statuses or uniquely labelled Log messages.
- Verify negative paths: fallback is skipped after success; a Running child does not prematurely advance.
- Confirm the saved tree reloads with the same task count, connections, and configuration.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `Opsive.BehaviorDesigner.Runtime.Tasks.Composites.Sequence` | Composite | Requires children to succeed in order. |
| `Opsive.BehaviorDesigner.Runtime.Tasks.Composites.Selector` | Composite | Tries children in order until one succeeds. |
| `Opsive.BehaviorDesigner.Runtime.Tasks.Decorators.Repeater` | Decorator | Repeats one child with configured limits. |
| `Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Time.Wait` | Entity action | Runs for Duration; defaults to one second without randomization. |
| `Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Debug.Log` | GameObject action | Logs Text and returns Success. |

## Common issues

- Fallback never runs → preferred child remains Running → inspect its completion condition rather than rearranging the Selector blindly.
- Unexpected priority → sibling order differs from the intended order → inspect left-to-right layout and retest.
- Endless repetition → Repeat Forever remains enabled → set a finite count or an explicit stopping condition.
- Missing patrol task → optional pack is absent → report the missing dependency or use an existing project task; do not invent a built-in node.

## Boundaries

This skill assembles available tasks. It does not implement a navigation system, infer combat rules, or require add-ons that are absent. Reuse existing tasks before generating code. Use [subtrees](../behavior-designer-pro-reuse-subtrees/SKILL.md) when the change is a reusable behavior asset.
