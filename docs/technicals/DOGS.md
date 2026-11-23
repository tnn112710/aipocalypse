# Dog companion (Loki)

Loki, a Border Collie, lives at its dog house in the WorldMap yard and comes exploring with the player. It follows like a dog, not a robot on a leash. Code in `_Core/DogSystem/` (namespace `MB13.Aipocalypse.Core.DogSystem`), the command wheel in `_Core/UI/Wheel/`. Design: `docs/superpowers/specs/2026-11-23-dog-companion-design.md`; plans per phase in `docs/superpowers/plans/2026-11-23-dog-companion-phase*.md`. **All three phases are built**: a living dog, fighting, the farm.

## How it plays

1. **Following** (order Follow, the default):
   - **Comfort zone:** within 3–8 m of the player it's free. Farther, it trots back to a point 3 m off the player on its side; past 15 m it sprints; past 40 m, or with no path for 3 s, it appears 10–15 m behind the player, out of sight.
   - **Reaction delay:** it reacts to the player starting or stopping after 0.3–0.8 s, never on the same frame.
   - **Scouting:** on a steady walk (2 s one way) it runs 4–7 m ahead, sprinting when the goal is more than 8 m off.
   - **Side and aim:** it keeps a side and swaps every 20–40 s. While the player aims, it leaves a 12° cone (+2 m) round the camera's line.
   - **Sprinting:** it sprints alongside the player.
   - **Separated:** a fence or wall between them (one in the yard and the other not, or on a different floor) brings it back even when close.
2. **Free time:**
   - **Sniffing:** sniff walks to spots within 6 m (inside the zone), sometimes a dig, or (outside the yard) marking a spot.
   - **Looking round** now and then.
   - **Check-in** every 30–60 s: trots in front of the player and looks up happy, panting.
   - **Settling** while the player stands still: sits after 3–6 s, lies after 15–25 s, yawns and naps after 60 s.
   - **Head tilt:** looked at within 10 m for 1 s, it tilts its head (at most once per 20 s).
3. **Water:** in water deeper than 0.9 m (the streams; the spawn system's `IWaterLevel`) it swims, the model floating up so its back is at the surface; out again, it stops and shakes itself dry.
   - **Rain:** 20 s in rain heavier than 0.15 (the weather's rain amount) with nothing over it soaks it. Out of it (indoors, its dog house, any roof or canopy over it, or the rain stopping), it shakes itself dry the same way, once. A shorter shower dries off by itself (`DogWetness`).
   - **The shake** (after a swim or rain) waits up to 20 s for it to stand still, and counts as done only once the act plays: a sit or lie in the same moment would swallow the trigger.
   - **Acts** (bark, happy, head tilt, howl…) only play on its feet: `Dog.Act` drops one asked for while it sits or lies, and settling cancels one still waiting (it would otherwise go off seconds later once it's up, e.g. a happy wag holding it back from following). Sent off mid-act faster than 1.5 m/s, once the act has played 0.5 s, it cross-fades to its legs instead of sliding along in the pose (`WalkOff`; earlier it is still slowing into the act).
   - **Turning to face something** (`DogLegs.Face`): further round than `ArcTurnAngle` 35°, it walks a short arc, `ArcTurnStep` 1.2 m ahead in the new direction, arriving facing it (the agent leads in on an arc), the way a dog steps round instead of pivoting; only on its feet with navmesh to land on, and never farther from a spot it's keeping near (its stay point: `DogTurn.KeepsNear`). A settle asked for mid-arc (sit, lie) waits for the arc (`Dog.PendingPose`). Smaller turns, or ones with nowhere to step, are on the spot: the collie has no turn clips, so a turn faster than 50°/s while standing plays the walk at 1 m/s and its legs step round (`DogTurn.AnimatorSpeed`), at `FaceTurnSpeed` 150°/s (A* would turn a standing agent at its max rate). The fence scramble squares up on the spot (`FaceInPlace`).
   - **Stopping and setting off:** the agent eases over 0.9 s (`slowdownTime`), and the legs follow the real speed, so it takes a slower step or two instead of braking. Following, it speeds up a gait at a time (trot, canter, sprint), `GaitRamp` 0.4 s each; slowing down is immediate (`DogFollow.Ramp`).
   - **Standing about** for `IdleActAfter` 8 s (nothing going on: no fight, raid, call, pet or welcome), every `IdleActGap` 10–20 s it does one of `IdleActs` (by weight: a look round, a look left or right, an ear scratch, a yawn) (`DogManager.IdleActs`).
   - **Ears and tail** lag the body a little: an Animation Rigging `DampedTransform` per ear tip and tail bone on the model's Animator (`DogSetup.Jiggle`, a RigBuilder on the model prefab: one unique character, so it's allowed).
   - **Body** (`DogBody` on the model, `LateUpdate` after the Animator; built by `DogSetup.Body`):
     - **Planted paws:** while it stands still (sitting, lying, getting up, `PlantPaws`), a paw that touches down stays where and how it landed (a two-bone solve per leg: upper arm / forearm / hand, calf / hock / foot); one the pose pulls 10 cm away (at 1×) steps over to the clip's spot. The clips alone slide them 12 / 16 / 54 / 36 cm through a sit-down (at 2×); planted, under 1 cm (`DogPlayCheck.Body`). The legs are solved in code, not in the RigBuilder: a rig attached to the Animator has already run by `LateUpdate`, so the paws couldn't be read as animated.
     - **Head turn:** the neck and head (`Look` rig: Multi-Aim on `Neck2` 0.4 and `Head` 0.75, ±60°) turn toward its fight target, else the nearest foe, else the player's head while it's calm (slower than `LookCalmSpeed` 2.5 m/s), within `HeadLookRange` 10 m and `LookMaxAngle` 100° of its facing (`DogLook`); not mid-act, asleep, down or over the fence. `LookWeight` 0.85.
     - **Panting:** after running at `PantRunSpeed` 5 m/s or more for `PantAfter` 4 s (short dashes don't add up), it pants for `PantFor` 12 s: the jaw opens `PantJaw` 14° at `PantRate` 3 breaths/s, the tongue droops, and the pant sound plays every 3.5 s (`DogPant`). Not while swimming or down. The pack has no panting clip.
   - **Turning while moving:** 200°/s, at most 260°/s, the drawn rotation smoothed over 0.2 s: a big dog turns in arcs. Link jumps and the fence crossing turn it at 360°/s at most, and it squares up to the fence before the leap (`DogScramble`).
4. **Jumping:**
   - **Leaping** (user, 2026-11-24): a fence, wall or surface from 0.6 m up to `LeapHeight` 3 m is leapt over or onto (`JumpKind.Leap`): it stands squared up `LeapPause` 0.4 s, then a standing jump (`Dog_Jump_Walk`: a crouch, 0.38 s, then 0.89 s in the air) lands it on the far side or on top. The body follows the clip's own timing (`DogJumpArc.LeapTakeOff` / `LeapFlight`, checked against the clip by `DogTests`). Taller walls are scrambled over (paws up, climb, drop: `ScrambleGrab` / `ScrambleClimb` / `FromEdge`, timed the same way) and taller steps climbed (the ledge clip by height over the model's scale); a drop past 0.6 m falls from the edge; below 0.6 m it hops. `DogPreview.Jumps` renders each as the game plays it.
   - **The fence:** cut off from a player within 25 m by the fence, it crosses by a crossing point (the 2 m fence: leapt): at once with no walkable way round, else after 2.5 s.
     - Only where the fence stands: each crossing knows its segment, and a fallen or not-yet-built segment or an open gate is a gap, never climbed. With a walkable way round, a gap at most `GapDetour` (10 m) longer than the way over is walked through instead of scrambling (`DogRoster.ScrambleCrossing`); a segment that falls while the dog trots to it ends the scramble.
     - The crossing points are the 53 `DogLinkInfo` markers in `Home Dog/Fence Links`, one per segment and the gate, each linked to its `BarrierSegment`.
     - The crossing is scripted (`DogScramble`, named for the scramble it was first): the model on the leap's arc; then the agent is placed on the far side.
     - There are no NodeLink2s: a link would join the shut yard's navmesh island to the outside for every agent, so raiders would stop breaking in.
   - **The house door:** it uses the robots' door link, walking to the sill and hopping the 1 m step.
   - Anywhere else it can't reach, it catches up by appearing near the player out of sight (a spot the player can walk to, retried every 0.5 s).
5. **The command wheel** (tap E on Loki; hold E pets it):
   - Slices: Follow, Stay, Pet. The current order is green.
   - While it's open, time runs at 0.3 and the cursor's direction from the centre (or the stick) picks a slice. Click or Submit takes it; Esc, right click, B or E close it.
   - A raid starting or the player dying closes it.
6. **Stay:**
   - **Told to stay:** it's a little sad for 6 s: a whine, it sits, then lies down watching the player go.
   - **In the yard,** Stay is home life: it strolls, sniff-walks and digs, trots over to its spots (by Bolt's garden, the house door, the gate, the herd's corner, the middle), sits or lies looking round, naps in its dog house doorway, and now and then does zoomies. It's never idle long.
   - **Outside the yard** it keeps within 4 m of the spot. At night, or when the player is over 150 m away, it walks home.
   - **Welcome home:** staying at home, with the player out of the yard for 60 s or more, when they come back in it runs over (canter) and greets them one of the `Welcomes` ways, by weight: a lap (a happy bark, one trot round them, stopping short if the lap runs into a wall; it ends 2.4 m away), a happy bounce, a roll, or a bark (these from about 3 m, facing them); then it sits facing them for 2.5 s. At most 25 s; in a raid, never.
7. **Night at home:** a yawn, then into its dog house to sleep with its head out until morning. Only the model goes in; the agent waits at the door, because the house is cut out of the navmesh.
8. **The whistle (Q):**
   - **Aimed at Loki,** or a plain whistle with nothing callable under the aim or heard (the robots come first) within 60 m: it comes to `CallDistance` 2.5 m in front of the player and sits facing them (a dog this size right at their feet was in their face), until they come within `ReachMe` 1.5 m of it or `CallWait` 6 s pass; then it follows. The fallback shows no prompt; aiming at Loki shows "[Q] Call Loki".
   - **Spam:** 4 plain whistles in 5 s annoy it. It shakes its head and walks over grumbling, then ignores plain whistles for 6 s. A pet forgives it. Whistles in a fight never count (Phase 2).
9. **Pet (hold E or the wheel):** it comes to `PetDistance` 2.5 m in front of the player, happy (a roll 1 in 4, or a head tilt), then sits; the player walks up to it.
10. **Shot by the player:** a yelp and a flinch by side. No damage, no hit marker.
11. **Bedtime:** following, when the player uses the bed it trots (canters from over 4 m) to the floor beside it and sits while they pick the wake-up time; when they lie down it lies down facing the bed and falls asleep.
12. **After death or a night's sleep:** it's back at its dog house, following (it then comes to the player).
13. **Sounds** (`Data/Audio/Dogs.asset`, placeholders): bark (the collie's own), whine, yelp, pant, sniff, howl, growl, bite (a short snappy bark: the packs have no dog bite). `DogVoice` keeps them rare: one sound per 1.2 s; barks once per 6 s (the rest of a raid's burst skips that gap) and at most 3 a minute; whines once per 8 s.

## Fighting (Phase 2)

1. **Stats:** every fighting number is a `DogStat` read through `DogManager.Stats` (`DogStatSheet`).
   - The base values are in `Data/Dogs/DogStatBlock.asset`. A levelling system adds an `IDogUpgradeSource`; nothing else changes.
   - Weight classes are `DogSettings.Classes` (the stagger profiles Light, Medium, Heavy, Boss). It starts able to bite Light only. An enemy whose stagger profile isn't in the list (or has none) counts as the heaviest (`DogCombat.ClassOf`), so a new kind is never bitten until it's given a class.
2. **Early warning:** an enemy within 25 m that hasn't noticed the player, with no fight on: it gets up, faces it and growls (spatial), at most once per 30 s.
3. **Who it fights:**
   - **Active** (the default): enemies aware of the player within 15 m of the player. Ranged first, then those attacking the player, then the nearest. It never bites an unaware one (no spoiled sneaks).
   - **Passive:** never on its own; it stays at heel.
   - **Q aimed at an enemy:** sends it at that one in either stance, up to 30 m from the player. It gives up 10 s after its last bite.
   - **A plain Q in a fight:** "back off". It stays at heel until the fight ends (5 s with no aware enemy within 20 m).
4. **Bite and run** (`DogSkirmish`):
   - **Circle:** it circles at 5 m in its fighting stance, growling, with a feint now and then.
   - **Dart in:** at an opening (the enemy faces away more than 60°, is swinging at the player, or it has circled for 1.5 × its 8 s cooldown), it darts in.
   - **Bite:** 12 damage, a 0.9 s stagger with the hit reaction.
   - **Out:** it dodges and runs out at 7 m/s to circling range.
   - **No room** to circle (fewer than 3 of 8 points reachable): it waits by the player between bites.
   - **Too big to bite:** above its class it circles and barks only.
5. **The bite lands outside the tree:** a bite is dealt by `DogManager` after the tree's tick. A kill inside a Behavior Designer task despawns the enemy's agent, a structural ECS change BD's systems forbid.
6. **Takedown:**
   - **Chance:** each bite rolls (`RandomService` stream "Dog") `TakedownChanceBase + TakedownChanceBonus × (1 − health)` for an enemy within `MaxTakedownClass`, with a 20 s cooldown. It starts at 0: off until upgraded.
   - **On a takedown:** the enemy is killed with the push pointing back at the dog, so it falls toward it, dragged down. The dog pins it for 1.5 s (the Pin bite loop), with a tiny hit-stop.
   - **Testing:** `dog.takedown 0.05 0.25` turns it on.
7. **Enemies fight back only when provoked:**
   - A bite gives the bitten enemy, and enemies within 3 m (`RetaliationRadius`, flat), 2.5 s of `EnemyBrain.Retaliate`: they may pick the dog (`TargetKind.Dog`, at most 2 attackers).
   - That holds only while it's within their melee reach (+0.5 m) or a thrower's range. A bitten archer shoots from where it stands.
   - They never chase. The dog is not in `TargetRegistry`, so nothing picks it by weight.
8. **Getting hurt:**
   - **Toughness** 100. A swing while it's escaping misses 35% of the time (a dodge).
   - **At 0 it's down:** it falls by side and lies whining, untargetable. "Hold: Help up Loki" puts it on its feet at 30% and limping for 30 s (it fights only when sent). After 90 s it gets up by itself.
   - **Regen:** 2 per second after 10 s unhurt.
   - **Retreat:** below 25% (or limping) in a fight, it backs off behind the player, whimpering.
9. **The player hurt:** below 30% health, it keeps close and whines now and then.
10. **The wheel's stance slice** shows Active or Passive and switches it.
11. **HUD:** `SafeFrame/BottomLeft/DogBar`, a `StatBar` with the paw icon (`Data/UI/DogToughness.asset`). Hidden (user, 2026-11-24): `DogSettings.ShowBar` off.
    - With `ShowBar` on, it shows while it follows within 40 m, fights or is down.
    - **Minimap:** a small teal paw (`Data/Map/Dog.asset`, a `MapMarker` on the dog prefab, set up by `DogSetup.Prefabs`) while it's within the minimap's range; not pinned to the rim, where it would sit on the home icon while it stays at home.
    - Warning colour below 30%.
12. **Saved:** stance, toughness, down, and limp time left.

## The farm (Phase 3)

1. **Herding** (a wheel slice, shown with an `AnimalInstaller`; greyed when every animal is home):
   - It rings the herd home (`HerdOrders.Call`, like the bell).
   - It runs behind the animal farthest from the pen (from the pen's side, 3 m back), barks within the budget, and hurries it (`Animal.Hurry`).
   - It skips an animal that hasn't got closer for 20 s.
   - When all are home, or after 3 min, it stays at home. A save keeps how long it had been herding: after a load it carries on with the time it had left.
2. **Robots:** a free robot within 4 m (`Robot.IsFree`: not asleep, called, chatting, stepping aside, working…) greets it once per 90 s (`Robot.TryGreetVisitor`: it faces it, waves, heart eyes, a happy beep), only while the dog is in home life. The dog trots over and sniffs it.
3. **Sheep chase:** at home by day, every 3–6 min, it runs at the nearest sheep within 25 m (never one going home or asleep).
   - It startles the sheep every 0.5 s for 5 s (`Animal.Startle`, run), then comes back happy.
   - The cheat is `dog.chase`.
4. **Howl:** at most once a night, a `HowlChance` (20%) roll between 22:00 and 02:00 while it sleeps in its house, never in a raid: it backs out to the door, howls, and goes back in. The night whose roll is used is saved. The cheat is `dog.howl`.
5. **Raids at home** (user's rule):
   - **Barking:** it barks toward raiders in bursts of 2–3 (1.3 s apart), 8–15 s apart, within the 3-a-minute cap.
   - **Active:** it goes for raiders that got inside the yard, aware of the player or not, by the Phase 2 rules. While it's in the yard it leaves raiders outside the fence alone (barks only), even when following the player.
   - **Passive:** it keeps 6 m away from raiders, running to the far side of the yard, and never bites.
   - It doesn't sleep during a raid: at home it stands alert where it is, facing the nearest raider it sees (`DogWatchRaid`).

## Pieces

```
Dog                  the entity bridge: tap E (wheel), hold E (pet), IWhistleTarget, INameTagged (Loki, paw icon), IDamageable
                     (flinch only); animator parameters; the model offset (into its house) and swim float
DogManager           ITickable + IWhistleFallback: night, the player (position, velocity, look, still time) into the roster,
                     follow plans, swim, head tilt, the wheel's slices, whistles, sounds (DogVoice), home after death / sleep
DogState             plain C#: order (sad after Stay), call, whistle spam, pet, head tilt, settle pose (DogStateTests)
DogFollow            plain C#: where to be (comfort zone, reaction delay, scout, side, aim cone, catch up, separated) (DogFollowTests)
DogVoice             plain C#: the noise budget (DogStateTests)
DogJumpArc           plain C#: hop / scramble / climb up / drop down, body path and time (DogTests)
DogTasks             Behavior Designer tasks (AgentScripts/Dogs/Dog.tree.txt → Content/Dogs/_Shared/Dog.asset)
DogLegs              EnemyMover on every nav tag; gaits
Links/DogJump        IOffMeshLinkHandler on the dog's FollowerEntity (own asmdef DogLinks); DogLinkInfo on a link: obstacle, Via
DogHouse, DogRoster, DogInstaller, DogSaveSection ("dog": order, position, facing, in the yard, stay point), DogCommands
UI/Wheel             CommandWheelPresenter (time 0.3, UI input, closes on death / raid), CommandWheel view, WheelMath, CommandWheelStyle
```

Tree (priority): down → over the fence → come when whistled → petted → sad (just told to stay) → catch up → keep away from raiders (Passive at home) → back off (hurt) → fight → warn → stay close to a hurt player → welcome home → herd → bedtime → follow → stay out in the world → a raid at home: watch → night at home: sleep in its house → home life.

Behavior Designer calls `OnEnd` a second time on a finished task when a later abort interrupts the tree, so a task whose `OnEnd` changes shared state guards it with a `_started` flag (`DogWelcome`, `DogWarn`, `DogSkirmish`, `DogFollowPlayer`, `DogStayHere`, `DogSleepInHouse`, `DogHomeLife`; `DogScramble` guards on `Scrambling`). Unguarded, home life's would pop it out of its house mid-sleep. A welcome home still pending is called off once it stops staying home (told to follow, a raid).

## In WorldMap

`Home Dog` scene root (built by `DogSetup.World`):
- `DogHouse`: the SICS `TFP_Dog_House_01B` at (265.8, 29.5, 288.9), its opening toward the middle of the yard, with `Door`, `Bed` and a `NavmeshCut` box.
- `Loki` (`Content/Dogs/Loki/Loki.prefab`).
- `Visits`: garden, door, gate, pen, middle.
- `Fence Links`: 53 crossing points (`DogLinkInfo`, 2 m: Outside and Inside markers), one per fence segment and the gate, for the scripted scramble. No `NodeLink2`s (see Jumping).

Also:
- the robots' `DoorLink_House` gets a `DogLinkInfo` with Via = its Sill;
- `DogInstaller` sits on the `GameLifetimeScope`;
- AstarPath names tag 2 "Dog only".

## Tuning

`Data/Dogs/DogSettings.asset`:
- **Speeds:** walk 1, trot 2.4, canter 4.8, sprint 9.5 m/s. The clips' own paces at its size are 0.95 / 2.31 / 5.95 / 7.5 / 9.83 (the blend thresholds), and the sprint plays up to 12.3 m/s faster.
- **Following:** the zone radii, delays (the sprint one `SprintReaction` 0.6–1.2 s before it matches the player breaking into a sprint), the gait ramp, lead, side swap, aim cone, sniff radius, settle times, check-ins, idle acts, swim depth, look.
- **Whistle:** range, the call distance, reach and wait, the pet distance, spam.
- **Stay:** radius and go-home distance.
- **Turning, rain, welcome home:** turn rate and step speed, the face turn speed, the arc turn angle and step, how heavy rain soaks it and for how long, how long the player is away before a welcome, how long it lasts and the greeting weights.
- **Wheel:** time scale.
- **Voice:** gaps.
- **Sounds:** the SoundIDs.

## Setup and checks

- **Size:** the model at `DogSetup.ModelScale` 2 (user, 2026-11-23): 1.3 m to the ear tips standing (0.66 m at 1×, a real collie's size). The clip ground speeds (the Move / Sniff / Combat / Swim / Limp blend thresholds), the agent, the body collider, the dog house (its door and bed spot) and the fence crossing points scale with it; body heights in code (name tag, swim float `SwimBodyDepth`, look and sound points), `SwimDepth` and `TurnStepSpeed` are set for 2.
  - **Cadence:** a bigger animal steps slower, so every moving clip plays at `DogSetup.Cadence` 0.85 with its threshold scaled to match (its feet's ground speed: no sliding). The walk is in the Move blend twice, the second at a crawl (0.3×), so the long idle mixes in only near a standstill (a blend syncs its clips' cycles: the idle dragged the walking legs slower than the body). A new size: change `ModelScale`, rerun `Controller`, `Prefabs`, `World` (WorldMap freshly opened: a scene saved after play mode keeps the sky's runtime ambient and sun colours), and scale those values.
- **Setup:** `AgentScripts/Dogs/DogSetup.cs`: `Data`, `Model` (a look-only copy of the Malbers Border Collie PolyArt, no Malbers scripts, root motion off), `Controller`, `Prefabs`, `Hud` (the wheel in HUD.prefab, the paw icon), `World`, `Audio`; `All` runs the first five.
- **Clips:** the collie's clips move their Root bone (the whole body) as root motion. Played as they are, the Animator drops that and puts the body wherever its default lands (it was 2 cm forward and 7 cm sideways of standing at Loki's size), so `DogSetup` writes a copy of every clip to `Content/Dogs/BorderCollie/Animations` with the body placed on one spot over the agent: `Standing`, the vendor's standing Root offset (z 0.237; sitting down starts there, standing up ends there, the acts stand there). Loops lose their forward drift and are centred on it, their bob kept; one-shots are held on it (jumps and ledges: the code moves the body); a clip that doesn't move the Root is pinned on it (`PlaceRoot`). The pose chain (sit, lie, sleep and their transitions) and the knockdown (fall, get up) keep the Root as authored (`keepRoot`): sitting puts the body back over planted paws and the knockdown shifts it sideways, each clip starting where the last ended, from and back to `Standing`. Before, locomotion clips sat up to 5 cm off standing, a hop's landing 4.8 m forward and the climb-up 43 cm back, and sitting slid the paws forward. The bite used both ways has two copies (`Dog_Attack_Bite_Pin` for the loop).
  - **Size:** the vendor imports the clips uncompressed (a key per bone and frame), so the copies are thinned (`DogSetup.Reduce`): a key goes when the curve from its neighbours' own values and slopes stays within 0.0015 (quaternion component) / 1 mm of the original, checked at every key and halfway between. The folder is 169 MB (1.1 GB unthinned; Look Around 194 MB → 5 MB); every bone but Root stays within 0.34° and 1.5 mm of the vendor clips. Most of what's left is the editor-only float and Euler copies Unity writes into every text `.anim`.
- **Clip check:** `AgentScripts/Dogs/DogClipCheck.cs` (editor, no Play): `Roots` lists where every state's clip puts the body and flags `OFF` any non-pose clip off `Standing` or played straight from the vendor; `Accuracy` samples every copy against its vendor clip (every bone but Root; 2026-11-24: within 0.34° and 1.5 mm); `Paws` measures paw slide through the sit / lie transitions with the Root kept against held.
- **Previews:** `AgentScripts/Dogs/DogPreview.cs` renders review sheets (colours, poses, a prop from four sides, `Jumps`: every jump as played, the real controller stepped beside a box of the obstacle) to a path outside Assets.
- **Play probes:** `AgentScripts/Dogs/DogPlayCheck.cs`: Ready, Follow, Scout, CatchUp, Fence, Scramble, Indoors, Stay, HomeLife, Whistle, WhistleSpam, WhistleRobot, Pet, HeadTilt, Wheel, WheelInterrupted, NightHouse, FriendlyFire, AfterLoad, Swim, Shot, LookAtDog; fighting: Skirmish, Takedown, Sic, WhistleAtDogNearEnemy, BackOff, Down, Warn, WarnInterrupted (spawn goblins with `enemy.spawn`, the player invulnerable); the farm: Herd, RobotGreet, RobotGreetBusy, SheepChase, RaidHome active / passive, RaidNight; polish: TurnInPlace, RainShake, WelcomeHome, Bedtime, SpuriousEnd (each guarded task's `OnEnd` called without a start changes nothing), NightHowl, HerdAfterLoad.
  - Probe traps: Follow takes 20 m (12 m ends under the house floor: `Ground()` keeps terrain hits only); Swim needs a radius of about 350 m to find a wadeable stream; WarnInterrupted needs 30 s since the last warning (WarnGap); SheepChase needs the herd out; Herd needs 90 s when the animals are far out; a real raid at 21:00 kills an idle player (a dead one isn't looked at or followed): probes that need the player start at 10:00 with enemies cleared; Follow, HeadTilt and Takedown wait for it to get up (a still player has a dog dozing off within seconds); RainShake holds the weather with the console's `weather.set` (the weather rules replace a plain `WeatherService.Set`).
  - All passed on 2026-11-23 (Phases 1–3, then the polish and fix passes). NightHouse stops a raid if one starts at 23:00.
- **Motion check:** `AgentScripts/Characters/MotionWatch.cs` (dog, animals, robots) records every frame while probes run: the fastest turn as drawn, snaps, turn wobble, animation freezes, cuts without a blend, and travelling in a single-clip state.

## Vendor update (Malbers Border Collie)

Updating or reimporting the pack (or changing its import settings) needs the dog's clips rebuilt and rechecked. What
`DogSetup` assumes about the pack, and how to check each:

1. **Rebuild:** `DogSetup.Model` (look-only copy + ear / tail rig), `Controller` (every clip copy and the controller),
   `Prefabs`. The copies are regenerated from the vendor clips, so nothing in `Content/Dogs/BorderCollie/Animations` needs
   keeping by hand. Delete copies a renamed or removed vendor clip leaves behind.
2. **Body placement:** `DogClipCheck.Roots` must show no `OFF` line. It relies on:
   - **`Standing` (z 0.237):** the vendor's standing Root offset, read off `Dog_Sit_From_Idle`'s first frame (also
     `Dog_Sit_to_Idle`'s last, the acts', `Dog_Death1_*`'s first). A pack whose standing offset moved needs `Standing`
     updated, or every clip lands off the sit chain.
   - **The pose chain and knockdown** keep their Root (`keepRoot`), so each must start where the last ended. Check the
     start / end pairs in `Roots`: SitDown ends where Sit starts, Sit where SitToLie starts, and so on, and the standing
     ends sit on `Standing`.
   - **Root motion:** the clips' Root is the motion node, so a vendor clip played as is drops its Root position. That's
     why every state uses a copy. A clip added to `DogSetup.Controller` without `Source` brings the problem back.
3. **Size and accuracy:** the vendor imports with animation compression off and resampled curves (a key per bone and
   frame). `Reduce` thins the copies; `DogClipCheck.Accuracy` should stay within about 0.35° and 1.5 mm. A pack that
   imports compressed or sparse curves is fine: `Reduce` keeps their slopes and checks between keys. Check the folder
   size (169 MB on 2026-11-24); a 100 MB+ file means `Reduce` didn't run.
4. **Bone names:** `Root` (the motion node), `Ear01/02 L/R`, `Tail01`–`Tail06` (`DogSetup.Jiggle`), the legs
   `L/R UpperArm`, `Forearm`, `Hand`, `Finger0` and `L/R Calf`, `HorseLink`, `Foot`, `Toe0`, `Neck2`, `Head`, `Jaw`,
   `Tongue`–`Tongue03` (`DogSetup.Body`; both throw on a missing bone). The head's forward is local +Y on this rig
   (`Body` picks the aim axes from the bind pose).
5. **Jump timing:** the leap's takeoff and touchdown (`DogJumpArc.LeapTakeOff` / `LeapFlight`) and the scramble's
   (`ScrambleUpTime` / `ScrambleDownTime`) are read off the clips; `DogTests.Controller_LeapClipMatchesTheBodyPath` and
   `Controller_ScrambleClipsMatchTheBodyPath` fail when a new pack moves them. Check `DogPreview.Jumps` by eye.
6. **Clip names and loops:** the states name vendor files and takes (`Source`); a renamed take throws "no clip". Two
   states using the same take one looping and one not need their own `copyName` (the bite: Attack 0 and `Pin`).
7. **Speeds:** the Move thresholds come from each locomotion clip's Root drift (`Controller` logs them: walk 0.96,
   trot 2.30, canter 5.95, run 7.50, sprint 9.82). If they change, retune `DogSettings` speeds.
8. **Play check:** Follow, Whistle, Pet, TurnInPlace, Scramble, Gap, Body, WelcomeHome (`DogPlayCheck`), and a look at the
   sit, the roll and a leap in game.

## Known gaps

- **Rolls:** dodges and takedowns use `RandomService`; behaviour flavour (feints, growls, activities) uses `UnityEngine.Random`.

- **Colour:** the colour is Black 01; the user picks from `DogPreview.Colours`.
- **Aim and sprint:** clearing the aim and running alongside are covered by EditMode tests, not play probes (the probes move the player by teleport).
- **Turning:** no turn-in-place clips (their Root rotation would fight the agent's turning).
- **Shake:** the shake-dry plays when it stops within 20 s of leaving the water.
- **Indoors:** it can go into the player's house while following (the robots' door), but home life never takes it inside.
