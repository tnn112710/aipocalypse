# Castle Bravo guns: which are easy or hard to animate

Every gun prefab in the Castle Bravo Low Poly Weapon Bundle (Packs 1, 2, 3 and 5: 165 prefabs), rated by how hard its reload will be to produce with the retarget pipeline in [HANDS.md](HANDS.md#retargeted-reloads). Use it when picking guns for the roster: take the Easy ones, think twice about Medium, and leave Hard and Avoid out unless the gun is worth a new animation mode.

Made 2026-11-16 from a side render of each prefab plus its part names and positions. The pack names its guns `AR_A`, `SMG_B` and so on, so the real gun in "Looks like" is a best guess (`?` = unsure); the rating follows what is visible (where the magazine sits, what kind of action), so a wrong name rarely changes it. The guns where it could are listed under [Unsure](#unsure-about-the-mechanism-from-the-picture). Only the AK47 has been baked so far: every other rating is a forecast.

Pictures of every gun with its prefab name: [AR 1](castle-bravo-guns/sheet_01.png), [AR 2](castle-bravo-guns/sheet_02.png), [AR, rifles, DMR](castle-bravo-guns/sheet_03.png), [DMR, recon](castle-bravo-guns/sheet_04.png), [recon, SMG](castle-bravo-guns/sheet_05.png), [SMG, pistols](castle-bravo-guns/sheet_06.png), [pistols, shotguns](castle-bravo-guns/sheet_07.png), [shotguns, LMG](castle-bravo-guns/sheet_08.png), [LMG, launchers](castle-bravo-guns/sheet_09.png).

## What the ratings mean

The pipeline copies a reload from the KINEMATION FPS Animation Pack: the left hand's path, fingers, the magazine and the bolt. The right hand stays on the pistol grip. So a gun is cheap when the pack has a reload for the same kind of gun and that reload is a magazine swap by the left hand.

The pack's reloads ("Closest pack source" in the table): AK, ASVal, DGL50 (Desert Eagle), Drake-12 (magazine-fed shotgun), G3, KXG12 (pump shotgun, shell by shell), Kar98K (bolt action, loose rounds), Kolibri (tiny pistol), L96X (bolt-action sniper, box magazine), M1911, MGX5 (belt-fed machine gun), MPS5 (MP5), MX16A4 (M16 / AR-15), Mk14EBR (M14), PDW90 (P90), RPG (RPG-7), SVD, Striker-V (Kriss Vector), Viper-357 (revolver, empty reload only), X18 (Glock).

| Rating | Means | Typical guns |
| --- | --- | --- |
| Easy | A close source exists and the reload is a plain magazine swap | Box magazine ahead of the trigger (AR-15, AK, G3, M14, SVD, MP5-style SMGs), slide pistols |
| Medium | A source exists but something needs extra work | Drum magazines, magazine-fed shotguns, magazine inside the pistol grip of an SMG (Uzi, MP7), P90, very large rifles, stocks without a pistol grip |
| Hard | Needs a new mode in the tool or parts our prefabs don't have | Bolt actions, pump / tube / lever loading, belt-fed guns, revolvers, bullpups, break actions, clips |
| Avoid | Nothing comparable to start from | Launchers other than the RPG-7, exotic feeds |

## Guns to leave out

- **Bolt action (the right hand leaves the grip to work the bolt):** Pack1 `Recon_B` (Barrett MRAD); Pack1 `Recon_C` (CheyTac M200 Intervention); Pack1 `Recon_D` (AI AX50); Pack1 `Recon_E, Recon_E_2` (Remington 700); Pack2 `Recon_F` (Barrett M95); Pack2 `Recon_H` (Bolt-action sniper with thumbhole stock (Steyr SSG 08-like)); Pack2 `Recon_I` (AI AWM); Pack2 `Recon_J` (DSR-1); Pack3 `Recon_K` (Desert Tech SRS); Pack3 `Recon_M` (Steyr Scout); Pack3 `Recon_O` (Sako TRG); Pack5 `CW_Recon_A` (PGM Ultima Ratio); Pack5 `CW_Recon_B` (ArmaLite AR-50); Pack5 `Recon_R` (Desert Tech HTI); Pack5 `WWII_Recon_B` (Springfield M1903A4).
- **Bullpup (magazine behind the grip; no matching source):** Pack1 `AR_E` (FAMAS); Pack1 `AR_F` (QBZ-95); Pack2 `AR_H` (Desert Tech MDR); Pack3 `AR_O` (Steyr AUG); Pack3 `AR_P` (OTs-14 Groza); Pack1 `DMR_E` (Bullpup DMR (Kel-Tec RFB-like)); Pack2 `SMG_I` (Bullpup SMG (Magpul PDR-like)); Pack1 `LMG_D` (QBB-95); Pack2 `LMG_G` (L86 LSW with drum); Pack3 `Launcher_D` (XM25 airburst launcher).
- **Belt fed (top cover, belt, box):** Pack1 `LMG_A` (RPD); Pack1 `LMG_B` (M249 SAW with belt box); Pack1 `LMG_B_2` (M249 Para fed from a rifle mag); Pack1 `LMG_E` (Belt-fed AR upper with ammo box in the mag well (Ares Shrike-like)); Pack2 `LMG_F` (FN EVOLYS-like); Pack2 `LMG_H` (IWI Negev); Pack2 `LMG_I` (HK21E); Pack3 `LMG_K` (SIG MG 338); Pack3 `LMG_M` (KAC Stoner LMG); Pack3 `LMG_O` (PKP Pecheneg).
- **Loaded shell by shell or round by round (pump, tube, lever):** Pack5 `Rifle_A` (Marlin 1895-style tactical lever action); Pack1 `ShotGun_B` (Remington 870); Pack1 `ShotGun_C` (Benelli M4 (stock collapsed)); Pack2 `ShotGun_F` (Franchi SPAS-12); Pack3 `ShotGun_K` (Benelli M4); Pack3 `ShotGun_N` (Benelli M3-like with folding wire stock); Pack5 `CW_ShotGun_B` (Sawn-off pump (Ithaca 37 Stakeout-like), taped grip, no stock); Pack5 `ShotGun_P` (Mossberg 590).
- **Revolver:** Pack2 `Pistol_G` (Revolver with a full barrel shroud (Chiappa Rhino-like)); Pack5 `Pistol_R` (Large revolver with a railed shroud).
- **Break action, clips and other odd feeds:** Pack5 `WWII_Rifle_B` (M1 Garand); Pack1 `SMG_E` (PP-19 Bizon); Pack1 `ShotGun_E` (Over/under double barrel).
- **Avoid outright (nothing comparable to start from):** Pack5 `CW_AR_G` (HK G11); Pack3 `ShotGun_O` (Pancor Jackhammer); Pack3 `LMG_L` (KAC ChainSAW); Pack3 `Launcher_B` (SMAW); Pack3 `Launcher_C` (FIM-92 Stinger); Pack3 `Launcher_E` (Standalone single-shot 40 mm launcher (M320)).

Whole categories to be careful with: **Recon** (16 of 20 are bolt actions), **ShotGun** (none Easy: tube-fed ones are Hard, magazine-fed ones Medium), **LMG** (none Easy: 12 of 17 belt-fed or bullpup), **Launcher** (only the RPG-7 has a source).

## Guns in the game

| Gun | Prefab | Rating | State |
| --- | --- | --- | --- |
| AK47 | Pack1 `AR_B` | Easy | Reloads retargeted, repair clips generated |
| AK-74M | Pack1 `AR_B_2` | Easy | Variant of the AK47: inherits its clips |
| MP5 | Pack1 `SMG_B` | Easy | Reloads retargeted, repair clips generated |
| Pistol (.45) | Pack1 `Pistol_E` | Easy | Reloads retargeted, repair clips generated |
| Glock 17 | Pack1 `Pistol_B` | Easy | Animated (reloads retargeted, repair clips); the model has no separate slide, so it does not move |
| Desert Eagle | Pack1 `Pistol_D` | Easy | Animated (reloads retargeted, repair clips) |
| M4 | Pack1 `AR_A` | Easy | Animated (reloads retargeted, repair clips) |
| SCAR | Pack1 `AR_C` | Easy | Added 2026-11-16; animated (reloads retargeted, repair clips) |
| Pump shotgun | Pack1 `ShotGun_C` | Hard | Hand-keyed clips (shell by shell) that work with the arms |
| Double barrel | Pack1 `ShotGun_E` | Hard | Hand-keyed clips that work with the arms; no pack source |
| RPD | Pack1 `LMG_A` | Hard | Block-out; belt fed, and the model has no cover or belt parts |

Parked on 2026-11-16 (user): assets deleted, builder rows kept ([WEAPONS.md](WEAPONS.md#block-out-guns)).

| Gun | Prefab | Rating |
| --- | --- | --- |
| FAL | Pack3 `DMR_L` | Easy |
| Uzi | Pack3 `SMG_M` | Medium (magazine in the grip) |
| M1 Carbine | Pack5 `WWII_Rifle_A` | Medium (no pistol grip, small gun) |
| AA-12 | Pack1 `ShotGun_A` | Medium (magazine-fed shotgun; Drake-12 is the source) |
| Hunting rifle | Pack1 `Recon_E` | Hard (bolt action) |

## Every gun

| Pack | Prefab | Looks like | Feed and action | Closest pack source | Rating | Why |
| --- | --- | --- | --- | --- | --- | --- |
| Pack1 | AR_A, AR_A_2 | M4A1 with carry handle; A_2 is an M16A2-style rifle with a 20-round mag | Box mag ahead of trigger, rear T charging handle | MX16A4 | Easy | Same gun family as the source |
| Pack1 | AR_B, AR_B_2 | AKM / AK-74 (wood); B_2 is a black AK-74M | Curved rock-in box mag ahead of trigger, right-side bolt handle | AK | Easy | Same gun as the source |
| Pack1 | AR_C, AR_C_2, AR_C_2_Black, AR_C_Black | FN SCAR-L (AR_C) and SCAR-H (AR_C_2) | Box mag ahead of trigger, side charging handle | MX16A4 (mag), G3 (side handle) | Easy | Plain mag swap, AR-style mag well |
| Pack1 | AR_D | HK G36C / G36K | Box mag ahead of trigger, charging handle on top under the carry rail | MX16A4 or G3 | Easy | Plain mag swap; only the top handle differs |
| Pack1 | AR_E | FAMAS | Bullpup, mag behind grip | none | Hard | Bullpup |
| Pack1 | AR_F | QBZ-95 | Bullpup, curved mag behind grip | none | Hard | Bullpup |
| Pack2 | AR_G, AR_G_2 | CZ Bren 2 / SIG MCX-like? | Box mag ahead of trigger, rear T handle plus a side handle | MX16A4 | Easy | AR layout |
| Pack2 | AR_H | Desert Tech MDR? | Bullpup, mag behind grip | none | Hard | Bullpup |
| Pack2 | AR_I, AR_I_2 | AR-15 with M-LOK rail (Mk18 / URG-I) | Box mag ahead of trigger, rear T charging handle | MX16A4 | Easy | Same gun family as the source |
| Pack2 | AR_J | AK-12 / modernised AK | Curved box mag ahead of trigger, right-side bolt handle | AK | Easy | Same gun family as the source |
| Pack2 | AR_K, AR_K_2 | CZ 805 Bren / Galil ACE with AK mag? | Curved AK mag ahead of trigger, small side handle | AK | Easy | AK-style mag swap |
| Pack2 | AR_S | HK416 / SIG MCX-like with suppressor? | Box mag ahead of trigger, side handle (both sides) | MX16A4 | Easy | AR layout |
| Pack3 | AR_L | SIG SG 550 (fixed stock)? | Box mag ahead of trigger, side bolt handle | AK or MX16A4 | Easy | Plain mag swap |
| Pack3 | AR_M, AR_M_2 | HK416; M_2 is the short HK416C | Box mag ahead of trigger, rear T charging handle | MX16A4 | Easy | Same gun family as the source |
| Pack3 | AR_N | HK433 / APC556-like? | Box mag ahead of trigger, top-side handle on both sides | MX16A4 or G3 | Easy | Plain mag swap |
| Pack3 | AR_O | Steyr AUG | Bullpup, mag behind grip | none | Hard | Bullpup |
| Pack3 | AR_P | OTs-14 Groza? | Bullpup, AK mag behind grip, carry handle | none | Hard | Bullpup |
| Pack3 | AR_Q, AR_Q_2 | HK433 / Bren 2-like? | Box mag ahead of trigger, charging handle far forward | MX16A4 (mag), G3 (forward handle) | Easy | Plain mag swap |
| Pack3 | AR_R | AS Val with 30-round mag | Curved box mag ahead of trigger, right-side bolt handle, integral suppressor | ASVal | Easy | Same gun as the source |
| Pack5 | AR_AA | Magpul Masada / Remington ACR | Box mag ahead of trigger, forward side handle | MX16A4, G3 | Easy | Plain mag swap |
| Pack5 | AR_AB | AR-15 (billet receiver, white) | Box mag ahead of trigger, rear T charging handle | MX16A4 | Easy | Same gun family as the source |
| Pack5 | AR_AC | Short tactical AK (AK-104-like) with railed top | Curved box mag ahead of trigger, right-side bolt handle | AK | Easy | Same gun family as the source |
| Pack5 | AR_W | AR-15 with long M-LOK rail | Box mag ahead of trigger, rear T charging handle | MX16A4 | Easy | Same gun family as the source |
| Pack5 | AR_X | AN-94? | Curved AK mag ahead of trigger (the mag looks canted sideways), right-side bolt handle | AK | Easy | AK mag swap; canted mag may need a rotated mag path |
| Pack5 | AR_Y | AR-15 in 7.62x39 with AK-pattern mag (CMMG Mk47-like) | Curved mag ahead of trigger, rear T charging handle | MX16A4 or AK | Easy | Plain mag swap |
| Pack5 | AR_Z | AR-15 / HK416-type with full-length rail | Box mag ahead of trigger, rear T charging handle | MX16A4 | Easy | Same gun family as the source |
| Pack5 | CW_AR_D | HK G3 / HK33 | Box mag ahead of trigger, forward cocking handle on the left | G3 | Easy | Same gun as the source |
| Pack5 | CW_AR_E | Galil ARM (bipod, folding stock) | Curved box mag ahead of trigger, bolt handle bent upward | AK | Easy | AK family |
| Pack5 | CW_AR_F | HK XM8 | Box mag ahead of trigger, top-rear charging handle | MX16A4 or G3 | Easy | Plain mag swap |
| Pack5 | CW_AR_G | HK G11 | Caseless; long horizontal mags slide in along the top front; rotary cocking handle at the rear side | none | Avoid | Exotic feed, nothing comparable |
| Pack5 | Rifle_A | Marlin 1895-style tactical lever action | Tube under barrel, lever | none (KXG12 shell loop is the nearest idea) | Hard | Lever action and round-by-round loading |
| Pack5 | Rifle_B | AR-10 in a sporting stock without pistol grip (FightLite SCR-like)? | Short box mag ahead of trigger, rear T charging handle | MX16A4 | Medium | Mag swap is plain, but the stock changes the hold |
| Pack5 | WWII_Rifle_A | M1 Carbine | Small box mag ahead of trigger, right-side operating handle, traditional stock | Mk14EBR | Medium | No pistol grip and a much smaller gun than the source |
| Pack5 | WWII_Rifle_B | M1 Garand | En-bloc clip pushed in from the top, no detachable mag | none | Hard | En-bloc clip |
| Pack1 | DMR_A | FN SCAR-H SSR / Mk 20 | 20-round box mag ahead of trigger, side charging handle | MX16A4, G3 | Easy | Plain mag swap |
| Pack1 | DMR_B | SR-25 / M110 | Box mag ahead of trigger, rear T charging handle | MX16A4 | Easy | AR layout |
| Pack1 | DMR_C | M14 EBR (Mk 14) | Box mag ahead of trigger, right-side operating handle | Mk14EBR | Easy | Same gun as the source |
| Pack1 | DMR_D | SKS in a tactical stock with a detachable AK mag? | Curved box mag ahead of trigger, right-side bolt handle | AK | Easy | AK-style mag swap (if the mag really detaches) |
| Pack1 | DMR_E | Bullpup DMR (Kel-Tec RFB-like)? | Bullpup, mag behind grip | none | Hard | Bullpup |
| Pack2 | DMR_F | HK SL8 / G36 sniper with thumbhole stock | Box mag ahead of trigger, top charging handle | MX16A4 or G3 | Medium | Thumbhole stock changes the hold |
| Pack2 | DMR_G | SVD Dragunov | Box mag ahead of trigger, right-side bolt handle | SVD | Easy | Same gun as the source |
| Pack2 | DMR_H, DMR_H_2 | HK417 (H short mag, H_2 long mag) | Box mag ahead of trigger, rear T charging handle | MX16A4 | Easy | AR layout |
| Pack2 | DMR_I | HK PSG1 / MSG90 | Box mag ahead of trigger, forward cocking handle on the left | G3 | Easy | G3 family |
| Pack2 | DMR_J | Long-barrel version of AR_K (Bren / Galil ACE-like)? | Box mag ahead of trigger, small side handle | AK or MX16A4 | Easy | Plain mag swap |
| Pack3 | DMR_K | Long-barrel version of AR_G (Bren 2 BR / MCX-like)? | Box mag ahead of trigger, rear T handle plus side handle | MX16A4 | Easy | AR layout |
| Pack3 | DMR_L | FN FAL | Box mag ahead of trigger, left-side charging handle | G3 or Mk14EBR | Easy | Plain rock-in mag swap |
| Pack3 | DMR_M | SIG SG 550 / Stgw 90 with skeleton stock | Curved box mag ahead of trigger, side bolt handle | AK | Easy | Plain mag swap |
| Pack3 | DMR_N | AR-10 type with side charging (SIG 716-like)? | Box mag ahead of trigger, side handle | MX16A4 | Easy | AR layout |
| Pack3 | DMR_O | SIG 556 / Galil ACE-like 7.62 rifle? | Box mag ahead of trigger, handle on both sides | AK or MX16A4 | Easy | Plain mag swap |
| Pack5 | CW_DMR_B | Mk 12 SPR | Box mag ahead of trigger, rear T charging handle | MX16A4 | Easy | Same gun family as the source |
| Pack5 | DMR_Q | 7.62 battle rifle (Bren 2 BR / SCAR-H-like)? | Thick box mag ahead of trigger, side handle | MX16A4, G3 | Easy | Plain mag swap |
| Pack1 | Recon_A | Barrett M82A1 / M107 | Semi-auto, large box mag ahead of trigger, right-side charging handle | SVD or Mk14EBR | Medium | Much longer and heavier than any source, big mag |
| Pack1 | Recon_B | Barrett MRAD | Bolt action, box mag ahead of trigger | L96X | Hard | Bolt action |
| Pack1 | Recon_C | CheyTac M200 Intervention | Bolt action, box mag ahead of trigger | L96X | Hard | Bolt action |
| Pack1 | Recon_D | AI AX50 / Barrett M98B? | Bolt action (handle sticks out of the receiver), box mag ahead of trigger | L96X | Hard | Bolt action |
| Pack1 | Recon_E, Recon_E_2 | Remington 700 / M40A5; E_2 is the same action in a chassis with a longer mag | Bolt action; E has a flush mag, E_2 a box mag | L96X (E_2), Kar98K (E) | Hard | Bolt action |
| Pack2 | Recon_F | Barrett M95 | Bullpup bolt action, mag behind grip | none | Hard | Bolt action and bullpup |
| Pack2 | Recon_G | Semi-auto AR-10 type precision rifle (M110 / SIG 716 DMR-like)? | Box mag ahead of trigger, side charging handle, no bolt handle | MX16A4 | Easy | Semi-auto with a plain mag swap |
| Pack2 | Recon_H | Bolt-action sniper with thumbhole stock (Steyr SSG 08-like)? | Bolt action, small box mag | L96X | Hard | Bolt action |
| Pack2 | Recon_I | AI AWM / L96 | Bolt action, box mag | L96X | Hard | Bolt action (exact source exists, but needs the right-hand-off-grip mode) |
| Pack2 | Recon_J | DSR-1 | Bullpup bolt action, mag behind grip, spare mag held ahead of the trigger | none | Hard | Bolt action and bullpup |
| Pack3 | Recon_K | Desert Tech SRS | Bullpup bolt action, mag behind grip | none | Hard | Bolt action and bullpup |
| Pack3 | Recon_L | AI AS50-like semi-auto .50? | Large box mag ahead of trigger, no bolt handle visible | SVD or Mk14EBR | Medium | Assumed semi-auto; very large gun and mag |
| Pack3 | Recon_M | Steyr Scout | Bolt action, small flush mag | L96X / Kar98K | Hard | Bolt action |
| Pack3 | Recon_N | Semi-auto anti-materiel rifle (OSV-96 / M82-like)? | Large box mag ahead of trigger, no bolt handle visible, carry handle | SVD or Mk14EBR | Medium | Assumed semi-auto; very large gun and mag |
| Pack3 | Recon_O | Sako TRG / AI AX-like chassis rifle | Bolt action, box mag | L96X | Hard | Bolt action |
| Pack5 | CW_Recon_A | PGM Ultima Ratio / FR F2? | Bolt action, box mag ahead of trigger | L96X | Hard | Bolt action |
| Pack5 | CW_Recon_B | ArmaLite AR-50 | Single-shot bolt action, no magazine part | Kar98K (single round) | Hard | Bolt action, one round by hand |
| Pack5 | Recon_R | Desert Tech HTI? | Bullpup bolt action, mag behind grip | none | Hard | Bolt action and bullpup |
| Pack5 | WWII_Recon_B | Springfield M1903A4 / scoped Kar98k | Bolt action, internal mag loaded round by round | Kar98K | Hard | Bolt action, loose rounds |
| Pack1 | SMG_A | HK MP7 | Mag inside the pistol grip, rear T handle | X18 / M1911 mag motion, or Striker-V | Medium | Pistol-style mag on a two-handed gun |
| Pack1 | SMG_B, SMG_B_2 | HK MP5 (fixed stock); B_2 is the MP5K with foregrip | Curved mag ahead of trigger, forward cocking handle on the left | MPS5 | Easy | Same gun as the source (B_2 is shorter) |
| Pack1 | SMG_C | HK UMP45 | Straight mag ahead of trigger, forward cocking handle on the left | MPS5 | Easy | MP5-style layout |
| Pack1 | SMG_D | MAC-10 / MAC-11 | Mag inside the pistol grip, cocking knob on top | X18 / M1911 mag motion | Medium | Mag in grip, top knob |
| Pack1 | SMG_E | PP-19 Bizon | Helical tube mag under the barrel | none | Hard | Helical magazine |
| Pack2 | SMG_F, SMG_F_Sand | Kriss Vector | Stick mag ahead of trigger | Striker-V | Easy | Same gun as the source |
| Pack2 | SMG_F_2, SMG_F_2_Sand | Kriss Vector carbine with a drum on the mag | Drum mag ahead of trigger | Striker-V | Medium | Drum instead of a stick |
| Pack2 | SMG_G | B&T APC9 / APC45 | Stick mag just ahead of trigger, top-side handles | MPS5 | Easy | Plain mag swap |
| Pack2 | SMG_H | B&T MP9 / Steyr TMP | Mag inside the pistol grip, rear top handle | X18 / M1911 mag motion | Medium | Mag in grip |
| Pack2 | SMG_I | Bullpup SMG (Magpul PDR-like)? | Bullpup, mag behind grip | none | Hard | Bullpup |
| Pack2 | SMG_J | SIG MPX | Curved mag ahead of trigger, rear T handle | MPS5 or MX16A4 | Easy | Plain mag swap |
| Pack2 | SMG_J_2 | SIG MPX with coupled twin mags (or a small drum?) | Wide double mag ahead of trigger, rear T handle | MPS5 or MX16A4 | Medium | Double-width mag changes the grab |
| Pack3 | SMG_K | Colt 9mm SMG (AR-15 pattern) | Stick mag ahead of trigger, rear T handle | MX16A4 | Easy | AR layout |
| Pack3 | SMG_L | PP-19-01 Vityaz | Curved mag ahead of trigger, right-side bolt handle | AK | Easy | AK family |
| Pack3 | SMG_M | Uzi | Mag inside the pistol grip, cocking knob on top | X18 / M1911 mag motion | Medium | Mag in grip, top knob |
| Pack3 | SMG_N | CZ Scorpion EVO 3 | Curved mag ahead of trigger, forward side handle | MPS5 | Easy | MP5-style layout |
| Pack3 | SMG_O | FN P90 | Horizontal mag on top, bullpup | PDW90 | Medium | Exact source exists, but a top mag is outside the plain mag-swap path |
| Pack5 | SMG_Q | B&T APC9 Pro-like? | Stick mag ahead of trigger, rear T handle plus a front handle | MPS5 or MX16A4 | Easy | Plain mag swap |
| Pack5 | SMG_R | Daewoo K7-like suppressed SMG? | Stick mag ahead of trigger, AR-style lower | MX16A4 or MPS5 | Easy | Plain mag swap |
| Pack5 | WWII_SMG_B | Thompson M1 / M1A1 | Stick mag ahead of trigger, side bolt handle | MPS5 or AK | Easy | Plain mag swap |
| Pack1 | Pistol_A | TEC-9 | Mag ahead of the trigger guard, bolt knob on the left | MPS5 (nearest) | Medium | Pistol-sized gun with an SMG-style mag position |
| Pack1 | Pistol_B | Glock 17 | Slide pistol, mag in grip | X18 | Easy | Same gun as the source |
| Pack1 | Pistol_C | HK USP? | Slide pistol, mag in grip | X18 / M1911 | Easy | Standard pistol |
| Pack1 | Pistol_D | Desert Eagle | Slide pistol, mag in grip | DGL50 | Easy | Same gun as the source |
| Pack1 | Pistol_E | M1911 | Slide pistol, mag in grip | M1911 | Easy | Same gun as the source |
| Pack2 | Pistol_F | FN FNX-45 Tactical? | Slide pistol, mag in grip | X18 / M1911 | Easy | Standard pistol |
| Pack2 | Pistol_G | Revolver with a full barrel shroud (Chiappa Rhino-like)? | Revolver, cylinder | Viper-357 (empty reload only) | Hard | Revolver |
| Pack2 | Pistol_H | SIG P320 / M17 | Slide pistol, mag in grip | X18 | Easy | Standard pistol |
| Pack2 | Pistol_I | CZ 75 SP-01 | Slide pistol, mag in grip | M1911 / X18 | Easy | Standard pistol |
| Pack2 | Pistol_J | SIG P226 / Beretta Px4-like? | Slide pistol, mag in grip | X18 / M1911 | Easy | Standard pistol |
| Pack3 | Pistol_K | FN Five-seveN | Slide pistol, mag in grip | X18 | Easy | Standard pistol |
| Pack3 | Pistol_L | Hudson H9 | Slide pistol, mag in grip | M1911 / X18 | Easy | Standard pistol |
| Pack3 | Pistol_M | 2011-style race pistol with compensator and top rail | Slide pistol, mag in grip | M1911 | Easy | Standard pistol |
| Pack3 | Pistol_N | Beretta M9A3 | Slide pistol, mag in grip | X18 / M1911 | Easy | Standard pistol |
| Pack3 | Pistol_O | MP-443 Grach? | Slide pistol, mag in grip | X18 / M1911 | Easy | Standard pistol |
| Pack5 | CW_Pistol_B | Skorpion vz. 61 | Small curved mag ahead of trigger, cocking knobs on both sides, wire stock folded over the top | MPS5 (nearest) | Medium | Tiny gun compared with any SMG source |
| Pack5 | CW_Pistol_C | Stechkin APS / Makarov PM | Slide pistol, mag in grip | X18 / M1911 | Easy | Standard pistol |
| Pack5 | Pistol_R | Large revolver with a railed shroud | Revolver, cylinder | Viper-357 (empty reload only) | Hard | Revolver |
| Pack5 | Pistol_S | Glock-type pistol in a stocked chassis (Flux Raider-like) with a spare mag as foregrip | Slide pistol action, mag in grip, shoulder stock | X18 | Medium | Pistol mag swap but held two-handed with a stock |
| Pack1 | ShotGun_A | AA-12 | Box mag ahead of trigger, charging handle on top | Drake-12 | Medium | Magazine-fed shotgun |
| Pack1 | ShotGun_B | Remington 870 / Mossberg 500 | Tube under barrel, pump | KXG12 | Hard | Pump, shell by shell |
| Pack1 | ShotGun_C | Benelli M4 (stock collapsed) | Tube under barrel, semi-auto | KXG12 (loop without the pump) | Hard | Shell by shell |
| Pack1 | ShotGun_D | Saiga-12 | Box mag ahead of trigger, right-side bolt handle | Drake-12 | Medium | Magazine-fed shotgun (exact source) |
| Pack1 | ShotGun_E | Over/under double barrel | Break action, top lever | none | Hard | Break action |
| Pack2 | ShotGun_F | Franchi SPAS-12 | Tube under barrel, pump / semi-auto | KXG12 | Hard | Pump, shell by shell |
| Pack2 | ShotGun_G | Magazine-fed pump (Remington 870 DM / Mossberg 590M-like) with AR stock? | Box mag ahead of trigger, ribbed sliding forend | Drake-12 (mag) plus KXG12 (pump) | Medium | Magazine-fed shotgun; a pump rack would be an extra step |
| Pack2 | ShotGun_H | Daewoo USAS-12 | Box mag ahead of trigger, carry handle | Drake-12 or MX16A4 | Medium | Magazine-fed shotgun |
| Pack2 | ShotGun_I | AR-pattern magazine-fed shotgun (Genesis Gen-12 / Typhoon F12-like)? | Box mag ahead of trigger, side handle | Drake-12 or MX16A4 | Medium | Magazine-fed shotgun |
| Pack2 | ShotGun_J | Saiga-12K / Vepr-12 with rails and folding stock | Curved box mag ahead of trigger, right-side bolt handle | Drake-12 | Medium | Magazine-fed shotgun |
| Pack3 | ShotGun_K | Benelli M4 / Beretta 1301 with fixed pistol-grip stock | Tube under barrel, semi-auto | KXG12 (loop without the pump) | Hard | Shell by shell |
| Pack3 | ShotGun_L | Compact magazine-fed shotgun (Origin-12-like)? | Box mag ahead of trigger, fat muzzle, unclear lever ahead of the mag well | Drake-12 | Medium | Magazine-fed shotgun |
| Pack3 | ShotGun_M | AR-style magazine-fed shotgun (MKA 1919 / Fostech-like)? | Box mag ahead of trigger, side handle | Drake-12 or MX16A4 | Medium | Magazine-fed shotgun |
| Pack3 | ShotGun_N | Benelli M3-like with folding wire stock? | Tube under barrel, ribbed forend (pump or semi-auto) | KXG12 | Hard | Shell by shell |
| Pack3 | ShotGun_O | Pancor Jackhammer | Bullpup, rotating drum cassette behind the grip | none | Avoid | Exotic drum behind the grip |
| Pack5 | CW_ShotGun_B | Sawn-off pump (Ithaca 37 Stakeout-like), taped grip, no stock | Tube under barrel, pump | KXG12 | Hard | Pump, shell by shell |
| Pack5 | ShotGun_P | Mossberg 590 | Tube under barrel, pump | KXG12 | Hard | Pump, shell by shell |
| Pack1 | LMG_A | RPD? | Belt carried in a drum under the receiver, right-side handle | MGX5 | Hard | Belt-fed (if it is really an RPK with a drum mag: Medium) |
| Pack1 | LMG_B | M249 SAW with belt box | Belt, top cover | MGX5 | Hard | Belt-fed |
| Pack1 | LMG_B_2 | M249 Para fed from a rifle mag | Rifle mag pushed into the angled well on the left side | none | Hard | Side-angled mag well on a belt-fed gun |
| Pack1 | LMG_C | HK MG36 with Beta C-Mag | Twin-drum mag ahead of trigger, top charging handle | MX16A4 or G3 | Medium | Drum mag on a rifle |
| Pack1 | LMG_D | QBB-95 | Bullpup, drum behind grip | none | Hard | Bullpup |
| Pack1 | LMG_E | Belt-fed AR upper with ammo box in the mag well (Ares Shrike-like)? | Box hanging from the mag well, raised top cover | MGX5 | Hard | Looks belt-fed (if the box is a plain magazine: Medium) |
| Pack2 | LMG_F | FN EVOLYS-like | Belt box, feed cover | MGX5 | Hard | Belt-fed |
| Pack2 | LMG_G | L86 LSW with drum | Bullpup, drum behind grip | none | Hard | Bullpup |
| Pack2 | LMG_H | IWI Negev? | Belt box under receiver | MGX5 | Hard | Belt-fed |
| Pack2 | LMG_I | HK21E / HK23E | Belt box under receiver, G3-style forward cocking handle | MGX5 | Hard | Belt-fed |
| Pack2 | LMG_J | Ultimax 100? | Drum ahead of trigger, forward pistol grip | AK or MX16A4 | Medium | Drum mag (if the box is a belt box: Hard) |
| Pack3 | LMG_K | SIG MG 338 / KAC LAMG-like | Flat belt box, feed cover | MGX5 | Hard | Belt-fed |
| Pack3 | LMG_L | KAC ChainSAW | Belt box, feed cover, chainsaw-style rear grip and top front handle | none | Avoid | Exotic hold, belt-fed |
| Pack3 | LMG_M | KAC Stoner LMG / Minimi-like | Belt box, feed cover | MGX5 | Hard | Belt-fed |
| Pack3 | LMG_N | RPK-16 with drum | Drum mag ahead of trigger, right-side bolt handle | AK | Medium | Drum mag on a rifle |
| Pack3 | LMG_O | PKP Pecheneg | Belt box, feed cover | MGX5 | Hard | Belt-fed |
| Pack5 | WWII_LMG_B | BAR M1918A2 | Box mag ahead of trigger, left-side charging handle, traditional stock | Mk14EBR or G3 | Medium | Magazine-fed LMG, long, no pistol grip |
| Pack3 | Launcher_A, Launcher_A_2 | RPG-7 (A_2 with optic and a different warhead) | Rocket pushed into the muzzle | RPG | Medium | Exact source, but it is a rocket insert, not a mag swap |
| Pack3 | Launcher_B | SMAW / Carl Gustaf-type reusable launcher? | Rocket inside the tube, loaded from the rear | none | Avoid | Launcher that is not RPG-7 shaped |
| Pack3 | Launcher_C | FIM-92 Stinger | Sealed missile tube with seeker antenna | none | Avoid | Launcher that is not RPG-7 shaped |
| Pack3 | Launcher_D | XM25 airburst launcher | Bullpup, box mag behind grip | none | Hard | Bullpup mag swap (as much work as any bullpup; treat as Avoid if no bullpup mode is built) |
| Pack3 | Launcher_E | Standalone single-shot 40 mm launcher (M320 / HK GLM-like)? | Single grenade, barrel opens to load | none | Avoid | Bespoke single-shot grenade loading |
| Pack5 | Launcher_G | Barrett XM109 payload rifle? | Semi-auto, large box mag ahead of trigger, right-side charging handle | SVD or Mk14EBR | Medium | Same layout as Recon_A; very large gun and mag |

## Counts per rating (by prefab)

| Rating | Count |
| --- | --- |
| Easy | 78 |
| Medium | 32 |
| Hard | 49 |
| Avoid | 6 |
| Total | 165 |

Per category (Easy / Medium / Hard / Avoid):

| Category | Prefabs | Easy | Medium | Hard | Avoid |
| --- | --- | --- | --- | --- | --- |
| AR, Rifle | 44 | 34 | 2 | 7 | 1 |
| DMR | 18 | 16 | 1 | 1 | 0 |
| Recon | 20 | 1 | 3 | 16 | 0 |
| SMG | 23 | 13 | 8 | 2 | 0 |
| Pistol | 19 | 14 | 3 | 2 | 0 |
| ShotGun | 17 | 0 | 8 | 8 | 1 |
| LMG | 17 | 0 | 4 | 12 | 1 |
| Launcher | 7 | 0 | 3 | 1 | 3 |

## Safest picks per category (the Easy ones)

- **AR, exact or near-exact source:** Pack1 AR_A, AR_A_2, AR_B, AR_B_2; Pack2 AR_I, AR_I_2, AR_J; Pack3 AR_M, AR_M_2, AR_R (AS Val); Pack5 AR_AB, AR_AC, AR_W, AR_Z, CW_AR_D (G3), CW_AR_E (Galil).
- **AR, plain mag swap with a different charging handle:** Pack1 AR_C, AR_C_2, AR_C_2_Black, AR_C_Black, AR_D; Pack2 AR_G, AR_G_2, AR_K, AR_K_2, AR_S; Pack3 AR_L, AR_N, AR_Q, AR_Q_2; Pack5 AR_AA, AR_X, AR_Y, CW_AR_F.
- **DMR:** Pack1 DMR_A, DMR_B, DMR_C (M14 EBR), DMR_D; Pack2 DMR_G (SVD), DMR_H, DMR_H_2, DMR_I (PSG1), DMR_J; Pack3 DMR_K, DMR_L (FAL), DMR_M, DMR_N, DMR_O; Pack5 CW_DMR_B, DMR_Q.
- **Recon:** only Pack2 Recon_G (semi-auto AR-10 type). Every other Recon gun is a bolt action or an oversized .50.
- **SMG:** Pack1 SMG_B, SMG_B_2 (MP5 / MP5K), SMG_C (UMP); Pack2 SMG_F, SMG_F_Sand (Vector), SMG_G, SMG_J (MPX); Pack3 SMG_K, SMG_L, SMG_N; Pack5 SMG_Q, SMG_R, WWII_SMG_B (Thompson).
- **Pistol:** Pack1 Pistol_B (Glock), Pistol_C, Pistol_D (Desert Eagle), Pistol_E (1911); Pack2 Pistol_F, Pistol_H, Pistol_I, Pistol_J; Pack3 Pistol_K, Pistol_L, Pistol_M, Pistol_N, Pistol_O; Pack5 CW_Pistol_C.
- **ShotGun:** none Easy. Least work: Pack1 ShotGun_D and Pack2 ShotGun_J (Saiga-12 type, the Drake-12 source matches), then Pack1 ShotGun_A, Pack2 ShotGun_H, ShotGun_I, Pack3 ShotGun_M.
- **LMG:** none Easy. Least work: Pack3 LMG_N (RPK-16 drum, AK source), Pack1 LMG_C (MG36 C-Mag), Pack5 WWII_LMG_B (BAR).
- **Launcher:** none Easy. Pack3 Launcher_A / Launcher_A_2 (RPG-7) have an exact source; Pack5 Launcher_G is really a big semi-auto rifle.

## Unsure about the mechanism from the picture

Rating would change:

- **Pack1 LMG_A**: drum under the receiver with no separate cover part. Read as an RPD (belt inside the drum, Hard). If it is meant as an RPK with a drum magazine, it is Medium.
- **Pack1 LMG_B_2**: M249 body with a curved rifle mag in place of the belt box. The mag part is 213 mm wide, so it sits in the angled side well. Rated Hard; Medium if an angled mag swap is acceptable.
- **Pack1 LMG_E**: AR-15 with a box hanging from the mag well and a raised block on top of the receiver. Read as a belt-fed upper (Hard). If the box is just a large magazine, it is Medium.
- **Pack2 LMG_J**: rectangular box ahead of the trigger plus a forward pistol grip, no cover part. Read as an Ultimax 100 drum (Medium). If it is a belt box, Hard.
- **Pack2 LMG_H, LMG_I**: box under the receiver, no part named Mag_Cover. Assumed belt-fed (Negev, HK21) from the box width; a drum-magazine variant would be Medium.
- **Pack3 Recon_L, Recon_N**: no bolt handle visible and the Bolt part (if any) stays inside the receiver width, so assumed semi-auto (Medium). If either is a bolt action, Hard.
- **Pack1 Recon_D**: could be the semi-auto AS50 instead of a bolt action. The Bolt part is wider than the receiver, which suggests a bolt handle, so rated Hard.
- **Pack1 DMR_D**: SKS shape with a curved mag. Assumed the mag detaches (Easy). A fixed mag loaded with stripper clips would be Hard.
- **Pack2 ShotGun_G**: box mag plus a ribbed forend. Pump or semi-auto is not clear; Medium either way, but a pump needs a rack after the mag swap.
- **Pack2 SMG_J_2**: the wide mag (139 mm) looks like two coupled mags, could be a small drum. Medium either way.
- **Pack5 Pistol_S**: the forward "Mag_Back" part is assumed to be a spare mag used as a foregrip, with the live mag in the grip. If the front one is the live mag, it is a different motion.
- **Pack5 Rifle_B, WWII_Rifle_A; Pack2 DMR_F; Pack5 WWII_LMG_B**: mechanism is clear (box mag ahead of trigger); rated Medium only because the stock has no normal pistol grip. If the right-hand pose is not a concern, all four are Easy.

Rating would not change (identity or detail unclear):

- **Pack1 ShotGun_C, Pack3 ShotGun_K, Pack3 ShotGun_N**: tube-fed; pump versus semi-auto can't be told from the side view. Hard either way.
- **Pack3 ShotGun_L**: a vertical part named Bolt_Part sits ahead of the mag well; unclear whether it is a charging lever or a latch.
- **Pack3 ShotGun_M, Pack2 ShotGun_I**: magazine-fed, exact model unknown.
- **Pack2 Pistol_G, Pack5 Pistol_R**: revolvers with full shrouds; swing-out or break-top is unknown.
- **Pack5 CW_Recon_B**: no magazine part, read as single-shot (AR-50). **Pack5 CW_Recon_A**, **Pack2 Recon_H**: bolt actions, model unknown. **Pack5 WWII_Recon_B**: M1903 or Kar98k.
- **Pack2 AR_H, Pack1 DMR_E, Pack2 SMG_I, Pack3 AR_P**: bullpups, exact model unknown.
- **Pack5 AR_X**: read as an AN-94 because the mag is 87 mm wide (canted). Still an AK-style swap, but the mag path is tilted.
- **Pack2 AR_S, AR_G, AR_K; Pack3 AR_N, AR_Q; Pack3 DMR_O; Pack5 DMR_Q**: modern rifles with a box mag ahead of the trigger; exact model and charging handle type are guesses.
- **Pack5 SMG_R, SMG_Q**: stick mag ahead of the trigger; exact model is a guess.
- **Pack3 Launcher_B, Launcher_E**: loading method unknown (rear-loaded tube, single-shot 40 mm). Avoid either way. **Pack5 Launcher_G**: XM109 or a plain Barrett; same mechanism.
