# Weapons

Guns (shooting, ammo, reloading, jams, recoil, aiming, bullets and their effects) and [melee](#melee) (swings, hits, knockback). Guns are ported from the old project's `WeaponSystem` and rebuilt on this project's rules (plain C# rules + one view, central ticks, pooling); melee is new, built the same way. Items, the hotbar and switching are in [EQUIPMENT.md](EQUIPMENT.md); script locations in [CODE_STRUCTURE.md](CODE_STRUCTURE.md).

Today: an **AK-47** (auto), a **pistol** (semi-auto), an **MP5** (auto, shares pistol ammo), a **double-barrel shotgun** (break action), a **pump shotgun** and a **fire axe** (melee), picked up from the table in `Scenes/Prototype.unity`, with a shooting range (backstop wall + two `TargetDummy` capsules) further ahead.

## Controls

| Input | Action | Binding (`InputSystem_Actions`, Player map) |
| --- | --- | --- |
| `Attack` | Fire (hold for auto); swing a melee weapon (hold to keep swinging) | Left mouse, gamepad west |
| `Aim` | Aim down sights while held | Right mouse, gamepad left trigger |
| `Reload` | Reload | R (no gamepad binding yet) |

## Features

| Feature | How it works | Where to change it |
| --- | --- | --- |
| Fire modes | `Auto` (hold), `SemiAuto` (one per click), `AutoBurst` (bursts while held), `SingleBurst` (one burst per click) | Gun asset → Fire |
| Fire rate | Rounds per minute, frame-rate independent (a long frame can hold several shots). Clicks faster than the rate are buffered, never extra | Fire → `RoundsPerMinute` |
| Bursts | `BurstCount` shots, then `BurstCooldown` before the next burst | Fire |
| Shotgun pellets | `Pellets` bullets per shot (one round, one muzzle flash), each up to `SpreadAngle`° off the aim. A rifle is `Pellets 1, SpreadAngle 0` | Fire |
| Travelling bullets | Plain data moved each frame and ray-tested along their path: speed, range (then they vanish), optional gravity. No rigidbodies, no GameObject per bullet (only its pooled tracer) | Gun asset → Bullet |
| Damage | Each bullet hurts what it hits through `DamageService.Deal` (`DamageType.Gunshot`, source = the shooter); the service publishes `DamageDealt` with what landed and whether it killed, which drives the hit marker and damage numbers ([UI.md](UI.md#hit-feedback)) | Fire → `Damage` |
| Never hits the shooter | Anything under the shooter's root is skipped (body, held gun). Triggers (pickups) are ignored | `IGunUser.Root` |
| Aim origin switch | **Muzzle**: bullets leave the muzzle along the barrel (follows pose and gun kick, the old behaviour). **Camera**: they leave the screen centre; the tracer still starts at the muzzle and slides onto the real path. **MuzzleToCrosshair**: a ray from the screen centre finds what the crosshair is on (or the point at full range), and bullets leave the muzzle towards it (`GunAim.MuzzleToPoint`); closer than 1 m or behind the muzzle, they use the camera direction. The aim kick (the view) moves the shots in every mode; the gun's small extra kick only moves them in Muzzle mode. Camera and MuzzleToCrosshair aim along the [aim sway](#aim-sway) (the crosshair's direction), not the exact screen centre | `PlayerHeldItemTuning` → Guns → `AimFrom` (live in Play mode). Set to MuzzleToCrosshair |
| Magazine | Rounds live on the gun (`GunInstance`), so they survive drop, pick up and swap. A new gun spawns full | Magazine → `Size` |
| Shared ammo by type | The player carries an `AmmoReserve`: spare rounds per `AmmoType`. Guns of the same type reload from the same pool | `Data/Weapons/AmmoConfig.asset` (starting amounts, `Infinite`) |
| Magazine reload | One timed reload; takes only what the reserve has | Magazine → `Reload = Magazine`, `ReloadTime` |
| Per-round reload | One round per `ReloadTime` until full or the reserve is empty (tube shotgun style). Pulling the trigger stops it once the round going in is loaded, then that click fires (no instant shot from the reload pose) | Magazine → `Reload = PerRound` |
| Pump / bolt action | After each shot that leaves a round, the gun works its action for `CycleTime` (`GunAction.Cycling`) before it can fire again. Aiming stays up. A click during the pump is kept and fires when it's done. After the last round there's nothing to chamber, so it goes straight to the reload. Switching away mid-pump finishes the pump when the gun is raised again. 0 = self-loading | Fire → `CycleTime` |
| Auto reload | Starts by itself when the magazine runs dry and there's ammo | Magazine → `AutoReload` |
| Jams | Each shot has `Chance` to jam instead of firing (no round used). A jammed gun can't fire: it stays stuck for `JamPause` (0.6 s) while trigger pulls only dry-click, then all five guns automatically clear over 4-5 seconds, with an optional timing QTE; guns without a profile retain automatic clearing over `ClearTime` | Gun asset → Jam |
| Recoil | Two parts per shot, along a random spray pattern that restarts after a 0.24 s pause. **Aim kick** turns the view (up, sideways, roll, shake), so the crosshair and the shots climb together. **Gun kick** is a small, fast extra tilt + push-back of the model only, on top of the view it's attached to, so the barrel stays near the crosshair. Both spring back. Hip-fire and aiming have separate values | Gun asset → Recoil → Aim kick / Aim response / Gun kick |
| Recoil modifiers | Extra kick while moving / airborne; the first `AccurateShots` of a spray kick less | Recoil |
| Stamina and fatigue | Each shot spends `StaminaPerShot` (pellets count as one) without pausing regen (18/s), so only fire rate × cost above the regen rate drains you: an AK spray does, pistol shots barely register. Low stamina never blocks firing: it multiplies recoil (aim kick, gun kick and shake) by the player's fatigue tier, eased in. See [Fatigue](#fatigue) | Fire → `StaminaPerShot` (AK47 4, MP5 2.6, Pistol 0.7, PumpShotgun 8, DoubleBarrel 9) |
| Airborne inaccuracy | Firing in the air sends the shot up to `AirborneSpread`° (2) off the aim, on top of the visible air drift. Aiming down sights keeps `AimMultiplier` of it. It rises at once and settles at `SettleSpeed` after landing. The whole shot moves, so a shotgun's pattern shifts as one. Walking adds no hidden spread: it shows as aim sway. The player's `StatId.MotionSpread` (base 1) scales it, for skills | Gun asset → Accuracy (all five guns: 2° / 0.4 / 6) |
| Aim sway | The crosshair, the shots and the gun move together off the screen centre: breathing, walking bob, move / turn lag, jump, air drift, landing, heavier when out of breath. See [Aim sway](#aim-sway) | `PlayerHeldItemTuning` → Aim sway |
| Model sway and bob | Extra motion of the held model only, which never changes where it points: position drift (breathing, bob, look / move lag, jump sink, landing dip) and roll around the barrel. Aiming calms each part. `StatId.HeldItemSway` (base 1) scales it and the aim sway, for skills | `PlayerHeldItemTuning` → Sway & bob (model only) |
| Poses | Hip, Aim and Sprint pose markers in the gun prefab; the gun blends between them. The Hip pose is turned so the barrel points at the screen centre 20 m ahead (bullets from the muzzle don't cross the crosshair at an angle) | Prefab → `Poses/*`; blend speeds: Gun asset → Handling |
| Aiming rules | Sprinting blocks fire and aim. Reloading and clearing a jam drop the aim (sprinting is allowed while reloading). Switching away cancels a reload or jam clear (it restarts next time) | `Gun.Tick` |
| Slower while aiming | Move speed × `AimMoveSpeedMultiplier` | `PlayerHeldItemTuning` → Guns |
| Aim zoom | Aiming down sights narrows the world camera to `AimZoom` × its field of view (0.87 = 13%), easing with the aim pose; the held gun keeps the unzoomed field of view on its overlay camera, and look speed scales with the zoom so aim feel stays the same | Gun asset → Handling → `AimZoom` |
| Hands | Floating hands follow each gun's two hand targets; the clips move the gun's own parts (mag, bolt, slide, pump, hinge, shells) and switch the left hand's contact between them | Gun prefab → `ItemHands`, clips; [HANDS.md](HANDS.md) |
| Reload / jam / pump motion | Every gun plays editable clips with hand contact constraints (`GunAnimation`, required on a gun prefab). One clip per `GunAction`, sampled at `Gun.ActionProgress`, so it always fits `ReloadTime` / the repair time / `CycleTime` | Gun prefab → AnimationRoot / clips |
| Pose preview | Hip / Aim / Sprint buttons on `HeldGun` snap the model to a pose in the prefab | `HeldGun` inspector |
| Effects | Muzzle flash, tracer, impact (removed after 3 s), bullet hole decal (removed after 20 s, at most 64). All pooled; no holes on things that take damage | `Data/Weapons/BulletEffects.asset`; lifetime / cap on each effect prefab's `PooledEffect` |
| Sounds | Equip, fire, reload start / insert / end, jam (trigger falls on the jammed round), dry fire (each pull while stuck), jam clear start / end, repair slap / punch, cycle start / end (pump back / home) as BroAudio `SoundID`s, played 2D through `AudioService` (pooled by BroAudio, so a sound isn't cut when the gun is holstered). Entities live in `Data/Audio/Guns/` (one per purpose and clip, e.g. `Fire_AssaultRifle1Fire`); each entity holds its own volume, random clips (`RepairSlap`, `RepairPunch`) and pitch variation. `Sounds.Volume` scales a whole gun. Clips are placeholders from the old project's samples (`Assets/_Samples/Audio/Weapons`) and Malbers / Highlands store audio; the shotguns borrow sniper and pistol clips | Gun asset → Sounds; entities in `Data/Audio/` (BroAudio Library Manager) |
| Crosshair | A 6 px dot that follows the aim sway (`CrosshairHud`), fades while aiming down sights (the sights take over) and while sprinting. The hit marker is its child, so it follows the dot | `HUD` → `SafeFrame/Center/Crosshair` |
| Ammo on the HUD | Magazine / reserve in the corner of each gun's hotbar slot (`30/90`; magazine only when the reserve is infinite), red when empty or jammed | [UI.md](UI.md#hotbar) |

Cut from the old system: built-in fallback recoil assets, legacy Animator triggers, the laser sight, the `Item` / `Weapon` / `IEquipable` bases (replaced by the item definitions and `EquipmentSwitcher`), and the hidden "legacy" stat copies. AK47 now has its own clip/constraint presentation, described below.

## Layers

```text
Data
  GunDefinition (Data/Items/)   Fire, Bullet, Magazine, Jam, Recoil (Hip / Aim), Handling, Sounds (BroAudio SoundIDs), Effects
  AmmoType (Data/Weapons/)      a kind of round
  AmmoConfig                    starting reserve + Infinite (on the Player prefab)
  BulletEffects                 effect prefabs + bullet hole settings, shared by guns
  MeleeDefinition (Data/Items/) Swing, Sounds, ImpactEffect

Rules (plain C#, EditMode-tested in GunTests)
  GunInstance                   Rounds, IsJammed (per copy)
  AmmoReserve                   counts by AmmoType, Take / Add, Infinite
  GunTrigger                    when shots happen (mode, rate, bursts)
  Gun                           one held gun: trigger + magazine + reload / jam clear + aim; Fired, Jammed, Action events
  GunRecoil                     aim kick → ViewEuler, ViewShake; gun kick → GunEuler, GunKickBack; spring-back
  BulletSpread                  direction inside a cone
  MeleeAttack                   one held melee weapon: swing timing, hold to repeat, buffered click; SwingStarted, Struck

Views and services
  HeldGun : HeldItem            on the gun prefab: feeds Gun from HeldItemInput, spawns bullets, applies poses and recoil, sounds
  HeldMelee : HeldItem          on the melee prefab: feeds MeleeAttack, swing motion, hit check → damage, knockback, impact, sounds
  BulletSystem  (root, tick)    bullets in flight → hits → damage, impact, hole
  EffectPool    (root, tick)    one pool per effect prefab, timed returns, MaxActive
```

Who calls what each frame: [CODE_STRUCTURE.md → Frame order](CODE_STRUCTURE.md#frame-order-playerupdate). In short, `Player` builds a `HeldItemInput` from its intent and motor state, `EquipmentRig.TickHeld` passes it to the held item (with `IsReady` from the switcher), and `Player` then applies the item's `ViewKick` to the look and its `IsAiming` to the motor speed.

## Gun prefab

```text
AK47 (variant of Gun_Base)    HeldGun
├─ Poses
│  ├─ Hip                     where the gun rests (relative to the rig's Hand), barrel on the screen centre 20 m out
│  ├─ Aim                     sights on the screen centre
│  └─ Sprint                  lowered and turned
└─ PosePivot                  moved to the active pose
   └─ RecoilPivot             gun kick
      ├─ Model                the store prefab (shadows off, no colliders)
      └─ Muzzle               bullets and the flash leave here, along its blue (Z) axis
```

Shotgun parts move only in their clips: `PumpShotgun`'s Cycling clip slides the pump; `DoubleBarrel`'s barrels, rib and forend sit under a `Hinge` at the front of the receiver that its reload clip opens.

Poses came from the old project (gun root at (0, −0.15, 0.3) under the camera) and are stored relative to `Hand`.

The current closer framing uses Hip Z = −0.40 m for AK47/MP5/both shotguns and −0.114 m for pistol. Aim Z = −0.46 m for AK47, −0.49 m for MP5, −0.44 m for both shotguns, and −0.34 m for pistol; Sprint Z = −0.18 m for MP5 and −0.10 m for the others. Tune eye distance with each gun's `Poses/*` local Z (more negative brings it closer), then use the Hip preview before saving. The shared Hand anchor remains at (0.17, −0.21, 0.62), and hand/weapon scale is separate from this framing.

MP5's `Sight` uses our `WeaponSystem/Meshes/MP5_Sight.asset` copy. Only the disconnected aperture drum is leveled by 25° and aligned to the front post at model Y = 0.15924 m; its mounting base stays in place. The source FBX is unchanged. `Poses/Aim` uses position (−0.17, 0.05076, −0.49) and zero rotation so the aperture axis and front-post tip line up with the camera. Moving the gun alone cannot correct the source mesh's tilted bore. Its lower hip pose uses position (−0.095, 0.04, −0.40), independently of ADS.

### Gun clips and constraints

AK47, MP5, Pistol, PumpShotgun and DoubleBarrel keep their `Gun_Base` variants and pose/recoil pivots. Under `RecoilPivot`, `AnimationRoot` adds an Animator using `Animations/<Gun>/<Gun>.overrideController`, a RigBuilder and `GunAnimation`. `AnimationRoot/Motion/Weapon` holds our unpacked copy of the store model with generic part names. Shared mesh/material assets are still referenced; original store prefabs are untouched. The inherited base muzzle marker remains as `UnusedMuzzle`; `HeldGun` references the animated `Weapon/Muzzle` instead (under `Weapon/Hinge` on DoubleBarrel).

The shared `Animations/Shared/Gun.controller` has one state per `GunClip` (Equipped, Idle, Firing, Reloading, ReloadingEmpty, Repairing, RepairingStubborn, RepairFocus, RepairEnd, Cycling); each name is also the shared clip an override replaces, so add a state, a shared clip and an enum value together. Each weapon Override Controller supplies clips for its actions; AK47 does not use Cycling. Gameplay chooses states explicitly; the controller has no automatic transitions. `GunAnimation` uses a manual AnimatorControllerPlayable graph with Animation Rigging outputs, sampled once from `HeldGun.Tick` before final muzzle/hand use. Reloads, repairs and cycles sample `Gun.ActionProgress`; ammo still transfers only when `Gun` completes a reload or shell insertion. A weapon switch cancels the action and the next show starts cleanly with Equipped. Reload/cycle timing, ammo and recoil remain owned by gameplay; repair initiation and QTE behavior are described below. State hashes and clip lengths are cached when the graph is built, so sampling allocates nothing per frame.

AK47, MP5 and pistol reloads separate the old magazine, an animated replacement, and a pooled world magazine (`Prefabs/Effects/<Gun>_Magazine.prefab`, Rigidbody + collider + PooledEffect). The existing EffectPool prewarms, caps and expires these cosmetics. The drop does not consume inventory ammo or create pickups. Empty reloads add the bolt/slide rack. PumpShotgun's reload clip repeats once per shell; its Cycling clip drives the pump and bolt with the hand attached. DoubleBarrel opens its hinge, loads a pair of shells, and closes. Shotguns have no detached magazine effect. All five guns use the multi-slap repair sequence described below. Right-hand contact remains on the grip; left-hand contacts, free paths and fingers are clip-authored. All five use hand scale 1.12. See [HANDS.md](HANDS.md#weapon-animation-authoring) for editing steps and contact weights. Melee remains procedural.

## Adding a gun

1. **Prefab:** right-click `Prefabs/Guns/Gun_Base.prefab` → Create → Prefab Variant. Drag the store model under `PosePivot/RecoilPivot`, rename it `Model`, turn off its shadows and remove colliders. Move `Muzzle` to the barrel tip (blue axis out of the barrel).
2. **Poses:** select the gun root, use the **Aim** preview button, and move `Poses/Aim` until the sights sit on the screen centre in the Game view (enter Play mode to check; edits to the prefab in Play mode are lost). Do the same for Hip and Sprint, then press **Hip** before saving.
3. **Item:** duplicate `Data/Items/AK47.asset` (or Create → Aipocalypse → Items → Gun). Set `DisplayName`, `Icon`, `Prefab` (the variant), Fire / Bullet / Magazine (pick or create an `AmmoType` in `Data/Weapons/`) / Jam / Recoil, and `Effects` (`Data/Weapons/BulletEffects.asset`).
4. **Clips and hands:** add `AnimationRoot` under `RecoilPivot` (Animator with a new Override Controller of `Gun.controller`, RigBuilder, `GunAnimation`) and assign it to `HeldGun` → Animation; a gun without one logs an error and stays inert. Point `ItemHands` at the two `Motion/HandTargets` grips ([HANDS.md → Weapon animation authoring](HANDS.md#weapon-animation-authoring)).
5. **World side** (same prefab, see [EQUIPMENT.md → One prefab per item](EQUIPMENT.md#one-prefab-per-item)): add `WorldItem` (`_definition` = the asset) and a trigger `BoxCollider` to the root, and a `World` child with the store model lying flat (shadows on); set `WorldItem._worldModel` to it and turn `World` off. Drop copies of the prefab into a scene to place pickups.
6. **New ammo type?** Add its starting amount to `Data/Weapons/AmmoConfig.asset`.

### Shotguns

Same steps with `Pellets` > 1 and `SpreadAngle` > 0 (each pellet does `Damage`); they share `Data/Weapons/ShotgunShells.asset` (12 gauge). The kind of shotgun is just settings:

| Kind | Example | Fire | Magazine | Held prefab |
| --- | --- | --- | --- | --- |
| Break action | `DoubleBarrel` (9 pellets, 6°) | `SemiAuto`, `CycleTime 0` (one barrel per click) | `Size 2`, `Reload = Magazine` (both shells at once) | break part = a hinge over the barrels |
| Pump, tube-fed | `PumpShotgun` (8 pellets, 5°) | `SemiAuto`, `CycleTime` 0.55 s | `Size 6`, `Reload = PerRound` (firing stops it after the current shell) | pump part = the forend |
| Semi-auto, tube-fed (XM1014) | — | `SemiAuto`, `CycleTime 0` | `PerRound` | none |
| Auto, box mag (Saiga-12) | — | `Auto`, `CycleTime 0` | `Reload = Magazine` | none |

## Melee

| Feature | How it works | Where to change it |
| --- | --- | --- |
| Swing | Click: `WindUp` seconds to the hit, then `Recovery` before the next swing can start. Holding keeps swinging at that rate (frame-rate independent); a click during a swing queues one more, never extra | Melee asset → Swing |
| Hit check | At the moment of the hit (after the camera is placed for the frame), one sphere-cast from the screen centre: `Radius`, `Range` from the eye, `HitLayers`. The first thing hit counts; one target per swing. The holder and triggers (pickups) are skipped | Swing → `Range`, `Radius`, `HitLayers` |
| Damage | `DamageService.Deal` with `DamageType.Melee`, source = the swinger | Swing → `Damage` |
| Knockback | The hit pushes along the view: `Knockback` N·s. Rigidbodies get the impulse; damageables get it as `DamageInfo.Force` and decide how to move (`TargetDummy` tips over and springs back; enemies later) | Swing → `Knockback` |
| Swing motion | Procedural, between pose markers: Idle → WindUp (eased) → a fast strike through the hit to FollowThrough (`_strikeTime`, the hit lands halfway) → back to Idle. Swings in a row alternate sides: every other one mirrors the poses left-right around Idle (a backhand). After a 0.5 s pause it starts on the first side again | Melee prefab → `Poses/*`, `HeldMelee` → Swing motion |
| Stamina and fatigue | Each swing spends `StaminaPerSwing` as it starts, hit or miss, without pausing regen. Damage and knockback are multiplied by the player's fatigue `MeleePower` at the moment of the hit | Swing → `StaminaPerSwing` (fire axe 15) |
| Rules | Sprinting starts no swing (the current one finishes). Switching away cancels a swing that hasn't landed | `MeleeAttack.Tick` |
| Effects, sounds | `ImpactEffect` at the hit point (the fire axe uses the bullet impact). Equip, swing (at the start), hit flesh (something that takes damage), hit surface, as BroAudio entities (`Data/Audio/Melee/`) played through `AudioService`. The fire axe uses Malbers' common audio: `Melee Swing`, `Flesh Hit`, `Wood Impact` | Melee asset → Sounds, `ImpactEffect` |
| Pose preview | Idle / Wind-up / Follow-through / Sprint buttons on `HeldMelee` | `HeldMelee` inspector |

```text
FireAxe                       HeldMelee
├─ Poses
│  ├─ Idle                    where it rests (relative to the rig's Hand)
│  ├─ WindUp                  raised, ready to strike
│  ├─ FollowThrough           where the swing ends, past the hit
│  └─ Sprint
└─ PosePivot                  moved along the swing / to the active pose (the swing turns around this point)
   └─ Model                   the store prefab (shadows off, no colliders)
```

**Adding a melee weapon:** duplicate `Prefabs/Melee/FireAxe.prefab`, swap the `Model`, and move the pose markers using the preview buttons (the hit doesn't depend on them, only the look does). Duplicate `Data/Items/FireAxe.asset` (or Create → Aipocalypse → Items → Melee), set `Prefab`, Swing, Sounds and `ImpactEffect`, and point the root `WorldItem._definition` at the new asset (swap `World/Model` too).

## Aim sway

`HeldItemSway` (player part) turns the movement state into one **aim offset**: degrees off the screen centre (x right, y up). Everything that points uses it, so the barrel can't point one way while the crosshair shows another:

- **Shots:** `IGunUser.AimOffset` turns the eye's forward; `Camera` and `MuzzleToCrosshair` aim along it.
- **Crosshair:** `Player.CrosshairPosition` projects it with the world camera's current field of view; `CrosshairHud` moves the dot there.
- **Model:** `EquipmentRig.SetAimRotation` turns the whole rig, which sits at the eye, so the gun pivots around the eye. The weapon camera keeps the unzoomed field of view while aiming zooms the world, so the rig turns further by the zoom (`Player.ModelAimRotation`): the sights land on the crosshair's pixel at any zoom.

The model's own extras (position drift, roll) don't change where it points. Gun kick (recoil, model only) and the reload / jam / pump clips do: the barrel leaves the crosshair briefly, while you can't fire anyway.

| Situation | Aim motion (hip defaults) |
| --- | --- |
| Standing | Breathing: a slow figure-8, 0.15° sideways × 0.2° up / down, 0.28 breaths/s |
| Walking forward / back | Bob in step with the head bob: 1° up / down each step, 0.35° sideways per two steps; the aim dips 0.08° per m/s |
| Strafing | 1.6° sideways per two steps, 0.3° up / down; trails 0.2° per m/s against the move. Diagonals blend by direction |
| Stopping | The drift springs back with a small overshoot (spring 90, damping 0.5): wait a beat for a steady shot |
| Turning | Trails 0.6° per 100°/s, at most 1° |
| Jump | Dips 1.2° on takeoff |
| Airborne | Slow random drift up to 2.5°, plus the gun's `AirborneSpread` |
| Landing (from 2 m/s) | Dips 0.4° per m/s of landing speed (at most 4°) with a random sideways jolt (30% of the dip), then wobbles and settles in about half a second |
| Sprinting | Bob × 2.5; the crosshair is hidden (you can't fire) |
| Out of breath | Breathing grows up to × 3 and speeds up to 0.75 breaths/s as `PlayerBreathing.Tiredness` builds (under 25% stamina, full when empty / exhausted); × 1.5 more while [winded](#fatigue) |

**Aiming down sights** keeps 70% of the breathing, 30% of the bob and move / air drift, 50% of the turn lag and of jump / landing kicks. Raising the sights overshoots 0.5° upwards and settles. The offset is clamped to 4° at the hip, 2° aimed.

`StatId.HeldItemSway` (base 1) scales the aim sway and the model sway, for skills that steady the hands.

## Fatigue

Low stamina makes fighting and moving worse, never impossible. `Fatigue` (player part, ticked after `Stamina`) picks the lowest tier whose threshold is above current stamina and eases its multipliers in over `EaseTime` (0.35 s), so crossing a threshold never snaps mid-spray.

| Stamina under | Recoil | Melee damage + knockback | Walk / sprint speed |
| --- | --- | --- | --- |
| 50% | ×1.25 | ×0.85 | ×0.95 |
| 25% | ×1.6 | ×0.65 | ×0.85 |
| 10% | ×2.5 | ×0.4 | ×0.7 |

Exhaustion (stamina hit 0, until it refills to 30%) still blocks only sprint and jump; shots and swings can drain stamina to 0 and exhaust you.

**Winded:** running dry stops regen for `WindedTime` (2 s, `PlayerVitalsTuning`) before recovery starts. Meanwhile walk and sprint speed drop by a further `WindedMoveSpeed` (× 0.75, eased with the tiers) and the aim breathes harder. Draining an already exhausted meter to 0 again doesn't restart the wait.

**Out of breath** (under 25%, or exhausted): `PlayerBreathing.Tiredness` builds as stamina drops (1 at empty / exhausted) and eases over `EaseTime` (0.6 s). The player loops `BreathSound` at `MinVolume`..1 by tiredness (fades in 0.4 s, out 1.5 s once you've caught your breath), and the aim sway breathes harder and faster with it.

Two stats (base 1) are the hooks for later growth: `StatId.AttackStaminaCost` scales every shot and swing cost; `StatId.FatiguePenalty` scales how far the tiers move from 1 (0 = no penalty). Weapons reach all of this through `IWeaponUser.Exert` / `MeleePower` and `IGunUser.FatigueRecoilScale`.

## Tuning

| To change | Edit |
| --- | --- |
| A gun's numbers | `Data/Items/<Gun>.asset` (live in Play mode) |
| A melee weapon's numbers, sounds | `Data/Items/<Melee>.asset` (live in Play mode) |
| How a melee swing looks | melee prefab → `Poses/*`, `HeldMelee` → Swing motion |
| Muzzle vs camera aiming, slower walk while aiming | `Data/Player/PlayerHeldItemTuning.asset` → Guns |
| Spread in the air | `Data/Items/<Gun>.asset` → Accuracy |
| Aim sway: crosshair / shot / gun motion per situation, ADS shares, limits | `Data/Player/PlayerHeldItemTuning.asset` → Aim sway |
| Model-only sway, bob, look / move lag (all held items) | `Data/Player/PlayerHeldItemTuning.asset` → Sway & bob (model only) |
| Fatigue tiers, winded speed, ease time | `Data/Player/PlayerVitalsTuning.asset` → Fatigue |
| How long you stay winded at 0 stamina | `Data/Player/PlayerVitalsTuning.asset` → `WindedTime` |
| Breath sound, threshold, volume, fades | `Data/Player/PlayerSoundTuning.asset` → Out of breath |
| Starting ammo, infinite ammo | `Data/Weapons/AmmoConfig.asset` (read at spawn). In Play mode, `Player` → **Add Ammo** gives more |
| Which effects a gun uses, bullet hole layers / size | `Data/Weapons/BulletEffects.asset` |
| How long an effect lives, how many at once | the effect prefab in `Prefabs/Effects/` → `PooledEffect` |
| Where the gun sits, aims, sprints | gun prefab → `Poses/*` |
| Reload / jam / pump motion, pump stroke, break angle | the gun's clips in `Animations/<Gun>/` ([HANDS.md](HANDS.md#weapon-animation-authoring)) |

## Known gaps

- **No breath clip yet:** `PlayerSoundTuning` → `BreathSound` is unset (silent) until a BroAudio entity with a breathing clip (Loop on) is added to `Data/Audio/Player`. The visual breathing rate (`TiredBreathRate`) isn't synced to the clip; tune it by ear.
- **Sound placeholders:** every gun sound is a stand-in clip (see Sounds above); swap the clips on the BroAudio entities, no code or gun asset changes needed.
- **Crosshair is a plain dot** with a hit marker round it (no spread indicator for the airborne spread, `HeldGun.MotionSpread`). It matches exactly with `Camera` and `MuzzleToCrosshair`, only roughly with `Muzzle` (there the model's zoom correction also turns the barrel a little more than the view while aiming).
- **Nothing in `Prototype.unity` takes damage yet:** the wander capsules aren't damageable and the scene has no `TargetDummy`, so hit feedback only shows on a dummy you add (`_Debug/TargetDummy`).
- **Clip authoring gaps** (their tests are marked `[Ignore]` with the reason): MP5 / pistol left-hand free control is ~1° off the magazine grip at the 0.145 switch; the AK47's is ~5.5° off the forend at 0.02; the DoubleBarrel's free hand is ~5 cm / 9° off the shell grip at 0.28 and its hinge curve overshoots ~0.5° after closing. Fix the keys in the Animation window (script edits of these clips re-tangent or rebind them), then drop the `Ignore`s in `WeaponAnimationTests`.
- **Close cover:** Muzzle / MuzzleToCrosshair check the eye-to-muzzle segment. If blocked, the shot and tracer start at the eye toward that obstruction so the bullet hits nearby cover rather than starting beyond it. Camera aiming keeps its screen-centre origin.
- **No dry-fire click** when the magazine and reserve are empty.
- **Ammo only from `AmmoConfig`:** there are no ammo pickups or inventory yet.
- **Shotguns have no damage falloff**: each pellet does full damage until the bullet's `Range`; spread alone thins hits at distance. No shotgun sound clips yet (placeholders above), and the pump's start sound plays with the shot.
- Held weapons and muzzle flashes render through the weapon overlay camera. A smooth obstacle pose retracts and tilts the model near walls and entities without pushing the player ([EQUIPMENT.md](EQUIPMENT.md#first-person-rendering-and-obstacle-pose)).
- **Melee:** no hit-stop or camera jolt yet; one target per swing (no cleave); the backhand is a mirror of the forehand rather than its own poses; the swing motion is procedural until there are animations. `Player` ignores `DamageInfo.Force` (nothing hits the player yet).


## Gun jam clearing and QTE

AK47, MP5, Pistol, PumpShotgun and DoubleBarrel each assign their own Jam.Repair profile (Data/Weapons/<Gun>_Repair.asset) and timing challenge (<Gun>_RepairQte.asset). These are separate assets so duration, QTE onset, fill speed, green zone and presentation choices can be tuned per gun. When a shot jams, the trigger falls on the jammed round (Jam click, a short gun wobble and a small camera jolt) and the gun stays stuck for `JamPause` seconds (default 0.6); each trigger pull in that window dry-clicks with a small twitch (`Gun.DryFired`). Clearing then starts automatically (with `RequireReloadPress` it waits for Reload instead, without the pause). The repair selects a 4-second three-hit or 5-second four-hit sequence once, alternating an open-palm slap and a closed-fist punch (slap, punch, slap[, punch]), with a configurable stubborn probability. The animation and gameplay timer keep advancing normally whether the player ignores, misses or succeeds at the QTE. Clearing does not restore durability or change ammo.

At QteStartProgress (default 0.5), HeldGun starts an owner-controlled QTE. This value is a fraction of the repair duration: 0.5 appears halfway, 0.25 appears after one quarter. Lowering it gives more opportunities to finish early and can later be driven by an upgrade. The QTE repeats until Gun completes the repair, succeeds early, or is interrupted; it has no independent timeout and never holds the repair timer. Each gun's default WorldTimeScale is 1, so the event makes no time-scale request. The UI appears only during the QTE, near the screen centre above/beside the gun, without a panel, heading, status popup or separate countdown.

Press Reload (R) while the fill is in the green zone to clear the jam immediately. A miss waits for the next fill and adds `MissPenalty` seconds (default 0.5) to the remaining repair, up to `MaxMissPenalty` (1.5) per repair. `Gun.AddRepairTime` stretches what is left of the action instead of moving progress back, so the clip slows down rather than jumping. InputService consumes the QTE press so a success cannot also start a reload. Gun remains the only owner of completion. On natural completion, the QTE closes with a short fade. On success, GunAnimation captures the current part transforms and hand shapes, then blends into RepairEnd over that clip's duration. Remaining slaps are skipped, so early success cannot fast-forward extra impacts or sounds. Switching, hiding, destruction, player death or active-player change cancels the QTE and leaves an interrupted gun jammed.

| Asset / field | Default | Meaning |
| --- | --- | --- |
| <Gun>_Repair / RequireReloadPress | false | Automatically begin clearing |
| QuickDuration / StubbornDuration | 4 / 5 s | Complete repair duration, including the time while the QTE is visible |
| StubbornChance | 0.5 | Probability of selecting the longer slap variant |
| JamPause | 0.6 s | Stuck time after a jam before automatic clearing; trigger pulls dry-click |
| QteStartProgress | 0.5 | Fraction elapsed before the QTE appears; lower values appear earlier |
| MissPenalty / MaxMissPenalty | 0.5 / 1.5 s | Remaining repair time each miss adds, and the most misses can add to one repair |
| <Gun>_RepairQte / CycleDuration | 0.9 s | Real seconds per fill |
| GreenZoneCenter / GreenZoneWidth | 0.72 / 0.18 | Normalized success interval; width 0.18 means 18% of a fill |
| OneAttemptPerCycle | true | One input per fill; misses can retry on the next pass |
| MissPauseDuration | 0.24 s | Hold the pointer after a miss while gun repair and standalone QTE lifetime continue |
| RelocateZoneOnMiss / MinimumZoneShift | true / 0.16 | Move the zone after the pointer hold; keep its width, stay within the track, and move at least this normalized distance where track space permits |
| WorldTimeScale | 1 | No slow-motion request |
| Presentations | Circle, Bar | Random widget ID; a single entry forces that presentation |
| TotalDuration | 4 s | Used only by standalone timed QTEs, ignored by the owner-controlled repair QTE |

QteChallenge.CreateSession(ownerControlled) and IQteSession separate mechanics from the service and widgets. Timed sessions still support TotalDuration for other uses; persistent sessions close with their owner. QteStarted/QteEnded are MessagePipe lifecycle announcements. QteView is the presentation seam: TimingQteView renders the same timing session as a circle or bar. New widgets register their ID in the HUD array and challenge Presentations. A different mechanic supplies a compatible challenge/session and view. Missing IDs fall back to a compatible view.

The shared Animator retains RepairingStubborn, RepairFocus and RepairEnd slots. The automatic repair sequences do not enter RepairFocus; the slot remains available for authoring/preview. Repairing and RepairingStubborn are editable clips sampled from Gun.ActionProgress. Each gun supplies its own Repairing, RepairingStubborn, RepairFocus and RepairEnd overrides. Pistol slaps target its slide; the others target their receivers. Mechanical parts remain at rest during repair, including the shotgun pump and hinge.

For an Editor trial, enter Play, equip any of the five guns, select its spawned HeldGun and click **Jam gun (test)**. Clearing starts automatically. Ignore the QTE to see full completion or press R in the green zone to finish early. The debug button exists only in the Editor.


Repair impact audio follows a stepped `_repairImpactCount` float curve on GunAnimation in each repair clip. It starts at zero and increments once at each contact (short: 0.8, 1.7, 2.8 seconds; long: 0.8, 1.7, 3.0, 4.0 seconds). A second stepped curve, `_repairPunch`, is 1 from each punch's approach and 0 for slaps (`GunAnimation.ImpactIsPunch`). Slaps take 0.12 seconds from wind-up; punches close the hand into a fist during the wind-up, come from higher and land in 0.09 seconds with a bigger gun reaction. Gameplay duration still scales the full clip through ActionProgress; changing duration keeps the markers synchronized with the hand. Move the counter keys with the contact keys when editing a clip. These are sampled markers, not Unity AnimationEvents, because this graph samples clips manually.

GunAnimation announces each newly crossed marker once through the local RepairSlapped event, including markers skipped by a slow frame. HeldGun plays the gun's `Sounds.RepairSlap` entity (`RepairPunch` for punches, falling back to the slap), which picks a random clip at the entity's volume, adds the matching gun jolt and publishes a GunJamBeat. All five guns share the `RepairSlap` (Malbers Hit2, Wood Impact) and `RepairPunch` (Hit1, Hit3, Highlands Metal Shield 1) entities as replaceable impact variations. The bolt and magazine remain stationary during repair. No new impacts fire during pause, interruption or the QTE return. Audio remains presentation-only and cannot complete the jam.

`AgentScripts/RepairQteSetup.OtherGuns` creates independent profiles/challenges and assigns impact SFX for the four later guns. `Ak47Animations.UpdateRepairing(weapon)` regenerates only that gun's slap/punch repair clips/overrides (`RenderRepairAt(weapon, progress[])` renders review frames to Temp), preserving its reload, firing, cycling, pose and vendor-source assets.

### Repair result feedback

All five repair profiles reference Data/Weapons/RepairFeedback.asset. Duplicate this preset and assign GunRepairProfile.Feedback to customize one gun without changing its timing challenge or animation clips. Each beat has up to three layers: gun sound and an additive gun jolt (HeldGun, on the recoil pivot), a camera impulse / hit stop (`GunJamFeedback` on the Player) and, for QTE results, the HUD widget.

| Beat | Gun (sound, jolt) | Camera (`Player/GunJamFeedback`) | HUD |
| --- | --- | --- | --- |
| Jammed | Jam click, `JamJolt` wobble | small jolt | — |
| Dry fire (pull while stuck) | DryFire click, `DryFireJolt` twitch | tiny bump | — |
| Slap / punch | `RepairSlap` / `RepairPunch` entities, `SlapJolt` / `PunchJolt` | small / medium shake; the punch adds a 35 ms hit stop at 0.1× | — |
| QTE miss | MissSound + dull `MissAccent` thud, `MissJolt` fumble wobble; +0.5 s repair | sideways recoil shake | red flash, 22 px shake + squash over 0.4 s |
| QTE success | metal `SuccessSound` hit + `SuccessAccent` chime, then `SuccessMechanism` (bolt home) after 0.12 s; strong `SuccessJolt` | big shake + 80 ms hit stop at 0.05× | full green widget, 150% punch, ring to 260%, 0.32 s hold, 1 s total |
| Natural completion | quieter `NaturalSound` | light bump | 0.18 s fade |

A `GunJolt` is a position/rotation peak, a duration and an optional number of damped wobbles (0 = one kick and return). Repair completes immediately on success, so the visual hold does not delay firing. Ignored presses, cancellation and switching produce no failure feedback. The clips are placeholders from `_Samples` and Malbers/Highlands store audio.

Camera: the player's `PlayerCinemachineCamera` has a `CinemachineImpulseListener` (camera space, gain 0.015, so the impulse itself barely moves the camera) whose reaction plays `Data/Camera/ImpulseReaction.asset`, a rotation-only noise scaled by impulse strength (about 2-3° peak for a success). Each beat is an `MMF_Player` child of `Player/GunJamFeedback` with one `MMF_CinemachineImpulse` (Uniform; Bump, or Recoil for the miss) and, for punch and success, an `MMF_HitStop`. Tune one beat with its impulse Velocity length, or all of them with the listener's reaction Amplitude Gain (5). `MMF_HitStop` asks `TimeService.RequestHitStop` (real-time duration; overlapping stops keep the longest time and lowest scale; ignored while paused); the QTE widget tweens run on unscaled time so they don't freeze.

TimingQteSession tracks total lifetime separately from pointer movement. A miss holds the pointer for MissPauseDuration, rejects presses during the hold, then relocates the green zone and resumes from the held position. Green-zone width remains unchanged; the random position excludes a band around the previous position using MinimumZoneShift, clamped for wide zones. With OneAttemptPerCycle enabled, the next accepted attempt remains on the next fill. Standalone timeouts and the owning gun's repair timer keep advancing during pointer holds. Zone relocation can be disabled and a zero hold is supported.

RepairFeedbackSettings exposes all durations, colors, scale/shake/squash strengths, the six gun jolts and the result sounds (SoundIDs into `Data/Audio/RepairFeedback/`, each entity holding its own volume). The repair's QTE rules live in `GunRepairFlow` (plain C#: start at `QteStartProgress`, early finish on success, miss penalty, interrupt on cancel); HeldGun presents them: plays the sounds, publishes GunJamBeat (Jammed, DryFire, Slap, Punch, Miss), GunRepairQteStarted (owner, item, session, feedback preset) and GunRepairCompleted with owner/item, and adds the active jolt to the existing recoil pivot on scaled time (a hit stop slows it too). It does not change gameplay recoil. The HUD only listens to those gun events; it never reads the gun. IQteSession.Press returns Ignored/Miss/Success, and QuickTimeService publishes QteAttempted only for accepted attempts so repeated input in a locked cycle cannot spam feedback. Cancellation is separate from a missed attempt.

AgentScripts/RepairJuiceSetup.All (re)builds the impulse listener, reaction noise and `Player/GunJamFeedback` players, assigns the jam/punch/success/miss clips, writes the jolt defaults and each profile's jam pause and miss penalty. RepairJuiceChecks.Live verifies the jam pause, slap → punch beats, camera shake, miss penalty and success hit stop in Play mode (captures in Temp/RepairJuice; set `Application.runInBackground` first or the editor won't tick). AgentScripts/RepairFeedbackSetup.Apply wires the shared preset and pre-placed HUD burst without rebuilding weapon clips. Enhance applies the stronger result settings and per-gun pointer-hold/zone-shift defaults. RepairFeedbackChecks.Live verifies miss/spam, held pointer with continuing repair, zone relocation, success hold or natural completion, cancellation, audio start, weapon kick, pause and cleanup in Play mode and writes review captures under Temp/RepairFeedback.
