# Day/night cycle

A world clock, a time-of-day look driven by it, and a digital clock on the HUD. Built in Core: the clock in `_Core/WorldTimeSystem/`, the look in `_Core/EnvironmentSystem/TimeOfDay/`, the HUD widget in `_Core/UI/`. Gameplay rules on top (dusk warning, sleeping, night enemies) are Features and not built yet.

COZY is installed but **not used**: the sky (our Time Of Day shader or Kronnect Skybox Plus, both driven by the director), lighting, fog and grade are ours (PLUGINS.md).

```text
RULES  WorldClock (scene scope, ticked)  ── HourChanged · DayPhaseChanged · DayStarted · TimeJumped · TimeSkipEnded
          │ Now / Phase / MoonPhase (read every frame)
VIEW   EnvironmentDirector (one per scene)            ClockHud → ClockView (HUD, top right)
          look = TimeOfDayProfile at the hour
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
| Phases (Dawn / Morning / Afternoon / Dusk / Evening / Midnight) | 06:00 / 08:00 / 12:00 / 16:00 / 18:00 / 00:00, speed 1 | One entry per `DayPhase`: its start (they must keep that order around the clock) and a speed on top of Master Speed. A missing phase warns and counts as 00:00, speed 1 |
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

**Scope:** a `WorldTimeInstaller` component on the scene's `GameLifetimeScope` (`Settings` = `ClockSettings.asset`) installs the clock. No installer = no clock: the HUD clock hides and the sky keeps its preview hour. Tests: `Tests/EditMode/WorldClockTests.cs`, `TimeOfDayTests.cs` (time windows, profile blending, sun path), `PhaseDataTests.cs` (every `ClockSettings`, `ClockStyle` and `TimeOfDayProfile` asset has every phase). `AgentScripts/DayNight/Sweep.cs` samples the director over a day in Play mode and flags sudden jumps in light, ambient or fog; one frame costs about 8 µs and allocates nothing.

## Look of the day

`Data/Environment/TimeOfDay/TimeOfDay.asset` lists keys; between two keys everything blends (Smooth, Linear or Hold per key).

**When the look changes** (`EnvironmentDirector → Transition`, Play mode):

| Transition | The look shown |
| --- | --- |
| By Phases (code default) | Holds one look per phase, the profile's `Phase Looks` hour: Dawn 07:00, Morning 09:00, Afternoon 14:00, Dusk 17:30, Evening 20:30, Midnight 00:00. Changes when a phase starts. |
| **By Hours** (WorldMap) | The look at the current whole hour; changes on the hour. |
| Continuous | The look at the exact time, every frame. |

While a held look stays the same (no glide running, the moon phase barely moved), the director skips re-applying it: no light, `RenderSettings`, sky-global or volume writes. Editing a `LightingPreset`, the profile or the director bumps a revision, so tuning in Play mode still shows at once.

A change takes `Transition Duration` (5 s, real time; 0 = cut). It glides the hour the look is sampled at, the short way round, so an Afternoon → Dusk change plays afternoon → golden hour in 5 s and the grades still blend key to key. The sun and moon belong to the look too: in By Phases / By Hours the light and the sky's sun and moon hold still between transitions (no shadow shimmer) and swing to their new place during the change. Light still fades by the shown sun's height, so pick phase hours with the sun or moon up: at 19:00 the sun is on the horizon and Dusk would get no direct light. Reactors with an hour window (lamps, mist, birds) follow the clock; Daylight / Darkness reactors follow the shown sun. `EnvironmentDirector.LookHour` is the hour shown; layers still get the real hour. Edit-mode preview is always continuous.

| Hour | Key | Look (`Looks/TOD_*`) | Grade (`Grades/PP_*`) |
| --- | --- | --- | --- |
| 00:00 | Midnight | Midnight | Night |
| 04:30 | Blue hour | BlueHour | Night |
| 05:30 | Dawn | Dawn | Dawn |
| 06:30 | Sunrise | Sunrise | Golden |
| 09:00 | Morning | Morning (the old static world map look) | Day (the old grade) |
| 12:30 | Noon | Noon | Day |
| 16:00 | Afternoon | Afternoon | Day |
| 17:00 | Golden hour | GoldenHour | Golden |
| 18:00 | Sunset | Sunset | Golden |
| 19:00 | Dusk | Dusk | Night |
| 20:30 | Night | Night | Night |

- **`LightingPreset`** (one asset per look): sun colour / intensity / shadow strength, moon colour / intensity / shadows, trilight ambient, reflection intensity, fog colour / density, sky (painted tint and exposure, night gradient and how much it covers the painted sky, horizon glow toward the sun, sun disc, stars, moon glow), far-mountain haze, unlit-effect tint. Sun direction is not in a look.
- **Grades:** each `PP_*` grade is the whole post-processing look (tonemapping, colour adjustments, white balance, bloom, vignette, lift/gamma/gain); the scene has no base volume under them. `Time Of Day/Grade From` (priority 10, weight 1) holds the key before the hour and `Grade To` (priority 11) the next key, weighted by the blend. URP's volume stack does the lerp. The director switches a grade volume off while its weight is 0, so outside a blend between two different grades only Grade From is on. The saved scene keeps Grade From on `PP_Day` so edit mode (no director running) shows the morning grade. Every grade must override the same settings, or a setting pops at the key; the profile shows a warning when they differ. `Base_PostProcessing.asset` is only the source the setup script copies the fixed settings from (`DayNightSetup.SingleGrade()` merges it into the grades and removes the scene's base volume).
- **Sun path** (profile → Sun and moon): sunrise 06:00, sunset 19:00, noon elevation 50°, rising at bearing 70° and travelling 220°. At 09:00 the sun is where the old static light was (33°, bearing 121). The moon rises at sunset (bearing 100°, 45° high).
- **One shadowed directional light:** the sun by day, the moon at night (full moon brightest, new moon `NewMoonLight` (a third) of it; both on the profile's Sun and moon group). Each fades in over the first `HorizonFade` (12°) above the horizon, about one game hour. The swap happens at twilight while both are near zero. The light never goes below `MinLightElevation` (8°), so terrain is never lit from below. At 30 s per hour the sun moves 0.5° per second. A shadow map whose light turns every frame makes shadow edges crawl, so in Continuous mode (the other transitions already hold the light still) the light holds still until the sun has moved `Shadow Step` (1°) and then glides there over `Shadow Glide` (0.3 s): a short slide about every 2 s instead of constant shimmer. Time jumps, skips, big moves and the sun/moon swap snap instead. The sky's sun disc still moves every frame. `Shadow Step` 0 = follow the sun every frame; `Shadow Glide` 0 = snap each step. In Play mode, while the moon lights the scene, the director requests `Night Shadow Distance` (60 m) from `GraphicsService` (root), which owns the URP asset's shadow distance (150 m) and applies the shortest request, since faint moon shadows don't need the range. The request swaps with the light at twilight and is released when the director is disabled; the service puts the asset's value back when Play stops (0 = no request).
- **Sky:** `TimeOfDay_Sky.mat` with `Aipocalypse/Skybox/Time Of Day` (`_Core/EnvironmentSystem/Shaders/`). It draws the same Komorebi panorama as before (same rotation, tint × exposure) and adds a night gradient, a horizon glow, a sun disc, procedural twinkling stars and a moon with phases. Its time values are `_TOD_*` shader globals the director sets, so the material asset is never written. With no director it uses its own Fallback Tint / Exposure. A look's sky tint and exposure are Skybox/Panoramic values, i.e. gamma space: the director converts them to linear before setting the globals (as Unity does for Panoramic's `[Gamma]` exposure and its tint), and the material's exposure is `[Gamma]` too. Passed raw, the sky renders about 10× too bright, clips to white and takes the grade's warm white balance (the flat peach sky of 2026-10-24). Star and moon size live on the material.
- **Skybox Plus sky (current WorldMap sky, on trial):** `SkyboxPlus_Sky.mat` (made from the Clouds-B1 preset by `SkyboxPlusSetup.Create`) on `Aipocalypse/Skybox/Skybox Plus Star Map` (`SkyStarMap.shader`), our fork of Kronnect's `Skybox/Skybox Plus/Clouds`: the vendor shader plus a **Star Map** toggle. Its own sky colours, sunset tint, sun disc, cloud layer and moon come from the material; the horizon fades into the look's fog colour (Use Unity Fog Color). The director feeds it the sun and moon through Skybox Plus's custom-position globals (`_SBPCustomPositions`, `_WorldSpaceSunDir`, `_LightWL`, `_WorldSpaceMoonDir`, `_MoonLightWL`), so Skybox Plus's own `SkyboxPlus` component is not used. Skybox Plus darkens its sky and shows stars from its sun's height alone and stays day-blue until the sun is ~27° down, while our sun sinks only 30° by midnight: below **Skybox Plus Dusk Elevation** (6°) the sun it gets sinks faster (`EnvironmentDirector.SkyboxPlusSun`): **Skybox Plus Morning Sink** (3.5) times before noon, **Evening Sink** (5) after. The evening is deeper because the night grade already darkens the ground at sunset: at 3.5 the 19:00 sky stayed bright royal blue with white clouds over a night-dark landscape, then cut to black at 20:00; at 5 it is deep indigo with the first stars. At dawn the dawn grade lights the ground, so 06:00 keeps a twilight-blue sky. The sink switches at noon and midnight, where both give the same sky. Stars from about 19:00 to 05:00; the disc sets a little early, while sunlight is already fading. Its moon phase is a material property, not a global: in Play mode the director swaps the scene's sky for a runtime copy and writes `_MoonPhase` (-1 new … 1 full) and `_MoonPhaseAngle` (0 waxing, lit right; 180 waning) to it; edit mode shows the material's phase.
- **Star map:** with Star Map on, the night sky is `StarMap/StarMap.png` (equirectangular, celestial coordinates: right ascension 0h at the left edge growing rightward, +90° declination at the top; the Milky Way at its real place), imported as a BC7 cubemap, in place of Skybox Plus's procedural stars. The generator and its README are in `AgentScripts/DayNight/StarMap/`. It sits under the clouds and fades out with twilight, fog and moon glare like the procedural stars. The director passes `_StarMapRotation`, so the sky turns 15° per game hour around the celestial pole: **Star Latitude** (30°) sets how high the pole stands, **Galactic Core Transit** (23:00) the hour the Milky Way's core is at its highest, and **Star Azimuth** (180°) turns the whole star sky around the vertical: at 180 the core stands due north, about 31° up over WorldMap's northern mountains (seen from the house), instead of the real south. Cubemap layout, measured: Unity's latitude-longitude import puts u = 0.5 + atan2(x, z) / 2π (+X holds RA 18h). It follows the shown look hour, like the sun. Star Map off = the vendor's procedural stars.
- **Star twinkle:** the star map's point stars twinkle; the Milky Way's glow stays steady (a mip three levels blurrier holds the glow, and only what stands above it is dimmed). Each cell of about one star (turning with the star map) eases between random brightnesses several times a second at its own rate and offset, stronger and faster low in the sky, like real scintillation. Cells are deliberately not blended: a first version blended 8 neighbouring cells, which averaged the flicker away (a few percent, invisible) and ran 2–5 s cycles. Measured now at level 1: a star's brightness swings 31% (median; p90 53%) over six frames. Material (`SkyboxPlus_Sky.mat`): **Star Map Twinkle Level** (0 calm … 1 strong: a star can dip to ~10% overhead), **Speed** (changes per second, 4), **Horizon Boost**, **Cells** (per radian; 250 ≈ one per star at 8K). On / off is a player setting, `GraphicsService.StarTwinkle` (on by default; the global keyword `STAR_TWINKLE`): off runs the shader variant without it, so a low-end PC pays nothing. Measured with the sky filling the view: GPU median 4.0 / 4.0 ms and 4.2 / 4.1 ms on / off, within the 0.1 ms noise. It runs on shader time, so it keeps twinkling while the game is paused, like Skybox Plus's own procedural stars. Edit mode shows no twinkle (the keyword is set by the service).
- **Switching skies:** the director feeds both skies all the time, so the scene's skybox material decides which one shows. `SkyboxPlusSetup.Use(true / false)` sets it and saves the scene; `TimeOfDay_Sky.mat` is unchanged. `DayNightSetup.Scene()` keeps a Skybox Plus sky.

**`EnvironmentDirector`** (`Environment/Time Of Day`) is the only writer of the sun light, RenderSettings ambient / fog / reflections, the sky globals and the two grade volumes. Atmosphere.cs still seeds those values when the generator runs, but the director overrides them in Play mode.

- **Preview** (edit mode): tick Preview and drag Preview Hour, or set Preview Speed to watch the day play. It writes the scene's lighting, so turn it off before saving. With Preview off, edit mode still feeds the sky shader at Preview Hour (globals only, nothing saved).
- **Jump To Hour** button: in Play mode it moves the clock, in edit mode the preview.

**Weather:** not built. The planned seam is a layer that modifies the evaluated `AtmosphereState` in `EnvironmentDirector.Apply`, after the profile and before anything is written (an unused `IAtmosphereLayer` version was removed: see git history). The held-look early-out must then re-apply while a layer runs. Keep changes relative (sun ×0.35, fog ×4, lerp the fog toward grey) so one rain layer works at any hour; a layer that needs its own grade adds its own global volume.

## Things that follow the time

A scene object that reacts gets an `EnvironmentReactor` component. The director ticks them after applying the look, in Play mode only. It has no `Update` of its own. Reactors placed in a scene are collected automatically when the scene is saved or Play starts (`Collect scene reactors` button too). Reactors spawned at runtime register when injected (`resolver.Instantiate`).

| Component | Does |
| --- | --- |
| `TimeOfDayLight` | Fades its lights (or all child lights) from 0 to their authored intensity. Lamps, lanterns, window lights |
| `TimeOfDayEmission` | Swaps the listed materials (e.g. `Window_Glass`) in child renderers for one runtime copy each with emission, glowing over the hours |
| `TimeOfDayParticles` | Scales child emitters' rate between Off Rate and On Rate. At 0 they stop, bursts too: on screen their live particles finish, off screen they are cleared (culled emitters don't simulate, so they'd reappear later). `Align To Sun` points each emitter's +Y at the sun (god rays) |
| `TimeOfDayActive` | Shows objects only during their hours |
| `AtmosphereTint` | Multiplies (or replaces) colour properties of child materials by an atmosphere channel (Unlit Tint, Haze, Fog, Sun, Ambient Sky), on runtime copies |
| `TimeOfDayWeight` | Nothing on its own: exposes the eased `Weight` (-1 before the director's first tick) for other code. The player's light reads one |

The five `TimeOfDay*` types share a weight from **Hours** (a `TimeWindow`: On, Off, Fade; may wrap past midnight), **Daylight** (follows the sun: 0 below `DaylightStartElevation` (-6°), 1 above `DaylightFullElevation` (10°), on the profile's SunPath) or **Darkness**, eased over `Response` real seconds so N / M jumps fade instead of popping.

- **Add a lamp:** put the light on the lamp prefab at its "on" intensity, add `TimeOfDayLight` with hours (for example On 18:15, Off 06:15, Fade 0.75). For a glowing shade, add `TimeOfDayEmission` with the shade material.
- **New kind of reactor:** derive from `TimeOfDayReactor` and implement `Apply(float weight)`, or from `EnvironmentReactor` for something that reads `director.State` / `SunDirection` every frame.
- **Game logic** (spawn waves, shop hours, fish bite times) subscribes to `DayPhaseChanged` / `HourChanged` or reads `WorldClock.Now`. It doesn't use reactors.

World map wiring (`DayNightSetup.Scene`):

| Object | Reactor |
| --- | --- |
| Atmosphere/God Rays | Particles by daylight, aligned to the sun; tinted |
| Atmosphere/Mist | Particles ×1 → ×2.6 from 04:00 to 09:30 (morning mist); tinted |
| Atmosphere/Birds | Particles 05:00–18:00; tinted |
| Atmosphere/Butterflies | Particles 07:30–17:00 |
| Atmosphere/Seed Fluff, Sunbeam Motes | Particles by daylight (motes tinted) |
| Atmosphere/Fireflies | Particles 20:00–05:00 |
| Atmosphere/Swamp Fog, Clouds; Boundary/Haze | Tinted (Unlit Tint × `_BaseColor`) |
| Boundary/Peaks | `_FogColor` replaced by the Haze channel (HIGHLANDS / Grasslands mountains skip scene fog) |
| Landmarks/Waterfall | Sheet (five colour properties), ripples, splash and mist tinted |
| Home Base/House_1 | `Window_Glass` glows 18:00–06:30 |
| Home Base/Night Lights | Warm door light 18:15–06:15 |

## Player light in the dark

At night a light around the player lights the near ground, so things close by read clearly. A worn lantern makes it warmer and wider. Code: `PlayerSystem/PlayerVision.cs`; tuning: `Data/Player/PlayerVisionTuning.asset`; the lantern: [EQUIPMENT.md → Belt](EQUIPMENT.md#belt).

- **When:** `Player.prefab → VisionLight` carries a `TimeOfDayWeight` on **Darkness** (response 1.5 s), so the light fades in and out with the shown sun, like the lamps. By day the light is disabled (no cost). With no director in the scene it never turns on.
- **How far:** radius = `StatId.VisionRadius` (base `Radius`, 4 m) + the worn lantern's `Radius` (6 m). Perks raise it through `IStatProvider`.
- **Radius → light:** URP point lights fade with distance squared, so a bigger range alone barely widens the lit area. `PlayerVision` sets the intensity so the ground at the radius' edge keeps `Brightness` (vision 0.12, lantern 0.15): a bigger radius is a brighter light. The range reaches `RangeScale` (1.6) times the distance to that edge, and `MaxIntensity` (30) caps it. At the defaults: no lantern ≈ intensity 4, range 8 m; lantern ≈ intensity 22, range 17 m.
- **Where:** the light sits above the head (`Position` 3 m; `LanternPosition` 3.2 m, a little to the right). The lantern isn't drawn in first person, so its light is placed for the look: from the belt it lit the underside of the gun and little else, and closer to the hands it washed them out.
- **Changing lantern** (on / off / another lantern) eases radius, colour, brightness and position over `BlendTime` (0.35 s). A lantern flickers (`Flicker`, `FlickerSpeed`).
- **No shadows** by default: a point light renders six shadow maps. `LanternDefinition.Shadows` turns them on per lantern; measure first.
- Against the current night looks (moonlit, fairly bright ambient) the lit area shows near you, but the far field stays readable. Darker `TOD_Night` / `TOD_Midnight` ambient would make the radius matter more.

## Ambience

`TimeOfDay/TimeOfDay_Ambience.asset` (`TimeOfDayAmbience`), played by `AmbienceService` (scene scope): the `EnvironmentInstaller`'s optional Ambience, installed only when the scope also has a ready `WorldTimeInstaller` (else a warning):

- **Beds** loop all day; each has a volume curve over 0–24 h. Birds (Lakefell ambience clips) peak at dawn for the chorus, drop through the day and stop at 20:00. Meadow wind is louder at night.
- **Calls** play now and then inside their hours: owls (HIGHLANDS clips) from 20:30 to 04:30, every 18–45 s. Calls wait while the game is paused (`TimeService.IsPaused`) or the clock is skipping.
- Sounds are BroAudio entities in `Data/Audio/Ambience/` (library `Ambience.asset`). Volume and pitch randomness live on the entities. No cricket clips are installed.

## HUD clock

Digital, as in Escape from Duckov: big monospaced `HH:MM` with the phase and day stacked beside it, on a translucent panel in `SafeFrame/TopRight`. Layout and style: [UI.md → Clock](UI.md#clock).

## Changing things

| To change | Edit |
| --- | --- |
| Day length | `ClockSettings` → Seconds Per Game Hour (the Real time box updates) |
| Test faster | `ClockSettings` → Master Speed in Play mode, or N / M |
| Night shorter or longer | `ClockSettings` → Phases → Evening / Midnight → Speed |
| When phases start | `ClockSettings` → Phases → Start (gameplay timing; the look doesn't move) |
| Which look a phase holds (By Phases) | `TimeOfDay` → Phase Looks |
| Phase names and colours on the HUD | `Data/UI/ClockStyle.asset` → Phases |
| What an hour looks like | Its `Looks/TOD_*.asset` (live in Play mode and in the edit preview) |
| Add an hour | Add a key to `TimeOfDay` with a look and a grade (duplicate a look) |
| Post-processing per hour | `Grades/PP_*.asset`; keep the same overrides in all of them |
| Where the sun rises, how high it gets | `TimeOfDay` → Sun and moon |
| Stars, moon size | `TimeOfDay_Sky.mat`; Skybox Plus: `SkyboxPlus_Sky.mat` (Star Map Intensity, or the procedural Star settings with Star Map off) |
| How much the stars twinkle / turning it off | `SkyboxPlus_Sky.mat` → Star Map Twinkle Level, Speed, Horizon Boost; off: `GraphicsService.StarTwinkle = false` |
| Where the Milky Way stands at night | `EnvironmentDirector` → Star Azimuth (direction), Star Latitude (height), Galactic Core Transit (hour) |
| The star map image | Re-run `AgentScripts/DayNight/StarMap/generate_starmap.py` (README there), then `SkyboxPlusSetup.StarMap(true, intensity)` |
| When the Skybox Plus sky gets dark | `EnvironmentDirector` → Skybox Plus Dusk Elevation / Morning Sink / Evening Sink |
| Unlit effects at night | Each look's Unlit Tint |
| Lamps, windows | `TimeOfDayLight` / `TimeOfDayEmission` hours and colours |
| The player's light at night | `Data/Player/PlayerVisionTuning.asset`; a lantern's share: `Content/Items/Lantern/Lantern.asset` |
| Ambience | `TimeOfDay_Ambience` curves and calls |

## Setup scripts

`AgentScripts/DayNight/` (run with `unity command run_script`):

- `DayNightSetup.Assets(reset)` creates the data assets. **`reset = true` overwrites the looks, grades, profile keys and ambience with the script's values.** Use `false` to keep hand tuning and only add what's missing.
- `DayNightSetup.Scene()` wires the world map (director, grade volumes, sky, reactors, door light) and saves the scene. Its scope step adds or updates the scope's `WorldTimeInstaller` (clock) and `EnvironmentInstaller` (director + ambience). Re-run it after `Atmosphere.Particles()` rebuilds the atmosphere groups.
- `DayNightSetup.Hud()` rebuilds `SafeFrame/TopRight/Clock` in `HUD.prefab`.
- `Prof.Start()` / `Prof.StartGpu()`, `Prof.Stop()`, `Prof.Window(from, to)`, `Prof.Compare(aFrom, aTo, bFrom, bTo, thread, top)`, `Prof.Spikes(...)`: Play-mode profiler captures (median CPU / GPU frame time, GC per frame, markers that grew between two frame ranges). Measured: a look transition costs about 0.3 ms main thread + 0.3 ms GPU over a hold (1–4 fps in the editor); the director writes the light, RenderSettings and volumes only when a value changes, so holds cost nothing.
- `DayNightSetup.RestoreHudOverrides(path)` sets the open scene's HUD instance overrides back to those of another copy of the scene (for example `git show HEAD:<scene>` saved to a temp file). Editor layout passes write layout-driven values onto the instance, which would otherwise show up as noise in the scene diff.
- `SkyboxPlusSetup.Create(preset)` builds `SkyboxPlus_Sky.mat` from a Skybox Plus preset (e.g. `"Clouds-B1"`) with the project's star, moon and sky values; **it overwrites hand tuning**. `Use(bool)` switches the open scene between Skybox Plus and the Time Of Day sky and saves it. `StarMap(on, intensity)` imports `StarMap.png` as a cubemap and turns the star map on or off. Play mode: `Try(preset)` shows another vendor preset (runtime copy, nothing saved), `Set(property, value)` tweaks the running copy.
- `Review.cs`: Play-mode helpers (`SetHour`, `Freeze`, `View`, `State`) for screenshots. Unity stops ticking Play mode while the editor is in the background: run `Application.runInBackground = true` (eval) first.

`AgentScripts/Vision/` (player light and lantern):

- `VisionSetup.Assets()` creates `PlayerVisionTuning` (linked from `PlayerTuning`), the `Lantern` item and its pickup prefab, keeping existing values. `VisionSetup.Defaults()` resets `PlayerVisionTuning` to the code defaults. `VisionSetup.Player()` adds / refreshes `VisionLight` on `Player.prefab`. `VisionSetup.Place(x, y, z)` drops a lantern pickup into the open scene (not saved).
- `VisionReview.cs`: Play-mode helpers (`State`, `Wear(bool)`, `SpawnPickup(distance)`, `PickUp()`, `Give(item)`, `Manual(...)` to try light values by hand, `Wait(seconds)`). `RunTests.Fixtures("A,B")` runs EditMode fixtures by reflection when a dirty scene would block the Test Runner. `shot.sh <out.png>` captures the game view to any path (`capture_game_view --save_path` writes under `Assets/` only); `run.sh <file> <entry> [args]` runs an entry and prints its result.
- `LightSweep.Run("0,6,12,...", settleSeconds)` (Play mode) steps the clock through the hours and prints the shown look hour, sun/moon light, ambient, active grades and the player light. An async entry: pass both `--timeout_ms` and the CLI's `--timeout`.

## Known limits

- The painted sky still has its own sun glow, painted in one place (rotation 17); the sun disc moves on its own. A fully procedural stylized sky would replace the painting.
- Skybox Plus ignores the looks' sky values (tint, exposure, night gradient, horizon glow, sun disc, stars, moon glow): its darkness follows its sun, not a look's Night Sky Blend, so a look can't make one hour's sky darker on its own. Its GPU cost against the Time Of Day sky is not measured yet (sky pixels only: a cloud cubemap and up to six noise samples per pixel for the star band).
- Reactors act in Play mode only; the edit preview shows the light, ambient, fog, sky and grades.
- Underwater fog (Stylized Water 3 areas) and the water materials are not driven by the time of day yet.
- Raygeas `Tree_Foliage` leaves (pines, bushes, sakura) add a fake subsurface glow (`_SSS_Color`) through Emission. It follows only the light's direction, not its intensity, so those trees stay bright green at night. Not driven by the time of day yet.
- Not built (GDD rules, Features): dusk warning, sleeping (use `SkipTo`), weather, night enemies.
