---
name: "behavior-designer-pro-reuse-subtrees"
description: "Use this skill whenever the user wants to 'share this behavior between enemies', 'extract this branch', 'override a subtree variable', or 'switch subtrees' in Behavior Designer Pro. Covers whole-tree export, branch extraction, references, per-instance overrides, and runtime selection. Do NOT use for ordinary task connections (see behavior-designer-pro-build-behavior-trees) or general variable scripting (see behavior-designer-pro-connect-gameplay-code). Confirm the installed product before assuming an external-tree API."
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
  last-verified: "2026-11-10"
---

# Create and Reuse Subtrees

Turn behavior into reusable Subtree assets while preserving source-tree behavior and explicit per-instance configuration.

## When to use this skill

- "Share this attack branch between three enemies."
- "Extract just this sequence into a subtree."
- "Give each enemy a different delay without copying the tree."
- "Switch behavior based on a selected index."

Not for: ordinary graph layout or implementing new gameplay actions.

## Prerequisites

Use Behavior Designer Pro 3.3.4 and Unity 2022.3.11 or newer. The package requires Graph Designer, Opsive Shared, Entities, Burst, and Mathematics; retain the versions supplied with the installed release (Entities 1.3.8 in the verified manifest). Core tree logic does not require a render-pipeline change; sample materials may require their own setup.

Run this read-only C# in an available Unity Editor execution tool before making changes:

```csharp
var treeType = System.Type.GetType("Opsive.BehaviorDesigner.Runtime.BehaviorTree, Opsive.BehaviorDesigner.Runtime");
UnityEngine.Debug.Log(treeType != null ? "Behavior Designer runtime found" : "Behavior Designer runtime unavailable");
```

If unavailable, check the first compiler error and **Tools > Opsive > Behavior Designer > Welcome**. If the product is absent, report that this skill requires [Behavior Designer Pro](https://assetstore.unity.com/packages/tools/visual-scripting/behavior-designer-pro-3-dots-powered-behavior-trees-368344); do not install another edition as a substitute. Confirm the installed release in the Welcome window. Preserve unsaved scenes and use the user's intended tree. Run Play Mode checks only when the scene can safely enter Play Mode.


Start with a valid saved graph and identify whether the request concerns the whole tree or one branch. Scene references must be supplied by the owning scene instance, not assumed portable inside an asset. Check for `Opsive.BehaviorDesigner.Runtime.Subtree` and the Subtree Reference task in the installed product.

## Quick start

1. Open the tree and select the logic root of the branch to reuse.
2. Right-click the selection and choose **Save as Subtree**. Choose a new asset path inside the customer's project.
3. Inspect the parent tree: the selected branch is replaced with a Subtree Reference. Inspect the new asset and compare its nodes with the original branch.
4. Configure required variable overrides on the reference. On a prefab asset or variant, the override dropdown must include compatible variables from the parent tree. Select the parent variable, save both assets, and verify the binding after reimporting the prefab. The Subtree asset defaults must remain unchanged.
5. Run the parent tree. Expected result: the extracted behavior still runs through the reference. Reopen both assets to verify persistence.

## Workflows

### Workflow: Choose export or extraction

**Goal:** Preserve exactly the intended source graph.

**Steps:** Use the graph toolbar's Export operation to copy the complete graph to a Subtree asset without replacing the source. Use **Save as Subtree** on a selection to extract and replace a branch. With multiple selected tasks, extraction includes their lowest common ancestor and all its descendants, not only the highlighted tasks. Event nodes cannot be selected for branch extraction. For an empty asset use **Assets > Create > Opsive > Behavior Designer > Subtree**, which creates the wrapper ScriptableObject type.

**Expected result:** Whole-graph export preserves the source; branch extraction replaces precisely the verified subtree extent.

### Workflow: Configure independent instances

**Goal:** Reuse structure while giving each reference different parameters.

**Steps:** Define a Graph variable in the Subtree and bind its task fields. Place a Subtree Reference in each caller and assign the asset in its Subtrees list. Set variable overrides on each reference, using the matching name/type or a caller variable as offered by the inspector. A variable marked Internal is intentionally excluded from override controls. Test two references with distinct values simultaneously. Inspect the original Subtree defaults afterward to ensure they were not edited in place to simulate an instance override.

**Expected result:** Shared structure stays in one asset; each caller observes its own parameter values.

### Workflow: Select behavior at runtime

**Goal:** Choose one of a known set of subtree assets.

**Steps:** Use Subtree Reference Selector, populate its Subtrees array, and bind Index to a Graph integer. Supply an index within `[0, count)`. Reevaluate On Index Change defaults to true; test a runtime index change through the tree variable API. Do not use an invalid index to mean "no behavior": the implementation falls back to the original array when no selection is produced. For custom selection, override `EvaluateSubgraphs(Opsive.GraphDesigner.Runtime.IGraphComponent)` and `Subtrees`; do not copy older parameterless `EvaluateSubtrees` examples. When requesting explicit reinjection, `BehaviorTree.ReevaluateSubtreeReferences()` restarts the tree, so verify active-action cleanup and re-entry.

**Expected result:** A valid selection activates the intended subtree and a change leaves no abandoned gameplay effects.

## Verification

- The saved asset exists, reopens, and contains the expected branch or whole graph.
- A branch extraction leaves a connected Subtree Reference at the original location.
- Both callers execute with independent overrides; asset defaults remain unchanged.
- Runtime selection tests exercise at least two valid indices and return to the first.
- Graph validation and Console are clear; scene-object references resolve on the intended caller.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `Opsive.BehaviorDesigner.Runtime.Wrappers.Subtree` | ScriptableObject | Asset-facing wrapper created by the asset menu. |
| `Opsive.BehaviorDesigner.Runtime.Tasks.Actions.SubtreeReference` | Node | Loads referenced graphs and exposes variable overrides. |
| `Opsive.BehaviorDesigner.Runtime.Tasks.Actions.SubtreeReferenceSelector` | Node | Selects a subtree using an integer index. |
| `SubtreeReference.EvaluateSubgraphs(Opsive.GraphDesigner.Runtime.IGraphComponent)` | Virtual method | Computes a custom selection before graph retrieval. |
| `Opsive.BehaviorDesigner.Runtime.BehaviorTree.ReevaluateSubtreeReferences()` | Method | Rebuilds references by restarting the tree. |

## Common issues

- Too much extracted → multiple selection includes a wider common ancestor → Undo and select the intended branch root.
- Override absent → variable marked Internal or wrong type → inspect the Subtree declaration and caller mapping.
- Reference fails at runtime → missing/unloaded subtree → check asset assignments and graph errors; an unresolved Subtree Reference returns Failure.
- Every NPC changes → shared asset defaults edited instead of overrides → restore defaults and configure the callers.

## Boundaries

Do not manually rewrite injected runtime indices or serialized graph payloads. Runtime selection is not a substitute for conditional aborts. This skill does not promise transparent state preservation across reinjection; test stop/restart behavior. Use the installed API when supporting older releases rather than applying current signatures blindly.
