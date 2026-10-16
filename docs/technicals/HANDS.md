# Hands

First-person floating hands (no arms), in the style of How to Fish and VR games. Shared hands follow **grips** on the held item. AK47, MP5, pistol, pump shotgun and double barrel use editable animation clips and Animation Rigging constraints to drive their grips and finger shapes. Other items can use the procedural hand actions below. Script locations: [CODE_STRUCTURE.md](CODE_STRUCTURE.md). Weapons: [WEAPONS.md](WEAPONS.md).

## Weapon animation authoring

On all five guns, `ItemHands` points to the two `Motion/HandTargets` grips, whose rotations are used as authored. Their `_animatedShape` finger controls are keyed in each clip; the automatic reach arc and elbow rotation do not override them. The player's shared hand meshes still follow those targets, so a hand-mesh swap does not require new weapon clips.

`ItemHands > Hand Scale` adjusts hand size per item while keeping the palm grip point fixed on the target. All five guns use **1.12** (12% larger), keeping the character's hands consistent when switching weapons; other items default to 1. Runtime placement and ghost previews both use this setting. The original hand mesh bounds are about 26 cm long against this AK47 model's roughly 1.2 m length; the increase is a first-person art adjustment. Gun meshes keep their existing scale. Large scale changes still need grip/finger-fit review. Trigger and magazine poses use authored curls instead of the old fit solver's partly open fingers.

`PosePivot/RecoilPivot/AnimationRoot` carries the Animator, RigBuilder and `GunAnimation`. Its `Motion` child contains `Weapon`, `Controls/LeftFree` and `HandTargets`. `Rig/LeftHand` is a Multi-Parent Constraint with five sources in this fixed order: forend grip, old magazine grip, replacement magazine grip, bolt grip, free-hand control. `Rig/RightHand` keeps the right target on the pistol grip. Contact switches are stepped keys, with matching free-control poses at each boundary. No hierarchy reparenting happens during playback.

The pistol's default left contact supports the right hand, and its bolt contact is on `Slide`. Shotguns use source 2 for the shell/shell-pair contact; unused magazine/bolt sources point to the forend. The pump's default contact is a child of `Pump`; the double barrel's is under `Hinge/Handguard`. Shell props stay active and their renderers are keyed, preserving constraint bindings while hidden.

Clips live in `_Core/WeaponSystem/Animations/<Gun>/`: `<Gun>_Equipped`, `_Idle`, `_Firing`, `_Reloading`, `_ReloadingEmpty`, `_Repairing`. The four later conversions also supply `_Cycling`; only the pump currently uses this action. Repairing means jam clearing. MP5, pistol and both shotguns use an open-palm receiver/slide slap at normalized time 0.43 with a small impact reaction. AK47 uses three fast open-palm receiver slaps in its 4-second variant and four in its 5-second variant. The left hand follows the free-hand control through wind-up, impact and recoil, then returns to the forend; the bolt and magazine stay at rest. Repair continues while the QTE runs and returns from the current pose after success. See WEAPONS.md for QTE and timing tuning. AK47/MP5/pistol empty reloads add a bolt/slide rack; normal reloads omit it. Firing moves the AK47 bolt or pistol slide and the trigger finger; MP5's charging handle stays still during firing. Accumulated gun and camera recoil remain procedural.

Pump `Reloading` and `ReloadingEmpty` each describe one complete shell insertion and reset to the forend at both ends. `Gun.ActionProgress` wraps for the next shell, so the clip repeats without adding a new gameplay action. `Cycling` moves the pump and bolt back and home while the left hand stays attached. Double-barrel reload opens the hinge, brings two shells from the belt to the chambers, hides them as they seat, then closes the hinge. Its muzzle marker follows the hinge. Shotgun tubes/barrels never detach as magazines.

To edit:

1. Open `Prefabs/Guns/<Gun>.prefab`, select `AnimationRoot`, and open **Window > Animation > Animation**. Select that gun's desired clip. Keep RigBuilder enabled for Animation-window constraint previews.
2. On the prefab root's `ItemHands`, use **Preview > Show hands** with default grips to show the shared ghost hands. Then scrub the clip in the Animation window; the ghosts follow the constrained targets. End the hand preview when finished.
3. Key `Motion` for whole-weapon gestures; `Motion/Weapon/Magazine`, `ReplacementMagazine`, `Bolt`, `Slide`, `Pump`, `Hinge` or `Shells` for the parts that gun has; `Motion/Controls/LeftFree` for independent reaches. Double-barrel shells live under `Hinge/Shells`. Grip contacts remain children of their parts.
4. Key `Rig/LeftHand > Source Objects > Weight` to select a contact. At a switch, make `LeftFree` coincide with the contact's position and rotation to prevent jumps. Key `Motion/HandTargets/Left > HandGrip > Animated Shape` to open and close fingers around contact.
5. Magazine visibility uses MeshRenderer **Enabled**, so hidden meshes do not disable their contact controls. On AK47/MP5/pistol, at normalized time 0.3 the original magazine hides and `GunAnimation` releases a pooled world magazine. If this moment changes, update the clip's hide/contact keys and `GunAnimation`'s **Release Time** together. The replacement appears at 0.42 and seats later; it resets on cancellation or the next idle frame. Shotgun shells show at 0.28 and hide at 0.73, without spawning a magazine.

`Animations/Shared/Gun.controller` defines the base action slots, including Cycling, plus RepairingStubborn, RepairFocus and RepairEnd; each gun's `<Gun>.overrideController` assigns its actual clips (AK47 does not need a Cycling override). `GunAnimation` selects states and samples the controller plus constraints manually in the player's tick, before hands and shots. During playback it detaches the controller from the Animator component and gives it exclusively to the manual graph, so Unity's later automatic Animator pass cannot restore Idle over the action. Stop/holster restores the component's controller for re-equipping and editor previews. Reload/repair/cycle progress comes from `Gun.ActionProgress`, so changing clip length does not change ammo, jam or pump rules. Equip/idle/fire and RepairEnd use clip duration; RepairFocus is retained for preview but is not used by the automatic AK47 flow. Early success captures current transforms and finger shapes, then blends into RepairEnd over its clip duration, skipping any remaining slaps. Keep the end of each repair clip at its normal grip/rest pose so completion can return cleanly from any progress. All clip-driven properties have baseline keys in every clip, including visibility and constraint weights; keep those reset keys when editing. The controller has no automatic transitions to compete with gameplay state.

For another gun, use the same generic control paths and a new Override Controller. Matching names make bindings reusable; different proportions and reload mechanisms still need fitting and often different clips. Do not rerun `FirstPersonHandsSetup.Item` on a converted gun: it rebuilds the old procedural setup. `AgentScripts/Ak47Animations.cs` provides the one-time migrations `Build` (AK47) and `BuildOther` (MP5/Pistol/PumpShotgun/DoubleBarrel); both refuse to overwrite a converted gun. `Render(false, "MP5")` renders a contact sheet under `Temp/MP5Animations` for inspection.

## How it works

```text
Procedural item prefab                Player
  ItemHands                              CameraPitch/HeldItems/Hands: FirstPersonHands (HandSet, view = CameraPitch)
    default grips: Right = Grip_R,         spawns FPHand_Left / FPHand_Right (HandRig)
                   Left  = Grip_L_Forend   each frame, after the item is posed:
    actions: Reload, ReloadEmpty,            grip = ItemHands.GetGrip(side)
             ClearJam…                       grip.Resolve: turn round the held part so the wrist points at the implied elbow
  HandGrip under each part it holds          hand.Place + hand.Apply(the grip's fitted finger shape)
  (Model/…/AR_B_Mag/Grip_L_Mag)              grip changed → arc there over the key's Blend seconds, fingers opening mid-way
```

- **Hands follow grips each frame.** On procedural items, grips sit under the part they hold. On clip-driven guns, constrained hand targets select part contacts or an animated free-hand control. Both follow the parent's recoil, aim / sprint poses, switch motion and wall retraction.
- **Implied arms.** There are no forearms, so the wrist is what tells the eye where an arm would be. A grip only fixes *where* the hand holds and *along which direction* (the held part's axis). By default (`Aim = TowardElbow`) the hand turns round that axis so its wrist points at an invisible elbow (`HandSet` → elbows, in view space, low and out to each side). That keeps the wrist believable on every grip, including mid-reload, without tuning a rotation per grip. `Roll` adds a turn on top. `AsAuthored` uses the grip's exact rotation instead (e.g. a palm-up hand feeding shells).
- **Fitted fingers.** **Bake fit** (on a grip, or **Bake fit for all grips** on `ItemHands`) runs in the editor. It slides the palm onto the part's surface, then searches each finger's curl and tip bend for the shape that hugs the item's meshes without going into them, staying close to the pose. The result is saved on the grip, so nothing runs in game. **Keep fingers** leaves chosen fingers at the pose (trigger finger, support hand round the other hand, small props).
- **Moving between grips.** The hand travels on an arc that bows away from the item's long axis (the line between its two default grips) and a little towards the eye, and its fingers open half way. This reads as letting go and reaching, not sliding (`FirstPersonHands` → Moving between grips).
- **An action** (`ItemHands` → Actions) is a named 0..1 timeline with three kinds of tracks:
  - **Hands:** at time t, a hand moves to a grip (empty grip = back to its default).
  - **Parts:** keyframed offsets of item parts from their rest pose (smoothed between keys; it starts and ends at rest unless there are keys at 0 / 1).
  - **Show:** an object visible only between two times (a shell in the hand).
- **Who plays actions:** the item's view. `HeldGun` plays one per `GunAction` and drives it from `Gun.ActionProgress`, so an action always fits the gun's `ReloadTime` / `ClearTime` / `CycleTime`. Switching weapons cancels it cleanly (parts go back to rest).

| `GunAction` | Action played | Progress |
| --- | --- | --- |
| Reloading (magazine) | `ReloadEmpty` when the magazine was empty at the start (falls back to `Reload`), else `Reload` | whole reload |
| Reloading (per round) | `LoadRound` | each round; restarts for the next one |
| ClearingJam | `ClearJam` | whole clear |
| Cycling | `Cycle` | pump / bolt stroke (the pump guns need none: the hand is on the pump part) |

An item with no matching action keeps its default grips.

### What the weapons do

| Weapon | Hold | Actions |
| --- | --- | --- |
| AK-47 | Right on the pistol grip, left cradling the forend; hands scaled 1.12 | Clips and contact constraints: grab/extract/release old magazine, independent reach, bring replacement and seat it; empty reload adds the bolt; Repairing has three slaps over 4 seconds; RepairingStubborn has four over 5 seconds. Each impact has a clip-authored SFX marker. The animation continues through the QTE and blends back to grip on success. See authoring above |
| MP5 | Right on the pistol grip, left cradling the forend | Clips: extract/drop old magazine, bring and seat replacement; empty reload adds the charging handle. Repairing slaps the receiver |
| Pistol | Two-handed: support hand on the grip's left side round the shooting hand | Clips: extract/drop magazine, bring and seat replacement; empty reload adds an overhand slide rack. Repairing slaps the slide |
| Pump shotgun | Left on the clip-animated pump | Clips: one shell from belt to loading port per reload loop; Cycling pumps back/home with continuous hand contact. Repairing slaps the receiver |
| Double barrel | Left on the forend (rides the clip-animated hinge) | Clips: open hinge, palm-up hand feeds two shells, close hinge. Repairing slaps the receiver |
| Fire axe | Both hands on the handle | (swing stays procedural) |

Each gun's `Motion` curves present the magazine well, loading port or chambers while the hands work. `HeldGun > Action motion` and its pump/break offsets apply only to prefabs without `GunAnimation`.

## Hand poses

A `HandPose` (`Data/Hands/Poses/`) holds three numbers per finger:

- **Curl:** 0 straight … 1 wrapped round a handle at the grip point.
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
7. Actions (optional), e.g. a magazine reload:
   - Add a grip under the mag (`Grip_L_Mag`, `HoldMag`) and one under the bolt (`Grip_L_Bolt`, `Pinch`).
   - Add an action named `Reload`. **Hands:** `0.02 → Left → Grip_L_Mag`, then `0.9 → Left → (none)` to return.
   - **Parts:** the mag: rest → pulled down → out of view → (hold: the "new" mag) → hooked under the well → seated.
   - Use the **Preview** foldout on `ItemHands`: pick the action and drag the time slider. The parts and both ghost hands pose at that point. Press **End preview** before saving, so the parts go back to rest (saving or closing the prefab also ends it).
   - Press **Bake fit for all grips** once the grips are placed.
8. Shells and other props: add a hidden child object (`HandProp_Shell`), give it a grip, animate it with a part track and show it with a Show span. The hand holding it follows it.
9. Check it from the player's view: the gun's reload tilt (`HeldGun` → Action motion) decides whether the hands' work is visible.

## Changing the hand mesh (retargeting)

Items only reference `HandGrip`s and `HandPose`s, which don't depend on the mesh. To use another hand model:

1. Make a prefab per side: an empty root with **`HandRig`**, and the hand model as a child (shadows off, **Update When Offscreen** on the skinned mesh renderer). Scale it so it reads well next to the weapons; they are ~1.3x real size, and the current hands are ~26 cm.
2. `HandRig` → Setup → **Auto-find finger bones**. It finds chains named thumb / index / middle / ring / pinky or little under the hand bone. Check the five lists and fix them by hand if the rig uses other names.
3. Calibrate three shapes: bend the bones in the prefab, then **Capture** each one:
   - **Open:** flat hand. Auto-find already stores the current bones as this.
   - **Closed:** fingers wrapped round a bar, thumb over the top.
   - **Spread:** fingers fanned out, thumb swung away from the index.

   Check them with Show Open / Closed / Spread or the preview sliders.
4. Add a **`GripPoint`** child at the centre of that bar (Z from little finger to index, leaning towards the fingers, as a handle crosses the palm diagonally; Y out of the back of the hand) and assign it. Set **Finger Radius** (finger half-thickness) and **Palm Thickness** (bone to palm skin) for fitting.
5. Point `Data/Hands/HandSet.asset` at the new prefabs, or make a new `HandSet` and assign it on the player's `Hands` object. Previews and Bake fit use the first `HandSet` found in the project.
6. Bake fit again on every item (**Bake fit for all grips**): fits depend on the hand's size and shape.

The current prefabs (`Prefabs/Hands/FPHand_Left`, `FPHand_Right`, VR Low Poly Hand pack, NO UV variant) were built and calibrated by `AgentScripts/FirstPersonHandsSetup.cs`. Its entries are `Hands`, `Poses`, `Player`, and `Item` (one weapon per call, as fitting can take ~20 s), plus `RenderItem` / `RenderTilts` for checking from the player's view. Re-running `Item` rebuilds that weapon's grips, actions and reload tilt, overwriting hand edits there.

## Tuning

| To change | Edit |
| --- | --- |
| Where a hand sits on an item | That item's `HandGrip` (Preview hand on), then Bake fit |
| Which way a hand faces round a part | `HandGrip` → Roll, or flip its Z; `Aim = AsAuthored` for full control |
| Where the arms seem to come from (all grips) | `Data/Hands/HandSet.asset` → elbows (view space), then re-bake |
| Finger shape everywhere a pose is used | `Data/Hands/Poses/<Pose>.asset` (Curl, Spread, Tip), then re-bake |
| How fast hands move between grips | `ItemHands` → Blend, or a hand key's Blend |
| Reach arc and finger opening | Player → `HeldItems/Hands` → `FirstPersonHands` → Moving between grips |
| Timing / path of a reload step | Guns: Animation clip on `AnimationRoot`; procedural items: `ItemHands` → Actions |
| How the whole gun tilts while reloading | Guns: clip's `Motion` curves; procedural fallback: `HeldGun` → Action motion |
| Hand size, mesh, grip centre | Per-item size: `ItemHands > Hand Scale`; shared mesh: `Prefabs/Hands/FPHand_*`, then review contacts and fingers |

## Known gaps

- **Arcs:** moves between grips arc and open the fingers but don't overshoot or settle.
- **No forearms.** If they're wanted later, the elbow points and grips can drive an Animation Rigging Two Bone IK.
- **Fitting is approximate.** Fingers can't each bend joint by joint, and open or overlapping vendor meshes can confuse it. Check fits with Preview and use Keep fingers where it misjudges.
- **Placeholder shells:** the shotgun shells are plain red cylinders (`Materials/ShotgunShell.mat`).
- **Axe:** the swing is the existing procedural one, and both hands stay on the handle (no one-handed swing).
