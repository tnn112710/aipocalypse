# Day/night cycle

A world clock, a time-of-day look driven by it, and a digital clock on the HUD. Built in Core: the clock in `_Core/WorldTimeSystem/`, the look in `_Core/EnvironmentSystem/TimeOfDay/`, the HUD widget in `_Core/UI/`. Gameplay rules on top (dusk warning, sleeping, night enemies) are Features and not built yet.

COZY is installed but **not used**: the sky, lighting, fog and grade are ours (PLUGINS.md).

```text
RULES  WorldClock (scene scope, ticked)  ── HourChanged · DayPhaseChanged · DayStarted · TimeJumped · TimeSkipEnded
          │ Now / Phase / MoonPhase (read every frame)
VIEW   EnvironmentDirector (one per scene)            ClockHud → ClockView (HUD, top right)
          look = TimeOfDayProfile at the hour
               → IAtmosphereLayer[] (weather later)
               → sun/moon light, ambient, fog, reflections, sky globals, grade volumes
               → EnvironmentReactors (lamps, windows, particles, material tints)
DATA   ClockSettings · TimeOfDayProfile → LightingPreset + VolumeProfile per key · TimeOfDayAmbience
```

## Clock

`Data/WorldTime/ClockSettings.asset`. Play-mode edits apply at once.

| Setting | Default | What it does |
| --- | --- | --- |
| Seconds Per Game Hour | 30 | Real seconds for one in-game hour at speed 1 |
| Master Speed | 1 | Multiplies every phase (2 = the whole day twice as fast). Use it to speed up testing |
| Start Day / Start Hour | 1 / 07:00 | Where a scene's clock starts |
| Dawn / Morning / Afternoon / Dusk / Evening / Midnight | 06:00 / 08:00 / 12:00 / 16:00 / 18:00 / 00:00, speed 1 | Phase starts (they must keep that order around the clock) and a speed per phase on top of Master Speed |
| Moon Cycle Days / First Full Moon Day | 8 / 1 | Moon phase: 0 new, 0.5 full |

The inspector's **Real time** box shows the result. At the defaults:

| | In-game | Real time |
| --- | --- | --- |
| Full day | 24 h | 12 min (1 game minute = 0.5 s) |
| Midnight | 00:00–06:00 | 3 min |
| Dawn | 06:00–08:00 | 1 min |
| Morning | 08:00–12:00 | 2 min |
| Afternoon | 12:00–16:00 | 2 min |
| Dusk | 16:00–18:00 | 1 min |
| Evening | 18:00–24:00 | 3 min |

`WorldClock` (plain C#, `ITickable`) runs on scaled time, so a `TimeService` pause stops it. Its own speed requests only affect the clock.

| API | Use |
| --- | --- |
| `Now` (`WorldTime`: `Day`, `Hour`, `WholeHour`, `Minute`, `TotalHours`), `Phase`, `MoonPhase` | Read every frame |
| `JumpTo(hour)`, `JumpToPhase(phase)`, `JumpToNextHour()`, `JumpToNextPhase()` | Instantly to the next time the clock shows that hour |
| `SetTime(WorldTime)` | Instantly to any time, also backwards (debug, loading a save) |
| `SkipTo(hour, realSeconds)` | Fast-forward (sleeping): hours pass normally, every event is sent, ends with `TimeSkipEnded`. It ignores speed requests (a freeze doesn't stop a sleep). `CancelSkip()` or any instant jump ends it, also with `TimeSkipEnded` |
| `RequestSpeed(owner, scale)`, `Freeze(owner)`, `Release(owner)` | Slow (0–1) or freeze the clock for an owner (cutscene, tutorial). The slowest request wins |
| `RealSecondsUntil(hour)`, `HoursPerSecond` | Countdowns ("next storm in 08:18") |

Events (MessagePipe, `_Common/Events/WorldTimeEvents.cs`):

| Event | When |
| --- | --- |
| `HourChanged` | Every whole hour the clock passes, also while skipping (one per hour, even in one long frame) |
| `DayPhaseChanged` (Previous, Current) | Dawn / Morning / Afternoon / Dusk / Evening / Midnight changed (Midnight starts at 00:00, with the new day) |
| `DayStarted` | Midnight |
| `TimeJumped` (From, To) | An instant jump. The hours in between are not replayed: resync instead. `DayStarted` / `DayPhaseChanged` still follow if the day or phase changed |
| `TimeSkipEnded` | A `SkipTo` reached its target |

Phases are for gameplay and stay fixed while the look changes: retuning a sunset never moves when Dusk starts.

**Cheats** (editor and development builds, `Debug` action map in `InputSystem_Actions`): **N** jumps to the next whole hour, **M** to the start of the next phase. `WorldClockCheats` reads them through `InputService`.

**Scope:** `GameLifetimeScope` → `Clock` field. Empty = no clock: the HUD clock hides and the sky keeps its preview hour. Tests: `Tests/EditMode/WorldClockTests.cs`, `TimeOfDayTests.cs` (time windows, profile blending, sun path). `AgentScripts/DayNight/Sweep.cs` samples the director over a day in Play mode and flags sudden jumps in light, ambient or fog; one frame costs about 8 µs and allocates nothing.

## Look of the day

`Data/Environment/MeadowHome/TimeOfDay/MH_TimeOfDay.asset` lists keys; between two keys everything blends (Smooth, Linear or Hold per key).

**When the look changes** (`EnvironmentDirector → Transition`, Play mode):

| Transition | The look shown |
| --- | --- |
| **By Phases** (default) | Holds one look per phase, the profile's `Phase Looks` hour: Dawn 07:00, Morning 09:00, Afternoon 14:00, Dusk 17:30, Evening 20:30, Midnight 00:00. Changes when a phase starts. |
| By Hours | The look at the current whole hour; changes on the hour. |
| Continuous | The look at the exact time, every frame. |

A change takes `Transition Duration` (5 s, real time; 0 = cut). It glides the hour the look is sampled at, the short way round, so an Afternoon → Dusk change plays afternoon → golden hour in 5 s and the grades still blend key to key. The sun and moon belong to the look too: in By Phases / By Hours the light and the sky's sun and moon hold still between transitions (no shadow shimmer) and swing to their new place during the change. Light still fades by the shown sun's height, so pick phase hours with the sun or moon up: at 19:00 the sun is on the horizon and Dusk would get no direct light. Reactors with an hour window (lamps, mist, birds) follow the clock; Daylight / Darkness reactors follow the shown sun. `EnvironmentDirector.LookHour` is the hour shown; layers still get the real hour. Edit-mode preview is always continuous.

| Hour | Key | Look (`Looks/TOD_*`) | Grade (`Grades/PP_*`) |
| --- | --- | --- | --- |
| 00:00 | Midnight | Midnight | Night |
| 04:30 | Blue hour | BlueHour | Night |
| 05:30 | Dawn | Dawn | Dawn |
| 06:30 | Sunrise | Sunrise | Golden |
| 09:00 | Morning | Morning (the old static Meadow Home look) | Day (the old grade) |
| 12:30 | Noon | Noon | Day |
| 16:00 | Afternoon | Afternoon | Day |
| 18:00 | Golden hour | GoldenHour | Golden |
| 19:00 | Sunset | Sunset | Golden |
| 20:00 | Dusk | Dusk | Night |
| 21:30 | Night | Night | Night |

- **`LightingPreset`** (one asset per look): sun colour / intensity / shadow strength, moon colour / intensity / shadows, trilight ambient, reflection intensity, fog colour / density, sky (painted tint and exposure, night gradient and how much it covers the painted sky, horizon glow toward the sun, sun disc, stars, moon glow), far-mountain haze, unlit-effect tint. Sun direction is not in a look.
- **Grades:** the scene's base volume (`MH_PostProcessing`) stays. `Time Of Day/Grade From` (priority 10, weight 1) holds the key before the hour and `Grade To` (priority 11) the next key, weighted by the blend. URP's volume stack does the lerp. Every grade must override the same settings (colour adjustments, white balance, bloom, vignette), or a setting pops at the key; the profile shows a warning when they differ.
- **Sun path** (profile → Sun and moon): sunrise 06:00, sunset 19:00, noon elevation 50°, rising at bearing 70° and travelling 220°. At 09:00 the sun is where the old static light was (33°, bearing 121). The moon rises at sunset (bearing 100°, 45° high).
- **One shadowed directional light:** the sun by day, the moon at night (full moon brightest, new moon a third). Each fades in over the first `HorizonFade` (12°) above the horizon, about one game hour. The swap happens at twilight while both are near zero. The light never goes below `MinLightElevation` (8°), so terrain is never lit from below. At 30 s per hour the sun moves 0.5° per second. A shadow map whose light turns every frame makes shadow edges crawl, so in Continuous mode (the other transitions already hold the light still) the light holds still until the sun has moved `Shadow Step` (1°) and then glides there over `Shadow Glide` (0.3 s): a short slide about every 2 s instead of constant shimmer. Time jumps, skips, big moves and the sun/moon swap snap instead. The sky's sun disc still moves every frame. `Shadow Step` 0 = follow the sun every frame; `Shadow Glide` 0 = snap each step. In Play mode, while the moon lights the scene, `Night Shadow Distance` (60 m) replaces the URP asset's shadow distance (150 m), since faint moon shadows don't need the range. It swaps with the light at twilight and is restored when the director is disabled or Play stops (0 = keep the asset's).
- **Sky:** `MH_Sky_TimeOfDay.mat` with `Aipocalypse/Skybox/Time Of Day` (`_Core/EnvironmentSystem/Shaders/`). It draws the same Komorebi panorama as before (same rotation, tint × exposure) and adds a night gradient, a horizon glow, a sun disc, procedural twinkling stars and a moon with phases. Its time values are `_TOD_*` shader globals the director sets, so the material asset is never written. With no director it uses its own Fallback Tint / Exposure. Star and moon size live on the material.

**`EnvironmentDirector`** (`Environment/Time Of Day`) is the only writer of the sun light, RenderSettings ambient / fog / reflections, the sky globals and the two grade volumes. Atmosphere.cs still seeds those values when the generator runs, but the director overrides them in Play mode.

- **Preview** (edit mode): tick Preview and drag Preview Hour, or set Preview Speed to watch the day play. It writes the scene's lighting, so turn it off before saving. With Preview off, edit mode still feeds the sky shader at Preview Hour (globals only, nothing saved).
- **Jump To Hour** button: in Play mode it moves the clock, in edit mode the preview.

**Weather seam:** `IAtmosphereLayer.Modify(ref AtmosphereState, hour)` runs after the profile, in `Order`. Add one with `EnvironmentDirector.AddLayer`. Keep changes relative (sun ×0.35, fog ×4, lerp the fog toward grey) so one rain layer works at any hour. A layer that needs its own grade adds its own global volume. Nothing implements it yet.

## Things that follow the time

A scene object that reacts gets an `EnvironmentReactor` component. The director ticks them after applying the look, in Play mode only. It has no `Update` of its own. Reactors placed in a scene are collected automatically when the scene is saved or Play starts (`Collect scene reactors` button too). Reactors spawned at runtime register when injected (`resolver.Instantiate`).

| Component | Does |
| --- | --- |
| `TimeOfDayLight` | Fades its lights (or all child lights) from 0 to their authored intensity. Lamps, lanterns, window lights |
| `TimeOfDayEmission` | Swaps the listed materials (e.g. `Window_Glass`) in child renderers for one runtime copy each with emission, glowing over the hours |
| `TimeOfDayParticles` | Scales child emitters' rate between Off Rate and On Rate. At 0 they stop, bursts too: on screen their live particles finish, off screen they are cleared (culled emitters don't simulate, so they'd reappear later). `Align To Sun` points each emitter's +Y at the sun (god rays) |
| `TimeOfDayActive` | Shows objects only during their hours |
| `AtmosphereTint` | Multiplies (or replaces) colour properties of child materials by an atmosphere channel (Unlit Tint, Haze, Fog, Sun, Ambient Sky), on runtime copies |

The four `TimeOfDay*` types share a weight from **Hours** (a `TimeWindow`: On, Off, Fade; may wrap past midnight), **Daylight** (follows the sun) or **Darkness**, eased over `Response` real seconds so N / M jumps fade instead of popping.

- **Add a lamp:** put the light on the lamp prefab at its "on" intensity, add `TimeOfDayLight` with hours (for example On 18:15, Off 06:15, Fade 0.75). For a glowing shade, add `TimeOfDayEmission` with the shade material.
- **New kind of reactor:** derive from `TimeOfDayReactor` and implement `Apply(float weight)`, or from `EnvironmentReactor` for something that reads `director.State` / `SunDirection` every frame.
- **Game logic** (spawn waves, shop hours, fish bite times) subscribes to `DayPhaseChanged` / `HourChanged` or reads `WorldClock.Now`. It doesn't use reactors.

Meadow Home wiring (`DayNightSetup.Scene`):

| Object | Reactor |
| --- | --- |
| Atmosphere/God Rays | Particles by daylight, aligned to the sun; tinted |
| Atmosphere/Mist | Particles ×1 → ×2.6 from 04:00 to 09:30 (morning mist); tinted |
| Atmosphere/Birds | Particles 05:00–20:00; tinted |
| Atmosphere/Butterflies | Particles 07:30–18:30 |
| Atmosphere/Seed Fluff, Sunbeam Motes | Particles by daylight (motes tinted) |
| Atmosphere/Fireflies | Particles 19:30–05:00 |
| Atmosphere/Swamp Fog, Clouds; Boundary/Haze | Tinted (Unlit Tint × `_BaseColor`) |
| Boundary/Peaks | `_FogColor` replaced by the Haze channel (HIGHLANDS / Grasslands mountains skip scene fog) |
| Landmarks/Waterfall | Sheet (five colour properties), ripples, splash and mist tinted |
| Home Base/House_1 | `Window_Glass` glows 18:00–06:30 |
| Home Base/Night Lights | Warm door light 18:15–06:15 |

## Ambience

`TimeOfDay/MH_Ambience.asset` (`TimeOfDayAmbience`), played by `AmbienceService` (scene scope, needs the clock):

- **Beds** loop all day; each has a volume curve over 0–24 h. Birds (Lakefell ambience clips) peak at dawn for the chorus, drop through the day and stop at 20:00. Meadow wind is louder at night.
- **Calls** play now and then inside their hours: owls (HIGHLANDS clips) from 20:30 to 04:30, every 18–45 s. Calls pause while the game is paused or the clock is skipping.
- Sounds are BroAudio entities in `Data/Audio/Ambience/` (library `Ambience.asset`). Volume and pitch randomness live on the entities. No cricket clips are installed.

## HUD clock

Digital, as in Escape from Duckov: big monospaced `HH:MM` with the phase and day stacked beside it, on a translucent panel in `SafeFrame/TopRight`. Layout and style: [UI.md → Clock](UI.md#clock).

## Changing things

| To change | Edit |
| --- | --- |
| Day length | `ClockSettings` → Seconds Per Game Hour (the Real time box updates) |
| Test faster | `ClockSettings` → Master Speed in Play mode, or N / M |
| Night shorter or longer | `ClockSettings` → Evening / Midnight → Speed |
| When phases start | `ClockSettings` phase starts (gameplay timing; the look doesn't move) |
| What an hour looks like | Its `Looks/TOD_*.asset` (live in Play mode and in the edit preview) |
| Add an hour | Add a key to `MH_TimeOfDay` with a look and a grade (duplicate a look) |
| Post-processing per hour | `Grades/PP_*.asset`; keep the same overrides in all of them |
| Where the sun rises, how high it gets | `MH_TimeOfDay` → Sun and moon |
| Stars, moon size | `MH_Sky_TimeOfDay.mat` |
| Unlit effects at night | Each look's Unlit Tint |
| Lamps, windows | `TimeOfDayLight` / `TimeOfDayEmission` hours and colours |
| Ambience | `MH_Ambience` curves and calls |

## Setup scripts

`AgentScripts/DayNight/` (run with `unity command run_script`):

- `DayNightSetup.Assets(reset)` creates the data assets. **`reset = true` overwrites the looks, grades, profile keys and ambience with the script's values.** Use `false` to keep hand tuning and only add what's missing.
- `DayNightSetup.Scene()` wires Meadow Home (director, grade volumes, sky, reactors, door light, scope fields) and saves the scene. Re-run it after `Atmosphere.Particles()` rebuilds the atmosphere groups.
- `DayNightSetup.Hud()` rebuilds `SafeFrame/TopRight/Clock` in `HUD.prefab`.
- `Prof.Start()` / `Prof.StartGpu()`, `Prof.Stop()`, `Prof.Window(from, to)`, `Prof.Compare(aFrom, aTo, bFrom, bTo, thread, top)`, `Prof.Spikes(...)`: Play-mode profiler captures (median CPU / GPU frame time, GC per frame, markers that grew between two frame ranges). Measured: a look transition costs about 0.3 ms main thread + 0.3 ms GPU over a hold (1–4 fps in the editor); the director writes the light, RenderSettings and volumes only when a value changes, so holds cost nothing.
- `DayNightSetup.RestoreHudOverrides(path)` sets the open scene's HUD instance overrides back to those of another copy of the scene (for example `git show HEAD:<scene>` saved to a temp file). Editor layout passes write layout-driven values onto the instance, which would otherwise show up as noise in the scene diff.
- `Review.cs`: Play-mode helpers (`SetHour`, `Freeze`, `View`, `State`) for screenshots. Unity stops ticking Play mode while the editor is in the background: run `Application.runInBackground = true` (eval) first.

## Known limits

- The painted sky still has its own sun glow, painted in one place (rotation 17); the sun disc moves on its own. A fully procedural stylized sky would replace the painting.
- Reactors act in Play mode only; the edit preview shows the light, ambient, fog, sky and grades.
- Underwater fog (Stylized Water 3 areas) and the water materials are not driven by the time of day yet.
- Not built (GDD rules, Features): dusk warning, sleeping (use `SkipTo`), weather, night enemies.
