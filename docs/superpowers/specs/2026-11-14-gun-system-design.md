# Guns: stats, rarity, parts, ammo and the Weapons Test scene

**Status: partly built (2026-11-14).** The first step is in the game (see [Built: step 1](#built-step-1-2026-11-14)); the rest is still a proposal for the user to correct. Numbers are first guesses to tune in the test scene, not balance. Decisions the user has made are marked *(user)*; everything else is a proposal.

## Why

The user is refocusing on weapons. They want a gun system that scales and plays like Escape from Duckov's: a base gun, parts that change its stats and weight, rarities, calibres and ammo tiers (Rusty, Standard, AP…) *(user)*. Shooting is the game's Very High polish area. A **Weapons Test scene** of simple blocks lets the user pick any gun, mod it, upgrade it and try it on a shooting range *(user)*.

Decided so far *(user, 2026-11-14)*: Castle Bravo is the gun and attachment pack; ArmoryCore is not used. One part can fit many guns, and some parts fit only one gun.

## Goals, non-goals, anti-goals

- **Goals:** every gun reads as a stat sheet the player can compare; parts and ammo change those stats with clear trade-offs; the same few gun models stay interesting from day 1 to late game; adding a gun, a part or an ammo type is data, not code.
- **Non-goals (for now):** gun durability and repair costs, crafting recipes and prices, enemies' own guns, launchers and grenades, a gun skin system.
- **Anti-goals:** a part that is strictly better than another (every part trades something), damage numbers that hide how many shots a kill takes (RAIDS.md: HP is fixed per unit so "every bullet counts" stays readable), an inventory Tetris of parts.

## Constraints

- Builds on our `_Core/WeaponSystem` (WEAPONS.md): `GunDefinition` is the base gun, `GunInstance` already keeps per-copy state (rounds, jam) through drops and saves, `ItemDefinition` already has `Rarity` (grey, green, blue, purple, gold), `Value` and `Weight`. Player skills already go through `StatModifiers`.
- **Each new gun model is expensive:** a prefab variant, poses, hand targets and seven clips (equip, idle, fire, reload, empty reload, jam clearing, cycle). ROADMAP's Phase 6 puts re-keying five guns at 46–69 h, so about 9–14 h per gun. The roster size drives the cost more than anything else here.
- Castle Bravo guns are split into named parts (receiver, barrel, handguard, rail, stock, grip, iron sight, mag, bolt, trigger). Its `*_PreSet` prefabs show where attachments sit on each gun. Attachments are grouped by gun class (`Muzzles_Pistol`, `Optics_Recon`, …), about 660 of them, but few separate magazines.
- Prototype mechanics stay modular (AGENTS.md): each piece has its own installer or component and an `Enabled` flag where cheap.
- Nothing has armour today, so armour-piercing ammo needs armoured enemies to mean anything. RAIDS.md already plans armoured elites and robots.

## 1. The gun stat sheet

What the player sees on a gun's tooltip, Duckov-style. Each stat maps onto what our guns already have, or is new.

| Stat | Shown as | Today | Change |
| --- | --- | --- | --- |
| Damage | per bullet (per pellet on shotguns) | `Fire.Damage` | — |
| Fire rate | rounds per minute | `Fire.RoundsPerMinute` | — |
| Magazine | rounds | `Magazine.Size` | — |
| Reload time | seconds | `Magazine.ReloadTime` | — |
| Effective range | metres at full damage | `Fire.Falloff` start / end, `Bullet.Range` | scaled together |
| Bullet speed | m/s | `Bullet.Speed` | — |
| Recoil, vertical / horizontal | 0–100 bars | the aim kick up / sideways (hip and aim) and gun kick | a multiplier on the whole pattern |
| Aim time | seconds to full aim | `Handling.AimSpeed` | shown inverted |
| Hip spread / aim spread | degrees | none (only airborne spread and pellets) | **new, see Decision 3** |
| Move speed / aimed move speed | × | `HoldFeel.AimMoveSpeedScale` | add a hip one |
| Headshot multiplier | × | ×2 on every gun (enemy `Hitbox`) | per gun |
| Noise | metres that hear the shot | `ShotFired` has no radius; animals use their own | new: carried on `ShotFired` |
| Jam chance | % per shot | `Jam.Chance` | ammo and rarity change it |
| Penetration | from the loaded ammo | — | new (section 4) |
| Weight | kg | `ItemDefinition.Weight` | + attachments (section 5) |

**How changes stack (Duckov-style):** final = base × (1 + the sum of every % from rarity, parts and ammo), clamped to sane limits. Two −20 % aim-time parts make −40 %, not −36 %. The sheet is recalculated when a part, the rarity or the loaded ammo changes, never per frame. Player skills (`StatModifiers`) apply on top. The tooltip shows each stat's final value with green / red arrows against the base, and the test scene's readout shows the full breakdown.

## 2. Rarity and upgrading — Decision 1

Three ways to do it, all using our existing five rarities:

| | A. Fixed per model (Duckov) | **B. Per copy, upgraded at the gunsmith (recommended)** | C. Rarity = slots (Warzone) |
| --- | --- | --- | --- |
| What rarity is | Each gun model has one rarity; better guns are other models | Each copy has a rarity within its model's range; it scales the stat sheet | Each copy's rarity sets how many part slots are open |
| Upgrading | Craft better models at the workbench | Pay Tokens + gun parts at the gunsmith robot for one step up | A kit opens the next slot |
| Progression needs | Many gun models | Few models, each spanning several rarities | Few models; modding is gated |
| Cost for us | Highest (each model is 9–14 h of animation) | Low: one rarity table, one bench | Low, but locks modding behind rarity |

Why B: a gun model is our most expensive content, and B lets about a dozen models carry the whole game, the way ARC Raiders' tier I–IV upgrades keep early guns worth upgrading. It also gives the gunsmith robot (CORE.md) a clear job.

Proposed rarity table (one shared data asset, numbers to tune):

| Rarity | Damage | Recoil | Aim and reload time | Jam chance | Value |
| --- | --- | --- | --- | --- | --- |
| Common (grey) | +0 % | +0 % | +0 % | ×1.5 | ×1 |
| Uncommon (green) | +6 % | −5 % | −4 % | ×1 | ×1.6 |
| Rare (blue) | +12 % | −10 % | −8 % | ×0.7 | ×2.5 |
| Epic (purple) | +18 % | −15 % | −12 % | ×0.5 | ×4 |
| Legendary (gold) | +25 % | −20 % | −15 % | ×0.3 | ×6 |

Damage steps are chosen so an upgrade changes the shots to kill: an AK47 at 20 damage kills a 100 HP goblin in 5 body shots, and at Legendary (25) in 4. Each model has a lowest and highest rarity (a pipe-grade gun never turns gold). Loot rolls the rarity with a `WeightedTable`, clamped to the model's range; Duckov's own loot weights fall roughly 250 / 150 / 80 / 30 / 10 from quality 1 to 5.

## 3. Gun parts (attachments)

**Six slots, as in Duckov:** Optic, Muzzle, Grip (underbarrel), Stock, Tactical (laser, flashlight), Magazine. Each gun has only the slots that make sense (a double barrel has no magazine slot).

**What fits:** a part lists the gun families it fits (Pistol, SMG, Rifle, Heavy, Shotgun), or the exact guns it fits. Gun-specific parts (an AK drum, an AK handguard grip such as Castle Bravo's `ForwardGrip_AK`) list their guns; generic parts list families. Castle Bravo's folder groups map onto families directly (`Optics_Pistol` → Pistol, `Optics_Recon` → Heavy bolt rifles, `Muzzles_ShotGun` → Shotgun).

**Tiers:** most parts come in I, II and III, which are green, blue and purple. A higher tier gives a bigger bonus or a smaller drawback.

**Slots per family:**

| Family | Optic | Muzzle | Grip | Stock | Tactical | Magazine |
| --- | --- | --- | --- | --- | --- | --- |
| Pistol | pistol optics | pistol muzzles | — | — | yes | yes |
| SMG | yes | yes | yes | yes | yes | yes |
| Rifle | yes | yes | yes | yes | yes | yes |
| Heavy (DMR, battle, LMG) | yes | yes | yes (LMG: bipod) | yes | yes | yes |
| Heavy (bolt action) | recon optics | recon muzzles | — | yes | — | — |
| Shotgun (pump, auto) | yes | chokes | yes | yes | yes | auto only |
| Shotgun (break action) | — | — | — | — | yes | — |

**A first set of parts (about 25 items with tiers):**

| Slot | Part | Tiers | Fits | Effect (+ is worse where marked) | Castle Bravo source |
| --- | --- | --- | --- | --- | --- |
| Optic | Red dot | I | SMG, Rifle, Heavy, Shotgun | aim time −5 %, headshot ×+0.1 | `Optics/Optics` |
| Optic | Holo | II | same | aim spread −15 %, aim time +5 % | `Optics/Optics` |
| Optic | 2× scope | II | Rifle, Heavy | zoom 2×, range +15 %, aim time +15 % | `Optics/Optics` |
| Optic | 4× scope | III | Heavy | zoom 4×, range +25 %, aim time +25 %, hip spread +15 % | `Optics/Optics_Recon` |
| Optic | Pistol red dot | I | Pistol | aim time −8 % | `Optics/Optics_Pistol` |
| Muzzle | Compensator | I, II | SMG, Rifle, Heavy | vertical recoil −10 / −18 %, noise +10 % | `Barrels/Muzzles` |
| Muzzle | Muzzle brake | II | Rifle, Heavy | recoil −15 % both, noise +25 % | `Barrels/Muzzles` |
| Muzzle | Suppressor | II, III | Pistol, SMG, Rifle, Heavy | noise −70 %, range −10 %, bullet speed −10 % (III: −5 %), suppressed shot sound | `Barrels/Muzzles*` |
| Muzzle | Choke | I, II | Shotgun | pellet spread −20 / −35 % | `Barrels/Muzzles_ShotGun` |
| Grip | Vertical grip | I, II | SMG, Rifle, Heavy, Shotgun | vertical recoil −12 / −22 %, aim time +5 % | `UnderBarrels/ForwardGrips` |
| Grip | Angled grip | I, II | same | aim time −10 / −16 %, horizontal recoil −5 % | `UnderBarrels/ForwardGrips` |
| Grip | Bipod | II | LMG, Heavy | recoil −30 % while crouched, aim time +15 % | `UnderBarrels/Bipods` |
| Stock | Light stock | I | SMG, Rifle | aim time −10 %, move speed +3 %, recoil +5 % | `Attachments/Stocks` |
| Stock | Heavy stock | II | Rifle, Heavy, Shotgun | recoil −15 %, aim time +10 % | `Attachments/Stocks` |
| Tactical | Laser | I, II | all but bolt action | hip spread −15 / −25 % | `Accessories/Tatical Lasers` |
| Tactical | Flashlight | I | all but bolt action | a cone light while held (toggle) | `Accessories/Tatical Lights` |
| Tactical | Laser-light combo | III | same | both | lasers + lights |
| Magazine | Extended mag | I, II, III | per family | capacity +33 / +66 / +100 %, aim time +5 / +8 / +10 % | `Attachments/Mags` and per-gun `Mag_*` |
| Magazine | Quick mag | I, II | per family | reload −15 / −25 % | per-gun `Mag_*` |
| Magazine | AK drum | III | AK47 only | capacity +150 %, aim time +20 %, move speed −5 % | to source or model |

The flashlight matters more in this game than in Duckov: nights are dark and "light is safety" (IDEAS.md), so a gun light is a real choice against the laser in the same slot.

**How parts look on the gun:** every gun prefab gets a `Mounts/<Slot>` transform per slot. An editor script reads the Castle Bravo `*_PreSet` prefabs to place them, then we check each by eye. A part's prefab is spawned under its mount. An optic hides the gun's iron sight and moves the Aim pose by its own sight height. A muzzle part moves the shot origin (`Muzzle`) to its tip. A magazine part swaps the mesh under the gun's existing `Mag` part, so the reload clips keep moving it; where Castle Bravo has no mesh for a gun's extended mag, the part changes stats only until one is made.

### The modding screen

Mockups: [Gunsmith UI concepts](https://claude.ai/artifact/HF14eiWwVF2TEzTgoi4bUD), page "Inspector in 3D". **Direction (user, 2026-11-14):** Duckov's bag screen (Tab): equipment and backpack on the left, an **inspector in the centre for whatever item is selected**, loot or storage on the right. Unlike Duckov's 2D icon, the inspector shows the item **in 3D and large**, like Call of Duty's gunsmith. The earlier layouts (slot cards around the gun, a separate gunsmith screen, a compact panel) were not chosen.

- **The inspector** shows the selected item: the gun in hand when the bag opens, then any item clicked (gun, part, ammo, consumable). Its name in the rarity colour, its kind, weight and value, then a 3D stage, then what the item needs (a gun's slots and loaded ammo, ammo's "used by" list), then its stats in two columns.
- **The 3D stage** is the real prefab (a gun with its fitted parts, an ammo box, a medkit), rendered by a camera into a texture only while the bag is open, at a three-quarter angle. Drag to turn it, scroll to zoom.
- **Gun slots** sit in a row under the stage in Duckov's order: Optic, Muzzle, Grip, Stock, Tactical, Magazine. Hovering a slot (or holding a part over it) draws one leader line from it to its `Mounts/<Slot>` point on the 3D gun and shows a tooltip with the part's changes. A gun's own part (its built-in stock or magazine) shows as the empty state.
- **Fitting a part:** drag it from the backpack (or a crate) onto its slot or onto the gun. The slots it fits light up as soon as it is picked up; items that don't fit the inspected gun dim in the backpack. While it is held over a slot, the stats preview the change in green and red and the weight shows its new total. Dropping on a fitted slot swaps, and the old part goes to the backpack. Right-click a slot to take its part off.
- **Ammo** has its own bar under the slots: drag ammo onto it (or pick from its list) to switch, and the rounds left go back to the backpack.
- **Refusals** use the inventory's existing message line ("Backpack full: make room for the part you take off").

## 4. Calibres, ammo tiers and armour

**Calibres** (Duckov has S, AR, L, MAG, SNP and SHT). Proposed four, one more than today:

| Calibre | Guns | Today |
| --- | --- | --- |
| Pistol | pistols, SMGs | `PistolAmmo` |
| Rifle | assault rifles, carbines | `RifleAmmo` |
| Heavy | DMRs, battle rifles, LMGs, bolt-action rifles | new |
| Shell | shotguns | `ShotgunShells` |

Later: Magnum (revolvers, the .50 rifle) and Launcher (40 mm, rockets).

**Tiers**, Duckov's ladder: each calibre × tier is its own stackable item (for example "Rifle — AP").

| Tier | Rarity | Damage | Penetration | Jam chance | Where from (economy to tune) |
| --- | --- | --- | --- | --- | --- |
| Rusty | grey | ×0.85 | 0 | ×4 | goblin drops, cheap scrap crafting |
| Standard | green | ×1 | 1 | ×1 | the trading robot's daily stock, crates |
| AP | blue | ×1 | 3 | ×1 | robot (small stock, pricey), workbench tier 2, elite drops |
| Advanced AP | purple | ×1 | 5 | ×1 | boss drops, rare crates |

Shells use their own names: Rusty buckshot, Buckshot, **Slug** in the AP place (one heavy projectile, no spread, penetration 3), and Flechette later. Special rounds (hollow point, incendiary) come later. Rusty rounds jamming more ties ammo to our jam-and-slap mechanic (IDEAS.md already suggested cheaper ammo that jams more). In Duckov, Rusty → Standard → AP (3) → Advanced AP (5) → Special is the same ladder; AP 3 handles normal enemies but not bosses.

**Armour:** enemies (and their hit zones, such as a helmet on the head) get an armour level from 0 to 6. If the round's penetration is at least the armour level, it does full damage. Each level short takes 30 % off, down to 10 %: Standard (1) against a level 3 robot does ×0.4. A blocked hit needs its own feedback: a spark, a metal clink and a grey damage number. Proposed armour: goblins 0, armoured elite goblins 2 (helmet 3), robots 3–4, bosses 4–5. Until armoured enemies exist, AP rounds only matter on the test scene's armoured dummies.

**Which ammo a gun uses:** the magazine holds one tier at a time and remembers it. Reloading takes that tier; when it runs out, it falls back to the lowest tier carried, so the good rounds aren't spent by accident. To switch, drag ammo onto the gun in the inventory (a hold-R ammo wheel later). Switching puts the rounds still in the magazine back into the backpack, which also fixes today's "rounds can't be unloaded" gap.

## 5. Weight and value

A gun weighs its base weight plus its parts, and that counts toward carry weight (`ItemInstance.Weight` becomes overridable for this). Weight has no hidden handling effect: heavy parts state their own aim-time and move-speed costs, as in Duckov, so tooltips stay honest. A gun's value is its base value × the rarity's multiplier, plus its parts.

## 6. Gun roster

Castle Bravo has 165 guns (19 pistols, 23 SMGs, 40 assault rifles, 18 DMRs, 17 LMGs, 20 sniper rifles, 17 shotguns, 7 launchers, 4 rifles), all on one shared material, all but the launchers with a preset.

**Roster for now** *(user, 2026-11-14: 3 pistols, 3 SMGs, 6 rifles, 3 shotguns, 1 heavy machine gun; the models are proposals)*: 16 guns, the 5 we have plus 11, one clear job each and picked to read differently in first person (wood vs black, drum vs box mag). "Rifles" covers assault, marksman and bolt-action rifles. With rarity per copy (Decision 1), each one spans several rarities.

| Class | Gun | Model | Calibre | Fire | Job | Damage / rate / mag (first guess) | Rarity range | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Pistol | 1911 | P1/Pistol_E | Pistol | semi | balanced starter | 25 / 400 / 12 → 4 body shots | grey–purple | built |
| Pistol | Glock 17 | P1/Pistol_B | Pistol | semi | quick to aim, big magazine | 20 / 500 / 17 → 5 | grey–blue | new |
| Pistol | Desert Eagle | P1/Pistol_D | Magnum | semi | hand cannon, big kick | 55 / 180 / 7 → 2 (head 1) | blue–gold | new |
| SMG | MP5 | P1/SMG_B | Pistol | auto | accurate, good from the hip | 15 / 800 / 30 → 7 | green–purple | built |
| SMG | Uzi | P3/SMG_M | Pistol | auto | cheap close-range spray | 14 / 950 / 32 → 8 | grey–blue | new |
| SMG | P90 | P3/SMG_O | Pistol | auto | 50-round magazine, low recoil | 13 / 900 / 50 → 8 | blue–purple | new |
| Rifle | M1 Carbine | P5/WWII_Rifle_A | Rifle | semi | light early rifle, the farm gun | 26 / 450 / 15 → 4 | grey–blue | new |
| Rifle | AK47 | P1/AR_B | Rifle | auto | hits hard, kicks hard | 20 / 600 / 30 → 5 | green–gold | built |
| Rifle | M4 | P1/AR_A | Rifle | auto | low recoil, takes every part | 18 / 750 / 30 → 6 | blue–gold | new |
| Rifle | AUG | P3/AR_O | Rifle | auto | built-in scope, so no optic slot | 19 / 700 / 30 → 6 | blue–gold | new |
| Rifle | FAL | P3/DMR_L | Heavy | semi | mid-range marksman | 38 / 400 / 20 → 3 (head 2) | blue–gold | new |
| Rifle | Hunting rifle | P1/Recon_E (Remington 700, scoped) | Heavy | bolt | one-shot heads, slow | 70 / bolt / 5 → 2 (head 1) | green–purple | new |
| Shotgun | Double barrel | P1/ShotGun_E | Shell | break | starter shotgun | 12 × 9 pellets / 2 | grey–blue | built |
| Shotgun | Pump | P1/ShotGun_C | Shell | pump | the reliable shotgun | 12 × 8 / 6 | grey–purple | built |
| Shotgun | AA-12 | P1/ShotGun_A | Shell | auto | late-game crowd clearer | 8 × 8 / 300 rpm / 8 | purple–gold | new |
| Machine gun | RPD | P1/LMG_A (wood, 100-round drum) | Rifle | auto | raid defence; drains rifle ammo | 22 / 650 / 100 → 5 | purple–gold | new |

Body shots are against a 100 HP goblin at Common, before parts; a Legendary copy's +25 % damage takes one shot off most of them.

- **The Desert Eagle brings the Magnum calibre forward** (section 4 listed it for later), making five calibres. Staying at four: the TEC-9 (P1/Pistol_A, an automatic pistol on Pistol ammo) instead.
- **No true heavy machine gun** (an M2 .50) is in the pack; the RPD is the closest. For one on Heavy ammo instead of sharing rifle ammo: the PKM (P3/LMG_L).
- **New guns start as block-outs** (prefab, poses and stats, about 1–2 h each; a gun without clips already poses, kicks and shoots). Full clips at about 9–14 h each come only for the guns that stay.

**Calibres for this roster:** Pistol (1911, Glock, MP5, Uzi, P90), Rifle (M1 Carbine, AK47, M4, AUG, RPD), Heavy (FAL, Hunting rifle), Shell (Double barrel, Pump, AA-12), Magnum (Desert Eagle). Tiers for now: Rusty, Standard, AP (shells: Rusty buckshot, Buckshot, Slug); Magnum only Standard and AP; Advanced AP once armoured bosses exist. 14 ammo items.

**Slots per gun:**

| Gun | Optic | Muzzle | Grip | Stock | Tactical | Magazine |
| --- | --- | --- | --- | --- | --- | --- |
| 1911, Glock, Desert Eagle | pistol | pistol | — | — | ✓ | ✓ |
| MP5 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Uzi | ✓ | ✓ | — | ✓ | ✓ | ✓ |
| P90 | ✓ | ✓ | — | — | ✓ | ✓ |
| M1 Carbine | ✓ | ✓ | — | — | — | ✓ |
| AK47, M4, FAL | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| AUG | — (built-in scope) | ✓ | ✓ | — | ✓ | ✓ |
| Hunting rifle | recon | recon | — | ✓ | — | — |
| Pump | ✓ | choke | ✓ | ✓ | ✓ | — |
| AA-12 | ✓ | choke | ✓ | — | ✓ | ✓ |
| Double barrel | — | — | — | — | ✓ | — |
| RPD | ✓ | ✓ | bipod | — | — | ✓ |

**Parts for this roster (about 30 items with tiers):** shared by any gun with the slot: red dot I, holo II, laser I–II, flashlight I, combo III, vertical grip I–II, angled grip I–II. Rifle and Heavy: 2× scope II, compensator I–II, muzzle brake II, light stock I, heavy stock II; Heavy and RPD: 4× scope III, bipod II. Pistol calibre: pistol red dot I, pistol compensator I, pistol suppressor II (pistols and SMGs). Shotguns: choke I–II. Per calibre: suppressors (Rifle II–III, Heavy II), extended mags (Pistol I–II, Rifle I–III, Heavy I–II, AA-12 box I), quick mags (Pistol and Rifle I–II). Gun-only: AK drum III, AK handguard grip, Glock 33-round stick mag, Desert Eagle comp.

**For later updates**: a revolver (P2/Pistol_G) and the Barrett M82 (P1/Recon_A) on Magnum; Vector (P2/SMG_F), Thompson (P5/WWII_SMG_B); SVD (P2/DMR_G); PKM (P3/LMG_L); a lever-action (P5/Rifle_A); sawn-off (P5/CW_ShotGun_B) and SPAS-12 (P2/ShotGun_F); RPG-7 (P3/Launcher_A) and M320 (P3/Launcher_E) on Launcher.

**A cheaper way to grow the roster, to test first:** Castle Bravo has 15 "`_2`" variants with the same layout as a base gun (AK-74M `AR_B_2`, M16 `AR_A_2`, MP5K `SMG_B_2`, SCAR-H `AR_C_2`). If our clips transfer to a variant with small fixes, these cost far less than 9–14 h each.

Thin in the pack: launchers (7, no presets), WWII (6 guns, no Kar98k or Mosin), revolvers (2), break-action shotguns (1). Too many near-copies of the AK (about 9) and the AR-10 (about 7), so later picks should come from the distinctive ones listed above.

## 7. Weapons Test scene

`Assets/_Game/Scenes/Prototypes/WeaponsTest.unity`: simple blocks with a first-party grid material, the real Player and HUD prefabs, no save (no `WorldSaveInstaller`). Only the installers it needs: `EnemyInstaller` and the small-grid `Navigation` prefab for the arena.

```text
┌──────────────────── Range ─────────────────────┐
│ 10 m   25 m   50 m   75 m   100 m   (boards)    │
│ dummies at armour 0 / 2 / 4 / 6, a moving rail  │
│ lane, steel plates                              │
├──────────────┬──────────────┬──────────────────┤
│ Armoury      │ Workbench    │ Arena            │
│ a rack per   │ parts shelf, │ spawn N goblins  │
│ gun, rarity  │ upgrade      │ (button)         │
│ selector     │ bench        │                  │
│ ammo wall    │              │                  │
└──────────────┴──────────────┴──────────────────┘
```

- **Guns:** the 16 of the roster (section 6), the 11 new ones as block-outs. Optional experiment: the **AK-74M** (P1/AR_B_2) with the AK47's clips (it could replace the AUG), to learn whether same-layout variants are cheap guns.
- **Armoury:** one rack slot per gun model; a rarity selector sets the rarity of the copies the racks give. An ammo wall with one crate per calibre × tier.
- **Workbench:** a shelf with every part as a pickup. Modding happens in the inventory screen (the real UI, section 8). An upgrade bench raises the held gun one rarity step, free here (it stands in for the gunsmith).
- **Range:** distance boards, armoured dummies (`TargetDummy` with an armour level), a moving target, steel plates that ring.
- **Arena:** a button spawns a wave of goblins to check shots-to-kill on real enemies.
- **Readout (dev only):** the held gun's final stat sheet with its breakdown (base, rarity, each part, ammo), live DPS, and shots to kill a 100 HP target at each armour level with the loaded ammo.

## 8. How it gets built

- `GunStat` (enum) and `GunStats` (plain C#): the final sheet plus its breakdown, rebuilt from base + rarity + parts + ammo. `Gun` and `HeldGun` read it instead of the asset's fields. EditMode-tested like the other gun rules.
- `GunDefinition` gains: family, calibre, its slots, base values for the new stats, lowest and highest rarity.
- `AttachmentDefinition : ItemDefinition` (a new `Attachment` category): slot, families or exact guns it fits, modifiers, a model prefab, optional optic (zoom, sight height), muzzle (tip, suppressed sound) and light data.
- `AmmoType` gains calibre, tier, damage multiplier, penetration and jam multiplier.
- `GunInstance` gains its rarity, its installed parts and its loaded ammo tier, all saved. `ItemInstance` weight, value and rarity become overridable, so the slot border shows the copy's rarity.
- Armour: `Hitbox` (or the enemy's stats) gains an armour level and `DamageInfo` a penetration; `DamageService` applies the rule and `DamageDealt` reports a blocked hit.
- Inventory screen: a centre inspector for the selected item, with a 3D stage (a camera rendering the real prefab into a texture while the bag is open), a gun's slot row with one leader line to the hovered slot's mount, an ammo bar and two-column stats with green / red previews; parts and ammo are dragged onto it ([The modding screen](#the-modding-screen)). The inspector is shared by every item, so it is built once for the inventory, not for guns only.
- Upgrade bench: an interactable plus a cost table asset (Tokens + gun parts).
- `AgentScripts/Weapons/MountSetup.cs`: places `Mounts/*` on our gun prefabs from the Castle Bravo presets.
- New sounds (suppressed shots, armour clink) are BroAudio entities, which need the user's OK before they're added (AGENTS.md).

## 9. Build order and rough cost

| Step | What | Rough hours |
| --- | --- | --- |
| 0 | Weapons Test scene with the 5 current guns, range, dummies, readout | 6–10 |
| 1 | The stat layer (`GunStats`), no change in how guns play | 8–12 |
| 2 | Calibres, ammo tiers, armour and penetration, choosing and unloading ammo | 14–22 |
| 3 | Rarity per copy and the upgrade bench | 8–12 |
| 4 | Parts: data, mounts, visuals, optic aim, suppressor, mod UI | 30–45 |
| 5 | Hip and aim spread with a reticle (if chosen) | 5–8 |
| 6 | Data: about 25 parts, 16 ammo items, icons | 8–12 |
| 7 | The 11 new guns as block-outs (prefab, poses, world model, stats) | 11–22 |
| 8 | Clips and hands for the guns that stay, about 9–14 h each | 100–150 if all 11 stay |

Steps 0–7 are about 90–140 h. The clips can share the Phase 6 animation rework instead of adding to it.

## Decisions for the user

1. **Rarity and upgrading:** A, B or C in section 2 (recommended B). What should "upgrading" in the test scene mean?
2. **When:** before the vertical slice, in it, or after. (The roster for now is settled at 16: section 6.)
3. ~~**Hip spread**~~ **Decided** *(user, 2026-11-14)*: Duckov-style hip spread, shrinking to an aim spread of 0 as the sights come up; the crosshair's ticks show it, and the laser shrinks it (−25 %). Built (WEAPONS.md → Hip spread).

## Calls made in this draft (for the user to check)

Four calibres; four ammo tiers with specials later; the armour rule; Duckov's six slots; additive percentages; weight counts toward carry only; no durability for now (jams stay our wear mechanic, durability is [TBD]); the lowest-tier fallback on reload; the scene's name and no saving; a first-party grid material for the blocks.

## Built: step 1 (2026-11-14)

The user asked for *(user, 2026-11-14)*: a Weapons Test scene where guns and gun parts lie on tables, are picked up and fitted to a gun; working scopes; preparation for the coming gun animation rework; built to carry into real gameplay; and the guns' first stats like real life, from the AK47 as tuned by hand. Plan: [2026-11-14-weapons-test-parts-scopes.md](../plans/2026-11-14-weapons-test-parts-scopes.md). What exists, with the details in WEAPONS.md → Gun parts and optics:

- **Parts** as items (`AttachmentDefinition`, category Attachment): 16 of them for the five built guns (red dot, holo, 2× and 4× scopes, pistol red dot, compensator, two suppressors, choke, vertical and angled grips, laser, two lights, two extended magazines). Fit by slot plus family or exact gun, magazines locked to the ammo type (section 3).
- **The stat sheet** (`GunStats`, section 1): additive percentages, clamped −90 … +300 %; the rules read it. Weight and value include parts (section 5).
- **Mounts** on both gun models from the Castle Bravo presets, under the part each rides on; optics aim through their own sight line with a zoom from their magnification; full scopes (the 4×) show a scope view and hide the held model; suppressors hide the flash; lights work in hand.
- **The modding screen** as chosen (section 3 → The modding screen): the inventory's centre inspector with the item in 3D, the slot row, the leader line, green / red previews. No ammo bar yet (no ammo tiers).
- **Weapons Test scene** (section 7), smaller than proposed: the five built guns and the 16 parts on tables, a backpack and the ammo crate, dummies at 10–100 m and a backstop. No armoury racks, rarity selector, ammo wall, workbench, armoured dummies or arena yet.
- **Real-life first stats** for the other four guns from the AK (WEAPONS.md → First-pass real-life stats).

Follow-up the same day *(user, 2026-11-14: "do 1, 2, 3, 4, 5")*: the inventory's fitting checked with real UI pointer events; hip spread (Decision 3) with the laser tightening it; brighter part icons with an outline; a gun light key (`GunLight`, F / d-pad up); scopes calm the aim sway while aimed through (2× ×0.7, 4× ×0.35).

Calls made while building (for the user to check): the 4× scope also fits rifles, so the AK can try it (section 3 had it on Heavy only); guns have no Stock slot until stock parts exist; magazine parts scale the gun's own magazine instead of new models; the support hand stays on the forend with a grip fitted (the grip's hand point is there for the animation rework); rarity, calibres beyond today's three ammo types and armour are untouched, since Decisions 1–2 are still open.

## References

- Escape from Duckov (fan wikis, data-mined pages; may differ by patch): gun stat sheet fields, six slots, tiered parts with weight and % modifiers, calibre-locked magazines, ammo ladder Rusty → Advanced AP with penetration 1 / 3 / 5, quality loot weights.
- ARC Raiders: weapons upgrade from tier I to IV at the Gunsmith (handling, magazine, reload, mod slots; not raw damage), and the bench's own level caps the tier.
- Call of Duty Warzone (Black Ops Royale): ground-loot rarity sets how many preset attachments a gun has; an Attachment Kit upgrades it one step.
