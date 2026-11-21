---
name: "audio-tools-export-processed-audio"
description: "Use this skill whenever the user asks to 'save the edited audio', export a processed clip as WAV, save many clips at once, choose 16-bit, 24-bit or 32-bit float output, replace original WAVs safely, or verify an Audio Tools export. Use audio-tools-trim-and-process-audio when processing has not yet been completed."
metadata:
  asset: "Audio Tools"
  publisher: "Impossible Robert"
  asset-version: "1.3.0"
  skill-version: "1.0.0"
  unity: "2022.3"
  render-pipelines: "Not applicable"
  category: "Tools/Audio"
  asset-store-url: "https://assetstore.unity.com/packages/tools/audio/audio-tools-356424"
  documentation-url: "https://www.wetzold.com/tools/audiotools/docs/"
  support-url: "https://discord.com/invite/uzeHzEMM4B"
  last-verified: "2026-11-11"
---

# Export Processed Audio

Write verified processed samples to new WAV files with the correct channels, sample rate, bit depth, and project import behavior.

## When to use this skill

- Exporting Audio Tools output as WAV, for one clip or many.
- Avoiding accidental filename collisions.
- Choosing 16-bit PCM, 24-bit PCM or 32-bit float output and confirming Unity imports the file.
- Replacing original WAV assets with a backup to restore from.

## Prerequisites

- Processed samples have been previewed and approved.
- The destination folder and overwrite behavior are explicit.

## Quick start

1. Choose a project-relative or explicit filesystem destination.
2. Generate a unique name unless the user explicitly confirms replacement.
3. In the Audio Editor, set the `Output` section: Format (`Match source`, `16-bit PCM`, `24-bit PCM`, `32-bit float`; Match source keeps 24-bit and float sources at their depth), Channels and Sample rate. Save Copy (a project asset), Import WAV (a destination inside `Assets`) or Save As WAV... (elsewhere) writes that format.
4. Inspect the resulting asset and compare its duration and playback.

## Workflows

### Workflow: Export a project WAV

1. Choose a writable folder under the project's `Assets` directory.
2. Use Save Copy for a project asset or Import WAV for a clip from outside the project. Copies get numbered names (`<name>_01.wav`, `_02`, ...) next to the source and keep the source's import settings.
3. With Trim Silence in `Split into clips` mode the same action saves one numbered file per sound and reads `Save N Clips`.
4. From editor code, call `AudioProcessor.ExportToWav(outputPath, samples, channels, frequency, WavSampleFormat.Pcm24)` (or `Pcm16`, `Float32`) with the approved processed samples.
5. Let Unity import the new file and verify its AudioClip metadata.

### Workflow: Save many files with the same settings

1. Open several files: select clips or folders and use `Assets > Edit Audio`, drag them onto the Audio Editor, or use Add Clips in an open editor.
2. Tune the settings while stepping through files with the `‹` and `›` buttons or Page Up and Page Down; each file keeps its own selection.
3. Optionally press Analyze in the Files list to see removed silence, gain, loudness and clipping for every file without writing anything.
4. Choose `Output > Save copies to` (next to each original, or a folder) and press `Save N Copies`. Only files not yet saved with the current settings are processed; Cancel stops after the current file.

### Workflow: Replace originals safely

1. Replace Original WAV (one file) or Replace N WAVs (all open files) only exists for WAV assets in the project and writes after a confirmation.
2. Audio Tools first copies each original to `Library/AudioTools/Backups`; `Advanced > Restore Replaced Files` puts the last replaced files back.
3. MP3, OGG, AIFF and FLAC sources are never replaced; save copies instead.

### Workflow: Verify exported output

1. Reload or preview the written WAV.
2. Compare duration, channels, frequency, bit depth and audible boundaries with the processed preview.
3. Report the final paths and whether the originals remained untouched.

## Verification

- Every destination file exists and has a valid WAV header with the chosen bit depth.
- Unity imports files under `Assets`, and copies keep the source's import settings.
- Duration, channels, frequency, and audible content match the processed preview.
- No existing file was replaced without explicit approval.

## API quick reference

- `AudioProcessor.GenerateUniqueFilename` avoids destination collisions.
- `AudioProcessor.ExportToWav(outputPath, samples, channels, frequency, WavSampleFormat format)` writes 16-bit or 24-bit PCM (rounded) or 32-bit float; the `bool use32BitFloat` overload remains for 16-bit or float output.
- `AudioHeaderReader.ReadHeader` can verify file metadata, including `BitsPerSample`, quickly.
- Await `AudioManager.LoadAudioFromFile` to reload exported output for preview. Set `allowStreaming: false` when verification needs decoded samples through `AudioClip.GetData`.
- Multi-file saves, Analyze, backups and restoring are Audio Editor actions without a public scripting API.

## Common issues

- **The file is not visible in Unity:** Export under `Assets`; Audio Tools imports the files it writes there.
- **The output plays at the wrong speed:** Use the original or explicitly requested sample frequency.
- **The export replaced an older file:** Use `GenerateUniqueFilename` unless replacement was explicitly confirmed, and restore with `Advanced > Restore Replaced Files`.
- **A multi-file save asks for a folder:** Some files are outside the project, so the copies need a destination folder.

## Boundaries

- Audio Tools exports WAV (16-bit PCM, 24-bit PCM or 32-bit float), not compressed formats such as OGG or MP3; Unity compresses audio on import.
- Do not claim mastering or platform loudness compliance; Audio Tools measures ITU-R BS.1770 integrated loudness but does not certify it.
- Do not overwrite a source or destination without explicit confirmation.
- Do not leave temporary preview files behind.
