# Hip wobble and dynamic crosshair (design)

Date: 2026-11-15. Status: direction approved by the user (option 2 + a large dynamic cross, plus Far Cry hip framing); plan in `docs/superpowers/plans/2026-11-15-hip-wobble-crosshair.md`.

## Intent

Hip-fire inaccuracy should be visible on the gun, not hidden. Today a shot from the hip goes up to `HipSpread`° off the aim in a random direction while the gun model stays still, so bullets leave at angles the barrel never pointed, and the crosshair's ticks barely react to what the player does. The user wants the Bodycam feel: the gun itself wobbles at the hip, bullets go exactly where the barrel points, and the wobble grows with movement, firing and tiredness (later sanity). A large, animated cross tells the player how far the barrel may wander.

## Goals

- No hidden hip randomness: the barrel, the crosshair dot and the shot always agree.
- One spread value (the wobble envelope, in degrees) that rises with walking, sprinting, jumping and each shot, falls when crouching or standing still, and is multiplied by fatigue and by the steady-hands stat (where sanity can plug in later).
- A large cross (four lines) that opens and closes with the envelope, animated (fast open, slower close, a jump per shot), always visible at the hip.
- Gun stats keep their meaning: each gun's `HipSpread` and the parts that change it (laser −25 %, 4× +15 %) set the envelope's base size.

## Non-goals

- No change to aiming down sights: the envelope shrinks to the gun's `AimSpread` (0) as the sights come up; ADS keeps today's aim sway and recoil.
- No sanity system (not built; the GDD says its effects are TBD). Only a hook.
- No "no crosshair" hardcore mode (possible later as a setting).
- Shotgun pellets still spread randomly around the barrel (`SpreadAngle`); that's the pattern, not inaccuracy.

## Anti-goals

- A dot that jitters so fast it can't be read. The wander is slow and smooth (well under a cycle per second at rest); only shots jolt it.
- Bullets landing outside the cross: the barrel never leaves the envelope except for a brief shot kick.

## Approaches considered

1. **Dynamic crosshair only (COD / Far Cry)**: the cone grows with actions, bullets stay random in it, the gun stays still. Cheap and readable, but keeps the barrel/bullet mismatch. Rejected.
2. **The gun's wobble is the spread (chosen)**: the envelope drives a procedural wander of the aim (gun, dot and shots move together, like the existing aim sway); bullets follow the barrel; the cross shows the envelope.
3. **Like 2 with no hip crosshair (pure Bodycam)**: most realistic, hard to read in a horde shooter. Kept as a possible later setting.

The user picked 2 with a large dynamic cross (2026-11-15).

## Design

### The envelope (plain C# rule, WeaponSystem)

`GunAccuracy` grows from "airborne spread" into the hip envelope. Each frame:

```text
envelope = (cone(aimWeight) × movement + air + bloom × (1 − aimWeight)) × fatigue × steadiness
```

- `cone(aimWeight)`: `GunStats.ConeAt` (HipSpread → AimSpread as the sights come up), as today.
- `movement`: 1 standing, walk ×1.5 while moving, crouch ×0.7 while crouching, sprint ×2.5 while sprinting (the cross is hidden while sprinting; the envelope is high when the sprint ends and settles, a sprint recovery). The player's `HeldItemDriver` picks the scale from `PlayerHeldItemTuning` → Guns and passes it as `HeldItemInput.MoveSpreadScale`, so the gun never reads player state; the envelope's settle smooths the change.
- `air`: today's airborne spread (`AirborneSpread`, `AimMultiplier` while aiming, × the player's `AirSpreadScale`).
- `bloom`: each shot adds `BloomPerShot`°, capped at `MaxBloom`, falling at `BloomRecovery`°/s.
- `fatigue`: the user's `FatigueRecoilScale` (fatigue tiers, already used for recoil).
- `steadiness`: the player's `StatId.HeldItemSwayScale` (skills that steady the hands; a future sanity modifier adds to it), exposed to guns as `IGunUser.SteadinessScale`.
- Rises within a frame or two, settles at `SettleSpeed` (as the airborne spread does today).

New settings go on the gun asset's Accuracy block (per gun: bloom) and the player's held-item tuning (movement scales), first guesses: walk ×1.5, crouch ×0.7, sprint ×2.5; bloom AK 0.5° / MP5 0.4° / pistols 0.8° / shotguns 1.5° per shot, `MaxBloom` 3°, recovery 4°/s. The 11 block-out guns take their reference gun's bloom (rebuilds copy the reference's asset).

### The wobble (player side, HeldItemSway)

- `HeldItem` exposes `Wobble` (the envelope, degrees; 0 by default) and `WobbleKick` (degrees this frame: a shot's jolt, random direction, about the bloom it added), read like `LookTurn`.
- `HeldItemSway` adds a hip wander term on top of the existing (clamped) aim sway: smooth 2D noise scaled by `Wobble`, a little faster when tired or moving, plus its own spring for `WobbleKick` jolts. The sum stays within the envelope (a brief overshoot on a kick is allowed). It exposes `RestOffset` (the aim sway without the wander) next to `AimOffset` (with it).
- Because the shots, the crosshair dot and the model already follow `AimOffset`, the barrel and the bullets agree with no extra plumbing.

### Shots

`HeldGun` drops the random hip and airborne cone from its shot (`BulletSpread.Apply(direction, AirSpread + HipCone)`); pellets keep `SpreadAngle`. `AimFrom` stays MuzzleToCrosshair.

### Crosshair (CrosshairHud, HUD.prefab)

Changed during the build (user, 2026-11-15): the whole reticle, the dot and the cross together, moves with the barrel (the cross no longer stays at the rest aim), and reticles are swappable prefabs (`HipReticle`, `CrosshairHud.SetReticle`) for unlockable designs later. Below is the original plan.


- The cross (four lines, larger than today's ticks: about 12 px long, 2 px thick at 1080p) is centred on `Player.CrosshairCentre` (from `RestOffset`) at a radius of the envelope plus the pellet spread, converted to pixels.
- The dot sits at `Player.CrosshairPosition` (from `AimOffset`), so it wanders inside the cross. The hit marker moves under the dot (it marks where the bullet went).
- Animation (`CrosshairStyle`): opens fast (`OpenSpeed`), closes slower (`CloseSpeed`); per-shot growth comes from bloom; a minimum gap keeps the cross visible at the hip; it fades while aiming down sights and sprinting, as today.
- `GunReadout` (WeaponsTest dev panel) shows the envelope in place of the cone.

### Hip framing (Far Cry)

Added 2026-11-15 (user, with Far Cry 4 screenshots of the AK47, P416 and MP5): the hip pose should hold the gun like Far Cry does. The gun sits in the lower right, upright (little roll), its barrel converging on the screen centre; the front sight lands a little right of and below the centre (about 56–62 % across, 55–60 % down at 16:9); the stock and receiver leave the frame at the bottom right; the right hand shows on the grip. Aimed views stay as they are (already close, like Far Cry's).

- Data only: each base gun's `Poses/Hip` (AK47, MP5, Pistol, PumpShotgun, DoubleBarrel), keeping today's rule that the hip barrel points at the screen centre 20 m ahead. Tuned by screenshot against the references. The block-out guns take their reference gun's hip pose, lowered by their sight height (`GunBlockoutSetup` does this on a rebuild; the existing prefabs get the same shift).
- Sprint poses unchanged.

### References

Far Cry (gun position and view, hip and aimed framing, handling) and Bodycam (procedural hip wobble, bullets following the barrel) are the references for gun play and gun feel. `WEAPONS.md` says so at the top.

### Tests

- `GunAccuracy`: still is the base cone; moving / crouching / sprinting scale it; a shot adds bloom up to the cap and it recovers; fatigue and steadiness multiply; aiming shrinks it.
- `HeldItemSway`: the wander stays inside the envelope over a long run; zero envelope gives no wander.
- Shot direction: a hip shot with zero pellet spread goes along the aim (no hidden cone).

### Docs

`WEAPONS.md` (hip spread, airborne inaccuracy, aim sway, crosshair rows), `UI.md` (Crosshair), `CODE_STRUCTURE.md` if a script is added.

## Left to the builder

The noise function, the spring constants, exact pixel sizes, and the readout layout, tuned by screenshots in the Weapons Test range.
