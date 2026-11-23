# Dog companion (Loki)

Loki, a Border Collie, lives at its dog house in the WorldMap yard and comes exploring with the player. It follows like a dog, not a robot on a leash. Code in `_Core/DogSystem/` (namespace `MB13.Aipocalypse.Core.DogSystem`), the command wheel in `_Core/UI/Wheel/`. Design: `docs/superpowers/specs/2026-11-23-dog-companion-design.md`; plans per phase in `docs/superpowers/plans/2026-11-23-dog-companion-phase*.md`. **Phase 1 (a living dog) and Phase 2 (fighting) are built**; the farm (Phase 3) follows.

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
3. **Water:** in water deeper than 0.45 m (the streams; the spawn system's `IWaterLevel`) it swims, the model floating up so its back is at the surface; out again, it stops and shakes itself dry.
4. **Jumping:**
   - **The fence:** cut off from a player within 25 m by the fence, it scrambles over by a crossing point: at once with no walkable way round, else after 2.5 s (a gap nearby gets used first).
     - The crossing points are the 53 `DogLinkInfo` markers in `Home Dog/Fence Links`, one per segment and the gate.
     - The scramble is scripted (`DogScramble`): the model on the arc, paws up the 2 m fence, climb, drop off the far side; then the agent is placed on the far side.
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
7. **Night at home:** a yawn, then into its dog house to sleep with its head out until morning. Only the model goes in; the agent waits at the door, because the house is cut out of the navmesh.
8. **The whistle (Q):**
   - **Aimed at Loki,** or a plain whistle with nothing callable under the aim or heard (the robots come first) within 60 m: it comes to heel and follows. The fallback shows no prompt; aiming at Loki shows "[Q] Call Loki".
   - **Spam:** 4 plain whistles in 5 s annoy it. It shakes its head and walks over grumbling, then ignores plain whistles for 6 s. A pet forgives it. Whistles in a fight never count (Phase 2).
9. **Pet (hold E or the wheel):** it comes in front of the player, happy (a roll 1 in 4, or a head tilt), then sits.
10. **Shot by the player:** a yelp and a flinch by side. No damage, no hit marker.
11. **After death or a night's sleep:** it's back at its dog house, following.
12. **Sounds** (`Data/Audio/Dogs.asset`, placeholders): bark (the collie's own), whine, yelp, pant, sniff, howl, growl, bite (a short snappy bark: the packs have no dog bite). `DogVoice` keeps them rare: one sound per 1.2 s; barks once per 6 s and at most 3 a minute; whines once per 8 s.

## Fighting (Phase 2)

1. **Stats:** every fighting number is a `DogStat` read through `DogManager.Stats` (`DogStatSheet`).
   - The base values are in `Data/Dogs/DogStatBlock.asset`. A levelling system adds an `IDogUpgradeSource`; nothing else changes.
   - Weight classes are `DogSettings.Classes` (the stagger profiles Light, Medium, Heavy, Boss). It starts able to bite Light only.
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
   - A bite gives the bitten enemy, and enemies within 3 m, 2.5 s of `EnemyBrain.Retaliate`: they may pick the dog (`TargetKind.Dog`, at most 2 attackers).
   - That holds only while it's within their melee reach (+0.5 m) or a thrower's range. A bitten archer shoots from where it stands.
   - They never chase. The dog is not in `TargetRegistry`, so nothing picks it by weight.
8. **Getting hurt:**
   - **Toughness** 100. A swing while it's escaping misses 35% of the time (a dodge).
   - **At 0 it's down:** it falls by side and lies whining, untargetable. "Hold: Help up Loki" puts it on its feet at 30% and limping for 30 s (it fights only when sent). After 90 s it gets up by itself.
   - **Regen:** 2 per second after 10 s unhurt.
   - **Retreat:** below 25% (or limping) in a fight, it backs off behind the player, whimpering.
9. **The player hurt:** below 30% health, it keeps close and whines now and then.
10. **The wheel's stance slice** shows Active or Passive and switches it.
11. **HUD:** `SafeFrame/BottomLeft/DogBar`, a `StatBar` with the paw icon (`Data/UI/DogToughness.asset`).
    - It shows while it follows within 40 m, fights or is down.
    - Warning colour below 30%.
12. **Saved:** stance, toughness, down, and limp time left.

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

Tree (priority): come when whistled → petted → sad (just told to stay) → catch up → follow → stay out in the world → night at home: sleep in its house → home life.

## In WorldMap

`Home Dog` scene root (built by `DogSetup.World`):
- `DogHouse`: the SICS `TFP_Dog_House_01B` at (265.8, 29.5, 288.9), its opening toward the middle of the yard, with `Door`, `Bed` and a `NavmeshCut` box.
- `Loki` (`Content/Dogs/Loki/Loki.prefab`).
- `Visits`: garden, door, gate, pen, middle.
- `Fence Links`: 53 `NodeLink2`s, one per fence segment and the gate, tagged DogOnly with `DogLinkInfo` (2 m).

Also:
- the robots' `DoorLink_House` gets a `DogLinkInfo` with Via = its Sill;
- `DogInstaller` sits on the `GameLifetimeScope`;
- AstarPath names tag 2 "Dog only".

## Tuning

`Data/Dogs/DogSettings.asset`:
- **Speeds:** walk 0.9, trot 1.8, canter 3.8, sprint 7.5 m/s. The clips' own paces are 0.56 / 1.35 / 3.5 / 4.41 / 5.78; the blend tree plays them at their pace, and the sprint up to 8 m/s faster.
- **Following:** the zone radii, delays, lead, side swap, aim cone, sniff radius, settle times, check-ins, swim depth, look.
- **Whistle:** range and spam.
- **Stay:** radius and go-home distance.
- **Wheel:** time scale.
- **Voice:** gaps.
- **Sounds:** the SoundIDs.

## Setup and checks

- **Setup:** `AgentScripts/Dogs/DogSetup.cs`: `Data`, `Model` (a look-only copy of the Malbers Border Collie PolyArt, no Malbers scripts, root motion off), `Controller`, `Prefabs`, `Hud` (the wheel in HUD.prefab, the paw icon), `World`, `Audio`; `All` runs the first five.
- **Clips:** the collie's clips move their Root bone forward, so `DogSetup` writes in-place copies to `Content/Dogs/BorderCollie/Animations` (loops: the Root's drift removed; jumps and ledges: the Root held, the code moves the body).
- **Previews:** `AgentScripts/Dogs/DogPreview.cs` renders review sheets (colours, poses, a prop from four sides) to a path outside Assets.
- **Play probes:** `AgentScripts/Dogs/DogPlayCheck.cs`: Ready, Follow, Scout, CatchUp, Fence, Scramble, Indoors, Stay, HomeLife, Whistle, WhistleSpam, WhistleRobot, Pet, HeadTilt, Wheel, WheelInterrupted, NightHouse, FriendlyFire, AfterLoad, Swim, Shot, LookAtDog; fighting: Skirmish, Takedown, Sic, BackOff, Down, Warn (spawn goblins with `enemy.spawn`, the player invulnerable).
  - All passed on 2026-11-23 (Phases 1 and 2).

## Known gaps

- **The shut yard leaks:** `DefensePlayCheck.Run` reports a walkable way into the shut yard ("gate shut: path in True") even with the dog and the robots' door link switched off. It predates the dog; the fence scramble triggers after 2.5 s for this reason.
- **Rolls:** dodges and takedowns use `RandomService`; behaviour flavour (feints, growls, activities) uses `UnityEngine.Random`.

- **Colour:** the colour is Black 01; the user picks from `DogPreview.Colours`.
- **Aim and sprint:** clearing the aim and running alongside are covered by EditMode tests, not play probes (the probes move the player by teleport).
- **Turning:** no turn-in-place clips (their Root rotation would fight the agent's turning).
- **Shake:** the shake-dry plays when it stops within 20 s of leaving the water.
- **Indoors:** it can go into the player's house while following (the robots' door), but home life never takes it inside.
