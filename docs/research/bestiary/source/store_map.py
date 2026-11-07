# Pack cards (owned now; store and Synty-pass entries are appended below once research lands).
from PIL import Image as _Im, ImageEnhance as _En
_RD = r"C:\Users\NhNg\AppData\Local\Temp\claude\2b06a147fe23b4c2\225d606b-acac-46db-af5c-d5ef84cdcf61\scratchpad\renders"
_MD = os.path.join(SCR, "eco", "mosaics"); os.makedirs(_MD, exist_ok=True)

def _mosaic(key, ids, cols=3, tile=180):
    out = os.path.join(_MD, key + ".jpg")
    rows = (len(ids) + cols - 1) // cols
    m = _Im.new("RGB", (cols * tile, rows * tile), (58, 65, 66))
    for i, k in enumerate(ids):
        p = os.path.join(_RD, k + ".png")
        if os.path.exists(p):
            t = _En.Brightness(_Im.open(p).convert("RGB").resize((tile, tile), _Im.LANCZOS)).enhance(1.1)
            m.paste(t, ((i % cols) * tile, (i // cols) * tile))
    m.save(out, quality=82)
    EXTRA_IMGS[key] = out
    return key

def P(id, name, source, what, uses="", tag="", style=0, verdict="", notes="", url="", folder="", imgs=None, img=None, price=0):
    if imgs:
        img = _mosaic("mosaic_" + id, imgs, cols=3 if len(imgs) > 4 else 2)
    PACKS.append(dict(id=id, name=name, source=source, sourceLabel={"Owned": "Owned", "SyntyPass": "Synty pass", "Buy": f"Buy ${price:g}"}.get(source, source),
                      what=what, uses=uses, tag=tag, style=style, verdict=verdict, notes=notes, url=url, folder=folder, img=img, price=price))

P("sk", "Synty Sidekick character packs", "Owned", "Goblin Fighters and Species, Apocalypse Zombies, Sci-Fi Robots, Fantasy Skeletons: modular humanoids built with SidekickKit like the in-game goblins.",
  uses="Goblin Sneak, Grunt, Archer, Brute; Gunner Bot, Shield Bot; zombie elites", tag="Undecided / Could", style=5, verdict="Core",
  notes="Sidekick's Resources folder (~180 MB) ships in builds while the pack is in the project: decide before a release build.",
  folder="Assets/Synty/SidekickCharacters", imgs=["sk_gobfighter_01", "sk_gobspecies_02", "sk_robot_01", "sk_robot_04", "sk_zomb_02", "sk_skel_01"])
P("cz", "POLYGON City Zombies", "Owned", "50 fixed zombie civilians at 2\u20133.6k tris, one material each.", uses="Shambler (the horde)", tag="Could", style=5, verdict="Core",
  folder="Assets/Synty/PolygonCityZombies", imgs=["cz_businessman", "cz_firefighter", "cz_clown", "cz_hotdogsuit"])
P("bz", "POLYGON Boss Zombies", "Owned", "Four big zombies with hair and tiara attachments.", uses="Bloater, Spitter, Zombie Brute, Crawler, Zombie Prom Queen",
  tag="Could", style=5, verdict="Core", folder="Assets/Synty/PolygonBossZombies", imgs=["bz_blobber", "bz_slobber", "bz_brute", "bz_wretch"])
P("dog", "POLYGON Dog Pack", "Owned", "About 30 dogs on one Generic rig with 88 clips: wolf, coyote, hellhound, zombie, robot and sci-fi dogs.",
  uses="Wolf, Robot Hound, Zombie Dog, Hellhound elite", tag="Could", style=5, verdict="Core: one controller for four units",
  folder="Assets/Synty/PolygonDog", imgs=["dog_wolf", "dog_robot", "dog_zombie_doberman", "dog_hellhound"])
P("gwc", "POLYGON Goblin War Camp", "Owned", "Goblin camp: characters (troll, shaman, archer, king), buildings, props.", uses="Goblin Shaman, War Troll; camp dressing",
  tag="Undecided \u2192 suggest Could", style=4, verdict="Core", notes="Flat POLYGON goblins look a little different from the Sidekick ones.",
  folder="Assets/Synty/PolygonGoblinWarCamp", imgs=["gwc_troll", "gwc_shaman", "gwc_archer", "gwc_king"])
P("riv", "POLYGON Fantasy Rivals", "Owned", "20 fantasy bosses and rivals on Humanoid rigs.", uses="Stone Golem; troll and ork alternates",
  tag="Could", style=5, verdict="Alternates", notes="Human-looking rivals (dwarf, dark elf, witch, slayer, queen) are out under the no-humans rule.",
  folder="Assets/Synty/PolygonFantasyRivals", imgs=["riv_elementalgolem", "riv_fortgolem", "riv_mechanicalgolem", "riv_troll", "riv_bigork", "riv_reddemon"])
P("ww", "POLYGON Werewolf", "Owned", "Two werewolves (normal, undead) on a Humanoid rig.", uses="Werewolf (Blood Moon miniboss)", tag="Could", style=5, verdict="Core",
  folder="Assets/Synty/PolygonWerewolf", imgs=["ww_werewolf", "ww_undead"])
P("kaiju", "POLYGON Kaiju", "Owned", "Four giant monsters on Humanoid rigs with tails.", uses="Eclipse Kaiju (one look per eclipse)", tag="Could", style=5, verdict="Core",
  folder="Assets/Synty/PolygonKaiju", imgs=["kaiju_01", "kaiju_02", "kaiju_03", "kaiju_04"])
P("mech", "POLYGON Mech", "Owned", "Six mechs (Humanoid) with 104 attachments and 12 mech-scale weapons.", uses="The Overseer (final boss)", tag="Could", style=5, verdict="Core",
  notes="68\u201393k tris: one at a time.", folder="Assets/Synty/PolygonMech", imgs=["mech_04", "mech_01", "mech_05", "mech_06"])
P("ipbr", "Magic Pig Games (Infinity PBR), LP prefabs", "Owned", "38 monster packs. Their low-poly LP prefabs use a flat-colour URP shader and sit close to Synty; most have 12\u201348 clips, flying ones included.",
  uses="Spiderling alt, Acid Spitter, Burrower, Brood Mother, Gargoyle, Flying Eye, Golem alt, Slime, Mandrake, Dragon, Bone Dragon, Weeper",
  tag="Could (LP prefabs only)", style=4, verdict="Core for the Hive and Mythics",
  notes="Use the _LP prefabs; the PBR and _HSL ones don't match (HSL is pink in URP). Their shared scripts and demo scripts stay out of _Game.",
  folder="Assets/_StoreAssets/Characters/Magic Pig Games (Infinity PBR)", imgs=["ipbr_gargoyle", "ipbr_dragons", "ipbr_flyingeye", "ipbr_scorpion", "ipbr_spiders", "ipbr_weeper"])
P("dm_rpg", "Dungeon Mason RPG Monster Bundle Polyart (waves 1\u20133)", "Owned", "About 35 Polyart monsters with 15\u201319 clips each (bat, battle bee, dragon, beholder, specter, golem, worm\u2026).",
  uses="Battle Wasp; Specter; alternates", tag="Missing from ASSETS.md \u2192 suggest Maybe", style=3, verdict="Alternates",
  notes="Flat colours but cute RPG proportions and saturated colours. Its folder sits inside the declared Dungeon Mason folder, so the pack check doesn't flag it.",
  folder="Assets/_StoreAssets/Characters/Dungeon Mason/RPGMonsterBundlePolyart", imgs=["dm_battlebee", "dm_bat", "dm_dragon", "dm_beholder", "dm_specter", "dm_golem"])
P("dm_swarm", "Dungeon Mason Monster Swarm Survivor Polyart", "Owned", "Ten small swarm monsters with move, attack, hit and die clips, made for crowds.", uses="None picked",
  tag="Undecided \u2192 suggest Maybe", style=3, verdict="Bench", folder="Assets/_StoreAssets/Characters/Dungeon Mason/Monster Swarm Survivor Polyart",
  imgs=["dm_swarm_01", "dm_swarm_05", "dm_swarm_07", "dm_swarm_10"])
P("malbers", "Malbers Animals Pack Forest (Poly Art)", "Owned", "Bear, boar, cougar, deer, fox, moose, rabbit, raccoon, tiger, wolf, with 30\u201348 Generic clips each.",
  uses="Boar, Bear, Night Stalker; wolf alternate", tag="Should", style=4, verdict="Core for beasts",
  notes="Models and clips only: never the Animal Controller prefabs or their AI.", folder="Assets/_StoreAssets/Characters/Malbers Animations",
  imgs=["mb_wolf", "mb_boar", "mb_bear", "mb_cougar"])
P("pp", "polyperfect Low Poly Animated Animals", "Owned", "~95 low-poly animals, farm and wild, with 3\u201315 clips each.", uses="Chicken Hawk, Spiderling, River Croc; wolf alternate",
  tag="Should", style=4, verdict="Core (few clips)", folder="Assets/_StoreAssets/Characters/polyperfect",
  imgs=["pp_eagle", "pp_vulture", "pp_spider_red", "pp_crocodile", "pp_wolf", "pp_bear_grizzly_wild"])
P("agor", "Agor2012 robots", "Owned", "Modular Robot (46 clips: carry, gun shots, saw attacks), Robot X-26-2 (26), Tracked Robot (20), Zombie (no clips).",
  uses="Repo Bot alternate, Siege Crawler (bench)", tag="Undecided \u2192 suggest Maybe", style=3, verdict="Alternates",
  notes="Shiny toy-robot look next to the Sidekick robots. All Generic rigs with their own clips, except the Zombie, which has none and is out.",
  folder="Assets/_StoreAssets/Characters/Agor2012", imgs=["agor_tracked", "agor_modular", "agor_x26"])
P("dd", "DeskDragons Spiders 1\u20133 and Swamp Monster 1\u20133", "Owned", "Six creatures with ~19\u201320 clips each.", uses="Alternates only",
  tag="Spiders Undecided, Swamp Monster missing from ASSETS.md \u2192 suggest Avoid", style=2, verdict="Skip",
  notes="Glossy painted look, 8\u201310k tris: the furthest from Synty here.", folder="Assets/_StoreAssets/Characters/DeskDragons",
  imgs=["dd_spider_1", "dd_spider_3", "dd_swamp_1", "dd_swamp_2"])
P("ki", "Kevin Iglesias animation packs", "Owned", "Melee, Basic Motions, Soldier, Crafting, Throwing, Spellcasting, Zombie, Humanoid Giant: humanoid clips that retarget onto every humanoid above.",
  uses="Every humanoid unit: attacks, guns, throws, casts, zombie walks, giant stomps", tag="Should / Could", style=5, verdict="Core",
  folder="Assets/_StoreAssets/Animation/Kevin Iglesias", imgs=["gob_current", "cz_firefighter", "kaiju_01", "sk_robot_05"])
P("bow", "Synty ANIMATION Bow Combat", "Owned", "499 archery clips per format, Sidekick-format included, with synced bow clips.", uses="Goblin Archer",
  tag="Could", style=5, verdict="Core", folder="Assets/Synty/AnimationBowCombat", imgs=["sk_gobfighter_02", "gwc_archer"])

# ---------- Synty pass (not installed: free to download, ask before importing) ----------
_SI = os.path.join(SCR, "research", "synty", "img")
def _simg(stem):
    for ext in (".png", ".jpg"):
        p = os.path.join(_SI, stem + ext)
        if os.path.exists(p):
            EXTRA_IMGS["syn_" + stem] = p
            CAPS["syn_" + stem] = "Synty store image (" + stem.rsplit("_", 1)[0].replace("-", " ") + ")"
            return "syn_" + stem
    return ""
_U = {u["id"]: u for u in UNITS}
def link(uid, key, primary=False):
    u = _U[uid]
    if key in u["assets"]:
        u["assets"].remove(key)
    u["assets"].insert(0, key) if primary else u["assets"].append(key)
SURL = "https://syntystore.com/products/"
def S(key, name, pack, handle, img, units, primary=(), note="", style=5, animq=4, cost="M", perf="few"):
    A(key, name=name, pack=pack, source="SyntyPass", tag="Not installed", rig="Humanoid (Mecanim, per the store)",
      anim="Humanoid retarget (KI, Synty clips)", clips=0, tris=0, native=0, style=style, animq=animq, cost=cost, perf=perf,
      img=_simg(img), url=SURL + handle, note=note)
    for uid in units:
        link(uid, key, primary=uid in primary)

S("syn_mutant", "POLYGON Apocalypse Wasteland \u00b7 glowing Mutants", "Synty POLYGON Apocalypse Wasteland", "polygon-apocalypse-wasteland",
  "polygon-apocalypse-wasteland_2", ["zom_bloater", "zom_shambler"], note="Glowing goop mutants: a radioactive zombie look that also fits an exploder.")
S("syn_giantmutant", "POLYGON Apocalypse Wasteland \u00b7 Giant Mutant", "Synty POLYGON Apocalypse Wasteland", "polygon-apocalypse-wasteland",
  "polygon-apocalypse-wasteland_1", ["zom_brute", "zom_queen"], note="Carries barrels in the key art: a lobber or a zombie miniboss.")
S("syn_behemoth", "POLYGON Dark Fortress \u00b7 Behemoth / Plague", "Synty POLYGON Dark Fortress (2025)", "polygon-dark-fortress",
  "polygon-dark-fortress_2", ["zom_brute", "zom_bloater"], note="Bloated plague undead with green glow; newest Synty undead.")
S("syn_wraith", "POLYGON Dark Fortress \u00b7 Wraith", "Synty POLYGON Dark Fortress (2025)", "polygon-dark-fortress",
  "polygon-dark-fortress_3", ["drk_weeper", "drk_specter"], note="A Humanoid walker: floating needs code or custom clips.")
S("syn_warrobot", "POLYGON Sci-Fi Space \u00b7 War Robot", "Synty POLYGON Sci-Fi Space", "polygon-sci-fi-space-pack",
  "polygon-sci-fi-space-pack_2", ["bot_enforcer"], note="A big combat robot, far lighter than a mech.")
S("syn_fancybot", "POLYGON Sci-Fi Cyber City \u00b7 Fancy Bot / Robot Mercenary", "Synty POLYGON Sci-Fi Cyber City", "polygon-sci-fi-cyber-city",
  "polygon-sci-fi-cyber-city_2", ["bot_repo", "bot_gunner"], note="The screen-faced Fancy Bot could show \u201cREPOSSESSED\u201d; it could also be a friendly service robot.")
S("syn_animatronic", "POLYGON Horror Carnival \u00b7 Animatronic", "Synty POLYGON Horror Carnival", "polygon-horror-carnival",
  "polygon-horror-carnival_2", ["drk_weeper"], note="A pink animatronic bunny with a chainsaw: a Five Nights at Freddy's-style \u201cmoves when unseen\u201d robot.")
S("syn_alien", "POLYGON Sci-Fi Horror \u00b7 Alien", "Synty POLYGON Sci-Fi Horror (2025)", "polygon-sci-fi-horror",
  "polygon-sci-fi-horror_2", ["drk_hunter"], primary=("drk_hunter",), note="Gaunt, toothed, long-clawed: the strongest night silhouette Synty has.")
S("syn_gargoyle", "POLYGON Dark Fantasy \u00b7 Gargoyle", "Synty POLYGON Dark Fantasy", "polygon-dark-fantasy",
  "polygon-dark-fantasy_2", ["myt_gargoyle"], animq=2, note="Humanoid with wings in the mesh: no flight clips, so it can only perch and walk.")
S("syn_bigdemon", "POLYGON Dungeon Realms \u00b7 Big Demon", "Synty POLYGON Dungeon Realms", "polygon-dungeon-realms",
  "polygon-dungeon-realms_2", ["myt_golem"], animq=3, note="Winged horned brute: a Mythics heavy or miniboss alternative.")

def SP(id, name, handle, img, what, uses, verdict, notes=""):
    P(id, name, "SyntyPass", what, uses=uses, tag="Not installed", style=5, verdict=verdict, notes=notes, url=SURL + handle, img=_simg(img))
SP("s_waste", "POLYGON Apocalypse Wasteland (2024)", "polygon-apocalypse-wasteland", "polygon-apocalypse-wasteland_2",
   "Glowing goop mutants and giant mutants, plus wasteland scrap props.", "Zombie variants: Bloater, Brute, Prom Queen alternatives", "Recommended",
   "Best fit for an apocalypse sandbox; its props could dress the junkyard too.")
SP("s_fort", "POLYGON Dark Fortress (2025)", "polygon-dark-fortress", "polygon-dark-fortress_3",
   "Behemoth, Colossus, Plague Lord, plague undead, a wraith, siege props.", "Zombie heavies, the Dark's wraith", "Recommended")
SP("s_space", "POLYGON Sci-Fi Space", "polygon-sci-fi-space-pack", "polygon-sci-fi-space-pack_2",
   "A War Robot and big aliens (the rest are human).", "Enforcer (robot miniboss)", "Optional")
SP("s_cyber", "POLYGON Sci-Fi Cyber City", "polygon-sci-fi-cyber-city", "polygon-sci-fi-cyber-city_2",
   "Robot Mercenary, crash-dummy robots, the screen-faced Fancy Bot.", "Repo Bot, Gunner alternatives; possibly the friendly service robots", "Optional")
SP("s_horror", "POLYGON Sci-Fi Horror (2025)", "polygon-sci-fi-horror", "polygon-sci-fi-horror_2",
   "A xenomorph-like alien, skull-faced space corpses, a small alien critter (rig unknown).", "Night Hunter (the outer zone's night apex)", "Recommended")
SP("s_carnival", "POLYGON Horror Carnival", "polygon-horror-carnival", "polygon-horror-carnival_2",
   "An animatronic robot bunny and a sack-headed figure (the rest are human).", "Weeper alternative (the Dark)", "Optional")
SP("s_darkfantasy", "POLYGON Dark Fantasy", "polygon-dark-fantasy", "polygon-dark-fantasy_2",
   "Gargoyle, demons, five skeletons including a ranger (archer).", "Gargoyle alternative (no flight)", "Bench")
SP("s_realms", "POLYGON Dungeon Realms", "polygon-dungeon-realms", "polygon-dungeon-realms_2",
   "A winged Big Demon, demon skeletons, an undead knight.", "Mythics heavy alternative", "Bench")
SP("s_egypt", "POLYGON Ancient Egypt (2024)", "polygon-ancient-egypt", "polygon-ancient-egypt_2",
   "Mummies and animal-headed guardians.", "A possible undead group later", "Bench")
SP("s_worlds", "POLYGON Sci-Fi Worlds", "polygon-sci-fi-worlds", "polygon-sci-fi-worlds_2",
   "Alien brutes, a rock/crystal alien.", "Golem or alien alternatives", "Bench")

# ---------- Asset Store (buy) ----------
_RS = os.path.join(SCR, "research")
_ST = {str(x["id"]): x for x in json.load(open(os.path.join(_RS, "store", "store_packs.json"), encoding="utf-8"))}
_PB = {str(x["id"]): x for x in json.load(open(os.path.join(_RS, "pubs", "pubs_packs.json"), encoding="utf-8"))}

def _stimg(i, n):
    for d in ("store", "pubs"):
        for ext in (".jpg", ".png", ".jpeg", ".webp"):
            p = os.path.join(_RS, d, "img", f"{i}_{n}{ext}")
            if os.path.exists(p):
                key = f"st_{i}_{n}"
                EXTRA_IMGS[key] = p
                return key
    return ""

BUY = {}
def B(i, units=(), primary=(), animq=4, cost="M", perf="group", note="", verdict="Optional", uses="", tag=""):
    i = str(i)
    x = _ST.get(i) or _PB.get(i)
    price = float(x.get("price_now") or 0)
    imgs = [k for k in (_stimg(i, 1), _stimg(i, 2), _stimg(i, 0), _stimg(i, 3)) if k][:3]
    rig = str(x.get("rig") or "unknown").split("(")[0].strip() or "unknown"
    n = x.get("anim_count")
    src = x.get("anim_source") or ""
    anim = ("Own clips" + (f" ({n})" if n not in (None, "", "unknown", 0) else "") if "own" in src or "both" in src else "Humanoid retarget (KI, Synty clips)")
    keys = x.get("anims_key")
    if isinstance(keys, list):
        keys = ", ".join(str(k) for k in keys)
    if keys and "own" in anim.lower():
        anim += ": " + str(keys)[:150]
    key = "st_" + i
    tris = x.get("tris")
    try:
        tris_n = int(str(tris).replace(",", "").split()[0])
    except Exception:
        tris_n = 0
    A(key, name=x["name"], pack=f"{x.get('publisher')} · Asset Store", source="Buy", price=price, tag=tag or f"{x.get('publisher')}",
      rig=rig, anim=anim, clips=n if isinstance(n, int) else 0, tris=tris_n, native=0, style=int(x.get("synty_match") or 3), animq=animq,
      cost=cost, perf=perf, img=imgs[0] if imgs else "", imgs=imgs[1:], url=x.get("url", ""), note=note or str(x.get("notes") or "")[:180])
    for k in imgs:
        CAPS[k] = f"Asset Store image: {x['name']} ({x.get('publisher')}, ${price:g})"
    for uid in units:
        link(uid, key, primary=uid in primary)
    BUY[key] = dict(id=i, x=x, price=price, verdict=verdict, uses=uses, img=imgs[0] if imgs else "", note=note)

# robots
B(110904, ["bot_drone"], primary=("bot_drone",), animq=4, cost="L", perf="crowd", verdict="Recommended", uses="Scout Drone",
  note="Boxy flying robot: flies in four directions, two attacks, damage and death clips. Built-in material: make a URP copy.")
B(66991, ["bot_drone"], animq=3, cost="L", perf="crowd", uses="Scout Drone alternative", note="Hovering orb robot with shoot, hurt and die clips; cheapest drone.")
B(111732, ["bot_drone", "bot_enforcer"], animq=4, cost="L", perf="few", uses="Heavy gun drone; a flying Enforcer", note="Bigger flying gun robot with charge-and-blast; the vendor pitches it as a boss.")
B(409654, ["bot_gunner", "bot_hound", "bot_siege", "bot_enforcer"], animq=4, cost="M", perf="crowd", verdict="Optional",
  uses="Robot variety: Gunner, Hound, Salvo (siege), Titan (enforcer), Mender (healer), Spider",
  note="8 small mechs with idle, move, attack, hit and death each, ally and enemy colourways, wreck prefabs; tested on Unity 6000.6 with URP 17.6. New (Nov 2026), no ratings yet.")
B(414976, ["bot_hound", "bot_siege"], animq=4, cost="M", perf="crowd", uses="Gun walkers (hound or turret)",
  note="Modular quadruped walkers; a torso yaw bone lets the gun track the player; 5 palettes.")
B(117494, ["bot_siege"], animq=4, cost="M", perf="crowd", uses="Hide-and-shoot turret", note="Capsule robot that hides in its shell, then pops up and fires: a turret unit.")
B(163308, ["bot_gunner"], animq=5, cost="M", perf="group", uses="Gunner alternative", note="Chibi robot soldiers with 102 Humanoid clips (rifle, sniper, shotgun, grenadier). Try the free sample first.")
# hive
B(116394, ["bug_wasp"], primary=("bug_wasp",), animq=4, cost="L", perf="crowd", verdict="Recommended", uses="Battle Wasp",
  note="Faceted bee with fly and attack clips; a closer Synty match than the owned Battle Bee.")
B(114773, ["bug_spiderling"], animq=4, cost="M", perf="crowd", uses="Spiderling alternative", note="Black-and-red faceted spiders; rig not stated (the series is Generic).")
B(221108, ["bug_spiderling"], animq=3, cost="M", perf="crowd", verdict="Optional", uses="Spiderling alternative (free)", note="Free, very simple faceted spiders with 7 clips.")
B(350670, ["bug_worm"], animq=4, cost="L", perf="group", uses="Burrower alternative", note="Has burrow-in and burrow-out clips; cartoony look.")
B(151383, ["ali_metalon", "bug_brood"], primary=("ali_metalon",), animq=4, cost="M", perf="few", uses="Metalon; Brood Mother stand-in", note="Updated 2025, URP listed, 4.9 stars.")
# beasts
B(56933, ["bst_hawk"], animq=4, cost="L", perf="crowd", uses="Crow thief instead of a hawk", note="Toon crow with fly, two attacks, hit and death: a crow flock that steals seeds and chicks.")
B(295890, ["bst_stalker"], animq=5, cost="M", perf="crowd", uses="Night Stalker alternative", note="Armoured big cats with 27 clips.")
# mythics
B(325662, ["myt_golem"], animq=5, cost="M", perf="few", uses="Stone Golem alternative", note="Faceted rock golems carrying boulders; 43 clips incl. throw and large-object throw.")
B(116161, ["myt_golem"], animq=4, cost="M", perf="few", uses="Golem alternative", note="Humanoid rig with 20 own clips: our humanoid clips apply too.")
B(330372, ["myt_eye"], animq=4, cost="L", perf="group", uses="Flying Eye alternative", note="Has laser start / loop / end clips.")
B(339170, ["myt_gargoyle"], animq=4, cost="L", perf="few", uses="Gargoyle alternative", note="Cartoonier than the owned Magic Pig gargoyle.")
B(159934, ["myt_plant"], animq=4, cost="M", perf="crowd", uses="Pea Shooter: a plant turret", note="A Plants vs. Zombies joke the AI sandbox can make.")
B(160055, ["myt_plant"], animq=4, cost="M", perf="crowd", uses="Venus flytrap ambusher")
# dinosaurs
B(110313, ["dino_raptor", "dino_rex", "dino_trike", "dino_ptero", "dino_spitter", "dino_compy"],
  primary=("dino_raptor", "dino_rex", "dino_trike", "dino_ptero", "dino_spitter", "dino_compy"), animq=3, cost="M", perf="crowd", verdict="If dinosaurs",
  uses="Raptor, T-Rex, Triceratops, Pteranodon (+ Stegosaurus, Pachycephalosaurus, Brachiosaurus…)",
  note="Same faceted look as the owned polyperfect animals. Clip list isn't published, and the owned polyperfect pack has no hit clips: check after buying. No dilophosaurus.")
B(91130, ["dino_raptor", "dino_rex", "dino_trike", "dino_ptero"], animq=3, cost="M", perf="crowd", uses="Dinosaur alternative",
  note="2017, Built-in materials; idles, attack, run, eat and die listed, hit not listed. Includes park props.")
B(267769, ["dino_rex", "dino_trike"], animq=4, cost="M", perf="crowd", uses="Cheap dino test", note="T-Rex, Triceratops, Brontosaurus with babies; toon look; 16 clips.")
B(389710, ["dino_raptor"], animq=4, cost="M", perf="crowd", uses="Raptor alternative", note="A horned raptor-like hound with 10 clips.")
B(292810, ["dino_ptero"], animq=4, cost="L", perf="crowd", uses="Pteranodon alternative", note="Quirky toon pterodactyl with 19 clips.")
# invaders
B(118233, ["ali_garoo"], primary=("ali_garoo",), animq=4, cost="M", perf="crowd", verdict="If invaders", uses="Garoo")
B(120257, ["ali_telekinis"], primary=("ali_telekinis",), animq=5, cost="L", perf="crowd", verdict="If invaders", uses="Telekinis")
B(121274, ["ali_sicklus"], primary=("ali_sicklus",), animq=4, cost="L", perf="crowd", verdict="If invaders", uses="Sicklus")
B(122280, ["ali_serpent"], primary=("ali_serpent",), animq=5, cost="L", perf="crowd", verdict="If invaders", uses="Serpent")
B(120098, ["ali_magantis"], primary=("ali_magantis",), animq=5, cost="M", perf="few", verdict="If invaders", uses="Magantis + Magantee")
# the dark, giants, goblins
B(166160, ["drk_hunter"], animq=3, cost="M", perf="crowd", uses="Night Hunter alternative (Wendigo, Skinwalker)",
  note="Untextured flat-colour cryptids on Humanoid rigs: Wendigo, Bigfoot, Mothman, a wraith, a werewolf.")
B(116780, ["drk_bats"], animq=4, cost="L", perf="crowd", uses="Bat Swarm alternative", note="Faceted one-eyed bats, 14 clips.")
B(224216, ["drk_bats"], animq=2, cost="L", perf="crowd", uses="Bat Swarm alternative", note="Only 5 clips; 8 colours.")
B(243386, ["drk_specter"], animq=3, cost="M", perf="crowd", uses="Ghost alternative", note="Cute horned ghosts, 6 clips.")
B(321069, ["gnt_kaiju"], animq=4, cost="L", perf="boss", uses="Eclipse giant alternative (Cthulhu)", note="Winged Cthulhu with 19 clips; 29.8k tris.")
B(340684, ["gob_wartroll"], animq=4, cost="M", perf="group", uses="Troll alternative; skeleton archer and mage for a later undead group",
  note="11 undead, goblin and giant bipeds on Humanoid rigs, one palette texture, URP; built to sit next to Synty-style kits.")

for key, b in BUY.items():
    x = b["x"]
    P(b["id"], x["name"], "Buy", (b["note"] or str(x.get("notes") or ""))[:220], uses=b["uses"], tag=x.get("publisher", ""),
      style=int(x.get("synty_match") or 3), verdict=b["verdict"], url=x.get("url", ""), img=b["img"], price=b["price"],
      notes=("Bundle" if x.get("is_bundle") else "") )

# owned polyperfect spider fails the animation rule
for p_ in PACKS:
    if p_["id"] == "pp":
        p_["uses"] = "Chicken Hawk, River Croc; wolf alternate"
        p_["notes"] = "The spider has only walk, idle and scared clips (no attack or death), so it's out. Birds and the croc have attack and death but no hit clip."

# ---------- Lil Pupinduy Enemy Goblin (added to the project on 8 Nov, not yet in ASSETS.md) ----------
A("lp_goblin", name="Lil Pupinduy · Enemy Goblin (3 looks)", pack="Lil Pupinduy Enemy Goblin", source="Owned", tag="Missing from ASSETS.md",
  rig="Generic", anim="Own clips (41): 8-way walk and run, crouch walks, Take_Item, Attack1/2, stick attack, Hit, Squish, Lose, roll, jump, sit",
  clips=41, tris=8693, native=1.12, style=2, animq=5, cost="M", perf="group", img="lp_goblin_v1", imgs=["lp_goblin_v2"],
  note="A smooth, round gremlin with its own Shader Graph, not faceted low-poly. Take_Item, crouch and roll clips suit a thief; its V2 electricity effect script throws outside Play.")
link("gob_sneak", "lp_goblin")
CAPS["lp_goblin_v1"] = "Unity render: Lil Pupinduy Enemy Goblin V1 (1.1 m)"
CAPS["lp_goblin_v2"] = "Unity render: Lil Pupinduy Enemy Goblin V2"
P("lp", "Lil Pupinduy Enemy Goblin", "Owned", "Three round gremlin-goblins (1.1 m) with 41 Generic clips, including Take_Item, crouch walks, a roll and Squish.",
  uses="Goblin Sneak alternative (a thief with ready clips)", tag="Missing from ASSETS.md → suggest Maybe", style=2, verdict="Your call",
  notes="Added in your “+ add goblins” commit. Smooth toon shading next to faceted Synty; its own scripts, shaders and Studio scene stay out of _Game.",
  folder="Assets/_StoreAssets/Characters/Lil Pupinduy", img="lp_goblin_v1")
