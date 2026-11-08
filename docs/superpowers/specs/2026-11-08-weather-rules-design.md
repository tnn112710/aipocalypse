# Weather, phase B: simple weather rules — design

Status: agreed in conversation (user, 2026-11-08) and built the same day. Follows phase A (`2026-11-08-weather-design.md`). How it works now: [WEATHER.md](../../technicals/WEATHER.md) → Rules. Numbers are starting values in `WeatherRules.asset`, not design canon.

## 1. Goal

The weather changes on its own. Most of the time it's clear, sunny or partly cloudy, sometimes cloudy and windy, and now and then it rains. A weather lasts a few hours and changes smoothly: no sunny-then-sudden-storm. Blood Moon nights and eclipse days have their own odds (no rain), and code or the console can hold or force any combination (Blood Moon + fog, eclipse + rain).

## 2. Decisions

| Topic | Decision |
| --- | --- |
| COZY's forecast | Not used. Its timer runs on `Time.deltaTime × TimeMovementSpeed`, not on `WorldClock`, so sleeps, jumps, freezes and speed changes break it. Its rolls aren't seeded, its save is broken, its chance curves need the Climate module, and only `CozyBridge` may touch COZY. We borrow its ideas: chance per weather, duration range, follow rules |
| Chances | Weights, never percentages (chance = weight / total), as AGENTS.md asks; picked with `RandomSystem` (`WeightedPick`, stream `"weather"`) |
| Periods | Four weight columns: Day, Night (18:00–06:00), Blood Moon (a night), Eclipse (a daytime). Weight 0 = never in that period, not even on the way to another weather |
| Smooth changes | A ladder walk: Clear ↔ Partly Cloudy ↔ Cloudy ↔ Overcast ↔ Light ↔ Medium ↔ Heavy Rain, each step 0.5–1 h; Fog beside Clear, Partly Cloudy, Cloudy |
| Durations | Per weather, weights over 1 / 2 / 4 / 6 / 8 h (user: "like 50% of 2 hours, 20% of 4..."), ± 0.5 h. Storms short, clear skies long |
| Period edges | A weather never runs into a period that forbids it: it ends early so the walk away finishes in time |
| Wind | Per weather wind weights (Calm / Breezy / Windy / Gale), rolled when it starts, moving one level per step |
| Blood Moon / eclipse | Not in the game yet: days are marked (`MarkBloodMoon` / `MarkEclipse`; console for now). The Blood Moon system calls them ahead of time later |
| Overrides | `Hold` until `Release` (console `weather.set` / `weather.auto`), `Force` from now or a later hour, walked to on time, rules resume after |
| Time | Plan in world-clock hours, 24 h ahead (`weather.forecast`); cut, don't blend, after a jump or time skip |
| Inspector | Grids per table with shares, stripes and averages, plus a seeded test of the real planner (user: "make sure the inspector is nice to read and adjust") |
| Out of scope | Gameplay effects, a player-facing forecast (drone), seasons and regions, saving the plan |

## 3. Pieces

- `WeatherRules` (SO) + `WeatherRulesEditor` (grids, test).
- `WeatherPlanner` (plain C#: the plan) → `WeatherSchedule` (scene entry point: clock → `WeatherService`).
- `WeatherCommands`: `auto`, `forecast`, `force`, `bloodmoon`, `eclipse`; `set` / `wind` hold.
- `WeatherSetup.Rules()` builds the asset; `WeatherScheduleTests`.

## 4. Changes during the build

- The real planner in the inspector's test showed 0.1% Overcast on Blood Moon nights: a rain spell that ended on its own shortly before the night left no time to clear. Clipping now books the walk out after a weather, not only inside it.
- When a forced weather is one the rules never pick there (heavy rain forced on a day with heavy rain at 0), the walk waits one step short until it starts instead of showing it early.
- A restart (first frame, clock going back) keeps the current weather for a rolled duration instead of re-rolling at once.
