# Text Studio AI Skills

These skills teach a coding assistant to create, animate, reuse, control, bind, integrate, preview, and export Text Studio text through supported editor and runtime workflows.

## Use the skills

1. Point a compatible coding assistant at this package's `AI/Skills` folder.
2. Ask it to load `text-studio-setup-and-overview` first when routing or setup is unclear.
3. Load only the narrowest skill that matches the requested text task.
4. Review scene changes, assets, generated code, integrations, and exports before applying them.

These Markdown files are documentation only. They do not add an AI service, subscription, runtime dependency, or generated animation to Text Studio.

## Included skills

- `text-studio-setup-and-overview`: Use this skill whenever the user asks how to start with Text Studio, choose among TextMeshPro, existing Unity UI Text, optional UniText, and UI Toolkit workflows, open galleries or examples, or verify package and project readiness.
- `text-studio-create-animated-text`: Use for creating new animated TextMeshPro text or preserving an existing Unity UI Text component, then applying markup, placement, and a complete reveal-to-hide lifecycle.
- `text-studio-build-effects-and-typewriter-reveals`: Use for add Text Studio effect markup, combine reveal, active, and hide effects, configure typewriter timing, or create reusable effect and reveal behavior.
- `text-studio-author-reactions`: Use for finite responses to reveal, hide, text change, markers, or named signals, including particles, audio cues, gameplay feedback, and outgoing glyph motion.
- `text-studio-reuse-animation-profiles`: Use for sharing Text Studio default effects, typewriter behavior, or finite reactions across text objects with TextStudioBehaviorProfile.
- `text-studio-control-text-at-runtime`: Use for set, reveal, hide, pause, resume, restart, format, or inspect Text Studio text from C# with TextStudioApi or TextStudioText.
- `text-studio-bind-ui-toolkit-text`: Use for animate a UI Toolkit Label, configure TextStudioBinding, bind label text or data, or handle Unity-version-specific UI Toolkit text capabilities.
- `text-studio-connect-supported-integrations`: Use for connect Text Studio to a supported dialogue, localization, visual scripting, tweening, timeline, UI effect, spline, LED, or other optional integration.
- `text-studio-preview-and-export-animations`: Use for preview a Text Studio effect, inspect lifecycle phases, export a supported animation to MP4 or GIF, or understand platform and transparency limits.

Documentation: https://www.wetzold.com/tools/text-studio/docs/

Support: https://discord.com/invite/uzeHzEMM4B
