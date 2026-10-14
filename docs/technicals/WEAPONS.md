# Weapons

Guns: shooting, ammo, reloading, jams, recoil, aiming, bullets and their effects. Ported from the old project's `WeaponSystem` and rebuilt on this project's rules (plain C# rules + one view, central ticks, pooling). Items, the hotbar and switching are in [EQUIPMENT.md](EQUIPMENT.md); script locations in [CODE_STRUCTURE.md](CODE_STRUCTURE.md). Melee is not built yet.

Today: an **AK-47** (auto) and a **pistol** (semi-auto), picked up from the table in `Scenes/Prototype.unity`, with a shooting range (backstop wall + two `TargetDummy` capsules) further ahead.

## Controls

| Input | Action | Binding (`InputSystem_Actions`, Player map) |
| --- | --- | --- |
| `Attack` | Fire (hold for auto) | Left mouse, gamepad west |
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
| Aim origin switch | **Muzzle**: bullets leave the muzzle along the barrel (follows pose and gun kick, the old behaviour). **Camera**: they leave the screen centre; the tracer still starts at the muzzle and slides onto the real path. **MuzzleToCrosshair**: a ray from the screen centre finds what the crosshair is on (or the point at full range), and bullets leave the muzzle towards it (`GunAim.MuzzleToPoint`); closer than 1 m or behind the muzzle, they use the camera direction. Camera kick still moves the shots; the gun's own extra kick doesn't | `PlayerTuning` → Guns → `AimFrom` (live in Play mode). Set to MuzzleToCrosshair |
| Magazine | Rounds live on the gun (`GunInstance`), so they survive drop, pick up and swap. A new gun spawns full | Magazine → `Size` |
| Shared ammo by type | The player carries an `AmmoReserve`: spare rounds per `AmmoType`. Guns of the same type reload from the same pool | `Data/Weapons/AmmoConfig.asset` (starting amounts, `Infinite`) |
| Magazine reload | One timed reload; takes only what the reserve has | Magazine → `Reload = Magazine`, `ReloadTime` |
| Per-round reload | One round per `ReloadTime` until full or the reserve is empty (shotgun style) | Magazine → `Reload = PerRound` |
| Auto reload | Starts by itself when the magazine runs dry and there's ammo | Magazine → `AutoReload` |
| Jams | Each shot has `Chance` to jam instead of firing (no round used). A jammed gun can't fire; clearing starts by itself and takes `ClearTime`, with a racking motion | Gun asset → Jam |
| Recoil | Each shot kicks the gun (tilt + push back) and the view (pitch, yaw, roll, shake) along a random spray pattern that restarts after a 0.24 s pause, then both spring back. Hip-fire and aiming have separate values | Gun asset → Recoil |
| Recoil modifiers | Extra kick while moving / airborne; the first `AccurateShots` of a spray kick less | Recoil |
| Poses | Hip, Aim and Sprint pose markers in the gun prefab; the gun blends between them | Prefab → `Poses/*`; blend speeds: Gun asset → Handling |
| Aiming rules | Sprinting blocks fire and aim. Reloading and clearing a jam drop the aim (sprinting is allowed while reloading). Switching away cancels a reload or jam clear (it restarts next time) | `Gun.Tick` |
| Slower while aiming | Move speed × `AimMoveSpeedMultiplier` | `PlayerTuning` → Guns |
| Reload / jam motion | Temporary procedural tilt (reload), dip per round (per-round reload), tilt + rack (jam) until there are animations | Gun prefab → `HeldGun` → Action motion |
| Pose preview | Hip / Aim / Sprint buttons on `HeldGun` snap the model to a pose in the prefab | `HeldGun` inspector |
| Effects | Muzzle flash, tracer, impact (removed after 3 s), bullet hole decal (removed after 20 s, at most 64). All pooled; no holes on things that take damage | `Data/Weapons/BulletEffects.asset`; lifetime / cap on each effect prefab's `PooledEffect` |
| Sounds | Equip, fire, reload start / insert / end, jam, jam clear start / end as plain `AudioClip`s, played with `PlayOneShot` on the gun prefab's 2D `AudioSource` (BroAudio later). Clips come from the old project's samples (`Assets/_Samples/Audio/Weapons`): rifle / pistol equip, fire, mag out (reload start), mag in (reload end), sniper bolt pull / push (jam clear). Jam and per-round insert have no clip yet | Gun asset → Sounds (clips, volume) |
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

Rules (plain C#, EditMode-tested in GunTests)
  GunInstance                   Rounds, IsJammed (per copy)
  AmmoReserve                   counts by AmmoType, Take / Add, Infinite
  GunTrigger                    when shots happen (mode, rate, bursts)
  Gun                           one held gun: trigger + magazine + reload / jam clear + aim; Fired, Jammed, Action events
  GunRecoil                     kick and spring-back → GunEuler, GunKickBack, ViewEuler, ViewShake
  BulletSpread                  direction inside a cone

Views and services
  HeldGun : HeldItem            on the gun prefab: feeds Gun from HeldItemInput, spawns bullets, applies poses and recoil, sounds
  BulletSystem  (root, tick)    bullets in flight → hits → damage, impact, hole
  EffectPool    (root, tick)    one pool per effect prefab, timed returns, MaxActive
```

Who calls what each frame: [CODE_STRUCTURE.md → Frame order](CODE_STRUCTURE.md#frame-order-playerupdate). In short, `Player` builds a `HeldItemInput` from its intent and motor state, `EquipmentRig.TickHeld` passes it to the held item (with `IsReady` from the switcher), and `Player` then applies the item's `ViewKick` to the look and its `IsAiming` to the motor speed.

## Gun prefab

```text
AK47 (variant of Gun_Base)    HeldGun · AudioSource (2D, gun sounds)
├─ Poses
│  ├─ Hip                     where the gun rests (relative to the rig's Hand)
│  ├─ Aim                     sights on the screen centre
│  └─ Sprint                  lowered and turned
└─ PosePivot                  moved to the active pose (+ reload / jam motion)
   └─ RecoilPivot             gun kick
      ├─ Model                the store prefab (shadows off, no colliders)
      └─ Muzzle               bullets and the flash leave here, along its blue (Z) axis
```

Poses came from the old project (gun root at (0, −0.15, 0.3) under the camera) and are stored relative to `Hand`.

## Adding a gun

1. **Prefab:** right-click `Prefabs/Guns/Gun_Base.prefab` → Create → Prefab Variant. Drag the store model under `PosePivot/RecoilPivot`, rename it `Model`, turn off its shadows and remove colliders. Move `Muzzle` to the barrel tip (blue axis out of the barrel).
2. **Poses:** select the gun root, use the **Aim** preview button, and move `Poses/Aim` until the sights sit on the screen centre in the Game view (enter Play mode to check; edits to the prefab in Play mode are lost). Do the same for Hip and Sprint, then press **Hip** before saving.
3. **Item:** duplicate `Data/Items/AK47.asset` (or Create → Aipocalypse → Items → Gun). Set `DisplayName`, `Icon`, `HeldPrefab` (the variant), Fire / Bullet / Magazine (pick or create an `AmmoType` in `Data/Weapons/`) / Jam / Recoil, and `Effects` (`Data/Weapons/BulletEffects.asset`).
4. **Pickup:** make a world prefab like `AK47_Pickup` (root with `WorldItem` + trigger `BoxCollider`, model lying flat) and assign it to the asset's `WorldPrefab`. Drop copies into a scene.
5. **New ammo type?** Add its starting amount to `Data/Weapons/AmmoConfig.asset`.

A shotgun is the same steps with `Pellets` > 1, `SpreadAngle` > 0 and `Reload = PerRound`.

## Tuning

| To change | Edit |
| --- | --- |
| A gun's numbers | `Data/Items/<Gun>.asset` (live in Play mode) |
| Muzzle vs camera aiming, slower walk while aiming | `Data/Player/PlayerTuning.asset` → Guns |
| Starting ammo, infinite ammo | `Data/Weapons/AmmoConfig.asset` (read at spawn). In Play mode, `Player` → **Add Ammo** gives more |
| Which effects a gun uses, bullet hole layers / size | `Data/Weapons/BulletEffects.asset` |
| How long an effect lives, how many at once | the effect prefab in `Prefabs/Effects/` → `PooledEffect` |
| Where the gun sits, aims, sprints | gun prefab → `Poses/*` |
| Reload / jam motion | gun prefab → `HeldGun` → Action motion |

## Known gaps

- **Sounds bypass BroAudio for now:** each gun plays its clips on its own `AudioSource`, so a sound is cut when the gun is fully holstered or dropped. Moving to BroAudio means swapping the `AudioClip` fields for `SoundID`s and `HeldGun.Play` for `BroAudio.Play`.
- **Crosshair is a plain dot:** no spread / hit feedback, no hit marker or damage numbers yet. It matches exactly with `Camera` and `MuzzleToCrosshair`, only roughly with `Muzzle`.
- **Muzzle inside a wall:** with Muzzle / MuzzleToCrosshair aiming and the muzzle pushed into a wall, bullets start on the far side. Camera aiming doesn't have this. (With MuzzleToCrosshair, cover in front of the barrel but below the crosshair is hit, as expected.)
- **No dry-fire click** when the magazine and reserve are empty.
- **Ammo only from `AmmoConfig`:** there are no ammo pickups or inventory yet.
- **Per-round reload can't be interrupted by firing** (as in the old system).
- The held gun renders in the world camera, so it can clip into walls (see [EQUIPMENT.md](EQUIPMENT.md#known-gaps)).
- Reload and jam motions are procedural placeholders; replace them with animations driven from `Gun.ActionStarted` / `ActionProgress`.
