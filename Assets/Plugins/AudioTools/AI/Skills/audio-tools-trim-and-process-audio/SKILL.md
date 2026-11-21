---
name: "audio-tools-trim-and-process-audio"
description: "Use this skill whenever the user asks to 'trim this clip', remove silence, shorten pauses, split a take into clips, normalize peak or loudness (LUFS) levels, adjust volume, add fades, reverse audio, make a seamless loop, remove DC offset or rumble, or apply the same edits to many clips with Audio Tools. Use audio-tools-export-processed-audio when the user mainly needs WAV files written to disk."
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

# Trim and Process Audio

Apply Audio Tools processing to an explicit selection, trimmed audio or the full source while preserving the original asset until export.

## When to use this skill

- Trimming or cropping to a selected range, or trimming leading and trailing silence automatically.
- Shortening long pauses or splitting one recording into separate clips.
- Normalizing to a peak or loudness target, adjusting volume, applying fades, reversing, or making a seamless loop.
- Removing DC offset or low rumble, mixing to mono or converting the sample rate.

## Prerequisites

- The intended source and selection have been verified.
- Processing settings, channels, and sample frequency are known.

## Quick start

1. Create or confirm the intended waveform selection, or enable Trim Silence to find the audible part.
2. Enable only the requested cards under `Enhance`. The Audio Editor runs Clean Up on the whole clip, resolves the saved region (selection, then Trim Silence), then per clip: channel mix, Reverse, Normalize, Fade In, Fade Out, Volume and Seamless Loop. Pause shortening, mono output and sample-rate conversion happen when saving, and the preview already sounds like the result.
3. Preview with Selection only, which plays exactly what saving writes.
4. Retain the original source and pass the processed result to the export workflow.

## Workflows

### Workflow: Trim silence and normalize

1. Enable Trim Silence with Mode `Trim start and end`; set the Silence threshold and Keep before or Keep after (milliseconds) so attacks and decays survive.
2. Enable Normalize. Mode `Peak` scales to Target peak (default -0.4 dB); `Loudness (LUFS)` scales to Target loudness (default -23 LUFS, ITU-R BS.1770 integrated loudness) and never raises a peak above Peak ceiling (default -1 dB).
3. Read the LOUDNESS and OUTPUT PEAK readouts and the status line; it reports when the ceiling held a clip below its loudness target.

### Workflow: Split a take or shorten pauses

1. Choose Trim Silence Mode `Split into clips` to save each sound separated by at least Minimum pause as its own numbered file; the waveform marks each clip.
2. Choose `Shorten pauses` to keep one clip but reduce every pause longer than Minimum pause to `Shorten pauses to`; the waveform shades the removed parts.

### Workflow: Make a seamless loop

1. Select the region to loop.
2. Enable Seamless Loop and set Crossfade (seconds) and Shape (Equal power for ambience, Linear for tonal material). Fades stay off while the loop is on.
3. Preview with Loop and Selection only enabled; playback repeats the loop exactly. The saved loop is shorter than the selection by the crossfade.

### Workflow: Process many clips

1. Open several clips or folders with `Assets > Edit Audio`, drag them onto the window, or press Add Clips to apply the current settings to the clips selected in the Project window.
2. Settings apply to every open file; each file keeps its own selection. Step through files with `‹` and `›` or Page Up and Page Down, and press Analyze to measure every file without writing.

### Workflow: Clean up and convert

1. Enable Clean Up for Remove DC offset and a High-pass filter (Cutoff, default 80 Hz, 24 dB per octave).
2. Under `Output`, set Channels (`Mono (mix)`, `Left channel`, `Right channel`) or Sample rate. A status line warns when a mono mix cancels opposite-phase channels.

## Verification

- The saved region, clip count or loop length matches the waveform overlay and the footer summary.
- Peak or loudness is within the requested target, and the clipping warning is clear.
- Fade endpoints and loop seams are smooth and no channel is offset.
- The original project asset has not been overwritten.

## API quick reference

- `AudioProcessor.DetectSilence(samples, channels, threshold)` returns per-channel sample-frame boundaries, with an exclusive end.
- `AudioProcessor.TrimAudio(samples, channels, startSample, endSample)` returns trimmed interleaved data. Pass frame indices, not indices into the flat interleaved array; output length is `(endSample - startSample) * channels`.
- `AudioProcessor.Normalize`, `AdjustVolume`, and `ApplyFade` modify the working samples.
- Await `AudioProcessor.CreateClipFromSamples` to create a preview clip from processed data.
- Loudness normalization, pause shortening, clip splitting, seamless loops, Clean Up, channel and sample-rate output and multi-file sessions are Audio Editor features without a public scripting API.

## Common issues

- **Stereo output sounds shifted:** Pass frame-aligned sample indices and the correct channel count.
- **Volume adjustment clips the normalized result:** Lower Volume or the normalization target and recheck the preview.
- **Too much quiet content was removed:** Lower the silence threshold or raise Keep before and Keep after.
- **Nothing is trimmed:** The status line reports no audible content at this threshold, or a DC offset keeps the signal above it; lower the threshold or enable Remove DC offset.

## Boundaries

- Never overwrite the source implicitly.
- Do not process compressed file bytes directly; work with decoded sample data.
- Keep channel interleaving intact.
- Audio Tools has no equalizer, noise reduction or time-stretching.
- Writing the result belongs to audio-tools-export-processed-audio.
