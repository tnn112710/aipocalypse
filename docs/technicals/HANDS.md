# Hands

First-person hands on arms. Shared hands follow **grips** on the held item; each hand is the end of a Synty Sidekick arm (`ArmRig`) that reaches for the grip from a shoulder point off screen. AK47, MP5, pistol, pump shotgun and double barrel use animation clips and Animation Rigging constraints to drive their grips and finger shapes; other items (the axe) hold still grips. The AK47's reloads are retargeted from the KINEMATION FPS Animation Pack ([Retargeted reloads](#retargeted-reloads)) and its jam clearing is a procedural shake ([WEAPONS.md](WEAPONS.md#gun-jam-clearing-and-qte)); the other four guns still use hand-keyed clips. Script locations: [CODE_STRUCTURE.md](CODE_STRUCTURE.md). Weapons: [WEAPONS.md](WEAPONS.md).

## Weapon animation authoring

On all five guns, `ItemHands` points to the two `Motion/HandTargets` grips, whose rotations are used as authored. Their `_animatedShape` finger controls are keyed in each clip; the elbow rotation does not override them. The player's shared hand meshes still follow those targets, so a hand-mesh swap does not require new weapon clips.

`ItemHands > Hand Scale` adjusts hand size per item while keeping the palm grip point fixed on the target. Every item uses **1**: the arms carry their own size (the variant prefab's root scale, 1.2), so the hands stay the same size when switching items. The guns used 1.12 for the smaller floating hands; left on, it enlarged the arms a second time (1.34 in all, visibly too big for the guns), so it was reset on every item on 2026-11-16 and `ReloadRetarget.Bake` resets it on the gun it bakes. The floating hands, if switched back to, are therefore 11% smaller on guns than they used to be. Runtime placement and ghost previews both use this setting. Gun meshes keep their existing scale (the AK47 is about 1.2 m long, 1.38 times the real one). Large scale changes still need grip/finger-fit review. Trigger and magazine poses use authored curls instead of the old fit solver's partly open fingers.

`PosePivot/RecoilPivot/AnimationRoot` carries the Animator, RigBuilder and `GunAnimation`. Its `Motion` child contains `Weapon`, `Controls/LeftFree` and `HandTargets`. `Rig/LeftHand` is a Multi-Parent Constraint with five sources in this fixed order: forend grip, old magazine grip, replacement magazine grip, bolt grip, free-hand control. `Rig/RightHand` keeps the right target on the pistol grip. Contact switches are stepped keys, with matching free-control poses at each boundary. No hierarchy reparenting happens during playback.

The pistol's default left contact supports the right hand, and its bolt contact is on `Slide`. Shotguns use source 2 for the shell/shell-pair contact; unused magazine/bolt sources point to the forend. The pump's default contact is a child of `Pump`; the double barrel's is under `Hinge/Handguard`. Shell props stay active and their renderers are keyed, preserving constraint bindings while hidden.

Clips live in `Content/Weapons/<Gun>/Animations/`: `<Gun>_Equipped`, `_Idle`, `_Firing`, `_Reloading`, `_ReloadingEmpty`, `_Repairing`. The four later conversions also supply `_Cycling`; only the pump currently uses this action. Repairing means jam clearing. All five guns alternate an open-palm slap and a closed-fist punch on the receiver/slide (slap, punch, slap in the 4-second variant; slap, punch, slap, punch in the 5-second one), with an authored impact reaction at each contact; punches come from higher, land faster and shake the gun more. The left hand follows the free-hand control through wind-up, impact and recoil, then returns to the forend; the bolt and magazine stay at rest. Repair continues while the QTE runs and returns from the current pose after success. See WEAPONS.md for QTE and timing tuning. AK47/MP5/pistol empty reloads add a bolt/slide rack; normal reloads omit it. Firing moves the AK47 bolt or pistol slide and the trigger finger; MP5's charging handle stays still during firing. Accumulated gun and camera recoil remain procedural.

Pump `Reloading` and `ReloadingEmpty` each describe one complete shell insertion and reset to the forend at both ends. `Gun.ActionProgress` wraps for the next shell, so the clip repeats without adding a new gameplay action. `Cycling` moves the pump and bolt back and home while the left hand stays attached. Double-barrel reload opens the hinge, brings two shells from the belt to the chambers, hides them as they seat, then closes the hinge. Its muzzle marker follows the hinge. Shotgun tubes/barrels never detach as magazines.

To edit:

1. Open `Content/Weapons/<Gun>/<Gun>.prefab`, select `AnimationRoot`, and open **Window > Animation > Animation**. Select that gun's desired clip. Keep RigBuilder enabled for Animation-window constraint previews.
2. On the prefab root's `ItemHands`, use **Preview > Show hands** with default grips to show the shared ghost hands. Then scrub the clip in the Animation window; the ghosts follow the constrained targets. End the hand preview when finished.
3. Key `Motion` for whole-weapon gestures; `Motion/Weapon/Magazine`, `ReplacementMagazine`, `Bolt`, `Slide`, `Pump`, `Hinge` or `Shells` for the parts that gun has; `Motion/Controls/LeftFree` for independent reaches. Double-barrel shells live under `Hinge/Shells`. Grip contacts remain children of their parts.
4. Key `Rig/LeftHand > Source Objects > Weight` to select a contact. At a switch, make `LeftFree` coincide with the contact's position and rotation to prevent jumps. Key `Motion/HandTargets/Left > HandGrip > Animated Shape` to open and close fingers around contact.
5. Magazine visibility uses MeshRenderer **Enabled**, so hidden meshes do not disable their contact controls. On AK47/MP5/pistol, `GunAnimation` releases a pooled world magazine the moment the reload clip hides the original one, at the pose the clip left it in (MP5 and pistol: normalized time 0.3). A magazine the clip was throwing (faster than 1.5 m/s) keeps its speed; a held one drops with **Drop Velocity**. The replacement appears as the original hides and seats later; it resets on cancellation or the next idle frame. Shotgun shells show at 0.28 and hide at 0.73, without spawning a magazine.

`Content/Weapons/_Shared/Animations/Gun.controller` defines the base action slots, including Cycling, plus RepairingStubborn and RepairEnd; each gun's `<Gun>.overrideController` assigns its actual clips (AK47 does not need a Cycling override). `GunAnimation` selects states and samples the controller plus constraints manually in the player's tick, before hands and shots. During playback it detaches the controller from the Animator component and gives it exclusively to the manual graph, so Unity's later automatic Animator pass cannot restore Idle over the action. Stop/holster restores the component's controller for re-equipping and editor previews. Reload/repair/cycle progress comes from `Gun.ActionProgress`, so changing clip length does not change ammo, jam or pump rules. Equip/idle/fire and RepairEnd use clip duration. Early success captures current transforms and finger shapes, then blends into RepairEnd over its clip duration, skipping any remaining slaps. Keep the end of each repair clip at its normal grip/rest pose so completion can return cleanly from any progress. All clip-driven properties have baseline keys in every clip, including visibility and constraint weights; keep those reset keys when editing. The controller has no automatic transitions to compete with gameplay state.

For another gun, use the same generic control paths and a new Override Controller. Matching names make bindings reusable; different proportions and reload mechanisms still need fitting and often different clips. Reloads come from the retarget tool below where the pack has a matching weapon; anything else is authored by hand in the Animation window (steps above).

## Retargeted reloads

`AgentScripts/Hands/ReloadRetarget.cs` bakes a KINEMATION FPS Animation Pack reload onto one of our guns. Nothing in `_Game` references the pack: the output is our own `<Gun>_Reloading.anim` / `<Gun>_ReloadingEmpty.anim`, with the same bindings as the hand-keyed clips, so `GunAnimation`, the controller and the hands don't change.

```bash
unity command script run --file AgentScripts/Hands/ReloadRetarget.cs --entry ReloadRetarget.Bake --args '["AK47"]' --timeout 300
unity command script run --file AgentScripts/Hands/ArmReview.cs --entry ArmReview.Strip --args '["AK47","Reloading",12,""]'   # check it
```

What `Bake` does, per clip:

1. **Samples the pack** in a preview scene: its arms (`FPSPlayer.prefab`) with its gun on the weapon bone, the character clip and the weapon clip together, 30 keys a second.
2. **Fits our gun to the pack's.** The two guns are lined up at the pistol grip (the right hand holds the gun) and scaled by the middle of three measures: grip to magazine, grip to muzzle, magazine height. The report prints them; AK47 is 1.33.
3. **Left hand path:** the pack hand's grip frame (palm centre, the handle direction across the palm, the back of the hand: the same rule `ArmSetup` uses for our `GripPoint`) in gun space, mapped onto our gun, keyed on `Motion/Controls/LeftFree`. The hand rides that control for the whole clip.
4. **Contact corrections:** while the hand is on the magazine it is shifted by the difference between our magazine and the pack's; near the bolt, onto our bolt contact when the pack's hand comes within 12 cm of its charging handle. `Entry.ForendOffset` / `MagazineOffset` / `BoltOffset` are hand-tuned nudges per gun.
5. **Fingers:** per finger and key, the `FingerShape` (Curl, Spread, Tip) whose bones point most like the pack's, searched on the hand set's own rig. Shapes, not bone rotations: the clips work on any hand mesh.
6. **Magazine and bolt:** the pack's magazine motion relative to its seat drives `Magazine` until the swap (its far point, or the moment the hand throws it), then `ReplacementMagazine`. The charging handle's travel, as a 0 to 1 share, drives `Bolt` over the travel the old clip used.
7. **Gun tilt:** the pack gun's motion in view, in its own rest frame, on `Motion`, plus `Entry.PresentPosition` / `PresentEuler`: an extra lift eased in and out so the hands work in view with our low hip framing.
8. **Idle hold:** the first clip's first frame becomes the forend contact (`Handguard/LeftGrip`), `LeftFree`'s rest pose and the left grip's finger shape. `HandFit` then presses the palm onto our handguard and closes the fingers until they touch. Both clips start and end exactly on that hold (their ends are eased onto it), and the gun's other clips are rebased to it where they held the old values still.

To add a gun: add an `Entry` to `Table` in the script (pack weapon prefab, clip paths), run `Bake` (it first sets the gun's `ItemHands > Hand Scale` to 1), look at `ArmReview.Strip` and `Gun` renders (a tag with `@left`, `@top` or `@below` looks from outside), then tune the entry's offsets and present pose. The gun needs the animated prefab layout first ([Weapon animation authoring](#weapon-animation-authoring)): `Motion/Weapon/Magazine`, `ReplacementMagazine`, `Bolt`, `Controls/LeftFree`, `HandTargets`, `Rig/LeftHand`, and existing reload clips to replace. `ReloadRetarget.SourceStrip` renders the pack's own animation for reference; `AgentScripts/Hands/ArmPlayReview.cs` captures reload and jam clearing from the real player camera in play mode.

Not carried over: the pack's camera clips (the reload's view bob) and the right hand's small moves (it stays on the pistol grip).

### Which guns the pack covers

The guns in the game, matched by name; only the AK47 has been baked. Every gun in the Castle Bravo bundle is rated the same way in [CASTLE_BRAVO_GUNS.md](CASTLE_BRAVO_GUNS.md): check it before adding a gun. The FAL, M1 Carbine, AA-12, Uzi and hunting rifle were parked on 2026-11-16.

| Gun | Pack source | How hard | Why |
| --- | --- | --- | --- |
| AK47, AK-74M | AK | Done | AK-74M is a variant of the AK47 prefab and inherits its clips |
| MP5 | MPS5 | Easy | Already has the animated layout; the charging handle sits forward on the left, check the bolt contact |
| Pistol (.45) | M1911 | Easy to medium | Has the layout, but the part is `Slide`, not `Bolt`, and the support hand wraps the shooting hand: the tool needs the part name per gun |
| M4 | MX16A4 | Medium | A block-out: needs the animated layout and base clips first. Mechanically a straight match |
| Glock 17, Desert Eagle | X18, DGL50 | Medium | Block-outs, and the pistol points above |
| Pump shotgun | KXG12 | Hard | Source exists (shell by shell: start, loop, end, plus pump), but the tool only does magazine swaps: it needs a loop mode, shell props and a hand that stays on the pump |
| RPD | MGX5 | Hard | Belt fed: top cover, belt and drum. Our model has no cover or belt parts; treating the drum as a magazine would work but looks wrong up close |
| Double barrel | none | Hard | No break-action gun in the pack: keep its hand-keyed clips (they work with the arms) or make the reload procedural |

Every block-out also needs the step the five clip guns already had: the animated prefab layout and its Equipped / Idle / Firing clips. The pack has no jam-clearing clips and equip clips only for a generic rifle and pistol, so those stay procedural or ours for every gun.

## How it works

```text
Item prefab                            Player
  ItemHands                              CameraPitch/HeldItems/Hands: FirstPersonHands (HandSet, view = CameraPitch)
    grips: Right = Grip_R,                 spawns FPHand_Left / FPHand_Right (HandRig)
           Left  = Grip_L_Forend           each frame, after the item is posed:
  HandGrip under the part it holds           grip = ItemHands.GetGrip(side)
  (guns: Motion/HandTargets, moved           grip.Resolve: turn round the held part so the wrist points at the implied elbow
   by the clips' constraints)                hand.Place(grip pose, shoulder, elbow): with an ArmRig, the arm reaches for it
                                             hand.Apply(the grip's finger shape)
```

- **Hands follow grips each frame.** On still items, grips sit under the part they hold. On clip-driven guns, constrained hand targets select part contacts or an animated free-hand control. Both follow the parent's recoil, aim / sprint poses, switch motion and wall retraction.
- **Arms.** A hand prefab with an `ArmRig` is a whole arm: `HandRig.Place` works out the hand bone's pose from the grip, and the arm solves two bones from the shoulder point to it, bending toward the elbow point (both in `HandSet`, view space). No Animator or RigBuilder runs: the solve happens in the hands' tick, after the item's final pose, so the arm is never a frame late. A grip out of reach pulls the shoulder along instead of stretching the arm; the forearm's twist bone takes half the wrist's turn. A hand prefab without an `ArmRig` (the floating hands) just moves to the grip.
- **The arms come along with the gun.** `ItemHands > Body Anchor` and `Body Rest` (on guns: the pose pivot and the hip pose) carry the shoulder and elbow points with the item's pose (`ItemHands.Carry`). Aiming or sprinting moves the whole arm with the gun, as a first-person rig does; with shoulders fixed to the camera, aiming brought the hands so close that the arms folded shut (left elbow 23°, wrist bent 121°). Recoil, jolts and the shake sit below the pose pivot, so the arms still absorb those.
- **The elbow favours a straight wrist.** `ArmRig > Wrist Share` (0.4) swings the elbow from the elbow point towards where it would sit behind the hand. With 0, a reload that turns the hand bent the left wrist up to 96° (the pack's own arms: 76° at that moment); with 0.4 it stays within about 10° of the pack's through the AK47 reload.
- **Implied elbow.** The wrist tells the eye where the arm is. A grip only fixes *where* the hand holds and *along which direction* (the held part's axis). By default (`Aim = TowardElbow`) the hand turns round that axis so its wrist points at an invisible elbow (`HandSet` → elbows, in view space, low and out to each side). That keeps the wrist believable on every grip, including mid-reload, without tuning a rotation per grip. `Roll` adds a turn on top. `AsAuthored` uses the grip's exact rotation instead (e.g. a palm-up hand feeding shells).
- **Fitted fingers.** **Bake fit** (on a grip, or **Bake fit for all grips** on `ItemHands`) runs in the editor. It slides the palm onto the part's surface, then searches each finger's curl and tip bend for the shape that hugs the item's meshes without going into them, staying close to the pose. The result is saved on the grip, so nothing runs in game. **Keep fingers** leaves chosen fingers at the pose (trigger finger, support hand round the other hand, small props).
- **Each hand has one grip per item.** A hand never jumps between grips: moving work (reloads, jam clears, reaches) is the item's clips moving that grip's target between contacts. Items without clips keep their hands still on their grips.

### What the weapons do

| Weapon | Hold | Actions |
| --- | --- | --- |
| AK-47 | Right on the pistol grip, left cradling the forend | Clips and contact constraints: grab/extract/release old magazine, independent reach, bring replacement and seat it; empty reload adds the bolt; Repairing has slap, punch, slap over 4 seconds; RepairingStubborn has slap, punch, slap, punch over 5 seconds. Each impact has a clip-authored SFX marker. The animation continues through the QTE and blends back to grip on success. See authoring above |
| MP5 | Right on the pistol grip, left cradling the forend | Clips: extract/drop old magazine, bring and seat replacement; empty reload adds the charging handle. Repairing/RepairingStubborn provide 3/4 alternating receiver slaps / punches with synchronized SFX and optional QTE |
| Pistol | Two-handed: support hand on the grip's left side round the shooting hand | Clips: extract/drop magazine, bring and seat replacement; empty reload adds an overhand slide rack. Repairing/RepairingStubborn provide 3/4 alternating slide slaps / punches with synchronized SFX and optional QTE |
| Pump shotgun | Left on the clip-animated pump | Clips: one shell from belt to loading port per reload loop; Cycling pumps back/home with continuous hand contact. Repairing/RepairingStubborn provide 3/4 alternating receiver slaps / punches with synchronized SFX and optional QTE |
| Double barrel | Left on the forend (rides the clip-animated hinge) | Clips: open hinge, palm-up hand feeds two shells, close hinge. Repairing/RepairingStubborn provide 3/4 alternating receiver slaps / punches with synchronized SFX and optional QTE |
| Fire axe | Both hands on the handle | (swing stays procedural) |
| Repair hammer | Right hand on the handle (`Content/Tools/Hammer/Hammer.prefab`, a hotbar item) | Procedural knocks between pose markers (`RepairTool`; [DEFENSE.md](DEFENSE.md#repair)) |

Each gun's `Motion` curves present the magazine well, loading port or chambers while the hands work.

## Hand poses

A `HandPose` (`Data/Hands/Poses/`) holds three numbers per finger:

- **Curl:** 0 straight, 1 wrapped round a handle, 1.8 the rig's calibrated Fist. Values between 1 and 1.8 blend Closed to Fist, with bounded rotation instead of extrapolating the handle grip. Missing Fist calibration safely holds Closed. Repair punches reach 1.8 on all five fingers with thumb Spread 0; little/ring fingers close slightly earlier and the thumb settles last. The shared hand prefabs have separate per-joint fist rotations with aligned fingers, moderate knuckle bend, tucked fingertips and the thumb across the outside.
- **Spread:** −1 … 1.
- **Tip:** added to Curl on the outer joints only. Negative keeps the finger straight past the knuckle, flat along a side; positive hooks the tip.

A pose holds no bone rotations, so the same pose works on both hands and on any hand mesh. Bake fit uses a grip's pose as its starting point and limit.

| Pose | Used for |
| --- | --- |
| `Grip` | Full wrap: axe handle |
| `TriggerGrip` | Pistol grip, index finger out on the trigger |
| `Forend` | Support hand under a handguard / pump |
| `Support` | Pistol support hand round the shooting hand |
| `HoldMag` | Holding a magazine, palm-up shells |
| `Pinch` | Bolt, slide, single shell |
| `Relaxed` | Spare: nearly open hand |

Editing a pose asset updates every grip that uses it and isn't fitted. Fitted grips keep their baked shape until you bake them again.

## Adding hands to an item

1. Open the item prefab, put it in its hip / idle pose (HeldGun's **Hip** preview button), and add **`ItemHands`** to its root.
2. Add an empty child under the part the hand holds (pistol grip, forend; for a melee weapon, the model) and add **`HandGrip`**. Set the side and pick a pose.
3. Place it on the part's centre line and point its **blue (Z) axis along the part, from the little finger towards the index finger**. For a pistol grip that is up the handle; for a forend it is towards the muzzle.
4. Tick **Preview hand**. The ghost hand shows where the elbow-aimed hand lands. If the palm faces the wrong way round the part, flip Z; for the odd case, use **Roll**. The ghost isn't saved.
5. Press **Bake fit**. Untick `Fit Palm` for pinches (bolt knob, shell) and set **Keep fingers** where the fit shouldn't touch them.
6. Assign the grips to `ItemHands` → Right / Left. Leave Left empty for a one-handed item: that hand is hidden.
7. Hands that move (reloads, props in hand): give the item clips and hand-target constraints like the guns ([Weapon animation authoring](#weapon-animation-authoring)); use **Preview > Show hands** on `ItemHands` while scrubbing them.
8. Check it from the player's view: the item's own pose decides whether the hands' work is visible.

## Arms and their variants

The hands in use are Sidekick arms built from a Sidekick character's upper-arm, lower-arm and hand parts. Each look is a **variant** in `Content/Player/Arms/<Variant>/`: the two `.sk` files (the parts and colours), a mesh, material and colour map per side, and `FPArm_<Variant>_Left` / `_Right.prefab` (root: `HandRig` + `ArmRig`; the prefab's root scale, 1.2, sizes the arm). Built so far: `Survivor02` (wrapped forearms, fingerless gloves; in use) and `Survivor04` (jacket sleeves, bare hands), both from Apocalypse Survivors.

```bash
# Build a variant from any Sidekick character and switch to it (editor open, repo root):
bash AgentScripts/Hands/build_arms.sh Assets/Synty/SidekickCharacters/Characters/ApocalypseSurvivors/ApocalypseSurvivor_04/ApocalypseSurvivor_04.sk Survivor04 --use
# Switch between built variants ("" = back to the floating hands):
unity command script run --file AgentScripts/Hands/ArmSetup.cs --entry ArmSetup.Use --args '["Survivor02"]'
```

Switching only changes the two prefabs `Data/Hands/HandSet.asset` points at; dragging a variant's prefabs into the HandSet does the same. Every Sidekick arm shares one skeleton and the clips store finger shapes, not bone rotations, so grips, poses and gun clips are the same for every variant. For a look of your own, save a character in **Synty > Sidekick Character Tool** (only its arm and hand parts and colours matter) and pass that `.sk`.

`ArmSetup.MakePrefab` does what [Changing the hand mesh](#changing-the-hand-mesh-retargeting) describes by hand: it drops the bones the arm doesn't use, sets up the `ArmRig` (elbow hinge read off the rest pose), places `GripPoint`, and writes the Open / Closed / Spread / Fist calibration from numbers at the top of the script (finger bend per joint, thumb directions in the palm's frame). The Sidekick fingers have three bones and no metacarpal, so joint 0 of each finger chain is a still marker at the wrist: fitting and the forearm direction read the chain as wrist, knuckle, joints.

Tuning: `HandSet` > shoulders (where each arm starts; keep them off screen) and elbows (which way the arms bend, and where wrists aim on elbow-aimed grips). The current values follow the pack's own AK idle mapped onto our AK47: the right shoulder beside the stock's butt with the elbow down and out to the right, the left elbow under the gun. An elbow point on the body's side of the arm folds the arm under the stock, which reads as broken from any outside view. Also: `ArmRig` > Twist Share, Max Reach and Wrist Share; `ItemHands` > Body Anchor / Body Rest (None = shoulders stay with the camera); the arm's size is the variant prefab's root scale (`ArmScale` in `ArmSetup`), then `ItemHands > Hand Scale` per item. Check with `ArmReview.Hand` (the hand alone at a given curl / spread / tip) and `ArmReview.Gun`.

## Changing the hand mesh (retargeting)

Items only reference `HandGrip`s and `HandPose`s, which don't depend on the mesh. To use another hand model that isn't a Sidekick arm:

1. Make a prefab per side: an empty root with **`HandRig`**, and the hand model as a child (shadows off, **Update When Offscreen** on the skinned mesh renderer). Scale it so it reads well next to the weapons; they are ~1.3x real size, and the current hands are ~26 cm.
2. `HandRig` → Setup → **Auto-find finger bones**. It finds chains named thumb / index / middle / ring / pinky or little under the hand bone. Check the five lists and fix them by hand if the rig uses other names.
3. Calibrate four shapes: bend the bones in the prefab, then **Capture** each one:
   - **Open:** flat hand. Auto-find already stores the current bones as this.
   - **Closed:** fingers wrapped round a bar, thumb over the top.
   - **Spread:** fingers fanned out, thumb swung away from the index.
   - **Fist:** fingers folded into the palm, thumb resting across their outside. Keep the fingertips from curling back toward the knuckles.

   Check them with Show Open / Closed / Spread / Fist or the preview sliders. Spread fades out while blending beyond Closed so a grip's thumb spread cannot twist the finished fist.
4. Add a **`GripPoint`** child at the centre of that bar (Z from little finger to index, leaning towards the fingers, as a handle crosses the palm diagonally; Y out of the back of the hand) and assign it. Set **Finger Radius** (finger half-thickness) and **Palm Thickness** (bone to palm skin) for fitting.
5. Point `Data/Hands/HandSet.asset` at the new prefabs, or make a new `HandSet` and assign it on the player's `Hands` object. Previews and Bake fit use the first `HandSet` found in the project.
6. Bake fit again on every item (**Bake fit for all grips**): fits depend on the hand's size and shape.

The floating hands (`_Core/HandSystem/Prefabs/FPHand_Left`, `FPHand_Right`, VR Low Poly Hand pack, NO UV variant) follow the steps above and stay as the fallback (`ArmSetup.Use ""`); recalibrate them with `HandRig`'s setup buttons and **Bake fit** (the editor script that first built them is not in the repo). Baked fits on the other items were made for those hands: bake them again for the arms when a grip looks off.

`Tools/HandFistReview.cs` (written for the floating hands; its `Weapon` render no longer compiles against `ItemHands`, use `ArmReview` instead) supplies their Fist calibration and targeted repair-finger curve update (`Apply`), isolated hand renders (`Render`) and player-view weapon renders (`Weapon`) under Temp/HandFist. It preserves existing Open/Closed/Spread calibrations and does not rebuild reloads or repair movement/impact tracks. HandRigTests checks ordinary grip compatibility, bounded fist blending, safe fallback and fist/closure timing at every gun's punch contacts.

## Tuning

| To change | Edit |
| --- | --- |
| Where a hand sits on an item | That item's `HandGrip` (Preview hand on), then Bake fit |
| Which way a hand faces round a part | `HandGrip` → Roll, or flip its Z; `Aim = AsAuthored` for full control |
| Where the arms come from and bend (all grips) | `Data/Hands/HandSet.asset` → shoulders and elbows (view space); re-bake fits after moving elbows |
| The arms' look | another variant: [Arms and their variants](#arms-and-their-variants) |
| Finger shape everywhere a pose is used | `Data/Hands/Poses/<Pose>.asset` (Curl, Spread, Tip), then re-bake |
| Timing / path of a reload step or reach | the item's Animation clip on `AnimationRoot` |
| How the whole gun tilts while reloading | the clip's `Motion` curves |
| Hand size, mesh, grip centre | Per-item size: `ItemHands > Hand Scale`; shared mesh: `_Core/HandSystem/Prefabs/FPHand_*`, then review contacts and fingers |

## Known gaps

- **Gun grips:** a fitted vertical or angled grip doesn't move the support hand yet: the clips keep it on the forend contact. The grip's hand point is ready for the gun animation rework (`GunMounts.SupportGrip`, [WEAPONS.md](WEAPONS.md#for-the-gun-animation-rework)).
- **Arms on the other items.** Only the AK47 (and the AK-74M) was reviewed with the arms. The other guns, the axe and the hammer use them too, with grips, finger shapes and fits made for the slimmer floating hands. The pistol is visibly wrong (2026-11-16): the two hands overlap and bury it, also at hand scale 1. The MP5's support fingers stand open above its handguard. Check each from the player's view (`ArmReview.Gun`); the fix is the gun's own retarget pass or re-baked fits. `ArmSetup.Use ""` puts the floating hands back meanwhile.
- **Hand proportions.** Sidekick hands are short and wide. Against the pack's hands scaled to our AK47 (1.33), ours at 1.2 have fingers about 9% shorter, a palm about as wide, and fingers about a fifth thicker. Thinning the finger vertices round their bones at build time was tried as an in-memory preview (15 to 22%) and is subtle; not built.
- **Shadow.** The arms cast none (like every held item) and there is no player body.
- **Retargeted reloads:** only the AK47 (and the AK-74M through it). The pack's camera motion isn't carried over, and the hand on the bolt in the empty reload isn't snapped to our bolt handle (the pack's hand never comes close enough to its own for the automatic correction).
- **AK47 repair clips** (`AK47_Repairing`, `_RepairingStubborn`, `_RepairEnd`) are no longer played while its repair profile has a shake; they still exist, keyed for the old forend hold.
- **Fitting is approximate.** Fingers can't each bend joint by joint, and open or overlapping vendor meshes can confuse it. Check fits with Preview and use Keep fingers where it misjudges.
- **Placeholder shells:** the shotgun shells are plain red cylinders (`Content/Weapons/_Shared/Materials/ShotgunShell.mat`).
- **Axe:** the swing is the existing procedural one, and both hands stay on the handle (no one-handed swing).
