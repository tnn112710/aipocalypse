# Testing approach

How we test the game as it grows, so we never have to play from a fresh save to reach content in the middle of a run. This is the direction to follow. Items marked *planned* don't exist yet. Build them when the need shows up, not before.

## Layers (many cheap tests, few expensive ones)

| Layer | What it checks | Where | When it runs |
| --- | --- | --- | --- |
| Rule tests | Pure C# rules (damage, stamina, clock, spawn tables) and data / prefab wiring | `Tests/EditMode/` | Every change (`test run --mode editor`) |
| Feature tests | One behaviour in a tiny setup: one system plus what it needs, not the whole map | `Tests/EditMode/`, or `Tests/PlayMode/` when it needs play | Every change |
| Smoke tests | The world map boots and the core loop works (`WorldMapSmokeTests`) | `Tests/PlayMode/` | Before a commit that touches the loop, and before every build |
| Soak runs *(planned)* | A bot plays many days and raids unattended. Watches errors, frame time, enemy count, memory | `AgentScripts/` play-mode script | Overnight, before milestones |
| Human playthroughs | Feel, pacing, fun, things nothing automated sees | Builds | Milestones, split by scenario (see below) |

Push every check down to the cheapest layer that can catch it. A bug found in a soak run or a playthrough should, where possible, become a rule or feature test.

## Start from a state, not from the beginning

Mid-game for us means **day, threat level, unlocks, inventory, base (fences, animals) and weather / Blood Moon**. Reach it directly:

- **Scenario presets *(planned)*.** Named recipes such as `day10_bloodmoon` or `late_full_armory`, stored in a ScriptableObject and applied with one console command (`scenario <name>`). A recipe only calls existing services: the same ones behind `clock.*`, `cheat.give`, `fence.upgrade`, `weather.bloodmoon` and `stat.set`. Tests, soak runs and humans all start from these.
- **Prefer recipes to hand-kept saves.** A recipe written in code survives save-format changes. A save file made by hand breaks on the next version bump.
- **Jump forward, not backward.** A scenario builds state from a new game. Never rewind a running game into an earlier state: that leaves stale state behind.
- Each new mechanic that has a progression state (unlocks, tiers, counters) should be settable from a scenario. Treat this like adding the mechanic to the save.

## Save files as test inputs *(planned)*

- Keep a few real saves under `Tests/` (day 1, day 7, after a death, full inventory, a base with every fence tier).
- An EditMode test loads each one and checks it loads and holds what it should.
- When a save-version bump breaks them, that's the signal to write a migration, or to consciously re-record them while saves are still allowed to break ([SAVE.md](SAVE.md#known-gaps)).

## Soak bot *(planned)*

A play-mode script that does the following:

1. Loads a scenario.
2. Turns on `cheat.autopilot` and `app.background on`.
3. Fast-forwards through N days and raids (`clock.skip`, `raid.*`).
4. Writes a report: errors and exceptions, frame-time spikes, alive enemy and pooled counts over time, managed memory, and whether each raid ended.

It catches leaks, pooling bugs, stuck raids and slow drift that a 5-minute test never sees. Reuse `AgentScripts/Perf/PerfSuite.cs` for the measuring.

## Smoke tests per mechanic

One short play-mode test per core mechanic, each starting from a scenario, for example:

- Sleep until morning.
- A raid starts and ends.
- Animals hand over Tokens.
- A save round-trips in the middle of the day.

Keep each one under a few seconds of game time (use clock skips and speed-up), so the whole set stays cheap enough to run often.

## Later

- Nightly automation: build, then EditMode tests, smoke tests and a soak run, with a report. Do this once builds happen regularly ([BUILD.md](BUILD.md)).
- Human playtests split by scenario, so several people cover the early, mid and late game in one session instead of all replaying day 1.

## References

- Rare, *Sea of Thieves* automated gameplay tests: [GDC 2019](https://gdcvault.com/play/1026366/Automated-Testing-of-Gameplay-Features), [Unreal Fest 2019](https://www.unrealengine.com/events/unreal-fest-europe-2019/automated-testing-at-scale-in-sea-of-thieves), [adapting it to Minecraft](https://gdcvault.com/play/1027345/Lessons-Learned-in-Adapting-the)
- Ubisoft, *The Division* client bots: [GDC 2019](https://gdconf.com/news/see-how-ubisoft-tested-division-automated-players-gdc-2019); EA, *Battlefield V* test bots: [GDC 2019](https://gdcvault.com/play/1025905/AI-for-Testing-The-Development)
- Mighty Bear, [Automating your test states](https://medium.com/mighty-bear-games/automating-your-test-states-an-introduction-b407904376c4)
- Keywords Studios, [bots that play over scripted tests](https://www.gamedeveloper.com/article-library/be-smart-about-automation-the-future-is-ai-driven-game-testing-with-keywords-studios)
