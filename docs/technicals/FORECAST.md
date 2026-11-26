# Forecast

The night forecast, the board in the yard that shows it, and how the player reveals it each day. Code in `_Core/ForecastSystem/` (namespace `MB13.Aipocalypse.Core.ForecastSystem`), data in `Data/Forecast/`, sounds in `Data/Audio/Forecast`, the board in `Content/Structures/ForecastBoard/`, script map in [CODE_STRUCTURE.md](CODE_STRUCTURE.md). Design and open questions: [docs/gdd/IDEAS.md](../gdd/IDEAS.md) → Night fight periods and sizes.

## Rules today

- A night has three **periods** (`NightPeriod`): Evening 20:00–22:00, Midnight 22:00–00:00, Late night 00:00–02:00. Each brings a **fight** (`FightSize`): none, small, medium or boss.
- **The same forecast every day**: small in all three periods (`ForecastSettings.Periods`). Rolling it per day and balancing come later.
- **Display only.** The forecast doesn't drive the raids yet: raids still follow [RAIDS.md](RAIDS.md) (`EveryNightSchedule`). The period hours are stored but nothing reads them yet.

## The reveal

- **Each morning** at `NewForecastHour` (06:00) the plaques hang as **'?'**. The day's forecast runs from then until the next 06:00, so it stays shown through the night.
- **The player pulls the rope** (`[E] Spin Forecast` on the handle by the right post): all three plaques spin like slot reels and land left to right; a boss always lands last.
- **Nobody pulled?** At `SelfRevealHour` (19:00) they spin by themselves, so the forecast is never hidden when the night starts.
- **Saved:** the day whose forecast was revealed (`ForecastSaveSection`). A load or a clock jump shows the state at once, without the spin.

**Changing the mechanic later:** the trigger is only `ForecastRope`. Anything else (a bell, a robot, the drone, shooting a target) calls `ForecastReveal.Reveal()`; remove or replace the rope and the board still spins. `ForecastSettings.HideUntilRevealed` off brings back the always-shown board (no '?', no rope prompt). Without a `WorldClock` in the scene the forecast always shows.

## The board

`ForecastBoard.prefab`: a frame of three HIGHLANDS logs (two posts 3.5 m apart, a crossbar), three Lakefell-wood plaques and a pull rope.

| Part | What it is |
| --- | --- |
| `Plaque_<Period>` | The hanger: two ropes from the crossbar hold the ends of an iron `Pin` through the plaque's middle |
| `Reel` | The plaque itself (two planks), turning on the pin; carries `Period` and `Mark` |
| `Period` (quad, top-left) | The period icon, fixed: a setting sun (Evening), a moon (Midnight), a star (Late night) |
| `Mark` (quad over the face) | '?' while hidden; then small crossed swords (Small), larger crossed swords (Medium), a horned skull (Boss), nothing for none |
| `PullRope` | `ForecastRope` + a trigger box to look at; a rope down to a wooden toggle `Handle` |

**The spin** (`ForecastBoard`, DOTween, tuning on the component):

1. The rope's handle is yanked down 0.22 m (lever sound) and springs back; the reveal fires as it bottoms out.
2. Every reel rocks back 18°, then spins forward and slows down (OutQuart): the first lands after 3 turns in 1.5 s, each next one turns once more and lands 0.5 s later. A boss spins 0.7 s longer and always lands last.
3. While a reel looks away during its last turn its face swaps from '?' to the result, so it lands showing it. Each edge-on pass ticks (one tick at most every 0.055 s for the whole board, so the ticking slows as the reels do).
4. Landing: a wooden clack, the plaque overshoots and wobbles (14°) and dips on its ropes. A boss: a deep thud, a bigger wobble (30°) and the whole board shakes.
5. A new morning turns the plaques back to '?' with one quick turn. A console override while shown spins to the new marks.

The paint is baked: `ForecastBoardSetup.Textures` composites the Layer Lab picto icons on the CPU, wears them with noise and writes one PNG and one alpha-clipped URP Lit material per mark, so the paint is lit like the wood. There is no text on the board.

**Sounds** (`Data/Audio/Forecast`, Gamemaster clips, 3D, 2–35 m): `Forecast_RopePull`, `Forecast_ReelTick`, `Forecast_Land`, `Forecast_LandBoss`. Swap clips in the Library Manager.

In WorldMap the board stands in front of the house on its right side (the `Forecast Board` scene root, house-local (10.5, 5), clear of the robots' `Spot_Garden` and the dog's `Visit_Garden`), its plaques turned to the path from the door to the gate.

## Testing

| Command | Does |
| --- | --- |
| `forecast.status` | Tonight's fight per period, shown or hidden |
| `forecast.reveal` | Reveal now, as the rope does (the plaques spin) |
| `forecast.hide` | Back to '?' (not after 19:00: past the self-reveal hour it stays shown) |
| `forecast.set LateNight Boss` | Override one period until `forecast.reset` (not saved); spins if shown |
| `forecast.reset` | Back to `ForecastSettings` |

`ForecastTests` cover the service (defaults, overrides, off), the reveal rules (the day before 06:00, hidden each morning, shown once pulled or after 19:00), `ForecastReveal` on a real clock (a live reveal, hidden the next morning, always shown without a clock or with hiding off), the save round trip, every `ForecastSettings` asset having every period, and the prefab (a plaque per period on a reel, a mark per fight, the '?', the rope with its collider).

## Wiring

`ForecastInstaller` (a `SceneInstaller` on the scene's `GameLifetimeScope`) registers `ForecastSettings`, `ForecastService`, `ForecastReveal` (entry point, ticked), `ForecastSaveSection` and, in cheat builds, `ForecastCommands`, and injects the scene's `ForecastBoard`s with their `ForecastRope` (collected on scene save / entering Play). It needs no other installer; `ForecastReveal` uses the `WorldClock` when there is one. Events (`_Common/Events/ForecastEvents.cs`): `ForecastChanged` (an override) and `ForecastRevealChanged` (`Revealed`, `Live`); the board spins on a live change and shows the state at once otherwise. Without the installer, or with `ForecastSettings.Enabled` off, the plaques stay blank.

Rebuild everything (sounds, textures, materials, prefab, settings, WorldMap placement): `unity command script run --file AgentScripts/Forecast/ForecastBoardSetup.cs --entry ForecastBoardSetup.All`, then the import presets (`AudioImportKit.Apply`, see [tools-unity-broaudio](../../.agents/skills/tools-unity-broaudio/SKILL.md)); each step (`Audio`, `Textures`, `Prefab`, `Data`, `World`) also runs alone. `World` reopens WorldMap from disk, so save the scene first.
