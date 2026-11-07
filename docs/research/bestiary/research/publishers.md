# Enemy packs from six publishers

Research for aipocalypse enemies (style anchor: Synty POLYGON / Sidekick). Data: `pubs_packs.json` (780 enemy-relevant packs, owned ones flagged), images in `img/<id>_1.jpg` (key image) plus `_2`/`_3` (screenshots) for every strong, possible or owned pack. Weak packs get only the 420x280 listing key image.

Prices are the current Asset Store USD prices on 2026-11-08. No pack from these publishers was on sale, so `price_orig` equals `price_now` everywhere.

## How the listing was built, and how complete it is

The publisher pages render on the client, but `assetstore.unity.com/search?f-publisher_name=<name>&firstResult=<n>` renders 96 product cards on the server, along with the Coveo result fields (category, price, rating). Paging through it and cross-checking with a `sortCriteria=date descending` pass gave each publisher's full catalogue, matching the store's own totals:

| Publisher | Products | Enemy-relevant in JSON | Owned |
| --- | --- | --- | --- |
| Meshtint Studio (3867) | 723 | 569 | 0 |
| Magic Pig Games / Infinity PBR (4645) | 332 | 99 | 41 (the whole low-poly line: 39 packs + both bundles) |
| Dungeon Mason (23554) | 70 | 43 | 5 |
| GrigoriyArx (20029) | 69 | 55 | 0 |
| Agor2012 (85001) | 26 | 13 | 4 |
| Polysplit Games (101065) | 5 | 1 | 0 |

The JSON leaves out humans (heroes, knights, peasants, wizards, astronauts), props, environments, icons, music, VFX and Magic Pig "Bonus Files". Every pack in the JSON had its product page fetched, except 47 Magic Pig PBR packs that duplicate owned low-poly creatures (listing data only, marked in `red_flags`). Most specs come straight from the pages. Where a page doesn't say, the field reads `unknown`. The `enemies` of weak Meshtint and GrigoriyArx singles are auto-derived from the pack name and clip list (`notes` says so).

## Rules applied

- **Animation-ready** (`anim_source`): `own clips` when the page lists idle, move, attack, hit and death; `humanoid retarget` when the rig is Humanoid; `both` when both apply. `unknown` drops the verdict one step. A pack with neither clips nor a Humanoid rig is `weak`, flagged "not animatable".
- **Budget**: single packs up to $20, bundles and combos (packs of separately sold items, evolution packs, ultimate packs) up to $60. Over the cap a pack is at most `possible`. Over twice the cap it is `weak`. Owned packs are exempt.
- **No human-looking enemies**: these are flagged and made `weak`. Undead, goblinoids, demons and robots stay in. Borderline cases (Medusa, Metal Ghost Knight, Flower Fairy) are `possible` with a flag.

## Magic Pig Games / Infinity PBR

- **Style:** realistic Substance PBR (1/5). The **Low Poly Character** line is the exception: faceted low-poly coloured by a palette (ColorID + `IPBR_LPColor` shader), which the vendor describes as "intended to work with the POLYGON series by Synty Studios" (4/5).
- **Low-poly coverage:** low-poly versions exist for 39 creatures, sold as $35 singles (Demon Dog $50) or in Low Poly Monster Bundle 1 (20 packs) and 2 (19 packs), plus Humans/Elves/Orcs ($50, human). **Every one of the 39 is already installed in its LP version** (checked: `*_LP.fbx`, `InfinityPBR - <X> LP.unity`, LPColor materials). The owned list is the whole LP line.
- **Owned clips, read from the installed FBX/meta files:**
  - Every pack has hit and death clips.
  - 21 of the 39 have a cast, spit or breath clip (Dragons, Bone Dragon, Cobra, Slime, Gargoyle, Treant, Weeper and others).
  - The other 18 have no ranged or cast clip at all, including Goblin, Troll, Minotaur, Imp, Scorpion, Golem, Fish Man, Skeleton Warrior and both Monster Creatures.
  - **24 have walk-only locomotion** (no run clip), among them Goblin, Troll, Golem, Imp, Skeleton Warrior, Demons, Devils and Mummy. That matters for enemies that must rush the player.
- **What the owned set lacks:** robots, drones and mechs; aliens; dinosaurs; firearm, bow or thrown ranged attackers; true small swarm units beyond Bats, Spiders and Slime. Demoness and Harlequin are human-looking, and Medusa is borderline.
- **Not owned, and none has a low-poly version:** Hydra ($70, Oct 2026, heads can be cut off and regrow), Banshee ($75), Jumper ($50), Forest Warrior ($50), Dragons 2 ($50). All are realistic PBR and over budget.
- **Verdict:** nothing new to buy. The installed LP set is a good, on-style base.

## Polysplit Games

- **Style:** low-poly medieval fantasy with one 2048 RGB colour-swap palette and a URP Shader Graph material (5/5). Only 5 products: two are human heroes, two are props, one is enemies.
- **Low-Poly Medieval Fantasy - Biped Creatures Pack ($14.99):** skeleton warrior, archer and mage on one modular base, ghoul, zombie male and female, goblin, orc, troll, ogre and golem. 2-7k tris, Humanoid rig, **no clips** (pose clips only), so they run on our Kevin Iglesias and Synty libraries. Check the troll, ogre and golem proportions. The screenshots show bows, staves, swords and shields, but the page lists no weapon meshes; Polysplit sells weapons separately.
- **Verdict:** buy. Best style match of the six publishers.

## Dungeon Mason

- **Style:** "Polyart" is low-poly with one flat 512 palette, but chibi RPG proportions and candy colours (3/5). The PBR and hand-painted twins are 2/5.
- **Owned:** RPG Monster BUNDLE Polyart (Waves 1-3, 30 monsters) and Monster Swarm Survivor Polyart. The free Duo, Partners, Buddy and Minion samples are covered by these.
- **Beyond Wave 3:** only **RPG Monster Wave 4** (Polyart or PBR, $30, Jan 2026). It has 10 monsters: Blue Slime, Bone Dragon, Castle Monster, Crawler, Flame Knight, Flower Monster, Ice Golem, Little Demon, Skeleton Mage and Starfish. Each has 16 clips, URP by default. It sits $10 over the single cap; the free "RPG Monster Couple" sample has two of them. There is no Wave 5 and no second bundle.
- **Robot Hero: Modular Polyart ($20):** helmeted robot soldiers with assault rifle, shotgun, sniper and pistol, and 102 Humanoid clips (single, burst and auto fire, sniper shot, grenade throw, hit, death). The most complete gun-enemy clip set across the six publishers. A free 16-clip sample exists.
- **Others:** Mini Legions are 430-1,060-tri RTS chibis (Orc and Undead factions as Polyart only inside the $50 four-faction bundle; free Grunt, Lich and Rock Golem). The free Dragon for Boss Monster (4 dragons) is hand-painted or PBR. Tiny Bird, Party Monster and Animal Knight are cute.
- **Verdict:** possible. Wave 4 only if more of the owned look is wanted. Robot Hero for ranged robots, if the chibi proportions are acceptable.

## Agor2012

- **Style:** faceted low-poly with a single 128px colour atlas and emissive trims, URP and HDRP materials on the newer packs (4/5).
- **Rigs:** the newer characters use the Epic/UE5 mannequin skeleton. They import as Generic but are Humanoid-mappable.
- **Owned, and checked in the installed FBX files:**
  - **Modular Robot:** its parts ship with clips. 46 takes live in `Robot_X_modular_Unity.fbx` (robot-style set with Shot L/R and saw attacks, plus a humanoid-style set), and the parts share that skeleton.
  - **Robot X-26.2:** 26 takes, melee only.
  - **Tracked Robot:** 20 takes, but **no hit or death clip**, so it fails the clip rule unless death is the OFF clip plus VFX.
  - **Zombie:** **16 takes** (idle, walk, run, attack x3, hit, dying, scream, crawl, rise from ground). ASSETS.md says "Generic mesh without clips", which is wrong. Confirm in the editor and fix the row.
- **Not owned:**
  - X-25.1, X-25.2, X-25.3 and X-26.1 robots ($10 each, 21-26 clips) add variety but no new behaviours.
  - Crocodile Man ($10) has no hit clip, but its Humanoid-mappable rig covers that.
  - Skeleton ($14.99, 18.8k tris).
  - Zombie Man Modular ($25).
  - The Alien with blaster ($19.99) is weak: 8 clips, no hit clip, rig not stated.
- **Verdict:** possible. Cheap robot variety in a good style.

## Meshtint Studio

- **Catalogue:** huge (723 products) in many series.
- **Polygonal Series (5/5):** faceted low-poly with 32x32 palettes, Generic rigs (Golem, Skeleton Pack and Fantasy Pack are Humanoid), 10-36 clips each with projectile, cast or breath, take damage and die. Built-in Standard materials convert to URP trivially. This is the closest match to POLYGON here and the best value:
  - **Polygonal Creatures Pack 2** ($39.90, 7 creatures).
  - Aliens Pack ($65.90, $5.90 over the cap; or singles $4.99-16.90).
  - Creatures Pack ($79.90, over the cap; singles $4.99-14.90, Dragon $23.90).
  - **Robot Hermit** ($10.90, machine-gun shell turret).
  - **Skeleton Pack** ($19.99, modular skeleton warrior, mage and archer with weapons, Humanoid, no clips).
- **Cute Series robots (3/5):** chunky gradient-textured robots and drones (43 products: 31 singles, 10 three-tier evolution packs at $22.90-37.90, 2 ultimate packs at $129.90 and $145.90, over the cap). Each robot has underground spawn, projectile, cast, take damage and die. CUBE robots (Punisher flying gunship, Crawler, Pin, Sting) and the 2016 Galaxy robots are similar (3/5).
- **Weak:** Cute and Cute Pro monsters (Pokemon-like), Toon and Mega Toon (voxel-blocky), and the Fantasy (Pro) Series (hand-painted). Cute Pro Scout Drone ($9.99) and Turret Walker ($12.99) are kept as possible only because they fill the drone and turret slots.
- **Dinosaurs:** none.
- **Verdict:** strong for the Polygonal series, possible for the robots, weak for the rest.

## GrigoriyArx

- **Style:** mostly 2015-2019 hand-painted mobile models (2/5) or realistic PBR horror (1/5), Built-in only.
- **Droll Robots ($8-18 each):** glossy toy-like walker mechs (3/5) with full clip sets (walk, strafe, turn, projectile attack, damage L/R, die) plus projectile logic: miniguns (05, 06), homing rockets (04), crates that unpack (02, 17). The $120 bundle saves nothing.
- **Dinosaurs:**
  - **Dinosaurus Mini Pack with Babies ($4.99):** T-Rex, Triceratops and Brontosaurus, 16 clips each (attack, tail attack, hit L/R, death, swim). Soft cartoon look (3/5).
  - **Dinosaurus Pack with Babies ($99):** 64 species, including raptors, Pteranodon, Spinosaurus, ankylosaurs and marine reptiles. Over the cap; watch for a sale.
  - A hand-painted Raptor exists only inside Fantasy Characters & Creatures pack 2 ($35).
- **Weak:** Goblins Pack ($4.99: archer with shot clips, warrior, shaman with cast clips) and the rest of the $4.99 fantasy series fill roles cheaply but are hand-painted. Emo Robots are cute companions.
- **Verdict:** possible for Droll robots and the dino mini pack, weak for the rest.

## Shortlist (not owned, in budget unless noted)

| Pick | Price | Synty | Why |
| --- | --- | --- | --- |
| Polysplit Biped Creatures Pack (340684) | $14.99 | 5 | 11 undead, goblinoid and giant enemies incl. skeleton archer and mage; Humanoid; URP Shader Graph; no clips |
| Meshtint Polygonal Creatures Pack 2 (160879) | $39.90 bundle | 5 | Pea Shooter turret, Frightfly flyer, Metalon miniboss, Dionaea and Venus plant ambushers, Mushroom, Flower Fairy |
| Meshtint Polygonal aliens, singles (120257, 120098, 122280, 121274, 118319...) | $4.99-16.90 | 5 | Telekinis caster, Magantis summoner, Serpent spitter, Sicklus/Longtail flyers (whole pack $65.90, $5.90 over the cap) |
| Meshtint Polygonal Robot Hermit (117494) | $10.90 | 5 | Machine-gun turret robot that hides in its shell |
| Meshtint Polygonal Skeleton Pack (138365) | $19.99 | 5 | Modular skeleton warrior, archer and mage with weapons; Humanoid, no clips |
| Meshtint Polygonal singles: Golem, Wolf, Magma, One Eyed Bat, King Cobra, Giant Bee (116161, 115817, 115058, 116780, 114716, 116394) | $4.99-14.90 | 5 | Brute, rusher, exploder, flyer swarm, spitter; Dragon (116690) is the boss at $23.90 |
| Dungeon Mason Robot Hero Modular Polyart (163308) | $20 | 3 | Robot riflemen, snipers and grenadiers, 102 Humanoid clips; free sample 106154 |
| GrigoriyArx Droll Robot 04 / 05 / 06 (246158, 250158, 252422) | $15-18 | 3 | Rocket and minigun walker mechs with projectile logic |
| Meshtint Cute Series robot evolution packs (212125, 204649, 212607, 205248) | $23.90-36.90 | 3 | 3-tier robot and drone families with projectile and cast clips |
| GrigoriyArx Dinosaurus Mini Pack (267769) | $4.99 | 3 | T-Rex, Triceratops, Brontosaurus with full clips (64-dino pack $99, wait for a sale) |

For variety on owned looks: Agor2012 X-25.x / X-26.1 robots ($10 each, 4/5) and Dungeon Mason RPG Monster Wave 4 Polyart ($30, over the cap).
