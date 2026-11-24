# Service robots

Friendly robots who live at home. Menu robots will open the shop, upgrades, storage and other menus; working robots (collecting, guarding) and a flying guide can come later on the same code. Built so far: the robots themselves, their reactions and the hooks for menus and jobs, not the menus or jobs. Code in `_Core/RobotSystem/` (namespace `MB13.Aipocalypse.Core.RobotSystem`), content in `Content/Robots/`, data in `Data/Robots/`, script map in [CODE_STRUCTURE.md](CODE_STRUCTURE.md). Design and plans: `docs/superpowers/specs/2026-11-22-service-robots-design.md`, `docs/superpowers/plans/2026-11-22-service-robots-pass1.md`, `…-pass2.md`. Draft design context: [FARM_AND_ECONOMY.md](../gdd/FARM_AND_ECONOMY.md#rescued-robots-and-crafting).

## How it plays

1. **Two robots at home** (WorldMap): **Bolt** (copper-orange, placeholder Shop service) in the garden east of the house, **Pip** (mint, placeholder Upgrades service) inside the house across from the bed. About 1.3 m tall.
2. **Home life.** Mostly a robot stands at its spot and looks around: small turns, blinks, a passing happy or surprised look. Now and then it strolls a few metres and comes back; rarely it dances or laughs. Each robot rolls its own temper (stroll, wave and flourish chances, patience), so the two never move in step.
3. **Aiming at one.** Its **name and job icon** float over its head (fading in, lingering a moment). Within reach: **[E] Shop Bolt** (its service's verb and name) and **Hold: Pat Bolt**; farther (up to 50 m): **[Q] Call Bolt**.
4. **Looking at one / E.** An awake robot stops, turns to the player and waves (at most once a minute) or talks (its mouth becomes a scrolling speech line), happy, while the player keeps looking. E also asks for its menu (`RobotServiceRequested`; no menu listens yet).
5. **Pat (hold E).** Heart eyes, a laugh, a chirp. It forgives a knockdown (ends the sulk).
6. **The whistle (Q).** Aim at a robot up to **50 m** away: the player whistles and the robot reacts with a thumbs-up or a hop, trots over and stops about 1.8 m from the player on its own side, then waits facing them. If the player walks on, it aims at where they are now. It goes home with a shrug when its wait ends (about 20 s × its patience) or the player walks 8 m from where they stood when it arrived.
   - **The dog:** a free robot (`Robot.IsFree`, the chat rules without the chat cooldown) within 4 m of it greets it once per 90 s while the dog is in home life (`Robot.TryGreetVisitor`: it faces it, waves, heart eyes).
   - **Whistle order with the dog** (DOGS.md): a robot under the aim, then the dog under the aim, then a robot heard near the aim, then the dog as the plain-whistle fallback. Robots walk `NavTags.Robots` (every tag but the dog's fence links).
   - **Heard through walls.** Props and walls block the aim, but when nothing callable is under it, the robot nearest the aim within 8° answers (a whistle carries through walls): Pip can be called from beside the house. With no walkable meeting point by the player (against a wall), it comes anywhere reachable within 3.6 m of them on their floor.
   - **Whistle spam.** 4 whistles at one robot within 5 s annoy it: a grumpy head shake, an angry face, it stops coming and sulks; for 8 s it ignores whistles (an angry look). A pat forgives it.
   - **Never leaves the yard.** Whistled from outside, it comes to the inside of the fence nearest the player (1.2 m in) and waits there.
   - **Through the house door.** The door is a robots-only way out (see In WorldMap): Pip walks to the sill and hops down the 1 m step (its jump clip), and hops back up going home.
7. **Reactions.** Walked into, it trots a little out of the way (never into the doorway), even while waiting by the player. A gun pointed at its head within 6 m: worried, a head shake. Shot: a flinch (hit clip by side) and an angry or sad face; **3 hits within 2.5 s knock it over**: it falls (on its back when shot from the front), lies a moment with crossed eyes, gets up grumpy and **sulks** for 10 s (angry resting face; a look gets a head shake instead of a wave). Hits apply nothing, show no hit marker and don't bleed. Raiders ignore robots.
8. **Raids.** When a raid starts, each robot is brave for a moment (boxing stance and a double punch, angry), then worried it trots to its **shelter** (Bolt: inside the house; Pip: its spot) and cowers there, looking round now and then. A whistle during a raid gets a worried head shake. When the raid ends: beaten or outlasted with every fence standing, a **win pose and a dance** (excited eyes); the player fell or a barrier broke, it **cries**. Ended by hand: nothing.
9. **Seeing off.** When the player walks out of the yard, awake robots within 25 m that aren't busy stop, turn to them and wave goodbye (happy, then a little sad); at most once a minute each.
10. **Chats.** Two free robots within 3.5 m of each other (both awake, not called, greeted, hit, cheering, welcoming, seeing off or working) stop, face each other and chat for 4 s: one waves then talks, the other talks then laughs; then not again for 90 s. In WorldMap this happens when both have come to the player and head home together.
11. **Welcome home.** Back in the yard after at least 60 s outside, awake robots within 45 m look round, spot the player and come over, waving or hopping on arrival with heart eyes. Not at night or during a raid.
12. **Night** (outside 06:00–20:00): a yawn, a slow walk to its spot, then it stands by facing out with sleepy eyes, its face glowing brighter and a small spot light from its chest lighting the ground in front (in its screen's green). E, a pat, a whistle or a shot wakes it for 12 s (a look doesn't). In the morning it hops awake with a happy face.
13. **Beeps.** Placeholder droid beeps for greeting, being called, happy, a shrug, nervous, a hit, waking and a chirp; a robot beeps at most once per 1.5 s and the two together once per 0.5 s.

## Rules

| Rule | Value | Where |
| --- | --- | --- |
| Awake | 06:00–20:00; standby at its spot outside | `RobotSettings.Awake` |
| Idle, stroll | Idle 4–9 s × patience, looks round ±70° every 2–4.5 s; stroll chance 0.25 ± 30%, within the spot's radius (garden 4 m, house 1.6 m), never into a NoGo box | `IdleTime`, `LookAround*`, `BaseTemper`, `PersonalitySpread`, `RobotSpot` |
| Speeds | Walk 0.62 m/s, trot 1.3 m/s, bedtime × 0.75 | `WalkSpeed`, `TrotSpeed`, `BedtimePace` |
| Greeting | Notices within 5 m (head turns); holds 2.5 s after the last look; waves at most once per 60 s (chance 0.6 ± 30%), else talks; not while asleep, called, hit or hiding | `NoticeRadius`, `GreetHold`, `WaveCooldown`, `RobotManager.React` |
| Whistle | Reach 50 m, aim radius 0.35 m, cooldown 0.6 s, prompt aim refreshed every 0.05 s; reacts 0.9 s; stops 1.8 m from the player; re-aims when they move 2 m; waits 20 s × patience; leaves when they walk 8 m off; 1.2 m inside the fence | `PlayerInteractionTuning` (Whistle), `CallReactTime`, `ArriveDistance`, `CallWait`, `LeaveDistance`, `YardMargin` |
| Heard through walls | Robot layer (`WhistleHeardMask`), within 8° of the aim; fallback meeting point within 2 × ArriveDistance of the player, within 0.6 m of their height | `PlayerInteractionTuning` (Whistle), `Robot.TryPlanMeet` |
| Whistle spam | 4 whistles within 5 s; annoyed (sulking, ignores whistles) 8 s; a pat forgives | `SpamWhistles`, `SpamWindow`, `AnnoyedTime`, `RobotState.Whistle` |
| Bump | Within 0.9 m: steps 1.5 m aside (not while flinching); a request goes stale after 0.5 s; a step gives up after 2.5 s | `BumpRadius`, `StepAside`, `StepExpiry`, `StepTimeout` |
| Walks | Give up after 1.5 × the straight-line time plus 4 s | `RobotCall.TravelTimeout`, `TravelTimeout` |
| Nervous | A held gun aimed within 6° of its head from 6 m; 2.2 s; cooldown 8 s | `NervousRadius`, `NervousAngle`, `NervousTime`, `NervousCooldown` |
| Hit | Stands 1.2 s; nothing applied, nothing published | `HitHold`, `Robot.TakeDamage` |
| Knockdown | 3 hits within 2.5 s; down 2.6 s (fall + lying), rising 2 s; sulks 10 s after; hits while down do nothing | `KnockdownHits`, `KnockdownWindow`, `KnockdownLie`, `KnockdownRise`, `SulkTime`, `RobotState.TakeHit` |
| Pat | Heart eyes 3 s, ends a sulk | `PatHold`, `RobotState.Pat` |
| Raid | Brave 2.2 s; looks round every 5–9 s while hiding; cheers or cries 5 s; good = Cleared / Dawn with no `BarrierBroken` since `RaidStarted` | `BraveTime`, `HideLookInterval`, `CheerTime`, `RobotManager.CheerFor` |
| Welcome home | Out of the yard ≥ 60 s; robots within 45 m; look round 1.8 s | `WelcomeAway`, `WelcomeRadius`, `WelcomeLookTime`, `RobotHomecoming` |
| Seeing off | Robots within 25 m; waves 3 s; once per 60 s | `SeeOffRadius`, `SeeOffTime`, `SeeOffCooldown`, `RobotHomecoming` |
| Chat | Within 3.5 m; 4 s; once per 90 s | `ChatRadius`, `ChatTime`, `ChatCooldown`, `RobotManager.StartChats` |
| Night light | Spot from the chest (0.85 m up, pitched 60° down, 100°), range 5 m, intensity 2, fades with the dark | `RobotSetup.NightLight`, `TimeOfDayLight` |
| Night wake | 12 s after E, a pat, a whistle or a hit | `NightWake` |
| Head | Turns toward the player's eyes up to 70° / 30°, eased | `HeadYawLimit`, `HeadPitchLimit`, `HeadTurnSpeed` |
| Name tag | 0.45 m over its head; fades 0.15 s, lingers 0.3 s | `NameTagHeight`, `NameTagHud` |
| Beeps | 1.5 s per robot, 0.5 s across robots (read live) | `BeepGap`, `SharedBeepGap`, `RobotBeeps` |

## Pieces

```text
Robot (prefab root)          the bridge: IInteractable (E), IHoldInteractable (pat), IWhistleTarget (Q + prompt), INameTagged, IDamageable
                             (flinch / knockdown only); its role, face, animator, head, spot; State, Call, Work, Mover; no Update
RobotState                   plain C#: greetings, pats, hits → knockdown → sulk, whistles → annoyed, nervous, night wake, step requests,
                             cheer, welcome, seeing off, chats (RobotStreak counts hits and whistles)
RobotRole (SO)               a robot's job: DisplayName, Verb, Icon. RobotService (Menu: Shop, Storage, Upgrades) | RobotJob (CreateWork)
IRobotLegs                   how it moves: RobotGroundLegs (EnemyMover on the navmesh, robots-only ground allowed); a flyer's later
RobotFace / RobotFaceGlow    the face screen (mood, held expression, blinks, speech line, night glow) / a TimeOfDayReactor feeding it
RobotFaceSet (SO)            per model: eye / mouth / speech renderers and which frames make each RobotExpression
RobotSettings (SO)           everything tunable above + Sounds (one SoundID per RobotSound) + the Pat / Call verbs
RobotRoster (scene service)  the robots, the yard, night, raid, the player
RobotManager (entry point)   per frame: night, raids, the player leaving (see off) and coming home (welcome), looks → greetings, bumps,
                             nervousness, chats between free robots, faces and legs, staggered thinks;
                             heads after the Animators; TryBeep
RobotTasks (Behavior Designer)  flinch > step aside > hide (raid) > answer a call > nervous > greet > cheer > see off > chat > welcome >
                                standby > work > home life
RobotSpot / RobotYard        scene components: where a robot lives (radius, NoGo boxes, shelter) / the yard it never leaves
RobotDoorHop (Links assembly)  A* NodeLink2 handler: walk to the sill, hop the step
RobotInstaller               registers the above, injects the scene's robots (collected on save / Play)
PlayerWhistle (PlayerSystem) the Q key: prompt target + the whistle sound + a 50 m aim cast to an IWhistleTarget, else the heard
                             target nearest the aim (through walls)
NameTagHud, InteractPromptHud (UI)  the name tag; the [E] / Hold / [Q] Call lines
```

- **Legs and tree:** the WanderCapsule contract without the enemy parts (`FollowerEntity` + `EnemyMover` with `holdGround` + a Manual `BehaviorTree`) plus `RobotGroundLegs`, on the **Robot** layer (13). The tree is `Content/Robots/_Shared/Robot.asset`, built from `AgentScripts/Robots/Robot.tree.txt`; only `RobotManager` ticks it, staggered by robot.
- **Model and animation:** `Content/Robots/Rob15/Model/Rob15_Model.prefab`, a first-party copy of the GrigoriyArx Droll Robots `Rob15` without its demo scripts, character controller or push collider, with our own face screen mesh (`Rob15_Eyes_Joined`: the vendor's two eye quads leave a 2.4 cm gap down the middle; ours meet at the centre) and materials (`Rob15Glass_Matte` at smoothness 0.15, and `Rob15Emo_*_Screen`: no highlights or reflections, so only the emoji glow; the tinted bodies at smoothness 0.3), nested as `Model` at scale 0.74, **animated off screen too** (the head turn goes on top of the animated pose). `Rob15.controller`: a 2D blend (Turn, Speed) of idle, walk (twice: once at a crawl, 0.3× speed, so the long idle doesn't drag the walking legs slower than the body), run and the 90° turns; turning on the spot to face something runs at `FaceTurnSpeed` 100°/s, near the turn clip's 90 (A* would spin a standing agent at its max 330); the door hop turns it at 270°/s at most instead of snapping; `Emote_<RobotEmote>` and `Hit_0..2` from any state (Brave chains the boxing stance into a double punch; walking off faster than 0.5 m/s once an emote has played 0.5 s cross-fades to Locomotion instead of sliding in the pose, `WalkOff`, the jump excepted; asking for the emote already playing does nothing, else the left-over trigger replayed it); `Knock` + `HitIndex` → `Fall_n` → `Lie_n` (the fall's last frame, while `Down`: held by motion time on `LieTime`, default 1, because cycle offset is ignored on non-looping clips) → `Rise_n`; `Standby` (idle at 0.4 speed).
- **Face:** the eyes and mouth are two quads whose texture (`Emotions4.png`) is a strip of 10 eye frames over 10 mouth frames; `RobotFace` picks a frame by offsetting `_BaseMap_ST` in a property block, swaps the mouth for the vendor's speech line (`Rob15_mouthSpeech`, scrolled) while it shows Talk, and brightens `_EmissionColor` at night. Frame map in `RobotSetup.Data` and `Rob15_Face.asset` (Dizzy = crossed eyes, Love = hearts, Excited = > <, Yawn = closed eyes and an "O").
- **Hits:** the Robot layer is in the bullet masks of the guns that hit animals, the interaction mask and the held-item clearance mask; `Robot` is not `IShowsOwnHits`, so bullets show the surface impact, not blood, and sound like metal (the `SurfaceSettings` rule "Robot" = Metal, first in the list); melee plays the weapon's surface sound on it, not flesh (flesh only on what bleeds).

## Extending

| To add | Do |
| --- | --- |
| A menu robot (storage, another shop) | A `RobotService` asset (Menu, verb and name rows in `Interaction.csv` / `Creatures.csv`, an icon) on a robot variant. The menu's UI subscribes to `RobotServiceRequested` and opens for its `Menu`. A "serving" branch (hold still while the menu is open) comes with the first menu. |
| A working robot (collect coins, guard) | A `RobotJob` subclass whose `CreateWork(robot)` returns an `IRobotWork`; the tree's Work branch runs it above home life and below every reaction (a whistle, a raid, the night interrupt it). Give it a `RobotSpot` (home) or none (home = where it was placed). A guard that fights or can be targeted needs more: `Robot.IsDead` is always false and raiders ignore robots. |
| A flying robot (a guide: tooltips, story) | Its own legs (`IRobotLegs`: fly to a point, hover, face) instead of `RobotGroundLegs`, its own model and tree (no navmesh, no spot; following the player is its job), and the shared `Robot`, `RobotState`, face, whistle, name tag and beeps. Lines and tooltips would go through a new event like `RobotServiceRequested`. |
| A new reaction | A rule on `RobotState` (tested in `RobotStateTests`), a conditional + action in `RobotTasks`, a line in `Robot.tree.txt` (rebuild), an emote / expression in `RobotEnums` with its clip in `RobotSetup.Controller` and frame in `RobotSetup.Data`. |
| Spawning robots at runtime (bought, rescued) | Not built: `RobotInstaller` only injects robots placed in the scene, and robots aren't saved. |

## Tuning and changing

| To change | Edit |
| --- | --- |
| A robot's job | its `Robot._role` (a `RobotService` in `Data/Robots/Services/`, or later a `RobotJob`) |
| Its look | a prefab variant of `Robot.prefab` that swaps `Model`, with its controller (same parameters) and a `RobotFaceSet`; the tint is the body material on the variant |
| Timings, reactions, beeps | `Data/Robots/RobotSettings.asset` |
| Spots, shelters, yard, the door link | `RobotSetup.World` constants (or move the scene objects) |
| Turn the robots off | `RobotSettings.Enabled`, or remove `RobotInstaller` |

## In WorldMap

`Home Robots` is its own scene root (a `Landmarks` rebuild doesn't delete it): `Yard` (`RobotYard`, 34 × 27 m, turned 50° like the fence), `Spot_Garden` (shelter: `Shelter_Garden`, inside the house), `Spot_House` (radius 1.6 m, a NoGo box over the doorway), `DoorLink_House` (a `NodeLink2` tagged RobotsOnly from the floor inside to the ground beside the flower pots, `Sill` and `Outside` children, `RobotDoorHop`), `RobotsOnly_House` (a `GraphUpdateScene` tagging the interior RobotsOnly, so enemies and animals never plan into the house: their paths end outside the door), `RobotCam_House`, `Robots/Robot_Shop`, `Robots/Robot_Upgrades`. AstarPath names tag 1 "Robots only". `RobotInstaller` sits on the `GameLifetimeScope`.

## Setup and checks

`AgentScripts/Robots/RobotSetup.cs`: `RobotLayer`, `Data` (settings and verbs, services and icons, face set), `Model` (model copy, tints, face materials, the joined eyes mesh), `Shared` (the whistle heard through walls by the Robot layer; the Robot = Metal surface rule), `Controller`, `Prefabs` (base + variants, the night light), `Audio`, `World` (WorldMap; ground casts skip the Robot layer), `Hud` (the [Q] Call line and the name tag in `HUD.prefab`). The tree: `TreeKit.Build AgentScripts/Robots/Robot.tree.txt`.

`RobotTests` and `RobotStateTests` (EditMode) cover the whistle (input, cast, prompt, own body), the rules (call, beeps, cooldown, personality, yard maths, state, knockdown, sulk, pat, night wake, step expiry, raid outcome, welcome home, nav tags), hits and the data and prefab wiring. `AgentScripts/Robots/RobotPlayCheck.cs` plays them in WorldMap (enter Play, `Application.runInBackground = true`, player in god mode): pass 1's `Navmesh`, `Idle`, `Look`, `Whistle*`, `Approach`, `Bump`, `Shoot`, `Nervous`, `Night`, `Beeps`, `Frame`, `Status`, pass 2's `Door`, `Tags`, `Prompt`, `Knockdown`, `Pat`, `Raid`, `Welcome`, `Speech`, `NightFacing`, and `HeardThroughWall`, `SeeOff`, `Chat`, `WhistleSpam`, `NightLight`, `DayLight`. Passed 2026-11-23 (pass 3): from beside the house (the aim on the wall) "[Q] Call Pip" shows and Pip comes; Bolt waves the player off as they leave the yard, facing them; Bolt and Pip 2.5 m apart chat (Bolt waves and talks, Pip talks and laughs), facing each other; 4 quick whistles: a head shake, angry, it stops coming, the next is ignored, a pat and it comes again; both robots are Metal to bullets; the night light is on at 23:00 and off by day; pass 2's look, door, welcome, knockdown and pat still pass. Passed 2026-11-22 (pass 2): Pip called from outside comes out through the door with the hop, meets the player 2.1 m away and goes back in; a walker's path to Pip's spot ends outside (y = ground), a robot's goes in; "[Q] Call Bolt" at 8–20 m and the name tag shows; three shots: Fall_0 > Lie_0 > Rise_0, crossed eyes, then a sulk; a pat: hearts and a laugh, the sulk ends; a raid: both brave, then at their shelters (Bolt 0.4 m, after ~20 s), worried; a win: win pose and dance, excited; a loss: crying, sad; welcome home: Bolt looks round, comes over 2.0 m away with hearts; night (raid stopped): both stand by facing out (1–3° off), in the morning both hop; the speech line scrolls while talking; pass 1's look, whistle, approach, bump, shot, nervous and beeps still pass. Performance (editor, two robots): `RobotManager` 0.1 ms a frame (worst 0.3–0.75 ms on a think), the whistle's prompt cast 0.03 ms, no allocations. Console `robot.status`, `robot.call [name]`, `robot.raid start|win|lose|stop`, `robot.welcome`, `robot.knock [name]`.

## Known gaps

- **Not judged by hand:** the beeps (placeholder Gamemaster droid beeps), feet sliding, how the head turn and the door hop read, the tints at every hour, the face frames, the job icons (placeholder map glyphs).
- A real raid at night makes the robots hide rather than stand by; Bolt takes about 20 s to reach its shelter in the house.
- Robots aren't saved: on load each starts at its spot.
- Animal losses don't count toward a bad night (no event for them yet).
- Melee on a robot plays the weapon's generic surface sound, not a metal one (`MeleeSounds` has one surface sound).
- The body's mirrored halves carry slightly different normals down the middle (vendor mesh); a test weld showed no visible change, so it's left as is.
