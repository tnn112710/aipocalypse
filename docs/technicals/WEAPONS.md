# Weapons

Guns (shooting, ammo, reloading, jams, recoil, aiming, bullets and their effects) and [melee](#melee) (swings, hits, knockback). Guns are ported from the old project's `WeaponSystem` and rebuilt on this project's rules (plain C# rules + one view, central ticks, pooling); melee is new, built the same way. Items, the hotbar and switching are in [EQUIPMENT.md](EQUIPMENT.md); script locations in [CODE_STRUCTURE.md](CODE_STRUCTURE.md).

Today: an **AK-47** (auto), a **pistol** (semi-auto), an **MP5** (auto, shares pistol ammo), a **double-barrel shotgun** (break action), a **pump shotgun** and a **fire axe** (melee), picked up from the table by the home in `Scenes/Prototypes/WorldMap.unity`, with a shooting range backstop wall further ahead. Guns take **parts** (optics, muzzles, grips, lights, magazines: [Gun parts and optics](#gun-parts-and-optics)); `Scenes/Prototypes/WeaponsTest.unity` has every gun and part on tables and a range with dummies at 10–100 m.

References for gun play and feel: **Far Cry** (gun position and view, hip and aimed framing, handling) and **Bodycam** (procedural hip wobble, bullets following the barrel).

## Controls

| Input | Action | Binding (`InputSystem_Actions`, Player map) |
| --- | --- | --- |
| `Attack` | Fire (hold for auto); swing a melee weapon (hold to keep swinging) | Left mouse, Enter, gamepad west |
| `Aim` | Aim down sights while held | Right mouse, gamepad left trigger |
| `Reload` | Reload | R (no gamepad binding yet) |
| `GunLight` | Switch the held gun's light (a flashlight part) on / off | F, gamepad d-pad up |

## Features

| Feature | How it works | Where to change it |
| --- | --- | --- |
| Fire modes | `Auto` (hold), `SemiAuto` (one per click), `AutoBurst` (bursts while held), `SingleBurst` (one burst per click) | Gun asset → Fire |
| Fire rate | Rounds per minute, frame-rate independent (a long frame can hold several shots). Clicks faster than the rate are buffered, never extra | Fire → `RoundsPerMinute` |
| Bursts | `BurstCount` shots, then `BurstCooldown` before the next burst | Fire |
| Shotgun pellets | `Pellets` bullets per shot (one round, one muzzle flash), each up to `SpreadAngle`° off the aim. A rifle is `Pellets 1, SpreadAngle 0` | Fire |
| Travelling bullets | Plain data moved each frame and ray-tested along their path: speed, range (then they vanish), optional gravity. No rigidbodies, no GameObject per bullet (only its pooled tracer). AK47 300 m/s, MP5 224, Pistol 178, PumpShotgun 222, DoubleBarrel 227 ([First-pass real-life stats](#first-pass-real-life-stats)) | Gun asset → Bullet |
| What bullets hit | `HitLayers`: bullets and the crosshair's aim ray only test these (all five guns: everything but Ignore Raycast, `Debris` and `HeldWeapon`). Dropped magazines are on `Debris`, so they never stop a shot or catch the aim ray; the player's capsule and the obstacle pose ignore them too | Bullet → `HitLayers`; damage type: Bullet → `DamageType` |
| Damage falloff | Every gun: full damage up to `Falloff.Start` metres travelled, easing (smoothstep) down to `MinShare` of it at `End` and beyond; knockback falls off the same way. AK47 40 → 120 m, 60 % · MP5 30 → 91 m, 50 % · Pistol 15 → 45 m, 50 % · PumpShotgun 13 → 38 m, 25 % · DoubleBarrel 14 → 41 m, 25 % (per pellet). Range parts scale both distances | Fire → `Falloff` |
| Knockback | Each bullet pushes along its flight by `Knockback` N·s (× the falloff): rigidbodies get the impulse at the hit point, damageables get it as `DamageInfo.Force` (`TargetDummy` tips; enemies slide back, [ARCHITECTURE.md → AI](ARCHITECTURE.md#ai)). AK47 1.5, MP5 0.85, Pistol 1, shotguns 0.36–0.38 per pellet (about 3.3 for a full blast) | Fire → `Knockback` |
| Damage | Each bullet hurts what it hits through `DamageService.DealHit` (the shot's `DamageType`, `Gunshot` by default; source = the shooter; a `Hitbox` on the collider hit scales it: enemy heads ×2 and critical, a red number); the service publishes `DamageDealt` with what landed and whether it killed, which drives the hit marker and damage numbers ([UI.md](UI.md#hit-feedback)) | Fire → `Damage` |
| Never hits the shooter | Anything under the shooter's root is skipped (body, held gun). Triggers (pickups) are ignored | `IGunUser.Root` |
| Aim origin switch | **Muzzle**: bullets leave the muzzle along the barrel (follows pose and gun kick, the old behaviour). **Camera**: they leave the screen centre; the tracer still starts at the muzzle and slides onto the real path. **MuzzleToCrosshair**: a ray from the screen centre finds what the crosshair is on (or the point at full range), and bullets leave the muzzle towards it (`GunAim.MuzzleToPoint`); closer than 1 m or behind the muzzle, they use the camera direction. The aim kick (the view) moves the shots in every mode; the gun's small extra kick only moves them in Muzzle mode. Camera and MuzzleToCrosshair aim along the [aim sway](#aim-sway) (the crosshair's direction), not the exact screen centre. The rules live in `GunAim.Shot`, reusable by anything that aims through an eye and leaves a muzzle (a bow, a turret, an enemy rifle) | `PlayerHeldItemTuning` → Guns → `AimFrom` (live in Play mode). Set to MuzzleToCrosshair |
| Magazine | Rounds live on the gun (`GunInstance`), so they survive drop, pick up and swap. A new gun spawns full | Magazine → `Size` |
| Ammo is an item | Spare rounds are `AmmoType` stacks in the backpack (pistol and rifle stack to 120, shells to 40; picked up from ammo boxes, looted, dropped, sorted like any item: [EQUIPMENT.md](EQUIPMENT.md#inventory)). The player's `AmmoReserve` is a view over its inventory: reloading takes from the stacks of the gun's type, last stack first. Guns of the same type share the stacks | `Data/Weapons/AmmoConfig.asset` (starting rounds, put in the backpack; `Infinite`), each type's asset in `Content/Weapons/_Shared/Ammo/` (stack, weight, icon, box prefab) |
| Ammo crate (test aid) | `AmmoCrate` (`Content/Props/AmmoCrate/`, one in the WorldMap yard): "[E] Take ammo" while carrying a gun tops each ammo type the hotbar guns use (`HotbarAmmo.TypesInUse`, also used by `cheat.ammo`) up to its `AmmoConfig` starting amount, never above (as backpack stacks, as far as they fit); unlimited uses, `AmmoCrate_Take` sound | `Data/Weapons/AmmoConfig.asset` (starting amounts); the prefab's `AmmoCrate` |
| Magazine reload | One timed reload; takes only what the reserve has | Magazine → `Reload = Magazine`, `ReloadTime` |
| Per-round reload | One round per `ReloadTime` until full or the reserve is empty (tube shotgun style). Pulling the trigger stops it once the round going in is loaded, then that click fires (no instant shot from the reload pose) | Magazine → `Reload = PerRound` |
| Pump / bolt action | After each shot that leaves a round, the gun works its action for `CycleTime` (`GunAction.Cycling`) before it can fire again. Aiming stays up. A click during the pump is kept and fires when it's done. After the last round there's nothing to chamber, so it goes straight to the reload. Switching away mid-pump finishes the pump when the gun is raised again. 0 = self-loading | Fire → `CycleTime` |
| Auto reload | Starts by itself when the magazine runs dry and there's ammo | Magazine → `AutoReload` |
| Jams | Each shot has `Chance` to jam instead of firing (no round used). A jammed gun can't fire: it stays stuck for `JamPause` (0.6 s) while trigger pulls only dry-click, then all five guns automatically clear over 4-5 seconds, with an optional timing QTE; guns without a profile retain automatic clearing over `ClearTime` | Gun asset → Jam |
| Recoil | Two parts per shot, along a random spray pattern that restarts after a `SprayResetDelay` (0.24 s) pause; its climb, drift, jitter and roll are per-gun `Spray*` settings. **Aim kick** turns the view (up, sideways, roll, shake), so the crosshair and the shots climb together. **Gun kick** is a small, fast extra tilt + push-back of the model only, on top of the view it's attached to, so the barrel stays near the crosshair. `KeptShare` of the aim kick (up / sideways) turns the look for good, rising at the snappiness rates, so the player pulls down against a spray; the rest springs back (AK47 and MP5 0.25, Pistol and shotguns 0.3; 0 = all springs back). Hip-fire and aiming have separate values that blend with the aim pose (`RecoilValues.Lerp`), so letting go of the aim mid-spray eases between them | Gun asset → Recoil → Aim kick / Aim response / Gun kick, `KeptShare` |
| Recoil modifiers | Hip kick ×0.75 of the tuned values on every gun (`Recoil → Hip → Multiplier`, since the hip wobble carries the inaccuracy). Extra kick while moving (×1.15) or airborne (×1.6), less while crouching (`CrouchMultiplier` ×0.8, stacks with moving); the first `AccurateShots` of a spray kick `AccurateShotsMultiplier` as much (0 = off) | Recoil |
| Stamina and fatigue | Each shot spends `StaminaPerShot` (pellets count as one) and pauses regen like any other spend (stamina regenerates while standing, and at half the rate while walking): a full-auto spray drains steadily, a shotgun blast takes a chunk, pistol shots barely register. Low stamina never blocks firing: it multiplies recoil (aim kick, gun kick and shake) by the player's fatigue tier, eased in. See [Fatigue](#fatigue) | Fire → `StaminaPerShot` (each gun's asset) |
| Hip spread (wobble envelope) | No hidden random cone: from the hip the **gun itself wobbles** and bullets go exactly where the barrel points (Bodycam-style). `GunAccuracy` computes the envelope, how far the aim may wander (degrees): `(cone × movement + airborne + (bloom + turning) × (1 − aim)) × fatigue × steadiness`. **Cone**: `HipSpread`, shrinking to `AimSpread` (0) as the sights come up (AK47 3°, Pistol 2.5°, MP5 and both shotguns 2°; the laser −25 %, the 4× scope +15 %, `GunStat.HipSpread`). **Movement**: walking ×1.5, crouching ×0.7 (crouch-walking 1.05), sprinting ×2.5 (`HeldItemInput.MoveSpreadScale`). **Turning**: +0.5° per 100°/s of look speed, at most +2°, eased over `TurnSmoothing` (0.1 s) so per-frame mouse jitter doesn't shake it (`HeldItemInput.TurnSpread`), less as the sights come up, so a flick shakes the aim and it settles back in about half a second. **Bloom**: each hip shot adds `BloomPerShot`° (AK-family rifles 0.5, SMGs 0.4, pistols 0.8, shotguns and the hunting rifle 1.5), up to `MaxBloom` 3°, falling at `BloomRecovery` 4°/s once no shot has come for `BloomRecoveryDelay` (0.2 s, so full auto builds to the cap within a second and spam-tapping builds too), and jolts the gun by the same amount. **Fatigue**: the fatigue tier's recoil scale. **Steadiness**: the player's `StatId.HeldItemSwayScale` (skills; a sanity effect can plug in here). It rises at once and settles at `SettleSpeed`. The wander that fills it is in [Aim sway](#aim-sway); the crosshair's cross shows it | Gun asset → Accuracy (`HipSpread`, `AimSpread`, `Bloom*` incl. `BloomRecoveryDelay`, `SettleSpeed`); movement scales: `PlayerHeldItemTuning` → Guns |
| Airborne inaccuracy | In the air the envelope grows by `AirborneSpread`° (2), on top of the visible air drift; aiming down sights keeps `AimMultiplier` of it. It settles at `SettleSpeed` after landing. The player's `StatId.AirSpreadScale` (base 1) scales it, for skills | Gun asset → Accuracy (all five guns: 2° / 0.4 / 6) |
| Aim sway | The crosshair, the shots and the gun move together off the screen centre: breathing, walking bob, move / turn lag, jump, air drift, landing, heavier when out of breath. See [Aim sway](#aim-sway) | `PlayerHeldItemTuning` → Aim sway |
| Model sway and bob | Extra motion of the held model only, which never changes where it points: position drift (breathing, bob, look / move lag, jump sink, landing dip) and roll around the barrel. Aiming calms each part. `StatId.HeldItemSwayScale` (base 1, for skills) and the item's `HoldFeel.SwayScale` (a heavier gun sways more) scale it and the aim sway | `PlayerHeldItemTuning` → Sway & bob (model only); per gun: Hold feel → `SwayScale` |
| Poses | Hip, Aim and Sprint pose markers in the gun prefab; the gun blends between them. The Hip pose is held Far Cry-style (see [Gun prefab](#gun-prefab)) and turned so the barrel points at the screen centre 20 m ahead (bullets from the muzzle don't cross the crosshair at an angle). Sprint carries the gun across the lower screen with the muzzle down and to the left: rifles, SMGs and shotguns pitched 10°, yawed -40°, rolled -30°, the right grip 22 cm right, 20 cm down (M4, SCAR, MP5: 23) and 32 cm out from the eye; pistols tipped 35° down in both hands, the grip 12 cm right, 24 cm down, 45 cm out. The pivot sits on the right grip, so each gun's pose is that rotation plus the position that puts its own grip there (`AimReview.SetSprint` works it out). Pistols aim with the pivot at z -0.22 (at -0.42 both hands filled the screen); the MP5's hip is at z -0.36, close enough for the left arm to bend | Prefab → `Poses/*`; blend speeds: Gun asset → Handling |
| Aiming rules | Holding aim or the trigger ends a sprint (it resumes when you let go, if Sprint is still held). After a sprint the gun can't fire for `SprintRecoveryTime` while it comes up from the sprint pose (pistols 0.1 s, MP5 0.18 s, rifles and shotguns 0.2–0.25 s, RPD 0.4 s); a click meanwhile is kept and fires then, and aiming starts at once. Reloading and clearing a jam drop the aim (sprinting is allowed while reloading). Switching away cancels a reload or jam clear (it restarts next time) | `Gun.Tick`, `HeldGun.StopsSprint`; Handling → `SprintRecoveryTime` (AK47 0.25, MP5 0.18, Pistol 0.1, PumpShotgun 0.25, DoubleBarrel 0.2 s) |
| Handling per gun | Aim speed / sprint pose speed / aim zoom: AK47 8 / 7 / 0.85 (slow, heavy), MP5 11 / 9 / 0.87, Pistol 14 / 12 / 0.9 (quick), PumpShotgun 8 / 7 / 0.88, DoubleBarrel 9 / 8 / 0.9. The Pistol (.45) kicks about as hard as the AK: 3.4° up / 1.3° sideways at the hip (2.3 / 0.85 aiming), snappy return, a bigger muzzle flip on the model, no accurate-shots discount | Gun asset → Handling, Recoil |
| Empty click | Pulling the trigger on an empty magazine with nothing being reloaded (no spare ammo, or `AutoReload` off) clicks once per pull: the `Empty` sound, or `DryFire` when unset. Holding the trigger past the last round doesn't click | `Gun.EmptyFired`; Sounds → `Empty` |
| Slower while held and aiming | Walk and sprint speed × the item's `HoldFeel.Mobility` (0.5–1, empty hands 1; a gun's parts' move-speed stat multiplies it, kept within 0.5–1), × `AimMoveSpeedMultiplier` (0.5, eased in with the gun's rise to the sights) while aiming. Sprinting with it costs stamina ÷ Mobility, and ground acceleration and braking × Mobility (a heavy gun gets going and stops slower). Starting values: pistols 0.95, MP5 and melee / tools 0.9, rifles and shotguns 0.8, RPD 0.65, consumables 1 | per gun: Hold feel → `Mobility`; `PlayerHeldItemTuning` → Guns |
| Aim zoom | Aiming down sights narrows the world camera to `AimZoom` × its field of view (0.87 = 13%), easing with the aim pose. The held gun's lens (`FirstPersonView`) zooms with it (COD-style: the sights grow on screen by the same factor), but never with the shot punch or sprint; `HeldAimZoomShare` 0 would keep the gun its hip size. Look speed scales with the zoom so aim feel stays the same | Gun asset → Handling → `AimZoom`; `Data/Player/PlayerCameraTuning.asset` → Aiming → `HeldAimZoomShare` (1) |
| Hands | Floating hands follow each gun's two hand targets; the clips move the gun's own parts (mag, bolt, slide, pump, hinge, shells) and switch the left hand's contact between them | Gun prefab → `ItemHands`, clips; [HANDS.md](HANDS.md) |
| Reload / jam / pump motion | Every gun plays editable clips with hand contact constraints (`GunAnimation`). A gun without one still works as a block-out: the model only poses and kicks. One clip per `GunAction`, sampled at `Gun.ActionProgress`, so it always fits `ReloadTime` / the repair time / `CycleTime` | Gun prefab → AnimationRoot / clips |
| Pose preview | Hip / Aim / Sprint buttons on `HeldGun` snap the model to a pose in the prefab | `HeldGun` inspector |
| Effects | Muzzle flash (`MuzzleFlash_Hovl3`: Hovl Studio's stylized star burst, plus a 0.07 s warm point light from its `FlashLight` particle; `Flash` and `MainFlash` simulate in local space so they stay on the muzzle while moving that lights hands, ground and enemies at night), tracer (a stylized glowing streak, Escape from Duckov-like, in two layers: a thin white-hot `Core` trail (Hovl's soft-edged `Line3`) over a wide, dim amber `Halo` trail whose glow fades smoothly to nothing at its edges (`Tracer_Glow.png`, a generated profile: URP particle shaders ignore texture tiling, so a round glow can't be used; kept below white so it doesn't show a reddish rim), its `Head` glow (Hovl's `Glow1`, a bright point at the front) is switched off in the prefab: with bloom it read as an unnatural glowing orb flying ahead of the streak; trails of about 0.03 s, short so the tail leaves the muzzle before strafing swings it, HDR so bloom picks it up; it stops where the bullet hits or runs out of range and lingers `TracerLinger` so the line always reaches the target, even point-blank, while its head goes out there. Its `TracerStreak` varies each shot's length, width and core brightness a little, brightens the halo up to 1.8× in the dark (the scene's `EnvironmentDirector` daylight, read by `HeldGun` when there is one), and widens it beyond 15 m from the camera so far shots keep their size on screen (up to 6×; measured against a 70° view, so a zoomed scope, which already magnifies, widens it less). Every round shows a tracer (`BulletEffects` → Tracer Every = (1, 1); e.g. (2, 3) shows one every 2nd–3rd round, re-picked at random after each tracer round, first round after equipping always traced; a shotgun round's pellets all show theirs or none), except a magazine's last 4 rounds, which all do so you see it running dry (Tracer Last Rounds); the others fly and hit the same, unseen), impact (removed after 3 s), bullet hole decal (stays until the 64th newer hole recycles it: an empty URP decal chunk is dropped and regrown at a 2.5–5 ms cost, so holes don't time out). Holes are parented to the collider they hit so they move with it; the decal is scale-invariant (size set on its `DecalProjector`, 10 cm deep) so a scaled parent can't stretch it. All pooled; no holes on things that take damage | `Data/Weapons/BulletEffects.asset`; lifetime / cap on each effect prefab's `PooledEffect` |
| Sounds | Equip, fire, reload start / insert / end, jam (trigger falls on the jammed round), dry fire (each pull while stuck), jam clear start / end, repair slap / punch, cycle start / end (pump back / home) as BroAudio `SoundID`s, played 2D through `AudioService` (pooled by BroAudio, so a sound isn't cut when the gun is holstered). Entities live in `Data/Audio/Guns/` (one per purpose and clip, e.g. `Fire_AssaultRifle1Fire`); each entity holds its own volume, random clips (`RepairSlap`, `RepairPunch`) and pitch variation. `Sounds.Volume` scales a whole gun. Clips are placeholders from the old project's samples (`Assets/_Placeholder/Audio/Weapons`) and Malbers / Highlands store audio; the shotguns borrow sniper and pistol clips | Gun asset → Sounds; entities in `Data/Audio/` (BroAudio Library Manager) |
| Crosshair | A reticle (default: a 6 px dot and a large cross of four 2×12 px lines) that moves with the barrel, wobble and all; bullets go to its centre. The cross opens to the wobble envelope plus pellet spread (`HeldItem.Spread`), so its size tells you how far the barrel may wander: fast to open, slower to close, never tighter than 10 px. It fades while aiming down sights (the sights take over) and while sprinting. Reticle designs are swappable prefabs. See [UI.md → Crosshair](UI.md#crosshair) | `HUD` → `SafeFrame/Center/Crosshair` |
| Ammo on the HUD | Magazine / reserve in the corner of each gun's hotbar slot (`30/90`, the reserve = all carried rounds of its type; magazine only when the reserve is infinite), red when empty or jammed | [UI.md](UI.md#hotbar) |

Cut from the old system: built-in fallback recoil assets, legacy Animator triggers, the laser sight, the `Item` / `Weapon` / `IEquipable` bases (replaced by the item definitions and `EquipmentSwitcher`), and the hidden "legacy" stat copies. Each gun has its own clip/constraint presentation, described below.

## Layers

```text
Data (per weapon: Content/Weapons/<Name>/; ammo types: Content/Weapons/_Shared/Ammo/; shared settings: Data/Weapons/)
  GunDefinition                 Fire, Bullet, Magazine, Jam, Recoil (Hip / Aim), Handling, Sounds (BroAudio SoundIDs), Effects, Hold feel
  AmmoType                      a kind of round: an ItemDefinition (Category Ammo); its Prefab is the ammo box pickup
  AmmoConfig                    starting rounds + Infinite (on the Player prefab) → CreateReserve(inventory)
  BulletEffects                 effect prefabs + bullet hole settings, shared by guns
  MeleeDefinition               Swing (+ swing motion), Sounds, ImpactEffect, Hold feel
  GunRepairProfile              per gun: jam pause, repair timing, QTE onset, miss penalty, challenge, RepairFeedbackSettings

Rules (plain C#, EditMode-tested in GunTests)
  GunInstance                   Rounds, IsJammed (per copy)
  AmmoReserve                   a view over the holder's Inventory: Count / Take / Add (as stacks), Infinite, Changed
  GunTrigger                    when shots happen (mode, rate, bursts)
  Gun                           one held gun: trigger + magazine + reload / jam clear + aim; Fired, Jammed, Action events
  GunAim                        shot origin / direction from the holder's eye and the muzzle (AimOrigin, cover check)
  GunRepairFlow                 the jam repair's QTE rules
  GunRecoil                     aim kick → ViewEuler, ViewShake (springs back) + LookTurn (kept share); gun kick → GunEuler, GunKickBack; hip / aim blended by aim weight
  DamageFalloff                 damage share by distance travelled
  BulletSpread                  pellet direction inside a cone (gameplay rolls: the RandomService "guns" stream, saved and seeded)
  MeleeAttack                   one held melee weapon: swing timing, hold to repeat, buffered click; SwingStarted, Struck

Views and services
  HeldGun : HeldItem            on the gun prefab: feeds Gun from HeldItemInput, spawns bullets, applies poses and recoil, sounds; its GunJam part
  GunJam                        one gun's jam: GunRepairFlow + GunRepairFeedback (built by the root GunJamFactory)
  HeldMelee : HeldItem          on the melee prefab: feeds MeleeAttack, swing motion, hit check → damage, knockback, impact, sounds
  BulletSystem  (root, tick)    bullets in flight → hits → damage, impact, hole
  EffectPool    (root, tick)    one pool per effect prefab, timed returns, MaxActive (_Common/Effects: enemies' blood uses it too)
```

Who calls what each frame: [CODE_STRUCTURE.md → Frame order](CODE_STRUCTURE.md#frame-order). In short, the player's `HeldItemDriver` builds a `HeldItemInput` from its intent and motor state, `EquipmentRig.TickHeld` passes it to the held item (with `IsReady` from the switcher), and the driver then applies the item's `LookTurn` and `ViewKick` to the look, its `Zoom` to the lens and its `IsAiming` to the motor speed.

## Gun prefab

```text
AK47 (variant of Gun_Base)    HeldGun
├─ Poses
│  ├─ Hip                     where the gun rests (relative to the rig's Hand), barrel on the screen centre 20 m out
│  ├─ Aim                     sights on the screen centre
│  ├─ Sprint                  lowered and turned
│  └─ Work                    optional: held here while reloading or clearing a jam at the hip
└─ PosePivot                  moved to the active pose
   └─ RecoilPivot             gun kick
      └─ AnimationRoot        Animator, RigBuilder, GunAnimation (see Gun clips and constraints)
         └─ Motion/Weapon     the model's parts
            └─ Muzzle         bullets and the flash leave here, along its blue (Z) axis
```

`Gun_Base` itself is `PosePivot > RecoilPivot > Muzzle` with no `GunAnimation`: a working block-out (put a model under `RecoilPivot` and it poses, kicks and shoots). The variants remove that base `Muzzle` and point `HeldGun` at their animated one.

Shotgun parts move only in their clips: `PumpShotgun`'s Cycling clip slides the pump; `DoubleBarrel`'s barrels, rib and forend sit under a `Hinge` at the front of the receiver that its reload clip opens.

**Work pose (2026-11-16):** the hip framing keeps the gun low, so a rifle's magazine well and the hand working it sat below the screen edge. A gun with a `Poses/Work` marker (`HeldGun._workPose`) eases to it while it is Reloading or ClearingJam and not aiming or sprinting, then back to Hip; the arms come along (`ItemHands.Carry`). It is the Hip pose plus an offset, set per gun with `AimReview.SetWork(gun, x, y, z, pitch, roll)` in edit mode: AK47 (and the AK-74M) (−0.03, 0.035, 0), M4 and SCAR (−0.05, 0.09, −0.02). The MP5, the pistols and the shotguns have none: their hands are in view already. The **Work** preview button shows it.

Poses came from the old project (gun root at (0, −0.15, 0.3) under the camera) and are stored relative to `Hand`.

**Hip framing (Far Cry, 2026-11-15):** the gun sits in the lower right, nearly upright (2° roll), its barrel converging on the screen centre 20 m out; the front sight lands at about 56–59 % across and 57–59 % down at 16:9, the stock and receiver leave the frame at the bottom right, and the trigger hand shows on the grip. Hip positions: AK47 (−0.05, −0.01, −0.50) since 2026-11-16 (user: lower, closer and nearer the centre so only the support hand and a short forearm show; its front sight lands at 55 % across, 63 % down; M4 and RPD moved by the same step), MP5 (−0.01, 0.03, −0.24), PumpShotgun and DoubleBarrel (0, 0.045, −0.33), Pistol (−0.01, 0.075, −0.04). The block-outs take their reference gun's hip lowered by their sight height (`GunBlockoutSetup` copies it; AK-74M inherits the AK47's as a variant). To retune, in Play mode in WeaponsTest (`AgentScripts/Weapons/AimReview.cs`): `Hold` the gun, `Hip(x, y, z, roll)` moves the pose live and turns it so the barrel meets the screen centre 20 m out (it prints where the muzzle lands on screen; `HipInfo` reads it back), `Capture` a screenshot, and `SaveHip` writes it into the prefab and shifts the block-outs that copy it. Aim Z (2026-11-15, user: sights closer to the eye) puts each gun's rear sight close enough to read: AK47 / AK-74M −0.68, RPD −0.66, Uzi −0.65, hunting rifle −0.50 (its scope's eyepiece stays 5.5 cm out, see Sight windows), M1 Carbine −0.62, PumpShotgun −0.56, DoubleBarrel −0.54, AA12 −0.52, M4 −0.50, FAL −0.46, MP5 −0.40 (2026-11-21: at −0.49 its rear sight drum filled the view), Pistol / Glock / Desert Eagle −0.42 (guns whose rear sight sits far forward, like the AK, come closest; too close and the rear sight passes behind the camera); Sprint Z = −0.18 m for MP5 and −0.10 m for the others. Tune eye distance with each gun's `Poses/*` local Z (more negative brings it closer), then use the Hip preview before saving. The shared Hand anchor remains at (0.17, −0.21, 0.62), and hand/weapon scale is separate from this framing.

MP5's `Sight` uses our `Content/Weapons/MP5/MP5_Sight.asset` copy. Only the disconnected aperture drum is leveled by 25° and aligned to the front post at model Y = 0.15924 m; its mounting base stays in place. The source FBX is unchanged. `Poses/Aim` uses position (−0.17, 0.05076, −0.40) and zero rotation so the aperture axis and front-post tip line up with the camera. Moving the gun alone cannot correct the source mesh's tilted bore. Its hip pose is in the Hip framing paragraph below, independent of ADS.

### Gun clips and constraints

AK47, MP5, Pistol, PumpShotgun and DoubleBarrel keep their `Gun_Base` variants and pose/recoil pivots. Under `RecoilPivot`, `AnimationRoot` adds an Animator using `Content/Weapons/<Gun>/Animations/<Gun>.overrideController`, a RigBuilder and `GunAnimation`. `AnimationRoot/Motion/Weapon` holds our unpacked copy of the store model with generic part names. Shared mesh/material assets are still referenced; original store prefabs are untouched. The variants remove the inherited base `Muzzle`; `HeldGun` references the animated `Weapon/Muzzle` (under `Weapon/Hinge` on DoubleBarrel).

The shared `Content/Weapons/_Shared/Animations/Gun.controller` has one state per `GunClip` (Equipped, Idle, Firing, Reloading, ReloadingEmpty, Repairing, RepairingStubborn, RepairEnd, Cycling); each name is also the shared clip an override replaces, so add a state, a shared clip and an enum value together. Each weapon Override Controller supplies clips for its actions; AK47 does not use Cycling. Gameplay chooses states explicitly; the controller has no automatic transitions. `GunAnimation` uses a manual AnimatorControllerPlayable graph with Animation Rigging outputs, sampled once from `HeldGun.Tick` before final muzzle/hand use. Reloads, repairs and cycles sample `Gun.ActionProgress`; ammo still transfers only when `Gun` completes a reload or shell insertion. A weapon switch cancels the action and the next show starts cleanly with Equipped. Reload/cycle timing, ammo and recoil remain owned by gameplay; repair initiation and QTE behavior are described below. State hashes and clip lengths are cached when the graph is built, so sampling allocates nothing per frame.

AK47, MP5 and pistol reloads separate the old magazine, an animated replacement, and a pooled world magazine (`Content/Weapons/<Gun>/<Gun>_Magazine.prefab`, Rigidbody + collider + PooledEffect). The existing EffectPool prewarms, caps and expires these cosmetics. The drop does not consume inventory ammo or create pickups. Empty reloads add the bolt/slide rack. PumpShotgun's reload clip repeats once per shell; its Cycling clip drives the pump and bolt with the hand attached. DoubleBarrel opens its hinge, loads a pair of shells, and closes. Shotguns have no detached magazine effect. All five guns use the multi-slap repair sequence described below. Right-hand contact remains on the grip; left-hand contacts, free paths and fingers are clip-authored. All five use hand scale 1.12. See [HANDS.md](HANDS.md#weapon-animation-authoring) for editing steps and contact weights. Melee remains procedural.

## Adding a gun

1. **Folder and prefab:** make `Content/Weapons/<Gun>/` (everything for this gun goes there). Right-click `_Core/WeaponSystem/Prefabs/Gun_Base.prefab` → Create → Prefab Variant and move it into the folder as `<Gun>.prefab`. Under `PosePivot/RecoilPivot`, add `AnimationRoot/Motion/Weapon` and put an unpacked copy of the store model in `Weapon` with generic part names (shadows off, colliders removed; the store prefab stays untouched). Add a `Muzzle` under the animated weapon at the barrel tip (blue axis out of the barrel) and remove the base `Muzzle`.
2. **Poses:** select the gun root, use the **Aim** preview button, and move `Poses/Aim` until the sights sit on the screen centre in the Game view (enter Play mode to check; edits to the prefab in Play mode are lost). Do the same for Hip and Sprint, then press **Hip** before saving.
3. **Item:** duplicate `Content/Weapons/AK47/AK47.asset` into the folder as `<Gun>.asset` (or Create → Aipocalypse → Items → Gun). Set `DisplayName`, `Icon`, `Prefab` (the variant), Fire / Bullet / Magazine (pick or create an `AmmoType` in `Content/Weapons/_Shared/Ammo/`) / Jam / Recoil, and `Effects` (`Data/Weapons/BulletEffects.asset`).
4. **Clips and hands:** give `AnimationRoot` an Animator with a new Override Controller of `Gun.controller`, a RigBuilder and `GunAnimation`, and assign it to `HeldGun` → Animation; point `HeldGun` at the animated `Weapon/Muzzle`. Until then the gun works as a block-out that only poses and kicks. Point `ItemHands` at the two `Motion/HandTargets` grips ([HANDS.md → Weapon animation authoring](HANDS.md#weapon-animation-authoring)).
5. **World side** (same prefab, see [EQUIPMENT.md → One prefab per item](EQUIPMENT.md#one-prefab-per-item)): add `WorldItem` (`_definition` = the asset) and a trigger `BoxCollider` to the root, and a `World` child with the store model lying flat (shadows on); set `WorldItem._worldModel` to it and turn `World` off. Drop copies of the prefab into a scene to place pickups.
6. **New ammo type?** An `AmmoType` asset in `Content/Weapons/_Shared/Ammo/` with a display name, icon, `MaxStack`, `Weight` per round and an ammo box pickup prefab (root `WorldItem` + trigger box + `World/Model`, as `AgentScripts/Inventory/InventorySetup.Ammo` builds them); its starting amount in `Data/Weapons/AmmoConfig.asset`; add it to loot tables where it should drop.

### Shotguns

Same steps with `Pellets` > 1 and `SpreadAngle` > 0 (each pellet does `Damage`); they share `Content/Weapons/_Shared/Ammo/ShotgunShells.asset` (12 gauge). The kind of shotgun is just settings:

| Kind | Example | Fire | Magazine | Held prefab |
| --- | --- | --- | --- | --- |
| Break action | `DoubleBarrel` (9 pellets of 10, 6°) | `SemiAuto`, `CycleTime 0` (one barrel per click) | `Size 2`, `Reload = Magazine` (both shells at once) | break part = a hinge over the barrels |
| Pump, tube-fed | `PumpShotgun` (9 pellets of 10, 5°) | `SemiAuto`, `CycleTime` 0.55 s | `Size 5`, `Reload = PerRound` (firing stops it after the current shell) | pump part = the forend |
| Semi-auto, tube-fed (XM1014) | — | `SemiAuto`, `CycleTime 0` | `PerRound` | none |
| Auto, box mag (Saiga-12) | — | `Auto`, `CycleTime 0` | `Reload = Magazine` | none |

## Gun parts and optics

Built 2026-11-14 ([design](../superpowers/specs/2026-11-14-gun-system-design.md)). Parts are items: picked up, carried in the backpack and fitted to a gun in the inventory screen's centre inspector ([UI.md → Inventory screen](UI.md#inventory-screen)). Try them in `Scenes/Prototypes/WeaponsTest.unity` (built by `AgentScripts/Weapons/WeaponsTestSetup.cs`; play it with the scene open, or set it as the play-mode start scene). Its HUD carries a dev readout top left (`GunReadout`, not localized, like the console): the held gun's stat sheet with each part's share, damage per second firing and with reloads, shots and time to kill a 100 HP target (body / head) at 10, 25, 50 and 100 m with damage falloff, and the current hip wobble. On the right edge an aim trace (`AimTrace`) draws the crosshair's last 2 s ×3, dotted 30 times a second, so uneven speed shows as uneven spacing, with a ring at the wobble envelope; it freezes in pause (`WeaponsTestSetup.AddTrace` rebuilds it alone). For scripted checks, `AgentScripts/Weapons/AimReview.cs` drives it in Play mode: hold any gun, fit parts, a virtual gamepad (move, aim, fire, crouch), a readout of the aim state (wobble, dot, cross), game view captures, and hip pose tuning (`Hip`, `HipInfo`, `SaveHip`). In play, parts come from goblins and crates ([Loot](#loot)).

| Feature | How it works | Where to change it |
| --- | --- | --- |
| Slots | Duckov's six: Optic, Muzzle, Grip, Stock, Tactical, Magazine. Each gun lists the ones it has: AK47 and MP5 Optic, Muzzle, Grip, Tactical, Magazine; Pistol Optic, Muzzle, Tactical, Magazine; PumpShotgun Optic, Muzzle, Grip, Tactical; DoubleBarrel Tactical. No stock parts yet | Gun asset → Parts → `Slots`, `Family` |
| What fits | A part fits a gun that has its slot and is either in its exact-gun list or of one of its families (Pistol, Smg, Rifle, Marksman, Sniper, MachineGun, Shotgun). Magazines also need the gun's ammo type | Part asset → `Families`, `Guns`, `Ammo` |
| Stats | Each part adds percentages to the copy's stat sheet (`GunStats`): damage, fire rate, magazine, reload time, range (bullet range and both falloff distances), bullet speed, recoil up and sideways, aim time, spread (pellets and airborne), move speed while held, hip spread. They add up, then apply: base × (1 + Σ % / 100), each sum clamped to −90 … +300 %. The gun's weight and value add its parts'. `Gun`, `GunRecoil` and `HeldGun` read the sheet, not the asset; fire-rate parts don't reach the trigger yet | Part asset → `Modifiers` |
| Fitting | With a gun inspected: drag a part onto its slot or onto the 3D view, or right-click it; the part it replaces takes its place. Drag a fitted part to a backpack slot, or right-click it, to take it off. A smaller magazine's extra rounds go back to the backpack as ammo. With no room for the part or the rounds nothing changes ("Backpack full"). A part going on or off plays its own sound (`Part_Attach` / `Part_Detach`, `InventoryStyle`) and nudges the 3D view (`ItemPreviewStage.Nudge`). Rules in `GunModding` | — |
| On the model | `GunMounts` (on the first-person `Weapon` and on `World/Model`) spawns each part's model on its slot's mount; rails and adapters show and iron sights hide while a part is fitted (the MP5's rear drum, the pump's rear sight). Mounts sit under the gun part they move with (the pistol optic on the slide, the pump's grip on the pump), so the clips carry them. Magazine parts scale the gun's own magazine (no models of their own yet). The model in hand changes at once when a part is fitted | gun prefab → `GunMounts` (placed from the Castle Bravo presets by `AttachmentSetup.Mounts`) |
| Muzzle parts | Shots and the flash leave the part's tip. Suppressors hide the flash (`HidesFlash`), replace the shot sound (`FireSound`: `Fire_SuppressedRifle`, `Fire_SuppressedPistol`; no tail unless `FireTailSound` is set) and carry less far: `Loudness` 0.3 scales `ShotFired.Loudness`, so animals scatter from a third of the distance (`AnimalSettings.ShotRadius` × it) | Part asset → Muzzle |
| Optics | Aiming moves the aim pose so the optic's sight point sits on the iron sights' line (the gun's `IronSight` marker, worked out from its Aim pose and the player's hand anchor), at the optic's `EyeDistance` from the eye, the same on every gun (red dot and holo 22 cm, 2× 20 cm, 4× 28 cm, pistol red dot 18 cm), whatever the gun's own Aim pose. Closer gives a bigger window; tube optics keep their sight window beyond 5 cm (below). Aim zoom is the gun's own `AimZoom` or 1 / `Magnification`, whichever zooms more (red dot and holo 1.5× = 0.67, 2× scope 0.5, 4× 0.25). Magnification shows the aim sway larger, so a sight's `SwayScale` calms the sway as the sights come up (red dot and holo 0.85, 2× 0.7, 4× 0.35; `HeldItem.SwayScale`). Lenses draw their reticle at infinity along the barrel (`Optics/OpticReticle` shader), so the dot stays on the aim point; the glass itself is clear (`_LensTint` alpha 0, so only the reticle draws). Reticles (`AttachmentSetup.Optics`, white shapes the lens tints): a red dot with a soft glow, a thick holo ring and dot, a bold chevron (2×), and for the 4× thick posts, a fine cross with range ticks and a centre dot | Part asset → Optic; lens look `Optics/Lens_*.mat` |
| Sight windows | Tube optics (red dot, 2×, 4×) and the hunting rifle's built-in scope have a `SightWindow`: a disc just inside the tube's rear opening (`Optics/SightWindowDisc.asset`, `SightWindow.mat`: transparent, alpha 0, depth write on, queue 1990, so it draws before the gun and writes only depth). While it shows, nothing of the held layer behind it draws: the tube's inside walls and the gun's front sight vanish and the target fills the whole rear opening. It carries the optic's lens material as a second material, so the reticle draws on it. `HeldGun` turns it on past 60 % aim (off in the prefabs, so pickups and the inventory view are untouched). It must sit beyond the world camera's near plane (5 cm), which culls the held layer too: keep `EyeDistance` (or a built-in scope's Aim pose) so the window lands past about 6 cm | Part spec → `WindowRadius` (`AttachmentSetup.Windows`); built-in: `GunBlockoutSetup` → `ScopeWindow` (`ScopeWindows`), wired into the held `GunMounts` by `AttachmentSetup.Mounts` |
| Full scopes | `FullScope` optics (the 4×): over the last 20 % of raising the sights the scope view fades in (HUD `ScopeView`: black around a circle with the scope's reticle, following the aim sway) and the held layer (gun, hands, flash) stops drawing. The circle opens from dark as the eye finds the scope, and slides with the scope off the eye's line (sway, kick) while a shadow darkens the edge it leaves: `HeldGun` measures the sight point's offset and tilt from the eye's aim line in shares of the optic's `EyeBox` (4×: 6 mm) as `ScopeView.EyeOffset` ([UI.md → Scope view](UI.md#scope-view)) | Part asset → Optic → `FullScope`, `ScopeReticle`, `EyeBox` |
| Lights and laser | Gun lights carry a spot light, and the laser puts a red dot where the next shot goes (it aims along the holder's shot line from the laser, like the bullets, so it agrees with the crosshair whatever way the hip pose holds the barrel; a camera-facing disc on the world layer, sized by distance). Both work only while the gun is in hand and switched on: `GunLight` (F, d-pad up) switches them, per gun copy, on by default, with the part's `SwitchSound` (`GunLight_Switch`) | the part's `_Model` prefab → Light, `AttachmentModel` (Laser, LaserDot, range, dot size); dot look `Optics/LaserDot.mat` |

| Part | Slot | Fits | Changes (first guesses) | Castle Bravo source |
| --- | --- | --- | --- | --- |
| Red Dot Sight | Optic | SMG, rifle, marksman, MG, shotgun | 1.5×, sway ×0.85 aimed, aim time −5 % | `Optic_H` |
| Holographic Sight | Optic | same | 1.5×, sway ×0.85 aimed, spread −15 %, aim time +5 % | `Optic_I` |
| 2x Scope | Optic | rifle, marksman, MG | 2×, sway ×0.7 aimed, range +15 %, aim time +15 % | `Optic_C` |
| 4x Scope | Optic | rifle, marksman, sniper | 4× full scope, sway ×0.35 aimed, range +25 %, aim time +25 %, hip spread +15 % | `Optic_Recon_D` |
| Pistol Red Dot | Optic | pistol | aim time −8 % | `Optic_Pistol_A` |
| Compensator | Muzzle | SMG, rifle, marksman, MG | recoil up −10 % | `Muzzle_Brake_C` |
| Suppressor | Muzzle | rifle, marksman, MG | no flash, suppressed shot, heard at 30 %, range −10 %, bullet speed −10 %, recoil up −5 % | `Muzzle_Suppressor_A` |
| Pistol Suppressor | Muzzle | pistol, SMG | no flash, suppressed shot, heard at 30 %, range −10 %, bullet speed −10 % | `Muzzle_Pistol_Suppressor_A` |
| Choke | Muzzle | shotgun | spread −20 % | `Muzzle_ShotGun_Shoke_A` |
| Vertical Grip | Grip | SMG, rifle, marksman, shotgun | recoil up −12 %, aim time +5 % | `ForwardGrip_C` |
| Angled Grip | Grip | same | aim time −10 %, recoil sideways −5 % | `ForwardGrip_A` |
| Laser Sight | Tactical | SMG, rifle, marksman, MG, shotgun | hip spread −25 %, a red dot | `Laser_A_Right` |
| Gun Flashlight | Tactical | same | a light | `Light_A_Right` |
| Pistol Light | Tactical | pistol | a light | `Light_Pistol_A` |
| Extended Rifle Mag | Magazine | rifles and marksman rifles on rifle ammo (not the RPD's drum) | magazine +33 %, aim and reload time +5 % | the AK's magazine, scaled |
| Extended Pistol Mag | Magazine | pistol ammo guns | magazine +33 %, aim time +5 % | the 1911's magazine, scaled |

**Adding a part:** add a row to `Specs` in `AgentScripts/Weapons/AttachmentSetup.cs` (Castle Bravo prefab, slot, families, modifiers, weight, value, rarity, optic settings), its name in `Data/Localization/Csv/Items.csv`, then run `AttachmentSetup.Parts` (Optics first for a new lens). It builds the unpacked model with its markers (`AttachmentModel`: muzzle tip, optic sight point at the lens centre, grip hand point, light), the pickup, the item asset, the icon, and refreshes the Item Catalog. **A new gun's mounts:** a `GunSpec` row (slots, family, mount points in the model's space from its Castle Bravo `*_PreSet`, the part each mount rides on, rails to show, sights to hide, magazines), then `AttachmentSetup.Mounts`; `GunPartsTests` checks every declared slot has a mount on both models.

### For the gun animation rework

- Mount points are children of the model parts they ride on (`Weapon/Slide/Mount_Optic`, `Weapon/Pump/Mount_Grip`, `Weapon/Hinge/Mount_Tactical`): a clip that moves a part moves its fitted part. New moving parts get their mounts parented the same way.
- `GunInstance.AttachmentsChanged` fires whenever parts change (also on the gun in hand): rebuild anything that depends on the setup there (hand contacts, clip variants per magazine).
- `GunMounts.SupportGrip` is where the support hand should hold a fitted grip (the grip model's `HandGrip` marker, laid out like a `HandGrip`). The hands don't use it yet: the current clips keep the left hand on the forend contact, so with a grip fitted the hand stays where it was.
- `GunMounts.Sight` / `IronSight` give the sight line; the optic's aim pose is a runtime marker `AimOptic` beside `Poses/Aim`, so clips and poses stay authored for iron sights.
- Magazine parts scale the `Magazine` and `ReplacementMagazine` transforms (the clips key the renderers' `enabled`, not the scale); the dropped pooled magazine keeps the base size.

### First-pass real-life stats

`AgentScripts/Weapons/RealisticGunStats.cs` (entry `Apply`) sets the other four guns from the AK47 asset (tuned by hand) and each gun's real-life ratio to the AKM. Re-run it after retuning the AK. Fire rate, magazine size and loaded weight are the real ones; reload times are real times × the AK's 2 s / 2.5 s. Damage, bullet speed, range and recoil use the **square root** of the real ratio (bullet momentum, muzzle velocity, effective range, free recoil energy), because the game compresses them: the AK already runs at 42 % of its real muzzle velocity, and a straight ratio would put .45 rounds at 106 m/s with 6 m of full damage. Knockback is momentum and takes the straight ratio. Feel values (aim speed, sway, zoom, recoil response, spread) are unchanged.

| Gun | Damage | Rate | Magazine | Reload | Speed | Range (falloff) | Recoil up (hip) | Weight |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AK47 (anchor) | 20 | 600 | 30 | 2 s | 300 m/s | 150 m (40–120) | 3.5° | 3.6 kg |
| MP5 (9 mm) | 15 | 800 | 30 | 2 s | 224 | 113 (30–91) | 1.9° | 3 kg |
| Pistol (.45) | 16 | 400 | 7 | 1.44 s | 178 | 57 (15–45) | 3.4° | 1.2 kg |
| PumpShotgun (00 buck) | 10 × 9 | pump 0.55 s | 5 | 0.48 s / shell | 222 | 47 (13–38) | 6.7° | 3.6 kg |
| DoubleBarrel (00 buck) | 10 × 9 | 300 | 2 | 2.4 s | 227 | 51 (14–41) | 7.2° | 3.4 kg |

Against a 100 HP goblin that is 5 AK body shots, 7 MP5, 7 pistol (one full magazine) and two close shotgun blasts (90 each, one when pellets hit the head). Before this the pistol did 25 with 12 rounds and a blast 96–108.

The block-out guns (below) by the same rule, from the real figures in `RealisticGunStats.Others`:

| Gun | Damage | Rate | Magazine | Reload | Speed | Range (falloff) | Recoil up (hip) | Weight |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Glock 17 (9 mm) | 15 | 450 (semi) | 17 | 1.44 s | 217 m/s | 57 m (15–45) | 3.2° | 0.9 kg |
| Desert Eagle (.50 AE, Magnum) | 25 | 180 (semi) | 7 | 1.6 s | 243 | 57 (15–45) | 7.1° | 2 kg |
| M4 (5.56) | 16 | 800 | 30 | 2 s | 333 | 179 (48–143) | 2.6° | 3.4 kg |
| AK-74M (5.45) | 15 | 650 | 30 | 2 s | 337 | 179 (48–143) | 2.1° | 3.9 kg |
| RPD (7.62×39, rifle ammo) | 20 | 700 | 100 | 4 s | 304 | 227 (60–181) | 2.3° | 9 kg |

The square-root rule flattens the big rounds: the Desert Eagle takes 4 body shots (2 to the head), where the design guessed 2. To tune by hand.

### Block-out guns

Built 2026-11-14 *(user: the design's roster, with the Desert Eagle, the RPD and the AK-74M picked)*: Glock 17, Desert Eagle, M4, AK-74M and RPD, on the tables of the Weapons Test scene. The Uzi, M1 Carbine, FAL, hunting rifle (a scoped Remington 700) and AA-12 were built too and **parked on 2026-11-16** *(user)*: their assets are deleted, and their rows stay in `GunBlockoutSetup`, `AttachmentSetup` and `RealisticGunStats` behind each script's `Parked` list. To bring one back, take its name out of the three lists, run the builders, and add it to `WeaponsTestSetup`, `AimReview` and `GunPartsTests`; its name is still in `Items.csv`. Which Castle Bravo guns are worth adding: [CASTLE_BRAVO_GUNS.md](CASTLE_BRAVO_GUNS.md). Two new ammo items: **Heavy** (`HeavyAmmo`; no gun uses it while the FAL and hunting rifle are parked) and **Magnum** (`MagnumAmmo`, Desert Eagle), one quality each until ammo tiers. They aren't in the starting rounds (they would fill the bare backpack), so the test scene places boxes and goblins drop them.

| | How |
| --- | --- |
| Prefab | A `Gun_Base` variant with no clips: it poses, kicks and shoots. The Castle Bravo model's parts keep their own names under `AnimationRoot/Motion/Weapon` (rails off until a part needs them), with a `Muzzle` at the barrel's tip and an `EjectionPort` by the bolt |
| Poses | Aim puts the sight line (iron sights, or the hunting rifle's built-in scope) on the eye's axis: `(−0.17, 0.21 − sight height, the reference's Aim z)`. Hip and Sprint are the reference gun's, lowered by how much taller the sights are, so a tall rail or optic doesn't fill the view |
| Hands | The reference gun's `HandGrip`s (finger shapes, hand scale): the right one where the reference holds its trigger, the left one on the forend (a point per gun) or, on pistols, cupping the right as the 1911's does. The shapes stay as authored; nothing keys them |
| Item | A copy of the reference gun's asset (handling, recoil, sounds, effects) with its own name, ammo and action, and no jam repair profile (that one plays clips): a jam clears on its own over `ClearTime`. References: the 1911 for the pistols, the MP5 for the SMGs, the AK47 for the rifles and the RPD, the pump shotgun for the hunting rifle (bolt `CycleTime` 1 s, rounds loaded one at a time, the sniper shot sound) and the AA-12 (automatic, box magazine, the AK's reload sounds) |
| Parts | Mounts from each model's `*_PreSet` (`AttachmentSetup` → Guns), all on `Weapon` itself until clips move parts. The hunting rifle's only slot is Optic (a fitted optic replaces its scope, whose lens draws the reticle at infinity; `AimZoom` 0.4); the RPD's Grip slot waits for a bipod |
| Icons | Rendered from the world model (`AttachmentSetup.GunIcons`) |
| AK-74M | A variant of the **AK47 prefab** with the AK-74M's meshes (Castle Bravo `AR_B_2`, the same layout) and its muzzle device: it keeps the AK47's clips, hands, mounts and jam repair, and they fit (checked in play: reload and jam clearing). Same-layout `_2` models can be cheap guns this way |

**Animated since 2026-11-16:** M4, Glock 17, Desert Eagle and the new SCAR (Castle Bravo `AR_C`, built with `GunBlockoutSetup.One("SCAR")`) went through `GunAnimateSetup` and now have clips, a rig and a jam repair profile ([HANDS.md → Animating a block-out](HANDS.md#animating-a-block-out)); `GunBlockoutSetup` skips them. The RPD is the only block-out left. The M4's Aim pose sits at z −0.40 (`AimCloser` −0.28): nearer the eye its carry-handle sight filled the view. The SCAR is not on the Weapons Test tables yet (`WeaponsTestSetup` lists it; the scene has not been rebuilt).

Rebuild order: `GunBlockoutSetup.All`, `AttachmentSetup.Mounts`, `GunBlockoutSetup.Ak74m` again (Mounts recreates the AK47's mount nodes, which drops the variant's overrides on them), `AttachmentSetup.GunIcons`, `RealisticGunStats.Apply`, `WeaponLootSetup.Apply`, `WeaponsTestSetup.Build`.

### Loot

Where parts come from in play *(user, 2026-11-14: goblins and crates)*, first guesses in `AgentScripts/Weapons/WeaponLootSetup.cs`: every part is in `Data/Loot/GoblinLoot.asset` at a weight by rarity (common 1, uncommon 0.7, rare 0.4, epic 0.15: all parts together about 7 % of a pick), with Heavy rounds 6–12 (weight 8) and Magnum rounds 4–8 (4). `Data/Loot/CrateLoot.asset` rolls one part (83 %) or nothing, and `LootCrate.prefab`'s `LootContainer` rolls it once on top of its fixed contents when first filled ([EQUIPMENT.md → Looting](EQUIPMENT.md#looting)). `InventorySetup.Loot` rebuilds GoblinLoot from scratch: re-run `WeaponLootSetup.Apply` after it.

## Melee

| Feature | How it works | Where to change it |
| --- | --- | --- |
| Swing | Click: `WindUp` seconds to the hit, then `Recovery` before the next swing can start. Holding keeps swinging at that rate (frame-rate independent); a click during a swing queues one more, never extra | Melee asset → Swing |
| Hit check | At the moment of the hit (after the camera is placed for the frame), one sphere-cast from the screen centre: `Radius`, `Range` from the eye, `HitLayers`. The first thing hit counts; one target per swing. The holder and triggers (pickups) are skipped | Swing → `Range`, `Radius`, `HitLayers` |
| Damage | `DamageService.DealHit` with `DamageType.Melee`, source = the swinger | Swing → `Damage` |
| Knockback | The hit pushes along the view: `Knockback` N·s. Rigidbodies get the impulse; damageables get it as `DamageInfo.Force` and decide how to move (`TargetDummy` tips over and springs back; enemies slide back and play a hit reaction) | Swing → `Knockback` |
| Swing motion | Procedural, between pose markers (`PoseBlender`): Idle → WindUp (eased) → a fast strike through the hit to FollowThrough (`StrikeTime`, 0.1 s; the hit lands halfway) → back to Idle at `PoseSpeed`. With `AlternateSides`, swings in a row alternate: every other one mirrors the poses left-right around Idle (a backhand). After an `AlternateResetDelay` (0.5 s) pause, or after the weapon was put away, it starts on the first side again | Melee prefab → `Poses/*`; Melee asset → Swing → Swing motion, `AlternateResetDelay` |
| Stamina and fatigue | Each swing spends `StaminaPerSwing` as it starts, hit or miss, and pauses regen. Damage and knockback are multiplied by the player's fatigue `MeleePower` at the moment of the hit | Swing → `StaminaPerSwing` (fire axe 15) |
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

**Adding a melee weapon:** make `Content/Weapons/<Melee>/`, duplicate `Content/Weapons/FireAxe/FireAxe.prefab` into it, swap the `Model`, and move the pose markers using the preview buttons (the hit doesn't depend on them, only the look does). Duplicate `Content/Weapons/FireAxe/FireAxe.asset` (or Create → Aipocalypse → Items → Melee), set `Prefab`, Swing, Sounds and `ImpactEffect`, and point the root `WorldItem._definition` at the new asset (swap `World/Model` too).

## Aim sway

`HeldItemSway` (player part) turns the movement state into one **aim offset**: degrees off the screen centre (x right, y up). Everything that points uses it, so the barrel can't point one way while the crosshair shows another:

- **Shots:** `IGunUser.AimOffset` turns the eye's forward; `Camera` and `MuzzleToCrosshair` aim along it.
- **Crosshair:** `Player.CrosshairPosition` projects it with the world camera's current field of view; `CrosshairHud` moves the dot there.
- **Model:** `EquipmentRig.SetAimRotation` turns the whole rig, which sits at the eye, so the gun pivots around the eye. The held model's lens can differ from the world's (it skips the shot punch and sprint, and takes `HeldAimZoomShare` of the aim zoom), so the rig turns by the ratio of the two (`PlayerZoom.ModelScale`, `PlayerAim.ModelRotation`): the sights land on the crosshair's pixel at any zoom.

The model's own extras (position drift, roll) don't change where it points. Gun kick (recoil, model only) and the reload / jam / pump clips do: the barrel leaves the crosshair briefly, while you can't fire anyway.

| Situation | Aim motion (hip defaults) |
| --- | --- |
| Standing | Breathing: a slow figure-8, 0.15° sideways × 0.2° up / down, 0.28 breaths/s; each breath's pace and depth stray up to `BreathVariation` (20%) on slow noise, so it never reads as a loop |
| Walking forward / back | Bob in step with the head bob: 1° up / down each step, 0.35° sideways per two steps; the aim dips 0.08° per m/s |
| Strafing | 1.6° sideways per two steps, 0.3° up / down; trails 0.2° per m/s against the move. Diagonals blend by direction |
| Stopping | The drift springs back with a small overshoot (spring 90, damping 0.5): wait a beat for a steady shot |
| Turning | Trails 0.6° per 100°/s, at most 1° |
| Jump | Dips 1.2° on takeoff |
| Airborne | Slow random drift up to 2.5°, plus the gun's `AirborneSpread` |
| Landing (from 2 m/s) | Dips 0.4° per m/s of landing speed (at most 4°) with a random sideways jolt (30% of the dip), then wobbles and settles in about half a second |
| Sprinting | Bob × 2.5; the crosshair is hidden (you can't fire) |
| Out of breath | Breathing grows up to × 3 and speeds up to 0.75 breaths/s as `PlayerBreathing.Tiredness` builds (under 25% stamina, full when empty / exhausted); × 1.5 more while [winded](#fatigue) |
| Hip wobble (guns) | On top of the rest: a slow, smooth wander that fills the gun's [hip envelope](#features) (`HeldItem.Wobble`: 3° standing with the AK, 4.5° walking, more after shots), two Perlin channels at 0.35 cycles/s (0.8 when gasping) for a 3° envelope (`WobbleSpeedSize`); a wider one slows the noise (a narrower one speeds it up, at most ×2) so the aim drifts at the same degrees per second whatever the envelope, heavy rather than frantic; eased toward the envelope's edge by a soft (tanh) limit so it never rides the rim or jerks off it; `WobbleFill` (2) sets how far it reaches (about 68% of the envelope on average, near the edge 2% of the time). Its size eases toward the envelope over `WobbleEase` (0.3 s, no sudden start or stop), at most `WobbleGrowSpeed` (4°/s), so starting to walk, landing, a flick or switching guns never pops it. Each hip shot jolts it about its bloom in a random direction on its own spring (150, damping 0.5), which settles in about half a second. `RestOffset` is the aim without it, `AimOffset` with it (the reticle, the shots and the model). It shrinks away as the sights come up, freezes in pause and hit stop, and a gun switch drops a pending jolt. Not rescaled by `HeldItemSwayScale` again: the envelope already includes it |

**Aiming down sights** keeps 70% of the breathing, all of the walking bob, 80% of the move / air drift, 50% of the turn lag and of jump / landing kicks, so the sights still bob and drift when you move (aiming walks at 60% speed, so in practice the bob is about 60% of the hip's). The model-only extras keep 70% of their bob and move lag. Raising the sights overshoots 0.5° upwards (with a random sideways share, `AimInKickSideways` 0.5) and settles; hip and aimed values blend over `BlendTime` (0.15 s). The offset is clamped to 4° at the hip, 3° aimed.

`StatId.HeldItemSwayScale` (base 1, for skills that steady the hands) and the held item's `HoldFeel.SwayScale` (`Hold feel` on its asset) scale the aim sway and the model sway. **Crouching** steadies the gun three ways: the aim and model sway keep `CrouchSway` (70 %, eased in), the hip wobble ×0.7 (`PlayerHeldItemTuning` → Guns), and recoil ×0.8 (gun asset → Recoil → `CrouchMultiplier`).

## Fatigue

Low stamina makes fighting and moving worse, never impossible. `Fatigue` (player part, ticked after `Stamina`) picks the lowest tier whose threshold is above current stamina and eases its multipliers in over `EaseTime` (0.35 s), so crossing a threshold never snaps mid-spray.

| Stamina under | Recoil | Melee damage + knockback | Walk / sprint speed |
| --- | --- | --- | --- |
| 50% | ×1.25 | ×0.85 | ×0.95 |
| 25% | ×1.6 | ×0.65 | ×0.85 |
| 10% | ×2.5 | ×0.4 | ×0.7 |

Exhaustion (stamina hit 0, until it refills to 30%) still blocks only sprint and jump; shots and swings can drain stamina to 0 and exhaust you.

**Winded:** running dry stops regen for `WindedTime` (2 s, `PlayerVitalsTuning`) before recovery starts. Meanwhile walk and sprint speed drop by a further `WindedMoveSpeed` (× 0.75, eased with the tiers) and the aim breathes harder. Draining an already exhausted meter to 0 again doesn't restart the wait.

**Out of breath** (under `Below` 25%, or exhausted; `PlayerVitalsTuning` → Out of breath): `PlayerBreathing.Tiredness` builds as stamina drops (1 at empty / exhausted) and eases over `EaseTime` (0.6 s). `PlayerBreathSound` loops `BreathSound` at `MinVolume`..1 by tiredness (fades in 0.4 s, out 1.5 s once you've caught your breath; `PlayerSoundTuning` → Out of breath), and the aim sway breathes harder and faster with it.

Two stats (base 1) are the hooks for later growth: `StatId.AttackStaminaCostScale` scales every shot and swing cost; `StatId.FatiguePenaltyScale` scales how far the tiers move from 1 (0 = no penalty). Weapons reach all of this through `IWeaponUser.Exert` / `MeleePower` and `IGunUser.FatigueRecoilScale`.

## Tuning

| To change | Edit |
| --- | --- |
| A gun's numbers | `Content/Weapons/<Gun>/<Gun>.asset` (live in Play mode) |
| A melee weapon's numbers, sounds | `Content/Weapons/<Melee>/<Melee>.asset` (live in Play mode) |
| How a melee swing looks | melee prefab → `Poses/*`; `Content/Weapons/<Melee>/<Melee>.asset` → Swing → Swing motion |
| Muzzle vs camera aiming, slower walk while aiming | `Data/Player/PlayerHeldItemTuning.asset` → Guns |
| Hip wobble size: hip cone, airborne, bloom per shot | `Content/Weapons/<Gun>/<Gun>.asset` → Accuracy |
| Hip wobble while walking / crouching / sprinting / turning | `Data/Player/PlayerHeldItemTuning.asset` → Guns → `*SpreadScale`, `TurnSpread`, `MaxTurnSpread`, `TurnSmoothing` |
| Hip wobble wander speed, shot jolt spring, safety cap | `Data/Player/PlayerHeldItemTuning.asset` → Aim sway → Hip wobble |
| What bullets hit, delay after a sprint | `Content/Weapons/<Gun>/<Gun>.asset` → Bullet → `HitLayers`, Handling → `SprintRecoveryTime` |
| Aim sway: crosshair / shot / gun motion per situation, ADS shares, limits | `Data/Player/PlayerHeldItemTuning.asset` → Aim sway |
| Model-only sway, bob, look / move lag (all held items) | `Data/Player/PlayerHeldItemTuning.asset` → Sway & bob (model only) |
| Fatigue tiers, winded speed, ease time | `Data/Player/PlayerVitalsTuning.asset` → Fatigue |
| How long you stay winded at 0 stamina | `Data/Player/PlayerVitalsTuning.asset` → `WindedTime` |
| When you're out of breath, how fast it eases | `Data/Player/PlayerVitalsTuning.asset` → Out of breath |
| Breath sound, its volume and fades | `Data/Player/PlayerSoundTuning.asset` → Out of breath |
| How heavy one item feels (aimed walk speed, sway) | its asset → Hold feel |
| Starting ammo, infinite ammo | `Data/Weapons/AmmoConfig.asset` (read at spawn, put in the backpack). In Play mode, `Player` → **Add Ammo**, `cheat.ammo` or `cheat.give rifle 60` give more |
| Which effects a gun uses, bullet hole layers / size | `Data/Weapons/BulletEffects.asset` |
| How long an effect lives, how many at once | the effect prefab in `Content/Weapons/_Shared/Effects/` → `PooledEffect` |
| Flash look and size, tracer line, flash light | `Content/Weapons/_Shared/Effects/`: `MuzzleFlash_Hovl3` → `Flash` scale; `Tracer` → `Core` / `Halo` trails (time, width, colour gradient) and `Head` (size, tint), with `Tracer_Line.mat` (core), `Tracer_Halo.mat`, `Tracer_Head.mat` (HDR colours); `MuzzleFlashLight.prefab` (colour, range) and each flash's `FlashLight` Lights module (intensity, fade); how long a tracer stays at its end and which rounds show one: `BulletEffects` → Tracer Linger, Tracer Every, Tracer Last Rounds; per-shot variation, night glow and the far-distance widening: `Tracer` → `TracerStreak`. The old Gabriel Aguiar `MuzzleFlash.prefab` (soft fireball) is kept as an alternative. All rebuilt by `AgentScripts/Weapons/ShotEffectsSetup.cs` (`Tracer()` rebuilds only the tracer and keeps its trail time); `EffectsReview.cs` fires real shots in Play mode for checking |
| Where the gun sits, aims, sprints | gun prefab → `Poses/*` |
| Reload / jam / pump motion, pump stroke, break angle | the gun's clips in `Content/Weapons/<Gun>/Animations/` ([HANDS.md](HANDS.md#weapon-animation-authoring)) |

## Feel pass

Added 2026-11-11 on top of the tables above. Content built by `AgentScripts/Feel/FeelSetup.cs` (safe to re-run). None of it has been tuned by hand yet.

| Feature | How it works | Where to change it |
| --- | --- | --- |
| View punch | Each shot widens the world view by `FovKick` (AK47 0.8 %, MP5 0.6 %, Pistol 1.2 %, shotguns 3 %), up to `FovKickMax` while spraying, settling at `FovReturnSpeed`. It rides on the gun's zoom, but the held model's lens skips it (only the aim zoom reaches it); a wider view never speeds the look up (`PlayerZoom.LookScale` is capped at 1) | Gun asset → Recoil → Field of view punch |
| Spent cases | A pooled case (a small brass or red cylinder with a Rigidbody, Debris layer, 3 s, at most 24) leaves the gun's `EjectionPort` (a child of `Weapon`, x out of the port) with a jittered throw and spin: with the shot, or with the pump when `OnCycle` (PumpShotgun). The DoubleBarrel has no port | Gun asset → Ejection; the port on the gun prefab; `Content/Weapons/_Shared/Effects/Shell_*.prefab` |
| Shot tail | `Sounds.FireTail` rings out once a spray ends (1.3 shot intervals after its last shot), or after every shot on a semi-auto. AK47 and MP5 have one | Gun asset → Sounds |
| Own shots | MP5 `Fire_Smg`, both shotguns `Fire_Shotgun` (Gamemaster clips; AK47 and Pistol keep their `_Placeholder` clips) | Gun asset → Sounds → `Fire` |
| Last rounds | From `Magazine.LowRounds` (a quarter of the magazine, magazines of 6 or more) each shot adds the `LowAmmo` click, louder as it empties, and the hotbar's ammo label turns to its warning colour (it used to at 0 only) | Magazine → `LowAmmoShare`; Sounds → `LowAmmo` |
| Surfaces | A bullet landing on the world asks `SurfaceService` what it hit (terrain layer or name keywords): that surface's tinted impact (`BulletImpact_<Surface>.prefab`, the gun's own when it has none) and a 3D impact sound, at most 2 sounds a frame. Hits on something that bleeds add `Impact_Flesh`. Footsteps use the same lookup under the player's feet | `Data/Surfaces/SurfaceSettings.asset` (rules, looks, `Enabled`) |
| Enemy hit flash | A hit lights the body for `HitFlashTime` (0.07 s): `HitFlashColor` written to `HitFlashProperty` (`_EmissionColor`) on its skinned meshes through a property block that is removed again, so the renderer is off the SRP Batcher only while it flashes | `Data/Enemies/` reaction settings → Hit flash |
| Kill feedback | `LandedHitFeedback` now has four players: `Stagger` and `HeadshotKill` (hit stop, at most one per 3 s) plus `Kill` (any other kill: a light impulse) and `MeleeHit` (a swing landing on something that takes damage: impulse + 45 ms hit stop). The light two play at most once a frame | `Player/GunJamFeedback/LandedHitFeedback/*` |
| Token burst | Auto-collected loot (Tokens) leaves a killed enemy's roll and flies out as up to `LootBurstPieces` pickups for the pickup magnet; the rest stays in the body to search | `Data/Enemies/EnemySystemSettings.asset` → Loot |
| Melee | `MaxTargets` (fire axe 3): one swing hurts the nearest that many damageable things, each body once; a wall in the way ends it. A landed swing jolts the view (`HitViewKick`) and the axe head draws a trail through the strike | Melee asset → Swing; the `Trail` on the melee prefab |
| Sprint view | The world view widens to `SprintFovScale` (1.07) while sprinting, eased at `SprintFovSpeed` | `Data/Player/PlayerCameraTuning.asset` → Sprint |

Not added: a Cinemachine impulse per shot. The view already kicks and shakes per shot (`RecoilValues` aim kick and `CameraShake`); a second shake on top only blurred it.

## Known gaps

- **Feel pass, unchecked by hand:** every value above is a first guess. The per-surface impacts are the one impact effect tinted, not own effects; the hit flash relies on the Sidekick shader's `_EmissionColor` and wasn't seen on screen; the heartbeat is a pitched-down body thump.

- **No breath clip yet:** `PlayerSoundTuning` → `BreathSound` is unset (silent) until a BroAudio entity with a breathing clip (Loop on) is added to `Data/Audio/Player`. The visual breathing rate (`TiredBreathRate`) isn't synced to the clip; tune it by ear.
- **Sound placeholders:** every gun sound is a stand-in clip (see Sounds above); swap the clips on the BroAudio entities, no code or gun asset changes needed.
- **Crosshair** matches exactly with `Camera` and `MuzzleToCrosshair`, only roughly with `Muzzle` (there the model's zoom correction also turns the barrel a little more than the view while aiming).
- **Clip authoring gaps** (their tests are marked `[Ignore]` with the reason): MP5 / pistol left-hand free control is ~1° off the magazine grip at the 0.145 switch; the AK47's is ~5.5° off the forend at 0.02; the DoubleBarrel's free hand is ~5 cm / 9° off the shell grip at 0.28 and its hinge curve overshoots ~0.5° after closing. Fix the keys in the Animation window (script edits of these clips re-tangent or rebind them), then drop the `Ignore`s in `WeaponAnimationTests`.
- **Close cover:** Muzzle / MuzzleToCrosshair check the eye-to-muzzle segment. If blocked, the shot and tracer start at the eye toward that obstruction so the bullet hits nearby cover rather than starting beyond it. Camera aiming keeps its screen-centre origin.
- **No empty-click sound of its own:** no gun sets `Sounds.Empty` yet, so the empty click reuses the jam's `DryFire` clip.
- **One quality per calibre:** no Rusty / Standard / AP tiers yet, and a gun takes exactly one `AmmoType`. Rounds can't be unloaded from a magazine back into the backpack, except the extra rounds a smaller magazine part can't hold.
- **Gun parts, first version:** no rarity per copy, no stock parts or stock slot on any gun, no laser beam (only its dot), magazine parts reuse the gun's magazine scaled up, fire-rate modifiers don't reach the trigger, and the support hand doesn't move onto a fitted grip ([For the gun animation rework](#for-the-gun-animation-rework)).
- **Block-out guns:** no clips (they pose and kick only; reload and jam clearing are timers) except the AK-74M's borrowed AK47 clips; hands sit on points read off the models, not fitted; sight heights, hip poses and stats are first guesses; the Desert Eagle, FAL and hunting rifle hit softer than the design wanted under the square-root rule; no grip part fits the RPD yet. Part icons are renders with lifted colours and a light outline (`AttachmentSetup.Outline`), so they read on the dark slots.
- **Shotguns:** no shotgun sound clips yet (placeholders above), and the pump's start sound plays with the shot.
- **Knockback shows on rigidbodies, `TargetDummy` and enemies** (`EnemyBody`); the player ignores it.
- Held weapons and muzzle flashes render through `FirstPersonRenderFeature` on the HeldWeapon layer. A smooth obstacle pose retracts and tilts the model near walls and entities without pushing the player ([EQUIPMENT.md](EQUIPMENT.md#first-person-rendering-and-obstacle-pose)).
- **Melee:** the backhand is a mirror of the forehand rather than its own poses; the swing motion is procedural until there are animations. `Player` ignores `DamageInfo.Force` (goblin blows carry none).


## Gun jam clearing and QTE

AK47, MP5, Pistol, PumpShotgun and DoubleBarrel each assign their own Jam.Repair profile (Content/Weapons/<Gun>/<Gun>_Repair.asset) and timing challenge (<Gun>_RepairQte.asset, same folder). These are separate assets so duration, QTE onset, fill speed, green zone and presentation choices can be tuned per gun. When a shot jams, the trigger falls on the jammed round (Jam click, a short gun wobble and a small camera jolt) and the gun stays stuck for `JamPause` seconds (default 0.6); each trigger pull in that window dry-clicks with a small twitch (`Gun.DryFired`). Clearing then starts automatically (with `RequireReloadPress` it waits for Reload instead, without the pause). The repair selects a 4-second three-hit or 5-second four-hit sequence once, alternating an open-palm slap and a closed-fist punch (slap, punch, slap[, punch]), with a configurable stubborn probability. The animation and gameplay timer keep advancing normally whether the player ignores, misses or succeeds at the QTE. Clearing does not restore durability or change ammo.

At QteStartProgress (default 0.5), the gun's `GunRepairFlow` (part of its `GunJam`) starts an owner-controlled QTE in `QuickTimeService`, keyed to the held gun. This value is a fraction of the repair duration: 0.5 appears halfway, 0.25 appears after one quarter. Lowering it gives more opportunities to finish early and can later be driven by an upgrade. The QTE repeats until Gun completes the repair, succeeds early, or is interrupted; it has no independent timeout and never holds the repair timer. Each gun's default TimeScale is 1, so the event makes no time-scale request. The UI appears only during the QTE, near the screen centre above/beside the gun, without a panel, heading, status popup or separate countdown.

Press the challenge's `Button` (Reload, R) while the fill is in the green zone to clear the jam immediately. A miss waits for the next fill and adds `MissPenalty` seconds (default 0.5) to the remaining repair, up to `MaxMissPenalty` (1.5) per repair. `Gun.AddRepairTime` stretches what is left of the action instead of moving progress back, so the clip slows down rather than jumping. `QuickTimeService` ticks before `Player.Update` and consumes that press (`InputService.Consume`), so a success cannot also start a reload. Gun remains the only owner of completion. On natural completion, the QTE closes with a short fade. On success, GunAnimation captures the current part transforms and hand shapes, then blends into RepairEnd over that clip's duration. Remaining slaps are skipped, so early success cannot fast-forward extra impacts or sounds. Switching, hiding, destruction, player death or active-player change cancels the QTE and leaves an interrupted gun jammed.

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
| TimeScale | 1 | No slow-motion request |
| Button | Reload | The press that attempts it; that frame's press goes to the QTE only |
| Presentations | Circle, Bar | Random widget ID; a single entry forces that presentation |
| TotalDuration | 4 s | Used only by standalone timed QTEs, ignored by the owner-controlled repair QTE |

QteChallenge.CreateSession(ownerControlled) and IQteSession separate mechanics from the service and widgets. Timed sessions still support TotalDuration for other uses; persistent sessions close with their owner. QteStarted/QteEnded are MessagePipe lifecycle announcements. QteView is the presentation seam: TimingQteView renders the same timing session as a circle or bar. New widgets register their ID in the HUD array and challenge Presentations. A different mechanic supplies a compatible challenge/session and view. Missing IDs fall back to a compatible view.

The shared Animator has RepairingStubborn and RepairEnd slots. Repairing and RepairingStubborn are editable clips sampled from Gun.ActionProgress. Each gun supplies its own Repairing, RepairingStubborn and RepairEnd overrides. The magazine guns' two clips are generated (Reseat tugs the magazine, Rack pulls the bolt or slide); the shotguns keep hand-keyed slaps on the receiver, with the pump and hinge at rest.

### Repair as a shake

A jam clears with one of three motions: the **shake** below, or one of the gun's two clips, **Reseat** (quick jams: the magazine is tugged and slapped home) and **Rack** (stubborn jams: the bolt or slide is pulled three times), generated per gun by `RepairClips` ([HANDS.md → Generated repair clips](HANDS.md#generated-repair-clips)). `GunRepairProfile.ShakeChance` (0 to 1, shown when a Shake is assigned) is the chance a repair is the shake; otherwise the clip for its kind plays. AK47: 0.34. The pump shotgun and the double barrel: 1 (their slap clips were keyed for the floating hands and look wrong with arms). A gun with no `GunAnimation` always shakes.

A repair profile with a **Shake** (`GunShakeSettings`, shared asset `Data/Weapons/GunShake.asset`) shows the repair without clips: both hands stay on the gun, it tilts to show the ejection port, is jerked a few times, and tilts back. The AK47 (and the AK-74M, which shares its profile) uses it; the magazine guns mix it with their Reseat and Rack clips by `ShakeChance`, and the shotguns still play their slap and punch clips. Any gun can opt in, block-outs included, by assigning the asset on its `<Gun>_Repair.asset` (`AgentScripts/Weapons/GunShakeSetup.cs` > `Assign`); clearing the field goes back to the gun's clips.

`HeldGun` owns a `GunShake` beside its jolt player and adds its offset on the recoil pivot, so the whole gun and both hand targets move together; `GunAnimation` stays on Idle. It follows `Gun.ActionProgress`, so miss penalties slow it like the clips. Each shake that lands reports to the gun's jam part (`GunJam.Shaken`), which plays the same slap beat as a clip's slap (sound, jolt, `GunJamBeat`); a stubborn repair's second and fourth shakes are harder and play the punch beat. When the repair ends, early or not, the gun eases back from wherever it was over `ReturnTime`.

| GunShake.asset | Default | Meaning |
| --- | --- | --- |
| TiltPosition / TiltEuler | (-0.05, 0.05, -0.02) m / (-8, -6, 28)° | The pose held while clearing; z rolls the gun so the port shows |
| TiltShare | 0.12 | Share of the repair spent tilting in, and again tilting back |
| QuickShakes / StubbornShakes | 3 / 4 | Shakes in a quick and a stubborn repair, spread evenly between the tilts |
| ShakePosition / ShakeEuler | (0, -0.035, 0.05) m / (9, 2, -5)° | The peak of one shake, added to the tilt |
| ShakeShare | 0.16 | Share of the repair one shake rings for |
| Swings | 1.75 | Back-and-forth swings in one shake |
| WindUp | 0.3 | Pull the other way before each shake, as a share of its peak |
| HardScale | 1.5 | How much harder a stubborn repair's hard shakes are |
| ReturnTime | 0.22 s | Back to rest when the repair ends |

For an Editor trial, enter Play, equip any of the five guns, select its spawned HeldGun and click **Jam gun (test)**. Clearing starts automatically. Ignore the QTE to see full completion or press R in the green zone to finish early. The debug button exists only in the Editor.


Repair impact audio follows a stepped `_repairImpactCount` float curve on GunAnimation in each repair clip. It starts at zero and increments once at each contact (short: 0.8, 1.7, 2.8 seconds; long: 0.8, 1.7, 3.0, 4.0 seconds). A second stepped curve, `_repairPunch`, is 1 from each punch's approach and 0 for slaps (`GunAnimation.ImpactIsPunch`). Slaps take 0.12 seconds from wind-up; punches close the hand into a fist during the wind-up, come from higher and land in 0.09 seconds with a bigger gun reaction. Gameplay duration still scales the full clip through ActionProgress; changing duration keeps the markers synchronized with the hand. Move the counter keys with the contact keys when editing a clip. These are sampled markers, not Unity AnimationEvents, because this graph samples clips manually.

GunAnimation announces each newly crossed marker once through the local RepairSlapped event, including markers skipped by a slow frame. The gun's `GunJam` hands it to its `GunRepairFeedback`, which plays the gun's `Sounds.RepairSlap` entity (`RepairPunch` for punches, falling back to the slap), which picks a random clip at the entity's volume, adds the matching gun jolt and publishes a GunJamBeat. All five guns share the `RepairSlap` (Malbers Hit2, Wood Impact) and `RepairPunch` (Hit1, Hit3, Highlands Metal Shield 1) entities as replaceable impact variations. The bolt and magazine remain stationary during repair. No new impacts fire during pause, interruption or the QTE return. Audio remains presentation-only and cannot complete the jam.

### Repair result feedback

All five repair profiles reference Data/Weapons/RepairFeedback.asset (`RepairFeedbackSettings`: the gun jolts and sounds). Duplicate this preset and assign GunRepairProfile.Feedback to customize one gun without changing its timing challenge or animation clips. Each beat has up to three layers: gun sound and an additive gun jolt (the gun's `GunRepairFeedback`, on the recoil pivot), a camera impulse / hit stop (`GunJamFeedback` on the Player) and, for QTE results, the HUD widget (`QteHud`, its look in `QteStyle`: [UI.md → QTE](UI.md#qte)).

| Beat | Gun (sound, jolt) | Camera (`Player/GunJamFeedback`) | HUD |
| --- | --- | --- | --- |
| Jammed | Jam click, `JamJolt` wobble | small jolt | — |
| Dry fire (pull while stuck) | DryFire click, `DryFireJolt` twitch | tiny bump | — |
| Slap / punch | `RepairSlap` / `RepairPunch` entities, `SlapJolt` / `PunchJolt` | small / medium shake; the punch adds a 35 ms hit stop at 0.1× | — |
| QTE miss | MissSound + dull `MissAccent` thud, `MissJolt` fumble wobble; +0.5 s repair | sideways recoil shake | red flash, 22 px shake + squash over 0.4 s |
| QTE success | metal `SuccessSound` hit + `SuccessAccent` chime, then `SuccessMechanism` (bolt home) after 0.12 s; strong `SuccessJolt` | big shake + 80 ms hit stop at 0.05× | full green widget, 150% punch, ring to 260%, 0.32 s hold, 1 s total |
| Natural completion | quieter `NaturalSound` | light bump | 0.18 s fade |

A `GunJolt` is a position/rotation peak, a duration and an optional number of damped wobbles (0 = one kick and return). Repair completes immediately on success, so the visual hold does not delay firing. Ignored presses, cancellation and switching produce no failure feedback. The clips are placeholders from `_Placeholder` and Malbers/Highlands store audio.

Camera: the player's `PlayerCinemachineCamera` has a `CinemachineImpulseListener` (camera space, gain 0.015, so the impulse itself barely moves the camera) whose reaction plays `Data/Camera/ImpulseReaction.asset`, a rotation-only noise scaled by impulse strength (about 2-3° peak for a success). Each beat is an `MMF_Player` child of `Player/GunJamFeedback` with one `MMF_CinemachineImpulse` (Uniform; Bump, or Recoil for the miss) and, for punch and success, an `MMF_HitStop`. Tune one beat with its impulse Velocity length, or all of them with the listener's reaction Amplitude Gain (5). `MMF_HitStop` asks `TimeService.RequestHitStop` (real-time duration; overlapping stops keep the longest time and lowest scale; ignored while paused); the QTE widget tweens run on unscaled time so they don't freeze.

TimingQteSession tracks total lifetime separately from pointer movement. A miss holds the pointer for MissPauseDuration, rejects presses during the hold, then relocates the green zone and resumes from the held position. Green-zone width remains unchanged; the random position excludes a band around the previous position using MinimumZoneShift, clamped for wide zones. With OneAttemptPerCycle enabled, the next accepted attempt remains on the next fill. Standalone timeouts and the owning gun's repair timer keep advancing during pointer holds. Zone relocation can be disabled and a zero hold is supported.

RepairFeedbackSettings exposes the six gun jolts, the result sounds (SoundIDs into `Data/Audio/RepairFeedback/`, each entity holding its own volume) and `MechanismDelay`; the widget's colors, durations and scale / shake / squash strengths are in `QteStyle`. A gun's jam is one part, `GunJam`, built for each `HeldGun` by the root `GunJamFactory` (which holds `AudioService`, `QuickTimeService` and the jam publishers): its `GunRepairFlow` holds the repair's QTE rules (plain C#: start at `QteStartProgress`, early finish on success, miss penalty, interrupt on cancel) and its `GunRepairFeedback` presents them: plays the sounds, publishes GunJamBeat (Jammed, DryFire, Slap, Punch, Miss) and GunRepairCompleted with owner/item, and adds the active jolt to the existing recoil pivot on scaled time (a hit stop slows it too). It does not change gameplay recoil. The HUD widget follows the QTE's own events (`QteStarted`, `QteAttempted`, `QteEnded`), never the gun. IQteSession.Press returns Ignored/Miss/Success, and QuickTimeService publishes QteAttempted only for accepted attempts so repeated input in a locked cycle cannot spam feedback. Cancellation is separate from a missed attempt.
