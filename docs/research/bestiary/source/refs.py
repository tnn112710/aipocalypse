# Page text, design principles and references (from research/refs).
import re as _re
_R = os.path.join(SCR, "research", "refs")

for r in json.load(open(os.path.join(_R, "references.json"), encoding="utf-8")):
    REFS.append(dict(source=r["source"], topic=r["topic"], what=r["what_they_do"], takeaway=r["takeaway"], url=r.get("url", "")))

_txt = open(os.path.join(_R, "principles.md"), encoding="utf-8").read()
for m in _re.finditer(r"^\d+\.\s+\*\*(.+?)\*\*\s+(.+?)\s+\[(.+?)\]\s*$", _txt, _re.M):
    PRINCIPLES.append(dict(title=m.group(1).rstrip("."), text=m.group(2), src=m.group(3)))

_units = [u for u in UNITS]
_ea1 = [u for u in _units if u["phase"] in ("EA1", "Built")]
META.update(
    eyebrow="aipocalypse · enemy ecosystem proposal · 8 Nov 2026",
    lede=(f"{len(_units)} enemies: eight candidate raid groups, plus the Dark and the giants. Goblins, zombies and robots stay; "
          "Beasts, the Hive, Mythics, Dinosaurs and Invaders compete for the two or three new groups. Every enemy has one job, a tell and a counter, "
          "and shots-to-kill come from your real guns. Open a row for its card, mark <b>Keep</b>, <b>Maybe</b> or <b>Cut</b> and leave a note: "
          "your picks are saved with the page and I can read them back."),
    rosterNote=("Numbers are proposals. HP is fixed per unit (RAIDS.md). Shots to kill use the pistol (25), AK (20), pump shotgun (8 × 12, close range) "
                "and fire axe (35), with headshots ×2; bosses are counted in AK magazines (600 damage). Power is the wave-budget cost against "
                "the in-game goblin (1). Rating weighs gameplay fit 35%, Synty style match 20%, animation 15%, build cost 15% and performance 15%. "
                "Speeds are m/s against your walk 3.5 and sprint 6."),
    sizeNote=("Rendered in Unity at the suggested scale next to a 1.8 m farmer (the player's height). Store candidates aren't installed, "
              "so only owned models appear here; store images are in each card and in Packs."),
    packNote=("Synty first (free with your pass), then packs you already own, then store buys under $20 for a single pack and $60 for a bundle. "
              "Every candidate is animated or on a Humanoid rig that our clip libraries drive; static meshes were dropped. Mark Buy, Maybe or Skip on each store pack."),
    foot=("Built from CORE.md, RAIDS.md, WEAPONS.md and the player tuning assets; Unity renders of the installed packs; Asset Store and Synty store pages "
          "read on 8 Nov 2026 (prices change with sales); and the design sources above. Nothing in the project or the GDD was changed."),
)

QUESTIONS.extend([
    dict(id="groups", title="Which new raid groups?",
         html=("Goblins, zombies and robots stay. Pick two or three of <b>Beasts</b>, <b>the Hive</b>, <b>Mythics</b>, <b>Dinosaurs</b> and <b>Invaders</b> "
               "(5–6 groups in all, as you asked). Suggested: Beasts and the Hive in the first Early Access version (mostly owned art, both fit a farm), "
               "Mythics with the Highlands update (casters, golems, the dragon). Dinosaurs ($20) or Invaders (about $57–72 in singles) as a later region, "
               "or one of them instead of Beasts or Mythics.")),
    dict(id="regions", title="Which region introduces which group, and its boss?",
         html=("Suggested: <b>Pine Forest</b> introduces zombies and beasts (boss: Zombie Prom Queen); <b>Swamps</b> the Hive, with robots in the junkyard bog "
               "(boss: Brood Mother); <b>Highlands</b> the Mythics (boss: Dragon); robots belong to the final zone (The Overseer). This keeps RAIDS.md's "
               "“a boss kill calms its type and wakes the next region's type”.")),
    dict(id="bloodmoon", title="Who leads Blood Moons before any boss is beaten?",
         html=("RAIDS.md leaves this open. Suggested: the War Troll (goblins) and the Werewolf (beasts) take turns; after region bosses die, their weaker versions join as RAIDS.md says.")),
    dict(id="eclipse", title="Which giant for the eclipse?",
         html=("The Kaiju walks in from the sea (owned, Humanoid, four looks: one per eclipse). Alternatives: the Bone Dragon (owned, flies, can collapse and reassemble) "
               "or a T-Rex if Dinosaurs come in.")),
    dict(id="ammo", title="Should fodder pay ammo?",
         html=("DOOM's lesson for scarce ammo: some enemies must be part of the resupply, or players hoard ammo and melee everything. Suggested: a melee kill on fodder "
               "(Shambler, Spiderling, Compy) drops a few rounds or scrap; heavies drop more when killed through their weak point. This is new GDD material: confirm before it goes anywhere.")),
    dict(id="systems", title="Build order of the shared systems",
         html=("Most new units need one of two systems that don't exist yet: <b>enemy projectiles</b> (archer, gunner, both spitters, shaman, flying eye) and "
               "<b>flying movement</b> (drone, hawk, wasp, gargoyle, dragon). Suggested: projectiles first (six units), then the drone as the simplest flyer, then winged flyers. "
               "A third, the shared dog controller, serves wolves, robot hounds, zombie dogs and hellhounds.")),
    dict(id="elites", title="One elite language for every group?",
         html=("RAIDS.md already wants visible elites. Suggested: the same mark on every group (a coloured aura plus a name prefix), and each elite affix adds one visible behaviour "
               "(Risk of Rain 2, Destiny, Valheim stars), so players learn it once.")),
    dict(id="tags", title="Pack tags and the two packs missing from ASSETS.md",
         html=("<b>Dungeon Mason RPG Monster Bundle Polyart</b> (waves 1–3, about 35 monsters) and <b>DeskDragons Swamp Monster 1–3</b> are installed but not in "
               "ASSETS.md. The Magic Pig packs' low-poly (LP) prefabs match Synty far better than their store images suggest. Tag suggestions are on each pack card; "
               "I'll update ASSETS.md once you decide.")),
    dict(id="where", title="Where the agreed roster goes",
         html=("Per your workflow, agreed picks go to <code>docs/gdd/IDEAS.md</code> first; a RAIDS.md “Enemy types” update or a new ENEMIES.md only when you say so.")),
])

_L = os.path.join(SCR, "eco", "lineups")
for _n, _cap in (("L1", "Goblins and zombies next to the player (1.8 m). The in-game goblin is 2.1 m; the Sneak is scaled down, the Brute and War Troll up."),
                 ("L2", "Robots and beasts. The beasts are owned Synty, Malbers and polyperfect models; the hawk is shown perched."),
                 ("L3", "The Hive, Mythics and the Dark. The burrower only shows its upper part above ground."),
                 ("L4", "Minibosses and bosses: from the War Troll (2.9 m) to the Eclipse Kaiju (10 m). The Brood Mother is about 5 m long.")):
    EXTRA_IMGS["lineup_" + _n] = os.path.join(_L, _n + "_lab.jpg")
    LINEUPS.append(dict(img="lineup_" + _n, caption=_cap))
