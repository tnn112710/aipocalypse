---
name: "behavior-designer-pro-use-atlas"
description: "Use this skill whenever the user asks to use Opsive Atlas to inspect, review, diagnose, plan, generate, or modify Behavior Designer Pro tasks. Prefer available Atlas connector tools for project context, exact API contracts, validation, and preview/apply; retain the Unity UI fallback. Do NOT use for texture atlases or direct task authoring where Atlas was not requested (see behavior-designer-pro-create-gameobject-tasks)."
metadata:
  asset: "Behavior Designer Pro"
  publisher: "Opsive"
  asset-version: "3.3.4"
  skill-version: "1.1.0"
  unity: "2022.3.11+"
  render-pipelines: "Built-in, URP, HDRP (core behavior logic)"
  category: "tools/visual-scripting"
  asset-store-url: "https://assetstore.unity.com/packages/tools/visual-scripting/behavior-designer-pro-3-dots-powered-behavior-trees-368344"
  documentation-url: "https://opsive.com/support/documentation/behavior-designer-pro/"
  support-url: "https://opsive.com/support/"
  last-verified: "2026-10-09"
---

# Use Opsive Atlas with Behavior Designer Pro

Use Atlas's supported connector when available to obtain product evidence and produce reviewed changes; use the Unity interface on installations without the connector.

## When to use this skill

- "Ask Atlas to plan this custom task."
- "Use Atlas to check this code against my installed APIs."
- "Have Atlas diagnose this graph or modify the selected script."

Use the user's requested outcome: an explanation or review does not authorize project edits.

## Prerequisites

For connected project work, use the following product requirements. Standalone account-mode knowledge/review does not require a running Unity project.

Use Behavior Designer Pro 3.3.3 and Unity 2022.3.11 or newer. The package requires Graph Designer, Opsive Shared, Entities, Burst, and Mathematics; retain the versions supplied with the installed release (Entities 1.3.8 in the verified manifest). Core tree logic does not require a render-pipeline change; sample materials may require their own setup.

If connector discovery is unavailable, check the local runtime with this read-only C# in an available Editor execution tool:

```csharp
var treeType = System.Type.GetType("Opsive.BehaviorDesigner.Runtime.BehaviorTree, Opsive.BehaviorDesigner.Runtime");
UnityEngine.Debug.Log(treeType != null ? "Behavior Designer runtime found" : "Behavior Designer runtime unavailable");
```

If unavailable, check the first compiler error and **Tools > Opsive > Behavior Designer > Welcome**. If the product is absent, report that this skill requires [Behavior Designer Pro](https://assetstore.unity.com/packages/tools/visual-scripting/behavior-designer-pro-3-dots-powered-behavior-trees-368344); do not install another edition as a substitute. Confirm the installed release in the Welcome window. Preserve unsaved scenes and use the user's intended tree. Run Play Mode checks only when the scene can safely enter Play Mode.

Atlas is separately installed; its Behavior Designer Pro integration requires 3.3.0 or newer. Discover the installed connector tools and call `atlas_get_capabilities`. Distinguish installed versions, server-enabled products, account grants, and available operations. The inspected Atlas package still reports 0.3.0, so that number alone does not prove connector availability.

When a local connection is needed, use **Tools > Opsive > Atlas > Connect an AI assistant**, enable the connection, and supply the copied connection-file path to `atlas_connect`. Obtain the path for the intended project; do not guess between projects or read/print its credential. The connector rereads the descriptor after reloads. The complete Atlas plugin supplies the stdio MCP tools and requires Node 22 or newer; these product skill files do not install it. Follow the assistant's supported plugin setup if absent. No private reflection or invented HTTP endpoint is needed.

Standalone account mode supports capabilities, version-matched knowledge, API contracts, and structural validation only. Project snapshots, delegated generation, preview/apply, and operation tracking require the Unity connection. Respect the configured provider (including available local Codex or Claude Code support) and Atlas access policy rather than forcing a provider change.

## Quick start

1. Use the available Atlas tools and confirm the intended product `behavior_designer` and project with `atlas_get_capabilities`. Copy its installed version object into `assetVersion`; do not hard-code this skill's metadata version.
2. For connected work, call `atlas_get_project_context` on the intended selection. A bounded snapshot is not execution history; missing/truncated data does not prove the graph lacks a node or setting.
3. Search `atlas_search_knowledge` with the exact version and inspect relevant fully qualified types with `atlas_get_api_contract`. Prefer a suitable existing feature over a redundant custom task.
4. For a plan-only request, call `atlas_generate` with a fresh UUID requestId, asset/version, `action: ask`, and request containing `mode: plan` and userPrompt such as `Plan a GameObject task that sets a shared Working bool while running, then clears it on completion or interruption.`. Poll its operationId with `atlas_get_operation`.
5. Expected result: an evidenced Atlas plan and no applied files. For implementation, follow the validation and preview/apply workflow below. If the connector is unavailable, use the UI fallback explicitly.

## Workflows

### Workflow: Inspect, review, or diagnose

**Goal:** Establish product-specific evidence without generating or changing code unnecessarily.

**Steps:** Match the exact requested product/version. Inspect selected-object context only when connected. Use knowledge searches for intended behavior and API contracts for signatures; reflected contracts do not expose method bodies. Keep allowApproximateCatalog false unless approximate catalog evidence is intentionally useful, and label/check its actual source version. Documentation never falls back. Missing exact evidence is a limitation, not permission to claim compatibility. For code review, call atlas_validate_artifacts on unchanged source. It performs structural/product checks without a model call; compilation/runtime remain not_checked. Report findings separately from hypotheses, with source locations and observable consequences. In account-only mode, use supplied code and an explicit target version without claiming local inspection.

**Expected result:** The answer distinguishes installed API evidence, structural validation, and unverified runtime behavior; review-only work leaves files untouched.

### Workflow: Generate or modify code through Atlas

**Goal:** Delegate the requested implementation using the correct product contract.

**Steps:** Read [connector requests and operation handling](references/connector-workflow.md) for action-specific request fields and result translation. Supply runtime style, input types, completion rules, and reset/cleanup needs. For modify, supply the actual current targetCode or targetFiles and the behavior to preserve. Omit modelSelection to retain Atlas's selected provider/model unless the user requests another available choice. Generation can consume model usage and automatically includes bounded project signatures, selected-object context, and enabled project instructions. Poll the returned operation rather than launching duplicate requests. A generated result contains previews, not applied files. Use Behavior Designer Pro task bases and TaskStatus.Success/Failure/Running. Verify interruption cleanup and re-entry with the [GameObject task skill](../behavior-designer-pro-create-gameobject-tasks/SKILL.md) or [Entity task skill](../behavior-designer-pro-create-entity-tasks/SKILL.md).

**Expected result:** Atlas returns relevant advice or a complete reviewed artifact set, with the source version and operation outcome recorded.

### Workflow: Preview, apply, and verify an authorized change

**Goal:** Apply the intended code while preserving current project edits.

**Steps:** Validate the reviewed artifacts, then call atlas_preview_changes with a fresh UUID requestId and allowed customer C# paths under Assets, excluding Assets/Opsive. Inspect original/proposed source and revisions. Pass the preview's operationId as changeSetId to atlas_apply_changes. Respect any Unity approval dialog enforced by Atlas; never fabricate approval arguments or bypass its policy. For an unknown response, retry the same requestId with unchanged input or poll the operation. A stale file or changed installed version requires renewed inspection and a fresh preview. Poll the apply operation for actual compiler evidence, then test the requested gameplay through separately available Unity tooling or the Editor. Plan/review-only requests stop before apply.

**Expected result:** The intended files are applied, real compile status is known, and runtime checks are reported independently.

### Workflow: Correct or recover an operation

**Goal:** Resolve a concrete failure without repeated paid requests or lost edits.

**Steps:** Use actual compilerDiagnostics and the prior generationId for Atlas correction. For assistant-authored source with no generationId, revise that source and preview again. Poll recoverable interrupted generation using its original ID. For interrupted/apply_failed/recovery_required results, inspect affected files and the retained journal before retrying; preserve concurrent edits. Cancellation does not roll back applied files. Stop repetitive corrections when no new evidence is available. The connector cannot save scenes, start Play Mode, build, or execute arbitrary C#; use an appropriate separate tool only within the user's scope.

**Expected result:** The operation either finishes with evidence or has a precise unresolved status and preserved project files.

### Workflow: Use an older Atlas UI

**Goal:** Complete the same request when the installed connector is unavailable.

**Steps:** Open **Tools > Opsive > Behavior Designer > Atlas** or **Tools > Opsive > Atlas > AI Assistant**. Confirm the product and selected script. Use Ask/Plan/Generate or `/ask`, `/plan`, `/gen`, `/modify` where supported. Review file previews before Apply/Save as/Apply all, then wait for Unity compilation and test behavior. Preserve complete multi-file batches; use Resume package build when offered. `/skill`, where present, controls reasoning effort rather than loading these files. Report the actual UI path used. If neither tools nor UI control is available, provide the prepared request and state that Atlas was not contacted.

**Expected result:** The user receives an honestly reported UI-based outcome with the same scope and validation boundaries.

## Verification

- Confirm product, version, project/selection, authorization, and connector versus account-only/UI capabilities.
- Retain exact source versions and label unavailable or approximate evidence.
- Separate structural validation, generation, applied files, compiler results, and runtime tests.
- Check reviewed destinations and stale revisions; preserve unrelated source and serialized identities.
- Test product-specific completion/conditions, interruption, and re-entry when implementation is requested.
- Do not count a simulated request, static validation, or generated preview as a live Atlas/runtime pass.

## API quick reference

| Tool / surface | Purpose |
|---|---|
| `atlas_connect` / `atlas_get_capabilities` | Bind an explicit local connection; discover project, product/version and access. |
| `atlas_get_project_context` | Bounded selected-object/graph snapshot. |
| `atlas_search_knowledge` / `atlas_get_api_contract` | Version-matched guidance and exact type signatures. |
| `atlas_validate_artifacts` | Non-generative structural validation of unchanged source. |
| `atlas_generate` | Asynchronous ask/plan/review, generation, modify, correction, or test generation. |
| `atlas_preview_changes` / `atlas_apply_changes` | Reviewed change set and policy-controlled application. |
| `atlas_get_operation` / `atlas_cancel_operation` | Observe status or cancel pending work without implicit undo. |
| Tools > Opsive > Behavior Designer > Atlas | UI fallback. |

## Common issues

- **Tools absent:** check plugin availability; Atlas's package version or window alone does not prove the connector is installed.
- **Only four tools available:** account-only mode cannot inspect or edit the local Unity project.
- **Wrong/missing version:** refresh capabilities or report exact_contract_unavailable; do not relabel old catalog results.
- **stale_preview / installed_version_mismatch:** inspect current source/version and create a fresh preview.
- **editor_busy / approval_declined:** respect the Editor state and local access policy; do not bypass them through direct writes.
- **Validation passed but behavior failed:** structural checks are not compilation or gameplay tests; reproduce the lifecycle/binding fault.

## Boundaries

Use supported connector tools or the UI; internal bridge classes are not a reflection API. Do not start recursive Atlas delegation from an Atlas-owned provider completion. Connection setup and file writes must remain within the user's request. These skills do not establish hosted deployment, account entitlements, or release availability; detect capabilities in the actual installation. See the [product Atlas documentation](https://opsive.com/support/documentation/behavior-designer-pro/integrations/atlas/) for the UI workflow.
