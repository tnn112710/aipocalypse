---
name: "text-studio-reuse-animation-profiles"
description: "Use this skill whenever the user asks to share Text Studio default effects, typewriter behavior, or reactions across many text objects with TextStudioBehaviorProfile."
metadata:
  asset: "Text Studio - Text Animation for Unity | TextMeshPro & UI Toolkit"
  publisher: "Impossible Robert"
  asset-version: "1.2.0"
  skill-version: "1.0.0"
  unity: "2023.2"
  render-pipelines: "Built-in, URP, HDRP, custom SRP"
  category: "Tools/GUI"
  asset-store-url: "https://assetstore.unity.com/packages/tools/gui/text-studio-text-animation-for-unity-textmeshpro-ui-toolkit-394000"
  documentation-url: "https://www.wetzold.com/tools/text-studio/docs/"
  support-url: "https://discord.com/invite/uzeHzEMM4B"
  last-verified: "2026-08-29"
---

# Reuse Animation Profiles

Centralize only renderer-neutral behavior domains while leaving host, renderer, content, and layout settings local.

## When to use this skill

- Reusing default effects across text objects.
- Sharing typewriter behavior or presets.
- Reusing finite event reactions across text objects.
- Creating consistent animation behavior without duplicating component settings.

## Prerequisites

- At least one text object has verified behavior worth reusing.
- The profile domains to centralize are explicitly chosen.

## Quick start

1. Create Assets/Create/Text Studio/Setup/Behavior Profile.
2. Enable only the Default Effects, Typewriter, and Reactions domains the asset should own.
3. Copy or configure the verified behavior into TextStudioBehaviorProfile.
4. Assign the profile to representative TextStudioText hosts or TextStudioBinding elements.
5. Change the profile once and verify all intended consumers without affecting local renderer settings.

## Workflows

### Workflow: Create a behavior profile

1. Start from a verified local configuration.
2. Create a TextStudioBehaviorProfile and name it by semantic role.
3. Enable only the behavior domains that should replace local fallback.
4. Assign it to two different text hosts and compare lifecycle behavior. If the profile owns Reactions, test a real trigger in Play Mode on each supported renderer.

### Workflow: Evolve a shared profile safely

1. Find all intended consumers before editing the asset.
2. Preview the change on short, long, and localized text.
3. Confirm local placement, font, material, and renderer settings remain untouched.
4. Commit the profile and consumer changes together when they form one design contract.

### Workflow: Share reactions without duplicating effects

1. Keep always-on and phase-specific tag strings in Default Effects, and keep reveal timing and reveal/hide effect references in Typewriter.
2. Enable Reactions only when the profile should replace each consumer's local finite event responses. Leave that domain disabled when a host needs its own reaction recipe.
3. Use a shared Reaction asset for reusable action layers, then assign the Behavior Profile to the intended component or UI Toolkit binding. Bind scene or panel targets on each host rather than storing them in the profile.
4. Trigger a text change or named signal in Play Mode and verify the intended response, cancellation, and cleanup on each host. Edit Mode assignment must not spawn output or dirty the scene.

## Verification

- The profile supplies only enabled TextStudioBehaviorDomain values.
- Default effects, typewriter behavior, and enabled reactions match the verified source configuration.
- Consumers retain local content, renderer, placement, font, and material settings.
- Changing the profile updates only intended objects.
- Missing profiles fall back to valid local behavior.

## API quick reference

- TextStudioBehaviorProfile is the reusable renderer-neutral behavior asset.
- TextStudioBehaviorDomain.DefaultEffects, Typewriter, and Reactions select independent owned domains.
- TextStudioBehaviorProfile.Provides reports whether requested domains are supplied.
- TextStudioBehaviorProfile.Reactions exposes the shared reaction recipe; a disabled Reactions domain leaves each host's local recipe in control.
- GetTypewriterSettings resolves the current profile timing.
- ApplyBehaviorSettings copies supported behavior domains into the asset.

## Common issues

- **Assigning a profile changes too much:** Disable domains that should remain local and restore local fallback settings.
- **Some objects do not update:** Verify they reference the same profile and refresh supported bindings after runtime changes.
- **A local reaction no longer plays:** Check whether the profile owns Reactions; disable that domain for a host-local recipe or move the intended recipe into the profile.
- **A renderer setting was added to the profile:** Keep renderer-specific style, material, and layout on the host or renderer add-on.

## Boundaries

- Do not store text content, scene references, fonts, or renderer-specific settings in TextStudioBehaviorProfile.
- Do not enable domains the asset is not intended to own.
- Do not create a new profile for every single object.
- Do not mutate a shared profile at runtime unless the application deliberately owns that global change.
