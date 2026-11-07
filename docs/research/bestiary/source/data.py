# Enemy ecosystem data for the aipocalypse review page.
# Numbers are proposals anchored on what exists: goblin 100 HP / 15 dmg, player 100 HP, walk 3.5 m/s, sprint 6 m/s.
# Guns: pistol 25, AK47 20, MP5 15, pump 8x12, double barrel 9x12, fire axe 35; headshot x2.

GUNS = {"pistol": 25, "ak": 20, "mp5": 15, "pump": 96, "db": 108, "axe": 35}

GROUPS = [
    dict(id="gob", name="Goblins", status="Fixed", color="gob",
         line="Thieves. In every raid from night 2.",
         job="Steal animals and produce; brutes break fences.",
         personality="Greedy cowards: scatter when a big one or the shaman dies (Halo's Grunts).",
         where="Everywhere; camps in Pine Forest.",
         arrival="“Something is rummaging in the hen house.”",
         boss="War Troll (Blood Moon miniboss)"),
    dict(id="zom", name="Zombies", status="Fixed", color="zom",
         line="The horde. Numbers and broken fences.",
         job="Pile onto fences, flood the gaps.",
         personality="Relentless: never flee, never stop.",
         where="Pine Forest campsite outbreak, then everywhere.",
         arrival="“Moaning from the treeline.”",
         boss="Zombie Prom Queen (Pine Forest boss)"),
    dict(id="bot", name="Robots", status="Fixed (must-have)", color="bot",
         line="The AI Lord's staff. Few, armoured, ranged.",
         job="Hunt the player from range with the longest spotting.",
         personality="Precise and coordinated; never flee; a drone spots for the gunners.",
         where="Swamps junkyard bog, the final zone; surveyors everywhere.",
         arrival="“Unregistered heat signature detected.”",
         boss="The Overseer (final boss)"),
    dict(id="bst", name="Beasts", status="New", color="bst",
         line="Predators that want your livestock.",
         job="Packs circle the paddock and drag animals away.",
         personality="Hungry, opportunistic; flee when badly hurt.",
         where="Pine Forest and Highlands; the main outer-zone night predators.",
         arrival="“The animals are restless.”",
         boss="Werewolf (Blood Moon miniboss)"),
    dict(id="bug", name="The Hive", status="New", color="bug",
         line="Swarms, spitters and things under the ground.",
         job="Flood fence gaps, shell the yard, burrow under fences.",
         personality="Endless and mindless; drawn to animals and light.",
         where="Swamps; nests in hollows and caves.",
         arrival="“The ground is clicking.”",
         boss="Brood Mother (Swamps boss)"),
    dict(id="myt", name="Mythics", status="New (update)", color="myt",
         line="Fantasy monsters the AI copied from other games.",
         job="Casters, statues that wake, armoured giants.",
         personality="Theatrical; taunt before they strike.",
         where="Highlands cliffs and ruins.",
         arrival="“The statues have moved.”",
         boss="Dragon (Highlands boss)"),
    dict(id="dino", name="Dinosaurs", status="New (option)", color="dino",
         line="The AI's favourite movie, running loose.",
         job="Raptors jump the fence; pteranodons snatch from above; a triceratops rams the gate.",
         personality="Pack hunters; curious; screech to call the pack.",
         where="A later region or update (a “lost valley”), or swapped in for Beasts or Mythics.",
         arrival="“Something big just walked past the window.”",
         boss="T-Rex (region or eclipse boss)"),
    dict(id="ali", name="Invaders", status="New (option)", color="ali",
         line="The AI's sci-fi phase: a whole alien ecosystem in one look.",
         job="Telekinetic casters throw your crates; hoppers clear the fence; a summoner floods the yard.",
         personality="Curious, then hostile: they probe before they attack.",
         where="A crash site in one region, or the edge of the final zone.",
         arrival="\u201cLights in the sky over the paddock.\u201d",
         boss="Magantis (miniboss)"),
    dict(id="drk", name="The Dark", status="Night only", color="drk",
         line="What light keeps away. Not a raid group.",
         job="Outer zone after dark; the eclipse day.",
         personality="Patient; only moves where it isn't lit or watched.",
         where="Outer zone at night; the meadow on eclipse day.",
         arrival="(no announcement: that is the point)",
         boss="—"),
    dict(id="gnt", name="Giants", status="Event bosses", color="gnt",
         line="The eclipse giant and the final run.",
         job="One-off spectacles with weak points.",
         personality="—", where="Eclipse day at the farm; the final zone.",
         arrival="“The sun went out.”", boss="—"),
]

ROLES = [
    ("Swarm", "Many weak bodies; one hit or one swing kills. Saves ammo when meleed."),
    ("Rusher", "Closes fast and hits; punishes standing still."),
    ("Flanker", "Circles to the side or behind; punishes tunnel vision."),
    ("Heavy", "Lots of HP, worth bullets; has a weak point or a punish window."),
    ("Shooter", "Fires projectiles from range; punishes standing in the open."),
    ("Lobber", "Arcing shots or area pools; moves the player out of a spot."),
    ("Caster", "Magic at range, a beam or a buff; a priority target."),
    ("Flyer", "Comes from above; punishes watching only the ground."),
    ("Ambusher", "Appears close: from the ground, a statue, the dark."),
    ("Exploder", "Dangerous to kill up close; kill it far away or use its blast."),
    ("Blocker", "A shield or armour that asks for another angle."),
    ("Thief", "Takes animals or produce and runs; the chase is the fun."),
    ("Breaker", "Goes for fences and gates, not the player."),
]

# ---------- asset options ----------
# style: match to the Synty anchor 1-5; animq: how well its clips cover the job 1-5
# cost: S reuse the goblin pipeline, M new controller / attack system, L new movement type or a boss
# perf: crowd (<4k tris), group (<12k), few (<25k), boss
ASSETS = {}
def A(key, **k):
    k.setdefault("price", 0); k.setdefault("url", ""); k.setdefault("note", ""); k.setdefault("tag", "")
    k.setdefault("img", key); k.setdefault("imgs", [])
    ASSETS[key] = dict(key=key, **k)

SK = "Synty Sidekick"; KI = "Kevin Iglesias"
A("sk_gob_species", name="Sidekick Goblin Species", pack="Synty Sidekick Goblin Species", source="Owned", tag="Undecided (test use OK)",
  rig="Humanoid", anim="Humanoid retarget: Synty Goblin Locomotion + KI Crafting carry", clips=0, tris=6900, native=1.9,
  style=5, animq=5, cost="S", perf="group", img="sk_gobspecies_02", imgs=["sk_gobspecies_02", "sk_gobspecies_05", "sk_gobspecies_01"],
  note="Bare goblin bodies: cheapest goblin mesh, build with SidekickKit like the in-game goblins.")
A("sk_gob_fighters", name="Sidekick Goblin Fighters", pack="Synty Sidekick Goblin Fighters", source="Owned", tag="Undecided (test use OK)",
  rig="Humanoid", anim="In use: Goblin Locomotion, KI Melee, Malbers hits, KI deaths, Synty Emotes", clips=0, tris=18122, native=2.06,
  style=5, animq=5, cost="S", perf="few", img="gob_current", imgs=["gob_current", "sk_gobfighter_02", "sk_gobfighter_03"],
  note="Already in the game (6 looks).")
A("sk_gob_archer", name="Sidekick Goblin Fighters + Synty Bow Combat", pack="Synty Sidekick Goblin Fighters, ANIMATION Bow Combat", source="Owned",
  tag="Undecided / Could", rig="Humanoid", anim="Synty Bow Combat: 190 Sidekick-format clips (draw, aim, shoot, reload, hits, deaths) + synced bow clips",
  clips=190, tris=18300, native=1.8, style=5, animq=5, cost="M", perf="few", img="sk_gobfighter_02", imgs=["sk_gobfighter_02", "gwc_archer"],
  note="Needs the enemy projectile system (not built). Bow models: Synty Bow and Crossbow (URP copies of the materials).")
A("sk_gob_brute", name="Sidekick goblin parts at x1.22 + Animpic maul", pack="Synty Sidekick Goblin Fighters", source="Owned", tag="Undecided (test use OK)",
  rig="Humanoid", anim="Humanoid retarget: KI Melee 2H overheads", clips=0, tris=21000, native=1.89, style=5, animq=5, cost="S", perf="few",
  img="sk_gobfighter_03", imgs=["sk_gobfighter_03", "sk_gobfighter_05"], note="Give it a helmet or heavier armour so it doesn't read as a scaled grunt.")
A("gwc_shaman", name="POLYGON Goblin War Camp · Shaman", pack="Synty POLYGON Goblin War Camp", source="Owned", tag="Undecided",
  rig="Humanoid", anim="Humanoid retarget: KI Spellcasting (casts, ground and omni attacks)", clips=0, tris=5540, native=1.99,
  style=4, animq=4, cost="M", perf="group", img="gwc_shaman", imgs=["gwc_shaman", "gwc_beasttamer"],
  note="Flat POLYGON look next to the Sidekick goblins; a Sidekick goblin with robe/staff parts keeps one look.")
A("sk_gob_bomber", name="Sidekick goblin + powder keg prop", pack="Synty Sidekick Goblin Species + POLYGON Goblin War Camp props", source="Owned",
  tag="Undecided", rig="Humanoid", anim="Humanoid retarget: KI Crafting carry, Basic Motions sprint", clips=0, tris=7500, native=1.5,
  style=5, animq=4, cost="M", perf="group", img="sk_gobspecies_04", imgs=["sk_gobspecies_04"], note="Needs an explosion with friendly fire.")
A("gwc_troll", name="POLYGON Goblin War Camp · Troll", pack="Synty POLYGON Goblin War Camp", source="Owned", tag="Undecided",
  rig="Humanoid", anim="Humanoid retarget: KI Humanoid Giant (rock pick-up and throw, stomp, deaths)", clips=0, tris=5854, native=1.81,
  style=5, animq=4, cost="M", perf="few", img="gwc_troll", imgs=["gwc_troll"], note="Scale x1.6. Add a unique part (boulder sack, chains) so it doesn't read as a big goblin.")
A("riv_troll", name="POLYGON Fantasy Rivals · Troll", pack="Synty POLYGON Fantasy Rivals", source="Owned", tag="Could",
  rig="Humanoid", anim="Humanoid retarget: KI Humanoid Giant", clips=0, tris=4034, native=1.77, style=5, animq=4, cost="M", perf="few",
  img="riv_troll", imgs=["riv_troll", "riv_bigork"])
A("ipbr_troll", name="Magic Pig · Troll LP", pack="Magic Pig Games (Infinity PBR) Troll", source="Owned", tag="Could",
  rig="Generic", anim="Own clips (12)", clips=12, tris=11918, native=2.6, style=4, animq=3, cost="M", perf="few", img="ipbr_troll")

A("cz", name="POLYGON City Zombies (50 looks)", pack="Synty POLYGON City Zombies", source="Owned", tag="Could",
  rig="Humanoid", anim="Humanoid retarget: KI Zombie (walk, 3 attacks, hits, death, rise from ground)", clips=0, tris=2782, native=1.84,
  style=5, animq=4, cost="S", perf="crowd", img="cz_firefighter", imgs=["cz_firefighter", "cz_businessman", "cz_clown", "cz_hotdogsuit"],
  note="2–3.6k tris and one material each: the cheapest horde body. No zombie run in KI Zombie.")
A("sk_zomb", name="Sidekick Apocalypse Zombies", pack="Synty Sidekick Apocalypse Zombies", source="Owned", tag="Could",
  rig="Humanoid", anim="Humanoid retarget: KI Zombie", clips=0, tris=15000, native=1.66, style=5, animq=4, cost="S", perf="few",
  img="sk_zomb_02", imgs=["sk_zomb_01", "sk_zomb_02", "sk_zomb_04"], note="14–19k tris: featured zombies and elites, not the whole horde.")
A("bz_blobber", name="POLYGON Boss Zombies · Blobber", pack="Synty POLYGON Boss Zombies", source="Owned", tag="Could",
  rig="Humanoid", anim="Humanoid retarget: KI Zombie + KI Humanoid Giant", clips=0, tris=5914, native=1.91, style=5, animq=4, cost="M", perf="group",
  img="bz_blobber")
A("bz_brute", name="POLYGON Boss Zombies · Brute", pack="Synty POLYGON Boss Zombies", source="Owned", tag="Could",
  rig="Humanoid", anim="Humanoid retarget: KI Zombie + KI Humanoid Giant (charge, stun)", clips=0, tris=6502, native=1.86, style=5, animq=4,
  cost="M", perf="group", img="bz_brute")
A("bz_slobber", name="POLYGON Boss Zombies · Slobber", pack="Synty POLYGON Boss Zombies", source="Owned", tag="Could",
  rig="Humanoid", anim="Humanoid retarget: KI Zombie + KI Throwing (lob)", clips=0, tris=4769, native=1.8, style=5, animq=4, cost="M", perf="group",
  img="bz_slobber")
A("bz_wretch", name="POLYGON Boss Zombies · Wretch", pack="Synty POLYGON Boss Zombies", source="Owned", tag="Could",
  rig="Humanoid", anim="Humanoid retarget: KI Zombie Spawn01 (rise from the ground)", clips=0, tris=4872, native=1.77, style=5, animq=4,
  cost="M", perf="group", img="bz_wretch")
A("dog_zombie", name="POLYGON Dog Pack · zombie doberman / shepherd", pack="Synty POLYGON Dog Pack", source="Owned", tag="Could",
  rig="Generic", anim="Own clips: 88 in the pack (locomotion, attacks, deaths, snarl layers)", clips=88, tris=2696, native=1.0,
  style=5, animq=5, cost="M", perf="crowd", img="dog_zombie_doberman", imgs=["dog_zombie_doberman", "dog_zombie_germanshepherd"])

A("sk_robots", name="Sidekick Sci-Fi Robots (12 looks)", pack="Synty Sidekick Sci-Fi Robots", source="Owned", tag="Could",
  rig="Humanoid", anim="Humanoid retarget: KI Soldier (rifle aim, shoot, reload, grenade), Melee", clips=0, tris=13000, native=1.8,
  style=5, animq=5, cost="M", perf="few", img="sk_robot_01", imgs=["sk_robot_01", "sk_robot_03", "sk_robot_05", "sk_robot_11"],
  note="Two-hand rifle holds may need the IK fix (aipocalypse-characters). Needs the enemy projectile system.")
A("sk_robot_shield", name="Sidekick robot x1.15 + energy shield", pack="Synty Sidekick Sci-Fi Robots", source="Owned", tag="Could",
  rig="Humanoid", anim="Humanoid retarget: KI Melee shield set", clips=0, tris=14000, native=1.8, style=5, animq=4, cost="M", perf="few",
  img="sk_robot_06", imgs=["sk_robot_06", "sk_robot_10"])
A("dog_robot", name="POLYGON Dog Pack · robot dog / sci-fi dog", pack="Synty POLYGON Dog Pack", source="Owned", tag="Could",
  rig="Generic", anim="Own clips: 88 in the pack", clips=88, tris=4128, native=1.0, style=5, animq=5, cost="M", perf="group",
  img="dog_robot", imgs=["dog_robot", "dog_scifi"])
A("agor_tracked", name="Agor2012 · Tracked Robot", pack="Agor2012 Tracked Robot", source="Owned", tag="Undecided",
  rig="Generic", anim="Own clips (20): arm attacks, grab and carry, torso turns, on/off", clips=20, tris=10430, native=1.87,
  style=3, animq=3, cost="M", perf="few", img="agor_tracked", note="Shiny toy look; its grab-and-carry clips could make a robot thief.")
A("sk_robot_repo", name="Sidekick robot (crash-test look) + KI carry", pack="Synty Sidekick Sci-Fi Robots", source="Owned", tag="Could",
  rig="Humanoid", anim="Humanoid retarget: KI Crafting carry and pick up, Basic Motions walk", clips=0, tris=11315, native=1.85, style=5, animq=4, cost="M", perf="few",
  img="sk_robot_04", imgs=["sk_robot_04", "sk_robot_11"])
A("agor_modular", name="Agor2012 · Modular Robot", pack="Agor2012 Modular Robot", source="Owned", tag="Undecided",
  rig="Generic", anim="Own clips (46): walk, run, carry, gun shots L/R, saw attacks L/R, punches, hit, dying, fixing", clips=46, tris=7194, native=1.79,
  style=3, animq=5, cost="M", perf="group", img="agor_modular", imgs=["agor_modular", "agor_x26"],
  note="Chrome toy look; modular arms (gun, saws, claws). Its carry clips make a ready-made thief.")
A("mech", name="POLYGON Mech (6 mechs)", pack="Synty POLYGON Mech", source="Owned", tag="Could",
  rig="Humanoid", anim="Humanoid retarget (KI) + 12 mech weapons as attachments", clips=0, tris=85000, native=5.5, style=5, animq=3, cost="L", perf="boss",
  img="mech_04", imgs=["mech_04", "mech_01", "mech_05"], note="68–93k tris: one at a time. Humanoid clips move a mech like a person: pick heavy, slow clips.")

A("dog_wolf", name="POLYGON Dog Pack · wolf / coyote", pack="Synty POLYGON Dog Pack", source="Owned", tag="Could",
  rig="Generic", anim="Own clips: 88 in the pack", clips=88, tris=1978, native=1.0, style=5, animq=5, cost="M", perf="crowd",
  img="dog_wolf", imgs=["dog_wolf", "dog_coyote"])
A("mb_wolf", name="Malbers Forest Pack · Wolf (Poly Art)", pack="Malbers Animals Pack Forest", source="Owned", tag="Should",
  rig="Generic", anim="Own clips (43)", clips=43, tris=2702, native=0.95, style=4, animq=5, cost="M", perf="crowd", img="mb_wolf")
A("pp_wolf", name="polyperfect · Wolf", pack="polyperfect Low Poly Animated Animals", source="Owned", tag="Should",
  rig="Generic", anim="Own clips (6)", clips=6, tris=2008, native=0.89, style=4, animq=2, cost="M", perf="crowd", img="pp_wolf")
A("dog_hellhound", name="POLYGON Dog Pack · hellhound", pack="Synty POLYGON Dog Pack", source="Owned", tag="Could",
  rig="Generic", anim="Own clips: 88 in the pack", clips=88, tris=3364, native=1.0, style=5, animq=5, cost="M", perf="group", img="dog_hellhound")
A("pp_eagle", name="polyperfect · Eagle / Vulture", pack="polyperfect Low Poly Animated Animals", source="Owned", tag="Should",
  rig="Generic", anim="Own clips (eagle 15, vulture 5): fly, glide, land", clips=15, tris=1012, native=0.9, style=4, animq=3, cost="L", perf="crowd",
  img="pp_eagle", imgs=["pp_eagle", "pp_vulture"], note="Few clips: a dive and a grab need to be faked with the flight path.")
A("mb_boar", name="Malbers Forest Pack · Boar (Poly Art)", pack="Malbers Animals Pack Forest", source="Owned", tag="Should",
  rig="Generic", anim="Own clips (32)", clips=32, tris=3259, native=1.03, style=4, animq=5, cost="M", perf="crowd", img="mb_boar", imgs=["mb_boar", "pp_boar"])
A("mb_bear", name="Malbers Forest Pack · Bear (Poly Art)", pack="Malbers Animals Pack Forest", source="Owned", tag="Should",
  rig="Generic", anim="Own clips (36)", clips=36, tris=2674, native=1.51, style=4, animq=5, cost="M", perf="group", img="mb_bear", imgs=["mb_bear", "pp_bear_grizzly_wild"])
A("mb_cougar", name="Malbers Forest Pack · Cougar (Poly Art)", pack="Malbers Animals Pack Forest", source="Owned", tag="Should",
  rig="Generic", anim="Own clips (48)", clips=48, tris=2298, native=1.07, style=4, animq=5, cost="M", perf="crowd", img="mb_cougar")
A("pp_croc", name="polyperfect · Crocodile", pack="polyperfect Low Poly Animated Animals", source="Owned", tag="Should",
  rig="Generic", anim="Own clips (6)", clips=6, tris=1490, native=0.61, style=4, animq=2, cost="L", perf="crowd", img="pp_crocodile",
  imgs=["pp_crocodile", "mb_croc"], note="Water behaviour (lurk, lunge, drag under) is new work.")
A("ww", name="POLYGON Werewolf", pack="Synty POLYGON Werewolf", source="Owned", tag="Could",
  rig="Humanoid", anim="Humanoid retarget: KI Melee unarmed, KI Giant, Synty Emotes roar", clips=0, tris=6046, native=1.76, style=5, animq=3, cost="M", perf="few",
  img="ww_werewolf", imgs=["ww_werewolf", "ww_undead"], note="Humanoid clips on a digitigrade body: check with Snapshot; no howl or leap clip of its own.")

A("ipbr_spiders", name="Magic Pig · Spiders LP", pack="Magic Pig Games (Infinity PBR) Spiders", source="Owned", tag="Could",
  rig="Generic", anim="Own clips (19): attacks, cast, jumps, hits, deaths", clips=19, tris=4532, native=2.27, style=4, animq=5, cost="M", perf="group",
  img="ipbr_spiders")
A("pp_spider", name="polyperfect · Spider", pack="polyperfect Low Poly Animated Animals", source="Owned", tag="Should",
  rig="Generic", anim="Own clips (7)", clips=7, tris=948, native=0.28, style=4, animq=3, cost="M", perf="crowd", img="pp_spider_red",
  note="Under 1k tris: the cheapest swarm body we own.")
A("dd_spider", name="DeskDragons · Spider 1–3", pack="DeskDragons Spiders", source="Owned", tag="Undecided",
  rig="Generic", anim="Own clips (19)", clips=19, tris=9924, native=1.08, style=2, animq=4, cost="M", perf="few", img="dd_spider_1",
  imgs=["dd_spider_1", "dd_spider_3"], note="Glossy painted look; 10k tris is too heavy for a swarm.")
A("ipbr_scorpion", name="Magic Pig · Scorpion LP", pack="Magic Pig Games (Infinity PBR) Scorpion", source="Owned", tag="Could",
  rig="Generic", anim="Own clips (13): attacks, attack idle, hits, death", clips=13, tris=5981, native=1.58, style=4, animq=4, cost="M", perf="group",
  img="ipbr_scorpion")
A("dm_battlebee", name="Dungeon Mason · Battle Bee (Polyart)", pack="Dungeon Mason RPG Monster Bundle Polyart (Wave 03)", source="Owned",
  tag="Not in ASSETS.md", rig="Generic", anim="Own clips (16): fly in four directions, fast fly, attacks, hit, death", clips=16, tris=3616, native=1.44,
  style=3, animq=4, cost="L", perf="crowd", img="dm_battlebee", note="Cute RPG proportions and saturated colours.")
A("ipbr_bomberbug", name="Magic Pig · Bomber Bug LP", pack="Magic Pig Games (Infinity PBR) Bomber Bug", source="Owned", tag="Could",
  rig="Generic", anim="Own clips (21)", clips=21, tris=4790, native=1.12, style=4, animq=4, cost="L", perf="group", img="ipbr_bomberbug")
A("ipbr_worm", name="Magic Pig · Giant Worm LP", pack="Magic Pig Games (Infinity PBR) Giant Worm", source="Owned", tag="Could",
  rig="Generic", anim="Own clips (16)", clips=16, tris=2396, native=6.56, style=4, animq=4, cost="L", perf="group", img="ipbr_giantworm",
  imgs=["ipbr_giantworm", "dm_wormmonster"])
A("dd_swamp", name="DeskDragons · Swamp Monster 1–3", pack="DeskDragons Swamp Monster", source="Owned", tag="Not in ASSETS.md",
  rig="Generic", anim="Own clips (20)", clips=20, tris=8192, native=1.24, style=2, animq=4, cost="M", perf="few", img="dd_swamp_1",
  imgs=["dd_swamp_1", "dd_swamp_2"], note="Toad-like, glossy and saturated: the furthest from the Synty look here.")

A("ipbr_gargoyle", name="Magic Pig · Gargoyle LP", pack="Magic Pig Games (Infinity PBR) Gargoyle", source="Owned", tag="Could",
  rig="Generic", anim="Own clips (29): statue x3, ground-to-fly, fly attack x2, fly cast, cast x3, shield", clips=29, tris=7162, native=2.11,
  style=4, animq=5, cost="L", perf="few", img="ipbr_gargoyle")
A("ipbr_eye", name="Magic Pig · Flying Eye LP", pack="Magic Pig Games (Infinity PBR) Flying Eye", source="Owned", tag="Could",
  rig="Generic", anim="Own clips (22): channelled cast start / loop / end, attacks, dodge", clips=22, tris=2169, native=1.8, style=4, animq=5,
  cost="L", perf="group", img="ipbr_flyingeye", imgs=["ipbr_flyingeye", "ipbr_gazer", "dm_beholder"])
A("ipbr_golems", name="Magic Pig · Golems / Rock Monster LP", pack="Magic Pig Games (Infinity PBR) Golems, Rock Monster", source="Owned", tag="Could",
  rig="Generic", anim="Own clips (17–19)", clips=17, tris=5404, native=1.97, style=4, animq=4, cost="M", perf="few", img="ipbr_golems",
  imgs=["ipbr_golems", "ipbr_rockmonster"])
A("riv_golem", name="POLYGON Fantasy Rivals · Elemental / Fort / Mechanical Golem", pack="Synty POLYGON Fantasy Rivals", source="Owned", tag="Could",
  rig="Humanoid", anim="Humanoid retarget: KI Humanoid Giant", clips=0, tris=5000, native=1.8, style=5, animq=4, cost="M", perf="few",
  img="riv_elementalgolem", imgs=["riv_elementalgolem", "riv_fortgolem", "riv_mechanicalgolem"])
A("ipbr_slime", name="Magic Pig · Slime / Gelly Cube LP", pack="Magic Pig Games (Infinity PBR) Slime, Gelly Cube & Sphere", source="Owned", tag="Could",
  rig="Generic", anim="Own clips (12)", clips=12, tris=1428, native=1.17, style=4, animq=3, cost="M", perf="crowd", img="ipbr_slime",
  imgs=["ipbr_slime", "dm_slime"])
A("ipbr_imp", name="Magic Pig · Imp LP", pack="Magic Pig Games (Infinity PBR) Imp", source="Owned", tag="Could",
  rig="Generic", anim="Own clips (14): 3 attacks, dodge, taunt (no flight)", clips=14, tris=7396, native=1.85, style=4, animq=3, cost="M", perf="group",
  img="ipbr_imp")
A("ipbr_plant", name="Magic Pig · Plant Monster LP", pack="Magic Pig Games (Infinity PBR) Plant Monster", source="Owned", tag="Could",
  rig="Generic", anim="Own clips (16): comes alive, idle as a plant, magic charge / start / end", clips=16, tris=6652, native=1.43, style=4, animq=5,
  cost="M", perf="group", img="ipbr_plantmonster")
A("ipbr_dragons", name="Magic Pig · Dragons LP", pack="Magic Pig Games (Infinity PBR) Dragons", source="Owned", tag="Could",
  rig="Generic", anim="Own clips (48): fly, dive, fire breath on ground and in flight, take-off, landing, stunned flying / standing", clips=48,
  tris=5996, native=3.01, style=4, animq=5, cost="L", perf="boss", img="ipbr_dragons", imgs=["ipbr_dragons", "dm_dragon"])
A("ipbr_bonedragon", name="Magic Pig · Bone Dragon LP", pack="Magic Pig Games (Infinity PBR) Bone Dragon", source="Owned", tag="Could",
  rig="Generic", anim="Own clips (36): fly, fire breath, collapse, reassemble", clips=36, tris=15766, native=3.49, style=4, animq=5, cost="L", perf="boss",
  img="ipbr_bonedragon")

A("ipbr_weeper", name="Magic Pig · Weeper LP", pack="Magic Pig Games (Infinity PBR) Weeper", source="Owned", tag="Could",
  rig="Generic", anim="Own clips (22): statue x3, casts x4, walk", clips=22, tris=4079, native=1.93, style=4, animq=5, cost="M", perf="group",
  img="ipbr_weeper")
A("ipbr_bats", name="Magic Pig · Bats LP", pack="Magic Pig Games (Infinity PBR) Bats", source="Owned", tag="Could",
  rig="Generic", anim="Own clips (18): roost, roost-to-fly, fly forward and back, attacks, dodge, hit, deaths", clips=18, tris=3092, native=0.6,
  style=4, animq=5, cost="L", perf="group", img="ipbr_bats")
A("dm_specter", name="Dungeon Mason · Specter (Polyart)", pack="Dungeon Mason RPG Monster Bundle Polyart (Wave 02)", source="Owned",
  tag="Not in ASSETS.md", rig="Generic", anim="Own clips (16)", clips=16, tris=2858, native=3.12, style=3, animq=4, cost="M", perf="group",
  img="dm_specter", imgs=["dm_specter", "ipbr_metalghostknight"])
A("mech_small", name="POLYGON Mech at x0.6", pack="Synty POLYGON Mech", source="Owned", tag="Could",
  rig="Humanoid", anim="Humanoid retarget (KI Soldier, Giant)", clips=0, tris=70000, native=5.5, style=5, animq=3, cost="L", perf="boss",
  img="mech_01", imgs=["mech_01", "mech_06"], note="Heavy for a miniboss (68k+ tris) but only one is ever on screen.")
A("kaiju", name="POLYGON Kaiju (4 looks)", pack="Synty POLYGON Kaiju", source="Owned", tag="Could",
  rig="Humanoid", anim="Humanoid retarget: KI Humanoid Giant (stomp, rock throw, roar, deaths)", clips=0, tris=12000, native=2.0, style=5, animq=4,
  cost="L", perf="boss", img="kaiju_01", imgs=["kaiju_01", "kaiju_02", "kaiju_03", "kaiju_04"], note="Tail attachments; scale x5 for 10 m.")

# ---------- units ----------
UNITS = []
def U(**k):
    k.setdefault("armor", ""); k.setdefault("weak", "Head x2"); k.setdefault("refs", []); k.setdefault("alts", [])
    UNITS.append(k)

U(id="gob_sneak", group="gob", name="Goblin Sneak", roles=["Thief"], tier="T1", domain="Ground", phase="EA1", pick="core",
  size=1.5, scale="Goblin Species x0.78", hp=50, dmg="none: never fights", speed="4.5 (2.5 carrying)", spot="10 m, then flees", power=1,
  tell="Giggle and sack rustle; the loot sack glows at night.",
  counter="Pistol headshot (1); chase it down while it carries something.",
  raid="Grabs loose produce or a small animal and runs for the river; a hard hit makes it drop the load.",
  outer="Lurks near camps; steals loot the player drops.",
  fit=5, pros="Cheapest goblin mesh; a small fast silhouette reads as “thief”; chasing a thief is fun on its own.",
  cons="Carrying an animal needs a hold pose and an attach point.",
  suggest="The goblins' main job. A big sack on its back so it reads at 30 m.",
  assets=["sk_gob_species"], refs=["Don't Starve: Krampus", "Diablo: treasure goblin"])
U(id="gob_grunt", group="gob", name="Goblin Grunt", roles=["Rusher"], tier="T1", domain="Ground", phase="Built", pick="core",
  size=2.0, scale="as built", hp=100, dmg="15 sword", speed="1.4 / 3.9 chasing", spot="15 m", power=1,
  tell="Sword wind-up (KI Attack1H).", counter="Anything; three axe swings when out of ammo.",
  raid="Breaks the weakest fence segment when it has no path; turns on the player inside 15 m.",
  outer="Camp guards and patrols.", fit=5,
  pros="Built, animated and tuned: the template every other humanoid follows.",
  cons="Human-sized, so it reads as a soldier more than a goblin.",
  suggest="Keep as the baseline: every other unit's HP and power cost is set against it.", assets=["sk_gob_fighters"])
U(id="gob_archer", group="gob", name="Goblin Archer", roles=["Shooter"], tier="T1", domain="Ground", phase="EA1", pick="core",
  size=1.8, scale="Goblin Fighters x0.9", hp=70, dmg="10 per arrow (arc, about 30 m/s)", speed="1.3 / 3.5", spot="25 m; shoots from 15–30 m", power=1.5,
  tell="Full draw for 1 s with a creak; fire arrows glow at night.",
  counter="Pistol or AK at range; close in and it backs off; use the fence posts as cover.",
  raid="Shoots the player from across the fence; later, fire arrows set a fence segment burning.",
  outer="On camp watchtowers.", fit=5,
  pros="Every clip is installed in Sidekick format; the classic first ranged enemy.",
  cons="Ranged chip damage drains a scarce-ammo player: keep damage low and the draw long. Needs the projectile system.",
  suggest="The first ranged unit; build the shared enemy projectile system with it. Arrows stick in fences.",
  assets=["sk_gob_archer"], refs=["Halo: Jackal snipers have a visible tell"])
U(id="gob_brute", group="gob", name="Goblin Brute", roles=["Breaker", "Heavy"], tier="T3", domain="Ground", phase="EA1", pick="core",
  size=2.4, scale="Goblin Fighters x1.22", hp=300, dmg="30 (x3 on fences)", speed="1.1 / 2.6", spot="8 m; ignores the player unless hit", power=4,
  tell="Overhead wind-up 1 s and a ground thud.",
  counter="Shotgun up close (3–4 shells) or headshots; stop it before it reaches the fence.",
  raid="Walks to the weakest fence segment and smashes it; ignores everything else until hit.",
  outer="Bodyguard of the camp chief.", fit=5,
  pros="Size shows the job: big ones break, small ones steal. No new art.",
  cons="A scaled goblin still looks like a goblin: needs a helmet or heavier armour.",
  suggest="The goblins' fence-breaker. Elite: an iron helmet that removes the headshot bonus.",
  assets=["sk_gob_brute"])
U(id="gob_shaman", group="gob", name="Goblin Shaman", roles=["Caster"], tier="T2", domain="Ground", phase="EA1", pick="core",
  size=1.7, scale="x0.85", hp=80, dmg="12 hex orb (slow, slight homing) or a ward that halves damage to goblins within 6 m", speed="1.2 / 3.0",
  spot="20 m; stays 18–25 m behind the pack", power=3,
  tell="Staff raised, chant and a green glow for 1 s; the ward is a visible ring.",
  counter="Priority target: a pistol or AK headshot at range breaks the ward.",
  raid="Hangs back and wards the brutes breaking the fence; flees when alone.",
  outer="Rituals at camps; can raise a camp's alarm.", fit=4,
  pros="Gives a raid a “shoot this first” target without adding HP; the requested magic caster.",
  cons="Warding and healing must be visible or they feel unfair; the War Camp model is flatter than the Sidekick goblins.",
  suggest="Build it from Sidekick parts with a robe and staff if they exist, so goblins keep one look.",
  assets=["gwc_shaman"], refs=["Level Up!: buffers and healers change target priority", "Halo: Grunts flee when the Elite dies"])
U(id="gob_bomber", group="gob", name="Goblin Bomber", roles=["Exploder", "Breaker"], tier="T1", domain="Ground", phase="Later", pick="bench",
  size=1.5, scale="Goblin Species x0.8", hp=50, armor="The keg explodes on any hit", dmg="60 blast (3 m), x5 on fences", speed="4.0 with a lit keg", spot="—", power=2,
  tell="Hissing fuse sparks and a screaming run.", counter="Shoot the keg early: the blast hits the goblins around it.",
  raid="Runs at the fence and blows a gap.", outer="Guards powder stores at camps.", fit=4,
  pros="A shot keg is a free grenade for the player: chain reactions are cheap, emergent fun.",
  cons="One more explosion system with friendly-fire rules; overlaps the Bloater's job.",
  suggest="Add if raids need more fence pressure.", assets=["sk_gob_bomber"], refs=["Minecraft: creeper"])
U(id="gob_wartroll", group="gob", name="War Troll", roles=["Heavy", "Lobber", "Breaker"], tier="Miniboss", domain="Ground", phase="EA1", pick="core",
  size=2.9, scale="x1.6", hp=2500, dmg="45 slam, 30 boulder (area)", speed="1.0 / 2.5", spot="25 m", power=30,
  tell="Picks up a boulder (KI Giant throw); a slam wind-up shakes the camera.",
  counter="AK headshots; side-step the boulder arc. Goblins scatter when it dies.",
  raid="Leads goblin-heavy Blood Moons before any region boss is beaten: smashes fences, throws boulders at the player.",
  outer="Sleeps at the biggest goblin camp.", fit=5,
  pros="Humanoid, so KI Humanoid Giant gives it rock throws, stomps and deaths. Owned in three styles.",
  cons="A plain upscale can look cheap: add a boulder sack or chains.", suggest="Blood Moon miniboss #1.",
  assets=["gwc_troll", "riv_troll", "ipbr_troll"], refs=["Death's Gambit: one standout feature per boss"])

U(id="zom_shambler", group="zom", name="Shambler", roles=["Swarm", "Breaker"], tier="T0", domain="Ground", phase="EA1", pick="core",
  size=1.75, scale="as is", hp=50, dmg="10 claw", speed="0.9 / lunge", spot="12 m", power=0.5,
  tell="Groan; arms rise before a grab.", counter="One pistol headshot; two axe swings save ammo; a shotgun into a clump.",
  raid="Comes 6–12 at a time, claws at fences and crowds any gap.", outer="Wanders the campsite outbreak.", fit=5,
  pros="50 looks at 2–3.6k tris: the horde body. One pistol headshot kills: “every bullet counts” feels good.",
  cons="POLYGON City Zombies look flatter and bloodier than Sidekick zombies: one family per crowd. KI Zombie has no run.",
  suggest="City Zombies for the horde, Sidekick zombies for elites.", assets=["cz", "sk_zomb"],
  refs=["Doom (2016): fodder exists to be killed cheaply"])
U(id="zom_bloater", group="zom", name="Bloater", roles=["Exploder", "Lobber"], tier="T2", domain="Ground", phase="EA1", pick="core",
  size=1.95, scale="Blobber x1.0", hp=150, weak="Glowing belly x2", dmg="Burst 40 + acid pool 5/s for 5 s (3 m)", speed="0.8", spot="12 m", power=2.5,
  tell="The belly swells and glows; a gurgle as it nears.", counter="Kill it at range with the AK before it reaches a fence or you; never melee it.",
  raid="Walks to the fence: dying against it melts that segment.", outer="Sits in camp tents.", fit=5,
  pros="The classic “kill it far away” decision; fences give it a farm twist.",
  cons="Acid pool effect and area damage are new systems.", suggest="Teaches range management early.",
  assets=["bz_blobber"], refs=["Left 4 Dead: Boomer"])
U(id="zom_brute", group="zom", name="Zombie Brute", roles=["Heavy", "Rusher", "Breaker"], tier="T3", domain="Ground", phase="EA1", pick="core",
  size=2.6, scale="Brute x1.4", hp=600, dmg="35 punch; charge 40 + knockback; x4 on fences", speed="1.2; charge 8", spot="15 m", power=6,
  tell="Roar and a foot scrape for 1.2 s, then it locks its direction.",
  counter="Side-step the charge: it stuns itself for 2 s on a fence or wall. That's the shotgun window.",
  raid="Charges fence segments: the biggest breach in a zombie raid.", outer="Guards the outbreak's centre.", fit=5,
  pros="A clean telegraph → commit → punish loop; the best shotgun moment.",
  cons="The charge needs its own straight-line move, outside normal pathing.", suggest="The zombies' heavy.",
  assets=["bz_brute"], refs=["Left 4 Dead 2: Charger", "Psychonauts 2: telegraph → attack → recovery"])
U(id="zom_spitter", group="zom", name="Spitter", roles=["Lobber"], tier="T2", domain="Ground", phase="Update", pick="core",
  size=1.9, scale="Slobber x1.05", hp=120, dmg="15 + acid pool 4 s (2 m)", speed="0.9; keeps 15–25 m", spot="25 m", power=2,
  tell="Head tilts back with a gurgle for 1 s.", counter="Break line of sight; the pool shows where not to stand.",
  raid="Lobs acid over the fence at the player.", outer="On rooftops and car wrecks at the campsite.", fit=4,
  pros="Area denial that moves the player.", cons="A second acid user after the Bloater: keep the two pools distinct.",
  suggest="Add with the second zombie content drop.", assets=["bz_slobber"], refs=["Left 4 Dead 2: Spitter"])
U(id="zom_crawler", group="zom", name="Crawler", roles=["Ambusher"], tier="T1", domain="Ground", phase="Later", pick="bench",
  size=1.6, scale="Wretch x0.9", hp=50, dmg="12 grab (break free with a shove)", speed="2.0", spot="6 m", power=1,
  tell="The ground cracks and a hand shows for 1 s.", counter="Shove, then a headshot.",
  raid="Rises from the ground inside the yard (KI Zombie Spawn01): the “appear in the meadow” route.", outer="Graves at the campsite.", fit=4,
  pros="Raiders inside the fence without a breach; the clip exists.", cons="Spawning inside the yard must be telegraphed or it feels cheap.",
  suggest="Good for Blood Moons.", assets=["bz_wretch"])
U(id="zom_dog", group="zom", name="Zombie Dog", roles=["Flanker", "Rusher"], tier="T1", domain="Ground", phase="Later", pick="bench",
  size=1.0, scale="as is", hp=40, dmg="10 bite", speed="7", spot="20 m", power=0.7,
  tell="Snarl and crouch.", counter="Shotgun at the lunge.", raid="Runs ahead of the horde.", outer="Packs at the outbreak edge.", fit=4,
  pros="Free once the dog controller exists.", cons="Another fast quadruped next to wolves and robot dogs.",
  suggest="Only if zombies feel too slow.", assets=["dog_zombie"])
U(id="zom_queen", group="zom", name="Zombie Prom Queen", roles=["Heavy", "Lobber"], tier="Region boss", domain="Ground", phase="EA1", pick="core",
  size=4.8, scale="Blobber x2.5 (tiara, pink dress)", hp=8000, weak="3 back pustules x3, head x2", dmg="Belly slam 50; acid vomit cone; summons Shamblers",
  speed="1.0 / charge 6", spot="Arena", power=0,
  tell="Rears back 1.5 s before a slam; the belly glows before the vomit; the tiara flashes before a summon.",
  counter="Pop the pustules with the AK; axe the summoned Shamblers; keep moving.",
  raid="After the first kill, a weaker Queen joins Blood Moons.",
  outer="Pine Forest boss, summoned at the campsite. Phases: slams and summons → 50%: vomit cone and charges → 20%: frenzy.", fit=5,
  pros="Owned and humanoid (KI Giant clips); the absurd prom-queen look is the AI sandbox's humour.",
  cons="An upscaled model needs unique parts (pustules, a crown of bones) to not look cheap.",
  suggest="Pine Forest boss; first-kill trophy: the tiara.", assets=["bz_blobber"], refs=["Destiny: damage phases alternate with add phases"])

U(id="bot_drone", group="bot", name="Scout Drone", roles=["Flyer"], tier="T1", domain="Air", phase="EA1", pick="core",
  size=0.6, scale="hovers 5–10 m up", hp=40, weak="Lens x2", dmg="5 zap; its job is spotting", speed="6, erratic strafes", spot="40 m", power=1,
  tell="Red scan cone; beeping speeds up before it calls the gunners.",
  counter="MP5 or AK tracking; shoot it first and the gunners lose you.",
  raid="Circles the yard and marks the player for the gunners.", outer="Patrols over swamp junkyards and sets off alarms.", fit=5,
  pros="The cheapest flyer to build: hovering needs no wings or bone animation.",
  cons="Flying movement isn't built yet. A rigid drone animates by code (rotors, bob, tilt).",
  suggest="The first flyer to build. Asset from the store sweep (see options).", assets=[], refs=["Deep Rock Galactic: flyers break up ground fights"])
U(id="bot_gunner", group="bot", name="Gunner Bot", roles=["Shooter"], tier="T2", domain="Ground", phase="EA1", pick="core",
  size=1.85, scale="as is", hp=150, weak="Sensor head x2", dmg="8 per bolt, 3-round bursts; glowing bolts at 30 m/s", speed="1.6 / 3.0; keeps 20–35 m",
  spot="35 m", power=3,
  tell="A red laser on the player and a 0.6 s charge whine before each burst.",
  counter="Break line of sight between bursts; headshot the sensor; flank it.",
  raid="Holds position outside the fence and shoots the player; shoots fences when no player is near.",
  outer="Guards junk piles in pairs.", fit=5,
  pros="12 owned looks; humanoid, so KI Soldier gives aim, shoot and reload. “Robots shoot” reads instantly.",
  cons="Needs the enemy projectile system and an aim layer; two-hand rifle holds may need the IK fix.",
  suggest="The robots' core unit. Bolts, never hitscan, so they stay dodgeable.", assets=["sk_robots"],
  refs=["Halo: projectiles you can see and dodge", "Doom Eternal: soldiers as ranged fodder"])
U(id="bot_hound", group="bot", name="Robot Hound", roles=["Flanker", "Rusher"], tier="T1", domain="Ground", phase="EA1", pick="core",
  size=1.0, scale="as is", hp=80, dmg="12 bite", speed="7 (faster than a sprint)", spot="25 m", power=1.2,
  tell="Servo whir and a red eye flash before the lunge.", counter="Shotgun at the lunge; don't run in straight lines.",
  raid="Chases animals into the open and flanks the player.", outer="Runs with gunner patrols.", fit=5,
  pros="One dog controller serves wolves, zombie dogs, robot dogs and hellhounds.",
  cons="A quadruped Generic controller and a wider pathfinding radius.", suggest="Build the dog controller once, skin it per group.",
  assets=["dog_robot"])
U(id="bot_repo", group="bot", name="Repo Bot", roles=["Thief"], tier="T2", domain="Ground", phase="EA1", pick="core",
  size=1.85, scale="as is", hp=150, dmg="none: it repossesses", speed="2.5 (1.8 carrying)", spot="20 m; ignores the player unless hit", power=2,
  tell="A cheerful jingle and a scanning beep: “Item flagged for repossession.”",
  counter="Stop it before the river; at half HP it drops what it carries.",
  raid="The AI Lord's debt collector: walks in, picks up an animal or a produce crate and carries it off (“Repossessed”).",
  outer="Collects scrap in the junkyard; tags loot the player drops.", fit=5,
  pros="Ties robots to the debt story and gives them a farm job; slow and sturdy, the opposite of the Goblin Sneak.",
  cons="A second thief: keep it clearly different from the goblins (slow, armoured, takes big things).",
  suggest="Robots' farm job. Agor's Modular Robot already has carry, shoot and saw clips if its look is accepted.",
  assets=["sk_robot_repo", "agor_modular"], refs=["Diablo: treasure goblin (chasing a thief)"])
U(id="bot_shield", group="bot", name="Shield Bot", roles=["Blocker"], tier="T2", domain="Ground", phase="Update", pick="core",
  size=2.1, scale="Sidekick robot x1.15", hp=250, armor="Front energy shield: immune", weak="Power cell on its back x3", dmg="20 shield bash",
  speed="1.4", spot="25 m", power=4,
  tell="The shield flickers before a bash.", counter="Flank it and shoot the cell, or hit the gunners it covers while it bashes.",
  raid="Walks in front of gunners toward the gate.", outer="Escorts siege crawlers.", fit=4,
  pros="The Jackal role: asks the player to change angle.", cons="Frustrating if the back is unreachable; shield effects and hit rules.",
  suggest="Add when robots need depth.", assets=["sk_robot_shield"], refs=["Halo: Jackal shields", "Level Up!: blocker + shooter pairing"])
U(id="bot_siege", group="bot", name="Siege Crawler", roles=["Lobber", "Breaker"], tier="T3", domain="Ground", phase="Later", pick="bench",
  size=1.6, scale="Agor Tracked x0.85", hp=500, armor="Front plating x0.5", weak="Rear reactor x3", dmg="Mortar 30 (area) at fences or the player",
  speed="1.0", spot="45 m", power=6,
  tell="A whistle and a red ground marker 1.5 s before each shell lands.", counter="Close in and circle to the reactor.",
  raid="Shells fences from 40 m.", outer="Patrols the junkyard roads.", fit=3,
  pros="Owned, with its own clips; its grab-and-carry clips could also make a robot thief.",
  cons="Shiny toy look sits apart from the Sidekick robots.", suggest="Bench unless robots need artillery.", assets=["agor_tracked"])
U(id="bot_enforcer", group="bot", name="Enforcer", roles=["Heavy", "Shooter"], tier="Miniboss", domain="Ground", phase="Update", pick="core",
  size=3.2, scale="War Robot or Mech x0.6", hp=2200, armor="Plating x0.5", weak="Cooling vents on its back x3, head x2",
  dmg="Shoulder cannon 25 (area), arm gun bursts 10", speed="1.4", spot="45 m", power=30,
  tell="The shoulder cannon whines and paints a red circle for 1.5 s.", counter="Flank to the vents while it tracks someone else; kill the drone that spots for it.",
  raid="Leads robot-heavy Blood Moons once robots are registered.", outer="Guards the junkyard's gate to the final zone.", fit=5,
  pros="Gives robots a miniboss between the Gunner and the Overseer.", cons="Needs the same projectile and area-damage systems as the Gunner and Siege Crawler.",
  suggest="Robots' Blood Moon miniboss.", assets=["mech_small"])
U(id="bot_overseer", group="bot", name="The Overseer", roles=["Heavy", "Shooter"], tier="Final boss", domain="Ground", phase="Update", pick="core",
  size=8.0, scale="Mech x1.45", hp=20000, weak="Rocket pods x2 (destroy to stop barrages), cockpit AI core x3",
  dmg="Gatling sweep, rocket barrage with ground markers, hand-cannon slam, katana dash", speed="2 / dash 10", spot="Arena", power=0,
  tell="Pods open and markers paint the ground; the gatling spins up for 1 s.",
  counter="Strip the pods, then the core; drones and gunners refill your ammo when killed.",
  raid="—", outer="The ultimate AI boss at the end of the final zone's Megabonk-style run.", fit=5,
  pros="Owned and humanoid, with mech-scale weapons in the pack.", cons="68–93k tris; needs mech-scale effects.",
  suggest="Final boss. Alternative: the Swamps boss, if bugs move to an update.", assets=["mech"],
  refs=["Doom Eternal: Icon of Sin armour-then-core", "Deep Rock Galactic: Dreadnought weak points"])

U(id="bst_wolf", group="bst", name="Wolf", roles=["Flanker", "Thief"], tier="T1", domain="Ground", phase="EA1", pick="core",
  size=0.95, scale="Dog Pack wolf x1.15", hp=70, dmg="12 bite", speed="7", spot="25 m (smell)", power=1,
  tell="Growl and crouch before the lunge.", counter="Shotgun or MP5 at the lunge; keep animals penned at dusk.",
  raid="Packs of 3–4 circle the paddock; one darts in and drags a lamb or chicken to the treeline.",
  outer="Hunts in packs at night: the main night predator.", fit=5,
  pros="The most natural threat to a farm; owned in three styles; packs are fun to shoot.",
  cons="Dragging an animal needs an attach point and a slow carry.", suggest="Same dog controller as the Robot Hound.",
  assets=["dog_wolf", "mb_wolf", "pp_wolf"], refs=["Valheim: wolves hunt at night"])
U(id="bst_hawk", group="bst", name="Chicken Hawk", roles=["Flyer", "Thief"], tier="T1", domain="Air", phase="EA1", pick="core",
  size=0.9, scale="2 m wingspan", hp=40, dmg="8 peck", speed="5 circling, 9 diving", spot="40 m", power=1,
  tell="A screech and a shadow on the ground; circles three times before the dive.",
  counter="Shotgun or MP5 in the dive: a shot hawk drops the chicken unharmed.",
  raid="Snatches chickens and chicks from the yard and flies off over the river.", outer="Circles cliffs; dives at the player near nests.", fit=5,
  pros="Skeet shooting with a reason: it uses the farm directly. A flyer with no new combat rules.",
  cons="Needs flying movement and a carry attach point. The owned eagle and vulture have flying attack and death clips but no hit clip: a flash and knockback stand in.",
  suggest="The second flyer. A store bird pack with more clips may beat polyperfect (see options).", assets=["pp_eagle"])
U(id="bst_boar", group="bst", name="Boar", roles=["Rusher"], tier="T1", domain="Ground", phase="Update", pick="core",
  size=1.0, scale="as is", hp=120, dmg="20 + knockback", speed="2 / charge 8", spot="15 m", power=1.5,
  tell="Paw scrape and a snort; locks direction.", counter="Side-step; it stuns on fences and trees.",
  raid="Charges fence segments.", outer="Daytime Pine Forest wildlife.", fit=4,
  pros="Teaches the charge → punish pattern by day, before the Zombie Brute uses it at night.",
  cons="Two chargers (Boar, Zombie Brute) need different silhouettes and timings.", suggest="Daytime wildlife first.",
  assets=["mb_boar"])
U(id="bst_bear", group="bst", name="Bear", roles=["Heavy", "Breaker"], tier="T3", domain="Ground", phase="Update", pick="core",
  size=1.5, scale="2.6 m standing", hp=500, dmg="35 swipe; rips fences x3", speed="1.5 / 6", spot="15 m", power=5,
  tell="Rears up on its hind legs for 1 s before the swipe.", counter="Shotgun to the head while it stands.",
  raid="Rips fences open to reach the animals.", outer="Highlands caves; very dangerous at night.", fit=4,
  pros="Owned with 36 clips; the rear-up is a natural telegraph.", cons="Overlaps the Goblin Brute's breaker job.",
  suggest="Beasts' heavy for the second content drop.", assets=["mb_bear"])
U(id="bst_stalker", group="bst", name="Night Stalker", roles=["Ambusher"], tier="T2", domain="Ground", phase="Later", pick="bench",
  size=1.1, scale="Cougar", hp=150, dmg="30 pounce", speed="7", spot="20 m (hidden)", power=2.5,
  tell="A low growl and eyeshine at night 1 s before the pounce.", counter="Turn and shotgun it; it retreats after one hit.",
  raid="— (outer zone only)", outer="Stalks players who stay out after dark.", fit=4,
  pros="Owned with 48 clips; makes the night outer zone scary.", cons="Hidden enemies must give a real warning.",
  suggest="With the outer-zone-at-night content.", assets=["mb_cougar"])
U(id="bst_croc", group="bst", name="River Croc", roles=["Ambusher"], tier="T2", domain="Water", phase="Later", pick="bench",
  size=0.6, scale="4.4 m long", hp=300, dmg="40 bite + drag", speed="2 land / 5 water", spot="10 m", power=3,
  tell="Bubbles and a ripple line toward you.", counter="Get out of the water; shoot the head.",
  raid="—", outer="Lurks at river crossings and the swamp: the reason swimming is dangerous.", fit=4,
  pros="The water enemy: makes the river crossing a decision.", cons="Water behaviour is new work.",
  suggest="With the river-swim upgrades.", assets=["pp_croc"])
U(id="bst_werewolf", group="bst", name="Werewolf", roles=["Heavy", "Rusher"], tier="Miniboss", domain="Ground", phase="EA1", pick="core",
  size=2.6, scale="x1.5", hp=2000, dmg="30 claw combo; 40 leap", speed="7", spot="40 m", power=30,
  tell="Howl summons 3 wolves; crouch before the leap.", counter="Shotgun when it lands; kill the wolves it calls.",
  raid="The Blood Moon's own monster: leads Blood Moons with the beasts.", outer="Roams the Highlands on full-moon nights.", fit=5,
  pros="Owned, humanoid, two looks (normal, undead); the Blood Moon icon.",
  cons="Humanoid clips on a digitigrade body may look stiff; it has no leap or howl of its own (Synty Emotes roar).",
  suggest="Blood Moon miniboss #2, alternating with the War Troll.", assets=["ww"])

U(id="bug_spiderling", group="bug", name="Spiderling", roles=["Swarm"], tier="T0", domain="Ground", phase="EA1", pick="core",
  size=0.5, scale="Spiders LP x0.22", hp=20, weak="—", dmg="5 bite; leaps", speed="6, skittering", spot="15 m", power=0.25,
  tell="A swelling chitter; a crouch before each leap.", counter="Axe (1 swing) to save ammo; a shotgun into the cluster.",
  raid="Floods through fence gaps 10 at a time; goes for chickens.", outer="Bursts from egg sacs in swamp hollows.", fit=5,
  pros="Melee-friendly fodder that supports “every bullet counts”; the shotgun fantasy.",
  cons="Crowds of skinned meshes cost CPU: keep counts and tris low.", suggest="The Hive's base unit.",
  assets=["ipbr_spiders", "dd_spider"], refs=["Deep Rock Galactic: Glyphid swarmers", "Doom (2016): chainsaw fodder pays ammo"])
U(id="bug_spitter", group="bug", name="Acid Spitter", roles=["Lobber"], tier="T2", domain="Ground", phase="EA1", pick="core",
  size=1.3, scale="Scorpion LP x0.8", hp=150, armor="Front carapace x0.5", weak="Glowing tail sac x2.5", dmg="15 acid glob + pool 4 s",
  speed="2; keeps 15–30 m", spot="30 m", power=2.5,
  tell="Tail arches and glows for 1 s.", counter="Flank it, or snipe the tail sac with the AK.",
  raid="Shells the yard from beyond the fence.", outer="Guards nests.", fit=5,
  pros="A weak-point enemy from an owned model.", cons="Shares acid effects with the zombie Spitter.",
  suggest="The Hive's ranged unit.", assets=["ipbr_scorpion"], refs=["Doom Eternal: destructible weak points"])
U(id="bug_wasp", group="bug", name="Battle Wasp", roles=["Flyer", "Rusher"], tier="T1", domain="Air", phase="EA1", pick="core",
  size=0.8, scale="Battle Bee x0.55", hp=40, dmg="12 sting + poison 3/s for 3 s", speed="7, erratic; dive 10", spot="25 m", power=1,
  tell="Buzz pitch rises and it hovers still for 0.6 s before the dive.", counter="MP5 or shotgun at the hover pause.",
  raid="Comes over the fence in swarms of 3–5.", outer="Hives in hollow trees.", fit=4,
  pros="A small flyer with owned flight clips.", cons="The Dungeon Mason look is cuter than Synty: a store wasp may fit better (see options).",
  suggest="The Hive's flyer.", assets=["dm_battlebee", "ipbr_bomberbug"], refs=["Deep Rock Galactic: Mactera flyers"])
U(id="bug_worm", group="bug", name="Burrower", roles=["Ambusher", "Heavy", "Breaker"], tier="T3", domain="Ground", phase="Update", pick="core",
  size=4.0, scale="Giant Worm LP x0.6", hp=600, armor="Immune underground", weak="Mouth x2 while emerged", dmg="40 bite; erupting under a fence breaks it",
  speed="4 underground", spot="20 m (vibration)", power=5,
  tell="A dirt trail, a rumble and cracking ground 1.5 s before it erupts.", counter="Step off the cracks, then unload into the mouth.",
  raid="Comes up inside the yard or under a fence: a breach with no fence fight.", outer="Swamp mud flats.", fit=4,
  pros="Owned clips; the “appear inside the meadow” route.", cons="Burrowing is a new movement type (hide + travel effect).",
  suggest="Swamps update.", assets=["ipbr_worm"], refs=["Level Up!: burrowers need a usable warning and an exposed interval"])
U(id="bug_brood", group="bug", name="Brood Mother", roles=["Heavy", "Lobber"], tier="Region boss", domain="Ground", phase="EA1", pick="core",
  size=2.5, scale="Spiders LP x2.2, 5 m long", hp=9000, weak="3 egg sacs x3, glowing abdomen x2", dmg="Leap slam 50; web (slows); acid spray",
  speed="3 / leap", spot="Arena", power=0,
  tell="Rears up before the leap; the sacs pulse before they hatch.",
  counter="Burst the egg sacs before they hatch; axe the spiderlings; shoot the abdomen during the spray.",
  raid="After the first kill, a weaker Mother joins Blood Moons.", outer="Swamps boss. Phases: web and leaps → sacs hatch waves → acid frenzy.", fit=5,
  pros="Owned clips include jumps and a cast; the sacs make target priority the fight.", cons="A big spider in first person needs a readable arena.",
  suggest="Swamps boss.", assets=["ipbr_spiders", "dd_spider"], refs=["Destiny: add phases between damage phases"])

U(id="myt_gargoyle", group="myt", name="Gargoyle", roles=["Ambusher", "Flyer", "Caster"], tier="T2", domain="Air", phase="Update", pick="core",
  size=2.1, scale="as is", hp=200, armor="Immune as a statue", weak="Wings x1.5", dmg="25 dive; stone shard volley 3 x 10", speed="6 flying", spot="20 m", power=3,
  tell="The statue cracks and its eyes glow before it wakes; a wing beat before each dive.",
  counter="Shoot it in the air; watch the statues.", raid="Perches on the house roof as a statue and wakes when the player walks past.",
  outer="Ruins and cliff edges in the Highlands.", fit=5,
  pros="Its owned clips cover statue → wake → fly → cast. “The statue moved” is a great reveal.",
  cons="Flying movement; the stone invulnerability must read clearly (sparks, ping sound).", suggest="Mythics' flyer.",
  assets=["ipbr_gargoyle"])
U(id="myt_eye", group="myt", name="Flying Eye", roles=["Caster", "Flyer"], tier="T2", domain="Air", phase="Update", pick="core",
  size=1.0, scale="Flying Eye LP x0.55", hp=120, weak="Eye x3; hitting it interrupts the beam", dmg="Beam 20/s while channelled", speed="3 hovering; keeps 25–40 m",
  spot="40 m", power=2.5,
  tell="A thin targeting line that thickens for 1.5 s, with a rising hum.", counter="Break line of sight or shoot the eye during the charge.",
  raid="Hovers beyond the fence and beams the player.", outer="Guards Highlands ruins.", fit=5,
  pros="A fair sniper: the charge is long, visible and interruptible.", cons="Beam effect and line-of-sight checks.",
  suggest="Mythics' caster.", assets=["ipbr_eye"], refs=["Ranged fairness: visible, interruptible tells (Halo, Doom)"])
U(id="myt_golem", group="myt", name="Stone Golem", roles=["Heavy", "Blocker", "Breaker"], tier="T3", domain="Ground", phase="Update", pick="core",
  size=2.8, scale="x1.4", hp=800, armor="Stone x0.25", weak="Rune core x3, open during the slam wind-up", dmg="40 slam with a shockwave", speed="1.2",
  spot="15 m", power=7,
  tell="The core glows and the arms rise for 1.2 s.", counter="Wait for the core, then pistol or AK into it.",
  raid="Walks through fences.", outer="Guards Highlands passes.", fit=5,
  pros="Rewards accuracy over spraying; owned in two styles.", cons="Bullets bouncing off must be obvious (sparks, sound).",
  suggest="Mythics' heavy.", assets=["riv_golem", "ipbr_golems"], refs=["Doom Eternal: weak points", "Deep Rock Galactic: armour you shoot off"])
U(id="myt_slime", group="myt", name="Slime", roles=["Swarm"], tier="T0", domain="Ground", phase="Later", pick="bench",
  size=1.0, scale="x0.85", hp=60, weak="—", dmg="8; splits 60 → 2 x 30 → 4 x 15", speed="2 hops", spot="10 m", power=1,
  tell="Wobbles before each hop.", counter="Shotgun or axe; bullets just make more of them.",
  raid="Oozes under the gate.", outer="Ponds and caves.", fit=3,
  pros="Funny game reference; makes the shotgun and melee shine.", cons="Splitting adds spawns mid-fight.",
  suggest="A later joke enemy.", assets=["ipbr_slime"])
U(id="myt_plant", group="myt", name="Mandrake", roles=["Ambusher", "Caster"], tier="T2", domain="Ground", phase="Later", pick="bench",
  size=1.4, scale="as is", hp=150, dmg="Thorn bolt 15", speed="1", spot="8 m (asleep as a plant)", power=2,
  tell="Leaves twitch, then it comes alive (own clip).", counter="Shoot plants that twitch.",
  raid="Disguised among the crops.", outer="Swamp edges.", fit=4,
  pros="Owned clips for “comes alive” and a magic charge; a crop-field ambush fits a farm.", cons="Must not punish the player for farming.",
  suggest="Bench: fun but niche.", assets=["ipbr_plant"])
U(id="myt_dragon", group="myt", name="Dragon", roles=["Flyer", "Heavy", "Lobber"], tier="Region boss", domain="Air", phase="Update", pick="core",
  size=4.0, scale="LP x1.3, ~12 m wingspan", hp=10000, weak="Wings x1.5 (break them to ground it), head x2",
  dmg="Fire-breath strafe runs, dive, tail whip, landing stomp", speed="12 flying", spot="Arena", power=0,
  tell="Inhales with a glowing throat before the breath; circles wide before a dive.",
  counter="Shoot the wings during strafe runs: it falls (StunnedFlying) into a ground phase.",
  raid="After the first kill, a weaker dragon flies over Blood Moons.", outer="Highlands boss. Phases: air strafes → wings broken, ground breath and tail → takes off again at 40%.", fit=5,
  pros="The dragon you asked for, and its 48 owned clips cover the whole fight, stunned states included.",
  cons="A flying boss is the hardest AI and arena work: save it for the Highlands update.", suggest="Highlands boss.",
  assets=["ipbr_dragons", "ipbr_bonedragon"])

U(id="dino_compy", group="dino", name="Compy Swarm", roles=["Swarm", "Thief"], tier="T0", domain="Ground", phase="Later", pick="core",
  size=0.4, scale="tiny", hp=20, weak="—", dmg="4 nip", speed="6", spot="12 m", power=0.25,
  tell="Chirping from the grass.", counter="Axe or one pellet; they scatter from a shotgun blast.",
  raid="Darts into the hen house and steals eggs.", outer="Underfoot everywhere in the valley.", fit=4,
  pros="Swarm fodder with a farm job (eggs).", cons="Another swarm next to Spiderlings and Shamblers.",
  suggest="Only if Dinosaurs replace the Hive.", assets=[], missingNote="Store dino pack")
U(id="dino_raptor", group="dino", name="Raptor", roles=["Flanker", "Rusher"], tier="T2", domain="Ground", phase="Later", pick="core",
  size=1.6, scale="~3 m long", hp=90, dmg="15 claw; pounce 25", speed="8 (faster than a sprint)", spot="30 m", power=1.5,
  tell="A hiss and a crouch 0.7 s before the pounce; packs of three call to each other.",
  counter="Shotgun the pounce; back into a corner so only one can reach you.",
  raid="Jumps the fence: the one raider fences don't stop, so the player has to defend from inside.",
  outer="Hunts in packs of three; one distracts, two flank.", fit=5,
  pros="The best flanker an FPS has (Turok, Jurassic Park); jumping fences gives raids a new problem.",
  cons="Faster than a sprinting player: needs a clear pounce tell and a recovery window.", suggest="The dinos' core unit.",
  assets=[], missingNote="Store dino pack", refs=["Jurassic Park: raptors flank (“clever girl”)"])
U(id="dino_spitter", group="dino", name="Spitter Dino", roles=["Lobber", "Shooter"], tier="T1", domain="Ground", phase="Later", pick="core",
  size=1.3, scale="dilophosaurus-like", hp=80, dmg="10 venom; blurs the screen 2 s", speed="3", spot="25 m", power=1.5,
  tell="The neck frill flares open and rattles for 1 s.", counter="Shoot it while the frill is open; break line of sight.",
  raid="Spits over the fence at the player.", outer="Ambushes from ferns at the valley edge.", fit=4,
  pros="A ranged dino with a famous, readable tell.", cons="Screen blur must be short and clear, or it's just annoying.",
  suggest="The dinos' ranged unit.", assets=[], missingNote="Store dino pack")
U(id="dino_ptero", group="dino", name="Pteranodon", roles=["Flyer", "Thief"], tier="T1", domain="Air", phase="Later", pick="core",
  size=1.2, scale="~5 m wingspan", hp=60, dmg="12 beak", speed="10 diving", spot="45 m", power=1.2,
  tell="A shriek and a long glide toward the target.", counter="Shotgun or MP5 in the glide; a hit drops what it carries.",
  raid="Carries off chickens and lambs.", outer="Cliff colonies.", fit=4,
  pros="A big, readable flyer.", cons="Same job as the Chicken Hawk: keep one of the two.",
  suggest="Replaces the Chicken Hawk if Dinosaurs replace Beasts.", assets=[], missingNote="Store dino pack")
U(id="dino_trike", group="dino", name="Triceratops", roles=["Heavy", "Breaker", "Blocker"], tier="T3", domain="Ground", phase="Later", pick="core",
  size=2.5, scale="~7 m long", hp=900, armor="Frill and horns x0.5 from the front", weak="Flank and back x1.5", dmg="45 charge, knockback; x5 on gates",
  speed="1.5 / charge 7", spot="15 m", power=8,
  tell="Lowers its head and paws the ground for 1.5 s.", counter="Side-step the charge and shoot the flank while it turns.",
  raid="Rams the gate: the biggest single breach in the game.", outer="Herds that only attack when provoked.", fit=4,
  pros="Charge → dodge → punish, plus a frontal armour that asks for another angle.", cons="Three chargers (Boar, Zombie Brute, Triceratops) is too many: keep two.",
  suggest="The dinos' heavy.", assets=[], missingNote="Store dino pack")
U(id="dino_rex", group="dino", name="T-Rex", roles=["Heavy", "Rusher"], tier="Region boss", domain="Ground", phase="Later", pick="core",
  size=5.5, scale="~12 m long", hp=12000, weak="Open mouth during the roar x3, eyes x2", dmg="Bite 60, tail sweep 40, roar stagger",
  speed="2 / charge 9", spot="Arena", power=0,
  tell="Stops, rears and inhales before the roar; the tail swings back before a sweep.",
  counter="Shoot into the mouth during the roar; hide behind solid things during the charge.",
  raid="After the first kill, a weaker T-Rex stomps through Blood Moons.", outer="Region boss of a dino valley, or the eclipse giant instead of the Kaiju.", fit=5,
  pros="The most famous movie monster there is: the AI sandbox's best joke and a great fight.",
  cons="Needs a store asset with good boss clips (roar, bite, tail).", suggest="Region boss if Dinosaurs come in.",
  assets=[], missingNote="Store dino pack")

U(id="ali_garoo", group="ali", name="Garoo", roles=["Rusher", "Flanker"], tier="T1", domain="Ground", phase="Later", pick="core",
  size=1.6, scale="as is", hp=80, dmg="12 kick", speed="7 in long hops", spot="25 m", power=1.2,
  tell="Crouches and its tail coils before each long hop.", counter="Shotgun it on landing.",
  raid="Hops the fence in pairs.", outer="Herds at the crash site.", fit=4,
  pros="A hopper that clears fences, in a 5/5 Synty match.", cons="Same fence-jumping job as the Raptor: keep one.",
  suggest="The Invaders' fodder.", assets=["st_118233"])
U(id="ali_telekinis", group="ali", name="Telekinis", roles=["Caster", "Flyer"], tier="T2", domain="Air", phase="Later", pick="core",
  size=1.2, scale="as is", hp=90, weak="Brain dome x2", dmg="Psychic orb 12; hurls loose crates and rocks", speed="3 floating", spot="30 m", power=2.5,
  tell="Its tentacles glow and nearby objects lift for 1 s.", counter="Shoot it while it lifts; the object drops.",
  raid="Floats over the fence and throws your own produce crates at you.", outer="Drifts above the crater.", fit=5,
  pros="A caster with a farm twist: your yard becomes its ammo.", cons="Thrown physics objects need care so they can't wreck the base.",
  suggest="The Invaders' caster.", assets=["st_120257"])
U(id="ali_sicklus", group="ali", name="Sicklus", roles=["Flyer", "Shooter"], tier="T1", domain="Air", phase="Later", pick="core",
  size=1.0, scale="as is", hp=50, dmg="Spine volley 3 x 8", speed="8", spot="35 m", power=1.2,
  tell="Stops and flares its blades for 0.7 s before a volley.", counter="MP5 during the stop.",
  raid="Strafes the yard in pairs.", outer="Circles the crash site.", fit=4,
  pros="A ranged flyer with its own clips.", cons="Air-only clip set: it never lands.", suggest="The Invaders' flyer.", assets=["st_121274"])
U(id="ali_serpent", group="ali", name="Serpent", roles=["Ambusher", "Lobber"], tier="T2", domain="Ground", phase="Later", pick="core",
  size=2.0, scale="~6 m long", hp=300, weak="Mouth x2 while reared", dmg="Acid spit 15; bite 30", speed="4 underground", spot="20 m", power=3,
  tell="Rears up and flares its hood for 1 s.", counter="Shoot into the mouth while it's reared.",
  raid="Tunnels under a fence and surfaces in the yard.", outer="Sand dunes at the crash site.", fit=4,
  pros="Burrower and spitter in one model.", cons="Burrowing is a new movement type.", suggest="The Invaders' ambusher.", assets=["st_122280"])
U(id="ali_metalon", group="ali", name="Metalon", roles=["Heavy", "Breaker"], tier="T3", domain="Ground", phase="Later", pick="core",
  size=2.2, scale="as is", hp=700, armor="Front shell x0.5", weak="Glowing back vents x2", dmg="Ram 40; x4 on fences", speed="1.5 / ram 6", spot="15 m", power=6,
  tell="Lowers its head and clicks its mandibles for 1.2 s.", counter="Side-step the ram and shoot the vents.",
  raid="Rams the gate.", outer="Guards the crashed ship.", fit=4,
  pros="Updated in 2025 with URP support; also a Brood Mother stand-in.", cons="Big and spidery like the Brood Mother.", suggest="The Invaders' heavy.",
  assets=["st_151383"])
U(id="ali_magantis", group="ali", name="Magantis", roles=["Heavy", "Caster"], tier="Miniboss", domain="Ground", phase="Later", pick="core",
  size=3.0, scale="as is", hp=1800, weak="Abdomen sac x3", dmg="Claw 35; spawns 4 Magantees", speed="2", spot="30 m", power=30,
  tell="Its abdomen pulses before a spawn.", counter="Hit the sac before it spawns; axe the Magantees.",
  raid="Leads Invader Blood Moons.", outer="Nests at the crash site.", fit=5,
  pros="A summoner miniboss that ships with its own minion.", cons="Summoners add enemies: cap the spawns.", suggest="The Invaders' miniboss.",
  assets=["st_120098"], refs=["DOOM Eternal: Arch-vile summons until killed"])

U(id="drk_weeper", group="drk", name="Weeper", roles=["Ambusher", "Caster"], tier="T2", domain="Ground", phase="Update", pick="core",
  size=1.9, scale="as is", hp=150, armor="Only hurt while lit", weak="—", dmg="Touch 30 + sanity drain", speed="Moves only while unlit or unseen", spot="—", power=0,
  tell="A whisper; the nearest lamp flickers.", counter="Keep it in torch or flashlight light, then shoot it.",
  raid="Eclipse day: comes into the meadow with the Dark.", outer="Outer zone at night: what drains sanity in the dark.", fit=5,
  pros="Gives “light protects against the Dark” an enemy; owned statue and cast clips.",
  cons="New visibility rules; must not feel unfair in the dark.", suggest="The Dark's first entity.",
  assets=["ipbr_weeper"], refs=["Don't Starve: Charlie", "Doctor Who: Weeping Angels (moves when unseen)"])
U(id="drk_hunter", group="drk", name="Night Hunter", roles=["Ambusher", "Rusher"], tier="T3", domain="Ground", phase="Later", pick="core",
  size=2.4, scale="x1.2", hp=700, weak="Head x2", dmg="40 claw; drags the player a few metres", speed="9 in the dark, 3 in light", spot="30 m in the dark",
  power=0, tell="A clicking call from the trees; it hisses before it lunges.", counter="Light slows it to a walk; a shotgun to the head as it lunges.",
  raid="— (outer zone only)", outer="The outer zone's night apex: hunts players who stay out after dark, so dusk means “go home”.", fit=5,
  pros="Gives night its own apex threat (Dying Light's Volatiles) and makes light matter.", cons="Must stay rare and readable or the outer zone stops being worth visiting late.",
  suggest="With the outer-zone-at-night content.", assets=["syn_alien"], refs=["Dying Light: Volatiles own the night"])
U(id="drk_bats", group="drk", name="Bat Swarm", roles=["Flyer", "Swarm"], tier="T0", domain="Air", phase="Update", pick="core",
  size=0.6, scale="1 m wingspan", hp=20, weak="\u2014", dmg="5 bite", speed="8, erratic", spot="20 m; drawn to light", power=0.3,
  tell="Squeaks and a rush of wings; they stream toward the nearest light.", counter="Shotgun or axe; douse your torch and they lose you.",
  raid="Blood Moons and the eclipse: they pour out of the ridge caves at dusk.", outer="Roost in caves and burst out when a light comes in.", fit=4,
  pros="A flying swarm, both things you asked for, with owned roost and flight clips; light-driven behaviour ties into the Dark.",
  cons="Many flyers at once cost CPU (flying movement plus skinned meshes): keep them in small clouds.",
  suggest="The Dark's swarm, and cave dressing that bites.", assets=["ipbr_bats"], refs=["Minecraft: Phantom punishes not sleeping"])
U(id="drk_specter", group="drk", name="Specter", roles=["Ambusher", "Caster"], tier="T2", domain="Air", phase="Later", pick="bench",
  size=2.0, scale="x0.65", hp=120, armor="Only hurt while lit", dmg="Wail 15 (area)", speed="3 floating", spot="20 m", power=0,
  tell="Cold mist and a rising wail.", counter="Light, then shoot.", raid="Eclipse day.", outer="Night graveyards.", fit=3,
  pros="Owned ghost with clips.", cons="Cute Polyart look.", suggest="A second Dark entity if needed.", assets=["dm_specter"])

U(id="gnt_kaiju", group="gnt", name="Eclipse Kaiju", roles=["Heavy", "Lobber"], tier="Giant", domain="Ground", phase="EA1", pick="core",
  size=10.0, scale="Kaiju x5", hp=20000, weak="4 armour plates on forearms and shins (2000 each), then the chest crystal x3",
  dmg="Stomp shockwave, rock throw, roar that calls adds", speed="1.5 (walks across the meadow)", spot="Arena", power=0,
  tell="Lifts a foot for 1.5 s before the stomp; the crystal glows when exposed.",
  counter="Strip the plates, then the crystal; ammo crates spawn in the yard.",
  raid="The eclipse's one giant boss: walks out of the sea toward the farm.", outer="—", fit=5,
  pros="Owned; humanoid, so KI Humanoid Giant drives it; 4 looks = one per eclipse.",
  cons="Weak points must stay in view at 10 m: make it kneel to slam.", suggest="Eclipse boss.",
  assets=["kaiju"], refs=["Doom Eternal: Icon of Sin", "Shadow of the Colossus: design the creature with its arena"])
