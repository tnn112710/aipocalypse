---
name: tools-unity-broaudio
description: BroAudio in aipocalypse — every sound goes through AudioService, never the static BroAudio facade; SoundID fields, libraries in Data/Audio, one-shots vs kept players for loops, ambience and music, audio import presets, and editing clips with Audio Tools. Use for any sound, music, ambience, footstep, weapon or UI audio, adding a SoundID, trimming/normalizing/fading a clip, or touching AudioService, AmbienceService or audio libraries.
---

# BroAudio (aipocalypse)

Installed at `Assets/Plugins/BroAudio/` (vendor code, don't modify). Rules here come from PLUGINS.md (BroAudio row) and ARCHITECTURE.md (AudioSystem); if they disagree with this file, the docs win.

## BroAudio's own guide (vendor how-to)

BroAudio ships `Assets/Plugins/BroAudio/CLAUDE.md` (its `AGENTS.md` only imports it). Claude Code loads it only when files under that folder are read, so read it yourself when you need:

- **Data model:** AudioAsset libraries, AudioEntity rows, `SoundID`, `BroAudioType` flags.
- **The facade and the fluent `IAudioPlayer` chain:** overloads, fades, pitch, `OnEnd`, effects, scheduling. Here these are reached through `AudioService` (below), except the player chain on a kept `IAudioPlayer`.
- **Pitfalls:** recycled pooled players, invalid ids are silent, no `Play` from `OnDestroy`/`OnApplicationQuit`, no raw `AudioSource` bypass.
- **Boundaries:** never hand-edit `AudioAsset` `.asset` files; adding or renaming entities is Library Manager authoring (ask first).

Full docs: https://man572142s-organization.gitbook.io/broaudio/

## Project rules

- **Game code plays sound only through `AudioService`** (`_Core/AudioSystem/AudioService.cs`, root scope, injected). Never call `BroAudio.Play` / `BroAudio.Stop` / `id.Play()` from game code; extend `AudioService` if a feature is missing.
- **API:** `Play(id, volume)` (2D one-shot: the listener's own sounds), `PlayAt(id, position, volume)` (3D one-shot), `Start(id, volume, fadeIn)` returns an `IAudioPlayer` the caller keeps (loops, beds, music), then `SetVolume(player, v, fade)`, `Pause`, `UnPause`, `Stop(player, fadeOut)`. All are null-safe and skip unset ids.
- **Kept players, examples to copy:** `PlayerBreathSound` (breath loop faded by fatigue), `AmbienceService` (time-of-day beds), `PoseSequencePlayer` (the death sequence's music). Loops are set on the entity (Loop), not in code. Re-check `IsActive` before reusing a kept player (pooled players are recycled).
- **Data:** sounds are `[SerializeField] SoundID` fields, mostly on settings / tuning ScriptableObjects (`GunSettings`, `MeleeSettings`, `PlayerSoundTuning`, `TimeOfDayAmbience`…), sometimes on a view MonoBehaviour (`HitFeedbackHud` hit / kill sounds). Libraries live in `Assets/_Game/Data/Audio/` (`Guns`, `Melee`, `RepairFeedback`, `Player`, `Combat`, `Ambience`, `Music`, `UI` (menu sounds on `MenuStyle`), `Forecast` (the forecast board: `SoundID`s on `ForecastBoard` and `ForecastRope`)), one folder of entities each. Clips, volume and random clip / pitch live on the entity.
- **Other plugins don't play audio:** no Feel sound feedbacks (`MMF_Sound`, `MMF_MMSoundManager*`, `MMF_AudioSource*`…), no MMSoundManager, no BD audio tasks, no MicroVerse Ambiance. A Feel player that needs sound uses the planned `MMF_BroSound` bridge (not built yet; `tools-unity-feel` §5), which plays through `AudioService` via `FeedbackContext`.
- **No-code components** (`SoundSource`, `SoundVolume`, `SpectrumAnalyzer`) are not used; they bypass `AudioService`. Ask before adding one (e.g. `SoundVolume` for an options menu).
- **Pause:** there is no global audio pause. Each kept player is paused by its owner while `TimeService.IsPaused`: `Player` calls `_breathSound.SetPaused(...)` and `_sequence.SetPaused(...)`, which call `AudioService.Pause` / `UnPause` on the breath loop and the sequence music. `AmbienceService` stops its one-shot calls while paused, but its beds keep playing. Follow that pattern for a new kept player (aipocalypse-architecture §4); don't add a global pause rule without asking.
- **Clip editing** before a clip goes into an entity: Audio Tools ("Clip editing" below).
- **Import settings:** every clip gets one of three Unity Presets in `Assets/_Game/Settings/Audio/`, picked by length (thresholds in `AudioImport.json` there). The tool is `AgentScripts/Audio/AudioImportKit.cs`, a copy of the user-scope skill `unity-audio-import-optimization`; read that skill for entries, rules and tuning, and keep the two copies identical. Pass the project's preset folder as `presetFolder`.
  - **After adding or replacing sounds:** `Apply` with `["used:Assets/_Game/Data/Audio", false, "Assets/_Game/Settings/Audio"]`. `true` instead of `false` is a dry run, and a re-run must report `would change 0`.
  - **After importing a pack:** `Apply` with `["<pack folder>", false, "Assets/_Game/Settings/Audio"]`. The packs in `Assets/_StoreAssets/Audio/` already have the presets on every clip.

  | Preset | Clips | Settings |
  | --- | --- | --- |
  | `AudioImport_Sfx` | ≤ 2 s: guns, footsteps, hits, UI, short calls | Decompress On Load, ADPCM, Force To Mono, Optimize Sample Rate, Preload |
  | `AudioImport_Medium` | 2–60 s: stingers, longer calls, voice | Compressed In Memory, Vorbis 0.7, Preload |
  | `AudioImport_Stream` | > 60 s: music, ambience beds | Streaming, Vorbis 0.6, no Preload |

  - **Preload is required:** a clip with Preload Audio Data off loads on its first play, an ~11 ms hitch.
  - **Starting a stream costs 1–3 ms of main thread**, and BroAudio pays it again on each loop, because it hands the loop to a fresh source. Load In Background is off until it has been A/B'd: the handover is scheduled only a short warm-up ahead, so a late background open could leave a gap.
  - **To change a setting**, tune the preset in the Inspector and run `Apply` again. A hand edit on one clip is undone by the next `Apply`.
- Adding a sound: add the entity in the Library Manager (`Tools > BroAudio > Library Manager`) in the right library, add a `SoundID` field on the data asset, play it through `AudioService`. Update CODE_STRUCTURE.md / PLUGINS.md if a new library or service method appears.

## Clip editing (Audio Tools)

Audio Tools 1.2.0 is installed at `Assets/Plugins/AudioTools/` (vendor code, don't modify). It's **editor only** (`AudioTool.Editor` asmdef): nothing ships and game code never references it. Its own skills live in `Assets/Plugins/AudioTools/AI/Skills/` (no agent loads them by itself); read the one that matches the task:

| Vendor skill | Read when |
| --- | --- |
| `audio-tools-setup-and-overview/` | Opening Audio Tools, what it can do, picking a workflow |
| `audio-tools-preview-and-select-audio/` | Previewing, looping part of the waveform, zoom / pan, selecting an exact range |
| `audio-tools-trim-and-process-audio/` | Trimming, removing silence, normalizing, volume, fades |
| `audio-tools-export-processed-audio/` | Writing the result as a WAV, output naming, verifying the export |

- **Never overwrite a vendor clip** (`Assets/_StoreAssets/`, `Assets/Synty/`, `Assets/Plugins/`). Export the processed WAV as a new file under `Assets/_Game/`, next to the content that uses it; ask the user for the folder if it isn't obvious.
- **Keep the source** referenced until the new clip is checked.
- **Into the game:** swap the clip in the BroAudio entity in the Library Manager; never add `AudioClip` fields to game code.
- **Import settings:** run `Apply` on the exported file ("Import settings" above), never on the vendor original.
