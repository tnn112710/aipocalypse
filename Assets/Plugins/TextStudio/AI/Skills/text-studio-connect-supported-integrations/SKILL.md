---
name: "text-studio-connect-supported-integrations"
description: "Use this skill whenever the user asks to connect Text Studio to a supported dialogue, localization, visual scripting, tweening, timeline, UI effect, spline, LED, or other optional integration."
metadata:
  asset: "Text Studio - Text Animation for Unity | TextMeshPro & UI Toolkit"
  publisher: "Impossible Robert"
  asset-version: "1.3.1"
  skill-version: "1.0.0"
  unity: "2023.2"
  render-pipelines: "Built-in, URP, HDRP, custom SRP"
  category: "Tools/GUI"
  asset-store-url: "https://assetstore.unity.com/packages/tools/gui/text-studio-text-animation-for-unity-textmeshpro-ui-toolkit-394000"
  documentation-url: "https://www.wetzold.com/tools/text-studio/docs/"
  support-url: "https://discord.com/invite/uzeHzEMM4B"
  last-verified: "2026-10-02"
---

# Connect Supported Integrations

Use the Integrations catalog to identify the exact companion package, then keep optional dependencies isolated from the Text Studio base package.

## When to use this skill

- Connecting Text Studio to a supported third-party tool.
- Choosing among dialogue, localization, scripting, tweening, Timeline, UIEffect, UniText, Splines, or LED integrations.
- Diagnosing why an optional integration is unavailable.

## Prerequisites

- Text Studio base works independently.
- The exact vendor product and supported version are installed or available.
- The matching Text Studio integration package is imported.

## Quick start

1. Open Tools/Text Studio/Integrations.
2. Select the exact installed vendor or companion product.
3. Read its compatibility and import guidance.
4. Import the matching top-level integration package.
5. Compile, create one minimal integration workflow, and verify it without changing the base package.

## Workflows

### Workflow: Install an integration

1. Confirm the vendor package and supported version first.
2. Import the matching TextStudio.Integration companion.
3. Let importing the top-level package activate it; do not invent a customer define.
4. Resolve compile errors by checking package versions and asmdef references.
5. Test one source-to-text round trip.

### Workflow: Troubleshoot an integration

1. Confirm the base Text Studio sample still works.
2. Verify the companion integration folder and vendor assemblies exist.
3. Inspect the integration-specific documentation and automatic version defines.
4. Reduce to one minimal scene and one integration component.
5. Keep fixes inside the integration package unless the base contract is actually wrong.

### Workflow: Configure Game Creator 2 Dialogue

1. Import the core Game Creator 2 integration before the separate Dialogue integration.
2. Select a SpeechSkin asset, SpeechUI prefab root, or SpeechUI scene instance, then choose Configure Selected on the Game Creator 2 Dialogue card.
3. Preserve the visible TextMeshPro or UnityEngine.UI.Text component. The provider redirects Game Creator's own typewriter output to an inactive TMP mirror so Text Studio exclusively owns visible presentation.
4. For Unity UI Text, verify the backend precedes Outline, Shadow, and custom Canvas mesh modifiers. Editable InputField output requires a separate presentation Text.
5. Repeat setup for every SpeechSkin used by default, actor, or expression overrides.

### Workflow: Choose UniText Edition and Appearance Ownership

1. Import the universal `TextStudio_UniText_Integration.unitypackage` for `media.lightside.unitext` Open Source `[1.0.0,2.0.0)` or Folio `[3.3.0-pre.7,4.0.0)`. The matching adapter activates automatically; import the matching edition's sample archive separately.
2. Plain `UniText` and `UniTextWorld` components retain native appearance. Text Studio Appearance settings remain stored with a diagnostic. On Folio 3.11+, explicitly choose the component's Use Text Studio Appearance command when portable Appearance is needed; the integration swaps to `TextStudioUniTextCanvas` or `TextStudioUniTextWorld` with Undo while preserving serialized configuration and references.
3. The owned-output components keep UniText shaping, layout, interaction, sorting, and render-root ownership. Supported Appearance draws from the published native glyphs; unsupported resources, native effect layers, decorations, media, or field combinations restore native output with a stated reason. Do not manually hide the UniText source to transfer ownership, because visibility also affects input picking.
4. For solid output, use `TextStudio3D_UniText_OpenSource_Sample.unitypackage` or `TextStudio3D_UniText_Folio_Sample.unitypackage` after the universal backend. UniText field deformation and virtual Rolling/Scramble output remain flat; shader effects do not become durable solid outlines. Keep native modifier clocks separate from Text Studio playback and qualify each edition you ship.

### Workflow: Share a Composed Effect With Timeline

1. Install Unity Timeline so the guarded base adapter is active, then select a Composed Effect asset.
2. Choose Assets > Create > Text Studio > Animation > Timeline From Composed Effect and save the Timeline. Bind its effect track to the intended TextStudioText.
3. Open Composition from the clip inspector to edit the shared asset. Every clip referencing that composition sees the changes.
4. Use Create Variant when one clip needs independent layers. This assigns a new asset while retaining the clip's timing, easing, weight curve, and glyph range.
5. Keep scheduling and whole-composition blending in Timeline. This workflow does not flatten arbitrary tracks into a Composed Effect.

## Verification

- The base Text Studio assemblies compile with or without optional packages as designed.
- The exact matching integration and vendor version are used.
- One representative value reaches the intended Text Studio host.
- Removing the integration returns the base package to a clean compile state.
- No customer-managed first-party activation symbol was added.

## API quick reference

- Tools/Text Studio/Integrations opens the supported integration catalog.
- Top-level TextStudio.Integration packages are activated by import.
- Available companions include Adventure Creator, Game Creator 2, Ink, LED Studio, Naninovel, Pixel Crushers, PlayMaker 2, PrimeTween, UIEffect, UniText, Unity Localization, Unity Splines, Unity Timeline, Visual Scripting, and Yarn Spinner.
- Vendor-specific symbols remain limited to genuinely optional vendor compatibility.
- The TextStudioExtensionRegistry is target-scoped for stateful extensions.
- Game Creator 2 Dialogue supports a visible TMP or legacy Unity UI Text target. Re-running Configure Selected repairs and reuses the existing hub, source, backend, dialogue unit, and hidden mirror.

## Common issues

- **Integration menu exists but types are missing:** Confirm the vendor package version and the matching integration import.
- **Base Text Studio stops compiling:** Remove accidental optional references from base assemblies and restore the package boundary.
- **A manually added define is required:** Recheck the integration's asmdef and version-define contract; top-level first-party integration import is the activation boundary.

## Boundaries

- Do not add vendor references to TextStudio runtime or editor base assemblies.
- Do not create undocumented scripting defines to activate first-party top-level integrations.
- Do not merge independent optional integrations into one assembly.
- Do not claim support for a vendor or version absent from the installed integration catalog.
