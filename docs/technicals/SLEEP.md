# Sleep

Sleeping in a bed to skip time. Code in `_Core/SleepSystem/` (namespace `MB13.Aipocalypse.Core.SleepSystem`), the screen in `_Core/UI/Hud/SleepScreen.cs`, data in `Data/Sleep/`, script map in [CODE_STRUCTURE.md](CODE_STRUCTURE.md).

## How it plays

1. Look at a bed: **[E] Sleep**. The bed in WorldMap stands in the house, against the west wall (the door stands open).
2. The wake-up clock opens (the game keeps running): **-1 H / +1 H** pick a whole clock hour, **Sleep** confirms, **Cancel** or Esc closes it. It offers the next `DefaultWakeHour` (07:00) when that's in reach, else the latest hour.
3. The view gets into the bed and lies down, the eyes close, the hours run past on a black screen with the clock, the eyes open, the view sits up and stands beside the bed.

Nothing else happens yet: no healing, no sanity, no morning report.

## Rules

| Rule | Value | Where |
| --- | --- | --- |
| Shortest / longest sleep | 1 h / 12 h | `SleepSettings.MinHours`, `MaxHours` |
| Never skips a raid | The bed refuses while a raid runs, and inside a night whose raid hasn't run. The latest wake-up hour is the last whole hour at least `WakeBeforeRaid` (0.5 h) before the next raid that hasn't run starts: by day at 20:00 raids, 19:00 | `SleepRules.Window` |
| Too close | Under `MinHours` before that limit: "Too close to tonight's raid to sleep" | `SleepRules` |
| Time passes for real | `WorldClock.SkipTo` over `SkipSeconds` (4 s): every hour and phase event fires, so the morning autosave, the weather and ambience follow | `SleepService` |
| Interrupted | The player dying ends it; a scene unload releases everything | `SleepService.End` |

A refusal shows as a line on screen for 2.5 s. A scene without raids (no `RaidInstaller`) has no limit but the longest sleep.

## Pieces

```text
Bed (IInteractable, scene object)        "[E] Sleep" → SleepService.Begin; its Anchor places the sequences
SleepService (scene entry point)         stages: Choosing → LyingDown → FallingAsleep → Asleep → Waking; Shift / Confirm / Cancel; Darkness 0..1
SleepRules (pure, SleepTests)            Window(now, min, max, wakeBeforeRaid, raids) → earliest / latest whole hours or a reason; DefaultWake
SleepSettings (SO, Data/Sleep)           Enabled, rules, the two PoseSequences, fade / skip seconds, optional sounds
SleepScreen (HUD.prefab, own canvas 150) the wake-up clock, the darkness, the clock while asleep, the refusal line; reads SleepService each frame
SleepInstaller (SceneInstaller)          registers the above, injects the scene's beds (collected on save / Play); needs WorldTimeInstaller
```

**The moment** is two `PoseSequence` assets (`Data/Player/Sequences/SleepLieDown`, `SleepGetUp`) played with the bed's `Anchor` (`IPlayer.PlaySequence(sequence, anchor)`): with an anchor a sequence's offsets, heights and yaws are in the anchor's space and nothing is ground-probed. The anchor sits on the floor under the bed, **x (red) to the side you get in from, z (blue) towards the pillow**. The get-up's last beat is where the player ends: `SleepService` teleports the body there in the same frame it gives the camera back, so nothing pops. The body itself stays beside the bed while asleep.

## Tuning and adding a bed

| To change | Edit |
| --- | --- |
| Sleep lengths, default hour, raid margin, fade and skip seconds | `Data/Sleep/SleepSettings.asset` |
| How getting in and out looks | the two sequences (beats: offset, height above the floor, pitch, yaw) or `AgentScripts/Sleep/SleepSetup.cs` (`LyingOffset`, `SittingOffset`, …) and re-run `Assets` |
| Turn sleeping off | `SleepSettings.Enabled`, or remove `SleepInstaller` from the scene's scope |

**Another bed:** a GameObject with `Bed`, a model with a collider under it, and an `Anchor` child on the floor (axes as above); the scene needs a `SleepInstaller`. A bed of another size needs its own sequences only if the lying height or the side differ much.

## Setup and checks

`AgentScripts/Sleep/SleepSetup.cs`: `Assets` (settings + sequences), `Hud` (the SleepScreen node), `World` (WorldMap: installer, the `Home Interior/Bed` scene root, the house door open with its collider off). `Home Interior` is its own scene root so a map rebuild of `Landmarks` doesn't delete it; the open door is on the house and is lost by that rebuild: re-run `SleepSetup.World`.

`SleepTests` cover the window rules. Play check 2026-11-11 (editor, scripted): prompt, clock, lie down, skip 07:47 → 15:00, get up beside the bed, no errors. Not checked by hand: mouse and gamepad on the clock's buttons.

## Known gaps

- No sound yet (`LieDownSound`, `WakeSound` unset), no reward for sleeping, no morning report.
- The HUD stays visible while getting in and out.
- The house has no furniture besides the bed; the bed is Lakefell `Bed_2` ([ASSETS.md](ASSETS.md)).
