---
name: "text-studio-setup-and-overview"
description: "Use this skill whenever the user asks how to start with Text Studio, choose among TextMeshPro, existing Unity UI Text, optional UniText, and UI Toolkit workflows, open galleries or examples, or verify package and project readiness."
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

# Set Up and Navigate Text Studio

Route the request through the supported text host, authoring surface, and ownership model before adding effects.

## When to use this skill

- First-time setup or package verification.
- Choosing World Space TMP, Canvas TMP, existing Unity UI Text, optional UniText, UI Toolkit binding, or temporary text.
- Finding galleries, recipes, examples, migration, health checks, or integrations.

## Prerequisites

- Unity 2023.2 or newer is open with Text Studio and TextMeshPro available.
- The target renderer, lifecycle, and text ownership are known.

## Quick start

1. Open Tools/Text Studio/Welcome, then Tools/Text Studio/Text Gallery for working patterns.
2. Create GameObject/Text Studio/Animated Text/World Space or UI Canvas for a new TextMeshPro host, or select an existing Unity UI Text component and choose Add Text Studio in its native inspector.
3. Use a TextStudioBinding for UI Toolkit Label text instead of adding GameObject components.
4. Use the optional UniText integration when complex shaping or color glyphs must remain UniText-owned.
5. Enter Play Mode and verify reveal, active effects, hide behavior, and cleanup.

## Workflows

### Workflow: Choose a text host

1. Use World Space for scene TextMeshPro text.
2. Use UI Canvas for TextMeshProUGUI.
3. Preserve an existing UnityEngine.UI.Text through its Add Text Studio inspector action when replacing the component or prefab references is undesirable.
4. Use the optional UniText backend for its shaping, bidirectional text, or color-glyph ownership.
5. Use TextStudioBinding on a normal UI Toolkit Label.text binding slot.
6. Use GameObject/Text Studio/Temporary Text/Emitter for pooled transient messages.

Canvas is a container and render pipeline, not a text backend. TextMeshProUGUI and Unity UI Text can both live below it. UI Toolkit is a separate retained-mode system and never uses the legacy Canvas backend.

### Workflow: Verify the installation

1. Open Text Gallery and inspect representative effects, reveals, and placements.
2. Create one animated text object through the GameObject menu.
3. Run Tools/Text Studio/Setup/Health Check when the project state is uncertain.
4. Test one reveal and one text replacement in Play Mode.

## Verification

- The selected host matches the target UI stack.
- Text Studio, TextMeshPro, and any chosen integration compile without errors.
- Reveal and active effects render in the intended pipeline.
- No optional add-on is assumed unless its package is imported.
- Text ownership and cleanup are explicit.

## API quick reference

- Tools/Text Studio/Text Gallery opens the main catalog.
- Tools/Text Studio/Recipes and Tools/Text Studio/Examples provide guided starting points.
- GameObject/Text Studio/Animated Text/World Space and UI Canvas create TextMeshPro hosts.
- TextStudioText is the core animated-text hub.
- TextStudioUnityUITextBackend preserves an existing UnityEngine.UI.Text and supports shared vertex effects except UV Offset. TMP material effects and Text Studio 2D Appearance remain TMP-only.
- TextStudioBinding is the UI Toolkit CustomBinding integration.

## Common issues

- **No animation is visible:** Verify TextStudioText is attached to a supported text source and the markup or default effect resolves.
- **UI Toolkit text was replaced by a custom control:** Use a normal Label with one TextStudioBinding on its text slot.
- **Unity UI Text shows no setup action:** Confirm the normal Text inspector is active and the Text is not the editable output owned by a legacy InputField.
- **Health Check reports an integration issue:** Open Tools/Text Studio/Integrations and follow the installed package's documented setup.

## Boundaries

- Do not assume a fixed package folder.
- Do not add optional vendor references to the base assembly.
- Do not create multiple competing owners for one text element.
- Do not describe Canvas, Unity UI Text, and UI Toolkit as interchangeable layers. Canvas hosts GameObject UI, Unity UI Text is one Canvas renderer, and UI Toolkit uses TextStudioBinding.
- Use a narrower Text Studio skill after routing.
