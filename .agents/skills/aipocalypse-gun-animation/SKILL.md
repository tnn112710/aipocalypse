---
name: aipocalypse-gun-animation
description: First-person gun animation in aipocalypse without an animator — Sidekick arms, reloads retargeted from the KINEMATION pack (ReloadRetarget), generated jam-clearing clips (RepairClips), turning a block-out into an animated gun (GunAnimateSetup), borrowed forend holds (HoldLike), hip / aim / work poses, and reviewing every gun from the player camera. Use for any task that adds a gun, animates or re-bakes one, changes the arms, hand set, grips or gun poses, or fixes how hands sit on a gun.
---

# Gun animation (aipocalypse)

No animator on the project. Every gun motion comes from one of four places:

| Motion | Where it comes from |
| --- | --- |
| Reload, empty reload | Baked from the KINEMATION FPS Animation Pack onto our gun and our hand rig (`ReloadRetarget`). Shotguns: old hand-keyed clips |
| Jam clearing | Three motions: procedural **shake** (`GunShake`), generated clips **Reseat** and **Rack** (`RepairClips`). `GunRepairProfile.ShakeChance` picks |
| Hip, aim, sprint, work | Pose markers on the prefab (`Poses/*`), eased by `HeldGun` |
| Recoil, sway, wobble, jolts | Code (`HeldGun`, WEAPONS.md) |

Arms are Synty Sidekick meshes on a two-bone solve (`ArmRig`); clips key **hand targets and finger shapes** (Curl / Spread / Tip), never arm bones, so a clip works on any hand mesh.

Read first, as needed:

- [HANDS.md](../../../docs/technicals/HANDS.md): the hand system, retargeted reloads, generated repair clips, animating a block-out, borrowed holds, play review, known gaps.
- [WEAPONS.md](../../../docs/technicals/WEAPONS.md): gun prefab layout, poses (hip framing, work pose), clips and constraints, adding a gun, repair as a shake, block-outs and builder order.
- [CASTLE_BRAVO_GUNS.md](../../../docs/technicals/CASTLE_BRAVO_GUNS.md): every gun in the gun pack rated Easy / Medium / Hard / Avoid for this pipeline. **Check it before adding a gun.**
- `tools-unity-animation-rigging` for the rig itself, `tools-unity-synty-characters` for the arm meshes.

Vendor rule: nothing in `_Game` references the KINEMATION pack. The bakes sample it in a preview scene and write our own `.anim` files.

## The scripts

All run with `unity command script run --file <path> --entry <Class.Method> --args '[...]'`, in **edit mode**.

| Script | Entry | Does |
| --- | --- | --- |
| `AgentScripts/Weapons/GunBlockoutSetup.cs` | `One(name)`, `Blockouts()` | Builds a block-out gun from a Castle Bravo prefab (table row per gun). Skips guns that have a `GunAnimation` |
| `AgentScripts/Weapons/GunAnimateSetup.cs` | `Animate(gun, like)` | Grafts an animated gun's layout, rig, clips and repair profile onto a block-out |
| `AgentScripts/Hands/ReloadRetarget.cs` | `Bake(gun)`, `Describe(gun)`, `SourceStrip(...)` | Pack reload → `<Gun>_Reloading` / `_ReloadingEmpty`, plus the idle forend hold. Needs a `Table` entry |
| `AgentScripts/Hands/RepairClips.cs` | `Bake(gun)` | Reseat and Rack clips from the gun's own reloads |
| `AgentScripts/Hands/HoldLike.cs` | `Apply(gun, like)` | Another gun's support-hand hold, fitted to this gun; for guns with no retarget |
| `AgentScripts/Hands/ArmSetup.cs` | see HANDS.md | Builds and switches arm variants |
| `AgentScripts/Weapons/AimReview.cs` | `Prepare`, `Hold`, `Fit`, `Aim`, `Pad`, `Hip`, `SaveHip`, `SetWork`, `SetSprint`, `Release` | Play-mode driving and pose tuning |
| `AgentScripts/Hands/ArmPlayReview.cs` | `Action(kind, progresses, tag)`, `Still(tag)` | Play-mode captures of one action at chosen progress values |
| `AgentScripts/Hands/ArmReview.cs` | `Gun`, `Strip` | Edit-mode renders of a clip (quick, but not what the camera crops) |
| `AgentScripts/Weapons/AttachmentSetup.cs`, `RealisticGunStats.cs` | `Mounts`, `GunIcons`, `Apply` | Mounts, icons and stats for a new gun |

## Adding a gun, start to finish

1. **Pick it** from CASTLE_BRAVO_GUNS.md (Easy first). Ask the user before adding one.
2. **Block-out:** add a row to `GunBlockoutSetup` (source prefab, reference gun, sight height, muzzle, left-hand point), run `One("<Gun>")`. Add its `Items.csv` row (LOCALIZATION.md), its `GunSpec` in `AttachmentSetup` (then `Mounts`, `GunIcons`) and its row in `RealisticGunStats` (then `Apply`). Add it to `GunPartsTests`, `AimReview.BlockOuts` and `WeaponsTestSetup`'s tables.
3. **Animate:** `GunAnimateSetup.Animate("<Gun>", "AK47")` for rifles and SMGs, `"Pistol"` for pistols. Check its report: parts it could not find by suffix (`Parts` table) are created as empty nodes and will not move.
4. **Retarget the reloads:** add an `Entry` to `ReloadRetarget.Table` (pack weapon, its clips, and the pack's part names: `Mag`, `Charge` are `"Mag"` and `"Charge"`, `"ChargingHandle"` or `"Slider"` depending on the weapon; `Describe` lists them), then `Bake`.
5. **Repair clips:** `RepairClips.Bake("<Gun>")`. Copy-pasted repair profiles carry the reference gun's `ShakeChance`.
6. **Magazines in `AttachmentSetup`** become `"Magazine", "ReplacementMagazine"`; rerun `Mounts`.
7. **Tests:** add the gun to the `TestCase` lists in `WeaponAnimationTests` (clips bind, repair impacts, parts restored).
8. **Review in play** (below) and tune: hip pose, work pose, aim depth. Sprint pose: `AimReview.SetSprint(gun, 10, -40, -30, 0.22, -0.2, 0.32)` for long guns, `(gun, 35, -10, -10, 0.12, -0.24, 0.45)` for pistols (rotation, then where the right grip sits from the eye).

`WeaponsTestSetup.Build` rebuilds and **saves the whole Weapons Test scene**: ask the user before running it.

## Re-baking

Order matters, each step reads the one before:

1. `ReloadRetarget.Bake` (resets `ItemHands` hand scale to 1, rewrites the idle hold, rebases the gun's other clips).
2. `RepairClips.Bake` (reads the reloads).
3. Run `WeaponAnimationTests`, `GunAnimationTests`, `HandRigTests`.

Re-bake after changing: the arms' scale or `GripPoint` (`ArmSetup`), `HandRig` palm thickness or finger radius, a gun's part positions, or its `Table` entry.

## Reviewing in play

Edit-mode strips look fine while the game view crops the hands off. Review from the player camera:

```bash
P="$(cygpath -w "$PWD")"
unity command --project-path "$P" editor.playmode start
unity command --project-path "$P" script run --file AgentScripts/Weapons/AimReview.cs --entry AimReview.Prepare
unity command --project-path "$P" script run --file AgentScripts/Weapons/AimReview.cs --entry AimReview.Hold --args '["M4"]'
unity command --project-path "$P" script run --file AgentScripts/Hands/ArmPlayReview.cs --entry ArmPlayReview.Action --args '["Reload","0.2,0.4,0.6,0.8",""]'
unity command --project-path "$P" script run --file AgentScripts/Weapons/AimReview.cs --entry AimReview.Release
unity command --project-path "$P" editor.playmode stop
```

Per gun, look at: hip; ADS with iron sights and with each optic that fits (`AimReview.Fit`: RedDot, Holo, Scope2x, Scope4x, PistolRedDot; `"none"` removes it); firing; `Reload` and `ReloadEmpty` at three or four points; `JamReseat`, `JamRack`, `JamShake`. Paste the captures into one sheet per gun and read the sheet, then open single frames for anything doubtful.

What to check and what fixes it:

| Symptom | Fix |
| --- | --- |
| Reload or repair happens below the screen edge | Give the gun a work pose: `AimReview.SetWork(gun, x, y, z, pitch, roll)` (offset from Hip). AK47 (−0.03, 0.035, 0); M4, SCAR (−0.05, 0.09, −0.02). Same offset on a different model can be far too much: tune per gun |
| Rear sight fills the ADS view | Move `Poses/Aim` away from the eye. **More negative z is closer to the eye** |
| Support fingers stand straight up beside the forend | The grip still has a shape made for the old floating hands: `ReloadRetarget.Bake` (sets the idle hold) or `HoldLike.Apply(gun, "AK47")` |
| Hand stays on the forend through a reload or repair, test says `LeftFree` is off | The prefab's `RigBuilder` layer lost its `Rig` (hand targets never follow their constraint). `GunAnimateSetup` now sets it outright |
| Hip gun too big, elbow bend showing | Hip pose: `AimReview.Hip(x, y, z, roll)` live (it re-aims the barrel at the screen centre 20 m out), then `SaveHip` |
| Arms fold shut or stretch when the gun moves to another pose | Shoulders and elbows must go through `ItemHands.Carry` (pose pivot as anchor, Hip as rest) |
| Upper arm or elbow in view while sprinting | `Carry` moves the points by the pose's move only (turning them with a tipped muzzle swings the shoulder in front of the eye), and `HandSet.LimitShoulder` caps the rest. If it still shows, the sprint pose holds the grip too far out or too high: rerun `AimReview.SetSprint` with the grip about 0.3 m from the eye, below the screen's middle (WEAPONS.md → Poses) |
| Support arm straight, hand only reaching the front of the gun | The forend is out of reach at that pose: bring the pose closer to the eye (MP5 hip z -0.36), don't move the contact off the handguard |
| Support fingers drape over the barrel | The hold from the pack sits on the side with the fingers over the top: `HoldLike.Apply(gun, "AK47")` (palm under the forend; it rebases the gun's clips) |
| Hands fill the ADS view | The Aim pose is too close: pistols at z -0.22, rifles about -0.4 to -0.68 |
| Hand folded at the wrist, forearm arriving from above | The solver gives way for the wrist (HANDS.md → Wrist comfort: `_shoulderGive`, `_wristShare`, and their into-view caps). If it is capped out, the hold itself is turned wrong: re-author or roll the hold, do not loosen the caps |
| Upper arm or elbow blob at the bottom of the hip view | The caps are too loose (`_giveIntoView`, `_elbowIntoView`), or a grip authored for the floating hands asks for an arm from in front |
| Palm sinks into the gun | `HandRig._palmThickness` (0.028 for the Sidekick arms), then re-bake |

## Traps

- **Never bake in Play mode.** `WorldItem.Awake` hides the held model on a fresh instance, the fit finds nothing and writes open fingers. The bake scripts refuse; don't remove the guard.
- **Stop Play before editing prefabs or pose markers**; play-mode prefab edits are lost, and `SetWork` / `SaveHip` write assets.
- **One hand scale.** The arms carry their own size (`ArmSetup.ArmScale`); a per-gun `ItemHands` hand scale on top doubles it. Guns stay at 1.
- **Magazine hold:** take it where the *replacement* magazine arrives home, not where the old one first moves. AR magazines drop free while the hand is still on the forend.
- **A reload that uses the bolt release never racks** (M4, SCAR): `RepairClips` falls back to a straight 7 cm pull and says so in its report.
- **Rotation curves:** set a rotation's components in one `AnimationUtility.SetEditorCurves` call (or on a cleared clip). One at a time, Unity re-matches the quaternion against the half-edited rest and the hand jumps at the next key.
- **Components that depend on each other** (`GunAnimation` and `RigBuilder` need the `Animator`): remove dependants first, add in the other order.
- **`AttachmentSetup.Mounts` finds parts by name**: after `GunAnimateSetup` renames them, update the gun's spec, or it throws on the missing name.
- **AK-74M is a variant of the AK47 prefab**: it inherits clips, poses and the work pose; rerun `GunBlockoutSetup.Ak74m` after `Mounts`.
- **Arm prefabs keep the values they were imported with.** A new `ArmRig` field shows its code default only until the prefab is next saved; write tuned values into the four `FPArm_*` prefabs explicitly.
- **Captures drop** when two are requested within a few frames ("Failed to capture screen shot" in the console): wait three frames between them.
- **Git Bash heredocs break on apostrophes**: write edit scripts to a file.

## Not done yet

- Shotguns: reloads are still hand-keyed; their slap repair clips look wrong with arms, so they clear jams with the shake only.
- The Rack repair bends the wrist hard on the M4, SCAR and Desert Eagle: the bolt hold needs rolling towards the elbow in `RepairClips`.
- A fitted vertical or angled grip does not move the support hand (`GunMounts.SupportGrip` is ready; see HANDS.md → Known gaps).
- The Glock's slide does not move (no separate part on the model).
- `<Gun>_RepairEnd` is still the old hand-keyed clip.
