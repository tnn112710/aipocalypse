# Service robots

Friendly robots who live at home and will open the shop, upgrades, storage and other menus. This first step (pass 1) builds the robots themselves, not the menus. Code in `_Core/RobotSystem/` (namespace `MB13.Aipocalypse.Core.RobotSystem`), content in `Content/Robots/`, data in `Data/Robots/`, script map in [CODE_STRUCTURE.md](CODE_STRUCTURE.md). Design and plan: `docs/superpowers/specs/2026-11-22-service-robots-design.md`, `docs/superpowers/plans/2026-11-22-service-robots-pass1.md`. Draft design context: [FARM_AND_ECONOMY.md](../gdd/FARM_AND_ECONOMY.md#rescued-robots-and-crafting).

## How it plays

1. **Two robots at home** (WorldMap): **Bolt** (copper-orange, placeholder Shop service) in the garden east of the house, **Pip** (mint, placeholder Upgrades service) inside the house across from the bed. About 1.3 m tall.
2. **Home life.** Mostly a robot stands at its spot and looks around: small turns, blinks, a passing happy or surprised look. Now and then it strolls a few metres and comes back; rarely it dances or laughs. Each robot rolls its own temper (stroll, wave and flourish chances, patience), so the two never move in step.
3. **Looking at one / E.** Look at a robot: **[E] Shop Bolt** (its service's verb and name). Looking or pressing E makes it stop, turn to the player and wave (at most once a minute) or talk, happy, for as long as the player keeps looking. E will open its menu later.
4. **The whistle (Q).** Aim at a robot up to **50 m** away and press Q: the player whistles (always; a short cooldown) and the robot reacts with a thumbs-up or a hop, trots over and stops about 1.8 m from the player on its own side, then waits facing them. If the player walks on while it comes, it aims at where they are now. It goes home with a shrug when its wait ends (about 20 s × its patience) or the player walks 8 m from where they stood when it arrived. Walls and props block the call.
   - **Never leaves the yard.** Whistled from outside, it comes to the inside of the fence nearest the player (1.2 m in) and waits there. The meeting point is planned inside the yard box with that margin, at the yard's height, snapped onto the navmesh and checked again, so it never lands past the fence.
   - **No way to the player** (Pip in the house: see Known gaps): it turns and waves from where it is.
5. **Reactions.** Walked into, it trots a little out of the way (never into the doorway), even while waiting by the player it was called to, so it never pins them. A gun pointed at its head within 6 m makes it worried: it faces the player and shakes its head (once per 8 s). Shot, it flinches (hit clip by side) and pulls an angry or sad face, but takes no damage, shows no hit marker and doesn't bleed. Raiders ignore robots.
6. **Night** (outside 06:00–20:00): each walks to its spot and stands by with sleepy eyes, its face screen glowing brighter. A whistle or E wakes it for a while.
7. **Beeps.** Placeholder droid beeps for greeting, being called, happy, a shrug, nervous, a hit, waking and a chirp; a robot beeps at most once per 1.5 s and the two together once per 0.5 s.

## Rules

| Rule | Value | Where |
| --- | --- | --- |
| Awake | 06:00–20:00; standby at its spot outside | `RobotSettings.Awake` |
| Idle, stroll | Idle 4–9 s × patience; stroll chance 0.25 ± 30%, within the spot's radius (garden 4 m, house 1.6 m), never into a NoGo box | `IdleTime`, `BaseTemper`, `PersonalitySpread`, `RobotSpot` |
| Speeds | Walk 0.62 m/s (the walk clip's own pace at 1.3 m), trot 1.3 m/s | `WalkSpeed`, `TrotSpeed` |
| Greeting | Notices within 5 m (head turns); holds 2.5 s after the last look; waves at most once per 60 s (chance 0.6 ± 30%), else talks | `NoticeRadius`, `GreetHold`, `WaveCooldown`, `WaveChance` |
| Whistle | Reach 50 m, aim radius 0.35 m, cooldown 0.6 s; stops 1.8 m from the player on its side, re-aims when they move 2 m; waits 20 s × patience; leaves when the player walks 8 m from where they stood on its arrival; outside the yard it stops 1.2 m inside the fence | `PlayerInteractionTuning` (Whistle), `ArriveDistance`, `CallWait`, `LeaveDistance`, `YardMargin`, `RobotCall.PlanMeetPoint`, `Robot.TryPlanMeet` |
| Bump | Within 0.9 m: steps 1.5 m aside (not while flinching) | `BumpRadius`, `StepAside` |
| Walks | Give up after 1.5 × the straight-line time plus 4 s | `RobotCall.TravelTimeout`, `TravelTimeout` |
| Nervous | A held gun aimed within 6° of its head from 6 m; cooldown 8 s | `NervousRadius`, `NervousAngle`, `NervousCooldown` |
| Hit | Stands 1.2 s; `TakeDamage` returns nothing applied, so `DamageService` publishes no `DamageDealt` (no hit marker) | `HitHold`, `Robot.TakeDamage` |
| Head | Turns toward the player's eyes up to 70° / 30°, eased | `HeadYawLimit`, `HeadPitchLimit`, `HeadTurnSpeed` |
| Beeps | 1.5 s per robot, 0.5 s across robots | `BeepGap`, `SharedBeepGap`, `RobotBeeps` |

## Pieces

```text
Robot (prefab root)          IInteractable (E), IWhistleTarget (Q), IDamageable (flinch only); its service, face, animator, head,
                             spot; Call, Greeting, Hit, Nervous, Asleep, step requests; legs (Speed, Turn) and head turn; no Update
RobotFace / RobotFaceGlow    the face screen (mood, held expression, blinks, night glow) / a TimeOfDayReactor (Darkness) feeding it
RobotService (SO)            DisplayName, Verb, Icon: the robot's job; swap it to change what the robot offers
RobotFaceSet (SO)            per model: eye / mouth renderers and which frames make each RobotExpression
RobotSettings (SO)           everything tunable above + Sounds (one SoundID per RobotSound)
RobotRoster (scene service)  the robots, the yard, night, the player
RobotManager (entry point)   per frame: night, the player, looks → greetings, bumps, nervousness, legs and faces, thinks; heads after
                             the Animators; TryBeep
RobotTasks (Behavior Designer)  flinch > step aside > answer the whistle > nervous > greet > standby at night > idle and stroll
RobotSpot / RobotYard        scene components: where a robot lives (radius, NoGo boxes) / the yard it never leaves
RobotInstaller               registers the above, injects the scene's robots (collected on save / Play)
PlayerWhistle (PlayerSystem) the Q key: the whistle sound + a 50 m aim cast to an IWhistleTarget
```

- **Legs and tree:** the WanderCapsule contract without the enemy parts (`FollowerEntity` + `EnemyMover` with `holdGround` + a Manual `BehaviorTree`), on the **Robot** layer (13). The tree is `Content/Robots/_Shared/Robot.asset`, built from `AgentScripts/Robots/Robot.tree.txt`; only `RobotManager` ticks it.
- **Model and animation:** `Content/Robots/Rob15/Model/Rob15_Model.prefab`, a first-party copy of the GrigoriyArx Droll Robots `Rob15` without its demo scripts (legacy input), character controller or push collider, nested as `Model` at scale 0.74. `Rob15.controller`: a 2D blend (Turn, Speed) of idle, walk, run and the 90° turns; `Emote_<RobotEmote>` and `Hit_0..2` from any state; `Standby` (idle at 0.4 speed). The in-place clips (`Rob15_walk`, `_run`, `_idle`, `_turn90*`), root motion off.
- **Face:** the eyes and mouth are two quads on one material whose texture (`Emotions4.png`) is a strip of 10 eye frames over 10 mouth frames; `RobotFace` picks a frame by offsetting `_BaseMap_ST` in a property block and brightens `_EmissionColor` at night. Frame map in `RobotSetup.Data` and `Rob15_Face.asset`.
- **Hits:** the Robot layer is in the bullet masks of the guns that hit animals, the interaction mask and the held-item clearance mask; `Robot` is not `IShowsOwnHits`, so bullets show the surface impact, not blood.

## In WorldMap

`Home Robots` is its own scene root (a `Landmarks` rebuild doesn't delete it): `Yard` (`RobotYard`, 34 × 27 m, turned 50° like the fence), `Spot_Garden`, `Spot_House` (radius 1.6 m, a NoGo box over the doorway), `RobotCam_House` (a point inside the house for screenshots), `Robots/Robot_Shop`, `Robots/Robot_Upgrades`. `RobotInstaller` sits on the `GameLifetimeScope`.

## Tuning and changing

| To change | Edit |
| --- | --- |
| A robot's job | its `Robot._service` (a `RobotService` in `Data/Robots/Services/`); a new service is a new asset with text rows in `Interaction.csv` (verb) and `Creatures.csv` (name) |
| Its look | a prefab variant of `Robot.prefab` that swaps `Model`, with its controller (same parameters) and a `RobotFaceSet`; the tint is the body material on the variant |
| Timings, reactions, beeps | `Data/Robots/RobotSettings.asset` |
| Spots, yard | `RobotSetup.World` constants (or move the scene objects) |
| Turn the robots off | `RobotSettings.Enabled`, or remove `RobotInstaller` |

## Setup and checks

`AgentScripts/Robots/RobotSetup.cs`: `RobotLayer`, `Data` (settings, services, face set), `Model` (model copy, tints), `Controller`, `Prefabs` (base + variants), `Audio` (the Robots library, `Player_Whistle`), `World` (WorldMap: saves the scene; open it fresh from disk first). The tree: `TreeKit.Build AgentScripts/Robots/Robot.tree.txt`.

`RobotTests` (EditMode) cover the whistle input and cast, the rules (call, beeps, cooldown, personality, yard maths), hits, spots and the data and prefab wiring. `AgentScripts/Robots/RobotPlayCheck.cs` plays them in WorldMap (enter Play, then `Application.runInBackground = true`): `Navmesh`, `Idle`, `Look`, `Whistle` / `WhistleFromYard` / `WhistleFromHouse`, `Bump`, `Shoot`, `Nervous`, `Night`, `Beeps`, `Frame` (for screenshots), `Approach`, `Status`. Passed 2026-11-22 (after the review fixes: a 26 m whistle across the yard arrives 2.0 m from the player; from beyond the south and west fences it waits inside them; walked toward, it meets the player and, walked into, steps 2.3 m aside and keeps waiting; a gun pointed from 2 m makes it nervous; at night Bolt walks 18 m home; keep the player in god mode for long probes, since wandering goblins kill them otherwise). Earlier the same day: idling and strolls inside their areas; a look greets with "[E] Shop Bolt" / "[E] Upgrades Pip"; whistled from 12 m in the yard Bolt trots over and stops 1.7 m away; from 25 m outside it waits inside the fence and never leaves the yard; from 60 m no answer; a crate in the line of aim blocks the call; Pip waves through the open door; step aside 1.6 m; a shot plays `Hit_0` with nothing published; a pointed AK47 plays the head shake; at 23:00 both stand by at their spots with the glow at full, at 07:00 awake; a burst of five beeps plays one. Console `robot.status`, `robot.call [name]`.

## Known gaps

- **Pip can't leave the house.** The doorway (1.18 m) is narrower than the navmesh allows for its 0.5 m agent radius (the enemies' size), so the house interior is its own navmesh island. Pip strolls inside and waves when whistled from outside. A robot-only link through the door (an A* off-mesh link on a tag enemies can't use) would let it out; not built.
- **Not judged by hand:** the beeps (placeholder Gamemaster droid beeps, picked by file, not by ear), feet sliding at walk and trot, how the head turn reads, the tints at every hour and weather, the face frame choices.
- Pip rarely strolls: the house spot's 1.6 m radius leaves few points far enough to walk to.
- Robots aren't saved: on load each starts at its spot.
- `robot.raid` and `robot.welcome` say "pass 2": raid reactions, welcome home, whistle spam, robots greeting each other and the service icon come in pass 2.
