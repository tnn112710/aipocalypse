---
name: "text-studio-bind-ui-toolkit-text"
description: "Use this skill whenever the user asks to animate a UI Toolkit Label, configure TextStudioBinding, bind label text or data, or handle Unity-version-specific UI Toolkit text capabilities."
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
  last-verified: "2026-08-29"
---

# Bind UI Toolkit Text

For finite responses on a bound Label, load `text-studio-author-reactions` and assign its recipe to `TextStudioBinding`.

Attach one TextStudioBinding CustomBinding to a normal Label.text slot and keep source, renderer, and lifecycle ownership explicit.

## When to use this skill

- Animating an existing UI Toolkit Label.
- Binding Label text, a VisualElement, or data through TextStudioBinding.
- Selecting Basic or native per-glyph behavior across Unity versions.

## Prerequisites

- Binding and component hosts share replacement and replay semantics. `ShowText` restarts unchanged content, while `SetText` is idempotent. UI Toolkit falls back to hide-then-reveal when separate outgoing mesh retention is unavailable.
- Count and select semantic text units rather than UTF-16 code units. `WordBoundaryProvider` can tailor language-specific word ends without replacing the renderer's shaping system.

- Unity 2023.2 or newer is used.
- A UIDocument, or PanelRenderer on Unity 6000.5+, supplies PanelSettings and a target Label. Keep shipped scenes and example builders on UIDocument for compatibility; reuse their controller scripts with either supported host.
- The source mode and update owner are known.

## Quick start

1. Keep the target as a normal UnityEngine.UIElements.Label.
2. Add one TextStudioBinding to the Label.text binding slot.
3. Choose Label Text, Element, or Data as the source within that binding.
4. Set markup and reveal behavior through the binding's public surface.
5. Test attach, detach, panel recreation, data changes, and long text. For PanelRenderer, obtain the root through `UIToolkitPanelRootTracker` and compare host, root, and `RootVersion`; retained trees reconnect callbacks after re-enable without duplicating controls or bindings.

## Workflows

### Workflow: Animate a Label's own text

1. Create or locate the Label in UXML or C#.
2. Attach TextStudioBinding to the text property using Label Text source mode.
3. Set or show markup through the binding.
4. Verify fallback rendering on supported older Unity versions and native per-glyph rendering where available.

### Workflow: Bind external data

1. Choose Data source mode within the same TextStudioBinding.
2. Keep data conversion outside the renderer when possible.
3. Use SetTextDeferred when the source update must wait for a safe panel boundary.
4. Test source replacement, null or empty data, detach, and reattach.

## Verification

- Call `binding.CollectCapabilities(report)` to inspect the actual strategy and sequential overlap fallback. An unattached binding reports that its output is not ready.
- Formatted counters can select `TextStudioTextChangeScope.NumericValue` with a matching `NumericDecimalSeparator`. Keep number formatting with the data owner and use idempotent text updates.
- Reactive UI Toolkit text can compute `TextStudioSignal.Normalize`, `AudioRms`, `BeatPhase`, or `Smooth`, then submit through `binding.Effects.ApplyExternalEffect` from its update owner. Do not add a GameObject Signal Driver to a Label.

- The target remains a standard Label.
- Exactly one TextStudioBinding owns animation for the text slot.
- Label Text, Element, and Data are treated as modes, not separate custom controls.
- Unity 2023.2 through 6000.1 uses the supported Basic path; Unity 6000.2+ may use native per-glyph post-processing.
- Panel detach and reattach do not leak scheduled work.
- On Unity 6000.2+, `PreferAdvanced` preserves the requested preference while falling back to Standard when the attached panel disables Advanced Text. First attachment and movement between panels re-evaluate that panel's availability. Removing a binding restores only generator styles it still owns.

## API quick reference

- TextStudioBinding is the UI Toolkit CustomBinding integration.
- TextStudioBinding.SetText and SetTextDeferred update markup.
- ShowText, ShowTextInstant, StopReveal, PauseReveal, and ResumeReveal control lifecycle.
- SetTextVisibility, RestartEffects, and RefreshEffectBindings mirror focused runtime controls.
- TextStudioUITK and TextStudioUITKCapabilities expose supported UI Toolkit capability routing.

## Common issues

- **Nothing renders after attach:** Verify PanelSettings, target Label, binding slot, and source mode.
- **Two bindings fight over text:** Keep one TextStudioBinding on the Label.text slot and consolidate source behavior.
- **Per-glyph features differ by Unity version:** Respect the Basic versus native capability boundary instead of assuming identical internals.

## Boundaries

- UI Toolkit is separate from Canvas UI. A UnityEngine.UI.Text component uses TextStudioUnityUITextBackend on a GameObject, while a UI Toolkit Label uses TextStudioBinding and receives no Canvas components.
- Do not replace Label with an undocumented custom element.
- Do not add multiple TextStudioBinding instances to the same text slot.
- Do not assume Unity 6000.2 native capabilities on earlier versions.
- Do not retain panel or VisualElement references after their lifecycle owner has detached them.
- Reusable sample hosts use `Component UIPanel` and serialize `_uiPanel`, with same-GameObject discovery when unassigned. Assign only UIDocument or guarded Unity 6000.5+ PanelRenderer; never add a document automatically to a renderer host. Reassign old `_document` / `_uiDocument` references after updating, especially references to another GameObject. Old named host arguments in Renderer Lesson `Configure`, Whisperwood `ConfigureForScene`, and PrimeTween Notification Course `Configure` become `uiPanel`; rebuild compiled callers and update exact-signature reflection.
