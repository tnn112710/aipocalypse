# Raids

Raid nights: when they run, what spawns and from where, how they end, and what a death costs in time. Code in `_Core/RaidSystem/` (namespace `Aipocalypse.Core.RaidSystem`), data in `Data/Raids/`, script map in [CODE_STRUCTURE.md](CODE_STRUCTURE.md). Design: [docs/superpowers/specs/2026-10-31-raid-night-design.md](../superpowers/specs/2026-10-31-raid-night-design.md). Enemy spawning, pooling and targeting belong to the enemy system ([ARCHITECTURE.md → AI](ARCHITECTURE.md), the `aipocalypse-enemy-ai` skill); the raid system only decides when, what and where.

## Rules today

- **Every night is a raid night** (`EveryNightSchedule`): the same plan, `Data/Raids/Night_Default.asset`. Quiet nights, a difficulty ladder and Blood Moons replace or wrap `IRaidSchedule` later.
- **Night N** runs from `RaidSettings.StartHour` (20:00) on day N to `DawnHour` (06:00) on day N + 1. The clock is never touched by a raid: the night passes at its normal speed.
- **Waves** spawn in order. Each wave lists entries (a spawn card and a count); an entry spawns in groups of the card's group size (goblins 2–3), `GroupInterval` seconds apart (4 s), from the wave's `SpawnZone`. Never more than `AliveCap` (12) raiders alive: a group waits, or spawns partly up to the cap. Nothing spawns while the player is dead.
- **Next wave** once the current one has fully spawned and either at most `NextWhenAliveAtMost` (2) raiders are alive and `MinBreather` (15 s) has passed, or `MaxWait` (90 s) after the wave started. Clearing a wave fast pulls the next one in.
- **End of a raid:**

| Reason | When | Raiders still alive |
| --- | --- | --- |
| `Cleared` | the last wave has fully spawned and none is alive | — (the rest of the night is free) |
| `Dawn` | the clock leaves the night (06:00, or a jump out of it) | despawn as soon as the player can't see them |
| `Defeat` | the player died during the raid (the death screen is black) | despawn at once |
| `Stopped` | `raid.stop` | despawn at once |

- A night that ended, for any reason, never restarts. Jumping the clock into a night that hasn't run starts it.
- **Banners** (`RaidBannerHud` in HUD.prefab, `Data/UI/RaidBannerStyle.asset`): "RAID INCOMING / Night N: hold the farm" with a gong (`Raid_Start`, Combat library) when a raid starts, "RAID REPELLED" with a victory jingle (`Raid_Victory`) when every wave is killed, "DAWN — The raiders retreat" (no sound) when dawn ends it. Nothing on a defeat (the death screen) or a console stop.
- **When the player dies** the raiders stop where they are and dance (the Melee subtree's celebrate branch) until the death screen turns black.
- **Death costs time** (`DeathTimeRule`, during the black part of the death screen): dying during a raid ends it (`Defeat`) and the player wakes at the next `DefeatWakeHour` (07:00); any other death moves the clock forward `HoursLostOnDeath` (1 h) and despawns raiders left over from a dawn withdrawal. Nothing is lost yet (losses come with the inventory).
- **Arrival cue:** the first group of each card in a wave plays the card's `ArrivalCue` (goblins: `Raid_Horn_Goblin`, a war horn heard to 220 m) at its spawn point, so the player hears where a wave comes from. Each kind sounds once per wave.

## Where raiders spawn

`Raid/Spawn Zone (Front)` in WorldMap sits at the home gate (274.4, 294.2), facing out (yaw 50). Its arc runs 60–80 m from the gate, ±45° around that facing (90° in front of the base). The arc is drawn whenever Gizmos are on (Scene view, or the Game view's Gizmos toggle in Play): a filled orange band with its id. The river crosses much of it; points on water have no navmesh and are skipped. `RaidSpawner` asks the `SpawnService` for each group in the zone with the raid's `SpawnRules` (`RaidSettings.Spawning`); its `SpawnPointPicker` then, per group ([SPAWNING.md](SPAWNING.md)):

1. Samples `SpawnRules.Candidates` (8) points on the arc, finds the ground under each (`GroundMask`), snaps them to the navmesh (`SnapDistance` 2 m) and keeps those with a path to the gate (`INavmeshQuery.IsReachable`, answered by `EnemyNavigation`).
2. `SpawnPointScorer` drops points closer than `MinPlayerDistance` (30 m) to the player, then prefers points the player can't see (the whole group's footprint, and not a spot just seen, `SeenLock` 3 s): outside the view cone (90° plus `ViewMargin`), farther than `VisibleDistance` (90 m), or behind terrain and props (`SightBlockers` line of sight from the camera to the point's chest height).
3. With no hidden point it tries again every `RetryInterval` (0.25 s); after `MaxHiddenWait` (5 s) it takes the visible point farthest from the view centre (night darkness hides most of it).

The group then spreads over a 3–5 m radius round the point (`SpawnSystemSettings.GroupRadius`, wider for big groups to keep `GroupSpacing`), each member placed on the navmesh again, facing the gate, and arrives within the `SpawnSystemSettings.SpawnBudgetMs` (2 ms) a frame: a prewarmed goblin wakes in about 1.1 ms, so about two a frame. The card's arrival cue plays at the group's spot once it's found. Each member first appears: its kind's appear clip (goblins roar) and `AppearTime` + up to `AppearJitter` seconds standing still (`EnemyDefinition`; goblins 3 + 0–0.8 s). Members still to come count as alive, so a raid can't end Cleared before they appear, and hunts the player through the open gate. Raiders always know their target (the player for now; see the enemy-ai skill → Targeting).

## Testing a raid

| Command | Does |
| --- | --- |
| `raid.start` | Start tonight's raid now, even by day (a manual raid ignores the night window) |
| `raid.next` | Start the next wave now |
| `raid.stop` | End the raid, despawn its raiders |
| `raid.status` | Night, wave, spawned / total, alive, running or how the last raid ended |
| `clock.set 19.95` | Reach the start hour the normal way |
| `cheat.god`, `cheat.notarget`, `cheat.killall`, `cheat.autopilot` | Survive, be ignored, clear the field, let the player fight by itself ([CONSOLE.md](CONSOLE.md)) |

**Soak test** (whole night, no hands): enter Play in WorldMap, then `unity command script run --file AgentScripts/Raids/RaidSoak.cs --entry RaidSoak.Run --args '[600]' --timeout 660`. It turns on god mode, infinite ammo and the autopilot, sets the clock to just before 20:00, plays until the raid ends (or the time runs out) and reports how it ended, waves reached, spawned and killed, the most alive at once, the player's lowest health, frame times and any console errors.

`WorldMapSmokeTests.WorldMap_RaidSpawnsRaidersAtItsStartHour` checks that a raid starts and spawns; `RaidDirectorTests`, `SpawnPointScorerTests` and `DeathTimeRuleTests` cover the rules in EditMode.

## Data

| Asset | Holds |
| --- | --- |
| `Data/Raids/RaidSettings.asset` | Start, dawn and wake hours, hours lost on death, alive cap, the nightly plan, spawn-point rules |
| `Data/Raids/Night_Default.asset` (`RaidPlan`) | Three waves of goblins: 5, 6 and 7 (18 a night), each from `Front` |
| `Content/Enemies/Goblin/Goblin_Card.asset` (`EnemySpawnCard`) | The six goblin looks, groups of 2–3, the war horn |

Settings and plans are read-only at runtime, like every definition asset.

## Extending

- **Another enemy kind in a raid:** its card (prefab variants, group size, its own `ArrivalCue`), listed in `EnemySystemSettings.Cards`, and an entry in a wave. Mixed waves announce each kind once.
- **Quiet nights, ladder, Blood Moons:** a new `IRaidSchedule` (registered in `RaidInstaller` instead of `EveryNightSchedule`) returns a plan per night, or null for a quiet night. Difficulty scaling goes on spawned enemies as `EnemyStats` modifiers.
- **Other routes** (river crossings, inside the meadow): more `SpawnZone`s with their own ids; waves name the one they use.
- **The closed fence:** the home gate starts shut. Raiders with no path to the player break the weakest of the 5 nearest standing segments and come through the gap, or through the gate if the player opens it ([DEFENSE.md](DEFENSE.md)). `Raid/Spawn Zone (Front)` has `OriginOffset` 3 m, so the point spawn points must reach lies outside the closed gate.
- **Other targets** (crops, animals): they register as `ITargetable` in `TargetRegistry` with their `TargetKind`; enemies weigh them through `EnemyDefinition.Targets`. The raid system doesn't change.

## Wiring

`RaidInstaller` (a `SceneInstaller` on the scene's `GameLifetimeScope`) needs a ready `WorldTimeInstaller`, `SpawnInstaller` and `EnemyInstaller` on the same scope and reports a `Problem` otherwise. It registers `RaidSettings`, `EveryNightSchedule` (`IRaidSchedule`), `RaidSpawner` (`IRaidSpawner`, ticked for the arrival cues; the dawn withdrawal is `SpawnService`'s `DespawnMode.WhenUnseen`), `RaidDirector` (ticked), `DeathTimeRule`, and `RaidCommands` in dev builds. Its `SpawnZone` list is collected automatically on scene save and entering Play. `RaidDirector` publishes `RaidStarted`, `RaidWaveStarted` and `RaidEnded` (`_Common/Events/RaidEvents.cs`; no subscribers yet: a raid HUD, music and the morning report come later). Setup script: `AgentScripts/Raids/RaidNightSetup.cs` (`Audio`, `EnemyData`, `Raid`).
