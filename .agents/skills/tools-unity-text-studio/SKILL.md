---
name: tools-unity-text-studio
description: Text Studio (animated TextMeshPro text, typewriter reveals, effects, reactions) in aipocalypse — its status (installed, Undecided, ask before using it in game code), the project constraints if adopted (uGUI + TMP only, no UI Toolkit, zero-allocation HUD text, DOTween for code tweens, TimeService pause, asmdef references, PLUGINS.md update), and where Text Studio's own skills answer how-to questions. Use for any animated or typewriter text, text effects, text reveal / hide, dialogue or HUD text animation work.
---

# Text Studio (aipocalypse)

Installed at `Assets/Plugins/TextStudio/` (vendor code, don't modify). Version **1.3.1** (its `package.json`; PLUGINS.md agrees).

**Status: Undecided** (PLUGINS.md). No game code uses it. Before building anything with it, ask the user whether to adopt it for this feature; otherwise use plain TMP (`TMP_Text`, `SetText`, `maxVisibleCharacters` for a typewriter) and DOTween. Exception: an `MMF_TMPTextReveal` inside an authored Feel MMF_Player is fine for a feedback-style reveal (`tools-unity-feel`); code-driven typewriters stay on TMP + DOTween.

## Text Studio's own skills (vendor how-to)

Text Studio ships agent skills in `Assets/Plugins/TextStudio/AI/Skills/`. No agent loads them on its own; read the one that matches the task:

| Vendor skill | Read when | Here |
| --- | --- | --- |
| `text-studio-setup-and-overview/` | First setup, choosing the text host, galleries, health checks | Start here; host is Canvas TMP |
| `text-studio-create-animated-text/` | Making a TMP text animated, markup, the reveal-to-hide lifecycle | Use |
| `text-studio-build-effects-and-typewriter-reveals/` | Effect markup, reveal / active / hide effects, typewriter timing | Use |
| `text-studio-control-text-at-runtime/` | `TextStudioApi` / `TextStudioText` from C#: set, reveal, hide, pause, resume | Use (from views only) |
| `text-studio-reuse-animation-profiles/` | Shared defaults with `TextStudioBehaviorProfile` | Use; profiles go in `Assets/_Game/Data/UI/` |
| `text-studio-author-reactions/` | Particles, audio cues or gameplay signals on reveal / marker | Use, but audio goes through `AudioService` (`tools-unity-broaudio`) and gameplay through MessagePipe, not Text Studio signals into services |
| `text-studio-connect-supported-integrations/` | Dialogue, localization, tweening, Timeline, splines | DOTween (used); Timeline (installed, unused). Ask before Timeline or any other |
| `text-studio-preview-and-export-animations/` | Previewing effects, exporting MP4 / GIF | Editor use only |
| `text-studio-bind-ui-toolkit-text/` | — | **Not applicable:** the game UI is uGUI (UI.md) |

## Constraints if adopted

- **uGUI + TextMeshPro only** (UI.md). No UI Toolkit, no UniText.
- **HUD rules still apply** (UI.md): widgets skip unchanged values and don't allocate per frame; don't put a reveal animation on a per-frame number (ammo, timers).
- **Views own it:** services never reference Text Studio; a view reacts to a MessagePipe event and drives the text (same rule as Feel).
- **Pause:** gameplay text animates on scaled time; menus and the death screen on unscaled time. `TimeService` owns `Time.timeScale`.
- **Assemblies:** add the `TextStudio` asmdef reference to the game assembly that uses it, and update CODE_STRUCTURE.md.
- **Docs:** change its PLUGINS.md row from Undecided to Should (or Must) with the usage rules in the same change.
