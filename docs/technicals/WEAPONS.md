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
| Damage | Each bullet calls `IDamageable.TakeDamage` with `DamageType.Gunshot` on what it hits | Fire → `Damage` |
| Never hits the shooter | Anything under the shooter's root is skipped (body, held gun). Triggers (pickups) are ignored | `IGunUser.Root` |
| Aim origin switch | **Muzzle**: bullets leave the muzzle along the barrel (follows pose and gun kick, the old behaviour). **Camera**: they leave the screen centre; the tracer still starts at the muzzle and slides onto the real path. **MuzzleToCrosshair**: a ray from the screen centre finds what the crosshair is on (or the point at full range), and bullets leave the muzzle towards it (`GunAim.MuzzleToPoint`); closer than 1 m or behind the muzzle, they use the camera direction. The aim kick (the view) moves the shots in every mode; the gun's small extra kick only moves them in Muzzle mode | `PlayerTuning` → Guns → `AimFrom` (live in Play mode). Set to MuzzleToCrosshair |
| Magazine | Rounds live on the gun (`GunInstance`), so they survive drop, pick up and swap. A new gun spawns full | Magazine → `Size` |
| Shared ammo by type | The player carries an `AmmoReserve`: spare rounds per `AmmoType`. Guns of the same type reload from the same pool | `Data/Weapons/AmmoConfig.asset` (starting amounts, `Infinite`) |
| Magazine reload | One timed reload; takes only what the reserve has | Magazine → `Reload = Magazine`, `ReloadTime` |
| Per-round reload | One round per `ReloadTime` until full or the reserve is empty (tube shotgun style). Pulling the trigger stops it once the round going in is loaded, then that click fires (no instant shot from the reload pose) | Magazine → `Reload = PerRound` |
| Pump / bolt action | After each shot that leaves a round, the gun works its action for `CycleTime` (`GunAction.Cycling`) before it can fire again. Aiming stays up. A click during the pump is kept and fires when it's done. After the last round there's nothing to chamber, so it goes straight to the reload. Switching away mid-pump finishes the pump when the gun is raised again. 0 = self-loading | Fire → `CycleTime` |
| Moving parts | Optional, on the held prefab: a **pump part** slides by `Pump Stroke` and back over the cycle (and racks while clearing a jam); a **break part** swings to `Break Open Euler` while reloading | Gun prefab → `HeldGun` → Moving parts |
| Auto reload | Starts by itself when the magazine runs dry and there's ammo | Magazine → `AutoReload` |
| Jams | Each shot has `Chance` to jam instead of firing (no round used). A jammed gun can't fire; clearing starts by itself and takes `ClearTime`, with a racking motion | Gun asset → Jam |
| Recoil | Two parts per shot, along a random spray pattern that restarts after a 0.24 s pause. **Aim kick** turns the view (up, sideways, roll, shake), so the crosshair and the shots climb together. **Gun kick** is a small, fast extra tilt + push-back of the model only, on top of the view it's attached to, so the barrel stays near the crosshair. Both spring back. Hip-fire and aiming have separate values | Gun asset → Recoil → Aim kick / Aim response / Gun kick |
| Recoil modifiers | Extra kick while moving / airborne; the first `AccurateShots` of a spray kick less | Recoil |
| Poses | Hip, Aim and Sprint pose markers in the gun prefab; the gun blends between them. The Hip pose is turned so the barrel points at the screen centre 20 m ahead (bullets from the muzzle don't cross the crosshair at an angle) | Prefab → `Poses/*`; blend speeds: Gun asset → Handling |
| Aiming rules | Sprinting blocks fire and aim. Reloading and clearing a jam drop the aim (sprinting is allowed while reloading). Switching away cancels a reload or jam clear (it restarts next time) | `Gun.Tick` |
| Slower while aiming | Move speed × `AimMoveSpeedMultiplier` | `PlayerTuning` → Guns |
| Reload / jam / pump motion | Temporary procedural tilt (reload), dip per round (per-round reload), tilt + rack (jam), small dip and roll over the pump stroke until there are animations | Gun prefab → `HeldGun` → Action motion |
| Pose preview | Hip / Aim / Sprint buttons on `HeldGun` snap the model to a pose in the prefab | `HeldGun` inspector |
| Effects | Muzzle flash, tracer, impact (removed after 3 s), bullet hole decal (removed after 20 s, at most 64). All pooled; no holes on things that take damage | `Data/Weapons/BulletEffects.asset`; lifetime / cap on each effect prefab's `PooledEffect` |
| Sounds | Equip, fire, reload start / insert / end, jam, jam clear start / end, cycle start / end (pump back / home) as plain `AudioClip`s, played with `PlayOneShot` on the gun prefab's 2D `AudioSource` (BroAudio later). Clips come from the old project's samples (`Assets/_Samples/Audio/Weapons`): rifle / pistol equip, fire, mag out (reload start), mag in (reload end), sniper bolt pull / push (jam clear). The shotguns borrow sniper fire / equip, bolt pull / push (pump; break open / close) and pistol mag in (shell insert) until there are shotgun clips. Jam has no clip yet | Gun asset → Sounds (clips, volume) |
| Crosshair | A 6 px dot at the screen centre (placeholder, to be polished) | `HUD` → `SafeFrame/Center/Crosshair` |
| Ammo on the HUD | Magazine / reserve in the corner of each gun's hotbar slot (`30/90`; magazine only when the reserve is infinite), red when empty or jammed | [UI.md](UI.md#hotbar) |

Cut from the old system: built-in fallback recoil assets, Animator triggers (no gun animations exist), the laser sight, the `Item` / `Weapon` / `IEquipable` bases (replaced by the item definitions and `EquipmentSwitcher`), and the hidden "legacy" stat copies.

## Layers

```text
Data
  GunDefinition (Data/Items/)   Fire, Bullet, Magazine, Jam, Recoil (Hip / Aim), Handling, Sounds (AudioClips), Effects
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
AK47 (variant of Gun_Base)    HeldGun · AudioSource (2D, gun sounds)
├─ Poses
│  ├─ Hip                     where the gun rests (relative to the rig's Hand), barrel on the screen centre 20 m out
│  ├─ Aim                     sights on the screen centre
│  └─ Sprint                  lowered and turned
└─ PosePivot                  moved to the active pose (+ reload / jam motion)
   └─ RecoilPivot             gun kick
      ├─ Model                the store prefab (shadows off, no colliders)
      └─ Muzzle               bullets and the flash leave here, along its blue (Z) axis
```

Shotguns add a moving part inside `Model`: `PumpShotgun` slides `ShotGun_C_BarrelGuard` (the forend) as its pump part; `DoubleBarrel` has its model unpacked so the barrels, rib and forend sit under a `Hinge` at the front of the receiver, its break part.

Poses came from the old project (gun root at (0, −0.15, 0.3) under the camera) and are stored relative to `Hand`.

## Adding a gun

1. **Prefab:** right-click `Prefabs/Guns/Gun_Base.prefab` → Create → Prefab Variant. Drag the store model under `PosePivot/RecoilPivot`, rename it `Model`, turn off its shadows and remove colliders. Move `Muzzle` to the barrel tip (blue axis out of the barrel).
2. **Poses:** select the gun root, use the **Aim** preview button, and move `Poses/Aim` until the sights sit on the screen centre in the Game view (enter Play mode to check; edits to the prefab in Play mode are lost). Do the same for Hip and Sprint, then press **Hip** before saving.
3. **Item:** duplicate `Data/Items/AK47.asset` (or Create → Aipocalypse → Items → Gun). Set `DisplayName`, `Icon`, `HeldPrefab` (the variant), Fire / Bullet / Magazine (pick or create an `AmmoType` in `Data/Weapons/`) / Jam / Recoil, and `Effects` (`Data/Weapons/BulletEffects.asset`).
4. **Pickup:** make a world prefab like `AK47_Pickup` (root with `WorldItem` + trigger `BoxCollider`, model lying flat) and assign it to the asset's `WorldPrefab`. Drop copies into a scene.
5. **New ammo type?** Add its starting amount to `Data/Weapons/AmmoConfig.asset`.

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
| Damage | `IDamageable.TakeDamage` with `DamageType.Melee` | Swing → `Damage` |
| Knockback | The hit pushes along the view: `Knockback` N·s. Rigidbodies get the impulse; damageables get it as `DamageInfo.Force` and decide how to move (`TargetDummy` tips over and springs back; enemies later) | Swing → `Knockback` |
| Swing motion | Procedural, between pose markers: Idle → WindUp (eased) → a fast strike through the hit to FollowThrough (`_strikeTime`, the hit lands halfway) → back to Idle. Swings in a row alternate sides: every other one mirrors the poses left-right around Idle (a backhand). After a 0.5 s pause it starts on the first side again | Melee prefab → `Poses/*`, `HeldMelee` → Swing motion |
| Rules | Sprinting starts no swing (the current one finishes). Switching away cancels a swing that hasn't landed | `MeleeAttack.Tick` |
| Effects, sounds | `ImpactEffect` at the hit point (the fire axe uses the bullet impact). Equip, swing (at the start), hit flesh (something that takes damage), hit surface, on the prefab's 2D `AudioSource`. The fire axe uses Malbers' common audio: `Melee Swing`, `Flesh Hit`, `Wood Impact` | Melee asset → Sounds, `ImpactEffect` |
| Pose preview | Idle / Wind-up / Follow-through / Sprint buttons on `HeldMelee` | `HeldMelee` inspector |

```text
FireAxe                       HeldMelee · AudioSource (2D)
├─ Poses
│  ├─ Idle                    where it rests (relative to the rig's Hand)
│  ├─ WindUp                  raised, ready to strike
│  ├─ FollowThrough           where the swing ends, past the hit
│  └─ Sprint
└─ PosePivot                  moved along the swing / to the active pose (the swing turns around this point)
   └─ Model                   the store prefab (shadows off, no colliders)
```

**Adding a melee weapon:** duplicate `Prefabs/Melee/FireAxe.prefab`, swap the `Model`, and move the pose markers using the preview buttons (the hit doesn't depend on them, only the look does). Duplicate `Data/Items/FireAxe.asset` (or Create → Aipocalypse → Items → Melee), set `HeldPrefab`, Swing, Sounds and `ImpactEffect`, and make a pickup like `FireAxe_Pickup.prefab`.

## Tuning

| To change | Edit |
| --- | --- |
| A gun's numbers | `Data/Items/<Gun>.asset` (live in Play mode) |
| A melee weapon's numbers, sounds | `Data/Items/<Melee>.asset` (live in Play mode) |
| How a melee swing looks | melee prefab → `Poses/*`, `HeldMelee` → Swing motion |
| Muzzle vs camera aiming, slower walk while aiming | `Data/Player/PlayerTuning.asset` → Guns |
| Starting ammo, infinite ammo | `Data/Weapons/AmmoConfig.asset` (read at spawn). In Play mode, `Player` → **Add Ammo** gives more |
| Which effects a gun uses, bullet hole layers / size | `Data/Weapons/BulletEffects.asset` |
| How long an effect lives, how many at once | the effect prefab in `Prefabs/Effects/` → `PooledEffect` |
| Where the gun sits, aims, sprints | gun prefab → `Poses/*` |
| Reload / jam / pump motion, pump stroke, break angle | gun prefab → `HeldGun` → Action motion / Moving parts |

## Known gaps

- **Sounds bypass BroAudio for now:** each gun plays its clips on its own `AudioSource`, so a sound is cut when the gun is fully holstered or dropped. Moving to BroAudio means swapping the `AudioClip` fields for `SoundID`s and `HeldGun.Play` for `BroAudio.Play`.
- **Crosshair is a plain dot:** no spread / hit feedback, no hit marker or damage numbers yet. It matches exactly with `Camera` and `MuzzleToCrosshair`, only roughly with `Muzzle`.
- **Close cover:** Muzzle / MuzzleToCrosshair check the eye-to-muzzle segment. If blocked, the shot and tracer start at the eye toward that obstruction so the bullet hits nearby cover rather than starting beyond it. Camera aiming keeps its screen-centre origin.
- **No dry-fire click** when the magazine and reserve are empty.
- **Ammo only from `AmmoConfig`:** there are no ammo pickups or inventory yet.
- **Shotguns have no damage falloff**: each pellet does full damage until the bullet's `Range`; spread alone thins hits at distance. No shotgun sound clips yet (placeholders above), and the pump's start sound plays with the shot.
- Reload, jam and pump motions are procedural placeholders; replace them with animations driven from `Gun.ActionStarted` / `ActionProgress`.
- Held weapons and muzzle flashes render through the weapon overlay camera. A smooth obstacle pose retracts and tilts the model near walls and entities without pushing the player ([EQUIPMENT.md](EQUIPMENT.md#first-person-rendering-and-obstacle-pose)).
- **Melee:** no hit-stop, hit marker or camera jolt yet; one target per swing (no cleave); the backhand is a mirror of the forehand rather than its own poses; the swing motion is procedural until there are animations. `Player` ignores `DamageInfo.Force` (nothing hits the player yet).
