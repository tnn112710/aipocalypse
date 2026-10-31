# Hands

First-person floating hands (no arms), in the style of How to Fish and VR games. Shared hands follow **grips** on the held item. AK47, MP5, pistol, pump shotgun and double barrel use editable animation clips and Animation Rigging constraints to drive their grips and finger shapes; other items (the axe) hold still grips. Script locations: [CODE_STRUCTURE.md](CODE_STRUCTURE.md). Weapons: [WEAPONS.md](WEAPONS.md).

## Weapon animation authoring

On all five guns, `ItemHands` points to the two `Motion/HandTargets` grips, whose rotations are used as authored. Their `_animatedShape` finger controls are keyed in each clip; the elbow rotation does not override them. The player's shared hand meshes still follow those targets, so a hand-mesh swap does not require new weapon clips.

`ItemHands > Hand Scale` adjusts hand size per item while keeping the palm grip point fixed on the target. All five guns use **1.12** (12% larger), keeping the character's hands consistent when switching weapons; other items default to 1. Runtime placement and ghost previews both use this setting. The original hand mesh bounds are about 26 cm long against this AK47 model's roughly 1.2 m length; the increase is a first-person art adjustment. Gun meshes keep their existing scale. Large scale changes still need grip/finger-fit review. Trigger and magazine poses use authored curls instead of the old fit solver's partly open fingers.

`PosePivot/RecoilPivot/AnimationRoot` carries the Animator, RigBuilder and `GunAnimation`. Its `Motion` child contains `Weapon`, `Controls/LeftFree` and `HandTargets`. `Rig/LeftHand` is a Multi-Parent Constraint with five sources in this fixed order: forend grip, old magazine grip, replacement magazine grip, bolt grip, free-hand control. `Rig/RightHand` keeps the right target on the pistol grip. Contact switches are stepped keys, with matching free-control poses at each boundary. No hierarchy reparenting happens during playback.

The pistol's default left contact supports the right hand, and its bolt contact is on `Slide`. Shotguns use source 2 for the shell/shell-pair contact; unused magazine/bolt sources point to the forend. The pump's default contact is a child of `Pump`; the double barrel's is under `Hinge/Handguard`. Shell props stay active and their renderers are keyed, preserving constraint bindings while hidden.

Clips live in `Content/Weapons/<Gun>/Animations/`: `<Gun>_Equipped`, `_Idle`, `_Firing`, `_Reloading`, `_ReloadingEmpty`, `_Repairing`. The four later conversions also supply `_Cycling`; only the pump currently uses this action. Repairing means jam clearing. All five guns alternate an open-palm slap and a closed-fist punch on the receiver/slide (slap, punch, slap in the 4-second variant; slap, punch, slap, punch in the 5-second one), with an authored impact reaction at each contact; punches come from higher, land faster and shake the gun more. The left hand follows the free-hand control through wind-up, impact and recoil, then returns to the forend; the bolt and magazine stay at rest. Repair continues while the QTE runs and returns from the current pose after success. See WEAPONS.md for QTE and timing tuning. AK47/MP5/pistol empty reloads add a bolt/slide rack; normal reloads omit it. Firing moves the AK47 bolt or pistol slide and the trigger finger; MP5's charging handle stays still during firing. Accumulated gun and camera recoil remain procedural.

Pump `Reloading` and `ReloadingEmpty` each describe one complete shell insertion and reset to the forend at both ends. `Gun.ActionProgress` wraps for the next shell, so the clip repeats without adding a new gameplay action. `Cycling` moves the pump and bolt back and home while the left hand stays attached. Double-barrel reload opens the hinge, brings two shells from the belt to the chambers, hides them as they seat, then closes the hinge. Its muzzle marker follows the hinge. Shotgun tubes/barrels never detach as magazines.

To edit:

1. Open `Content/Weapons/<Gun>/<Gun>.prefab`, select `AnimationRoot`, and open **Window > Animation > Animation**. Select that gun's desired clip. Keep RigBuilder enabled for Animation-window constraint previews.
2. On the prefab root's `ItemHands`, use **Preview > Show hands** with default grips to show the shared ghost hands. Then scrub the clip in the Animation window; the ghosts follow the constrained targets. End the hand preview when finished.
3. Key `Motion` for whole-weapon gestures; `Motion/Weapon/Magazine`, `ReplacementMagazine`, `Bolt`, `Slide`, `Pump`, `Hinge` or `Shells` for the parts that gun has; `Motion/Controls/LeftFree` for independent reaches. Double-barrel shells live under `Hinge/Shells`. Grip contacts remain children of their parts.
4. Key `Rig/LeftHand > Source Objects > Weight` to select a contact. At a switch, make `LeftFree` coincide with the contact's position and rotation to prevent jumps. Key `Motion/HandTargets/Left > HandGrip > Animated Shape` to open and close fingers around contact.
5. Magazine visibility uses MeshRenderer **Enabled**, so hidden meshes do not disable their contact controls. On AK47/MP5/pistol, at normalized time 0.3 the original magazine hides and `GunAnimation` releases a pooled world magazine. If this moment changes, update the clip's hide/contact keys and `GunAnimation`'s **Release Time** together. The replacement appears at 0.42 and seats later; it resets on cancellation or the next idle frame. Shotgun shells show at 0.28 and hide at 0.73, without spawning a magazine.

`Content/Weapons/_Shared/Animations/Gun.controller` defines the base action slots, including Cycling, plus RepairingStubborn and RepairEnd; each gun's `<Gun>.overrideController` assigns its actual clips (AK47 does not need a Cycling override). `GunAnimation` selects states and samples the controller plus constraints manually in the player's tick, before hands and shots. During playback it detaches the controller from the Animator component and gives it exclusively to the manual graph, so Unity's later automatic Animator pass cannot restore Idle over the action. Stop/holster restores the component's controller for re-equipping and editor previews. Reload/repair/cycle progress comes from `Gun.ActionProgress`, so changing clip length does not change ammo, jam or pump rules. Equip/idle/fire and RepairEnd use clip duration. Early success captures current transforms and finger shapes, then blends into RepairEnd over its clip duration, skipping any remaining slaps. Keep the end of each repair clip at its normal grip/rest pose so completion can return cleanly from any progress. All clip-driven properties have baseline keys in every clip, including visibility and constraint weights; keep those reset keys when editing. The controller has no automatic transitions to compete with gameplay state.

For another gun, use the same generic control paths and a new Override Controller. Matching names make bindings reusable; different proportions and reload mechanisms still need fitting and often different clips. Author the clips by hand in the Animation window (steps above); there is no generator script for them in the repo.

## How it works

```text
Item prefab                            Player
  ItemHands                              CameraPitch/HeldItems/Hands: FirstPersonHands (HandSet, view = CameraPitch)
    grips: Right = Grip_R,                 spawns FPHand_Left / FPHand_Right (HandRig)
           Left  = Grip_L_Forend           each frame, after the item is posed:
  HandGrip under the part it holds           grip = ItemHands.GetGrip(side)
  (guns: Motion/HandTargets, moved           grip.Resolve: turn round the held part so the wrist points at the implied elbow
   by the clips' constraints)                hand.Place + hand.Apply(the grip's finger shape)
```

- **Hands follow grips each frame.** On still items, grips sit under the part they hold. On clip-driven guns, constrained hand targets select part contacts or an animated free-hand control. Both follow the parent's recoil, aim / sprint poses, switch motion and wall retraction.
- **Implied arms.** There are no forearms, so the wrist is what tells the eye where an arm would be. A grip only fixes *where* the hand holds and *along which direction* (the held part's axis). By default (`Aim = TowardElbow`) the hand turns round that axis so its wrist points at an invisible elbow (`HandSet` → elbows, in view space, low and out to each side). That keeps the wrist believable on every grip, including mid-reload, without tuning a rotation per grip. `Roll` adds a turn on top. `AsAuthored` uses the grip's exact rotation instead (e.g. a palm-up hand feeding shells).
- **Fitted fingers.** **Bake fit** (on a grip, or **Bake fit for all grips** on `ItemHands`) runs in the editor. It slides the palm onto the part's surface, then searches each finger's curl and tip bend for the shape that hugs the item's meshes without going into them, staying close to the pose. The result is saved on the grip, so nothing runs in game. **Keep fingers** leaves chosen fingers at the pose (trigger finger, support hand round the other hand, small props).
- **Each hand has one grip per item.** A hand never jumps between grips: moving work (reloads, jam clears, reaches) is the item's clips moving that grip's target between contacts. Items without clips keep their hands still on their grips.

### What the weapons do

| Weapon | Hold | Actions |
| --- | --- | --- |
| AK-47 | Right on the pistol grip, left cradling the forend; hands scaled 1.12 | Clips and contact constraints: grab/extract/release old magazine, independent reach, bring replacement and seat it; empty reload adds the bolt; Repairing has slap, punch, slap over 4 seconds; RepairingStubborn has slap, punch, slap, punch over 5 seconds. Each impact has a clip-authored SFX marker. The animation continues through the QTE and blends back to grip on success. See authoring above |
| MP5 | Right on the pistol grip, left cradling the forend | Clips: extract/drop old magazine, bring and seat replacement; empty reload adds the charging handle. Repairing/RepairingStubborn provide 3/4 alternating receiver slaps / punches with synchronized SFX and optional QTE |
| Pistol | Two-handed: support hand on the grip's left side round the shooting hand | Clips: extract/drop magazine, bring and seat replacement; empty reload adds an overhand slide rack. Repairing/RepairingStubborn provide 3/4 alternating slide slaps / punches with synchronized SFX and optional QTE |
| Pump shotgun | Left on the clip-animated pump | Clips: one shell from belt to loading port per reload loop; Cycling pumps back/home with continuous hand contact. Repairing/RepairingStubborn provide 3/4 alternating receiver slaps / punches with synchronized SFX and optional QTE |
| Double barrel | Left on the forend (rides the clip-animated hinge) | Clips: open hinge, palm-up hand feeds two shells, close hinge. Repairing/RepairingStubborn provide 3/4 alternating receiver slaps / punches with synchronized SFX and optional QTE |
| Fire axe | Both hands on the handle | (swing stays procedural) |

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

## Changing the hand mesh (retargeting)

Items only reference `HandGrip`s and `HandPose`s, which don't depend on the mesh. To use another hand model:

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

The current prefabs (`_Core/HandSystem/Prefabs/FPHand_Left`, `FPHand_Right`, VR Low Poly Hand pack, NO UV variant) follow the steps above; recalibrate them with `HandRig`'s setup buttons and **Bake fit** (the editor script that first built them is not in the repo).

`Tools/HandFistReview.cs` supplies the current Fist calibration and targeted repair-finger curve update (`Apply`), isolated hand renders (`Render`) and player-view weapon renders (`Weapon`) under Temp/HandFist. It preserves existing Open/Closed/Spread calibrations and does not rebuild reloads or repair movement/impact tracks. HandRigTests checks ordinary grip compatibility, bounded fist blending, safe fallback and fist/closure timing at every gun's punch contacts.

## Tuning

| To change | Edit |
| --- | --- |
| Where a hand sits on an item | That item's `HandGrip` (Preview hand on), then Bake fit |
| Which way a hand faces round a part | `HandGrip` → Roll, or flip its Z; `Aim = AsAuthored` for full control |
| Where the arms seem to come from (all grips) | `Data/Hands/HandSet.asset` → elbows (view space), then re-bake |
| Finger shape everywhere a pose is used | `Data/Hands/Poses/<Pose>.asset` (Curl, Spread, Tip), then re-bake |
| Timing / path of a reload step or reach | the item's Animation clip on `AnimationRoot` |
| How the whole gun tilts while reloading | the clip's `Motion` curves |
| Hand size, mesh, grip centre | Per-item size: `ItemHands > Hand Scale`; shared mesh: `_Core/HandSystem/Prefabs/FPHand_*`, then review contacts and fingers |

## Known gaps

- **No forearms.** If they're wanted later, the elbow points and grips can drive an Animation Rigging Two Bone IK, evaluated after the hands are placed; options and steps in the `tools-unity-animation-rigging` skill.
- **Fitting is approximate.** Fingers can't each bend joint by joint, and open or overlapping vendor meshes can confuse it. Check fits with Preview and use Keep fingers where it misjudges.
- **Placeholder shells:** the shotgun shells are plain red cylinders (`Content/Weapons/_Shared/Materials/ShotgunShell.mat`).
- **Axe:** the swing is the existing procedural one, and both hands stay on the handle (no one-handed swing).
