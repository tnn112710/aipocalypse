---
name: tools-unity-cozy
description: COZY 4 Stylized Weather in aipocalypse — how COZY draws the sky, clouds, sun and moon while WorldClock decides the time, the bridge that is the only code touching COZY, fog modes, what stays first-party (clock, grades, reactors, ambience, saves), COZY settings and traps here. Use for any sky, cloud, fog, weather, eclipse, sun / moon or time-of-day look work, or anything that touches COZY in this repo.
---

# COZY 4 in aipocalypse

Library facts, API and vendor bugs: the user-scope **`unity-cozy-weather`** skill (read it first; other agents: `~/.claude/skills/unity-cozy-weather/`, or the package source under `Packages/space.distantlands.cozyweather.*`). This file is only what is specific to this repo. The system doc is `docs/technicals/DAYNIGHT.md`; PLUGINS.md has the COZY row.

Installed: COZY 4 core (version in `Packages/space.distantlands.cozyweather.core/package.json`) with Blocks, Eclipse, Horizon, Resound. The empty `Packages/com.distantlands.cozy.*` folders are COZY 3 leftovers.

## 1. Who owns what (decided 2026-11-08)

**COZY draws, our clock decides.** A later switch to COZY's own continuous time and calendar is possible but not decided; don't start it unasked.

| Owner | What |
| --- | --- |
| `WorldClock` (ours) | Time, day, phases, jumps, skips, freeze, speed, moon cycle, all gameplay events. Raids, spawns, HUD, cheats, console and tests read it. COZY never advances time here: its clock is paused |
| Bridge (ours, the only code that touches COZY) | Pushes the director's look hour, our day and moon cycle into COZY each frame; exposes COZY's sun and colours to the director; switches fog modes |
| COZY | Sky (stars, moon, sun disk), clouds, sun and moon lights + ambient (its `Sun Light` / `Moon Light`), its fog when the fog mode is COZY, weather visuals |
| `EnvironmentDirector` (ours) | Grade volumes per hour, reactors (lamps, windows, particles, tints), Daylight / Darkness for the player light and enemy night highlight, night shadow distance, look transitions (By Hours / Continuous). Old looks, sky shaders and its own light stay switchable until the user signs off on COZY. Weather's Unity fog density (`WeatherFogScale`) |
| Ours, not COZY | Ambience and music (BroAudio via `AudioService`: never COZY `AudioFX`, thunder audio or Resound playback), saves (COZY's save is broken), weather (`WeatherService` / `WeatherSchedule`: decisions by our own weighted rules on `WorldClock`, never COZY's forecast or weather timer, blend, wind, rain / wind / thunder sound, lightning flash) |

Gameplay `DayPhase`s don't change for the look; the look may change hourly or continuously.

## 2. Rules

- **Only the bridge references `DistantLands.Cozy`.** Game code reads time from `WorldClock` and the look from `EnvironmentDirector`; never `CozyWeather.Instance`. The bridge is the one sanctioned exception to "no plugin singletons / static events" (AGENTS.md): it may use `CozyWeather` and its static `OnFrameReset` hook, and must unsubscribe on dispose. Cross-system weather news goes out as MessagePipe events from our own code, never COZY's `OnProfileChange`.
- **Vendor profiles stay untouched.** COZY's shipped profiles live in the package; copy one into `Assets/_Game/Data/Environment/Cozy/` before changing it (e.g. stripping `AudioFX` from a weather profile) and reference the copy.
- **Never `AssetDatabase.SaveAssets()`** (AGENTS.md). COZY's `IntegrationValidator` calls it itself on script imports: save your own work first and check `git status` for unrelated files (WorldMap TerrainData, vendor materials) after compiles.
- **COZY settings are project-global** in the package (`Content/Resources/COZY Settings.asset`: styles, `HandleSceneLighting`, downsampling, FX blocking, sky rotation/pitch). Changing them is allowed (they're configuration), but record each change in PLUGINS.md and re-check them after a COZY update (an update may overwrite them).
- **Renderer feature:** `PC_Renderer` is shared by every scene, so the feature is on once WorldMap uses COZY (`CozyKit.Feature`). A scene without a `CozyWeather` then pays a `FindAnyObjectByType` per camera per frame (small); if that ever matters, the bridge's absence is the signal to switch it off at runtime, not a second renderer.
- **FX blocking off** (or a depth-only renderer): it renders a second camera through `PC_Renderer` with all its features every frame.
- **Integrations:** `COZY_SW3` does nothing (the integration is commented out). `COZY_BK` writes the same globals as our `BKFoliageGlobals` every frame once COZY has a Wind module (none yet), with a 30 m `renderDistance` default: match its fields to `BKFoliageGlobals` or turn it off before adding wind.
- **Water and transparents get no COZY fog.** SW3's own COZY fog hook targets COZY 3 and doesn't work with 4. Don't edit SW3; compare fog modes instead.
- **Prompt first.** Eclipse days, Blood Moons, region biomes, weather rules and their effects come from the GDD: propose, get confirmation, then build.
- **Measure.** Perf is judged on the editor benchmark (user, 2026-11-08: editor only, no builds) with the same views and hours before and after; numbers live in DAYNIGHT.md.

## 3. What's built (WorldMap)

- `_Core/EnvironmentSystem/Cozy/CozyBridge.cs` on `Environment/Time Of Day/COZY Weather` (COZY with Atmosphere, Time, Weather modules). The director's **Cozy** field turns COZY mode on: it pushes `CozyBridge.CozyHour(lookHour, sunrise, sunset)` (our sunrise–sunset onto COZY's 06–18), the day and the moon phase, reads COZY's sun back for Daylight and reactors, turns its own light off and hides the skybox in Play. The bridge also matches COZY's moon phase to ours, scales the moon light by phase, mutes COZY fog unless the fog mode is Cozy, and points `RenderSettings.sun` at the moon at night (COZY leaves it on its switched-off sun, and URP would pick the enemy highlight light as the main light).
- Look: `Data/Environment/Cozy/Aipocalypse_Atmosphere.asset`, **baked from the `TOD_*` looks** by `CozySetup.Atmosphere()` (light, ambient, sky horizon, fog colour, stars, night sky). Change a look, then re-bake; don't hand-edit the baked overrides. Its volumetric clouds override (finer raymarch steps) comes from `CozySetup.Clouds()`, which also sets clouds to half resolution: COZY drops its temporal cloud smoothing whenever the camera turns, so coarse steps or quarter resolution show as rings and blocks in first person (DAYNIGHT.md → COZY).
- Style: `Data/Environment/Cozy/Aipocalypse_Style.asset` (Desktop minus sunshafts), first in COZY's style list because COZY always starts on the first. Measure before turning features on (DAYNIGHT.md → COZY → Cost).
- Fog: director **Fog** = Unity (default) / Cozy / VolumetricFog2 / DynamicFog (the Kronnect two are comparison set-ups with fixed colours).
- Setup and switches: `AgentScripts/DayNight/CozySetup.cs` (`Scene`, `Atmosphere`, `Style`, `FogModes`, `Use(bool)`, `Fog(mode)`, `Status`). Tests: `Tests/EditMode/CozyBridgeTests.cs`.
- Weather (phase A, docs/technicals/WEATHER.md): `WeatherService` (ours) picks a sky weather + wind level and calls `CozyBridge.SetWeather(profile, seconds)` with our profile copies in `Data/Environment/Cozy/Weather/` (COZY Audio / Thunder / inert FX stripped; `WeatherSetup.Profiles()`). **COZY's VFX Graph rain is stripped too**: ~18k drops (+1.6–2.2 ms), oval drops, and with FX blocking off COZY never sets its range globals so it doesn't show at all. The rain is `WeatherRain` (Unluck's Rain Multi + Rain Warpy, no collision, fake ground splashes). Wind is ours (`WeatherWind`), never COZY's Wind module.
- Vendor churn: COZY rewrites keywords on its own `Resources` materials at runtime (e.g. `COZY Sunshafts.mat`). Revert, don't commit. A WorldMap save also writes COZY's edit-mode state: `Sun Light` cookie cleared and size 20000, the atmosphere module's parameter cache. Keep only your hunks.

## 4. Driving the editor

The user-scope kit does generic work (`Status`, `Profiles`, `Create`, `SetTime`, `SetWeather`, `Feature`, `Style`, `Lighting`):

```bash
K="$(cygpath -m "$HOME/.claude/skills/unity-cozy-weather/scripts/CozyKit.cs")"; P="$(cygpath -w "$PWD")"
unity command script run --file "$K" --entry CozyKit.Status --project-path "$P" --result-only
```

Project setup, fog-mode switching and benchmarks live in `AgentScripts/DayNight/` (see DAYNIGHT.md → Setup scripts).
