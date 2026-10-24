---
name: tools-unity-audio-tools
description: Audio Tools 1.2.0 (editor-only audio clip editor) in aipocalypse — previewing, selecting, trimming, removing silence, normalizing, adjusting volume, fading and exporting processed WAVs, where processed clips go (never over vendor clips) and how they reach the game (BroAudio entities), plus where Audio Tools' own skills answer how-to questions. Use when asked to edit, trim, normalize, fade or export an audio clip in the editor.
---

# Audio Tools (aipocalypse)

Installed at `Assets/Plugins/AudioTools/` (vendor code, don't modify). **Editor only** (`AudioTool.Editor` asmdef): nothing ships in builds and game code never references it. Playback is BroAudio's job (`tools-unity-broaudio`).

## Audio Tools' own skills (vendor how-to)

Audio Tools ships agent skills in `Assets/Plugins/AudioTools/AI/Skills/` (written for 1.2.0). No agent loads them on its own; read the one that matches the task:

| Vendor skill | Read when |
| --- | --- |
| `audio-tools-setup-and-overview/` | Opening Audio Tools, what it can do, picking a workflow |
| `audio-tools-preview-and-select-audio/` | Previewing, looping part of the waveform, zoom / pan, selecting an exact range |
| `audio-tools-trim-and-process-audio/` | Trimming, removing silence, normalizing, volume, fades |
| `audio-tools-export-processed-audio/` | Writing the result as a WAV, output naming, verifying the export |

## Project rules

- **Never overwrite a vendor clip** (`Assets/_StoreAssets/`, `Assets/Synty/`, `Assets/Plugins/`). Export the processed WAV as a new file under `Assets/_Game/`, next to the content that uses it; ask the user for the folder if it isn't obvious.
- **Keep the source:** processing is destructive to the exported file only; keep the original clip referenced until the new one is checked.
- **Getting it into the game:** swap the clip in the BroAudio entity in the Library Manager (`tools-unity-broaudio`). Don't add `AudioClip` fields to game code.
- **Import settings** of new clips (load type, compression, mono) follow the audio import guidance of the user-scope `unity-optimization-pc` skill (`references/assets-memory.md`, audio import); set them on the exported file, not on the vendor original.
- PLUGINS.md lists Audio Tools without usage notes; add one there if the project starts relying on it.
