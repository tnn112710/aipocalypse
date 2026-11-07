# Synty enemy gaps (drives the Asset Store search)

Based on the full store sitemap (211 products, 2026-11-07) and the installed packs. Rule applied: keep a creature only if it ships its own idle/move/attack/hit/death clips or sits on a Humanoid rig.

**What Synty covers well:** bipedal Humanoid monsters (zombies, skeletons, goblins, orcs, trolls, golems, demons, mutants, aliens, humanoid robots, mechs, werewolves, mummies, ghosts, wraiths). Every one of them has **no own clips**; they rely on humanoid retargeting.

**Only one non-humanoid with clips:** POLYGON Dog Pack (Generic rig; wolf, hellhound, robot dog, zombie dogs, coyote, fox, plus breeds).

## Missing or weak (search the Asset Store for these)

| Need | Synty status |
| --- | --- |
| Dinosaurs (raptor, T-rex, pteranodon, triceratops) | **None.** Only a child's dino onesie in the Kids Pack. The nearest thing is the Kaiju Pack's tailed reptile giants (Humanoid, bipedal, no tail animation). |
| Dragons / wyverns | **None** (Cyber City only shows a hologram dragon in a screenshot). |
| Flying creatures with fly loops (bats, birds, wasps, flying eyes, harpies) | **None.** Birds, vultures and bees are FX flocks only. The winged Big Demon (Dungeon Realms) and Gargoyle (Dark Fantasy) are Humanoid bipeds with wings in the mesh. No flying clips exist anywhere in Synty's ANIMATION line. |
| Quadruped predators besides canines (bears, big cats, boars, crocodiles) | **None.** |
| Spiders, insects, swarms, worms | **None** as characters. Locusts, scarabs, cockroaches and alien insects are FX only. |
| Sea and swamp creatures (sharks, crocodiles, eels, kraken, swamp beasts) | Pirate Pack shark/fish are Generic with **only a swim clip** (no attack, hit or death). The Pirate Realm kraken is static tentacles. Only Humanoid fish-folk pirates (Pirate Realm) qualify. |
| Non-humanoid robots (spider bots, quad walkers, rolling bots, animated drones) | **None animated.** Drones are static props (Police Station, Spy Kit, Cyber City, Sci-Fi Space): excluded by the animation rule, though rigid drones could be moved in code if the user allows it. Cyber City's Helper Bot and MurderKitten have unknown rigs. |
| Ghosts with float/phase motion | Ghosts exist only as Humanoid walkers (Dungeon Pack ghosts, Dark Fortress wraith, Fantasy Rivals Spirit Demon). Hover needs custom clips or code. |
| Slimes, blobs, elementals | None, apart from the Fantasy Rivals Elemental Golem (Humanoid). |
| Creature-specific attacks for Humanoid brutes and bosses (slams, grabs, throws, roars, spits, casting, crawling, zombie shamble) | **None.** Synty's ANIMATION line has 6 humanoid packs (locomotion, goblin locomotion, sword, bow, idles, emotes; all installed). It has no creature, zombie, spellcasting, throwing, gun or flying packs as of 2026-11-08. |
| Animated giant bosses (kaiju, mechs, golems) | Models exist (Kaiju, Mech, Fantasy Rivals big rig, all Humanoid) but **no boss attack clips**, and kaiju tails are separate rigid meshes. |
| Aliens beyond bipeds (quadruped, crawling, flying) | None. Only humanoid aliens; the Sci-Fi Horror "zub" critter has an unknown rig. |
