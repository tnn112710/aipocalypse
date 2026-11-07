# Builds aipocalypse-bestiary.html from data.py (+ store_map.py, refs.py when present).
import base64, io, json, math, os, sys
from PIL import Image, ImageEnhance

HERE = os.path.dirname(os.path.abspath(__file__))
SCR = os.path.dirname(HERE)
RENDERS = [os.path.join(HERE, "renders"),
           r"C:\Users\NhNg\AppData\Local\Temp\claude\2b06a147fe23b4c2\225d606b-acac-46db-af5c-d5ef84cdcf61\scratchpad\renders"]

ns = {}
exec(open(os.path.join(HERE, "data.py"), encoding="utf-8").read(), ns)
GROUPS, ROLES, UNITS, ASSETS, GUNS = ns["GROUPS"], ns["ROLES"], ns["UNITS"], ns["ASSETS"], ns["GUNS"]
PACKS, LINEUPS, PRINCIPLES, REFS, QUESTIONS, META = [], [], [], [], [], {}
EXTRA_IMGS = {}   # key -> absolute path for store / synty images
CAPS = {}
for extra in ("store_map.py", "refs.py"):
    p = os.path.join(HERE, extra)
    if os.path.exists(p):
        ns2 = dict(ASSETS=ASSETS, UNITS=UNITS, A=ns["A"], PACKS=PACKS, LINEUPS=LINEUPS, PRINCIPLES=PRINCIPLES, REFS=REFS,
                   QUESTIONS=QUESTIONS, META=META, EXTRA_IMGS=EXTRA_IMGS, CAPS=CAPS, SCR=SCR, os=os, json=json)
        exec(open(p, encoding="utf-8").read(), ns2)

# ---------- derived unit fields ----------
MUL = {  # body multiplier, weak-point multiplier (None = no weak point)
    "myt_golem": (0.25, 3), "bug_spitter": (0.5, 2.5), "bot_shield": (1, 3), "bot_siege": (0.5, 3), "bot_drone": (1, 2),
    "myt_eye": (1, 3), "myt_gargoyle": (1, 1.5), "bug_spiderling": (1, None), "myt_slime": (1, None), "dino_compy": (1, None),
    "dino_trike": (0.5, 1.5), "drk_weeper": (1, None), "drk_specter": (1, None), "zom_bloater": (1, 2),
}
BOSS = ("Region boss", "Final boss", "Giant", "Miniboss")
COST_SCORE = {"S": 5, "M": 3.5, "L": 2}
PERF_SCORE = {"crowd": 5, "group": 4.5, "few": 3.5, "boss": 4}
PERF_LABEL = {"crowd": "crowd-ready", "group": "groups", "few": "a few", "boss": "one at a time"}
SRC_LABEL = {"Owned": "Owned", "SyntyPass": "Synty pass", "Buy": "Buy"}
PHASE_LABEL = {"Built": "In game", "EA1": "First EA", "Update": "Update", "Later": "Bench"}
dots = lambda n: "\u25cf" * int(round(n)) + "\u25cb" * (5 - int(round(n)))

for a in ASSETS.values():
    a["sourceLabel"] = SRC_LABEL.get(a["source"], a["source"]) + (f" ${a['price']:g}" if a["source"] == "Buy" and a.get("price") else "")
    a["rigShort"] = a["anim"].split(":")[0] if ":" in a["anim"] else a["anim"]
    a["dotsStyle"], a["dotsAnim"] = dots(a["style"]), dots(a["animq"])
    a.setdefault("imgs", [])

def shots(hp, per, mul):
    return math.ceil(hp / (per * mul)) if mul else None

for i, u in enumerate(UNITS):
    u["order"] = i
    u["phaseLabel"] = PHASE_LABEL[u["phase"]]
    u["assets"] = [k for k in u["assets"] if k in ASSETS]
    u["primary"] = u["assets"][0] if u["assets"] else None
    a = ASSETS.get(u["primary"]) if u["primary"] else None
    u["src"] = a["source"] if a else "Missing"
    gal = []
    for k in u["assets"]:
        for key in ([ASSETS[k]["img"]] + ASSETS[k]["imgs"]):
            if key and key not in gal:
                gal.append(key)
    u["gallery"] = gal
    u["thumb"] = gal[0] if gal else ""
    bm, wm = MUL.get(u["id"], (1, 2))
    hp = u["hp"]
    if u["tier"] in BOSS:
        mag = 600  # one AK magazine: 30 x 20
        u["stk"] = dict(pistol="", ak=f"{hp / mag:.1f} mags", sg="", axe="")
        weak = f" \u00b7 {hp / (mag * wm):.1f} on weak points" if wm else ""
        u["stkHtml"] = f"<b>{hp:,}</b> HP<br>{hp / mag:.1f} AK mags{weak}"
    else:
        st = dict(pistol=shots(hp, GUNS["pistol"], bm), pistolHead=shots(hp, GUNS["pistol"], wm) if wm else None,
                  ak=shots(hp, GUNS["ak"], bm), sg=shots(hp, GUNS["pump"], bm), axe=shots(hp, GUNS["axe"], bm))
        u["stk"] = dict(pistol=f"{st['pistol']}" + (f" ({st['pistolHead']} weak)" if st["pistolHead"] else ""), ak=st["ak"], sg=st["sg"], axe=st["axe"])
        head = f" \u00b7 {st['pistolHead']} weak" if st["pistolHead"] else ""
        armour = "" if bm == 1 else f"<br><span class='small'>armour x{bm:g}</span>"
        u["stkHtml"] = (f"<b>{hp}</b> HP{armour}<br>Pistol {st['pistol']}{head}<br>AK {st['ak']} \u00b7 Shotgun {st['sg']}<br>Axe {st['axe']}")
    if a:
        u["rating"] = round(0.35 * u["fit"] + 0.20 * a["style"] + 0.15 * a["animq"] + 0.15 * COST_SCORE[a["cost"]] + 0.15 * PERF_SCORE[a["perf"]], 1)
        u["dotsStyle"], u["dotsAnim"] = dots(a["style"]), dots(a["animq"])
        u["costLabel"] = {"S": "S \u00b7 reuse", "M": "M \u00b7 new parts", "L": "L \u00b7 new system"}[a["cost"]]
        u["perfLabel"] = PERF_LABEL[a["perf"]]
    else:
        u["rating"] = round(0.35 * u["fit"] + 0.65 * 2.5, 1)
        u["dotsStyle"] = u["dotsAnim"] = "?"
        u["costLabel"] = u["perfLabel"] = "?"
    u["dotsFit"] = dots(u["fit"])
    u["jobShort"] = u["raid"].split(":")[0].split(";")[0].rstrip(".")[:110]
    u.setdefault("missingNote", "")

# ---------- summary numbers ----------
RAID = ("gob", "zom", "bot", "bst", "bug", "myt", "dino", "ali")
core = [u for u in UNITS if u["pick"] == "core"]
ea1 = [u for u in UNITS if u["phase"] in ("EA1", "Built")]
def buys(units):
    seen = {}
    for u in units:
        a = ASSETS.get(u["primary"]) if u["primary"] else None
        if a and a["source"] == "Buy":
            seen[a["key"]] = a
    return seen
ea1_buy = buys(ea1)
def total(d): return sum(a["price"] for a in d.values())
by_group = lambda g: [u for u in UNITS if u["group"] == g and u["pick"] == "core"]
dino_buy, ali_buy = buys(by_group("dino")), buys(by_group("ali"))
owned_core = sum(1 for u in core if u["src"] == "Owned")
synty_packs = sum(1 for p in PACKS if p["source"] == "SyntyPass")
all_buy = [p for p in PACKS if p["source"] == "Buy"]
fmt = lambda v: f"${v:,.2f}".replace(".00", "")
META["facts"] = [
    (str(len(UNITS)), "enemies, minibosses and bosses"),
    (str(len(RAID)), "raid-group candidates (pick 5–6)"),
    (str(len(ea1)), "in the suggested first EA set"),
    (f"{owned_core}/{len(core)}", "suggested picks use art you own"),
    (fmt(total(ea1_buy)), "to buy for the first EA set"),
]
META["cost"] = [
    ("$0", f"{owned_core} of {len(core)} suggested picks use packs you own"),
    (str(synty_packs), "Synty pass packs worth downloading (free) for alternates"),
    (fmt(total(ea1_buy)), "first EA set: " + ", ".join(a["name"] for a in ea1_buy.values())),
    (fmt(total(dino_buy)), "if Dinosaurs come in (" + ", ".join(a["name"] for a in dino_buy.values()) + ")"),
    (fmt(total(ali_buy)), f"if Invaders come in ({len(ali_buy)} Meshtint singles; the $65.90 bundle is over your $60 cap)"),
    (str(len(all_buy)), "store packs reviewed in all (Buy / Maybe / Skip on each)"),
]

# ---------- images ----------
IMG = {}
def enc(im, q):
    b = io.BytesIO(); im.convert("RGB").save(b, "JPEG", quality=q, optimize=True, progressive=True)
    return "data:image/jpeg;base64," + base64.b64encode(b.getvalue()).decode()
def load_render(key, size=288):
    for d in RENDERS:
        for ext in (".png", ".jpg"):
            p = os.path.join(d, key + ext)
            if os.path.exists(p):
                im = Image.open(p).convert("RGB")
                if im.width == im.height:
                    im = im.resize((size, size), Image.LANCZOS)
                    im = ImageEnhance.Brightness(im).enhance(1.1)
                return im
    return None
def load_any(key, width):
    path = EXTRA_IMGS.get(key)
    if path and os.path.exists(path):
        try:
            im = Image.open(path).convert("RGB")
        except Exception:
            return None
        if im.width > width:
            im = im.resize((width, round(im.height * width / im.width)), Image.LANCZOS)
        return im
    return load_render(key)
def add(key, width=440, q=70):
    if not key or key in IMG:
        return
    im = load_any(key, width)
    if im is not None:
        IMG[key] = enc(im, q)

for u in UNITS:
    for k in u["gallery"]:
        add(k)
for a in ASSETS.values():
    add(a["img"])
for p in PACKS:
    add(p.get("img"), 480)
for l in LINEUPS:
    if l["img"] not in IMG:
        im = load_any(l["img"], 2000)
        if im is not None:
            IMG[l["img"]] = enc(im, 74)
for k in IMG:
    CAPS.setdefault(k, "")

DATA = dict(groups=GROUPS, roles=ROLES, units=UNITS, assets=ASSETS, packs=PACKS, lineups=LINEUPS, principles=PRINCIPLES,
            refs=REFS, questions=QUESTIONS, meta=META, cost=META.get("cost", []), caps=CAPS)
tpl = open(os.path.join(HERE, "page.html"), encoding="utf-8").read()
out = tpl.replace("/*__DATA__*/null", json.dumps(DATA, ensure_ascii=False).replace("</", "<\\/")).replace("/*__IMG__*/null", json.dumps(IMG))
dst = os.path.join(HERE, "aipocalypse-bestiary.html")
open(dst, "w", encoding="utf-8").write(out)
missing = sorted({k for u in UNITS for k in u["gallery"] if k not in IMG})
print(f"units {len(UNITS)}, assets {len(ASSETS)}, packs {len(PACKS)}, images {len(IMG)}, size {os.path.getsize(dst) / 1e6:.2f} MB")
if missing:
    print("missing images:", missing)
