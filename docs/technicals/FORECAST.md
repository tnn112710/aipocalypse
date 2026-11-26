# Forecast

The night forecast and the board in the yard that shows it. Code in `_Core/ForecastSystem/` (namespace `MB13.Aipocalypse.Core.ForecastSystem`), data in `Data/Forecast/`, the board in `Content/Structures/ForecastBoard/`, script map in [CODE_STRUCTURE.md](CODE_STRUCTURE.md). Design and open questions: [docs/gdd/IDEAS.md](../gdd/IDEAS.md) → Night fight periods and sizes.

## Rules today

- A night has three **periods** (`NightPeriod`): Evening 20:00–22:00, Midnight 22:00–00:00, Late night 00:00–02:00. Each brings a **fight** (`FightSize`): none, small, medium or boss.
- **The same forecast every day**: small in all three periods (`ForecastSettings.Periods`). Rolling it per day and balancing come later.
- **Display only.** The forecast doesn't drive the raids yet: raids still follow [RAIDS.md](RAIDS.md) (`EveryNightSchedule`). The period hours are stored but nothing reads them yet.
- **No reveal yet.** The plaques simply show the forecast. A reveal (rope spin, bell, cloth…; options in IDEAS.md) will replace that step later, reading the same `ForecastService`.

## The board

`ForecastBoard.prefab`: a frame of three HIGHLANDS logs (two posts, a crossbar), three Lakefell-wood plaques hanging on ropes, one per period. Each plaque has:

| Part | Shows |
| --- | --- |
| `Period` (quad, top-left) | The period icon, fixed: a setting sun (Evening), a moon (Midnight), a star (Late night) |
| `Mark` (quad over the face) | The fight: small crossed swords (Small), larger crossed swords (Medium), a horned skull (Boss); hidden for none |

The paint is baked: `ForecastBoardSetup.Textures` composites the Layer Lab picto icons on the CPU, wears them with noise and writes one PNG and one alpha-clipped URP Lit material per mark, so the paint is lit like the wood. `ForecastBoard` swaps the `Mark` renderer's material; there is no text on the board.

In WorldMap the board stands in front of the house on its right side (the `Forecast Board` scene root, house-local (10.5, 5), clear of the robots' `Spot_Garden` and the dog's `Visit_Garden`), its plaques turned to the path from the door to the gate.

## Testing

| Command | Does |
| --- | --- |
| `forecast.status` | Tonight's fight per period |
| `forecast.set Evening Boss` | Override one period until `forecast.reset` (not saved) |
| `forecast.reset` | Back to `ForecastSettings` |

`ForecastTests` cover the service (defaults, overrides, off), every `ForecastSettings` asset having every period, and the prefab having a plaque per period and a mark per fight.

## Wiring

`ForecastInstaller` (a `SceneInstaller` on the scene's `GameLifetimeScope`) registers `ForecastSettings`, `ForecastService` and, in cheat builds, `ForecastCommands`, and injects the scene's `ForecastBoard`s (collected on scene save / entering Play). It needs no other system. `ForecastService.Override` / `ClearOverrides` publish `ForecastChanged` (`_Common/Events/ForecastEvents.cs`); the boards refresh on it and on `Start`. Without the installer, or with `ForecastSettings.Enabled` off, the plaques stay blank.

Rebuild everything (textures, materials, prefab, settings, WorldMap placement): `unity command script run --file AgentScripts/Forecast/ForecastBoardSetup.cs --entry ForecastBoardSetup.All`; each step (`Textures`, `Prefab`, `Data`, `World`) also runs alone. `World` reopens WorldMap from disk, so save the scene first.
