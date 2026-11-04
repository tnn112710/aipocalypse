---
name: "behavior-designer-pro-setup-and-overview"
description: "Use this skill whenever the user wants to set up Behavior Designer Pro, 'get a behavior tree running', 'open the AI editor', or 'fix missing dependencies'. Covers installation checks, the first running tree, task execution choices, and routing to the other Behavior Designer Pro skills. Do NOT use for writing a custom task (see behavior-designer-pro-create-gameobject-tasks). When in doubt whether a behavior-tree setup request involves Behavior Designer Pro, use Prerequisites to confirm the product is installed."
metadata:
  asset: "Behavior Designer Pro"
  publisher: "Opsive"
  asset-version: "3.3.4"
  skill-version: "1.0.2"
  unity: "2022.3.11+"
  render-pipelines: "Built-in, URP, HDRP (core behavior logic)"
  category: "tools/visual-scripting"
  asset-store-url: "https://assetstore.unity.com/packages/tools/visual-scripting/behavior-designer-pro-3-dots-powered-behavior-trees-368344"
  documentation-url: "https://opsive.com/support/documentation/behavior-designer-pro/"
  support-url: "https://opsive.com/support/"
  last-verified: "2026-10-12"
---

# Set Up Behavior Designer Pro

Verify the installed product and create a minimal, saved tree whose execution can be observed in Unity.

## When to use this skill

- "Get Behavior Designer working in this project."
- "Do I need ECS to use this on my GameObject?"
- "Open the editor and make a first tree."

Not for: custom task code or diagnosing an already-running tree; use the matching skill in the index below.

## Prerequisites

Use Behavior Designer Pro 3.3.3 and Unity 2022.3.11 or newer. The package requires Graph Designer, Opsive Shared, Entities, Burst, and Mathematics; retain the versions supplied with the installed release (Entities 1.3.8 in the verified manifest). Core tree logic does not require a render-pipeline change; sample materials may require their own setup.

Run this read-only C# in an available Unity Editor execution tool before making changes:

```csharp
var treeType = System.Type.GetType("Opsive.BehaviorDesigner.Runtime.BehaviorTree, Opsive.BehaviorDesigner.Runtime");
UnityEngine.Debug.Log(treeType != null ? "Behavior Designer runtime found" : "Behavior Designer runtime unavailable");
```

If unavailable, check the first compiler error and **Tools > Opsive > Behavior Designer > Welcome**. If the product is absent, report that this skill requires [Behavior Designer Pro](https://assetstore.unity.com/packages/tools/visual-scripting/behavior-designer-pro-3-dots-powered-behavior-trees-368344); do not install another edition as a substitute. Confirm the installed release in the Welcome window. Preserve unsaved scenes and use the user's intended tree. Run Play Mode checks only when the scene can safely enter Play Mode.


## Quick start

1. Open **Tools > Opsive > Behavior Designer > Welcome**. If the license gate appears, have the user complete the eligibility and EULA workflow below. Resolve missing required packages through the package workflow shown there. Wait for compilation to finish.
2. Select the intended GameObject and open **Tools > Opsive > Behavior Designer > Editor**.
3. Right-click empty graph space and choose **Add Behavior Tree** if the object does not already have the intended component. Use the editor/Add Component workflow, which creates `Opsive.BehaviorDesigner.Runtime.Wrappers.BehaviorTree`; do not add the implementation base class directly.
4. Use **Create Task** in the graph context menu and search for **Sequence**. The editor supplies a Start event for the first logic task. Confirm Start connects to Sequence.
5. Add **Wait** (`Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Time.Wait`) and **Log** (`Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Debug.Log`) as Sequence children, in that left-to-right order. Set Wait Duration to `1`, Random Duration to false, and Log Text to `AI skill ready`.
6. Open **Tools > Opsive > Behavior Designer > Error Window** and resolve errors. Save the scene, enter Play Mode, and observe the Console.
7. Expected result: one `AI skill ready` message after the wait, with no errors. Stop Play Mode and reopen the saved graph to confirm its connections persist.

## Workflows

### Workflow: Complete the license gate

**Goal:** Complete the user's financial eligibility declaration and separate EULA acceptance before using the editor.

**Steps:** The user selects one radio option: **Personal License** (combined gross revenue and funding below $200,000 USD over the preceding 12 months), **Professional License Required** ($200,000 or more combined), or **Existing Professional Subscriber** (an active Professional license for their seat). No option is selected initially, and no financial amounts are requested. Do not make these declarations for the user. The user must separately accept the linked EULA. **Accept and Continue** closes the gate for Personal or an existing Professional subscriber. Professional License Required opens the next screen linking to the Behavior Designer Pro page. Opening that link does not dismiss the gate; the user can return to change their selection or confirm they now have an active Professional license. Exactly $200,000, including $50,000 revenue plus $150,000 funding, requires Professional. Users who accepted the earlier four-checkbox gate must complete this eligibility check once.

**Expected result:** The gate closes across open Behavior Designer windows only after an explicit radio selection, EULA acceptance, and the applicable eligibility declaration. The host editor windows remain open. The check uses the combined inclusive threshold published in the EULA and on the landing page on October 11, 2026.

### Workflow: Choose the execution style

**Goal:** Use the task model appropriate to the existing gameplay code.

**Steps:** Use GameObject tasks for components, transforms, animation, and other managed gameplay APIs. Use Entity tasks when the requested runtime logic must execute over unmanaged ECS data. Both use the DOTS-based tree runtime; GameObject tasks still require Entities. A tree may contain both styles. Choose custom Entity authoring only when the behavior or performance requirement justifies it.

**Expected result:** The chosen tasks can access the required data without introducing an unnecessary conversion of the user's game to ECS.

### Workflow: Route the next request

**Goal:** Load only the instructions needed for the next job.

**Steps:** Select a sibling skill by outcome:

| Skill | Job |
|---|---|
| [Build behavior trees](../behavior-designer-pro-build-behavior-trees/SKILL.md) | Create and edit task topology and behavior recipes. |
| [Configure reactive behaviors](../behavior-designer-pro-configure-reactive-behaviors/SKILL.md) | Change branches when conditions change. |
| [Connect gameplay code](../behavior-designer-pro-connect-gameplay-code/SKILL.md) | Bind variables, events, and lifecycle controls. |
| [Reuse subtrees](../behavior-designer-pro-reuse-subtrees/SKILL.md) | Share branches and configure per-instance overrides. |
| [Create GameObject tasks](../behavior-designer-pro-create-gameobject-tasks/SKILL.md) | Write managed actions and conditionals. |
| [Create Entity tasks](../behavior-designer-pro-create-entity-tasks/SKILL.md) | Write ECS data and task systems. |
| [Debug behavior trees](../behavior-designer-pro-debug-behavior-trees/SKILL.md) | Explain and fix observed execution failures. |
| [Use Atlas](../behavior-designer-pro-use-atlas/SKILL.md) | Ask Atlas to plan, generate, or refine tasks. |

**Expected result:** The agent uses the relevant workflow without loading every skill.

### Workflow: Inspect installation and samples

**Goal:** Distinguish a missing product from an incomplete import.

**Steps:** Check required assemblies and the first Console error. Inspect the installed package's manifest rather than guessing dependency upgrades. Open **Tools > Opsive > Behavior Designer > Samples** for examples. Locate the package through Package Manager or its imported Opsive folder; do not assume every customer has the publisher's source layout. Runtime/editor implementations may be DLLs; use public APIs and UI when source is absent.

**Expected result:** The runtime type resolves, the editor opens, and samples are imported only when needed for the user's request.

## Verification

- The installation check reports the runtime type found; there are no compile errors.
- The selected object has the intended tree component, with Start, Sequence, Wait, and Log connected.
- The Error Window is clear; Play Mode produces the expected log once.
- Reopening the saved scene preserves the tree and field values.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `Opsive.BehaviorDesigner.Editor.BehaviorDesignerWindow.ShowWindow()` | Static method | Opens the graph editor. |
| `Opsive.BehaviorDesigner.Editor.BehaviorDesignerWindow.ShowErrorWindow()` | Static method | Opens graph validation. |
| `Opsive.BehaviorDesigner.Runtime.Wrappers.BehaviorTree` | Component | Scene/prefab-facing tree component. |
| `Opsive.BehaviorDesigner.Runtime.BehaviorTree` | Runtime base | Public tree control and variable APIs. |

## Common issues

- Editor missing → incomplete import or compilation → resolve the first Console error and check dependencies in Welcome.
- Empty tree never runs → no connected Start branch → connect Start to a valid logic root.
- Tree does not start automatically → Start When Enabled is off → enable it if automatic startup is intended, or explicitly start the tree.
- Visual sample problems → sample materials differ from the active pipeline → fix sample assets independently of tree logic.

## Boundaries

This skill covers Behavior Designer Pro, not the original Behavior Designer API. The documented requirements exclude WebGL. Installing the tree runtime does not install optional Movement, Senses, Tactical, or Formations packs. Do not change the render pipeline to repair a tree. Authentication, purchases, and optional downloads remain separate from an installation check.
