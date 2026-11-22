# Dog Companion, Phase 1 (a living dog) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Loki, a Border Collie, lives at its dog house in the WorldMap yard and follows the player like a dog. It has a comfort zone, scouts ahead, keeps out of the aim, sniffs, settles, checks in, swims, catches up, tilts its head and sprints alongside. It obeys Follow and Stay from a command wheel (tap E), comes to a plain whistle, can be petted, scrambles over the fence, goes indoors, sleeps in its dog house at night, and is saved.

**Architecture:**
- **System:** a new `DogSystem` scene system modelled on `RobotSystem`. `DogInstaller` registers `DogSettings`, a `DogRoster` and a `DogManager` (ITickable + ILateTickable).
- **Brain:** the manager runs `DogState` and `DogFollow` (plain C#) and thinks Loki's Manual Behavior Designer tree.
- **Body:** Loki moves through `EnemyMover` over A* `FollowerEntity`, with one `DogJump` off-mesh link handler for every link.
- **Player side:** gains a generic `CommandWheel` and an optional whistle fallback.

**Tech Stack:** Unity 6000.6, VContainer, MessagePipe, Behavior Designer Pro (TreeKit specs), A* 5.4 (`FollowerEntity`, `NodeLink2`, `IOffMeshLinkHandler`), BroAudio (`AudioService`, `SoundID`), Unity Localization CSVs, Input System (`GameInputActions`), uGUI + Layer Lab sprites, NUnit EditMode tests.

**Spec:** `docs/superpowers/specs/2026-11-23-dog-companion-design.md`.
- **This plan:** the spec's Phase 1, plus the parts later phases build on:
  - the wheel framework;
  - nav tags;
  - the whistle fallback;
  - the friendly-fire flinch.
- **Later phases:** `2026-11-23-dog-companion-phase2.md` and `-phase3.md`. Each phase starts only after the user plays the one before it.

## Global Constraints

**Code style and rules.** Follow AGENTS.md → Code style:
- `_camelCase` private fields, no `private`, `sealed` leaf classes, `[SerializeField] T _field` plus a read-only property;
- no XML docs; at most 3 comment lines per class or method;
- no Odin serialization; Unity 6.6 `Dictionary` serialization for keyed data;
- chance rolls use a named `RandomService` stream (`Dog`).

**Where things go:**

| What | Where |
|---|---|
| Namespace and code | `MB13.Aipocalypse.Core.DogSystem` in `Assets/_Game/_Core/DogSystem/` |
| Link code | `Links/` subfolder, own asmdef `MB13.Aipocalypse.DogLinks` (like `RobotLinks`) |
| Content | `Assets/_Game/Content/Dogs/` (`BorderCollie/` model copy, `Loki/` entity) |
| Data | `Assets/_Game/Data/Dogs/` |
| Editor scripts | `AgentScripts/Dogs/` |

**Runtime rules:**
- **No per-dog `Update`:** `DogManager` ticks everything.
- **No Malbers components** on any `_Game` prefab: MAnimal, MAnimalBrain, Stats, MDamageable, StepsManager, AnimatorEventSounds, ReboneMesh or any `Malbers*` script. No Malbers controller, and `applyRootMotion` off.
- **Input** only through `InputService`.
- **Time scale** only through `TimeService.Request(owner, scale)` / `Release(owner)`.
- **Audio** only through `AudioService`.
- **Off switch:** `DogSettings.Enabled` false means Loki isn't activated, no wheel, no HUD, no whistle fallback.

**Numbers from the spec (Phase 1 defaults in `DogSettings`):**

| Setting | Value |
|---|---|
| Comfort zone | inner 3 m, outer 8 m; sprint past 15 m |
| Catch-up | past 40 m, or no path for 3 s; reappears 10–15 m behind the player, out of view |
| Reaction delay | 0.3–0.8 s |
| Scouting | after a steady heading for 2 s; lead 4–7 m |
| Side swap | every 20–40 s |
| Aim cone | 12° + 2 m |
| Sniff points | within 6 m, inside the outer ring |
| Settle | sit after 3–6 s, lie after 15–25 s, nap after 60 s |
| Check-in | every 30–60 s, to 1.5 m in front |
| Swim | water deeper than 0.45 m |
| Head tilt | looked at within 10 m for 1 s; once per 20 s |
| Whistle | hear range 60 m; spam 4 within 5 s → ignores plain whistles 6 s (never counts in a fight) |
| Stay | within 4 m of the stay point; goes home at night or when the player is over 150 m away |
| Wheel | time scale 0.3 |
| Voice | at most one sound per 1.2 s; Bark once per 6 s and 3 per minute; Whine once per 8 s; Yelp once per 1 s; idle barks off |
| Model | FollowerEntity radius 0.3, height 0.7 |

**Text and assets:**
- Every player-facing string goes in `Data/Localization/Csv/*.csv`, English only (`vi`/`fr` empty). That covers the name "Loki", "Orders", "Pet", "Follow", "Stay", and the whistle prompt verb.
- **SoundIDs** in this phase, approved by the user on 2026-11-23: Bark, Whine, Yelp, Pant, Sniff, Howl, in a new BroAudio library `Data/Audio/Dogs.asset`. Sources: Gamemaster Pro `Animals_Nature_Ambiences/dog_bark_small_*`, `Animal_Impersonations/dog_*`, and the collie pack's `Audio/*.ogg`.
- **ASSETS.md:**
  - the Malbers row 74 (Pets: Border Collie now in use, models and clips only);
  - the SICS row (`TFP_Dog_House_01B` used, approved by the user on 2026-11-23).

**Editor and workflow:**
- Save only what you wrote (`SaveAssetIfDirty`); never `AssetDatabase.SaveAssets()`.
- After a scene save, `git diff` WorldMap and revert unrelated hunks. COZY's cirrus texture line is known and accepted.
- No commits (AGENTS.md). Each task ends with a clean recompile and its tests green.

**Commands (Git Bash):**
- `P="$(cygpath -w "$PWD")"`
- `unity command --project-path "$P" script recompile`, then poll `script recompile_status`.
- `unity command --project-path "$P" test run --mode editor --filter Dog`. Check that the scene isn't dirty first; if it is, run the classes by reflection (memory "tests with a dirty scene").
- Play probes: `script run --file AgentScripts/Dogs/DogPlayCheck.cs --entry DogPlayCheck.<Probe> --timeout 120`, after `editor.playmode start` and `Application.runInBackground = true`.

## Review Focus

1. **Loki left behind or stuck** (the player jumps a wall, swims a lake, sleeps, dies or loads).
   - Expected: it reappears near the player out of view within about 3 s, or at its dog house after sleep, death or a load. It never stays stranded.
   - Pinned by: Task 3 `Follow_CatchesUpWhenFarOrPathless`, Task 12 probes `CatchUp` and `AfterLoad`.
2. **Loki in the player's line of fire while aiming.**
   - Expected: it leaves the aim cone within about 1 s.
   - Pinned by: Task 3 `Follow_LeavesTheAimCone`, Task 12 probe `AimClear`.
3. **The fence scramble with the gate shut, and the house step.**
   - Expected: Loki crosses both ways, and no enemy, robot or animal paths over the fence links.
   - Pinned by: Task 5 `NavTags_DogOnlyIsOffForWalkersRobotsAndAnimals`, Task 12 probes `FenceOut`, `FenceIn`, `Indoors`.
4. **The wheel left open or interrupted** (death, raid start, the dog walking away, Esc).
   - Expected: the time scale and input are always released, and the cursor is locked again.
   - Pinned by: Task 9 `Wheel_ReleasesTimeAndInputOnEveryClose`, Task 12 probe `WheelInterrupted`.
5. **Plain whistle at home with the robots near.**
   - Expected: a robot under the aim or heard within 8° answers. Otherwise Loki comes. Robot whistles never annoy Loki.
   - Pinned by: Task 6 `Whistle_FallsBackToTheDogWhenNoRobotAnswers`, `Whistle_RobotUnderAimWinsOverDog`.

---

### Task 1: Dog enums, settings and text

**Files:**
- Create: `_Core/DogSystem/DogEnums.cs`:
  - `enum DogOrder { Follow, Stay, Herding }`
  - `enum DogStance { Active, Passive }`
  - `enum DogGait { Still, Walk, Trot, Canter, Sprint }`
  - `enum DogPose { Stand, Sit, Lie, Sleep }`
  - `enum DogAct { Bark, Happy, HeadTilt, ShakeWater, Dig, Pee, Howl, Yes, No, Roll, Alert, Yawn }`
  - `enum DogSound { Bark, Whine, Yelp, Pant, Sniff, Howl }`, appended to in Phase 2 (Growl, Bite)
  - `enum DogWhistle { Come, Annoyed, Ignored }`
- Create: `_Core/DogSystem/DogSettings.cs`, a ScriptableObject (`CreateAssetMenu` "Aipocalypse/Dogs/Dog Settings"). Public tuning fields grouped by `[Header]`, every number from Global Constraints with tooltips, plus:
  - `bool Enabled = true`
  - `LocalizedString Name`, `OrdersVerb`, `PetVerb`, `CallVerb`
  - `float ThinkInterval = 0.1f`
  - `Dictionary<DogSound, SoundID> Sounds`
  - `Sprite Icon`
- Create the asset `Data/Dogs/DogSettings.asset` (via `DogSetup.Data`, Task 7).
- Modify: `Data/Localization/Csv/Interaction.csv`, adding:
  - `name.dog_loki` = Loki
  - `verb.dog_orders` = Orders
  - `verb.dog_pet` = Pet
  - `verb.dog_call` = Call
  - `wheel.dog_follow` = Follow
  - `wheel.dog_stay` = Stay
  - `wheel.dog_pet` = Pet
  - Read `docs/technicals/LOCALIZATION.md` first. If a `UI` table is the convention for wheel labels, put the `wheel.*` rows there instead.
- Test: `Tests/EditMode/DogTests.cs` (new class `DogTests`).

**Interfaces:**
- Produces: the enums above and `DogSettings`, with the public fields named in Global Constraints and these field names, used by later tasks:
  - `InnerRadius`, `OuterRadius`, `SprintRadius`, `CatchUpRadius`, `NoPathTime`, `ReappearMin`, `ReappearMax`
  - `ReactionDelay` (`Vector2`), `SteadyTime`, `Lead` (`Vector2`), `SideSwap` (`Vector2`), `AimCone`, `AimClearance`
  - `SniffRadius`, `SitAfter` (`Vector2`), `LieAfter` (`Vector2`), `NapAfter`, `CheckIn` (`Vector2`), `CheckInDistance`
  - `SwimDepth`, `LookRange`, `LookTime`, `HeadTiltGap`
  - `HearWhistleRange`, `SpamWhistles`, `SpamWindow`, `AnnoyedTime`
  - `StayRadius`, `StayGoHomeDistance`, `WheelTimeScale`
  - `VoiceGap`, `BarkGap`, `BarkPerMinute`, `WhineGap`, `YelpGap`
  - `WalkSpeed`, `TrotSpeed`, `CanterSpeed`, `SprintSpeed`

- [ ] **Step 1:** Write the failing test `Settings_EveryDogSoundHasAnId`. Load `Data/Dogs/DogSettings.asset` and assert that every `DogSound` value is a key in `Sounds` with a valid id. It runs after Task 12 fills the ids, so mark it `[Ignore("filled by Task 12")]` until then.
- [ ] **Step 2:** Write the enums, the settings and the CSV rows. Recompile clean.

### Task 2: DogState (order, whistle, pet, look, settle timers)

**Files:**
- Create: `_Core/DogSystem/DogState.cs`. Plain C#; times are always passed in. Reuse `RobotStreak` from RobotSystem, or move it to `_Common`; if you move it, update CODE_STRUCTURE.
- Test: `Tests/EditMode/DogStateTests.cs`.

**Interfaces:**
- Produces:
  - Constructor: `DogState(DogSettings settings)`.
  - Order: `DogOrder Order`, `Vector3 StayPoint`, `void SetOrder(DogOrder order, Vector3 at)`.
  - Whistle: `DogWhistle Whistle(float now, bool inFight)`, `bool Annoyed(float now)`.
  - Pet: `void Pet(float now)`, which clears annoyance.
  - Look: `bool TryHeadTilt(float now)`, which applies the `HeadTiltGap` cooldown.
  - Settling: `DogPose SettlePose(float stillFor, float napAt, float sitAt, float lieAt)`, with the per-settle randoms rolled by the caller and passed in.
  - `void Reset()`.

- [ ] **Step 1:** Write the failing tests:
  - `Whistle_FourInFiveSecondsAnnoysThenIgnores`: whistles at t = 0, 1, 2, 3. The 4th returns Annoyed and the next returns Ignored until t = 9.
  - `Whistle_InAFightNeverCounts`: 10 whistles with `inFight` true all return Come.
  - `Pet_ForgivesAnnoyance`.
  - `HeadTilt_OncePer20Seconds`.
  - `Settle_SitsThenLiesThenNaps`: with `stillFor` 4 / 18 / 61 and sit 3, lie 15, nap 60, the poses are Sit, Lie, Sleep; 2 gives Stand.
  - `SetOrder_StayRemembersThePoint`.
- [ ] **Step 2:** Run `--filter DogState`. Expected: FAIL (types missing).
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run again. Expected: PASS.

### Task 3: DogFollow (where to be, how fast)

**Files:**
- Create: `_Core/DogSystem/DogFollow.cs`. Plain C#, no Unity scene access; vectors are flat XZ.
- Test: `Tests/EditMode/DogFollowTests.cs`.

**Interfaces:**
- Consumes: `DogSettings`.
- Produces:
  - `readonly struct FollowInput`: `Vector3 Player`, `Vector3 PlayerVelocity`, `Vector3 PlayerForward`, `bool PlayerSprinting`, `bool PlayerAiming`, `Vector3 CameraPosition`, `Vector3 CameraForward`, `Vector3 Dog`, `bool HasPath`, `float Now`.
  - `readonly struct FollowPlan`: `FollowMode Mode`, `Vector3 Goal`, `DogGait Gait`.
  - `enum FollowMode { Free, Return, Scout, Sprint, Alongside, ClearAim, CatchUp }`.
  - `FollowPlan Plan(in FollowInput input)`.
  - `void Reset(float now)`.
  - Internal state: the chosen side, its swap time, the heading-steady timer, the reaction-delay timer, the no-path timer and the mode with hysteresis (leave Return only inside `InnerRadius + 1`).
  - `static Vector3 ClearOfAim(Vector3 dog, Vector3 camPos, Vector3 camFwd, float coneDeg, float clearance)`: the nearest point outside the cone.

- [ ] **Step 1:** Write the failing tests. The fixture is a player at the origin facing +Z, defaults from a `DogSettings` made with `ScriptableObject.CreateInstance`.
  - `Follow_InsideTheZoneIsFree`: dog at 5 m → Free.
  - `Follow_PastOuterReturnsAfterTheReactionDelay`: dog at 10 m. At t = 0 it's still Free; after `ReactionDelay.y` it's Return with gait Trot, and the goal is within `InnerRadius + 1` of the player on the dog's side.
  - `Follow_PlayerStopNeverStopsTheDogTheSameFrame`: the player walks then stops; the dog keeps the previous plan until the delay passes.
  - `Follow_PastSprintRadiusSprints`: 20 m → Sprint.
  - `Follow_CatchesUpWhenFarOrPathless`: 45 m → CatchUp; `HasPath` false for 3.1 s → CatchUp.
  - `Follow_SteadyWalkScoutsAhead`: player velocity (0, 0, 1.5) steady for 2.1 s → Scout, with the goal 4–7 m ahead along +Z.
  - `Follow_PlayerSprintingRunsAlongside`: → Alongside, gait Sprint, goal beside the player on the side.
  - `Follow_LeavesTheAimCone`: aiming, with the dog 5 m straight ahead of the camera → ClearAim, and the goal is outside the 12° cone by at least 2 m.
  - `Follow_SideSwapsWithinItsWindow`.
- [ ] **Step 2:** Run `--filter DogFollow`. Expected: FAIL.
- [ ] **Step 3:** Implement. Gait from distance to goal: under 2 m Walk, then Trot, Canter past 10 m, Sprint for Sprint / Alongside / CatchUp.
- [ ] **Step 4:** Expected: PASS.

### Task 4: DogVoice (bark budget)

**Files:**
- Create: `_Core/DogSystem/DogVoice.cs`.
- Test: in `DogStateTests.cs`.

**Interfaces:**
- Produces: `DogVoice(DogSettings s)` and `bool TryPlay(DogSound sound, float now)`. Global gap `VoiceGap`; Bark gap and a rolling 60 s count of at most `BarkPerMinute`; Whine and Yelp gaps; Pant, Sniff and Howl use only the global gap.

- [ ] **Step 1:** Write the failing tests:
  - `Voice_GlobalGapBlocksBackToBack`
  - `Voice_BarkAtMostThreePerMinute`: barks every 6 s; the 4th within 60 s is refused.
  - `Voice_YelpGapIsOneSecond`
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Expected: PASS.

### Task 5: Nav tags for the dog

**Files:**
- Modify: `_Core/EnemySystem/NavTags.cs`:
  - add `public const int DogOnly = 2;`
  - `Walkers = ~((1 << RobotsOnly) | (1 << DogOnly))`
  - add `public const int Robots = ~(1 << DogOnly);`
- Modify: `_Core/RobotSystem/RobotGroundLegs.cs`: `traversableTags: NavTags.Robots` instead of `All`.
- Check every other `EnemyMover.Initialize` caller (animals, enemies) uses `Walkers` or the default.
- Modify: `AgentScripts/Robots/RobotSetup.cs` where AstarPath tag names are set: name tag 2 "DogOnly". Run that step, or set the name in `DogSetup.World` (Task 11).
- Test: `Tests/EditMode/RobotStateTests.cs` (it already holds NavTags tests) or `DogTests.cs`.

- [ ] **Step 1:** Write the failing test `NavTags_DogOnlyIsOffForWalkersRobotsAndAnimals`:
  - `Allows(Walkers, 2)` is false and `Allows(Robots, 2)` is false;
  - `Allows(All, 2)` is true and `Allows(Robots, 1)` is true;
  - reflect over the source of `AnimalSystem` callers: `EnemyMover.Initialize` calls pass no `All`.
  - If the reflection check is impractical, assert the constants only and grep in Step 3.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement and grep `NavTags.All` across `_Core`; the only user left should be the dog (Task 8).
- [ ] **Step 4:** Run the robot and dog tests. Expected: PASS, with the existing NavTags tests updated.

### Task 6: Whistle fallback to the dog

**Files:**
- Create: `_Core/_Common/Interaction/IWhistleFallback.cs`: `bool TryAnswer(GameObject actor, Vector3 eye, Vector3 aim, out IWhistleTarget target)` (namespace `MB13.Aipocalypse.Core.Interaction`).
- Modify: `_Core/PlayerSystem/PlayerWhistle.cs`:
  - Order of `FindTarget`: aimed `IWhistleTarget` (robot or dog), then `Heard` (robots within the heard angle), then the fallback.
  - `Heard` must skip colliders whose parents have no `IWhistleTarget`.
  - The fallback is injected as optional (`TryResolve`).
- Modify: `PlayerInteractionTuning` asset: add the Animal layer (20) to `WhistleHeardMask`.
- Test: `Tests/EditMode/RobotTests.cs` WhistleScene, extended with a stub fallback.

**Interfaces:**
- Produces: `IWhistleFallback`. `DogManager` implements and registers it in Task 10. The prompt shows "[Q] Call Loki" when the fallback would answer.

- [ ] **Step 1:** Write the failing tests:
  - `Whistle_FallsBackToTheDogWhenNoRobotAnswers`
  - `Whistle_RobotUnderAimWinsOverDog`
  - `Whistle_HeardRobotWinsOverDog`
  - `Whistle_HeardSkipsNonWhistleColliders`: an Animal-layer collider without the interface, near the aim, is ignored.
- [ ] **Step 2:** Run `--filter Whistle`. Expected: the new tests FAIL; the existing robot whistle tests PASS.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Expected: all PASS.

### Task 7: Model copy, controller and entity prefab (editor setup)

**Files:**
- Create: `AgentScripts/Dogs/DogSetup.cs`: a static class of steps returning log strings (pattern: `RobotSetup`, `AnimalSetup`).
  - `Data()`: `DogSettings.asset`, with the Name and verbs set to the CSV keys.
  - `Model()`:
    - copies `Border_Collie_Skye_PA` into `Content/Dogs/BorderCollie/Model/BorderCollie_Model.prefab`;
    - strips every MonoBehaviour, collider and Rigidbody, sets `applyRootMotion=false`, and sets the layer to Animal on every child;
    - the default material is `Dog 4x4 Black 01`;
    - writes `Model/ColourVariants.png` (a renders sheet of the PolyArt colour mats) for the user to pick from.
  - `Controller()`:
    - builds `Content/Dogs/BorderCollie/BorderCollie.controller` as in the spec's parameter table;
    - Phase 1 states only: Speed blend Walk/Trot/Canter/Run/Sprint, Turn; Stance 0 normal and 2 sniff; Pose stand/sit/lie/sleep with the pack's transitions; `Act`+`ActIndex` for the `DogAct` values; Hit + Side (flinch); Jump + JumpKind (trot jump, ledge 0.5/1.0/1.5/2.0 + climb up, fall from edge, land); Swim;
    - looping copies where needed (pattern: `AnimalSetup.Looping`);
    - the blend timeScale comes from measured ground speeds: adapt `AnimalStrideProbe` into `DogStrideProbe` and write the speeds into `DogSettings` Walk/Trot/Canter/Sprint speeds.
  - `Prefabs()`:
    - base `Content/Dogs/Dog.prefab` from the WanderCapsule template, with the enemy parts removed;
    - FollowerEntity radius 0.3, height 0.7, rotationSpeed 300, `maxOnSpotRotationSpeed` 240;
    - BehaviorTree Manual (`m_UpdateMode` 1, not started when enabled), subtree `Content/Dogs/_Shared/Dog.asset`;
    - nested model; `Dog` component; `SaveId`;
    - variant `Content/Dogs/Loki/Loki.prefab`.
  - `All()` runs the steps in order.
- Test: `DogTests.Prefab_LokiHasNoMalbersOrRootMotion`:
  - load `Loki.prefab`; no component whose type namespace starts with `MalbersAnimations`;
  - the Animator has `applyRootMotion` false and the controller is `BorderCollie.controller`;
  - FollowerEntity radius 0.3.

- [ ] **Step 1:** Write the failing test above.
- [ ] **Step 2:** Run. Expected: FAIL (no prefab).
- [ ] **Step 3:** Write `DogSetup`. Run `script run --file AgentScripts/Dogs/DogSetup.cs --entry DogSetup.All`.
- [ ] **Step 4:** Capture the model with `capture screenshot` (an open scene with the prefab, then delete it) and check the colours, the scale (about 0.7 m tall) and the controller states.
- [ ] **Step 5:** Expected: the test PASSes and `git status` shows only the new dog assets.

### Task 8: Dog runtime bridge, legs and jumping

**Files:**
- Create: `_Core/DogSystem/Dog.cs`, a MonoBehaviour with no Update. It implements:
  - `IInteractable`: tap → `DogManager.OpenWheel(this)`; prompt "[E] Orders".
  - `IHoldInteractable`: hold → `Pet`; prompt "Hold: Pet".
  - `IWhistleTarget`: prompt "[Q] Call Loki"; whistle → `DogManager.Whistled`.
  - `INameTagged`: name, `Icon`, point above the head.
  - `IDamageable`:
    - `TakeDamage` from a `source` under the player's root plays Yelp (through the voice) and a flinch by side, and returns `default`;
    - any other source in Phase 1 also only flinches and returns `default`; Phase 2 adds toughness.
  - It holds the `Animator` parameter hashes and `Advance(float dt, float now)`, which sets Speed from the mover velocity (damped 0.15) and Turn.
- Create: `_Core/DogSystem/DogLegs.cs`, wrapping `EnemyMover`:
  - `Initialize(nav, settings.TrotSpeed, 0.25f, holdGround: true, traversableTags: NavTags.All)`;
  - `SetGait(DogGait)` maps to the settings speeds;
  - `MoveTo`, `Face`, `Halt`, `Settled`, `Velocity`, `TryFindDestination` (copy the `IRobotLegs` members; don't share the interface);
  - `Warp(Vector3)`, for catch-up.
- Create: `_Core/DogSystem/Links/DogJump.cs` with asmdef `MB13.Aipocalypse.DogLinks` (references as for `RobotLinks`):
  - implements `IOffMeshLinkHandler` and `IOffMeshLinkStateMachine`, set as `FollowerEntity.onTraverseOffMeshLink` by `Dog` on activate;
  - moves to the link start, `DisableLocalAvoidance()`, built-in movement off, then plays the kind from `DogJumpArc`.
- Create: `_Core/DogSystem/DogJumpArc.cs`, plain and tested:
  - `enum JumpKind { Hop, Scramble, ClimbUp, DropDown }`
  - `static JumpKind Pick(float rise, float obstacleHeight)`
  - `static Vector3 Position(JumpKind kind, Vector3 start, Vector3 end, float peak, float t)`
  - `static float Duration(JumpKind kind, float distance)`
  - **Pick:** a rise under 0.6 m with no obstacle above 0.6 m → Hop. An obstacle over 0.6 m with both ends within 0.6 m of each other → Scramble (up to the obstacle top + 0.1, then down). A rise of 0.6–2.2 m → ClimbUp. A drop over 0.6 m → DropDown.
  - **Clip per kind:** Scramble plays `Dog_Ledge_2.0M` → `Climb Up` → `Fall_from_Edge` → `Land`. ClimbUp plays the ledge clip nearest the rise.
  - **Obstacle height:** for a `NodeLink2`, `DogSetup` writes it into the link's GameObject name suffix or a small `DogLinkInfo` component. Builder's choice; document it in DOGS.md.
- Test: `DogTests`:
  - `TakeDamage_FromPlayerYelpsAndAppliesNothing`: no `DamageDealt` published, returns default.
  - `JumpArc_PicksByRiseAndObstacle`: fence 2.0 / rise 0 → Scramble; rise 1.0 → ClimbUp; rise −1.2 → DropDown; rise 0.3 → Hop.
  - `JumpArc_ScrambleClearsTheObstacle`: the peak ≥ obstacle + 0.1, and the ends are exact at t = 0 and 1.

- [ ] **Step 1:** Write the failing tests above.
- [ ] **Step 2:** Run `--filter Dog`. Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Expected: PASS.

### Task 9: Command wheel (generic) and the dog's slices

**Files:**
- Create: `_Core/UI/Wheel/WheelMath.cs`: `static int SliceAt(Vector2 fromCentre, int count, float deadZone)`. It returns −1 inside the dead zone; slice 0 is centred straight up and the order is clockwise.
- Create: `_Core/UI/Wheel/WheelSlice.cs`, a struct: `string Label`, `Sprite Icon`, `bool Enabled`, `bool Current`, `Action Picked`.
- Create: `_Core/UI/Wheel/CommandWheel.cs`, a passive view: `Show(IReadOnlyList<WheelSlice>)`, `Highlight(int)`, `Hide()`.
- Create: `_Core/UI/Wheel/CommandWheelPresenter.cs`, a scene service:
  - `bool TryOpen(object owner, IReadOnlyList<WheelSlice> slices)` and `void Close()`, which also runs when the owner calls `Close` or on `PlayerDied` and `RaidStarted`;
  - on open: `TimeService.Request(this, settings.WheelTimeScale)`, `InputService.RequestUI(this)`, the cursor warped to the screen centre;
  - each frame it reads the pointer (or the UI Navigate stick) and `SliceAt`;
  - Submit or click picks an enabled slice; Cancel, right click or the new UI action closes;
  - every exit releases time and input.
- Modify: `InputSystem_Actions.inputactions`: UI map action `CloseWheel` bound to `<Keyboard>/e` and `<Gamepad>/buttonEast`. Regenerate the wrapper and expose `InputService.CloseWheelPressed`.
- Modify: `HUD.prefab` through `DogSetup.Hud`, adding a `CommandWheel` root with up to 8 slice slots (Layer Lab sprites via `LayerLabSetup` conventions) and labels as TMP texts. UI.md tree updated.
- Test: `Tests/EditMode/WheelTests.cs`:
  - `WheelMath_PicksSlicesClockwiseFromTop`: 4 slices; up → 0, right → 1, down → 2, left → 3; inside the dead zone → −1.
  - `WheelMath_SixSlicesBoundaries`: angles at slice edges ± 1° land on the right slices.
  - `Wheel_ReleasesTimeAndInputOnEveryClose`: a presenter built with fake `TimeService` and `InputService` stand-ins (or the real ones, constructed as other UI tests do). Open, then each of Pick, Cancel, `Close()` and `PlayerDied` leaves no time request and no UI request.
  - `Wheel_DisabledSliceCannotBePicked`.

- [ ] **Step 1:** Write the failing tests.
- [ ] **Step 2:** Run `--filter Wheel`. Expected: FAIL.
- [ ] **Step 3:** Implement and build the HUD node.
- [ ] **Step 4:** Expected: PASS. Capture the HUD with the wheel forced open from a play probe (Task 12); visual check only.

### Task 10: Behaviour tree, the dog house and the scene system

**Files:**
- Create: `_Core/DogSystem/DogTasks.cs`, Behavior Designer `ConditionalNode` / `ActionNode` classes.
  - Each gets `GetComponent<Dog>()` in `OnAwake`; `OnEnd` must be null-safe (the lesson from `RobotDoWork`).
  - Conditionals: `DogIsWhistled`, `DogIsPetting`, `DogMustCatchUp`, `DogIsFollowing`, `DogIsStaying`, `DogIsNight`, `DogIsInWater`, `DogIsHome`.
  - Actions:
    - `DogComeToHeel`: run to 1.5 m, sit, and face the player; Annoyed comes at walk speed with an Act No first.
    - `DogBePetted`: come in front, sit, then happy / head tilt / roll 1 in 4.
    - `DogCatchUp`: warp out of view, then trot.
    - `DogFollowPlayer`: drives `DogFollow.Plan`, runs the sniff, settle and check-in sub-behaviours in Free mode, and the head tilt.
    - `DogStayHere`: life within `StayRadius`.
    - `DogHomeLife`: yard wander, sniff, nap in the doorway.
    - `DogSleepInHouse`.
    - `DogSwim`: the Swim bool while depth > 0.45, then ShakeWater on leaving.
- Create: `AgentScripts/Dogs/Dog.tree.txt`. Priority order, each a `Sequence AbortType=Both|LowerPriority`:
  1. Whistled → come
  2. Petting → pet
  3. Catch up
  4. Swim (parallel guard)
  5. Following → follow
  6. Staying away from home → stay here
  7. Night at home → sleep in house
  8. Home life
  
  Build with TreeKit into `Content/Dogs/_Shared/Dog.asset`.
- Create: `_Core/DogSystem/DogHouse.cs`, a scene MonoBehaviour: `Transform Door` (navmesh point), `Transform Bed` (lie-in pose, facing out), `Vector3 Entry`.
  - Scripted in and out moves for `DogHomeLife` / `DogSleepInHouse`: movement off, lerp Door → Bed over 1 s with the walk clip, turn to face out, then pose Lie or Sleep. The reverse on leaving.
- Create: `_Core/DogSystem/DogRoster.cs`, a scene service: `Dog Dog`, `DogHouse House`, `RobotYard Yard` (reused), `bool Night`, `bool Raid`, `bool HasPlayer`, `Vector3 PlayerPosition`.
- Create: `_Core/DogSystem/DogManager.cs`, `ITickable, ILateTickable, IDisposable, IWhistleFallback`:
  - ctor takes `DogRoster`, `DogSettings`, `WorldClock`, `PlayerRegistry`, `AudioService`, `TimeService`, `CommandWheelPresenter`, `RandomService`, the `RaidStarted` and `RaidEnded` subscribers, and an optional `IWaterLevel`;
  - per frame: night (06:00–20:00 awake, as the robots), the player view, `Dog.Advance`, then `Think()` every `ThinkInterval`;
  - wheel slices: Follow, Stay, Pet;
  - `Whistled`; `TryAnswer` (within `HearWhistleRange`, not while down);
  - `TryPlay(DogSound)` through `DogVoice` and `AudioService.PlayAt`;
  - on `PlayerRevived` and when the sleep sequence ends (`SleepService.Stage` back to None after Asleep, or `WorldClock.TimeSkipEnded`): Loki is placed at its house with the order Follow.
- Create: `_Core/DogSystem/DogInstaller.cs`, `SceneInstaller`:
  - `Problem` requires `EnemyInstaller` and `WorldTimeInstaller`;
  - registers the settings, `DogRoster`, `RegisterEntryPoint<DogManager>().AsSelf().As<IWhistleFallback>()`, and `DogCommands` when `Cheats.Enabled`;
  - collects the Dog and DogHouse in the scene on save / Play (`SceneCollection.Hook`, as `RobotInstaller` does);
  - strict-scope gotcha: add the installer component to every strict scene that needs it (WorldMap), or it logs an error.
- Create: `_Core/DogSystem/DogSaveSection.cs`:
  - a `[Serializable]` DTO `{ DogOrder Order; Vector3 Position; float Yaw; bool InYard; Vector3 StayPoint; }`, Order `SaveOrder.World`;
  - register it with `RegisterEntryPoint<DogSaveSection>()`;
  - bump `SaveService.Version`.
- Create: `_Core/DogSystem/DogCommands.cs`, `[CommandGroup("dog")]`: `come`, `order follow|stay`, `where`. Also add them to the CONSOLE.md table.
- Test:
  - `DogStateTests.Save_RoundTripsOrderPositionAndStayPoint` (pattern: the `AnimalSaveSection` test).
  - `DogTests.Installer_RegistersFallbackOnlyWhenEnabled`: with `Enabled` false, `TryAnswer` returns false.

- [ ] **Step 1:** Write the failing tests.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement the tasks, tree, house, roster, manager, installer, save and commands. Build the tree with `script run --file .agents/skills/aipocalypse-enemy-ai/scripts/TreeKit.cs --entry TreeKit.Build --args '["AgentScripts/Dogs/Dog.tree.txt"]'`.
- [ ] **Step 4:** Recompile clean, `--filter Dog` PASS, and `console status` shows no errors.

### Task 11: WorldMap placement, links and navmesh

**Files:**
- Modify: `AgentScripts/Dogs/DogSetup.cs`, adding `World()`:
  - places `TFP_Dog_House_01B` as a prefab instance in the yard: near the house, clear of the robot spots, the herd corner and the gate path. Capture and check before saving.
  - adds `DogHouse` with its Door and Bed children;
  - places the `Loki.prefab` instance at the door;
  - adds the `DogInstaller` component on the scene scope;
  - names AstarPath tag 2 "DogOnly";
  - for every fence segment and the gate (from `DefenseSetup`'s data or the scene's `BarrierSegment`s), places a two-way `NodeLink2`: start 1 m inside, end 1 m outside, on the navmesh (`GetNearest`), tagged DogOnly, with the obstacle height recorded;
  - a navmesh update over the dog house bounds (a `NavmeshCut` box on the house instance, as the test ammo crate does, so no re-bake is needed).
- Modify: `docs/technicals/ASSETS.md`, both rows (see Global Constraints).

- [ ] **Step 1:** Run `DogSetup.World`.
  - Check `git diff --stat` on WorldMap: only the dog house, Loki, the installer, the links and the tag name. Revert unrelated hunks.
  - Capture the yard from above with `capture screenshot --view scene`.
- [ ] **Step 2:** In Play, use `script eval` to check `AstarPath.active.GetNearest` at the door point (within 0.3 m) and that a path from the yard to outside with the gate shut contains a DogOnly link for the dog but none for a `Walkers` path.

### Task 12: Sounds, docs and play checks

**Files:**
- Modify: `AgentScripts/Dogs/DogSetup.cs`, adding `Audio()` (pattern: `RobotSetup.Audio`):
  - the BroAudio library `Data/Audio/Dogs.asset` with `Dog_Bark`, `Dog_Whine`, `Dog_Yelp`, `Dog_Pant`, `Dog_Sniff`, `Dog_Howl`;
  - a spatial setting of 1.5–35 m;
  - fills `DogSettings.Sounds`;
  - un-ignore `Settings_EveryDogSoundHasAnId`.
- Create: `AgentScripts/Dogs/DogPlayCheck.cs`: static `async Task<string>` probes in WorldMap Play, returning PASS or FAIL lines. Each probe moves the player by teleport and drives input through `InputService` stand-ins or a virtual gamepad, as `RobotPlayCheck` does.

  | Probe | Passes when |
  |---|---|
  | `Idle` | Loki stands at its house and idles |
  | `Follow` | the player walks 30 m; Loki ends within 8 m, never stops on the same frame as the player, and sits within 7 s of the player stopping |
  | `Scout` | a steady walk puts Loki ahead of the player |
  | `AimClear` | aiming at Loki → it leaves the cone within 1 s |
  | `Sprint` | Loki runs alongside |
  | `CatchUp` | teleport the player 60 m → Loki appears within 15 m, out of view, within 3 s |
  | `Swim` | walk through a stream → Swim on, then ShakeWater |
  | `FenceOut` / `FenceIn` | gate shut, the player outside → Loki crosses by a DogOnly link both ways |
  | `Indoors` | Loki follows the player into the house and back out |
  | `Stay` | wheel Stay 20 m out → Loki keeps within 4 m; at night it walks home |
  | `Whistle` | plain whistle from 40 m → Loki comes to heel |
  | `WhistleSpam` | spam → annoyed, then ignored; a pet forgives |
  | `WhistleRobot` | aim at Pip → Pip answers, not Loki |
  | `Pet` | hold E → Loki comes and sits, happy |
  | `HeadTilt` | look at Loki → head tilt |
  | `Wheel` | tap E → the wheel opens, time 0.3 → pick Stay → closed, time 1, input released |
  | `WheelInterrupted` | the wheel open, then the player dies → everything released |
  | `NightHouse` | 23:00 at home → Loki sleeps in its house |
  | `FriendlyFire` | shooting Loki → yelp, flinch, no damage event |
  | `AfterLoad` | save, move, load → Loki is where it was, with the same order |
  | `Frame` | captures from the player camera of following, sitting and the house |

- Create: `docs/technicals/DOGS.md`, laid out like ROBOTS.md: How it plays (Phase 1), Rules table, Pieces, In WorldMap, Tuning, Setup and checks, Known gaps.
- Modify: `docs/technicals/CODE_STRUCTURE.md` (DogSystem, Wheel, the tests), `AGENTS.md` (docs list: DOGS; the UI map's new action), `UI.md` (the wheel node), `CONSOLE.md` (dog commands), `SAVE.md` (the section list), `ROBOTS.md` (the whistle order and the nav-tag change).

- [ ] **Step 1:** Run `DogSetup.Audio`. Expected: `Settings_EveryDogSoundHasAnId` PASSes.
- [ ] **Step 2:** Enter Play in WorldMap and run every probe. Expected: all PASS. Fix and re-run any FAIL, then report each line.
- [ ] **Step 3:** Run the full EditMode suite (`test run --mode editor`). Expected: only the 4 known COZY calendar failures. Recompile clean; `console status` shows 0 errors.
- [ ] **Step 4:** Report to the user what was verified and what wasn't (look and feel are theirs to judge), and send the colour-variant sheet. Don't commit.
