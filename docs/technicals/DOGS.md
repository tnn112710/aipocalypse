# Dog companion (Loki)

Loki, a Border Collie, lives at its dog house in the WorldMap yard and comes exploring with the player. It follows like a dog, not a robot on a leash. Code in `_Core/DogSystem/` (namespace `MB13.Aipocalypse.Core.DogSystem`), the command wheel in `_Core/UI/Wheel/`. Design: `docs/superpowers/specs/2026-11-23-dog-companion-design.md`; plans per phase in `docs/superpowers/plans/2026-11-23-dog-companion-phase*.md`. **Phase 1 (a living dog) is built**; fighting (Phase 2) and the farm (Phase 3) follow.

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
   - **The fence:** it scrambles over every fence segment and the gate by dog-only links (`NavTags.DogOnly`, cost 3, so it prefers a gap or the gate nearby): paws up the 2 m fence, climb, drop off the far side.
   - **The house door:** it uses the robots' door link, walking to the sill and hopping the 1 m step.
   - Anywhere else it can't reach, it catches up by appearing near the player out of sight.
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
12. **Sounds** (`Data/Audio/Dogs.asset`, placeholders): bark (the collie's own), whine, yelp, pant, sniff, howl. `DogVoice` keeps them rare: one sound per 1.2 s; barks once per 6 s and at most 3 a minute; whines once per 8 s.

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
- **Play probes:** `AgentScripts/Dogs/DogPlayCheck.cs`: Ready, Follow, Scout, CatchUp, Fence, Scramble, Indoors, Stay, HomeLife, Whistle, WhistleSpam, WhistleRobot, Pet, HeadTilt, Wheel, WheelInterrupted, NightHouse, FriendlyFire, AfterLoad, Swim, Shot, LookAtDog.
  - All passed on 2026-11-23 (Phase 1).

## Known gaps

- **Colour:** the colour is Black 01; the user picks from `DogPreview.Colours`.
- **Aim and sprint:** clearing the aim and running alongside are covered by EditMode tests, not play probes (the probes move the player by teleport).
- **Turning:** no turn-in-place clips (their Root rotation would fight the agent's turning).
- **Shake:** the shake-dry plays when it stops within 20 s of leaving the water.
- **Indoors:** it can go into the player's house while following (the robots' door), but home life never takes it inside.
