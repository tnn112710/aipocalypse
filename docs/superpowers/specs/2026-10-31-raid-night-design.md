# Raid night: goblin raids, enemy spawning, player death — design

Status: draft for review (2026-10-31). Decisions come from the user's answers in the planning conversation; nothing here is taken from `docs/gdd/` without their confirmation. Placeholder numbers are tunable assets, not design canon.

## 1. Goal

Every night at 20:00 a fixed raid of 15–20 melee goblins arrives in waves from 60–80 m in front of the home base and hunts the player through the open gate. Goblin swings hurt and can kill the player. The structure must grow without rewrites into: other enemy kinds (spear throwers), other targets (closed fence, crops, animals), quiet nights, a difficulty ladder, Blood Moons.

## 2. Decisions

| Topic | Decision |
| --- | --- |
| What raiders go for | Hunt the player wherever they are. Targeting is a service over target kinds, so fences, crops and animals become targets later. Only the player registers now |
| Raid size | One fixed plan every night: 18 goblins in 3 waves (5 / 6 / 7), at most 12 alive |
| Raid start | 20:00 (`RaidSettings.StartHour`) |
| Next wave | When the previous waves are down to ≤ 2 alive, or after 90 s, whichever first, with at least 15 s of breather (per wave, in data) |
| End of raid | **Cleared** when every raider is dead (the rest of the night is free); **Dawn** at 06:00 (leftovers despawn out of sight); **Defeat** when the player dies |
| Clock | Untouched by raids: 45 s per game hour, night 18:00–06:00 = 9 min |
| Death during a raid | Raid ends, raiders despawn during the death screen, the clock jumps to the next 07:00. Nothing is lost (losses come with the inventory) |
| Other deaths (fall, day) | Respawn at the gate as today, plus the clock moves forward 1 hour (new: today's respawn doesn't move the clock) |
| Quiet nights, Blood Moon, ladder, difficulty scaling | Out of scope; the schedule interface is the seam |
| Spawn zone | Arc 60–80 m from the gate, ±120° around its facing |
| Swarm fairness | At most 3 enemies swing at the player at once; the rest hold a few metres out |
| Arrival cue | One cue per enemy kind (its spawn card), played once per wave for each kind in it, 3D at the wave's first spawn point. Goblins: war horn |
| Enemy sounds | Per-kind sound set (attack, swing, impact, hurt, death, shout, footsteps), any attacker gets sounds by data |
| Hurt feedback | Directional red edge flash, small view kick, pain grunt |
| Test aids | Infinite ammo crate in the yard ("[E] Take ammo"); `cheat.*`, `raid.*`, `enemy.*` console commands |

## 3. Architecture

Two scene systems. EnemySystem gains generic spawning, pooling, targeting and attack slots (anything may spawn enemies: raids, Blood Moons, outer-zone wanderers, the console). A new RaidSystem decides when, what and where, and owns the death-costs-time rule. Trees only execute.

```text
RaidSystem (new, scene: RaidInstaller)                     EnemySystem (scene: EnemyInstaller)
  RaidDirector  ── timeline, pure logic, Advance(dt)          EnemySpawner ── card → variant → pool
     │ IRaidSchedule (EveryNightSchedule)                      EnemyPool    ── per-prefab stacks, prewarm
     │ IRaidSpawner  (RaidSpawner: picker + EnemySpawner       EnemyNavigation ── snap / reachable (injected AstarPath)
     │                + arrival cues)                          TargetRegistry + TargetSelector
  SpawnZone (scene component) + SpawnPointPicker              AttackSlots
  DeathTimeRule ── PlayerRespawning → raid defeat / +1 h      EnemySoundManager (footsteps tick)
  RaidCommands (dev)                                          EnemyCommands, EnemyCheatCommands (dev)
        │ reads WorldClock (WorldTimeSystem), PlayerRegistry; publishes RaidStarted / RaidWaveStarted / RaidEnded
```

Dependencies point one way: RaidSystem → EnemySystem, WorldTimeSystem, PlayerSystem. EnemySystem never knows about raids.

## 4. Components

### 4.1 Goblin hits (the player can die)

- `EnemyStat.AttackDamage` (appended). Every definition gets a base value (Goblin 15, WanderCapsule 0). Read through `EnemyStats`, so a later difficulty scales it with a modifier.
- `MeleeSettings` adds `HitTime` (s into the swing the blow lands, 0.5), `HitReach` (m, 2.0) and `HitArc` (degrees in front, 90). `DetectRadius` becomes `EngageRadius` (5 m: switches the fighting stance and the chase sprint; `[FormerlySerializedAs]`); `LoseRadius` is removed (hunting doesn't give up by distance). New: `ApproachSpeedMultiplier` (1.6, while far) next to `ChaseSpeedMultiplier` (2.5, inside `EngageRadius`), `RetargetInterval` (1 s), `HoldDistance` (m from the target where an enemy without an attack slot waits, 3.5).
- `MeleeHit.Lands(attacker, forward, target, reach, arc)`: pure rule, unit-tested.
- `MeleeAttack` task: on swing start plays the kind's attack and swing sounds; at `HitTime`, if `MeleeHit.Lands`, calls `EnemyBrain.Strike(target)` → `DamageService.Deal(target, DamageInfo(AttackDamage, Melee, point, source: the goblin))` and plays the impact sound. A swing aborted or an enemy killed before `HitTime` deals nothing. Stepping back during the wind-up dodges.
- Timing comes from data, not animation events (vendor clips, three random attack clips). Trees think at 15 Hz, so a hit lands within about 67 ms of `HitTime`.
- `EnemyBrain` injects the root `DamageService` and `AudioService`.

### 4.2 Targeting

- `ITargetable : IDamageable` in `_Common/Entities`: `Transform`, `TargetKind Kind`, `bool IsTargetable` (alive, not hidden by a cheat). `TargetKind` enum: `Player` now; `Fence`, `Crop`, `Animal` appended when they exist. `IPlayerTarget : ITargetable`.
- `TargetRegistry` (EnemySystem, scene): the player comes from `PlayerRegistry.Current` (no player code registers); `Add/Remove(ITargetable)` for future targets.
- `TargetSelector.Choose(EnemyBrain)`: plain C#; score = kind weight / distance, skipping untargetable ones and kinds with no weight. Weights: `EnemyDefinition.Targets` (`Dictionary<TargetKind, float>`, goblin: Player 1). Seam for later: "no path to the player because the fence is closed → the blocking fence segment".
- `EnemyBrain.Target` becomes `ITargetable`. The fighting stance follows `Engaged` (target within `EngageRadius`), not `Target`, so hunters run in from 70 m in their normal gait.
- `cheat.notarget`: the player reports `IsTargetable = false`; hunters drop it and wander.

### 4.3 Attack slots

`AttackSlots` (EnemySystem, scene): per target, how many enemies may swing at once (`EnemySystemSettings.AttackersPerTarget`, `Dictionary<TargetKind, int>`: Player 3; a missing kind is unlimited). An enemy takes a slot when it comes within `HoldDistance` (`ChaseTarget`) and keeps it while it stays close; it frees it when it backs off past `HoldDistance` (with slack), switches target, dies or despawns. Without a free slot it waits at `HoldDistance` facing the target. Unit-tested.

### 4.4 Goblin tree

`AgentScripts/Enemies/Melee.tree.txt`, rebuilt with TreeKit (keeps the GUID). `PlayerInReach` is replaced by `ChooseTarget` (only `Melee.asset` uses it).

```text
Repeater "Repeat forever"
  Selector "Hunt, else wander"
    Sequence AbortType=Both "Hunt (aborts both ways)"
      ChooseTarget "Target?"       # asks TargetSelector every RetargetInterval; sets brain.Target
      ChaseTarget "Chase"          # approach pace far, sprint inside EngageRadius; holds without an attack slot
      MeleeAttack "Swing"          # slot, swing, hit at HitTime
    Sequence "Wander"              # no target (player dead, notarget): wander round home
      ChooseWanderDestination
      WalkToWanderDestination
      PauseWander
```

`ChaseTarget` repaths when the target moved more than `max(RepathDistance, 10% of the distance)`, so far hunters don't repath every think.

### 4.5 Spawning and pooling (EnemySystem)

- `EnemySpawnCard` (ScriptableObject, `Content/Enemies/Goblin/Goblin_Card.asset`): weighted prefab variants (the six goblin looks), group size (2–3), `ArrivalCue` (`SoundID`). A spear thrower later is a new card and prefab.
- `EnemySystemSettings.Cards`: the cards this scene can spawn (console lookup by name, pool prewarm).
- `EnemyPool`: one stack per prefab under an `Enemies` root; instances made with `resolver.Instantiate` (injected before `Awake`). Get: position and rotate before `SetActive(true)`, then teleport the `FollowerEntity`. An enemy returns when it disables itself (after the corpse sinks) or is despawned. `Prewarm(card, count)` spreads instantiation over frames at scene start (about 2 per frame) so the first wave doesn't hitch.
- `EnemySpawner`: the only code path that spawns enemies. `Spawn(card, position, rotation)`, `SpawnGroup(card, center, count, facing)` (members on a 1.5 m ring snapped to the navmesh), `Despawn(brain)`.
- `EnemyNavigation`: navmesh queries on the injected `AstarPath` (snap a point within a distance, path possible between two points). Today's `EnemyMover.TryFindDestination` keeps working as is.

### 4.6 Raid (RaidSystem)

**Data** (`Data/Raids/`):
- `RaidSettings`: `StartHour` 20, `DawnHour` 6, `DefeatWakeHour` 7, `HoursLostOnDeath` 1, `AliveCap` 12, `Plan`.
- `RaidPlan`: a list of `RaidWave`. A wave has `Entries` (card + count; one goblin entry now, mixed waves later), `GroupInterval` (s between groups, 4), `Zone` (zone id, "Front"), and how the next wave starts: `NextWhenAliveAtMost` 2, `MaxWait` 90 s, `MinBreather` 15 s.
- Night 1 placeholder: waves of 5, 6 and 7 goblins.

**`IRaidSchedule`** → `EveryNightSchedule` returns `RaidSettings.Plan` for any night. Quiet nights, the ladder and Blood Moons replace or wrap it later.

**`RaidDirector`** (`ITickable`, logic in `Advance(dt)`; no Unity objects, so EditMode tests drive it with a real `WorldClock` and a fake `IRaidSpawner`):
- Night N runs from `StartHour` on day N to `DawnHour` on day N+1. Each tick it derives what should be happening from `WorldClock.Now`, so cheats, `clock.set` and jumps behave: inside a night not yet run → start it; outside the window while running → end it (Dawn). A night that ended (any reason) never restarts.
- Spawning: wave by wave; each entry spawns in groups (card group size) every `GroupInterval` at a freshly picked point, never above `AliveCap` (a group waits), never while the player is dead.
- Next wave per the wave's rule once the current one has fully spawned. After the last wave, the raid ends Cleared when no raider is alive.
- Tracks its raiders; dead or disabled ones drop out each tick.
- Publishes `RaidStarted(night)`, `RaidWaveStarted(night, wave, count)`, `RaidEnded(night, reason)` (`_Common/Events/RaidEvents.cs`). No subscribers yet (HUD, music later).

**`RaidSpawner : IRaidSpawner`**: picks a point (`SpawnPointPicker`), spawns the group (`EnemySpawner`), plays each card's arrival cue once per wave at its first group's point (`AudioService.PlayAt`), despawns raiders on request (Dawn: those out of view now, the rest as soon as they leave view; Defeat: all, the screen is black).

**`SpawnZone`** (scene component under `Raid` in WorldMap, at the gate, facing the gate's outward direction): `Id`, `MinRadius` 60, `MaxRadius` 80, `HalfAngle` 120, gizmo arc. Referenced by `RaidInstaller`.

**`SpawnPointPicker`**: samples 8 points in the zone, snaps each to the navmesh (≤ 2 m), keeps those with a path to the gate, rejects those within 30 m of the player. Scoring (`SpawnPointScorer`, pure, unit-tested): hidden points first (outside the player's view cone, or line of sight blocked by terrain and props: one raycast per candidate in the cone within 90 m); among hidden ones, random. If none is hidden it retries next tick, and after 5 s takes the visible point farthest from the view centre. The player's view comes from a new `IPlayer.View` (the camera transform).

**`DeathTimeRule`**: handles `PlayerRespawning` (the black part of the death screen): a raid running → `RaidDirector.EndRaid(Defeat)` and `WorldClock.JumpTo(DefeatWakeHour)` (21:00 day 5 → 07:00 day 6; 02:00 day 6 → 07:00 day 6); otherwise the clock moves forward `HoursLostOnDeath`. `TimeJumped` lets the environment resync while the screen is black.

### 4.7 Player hurt feedback

- `PlayerLife` raises `Hurt(DamageInfo, applied)` for non-lethal hits (death keeps its own sequence and groans). `Player` plays `Player_Hurt` (rate-limited) and a short view kick.
- `HurtFeedbackHud : PlayerHudView`: on `DamageDealt` to the active player, flashes the screen edge toward the hit's source (front/back/left/right weights from the source direction against the view yaw); strength scales with damage. Edge gradient generated in code (no art asset). Tuning in a `HurtFeedbackStyle` asset.
- `cheat.god`: `PlayerLife.Invulnerable` (hits apply nothing, no feedback).

### 4.8 Enemy sounds

- `EnemySoundSet` (ScriptableObject, referenced from `EnemyDefinition.Sounds`): `Attack`, `Swing`, `Impact`, `Hurt`, `Death`, `Shout`, `Footstep`, stride length (m), footstep range (m, 20), per-enemy hurt cooldown (s, 0.3; a shotgun volley yields one cry).
- Hooks: `MeleeAttack` (attack, swing, impact), `EnemyBody` (hurt or death), `ChaseTarget` (shout when first engaging, per-enemy cooldown).
- `EnemySoundManager` (central tick, no per-enemy `Update`): accumulates distance moved per registered enemy within footstep range of the player and plays a step per stride. Voice counts are limited per BroAudio entity.
- New BroAudio entries (approved): `Goblin_Attack`, `Goblin_Hurt`, `Goblin_Death`, `Goblin_Shout`, `Enemy_Step_Grass`, `Enemy_Swing`, `Enemy_Impact`, `Raid_Horn_Goblin`, `Player_Hurt`, `AmmoCrate_Take`, in an `Enemies` library plus the existing `Player` library. Sources: Gamemaster Audio Pro `Voice/GoblinFairy` (attack, hurt_pain, death, battle_shout; pitched down slightly), `Comedy_Cartoon/battle_viking_horn_call_far_*`, `Footsteps/footstep_grass_walk/run_*`, `Voice/Human Male A/voice_male_grunt_pain_*`; swing and impact reuse the Malbers clips already in use; crate from PlaceHolder Inc Items & Collectibles.

### 4.9 Test aids

- **Ammo crate** (`Content/Props/AmmoCrate/AmmoCrate.prefab`, WeaponSystem `AmmoCrate : IInteractable`): prompt "[E] Take ammo"; tops every ammo type the hotbar guns use up to the starting reserve (`AmmoConfig`); unlimited uses; `AmmoCrate_Take` sound. Model: Animpic Mega Weapons Kit `SM_Ammo_Box_Wood_Open`. Placed in the WorldMap yard next to the house.
- **Console** (dev builds only, CONSOLE.md updated):
  - `cheat.*` (PlayerSystem, moved from `player.*`): `cheat.kill`, `cheat.revive`, `cheat.hp [amount]`, `cheat.stamina`, `cheat.ammo [amount]`, `cheat.infammo [on/off]`; new `cheat.god [on/off]`, `cheat.notarget [on/off]`. `cheat.killall` (EnemySystem, same group): kills every enemy through `DamageService`. `player.pos` stays.
  - `raid.start` (tonight's raid now), `raid.stop` (end it, despawn raiders), `raid.next` (next wave now), `raid.status`.
  - `enemy.spawn <card> [count]` (in front of the player, out of the crosshair), `enemy.clear` (despawn all), `enemy.count`.

## 5. Scene and asset changes

- WorldMap: `RaidInstaller` on the `GameLifetimeScope` (strict scope: required there), `Raid/Spawn Zone (Front)` at the gate, the ammo crate in the yard. Nothing else in the scene changes.
- New data: `Data/Raids/RaidSettings.asset`, `Data/Raids/Night_Default.asset`, `Content/Enemies/Goblin/Goblin_Card.asset`, `Goblin_Sounds.asset`, `Data/UI/HurtFeedbackStyle.asset`, BroAudio library entries.
- Updated data: `Goblin.asset` (AttackDamage, Targets, Sounds), `Goblin_Melee.asset` (new fields), `WanderCapsule.asset` (AttackDamage 0), `EnemySystemSettings.asset` (Cards, AttackersPerTarget).

## 6. Testing

- EditMode: `MeleeHit` rule; `AttackSlots`; `TargetSelector`; `SpawnPointScorer`; `RaidDirector` timeline with a real `WorldClock` and a fake spawner (not before 20:00; wave rule; alive cap; Cleared stays ended that night; jump into and out of the window; Dawn; Defeat; next night restarts); `DeathTimeRule` (raid death → next 07:00, other death → +1 h); data checks (every definition has every stat; card variants have a brain and a definition; plan waves have cards; goblin has sounds); console registration of the new and moved commands.
- PlayMode smoke (`WorldMapSmokeTests`): it asserts placed enemies today and fails since the goblins were removed. It will jump the clock to 20:00 and assert raiders spawned. If the CLI reports 0/0, verify by entering play and probing with `script run`.
- In play: the full night (spawn points, horn direction, hunting through the gate, slots, hits, hurt feedback, death → 07:00, clear → free night, dawn despawn), the crate, every console command.

## 7. Docs updated in the same change

New `docs/technicals/RAIDS.md` (listed in AGENTS.md); ARCHITECTURE.md → AI (spawning, pooling, targeting, slots) and events; CODE_STRUCTURE.md (every new script); CONSOLE.md; ASSETS.md (Gamemaster Audio Pro: now in use, why a Could pack; Animpic Mega Weapons Kit: test crate); ROADMAP.md (`Proto_Enemies`: attack damage, code spawning, pooling done); the `aipocalypse-enemy-ai` skill (§1 pooled enemies, §4 spawning and targeting, §7 not-built list) and the `aipocalypse-architecture` events table.

## 8. Risks

- `FollowerEntity` reuse: teleport after enabling (unity-astar → Pooling); verify no stale paths on the first frame.
- Gate funnel: up to 12 hunters squeezing through one gate with RVO; watch for jams.
- Goblin voice timbre ("fairy" set) may need different clips; swapping is data only.
- 15 Hz hit timing and one `HitTime` for three attack clips; tune by eye, per-clip times later if needed.
- The 300-enemy perf gate is still unmeasured; the spawner makes it a one-liner (`enemy.spawn goblin 300`), not part of this work.

## 9. Out of scope (later)

Quiet nights and the shuffle bag, the difficulty ladder and stat scaling, Blood Moon and eclipse, the midnight retreat, losses on death, closing the fence and fence targeting, crops and animals as targets, spear throwers, raid HUD and morning report, enemy ammo drops, crouch, `cheat.noclip`.
