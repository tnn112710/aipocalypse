# Service Robots, Pass 1 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Two cute, lively service robots at home (garden and house) that idle, stroll, greet the player on E or a look, answer a Q whistle from up to 50 m without leaving the yard, react to being shot or aimed at, glow at night and beep — ready for menus later.

**Architecture:** A new self-contained `RobotSystem` scene system modelled on `AnimalSystem`: `RobotInstaller` registers settings, a `RobotRoster` and a `RobotManager` (ITickable + ILateTickable) that thinks each robot's Manual Behavior Designer tree and drives face, head look and nervousness. Each robot entity nests a first-party `Model` built from the vendor `Rob15` and walks the A* navmesh through `FollowerEntity` + `EnemyMover`. The player side only knows `IWhistleTarget`.

**Tech Stack:** Unity 6000.6, VContainer, MessagePipe, Behavior Designer Pro (TreeKit specs), A* 5.4 `FollowerEntity` + `EnemyMover`, BroAudio (`AudioService`, `SoundID`), Unity Localization CSVs, Input System (`GameInputActions`), NUnit EditMode tests.

**Spec:** `docs/superpowers/specs/2026-11-22-service-robots-design.md` (pass 1 = everything except Extras 3–7; pass 2 gets its own plan after the user plays pass 1).

## Global Constraints

- Code style and rules: AGENTS.md → Code style (`_camelCase` private fields, no `private`, `sealed` leaf classes, `[SerializeField] T _field` + read-only property, no XML docs, ≤ 3 comment lines per class/method, no Odin serialization, Unity 6.6 `Dictionary` serialization for keyed data).
- Namespace `MB13.Aipocalypse.Core.RobotSystem`, folder `Assets/_Game/_Core/RobotSystem/`; content `Assets/_Game/Content/Robots/`; data `Assets/_Game/Data/Robots/`; editor scripts `AgentScripts/Robots/`.
- No per-robot `Update`/`LateUpdate`: `RobotManager` ticks everything. No vendor MonoBehaviour (`Rob15Ctrl`, `RobotColorManager`, `EmotionChanger`, `PushObjects`, `PushDetection`, `TextureScroller`, `CharacterController`) on any `_Game` prefab.
- Input only through `InputService`; Whistle = Player map action bound to `<Keyboard>/q` (gamepad: a free button, builder's pick, e.g. `<Gamepad>/leftStickPress` if unused).
- Whistle range **50 m** (`PlayerInteractionTuning.WhistleRange` default 50).
- Robot height **≈ 1.3 m** (model scale 1.3 / 1.76 ≈ 0.74).
- Robots never leave the yard and never path through the home gate; raiders never target them (never added to `TargetRegistry`).
- Hits: `Robot.TakeDamage` reacts and returns `default` (Applied 0) so `DamageService` publishes nothing (no hit marker); `Robot` does **not** implement `IShowsOwnHits` (no blood).
- Every player-facing string in `Data/Localization/Csv/*.csv`, English only (`vi`/`fr` empty).
- New `SoundID`s approved by the user: `Player_Whistle` (Malbers `Common/Audio/Whistle/Whistle.wav`) and the robot beeps (Gamemaster Audio Pro `Retro_Classic/Robot`, `Retro_Classic/Menu_UI_Beeps`, `Alarms_Beeps_Siren/beep_*`, `Sci-Fi`).
- Editor: save only what you wrote (`SaveAssetIfDirty`), never `AssetDatabase.SaveAssets()`. WorldMap's uncommitted removal of a vendor `Rob11` is intended and stays; revert any other unrelated scene hunks.
- No commits (AGENTS.md: commit only when asked). Each task ends with recompile clean + its tests green instead.
- Recompile / test commands (Git Bash): `P="$(cygpath -w "$PWD")"`, `unity command --project-path "$P" script recompile` then poll `script recompile_status`; `unity command --project-path "$P" test run --mode editor` (check `SceneManager.GetActiveScene().isDirty` first; if dirty, run the affected test classes by reflection, see memory "tests with a dirty scene").

## Review Focus

1. **The house robot blocked by the navmesh** (house interior not in the cached graph): expect the robot to reach its spot inside; Task 9 checks `GetNearest` at the house spot in Play and re-bakes (`Navigation.Bake`) if it misses.
2. **Whistle from outside the yard or through a wall**: expect the robot to come to the inside of the fence nearest the player (never out the gate), and a wall between player and robot to stop the call. Task 3 (`ClosestInside`) and Task 9 play check (whistle from 30 m outside, from 60 m, and through the house wall).
3. **Player standing in the doorway or walking into a robot**: expect the robot to step aside, never pin the player. Task 7 (`RobotStepAside`) + Task 9 probe.
4. **Shooting a robot**: expect a flinch and face, no hit marker, no blood, no damage number, no `DamageDealt`. Task 6 test `TakeDamage_ReactsAndAppliesNothing`.
5. **Beep spam** from several triggers at once: expect at most one beep per robot per `BeepGap` and one across both per `SharedBeepGap`. Task 3 `RobotBeeps` tests.

---

### Task 1: Whistle input (Q)

**Files:**
- Modify: `Assets/_Game/Settings/InputSystem_Actions.inputactions` (Player map: action `Whistle`, Button, bindings `<Keyboard>/q` group `Keyboard&Mouse`, one gamepad binding group `;Gamepad`) and regenerate `GameInputActions` (the asset's generate-C# setting; check the generated file changed)
- Modify: `Assets/_Game/_Core/InputSystem/InputService.cs` (add `public bool WhistlePressed => _actions.Player.Whistle.WasPressedThisFrame();` next to `GunLightPressed`)
- Modify: `Assets/_Game/_Core/PlayerSystem/PlayerIntent.cs` (`public bool Whistle;`), `Assets/_Game/_Core/PlayerSystem/Player.cs` `ReadIntent` (`Whistle = _input.WhistlePressed,`)
- Test: `Assets/_Game/Tests/EditMode/RobotTests.cs` (new file, class `RobotTests`)

**Interfaces:**
- Produces: `InputService.WhistlePressed`, `PlayerIntent.Whistle`.

- [ ] **Step 1: Failing test** `Input_WhistleIsBoundToQ`: load the actions asset, `FindAction("Player/Whistle")` not null, has a binding whose `path == "<Keyboard>/q"`; and no other Player action is bound to `<Keyboard>/q`.
- [ ] **Step 2:** Recompile, run `RobotTests` → FAIL (action missing).
- [ ] **Step 3:** Add the action, binding, generated wrapper, `WhistlePressed`, intent field, `ReadIntent` line. Update `docs/technicals/CODE_STRUCTURE.md` input line listing Player actions (add Whistle) and AGENTS.md's Player map list (add Whistle).
- [ ] **Step 4:** Recompile clean, `RobotTests` PASS.

### Task 2: Player whistle (sound + 50 m call)

**Files:**
- Create: `Assets/_Game/_Core/_Common/Interaction/IWhistleTarget.cs` (namespace `MB13.Aipocalypse.Core.Interaction`)
- Create: `Assets/_Game/_Core/PlayerSystem/PlayerWhistle.cs`
- Modify: `PlayerInteractionTuning.cs` (`[Header("Whistle")]` `_whistleRange` 50, `_whistleRadius` 0.35, `_whistleMask` (robots' layer + blockers: Default, terrain), `_whistleCooldown` 0.6 s; read-only properties `WhistleRange`, `WhistleRadius`, `WhistleMask`, `WhistleCooldown`), `PlayerSoundTuning.cs` (`[SerializeField] SoundID _whistleSound;` + `WhistleSound`), `Player.cs` (construct `PlayerWhistle` next to `PlayerInteractor`; call `Tick` each frame with `intent.Whistle`; skip while a menu holds input like the interactor)
- Audio: `Player` BroAudio library (`Assets/_Game/Data/Audio/Player`) gets entity `Player_Whistle` (the Malbers clip); assign `PlayerTuning`'s sound tuning field; run the audio preset `Apply` per tools-unity-broaudio
- Test: `RobotTests`

**Interfaces:**
- Produces:
  - `public interface IWhistleTarget { bool TryWhistle(GameObject actor); }` (true = it answers)
  - `public sealed class PlayerWhistle { public PlayerWhistle(Transform eye, GameObject actor, PlayerInteractionTuning tuning, AudioService audio, SoundID sound); public IWhistleTarget Tick(bool pressed, float time); }` — returns the target that answered, or null. Plays the whistle on every accepted press (cooldown), then one `SphereCast` from `eye` along its forward, `WhistleRadius`, `WhistleRange`, `WhistleMask`, `QueryTriggerInteraction.Ignore`, ignoring the actor's own colliders; first hit's `GetComponentInParent<IWhistleTarget>()`; a hit on anything else (a wall) = nobody.
  - `PlayerSoundTuning.WhistleSound`, the four tuning properties.

- [ ] **Step 1: Failing tests** (physics in an EditMode test scene, `Physics.SyncTransforms()` after placing):
  - `Whistle_CallsATargetWithinRange`: eye at origin facing +Z, a cube collider with a test `IWhistleTarget` stub at z = 30 → `Tick(true, 0)` returns the stub.
  - `Whistle_IgnoresTargetsBeyondRange`: stub at z = 60 → null.
  - `Whistle_IsBlockedByWalls`: wall cube at z = 10, stub at z = 30 → null.
  - `Whistle_RespectsCooldown`: two presses 0.1 s apart call the stub only once.
  (A stub MonoBehaviour can't live in the EditMode assembly — use the PlayMode test assembly's pattern (`ControllerHitRelay`) or a test-only `IWhistleTarget` component added in the PlayMode assembly; if that is awkward, test the cast through a `PlayerWhistle.Cast(Vector3 origin, Vector3 dir)` static that returns the hit `Collider` and assert on it.)
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement `IWhistleTarget`, `PlayerWhistle`, tuning fields, sound entity, Player wiring. `AudioService.PlayAt(sound, eye.position)` (or the player's 2D path if one exists — check how `PlayerSoundTuning.JumpSound` is played and do the same).
- [ ] **Step 4:** Recompile clean; tests PASS; set the tuning asset's `WhistleMask` (Default, Terrain if separate, the Robot layer from Task 5) once Task 5 adds the layer (note it there).

### Task 3: Robot rules (plain C#, fully tested)

**Files:**
- Create in `_Core/RobotSystem/`: `RobotCall.cs`, `RobotBeeps.cs`, `RobotPersonality.cs`, `RobotCooldown.cs`, `YardMath.cs`
- Test: `RobotTests`

**Interfaces (Produces):**
- `public sealed class RobotCall { public bool Active { get; } public Vector3 MeetPoint { get; } public void Begin(Vector3 meetPoint, float now); public void End(); public bool ShouldLeave(float now, float playerDistanceToMeetPoint, float waitSeconds, float leaveDistance); }` — leave when `now - start >= waitSeconds` or the player is farther than `leaveDistance` from `MeetPoint` (only once the robot has arrived: `MarkArrived(float now)` restarts the wait clock; `public void MarkArrived(float now)`).
- `public sealed class RobotBeeps { public RobotBeeps(float robotGap, float sharedGap); public bool TryPlay(int robot, float now); }`
- `public struct RobotCooldown { public bool Ready(float now); public void Use(float now, float seconds); }`
- `public readonly struct RobotPersonality { public readonly float StrollChance, WaveChance, FlourishChance, Patience; public static RobotPersonality Roll(in RobotSettings.Temper baseTemper, float spread, float r1, float r2, float r3, float r4); }` — each value = base × (1 + spread × (2r − 1)); chances clamped 0–1, Patience ≥ 0.1.
- `public static class YardMath { public static Vector3 ClosestInside(Matrix4x4 worldToLocal, Vector3 halfSize, Vector3 point, float margin); public static bool Contains(Matrix4x4 worldToLocal, Vector3 halfSize, Vector3 point); }` — local XZ clamped to ±(half − margin), Y kept; result back in world space.

- [ ] **Step 1: Failing tests:**
  - `Call_LeavesAfterTheWaitOnceArrived` (wait 20: at arrived+19.9 false, +20 true), `Call_LeavesWhenThePlayerWalksAway` (leave 6: distance 6.1 true), `Call_DoesNotLeaveBeforeArriving` (no `MarkArrived`, t=+100, distance 0 → false).
  - `Beeps_KeepAGapPerRobot` (gap 1.5: robot 0 at 0 true, 1.0 false, 1.6 true), `Beeps_KeepASharedGap` (shared 0.5: robot 0 at 0 true, robot 1 at 0.3 false, at 0.6 true).
  - `Cooldown_ReadyAfterItsTime`.
  - `Personality_StaysWithinTheSpread` (base 0.5, spread 0.25: r=0 → 0.375, r=1 → 0.625), `Personality_ClampsChances` (base 0.9, spread 0.5, r=1 → 1).
  - `Yard_ClosestInside_PullsAPointOutsideToTheFenceMinusMargin` (identity matrix, half (10,1,5), point (20,0,0), margin 1 → (9,0,0)); `Yard_ClosestInside_KeepsAPointInside`; `Yard_Contains` both ways; a rotated case (yaw 90°).
- [ ] **Step 2:** Run → FAIL (types missing).
- [ ] **Step 3:** Implement. (`RobotSettings.Temper` is defined in Task 4; implement Task 3 and Task 4's `RobotSettings` together if order forces it.)
- [ ] **Step 4:** Recompile clean; tests PASS.

### Task 4: Robot data (settings, service, face set, text)

**Files:**
- Create: `_Core/RobotSystem/RobotSettings.cs`, `RobotService.cs`, `RobotFaceSet.cs`, `RobotEnums.cs`
- Create assets: `Data/Robots/RobotSettings.asset`, `Data/Robots/Services/Shop.asset`, `Data/Robots/Services/Upgrades.asset` (via the Task 5 setup script or by hand)
- Modify: `Data/Localization/Csv/Interaction.csv` (`verb.robot_shop` "Shop", `verb.robot_upgrades` "Upgrades"), `Creatures.csv` (`robot.shop` "Bolt", `robot.upgrades` "Pip" — placeholder names, English only; note "placeholder names" in the description column); reimport the CSVs per LOCALIZATION.md
- Test: `RobotTests`

**Interfaces (Produces):**
- `public enum RobotEmote { None, Hello, Talk, ThumbsUp, Laugh, DontKnow, No, Win, Dance, Jump }` (int value = animator `EmoteIndex`)
- `public enum RobotExpression { Neutral, Happy, Blink, Sleepy, Surprised, Sad, Angry, Talk, Worried }`
- `public enum RobotSound { Greet, Called, Happy, Shrug, Hit, Nervous, Wake, Chirp }` (pass 2 appends Welcome, Annoyed, Fear, Relief)
- `RobotSettings : ScriptableObject` (pure data, public fields allowed): `Enabled`, `ThinkInterval` 0.2, `Awake` (`TimeWindow` 06–20), `Temper Temper` (`[Serializable] struct Temper { StrollChance 0.25, WaveChance 0.6, FlourishChance 0.05, Patience 1 }`), `PersonalitySpread` 0.3, `IdleTime` (`Vector2` 4–9 s), `StrollRadius` 4, `TravelTimeout` 15, `DestinationAttempts` 8, `WalkSpeed` 0.62, `TrotSpeed` 1.3, `NoticeRadius` 5, `GreetHold` 2.5 s, `WaveCooldown` 60 s, `FaceTurnSpeed` (deg/s) 240, `ArriveDistance` 1.8, `CallWait` 20 s, `LeaveDistance` 8, `YardMargin` 1.2, `BumpRadius` 0.9, `StepAside` 1.5, `NervousRadius` 6, `NervousAngle` 6°, `NervousCooldown` 8 s, `HitHold` 1.2 s, `BlinkInterval` (`Vector2` 2.5–6 s), `BlinkTime` 0.15, `HeadYawLimit` 70, `HeadPitchLimit` 30, `HeadTurnSpeed` 6 (smoothing), `BeepGap` 1.5, `SharedBeepGap` 0.5, `Dictionary<RobotSound, SoundID> Sounds`.
- `RobotService : ScriptableObject`: `LocalizedString DisplayName`, `LocalizedString Verb`, `Sprite Icon` (pass 2 uses it).
- `RobotFaceSet : ScriptableObject`: `string EyesRenderer`, `string MouthRenderer`, `int Columns` (10), `Dictionary<RobotExpression, FaceFrame> Frames` where `[Serializable] struct FaceFrame { public int Eyes; public int Mouth; }`.

- [ ] **Step 1: Failing tests:** `FaceSet_HasAFrameForEveryExpression` (load every `RobotFaceSet` asset under `Content/Robots`; each enum value present; indices in 0..Columns−1), `Settings_HasASoundForEveryPassOneSound` (every `RobotSound` value has a non-empty `SoundID`), `Services_HaveNameAndVerb` (both service assets: `DisplayName` and `Verb` not empty).
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement the types and CSV rows. Assets are created in Task 5's setup script (`RobotSetup.Data`); face indices come from looking at the eyes / mouth atlas textures (`Art/Textures/Robots/...Emo*`), recorded in `Content/Robots/Rob15/Rob15_Face.asset`.
- [ ] **Step 4:** After Task 5 creates the assets, tests PASS.

### Task 5: Model, controller, tints and entity prefab (editor setup)

**Files:**
- Create: `AgentScripts/Robots/RobotSetup.cs` (static entries, run with `unity command script run --file AgentScripts/Robots/RobotSetup.cs --entry RobotSetup.<Step>`): `Data` (settings, services, face set), `Model`, `Controller`, `Prefabs`, `Audio`, `World` (Task 9)
- Create content: `Content/Robots/Rob15/Model/Rob15_Model.prefab`, `Content/Robots/Rob15/Rob15.controller`, `Content/Robots/Rob15/Materials/Rob15Body_Tint1.mat`, `_Tint2.mat`, `Content/Robots/Rob15/Rob15_Face.asset`, `Content/Robots/Robot.prefab` (base entity), `Content/Robots/Robot_Shop.prefab`, `Content/Robots/Robot_Upgrades.prefab` (variants: service + tint)
- Modify: `ProjectSettings/TagManager.asset` (new layer `Robot` in a free user slot); every LayerMask that includes `Animal` for interaction / bullets / melee gets `Robot` too (grep the tuning and settings assets for the Animal bit), plus `WhistleMask` (Task 2)
- Test: `RobotTests`

**Interfaces (Produces):**
- Model contract (characters skill §2): root holds the Animator (Generic avatar from this hierarchy, controller unassigned, `applyRootMotion` false, culling `CullCompletely`), scale 0.74, vendor LODGroup kept, `updateWhenOffscreen` off, **no vendor scripts, no CharacterController**. Eyes / mouth renderers keep the names `RobotFaceSet` lists.
- Controller parameters: `Speed` (float, m/s), `Turn` (float −1..1), `Emote` (trigger) + `EmoteIndex` (int = `RobotEmote`), `Hit` (trigger) + `HitIndex` (int: 0 front, 1 left, 2 right), `Standby` (bool). Base layer: `Locomotion` (2D or nested 1D: Speed 0 idle → walk clip at authored 0.84 × 0.74 ≈ 0.62 m/s → run clip at 1.75 × 0.74 ≈ 1.3 m/s; at Speed 0, Turn −1/1 → `turn90Left/Right`), `Emote_<n>` states from Any State on `Emote` with `EmoteIndex == n`, back to Locomotion at exit time, `Hit_<n>` likewise, `Standby` (idle clip, slow) while the bool is on. Clip map: Hello→`Rob15_EmoHello`, Talk→`EmoTalk1`, ThumbsUp→`EmoThumbsUp`, Laugh→`EmoLaught`, DontKnow→`EmoDontKnow`, No→`EmoNo`, Win→`EmoWin`, Dance→`Dance1b` (looped twice) , Jump→`Jump`; hits `getHitFront/Left/Right`. Use the in-place versions (root motion off).
- Entity prefab `Robot.prefab` (layer `Robot`): `FollowerEntity` (radius 0.35, height 1.3, turning like the animals' calmer settings), `EnemyMover`, `BehaviorTree` (Manual, `Content/Robots/_Shared/Robot.asset` from Task 7), `Robot`, `RobotFace`, a `CapsuleCollider` (r 0.35, h 1.3) for look / whistle / bullets, `TimeOfDayEmission` (eyes + mouth materials, soft cyan-white, Darkness source) for the night glow, `SaveId` not needed (not saved), `Model` child.
- Variants: `Robot_Shop` (`Robot._service` = Shop, body material Tint1), `Robot_Upgrades` (Upgrades, Tint2). Two clearly different colours (e.g. warm orange / mint); screenshot both for the user.

- [ ] **Step 1: Failing test** `RobotPrefabs_AreWired`: for each of `Robot_Shop`, `Robot_Upgrades`: has `Robot` with service + face set + head bone + animator assigned, `EnemyMover`, `BehaviorTree` with a subtree/asset, a collider on layer `Robot`; the model has no `CharacterController` and no MonoBehaviour from the vendor's `Scripts` folder (check by type namespace / assembly: none of `Rob15Ctrl`, `RobotColorManager`, `EmotionChanger`, `PushObjects`, `PushDetection`, `TextureScroller`); Animator `applyRootMotion` false, has every parameter listed above; model bounds height 1.2–1.4 m.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Write `RobotSetup` (follow `AgentScripts/Animals/AnimalSetup.cs` for the model copy, controller building, library creation; `CharacterKit.Describe` / `Snapshot` from `.agents/skills/aipocalypse-characters/scripts/CharacterKit.cs` to check). Run `Data`, `Model`, `Controller`, `Prefabs`. Snapshot each variant (idle, hello, walk) to the scratchpad and look at them.
- [ ] **Step 4:** Recompile clean; Task 4 + Task 5 tests PASS. ASSETS.md GrigoriyArx row: "In use: Rob15 model and clips for the service robots (first-party copies in `Content/Robots/Rob15/`)".

### Task 6: Robot entity runtime (interaction, whistle, hits, face, head look)

**Files:**
- Create: `_Core/RobotSystem/Robot.cs`, `RobotFace.cs`
- Test: `RobotTests`

**Interfaces:**
- Consumes: Task 2 `IWhistleTarget`; Task 3 types; Task 4 data; Task 8 `RobotRoster`, `RobotManager` (constructor-injected via `[Inject] Construct`, as `Animal` does).
- Produces (`Robot : MonoBehaviour, IInteractable, IWhistleTarget, IDamageable`, `[RequireComponent(typeof(BehaviorTree), typeof(EnemyMover))]`, no Update):
  - Serialized: `_service` (RobotService), `_face` (RobotFace), `_animator`, `_head` (Transform), `_spot` (RobotSpot, set by the scene).
  - State the tree reads: `RobotCall Call`, `bool Greeting` (until `GreetUntil`), `bool Hit` (until `HitUntil`) + `HitIndex`, `bool Nervous`, `bool Asleep`, `bool LookedAt`, `RobotPersonality Personality`, `Vector3 Destination`, `float NextThinkAt`, `EnemyMover Mover`, `RobotSpot Spot`, `RobotRoster Roster`.
  - Order: `Robot` and Task 8's `RobotRoster` / `RobotManager` / `RobotYard` / `RobotSpot` reference each other; write Task 8's class shells (constructors and the members listed there) before finishing this task, or do Tasks 6 and 8 as one unit.
  - Actions: `void StepAside(Vector3 from)` (moves `StepAside` m away to a point its spot `Allows`, at trot speed; ignored while called or greeting), `void Emote(RobotEmote emote)`, `void Express(RobotExpression expression, float seconds)` (0 = until changed), `void Beep(RobotSound sound)` (through `RobotManager.TryBeep(this, sound)`), `void FacePoint(Vector3 point)`, `void Greet(float now)`, `void Startle(Vector3 from, float now)`.
  - `TryGetPrompt`: `Verb` + `DisplayName` from the service (`InteractionPrompt(verb, name)`), always true while the system is enabled. `Interact`: `Greet(Time.time)` (menus later: leave one `// later: open the service menu` line).
  - `TryWhistle(actor)`: returns false when disabled; computes the meet point (player inside the yard → the player's position; else `RobotYard.ClosestInside(player, YardMargin)`), snaps it with `Mover.TryFindDestination(point, 2f, 0f, attempts, out p)`; none → false; else `Call.Begin(p, now)`, `Express(Surprised, 0.4)` then Happy, `Emote(ThumbsUp or Jump)`, `Beep(Called)`, true.
  - `IsDead` false; `TakeDamage(in DamageInfo info)`: `Startle(info.Point, Time.time)` (HitIndex from the side of `info.Point` relative to forward), `Express(Surprised, 0.3)` then `Angry` or `Sad` (personality roll), `Beep(Hit)`, returns `default`.
- `RobotFace : MonoBehaviour`: `[SerializeField] RobotFaceSet _set; Renderer _eyes, _mouth` (found by name on Awake), a `MaterialPropertyBlock` setting `_BaseMap_ST` = (1/Columns, 1, index/Columns, 0); `public void Show(RobotExpression e)`; `public void Blink(bool closed)`; no Update.

- [ ] **Step 1: Failing tests:**
  - `TakeDamage_ReactsAndAppliesNothing`: a `Robot` on a GameObject in the EditMode test (if MonoBehaviour creation fails in EditMode, put this in PlayMode like `ControllerHitRelay`): `TakeDamage(new DamageInfo(25, …))` returns `Applied == 0`, `Hit == true`; also through a real `DamageService` with a fake `IPublisher<DamageDealt>`: no publish.
  - `Robot_IsNotShowsOwnHits` (`typeof(IShowsOwnHits).IsAssignableFrom(typeof(Robot))` is false).
  - `Robot_PromptUsesItsService`: prompt verb and target come from the assigned service.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement `Robot`, `RobotFace`.
- [ ] **Step 4:** Recompile clean; tests PASS.

### Task 7: Behaviour tree (tasks + TreeKit spec)

**Files:**
- Create: `_Core/RobotSystem/RobotTasks.cs` (`[Category("Aipocalypse/Robots")]`, pattern of `AnimalTasks.cs`)
- Create: `AgentScripts/Robots/Robot.tree.txt` (header comment like `Animal.tree.txt`: job, aware of, told by, then, warns, gives up) → `Content/Robots/_Shared/Robot.asset` via `TreeKit.Build`
- Test: `RobotTests` (tree asset wiring)

**Tree (priority order, the first five Sequences `AbortType=Both`):**

```text
Repeater
  Selector
    Sequence: RobotIsHit → RobotHitReaction          (stop, hit clip, face; HitHold)
    Sequence: RobotIsCalled → RobotAnswerCall        (react 0.8 s, trot to MeetPoint, MarkArrived, face player, wait;
                                                      ShouldLeave → DontKnow shrug + Sad, Call.End)
    Sequence: RobotIsGreeting → RobotGreet           (halt, turn to player, Hello if WaveCooldown ready & WaveChance
                                                      else Talk + Talk face; hold while LookedAt or within NoticeRadius)
    Sequence: RobotIsNervous → RobotNervous          (halt, face player, Worried + No; NervousCooldown)
    Sequence: RobotIsNight → RobotGoToSpot → RobotStandby   (Standby bool, Sleepy face; wake on any branch above)
    Sequence: RobotAwayFromSpot → RobotGoToSpot
    Sequence: RobotIdle → RobotStroll                (idle IdleTime × Patience: small look-around turns, blinks,
                                                      FlourishChance → Dance or Laugh + Happy; then StrollChance →
                                                      a point within StrollRadius of the spot, inside the spot's
                                                      allowed area, walk there, idle, walk back)
```

- Step-aside (fix 1) is not a branch: `RobotManager` calls `Robot.StepAside(playerPosition)` (Task 8) like `AnimalManager` does with `BumpRadius`, which moves `StepAside` m away only to a point `RobotSpot.Allows(point)`.
- `RobotSpot.Allows(point)` excludes its `NoGo` boxes (the house doorway) — Task 8.

- [ ] **Step 1: Failing test** `RobotTree_IsBuiltAndAssigned`: `Content/Robots/_Shared/Robot.asset` exists, and the base prefab's `BehaviorTree` uses it (same check style as the animal tree test in `AnimalTests`).
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement the tasks; write the spec; `TreeKit.Check` then `TreeKit.Build`; assign on `Robot.prefab` (via `RobotSetup.Prefabs`).
- [ ] **Step 4:** Recompile clean; PASS.

### Task 8: Scene system (installer, roster, manager, spot, yard, commands)

**Files:**
- Create: `_Core/RobotSystem/RobotInstaller.cs` (`SceneInstaller`, partial, `SceneCollection` hook like `AnimalInstaller`; `Problem` needs EnemyInstaller + WorldTimeInstaller), `RobotRoster.cs`, `RobotManager.cs`, `RobotSpot.cs`, `RobotYard.cs`, `RobotCommands.cs` (dev builds: `robot.status`, `robot.call [name]` (as if whistled), `robot.raid`, `robot.welcome` — the last two log "pass 2" until then)
- Test: `RobotTests`

**Interfaces (Produces):**
- `RobotRoster` (scene service): `IReadOnlyList<Robot> Robots`, `Add/Remove`, `RobotYard Yard`, `bool HasPlayer`, `Vector3 PlayerPosition`, `bool Asleep`, `bool Enabled`, `RobotSettings Settings`.
- `RobotManager : ITickable, ILateTickable` (ctor: `RobotRoster`, `RobotSettings`, `WorldClock`, `PlayerRegistry`, `AudioService`): `Tick` → `Asleep = !Settings.Awake.Contains(clock hour)`; player position; per robot: step-aside within `BumpRadius` (not while called/greeting), `LookedAt = player.Interactor.Focus == robot`, nervous check (`player.HeldItem is HeldGun`, distance ≤ `NervousRadius`, angle between `player.View.forward` and the robot's head ≤ `NervousAngle`, cooldown), blink and expression timers, Animator `Speed` (`Mover.Velocity.magnitude`, damped 0.15) and `Turn`; think every `ThinkInterval` (`Tree.Tick()` as `Animal.Think` does). `LateTick` → head look (rotate `_head` toward the player's head inside `NoticeRadius` or while greeting / called, clamped `HeadYawLimit` / `HeadPitchLimit`, smoothed `HeadTurnSpeed`, weight faded; skipped when the Animator is culled / off screen). `public bool TryBeep(Robot robot, RobotSound sound)` through `RobotBeeps`.
- `RobotSpot : MonoBehaviour`: `Center`, `Radius` (stroll), `Box[] NoGo` (local boxes, the doorway), `bool Allows(Vector3 p)`, gizmo.
- `RobotYard : MonoBehaviour`: box (`_halfSize`), `Contains(p)`, `ClosestInside(p, margin)` via `YardMath`, gizmo.

- [ ] **Step 1: Failing tests:** `Spot_DisallowsItsNoGoBoxes`, `Spot_AllowsInsideItsRadius` (plain transforms, no Play); `Installer_ReportsMissingDependencies` (an installer without EnemyInstaller reports a `Problem`, like the animal installer test if one exists — else skip this one).
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement. Register in `RobotInstaller.Install`: `RegisterInstance(_settings)`, `Register<RobotRoster>(Singleton)`, `RegisterEntryPoint<RobotManager>().AsSelf()`, `RobotCommands` when `Cheats.Enabled`; build callback binds the yard and injects collected robots.
- [ ] **Step 4:** Recompile clean; PASS. CODE_STRUCTURE.md: RobotSystem block (every new script, one line each, style of the AnimalSystem block), the scope diagram line for `RobotInstaller`, `Content/Robots/`, `Data/Robots/`, test file line.

### Task 9: Robot sounds, WorldMap placement, navmesh and play check

**Files:**
- `RobotSetup.Audio`: a `Robots` BroAudio library in `Data/Audio/Robots` with one entity per `RobotSound` (2–3 clip variations each, random pitch ± a little) from the Gamemaster folders above; fill `RobotSettings.Sounds`; `Apply` the audio presets. List the chosen clips per sound in the hand-off for the user to listen to.
- `RobotSetup.World` (WorldMap open fresh from disk): new scene root `Home Robots` with `Yard` (`RobotYard` over the fenced yard, about 34 × 27 m, rotated with the yard), `Spot_Garden` and `Spot_House` (`RobotSpot`; the house spot's `NoGo` covers the doorway ±1 m), `Robots/Robot_Shop` (garden), `Robots/Robot_Upgrades` (house), each with `_spot` set; `RobotInstaller` on the `GameLifetimeScope` with `RobotSettings`, the yard; collect + save the scene; a `RobotCam_House` empty inside the house for screenshots (fix 4). Check `git diff` of WorldMap: only these additions plus the intended `Rob11` removal.
- Create: `AgentScripts/Robots/RobotPlayCheck.cs` (enter Play first; `runInBackground = true`; virtual gamepad for input per AGENTS.md)
- Docs: `docs/technicals/ROBOTS.md` (new: how it plays, rules table with the settings above, pieces, in WorldMap, tuning and adding a robot / model / service, setup and checks, known gaps); AGENTS.md → docs list adds `ROBOTS`; ROADMAP: note the robots under Trading.

- [ ] **Step 1:** In Play, check the navmesh at both spots (`AstarPath.active.GetNearest(spot, NearestNodeConstraint.Walkable)` within 0.5 m, same area as the player). If the house spot misses: stop Play, `BuildWaterVolume` is not needed — run `Navigation.Bake` (ENVIRONMENT.md), re-check.
- [ ] **Step 2: Play check** (`RobotPlayCheck.Run`), each a logged PASS/FAIL:
  1. both robots idle at their spots, stroll at least once in 3 game minutes, return; never inside a `NoGo` box;
  2. look at a robot within 3 m → it halts, faces the player (yaw within 15°), Hello or Talk plays; E → greet again;
  3. whistle (virtual press of Whistle) at Robot_Shop from 30 m inside the yard → arrives within `ArriveDistance`, waits, leaves after `CallWait` with DontKnow;
  4. from 30 m outside the fence → its meet point is inside the yard (`Yard.Contains`), within `YardMargin` + 1 m of the fence; it never passes the home gate;
  5. from 60 m → no answer; through the house wall → no answer;
  6. walk the player into a standing robot → it moves ≥ 1 m within 2 s;
  7. a shot (`ProbeCombat.Fire` pattern) at a robot → Hit state plays, no `DamageDealt` published (subscribe in the probe), no damage number;
  8. hold the AK47 pointed at a robot at 4 m → Worried face, No emote, once per `NervousCooldown`;
  9. set the clock to 23:00 → both go to their spots and standby, faces glow (emission weight > 0); 07:00 → awake;
  10. fire every trigger at once → beeps respect `BeepGap` / `SharedBeepGap` (count AudioService plays in the probe);
  11. console `robot.status` / `robot.call Bolt` work.
  Then stop Play; `console get --level error` shows nothing new.
- [ ] **Step 3:** Screenshots for the user: both robots at their spots (garden view, `RobotCam_House`), each tint, a close-up of the face (happy, sleepy at night). `capture screenshot`, never `--save_path`.
- [ ] **Step 4:** Full `test run --mode editor` green (except failures already present before this work — record them); recompile clean; docs updated.

---

## Self-review notes

- Spec coverage (pass 1): goals 1–6 → Tasks 1–9; fixes 1 (step-aside, NoGo) T7/T8, 2 (beeps) T3/T8, 3 (console) T8, 4 (house cam) T9, 5 (glow) T5/T9, 6 (nervous) T8/T9, 7 (personality) T3/T6. Extras 1 (head look) T8, 2 (beeps) T9. Extras 3–7 → pass 2 plan.
- Polish beyond the roadmap's Medium is user-approved (spec → Goals).
